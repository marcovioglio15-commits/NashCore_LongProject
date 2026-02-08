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
    /// Configures the system state to require updates for projectile-related components (Projectile because it contains the velocity and inheritance settings, 
    /// but also ProjectileRuntimeState, which tracks the projectile's traveled distance and elapsed lifetime, 
    /// ProjectileOwner to identify the shooter entity for velocity inheritance, 
    /// and ProjectileActive to filter for only active projectiles that should be simulated). 
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
            MovementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true)
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
        public float DeltaTime;
        [ReadOnly] public ComponentLookup<PlayerMovementState> MovementStateLookup;

        private void Execute(ref LocalTransform projectileTransform, ref ProjectileRuntimeState runtimeState, in Projectile projectile, in ProjectileOwner owner)
        {
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

            // Get the shooter's movement velocity and zero out the vertical component.
            float3 inheritedVelocity = MovementStateLookup[owner.ShooterEntity].Velocity;
            inheritedVelocity.y = 0f;

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
    }
    #endregion
}
#endregion
