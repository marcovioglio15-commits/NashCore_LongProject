using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(ProjectileSpawnSystem))]
public partial struct ProjectileSimulationSystem : ISystem
{
    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Projectile>();
        state.RequireForUpdate<ProjectileRuntimeState>();
        state.RequireForUpdate<ProjectileOwner>();
        state.RequireForUpdate<ProjectileActive>();
    }
    #endregion

    #region Update
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ProjectileSimulationJob simulationJob = new ProjectileSimulationJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            MovementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true)
        };

        state.Dependency = simulationJob.ScheduleParallel(state.Dependency);
    }
    #endregion

    #region Jobs
    [BurstCompile]
    [WithAll(typeof(ProjectileActive))]
    private partial struct ProjectileSimulationJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public ComponentLookup<PlayerMovementState> MovementStateLookup;

        private void Execute(ref LocalTransform projectileTransform, ref ProjectileRuntimeState runtimeState, in Projectile projectile, in ProjectileOwner owner)
        {
            float3 velocity = projectile.Velocity;

            if (projectile.InheritPlayerSpeed != 0)
                velocity += ResolveInheritedVelocity(projectile, owner);

            float3 displacement = velocity * DeltaTime;
            projectileTransform.Position += displacement;
            runtimeState.TraveledDistance += math.length(displacement);
            runtimeState.ElapsedLifetime += DeltaTime;
        }

        private float3 ResolveInheritedVelocity(in Projectile projectile, in ProjectileOwner owner)
        {
            if (MovementStateLookup.HasComponent(owner.ShooterEntity) == false)
                return float3.zero;

            float3 inheritedVelocity = MovementStateLookup[owner.ShooterEntity].Velocity;
            inheritedVelocity.y = 0f;

            float speedSquared = math.lengthsq(projectile.Velocity);

            if (speedSquared > 1e-6f)
            {
                float projectionScale = math.dot(inheritedVelocity, projectile.Velocity) / speedSquared;
                inheritedVelocity -= projectile.Velocity * projectionScale;
            }

            return inheritedVelocity;
        }
    }
    #endregion
}
#endregion
