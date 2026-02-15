using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Systems
/// <summary>
/// Simulates the movement and state updates of active projectiles, handling velocity, player speed inheritance, and
/// lifetime tracking within the player controller system group.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(ProjectileSpawnSystem))]
public partial struct ProjectileSimulationSystem : ISystem
{
    #region Lifecycle

    /// <summary>
    /// Configures the system state to require updates for projectile-related components 
    /// (Projectile for velocity and inheritance settings, 
    /// ProjectileRuntimeState to tracks the projectile's traveled distance and elapsed lifetime, 
    /// ProjectileOwner to identify the shooter entity for velocity inheritance, 
    /// and ProjectileActive to filter for active projectiles that should be simulated). 
    /// This ensures that the system will only run when there are relevant entities to process, 
    /// optimizing performance by avoiding unnecessary updates when no projectiles are present or active. 
    /// </summary>
    /// <param name="state">Reference to the system state to configure.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Projectile>();
        state.RequireForUpdate<ProjectileRuntimeState>();
        state.RequireForUpdate<ProjectileOwner>();
        state.RequireForUpdate<ProjectileActive>();
    }

    /// <summary>
    /// Schedules the projectile simulation job to update projectile movement in parallel.
    /// </summary>
    /// <param name="state">The current system state used to manage dependencies.</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Create the projectile simulation job,
        // passing in delta time and component lookups.
        ProjectileSimulationJob simulationJob = new ProjectileSimulationJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            GlobalTime = (float)SystemAPI.Time.ElapsedTime,
            MovementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true),
            PassiveToolsLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(true),
            PlayerWorldTransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)
        };

        // Schedule the job to run in parallel across all entities matching the query,
        state.Dependency = simulationJob.ScheduleParallel(state.Dependency);
    }

    #endregion


    #region Jobs

    /// <summary>
    /// This job Simulates the movement of active projectiles by updating their positions based on their velocities, inherited
    /// player speed, and elapsed time.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(ProjectileActive))]
    private partial struct ProjectileSimulationJob : IJobEntity
    {
        #region Fields
        public float DeltaTime;
        public float GlobalTime;
        [ReadOnly] public ComponentLookup<PlayerMovementState> MovementStateLookup;
        [ReadOnly] public ComponentLookup<PlayerPassiveToolsState> PassiveToolsLookup;
        [ReadOnly] public ComponentLookup<LocalToWorld> PlayerWorldTransformLookup;
        #endregion

        #region Methods
        #region Execute
        private void Execute(ref LocalTransform projectileTransform,
                             ref ProjectileRuntimeState runtimeState,
                             ref Projectile projectile,
                             ref ProjectilePerfectCircleState perfectCircleState,
                             in ProjectileOwner owner)
        {
            // If the projectile has the perfect circle behavior enabled
            // and the shooter has the perfect circle passive tool,
            // then attempt to simulate the perfect circle behavior for this projectile
            //(if successful quit).
            if (TrySimulatePerfectCircle(ref projectileTransform,
                                         ref runtimeState,
                                         ref projectile,
                                         ref perfectCircleState,
                                         in owner))
            {
                return;
            }

            float3 velocity = projectile.Velocity;

            // If the projectile is set to inherit player speed,
            // calculate the inherited velocity and add it to the projectile's velocity.
            if (projectile.InheritPlayerSpeed != 0)
                velocity += ResolveInheritedVelocity(projectile, owner);

            // Calculate the displacement based on the velocity and delta time,
            // then update the projectile's position.
            float3 displacement = velocity * DeltaTime;
            projectileTransform.Position += displacement;
            runtimeState.TraveledDistance += math.length(displacement);
            runtimeState.ElapsedLifetime += DeltaTime;
        }
        #endregion

        #region Perfect Circle
        private bool TrySimulatePerfectCircle(ref LocalTransform projectileTransform,
                                              ref ProjectileRuntimeState runtimeState,
                                              ref Projectile projectile,
                                              ref ProjectilePerfectCircleState perfectCircleState,
                                              in ProjectileOwner owner)
        {
            if (perfectCircleState.Enabled == 0)
                return false;

            Entity shooterEntity = owner.ShooterEntity;

            if (PassiveToolsLookup.HasComponent(shooterEntity) == false)
                return false;

            PlayerPassiveToolsState passiveToolsState = PassiveToolsLookup[shooterEntity];

            if (passiveToolsState.HasPerfectCircle == 0)
                return false;

            if (PlayerWorldTransformLookup.HasComponent(shooterEntity) == false)
                return false;

            PerfectCirclePassiveConfig perfectCircleConfig = passiveToolsState.PerfectCircle;
            float3 shooterPosition = PlayerWorldTransformLookup[shooterEntity].Position;
            float3 currentPosition = projectileTransform.Position;
            float3 entryDirection = perfectCircleState.RadialDirection;
            entryDirection.y = 0f;

            if (math.lengthsq(entryDirection) <= 1e-6f)
            {
                entryDirection = projectile.Velocity;
                entryDirection.y = 0f;
                entryDirection = math.normalizesafe(entryDirection, new float3(0f, 0f, 1f));
                perfectCircleState.RadialDirection = entryDirection;
            }

            // Calculate the minimum and maximum radius,
            // pulse frequency and phase for dynamic radius changes,
            // entry threshold for transitioning from radial entry to orbiting,
            // and radial speed for the entry phase.
            float minimumRadius = math.max(0f, perfectCircleConfig.OrbitRadiusMin);
            float maximumRadius = math.max(minimumRadius, perfectCircleConfig.OrbitRadiusMax);
            float pulseFrequency = math.max(0f, perfectCircleConfig.OrbitPulseFrequency);
            float pulsePhase = GlobalTime * pulseFrequency * (math.PI * 2f);
            float pulse = pulseFrequency > 0f ? math.sin(pulsePhase) * 0.5f + 0.5f : 1f;
            float targetRadius = math.lerp(minimumRadius, maximumRadius, pulse);
            float entryThreshold = math.max(0.05f, targetRadius * math.clamp(perfectCircleConfig.OrbitEntryRatio, 0f, 1f));
            float radialSpeed = math.max(0f, perfectCircleConfig.RadialEntrySpeed);
            float3 perfectCircleInheritedVelocity = ResolveFullInheritedVelocity(owner);

            // If the projectile has not yet entered orbit, move it in a straight line along the entry direction.
            if (perfectCircleState.HasEnteredOrbit == 0)
            {
                float entryStepDistance = radialSpeed * DeltaTime;
                runtimeState.TraveledDistance += entryStepDistance;
                runtimeState.ElapsedLifetime += DeltaTime;
                perfectCircleState.EntryOrigin += perfectCircleInheritedVelocity * DeltaTime;
                float traveledEntryDistance = runtimeState.TraveledDistance;
                float3 entryPosition = perfectCircleState.EntryOrigin + entryDirection * traveledEntryDistance;
                entryPosition.y = perfectCircleState.EntryOrigin.y + perfectCircleConfig.HeightOffset;
                projectileTransform.Position = entryPosition;
                projectile.Velocity = entryDirection * radialSpeed + perfectCircleInheritedVelocity;
                perfectCircleState.RadialDirection = entryDirection;

                // If the projectile has not yet reached the entry threshold distance, continue moving it along the entry direction.
                if (traveledEntryDistance < entryThreshold)
                    return true;

                //else the projectile should transition to orbiting around the shooter.
                float3 entryOffset = entryPosition - shooterPosition;
                entryOffset.y = 0f;
                float entryRadius = math.length(entryOffset);
                float3 orbitEntryDirection = entryRadius > 1e-6f
                    ? entryOffset / entryRadius
                    : entryDirection;
                perfectCircleState.HasEnteredOrbit = 1;
                perfectCircleState.OrbitBlendProgress = 1f;
                perfectCircleState.CurrentRadius = math.max(0.05f, entryRadius);
                perfectCircleState.OrbitAngle = math.atan2(orbitEntryDirection.z, orbitEntryDirection.x);
                perfectCircleState.AccumulatedOrbitRadians = 0f;
                perfectCircleState.CompletedFullOrbit = 0;
                currentPosition = entryPosition;
            }

            // Once in orbit, calculate the new orbit radius based on the target radius and any dynamic pulsing,
            // then update the orbit angle based on the orbital speed and delta time.
            float orbitRadius = targetRadius;
            perfectCircleState.CurrentRadius = orbitRadius;
            float angularSpeed = orbitRadius > 0.001f ? math.max(0f, perfectCircleConfig.OrbitalSpeed) / orbitRadius : 0f;
            float angularStep = angularSpeed * DeltaTime;
            perfectCircleState.OrbitAngle += angularStep;

            // If the projectile has not yet completed a full orbit,
            // accumulate the orbit angle to track how much of the orbit has been completed.
            if (perfectCircleState.CompletedFullOrbit == 0)
            {
                perfectCircleState.AccumulatedOrbitRadians += math.abs(angularStep);

                if (perfectCircleState.AccumulatedOrbitRadians >= math.PI * 2f)
                    perfectCircleState.CompletedFullOrbit = 1;
            }

            // Calculate the new position on the orbit based on the current orbit angle and radius,
            // then update the projectile's position and velocity to maintain the orbit around the shooter.
            float cos = math.cos(perfectCircleState.OrbitAngle);
            float sin = math.sin(perfectCircleState.OrbitAngle);
            float3 orbitOffset = new float3(cos * orbitRadius, 0f, sin * orbitRadius);
            float3 orbitPosition = shooterPosition + orbitOffset;
            orbitPosition.y = shooterPosition.y + perfectCircleConfig.HeightOffset;
            float3 orbitDisplacement = orbitPosition - currentPosition;
            projectileTransform.Position = orbitPosition;
            runtimeState.TraveledDistance += math.length(orbitDisplacement);
            runtimeState.ElapsedLifetime += DeltaTime;
            float3 tangent = math.normalizesafe(new float3(-orbitOffset.z, 0f, orbitOffset.x), new float3(1f, 0f, 0f));
            float3 orbitalVelocity = tangent * math.max(0f, perfectCircleConfig.OrbitalSpeed) + perfectCircleInheritedVelocity;

            projectile.Velocity = orbitalVelocity;
            perfectCircleState.RadialDirection = entryDirection;
            return true;
        }
        #endregion


        #region Inherited Velocity
        /// <summary>
        /// Calculates the Velocity inherited by a projectile from its shooter, excluding any component in the direction
        /// of the projectile's own velocity.
        /// </summary>
        /// <param name="projectile">The projectile whose inherited velocity is being resolved.</param>
        /// <param name="owner">The owner of the projectile (shooter entity).</param>
        /// <returns>The inherited velocity vector for the projectile.</returns>
        private float3 ResolveInheritedVelocity(in Projectile projectile, in ProjectileOwner owner)
        {
            // If the shooter entity does not have a PlayerMovementState component, return zero velocity.
            if (MovementStateLookup.HasComponent(owner.ShooterEntity) == false)
                return float3.zero;

            // Get the shooter's full movement velocity (including vertical component).
            float3 inheritedVelocity = MovementStateLookup[owner.ShooterEntity].Velocity;

            // Remove any component of the inherited velocity that is in the direction
            // of the projectile's own velocity.
            float speedSquared = math.lengthsq(projectile.Velocity); // Calculate the squared speed of the projectile to use for projection.
            // Only perform the projection if the projectile has a non-negligible speed to avoid division by zero.
            if (speedSquared > 1e-6f)
            {
                // Calculate the projection of the inherited velocity onto the projectile's velocity and subtract it from the inherited velocity.
                float projectionScale = math.dot(inheritedVelocity, projectile.Velocity) / speedSquared;
                inheritedVelocity -= projectile.Velocity * projectionScale;
            }

            return inheritedVelocity; // Return the final inherited velocity after removing the component in the direction of the projectile's velocity.
        }

        private float3 ResolveFullInheritedVelocity(in ProjectileOwner owner)
        {
            // If the shooter entity does not have a PlayerMovementState component, return zero velocity.
            if (MovementStateLookup.HasComponent(owner.ShooterEntity) == false)
                return float3.zero;

            return MovementStateLookup[owner.ShooterEntity].Velocity;
        }
        #endregion
        #endregion
    }
    #endregion
}
#endregion
