using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// This system handles the despawning of projectile entities when they exceed their maximum range 
/// or lifetime. It runs after the ProjectileSimulationSystem to ensure that projectile movement 
/// and state updates have been processed before checking for despawn conditions. 
/// When a projectile is despawned, 
/// it is parked and returned to the shooter's projectile pool for reuse.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(ProjectileSimulationSystem))]
public partial struct ProjectileDespawnSystem : ISystem
{
    #region Lifecycle
    /// <summary>
    /// Configures the system to require updates for entities that have the Projectile,
    /// ProjectileRuntimeState, and ProjectileOwner components, as well as the ProjectileActive tag component. 
    /// This ensures that the system
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Projectile>();
        state.RequireForUpdate<ProjectileRuntimeState>();
        state.RequireForUpdate<ProjectileOwner>();
        state.RequireForUpdate<ProjectileSplitState>();
        state.RequireForUpdate<ProjectilePerfectCircleState>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<ProjectileActive>();
    }

    /// <summary>
    /// updates the system by iterating through all active projectile entities 
    /// and checking if they have exceeded their maximum range or lifetime.
    /// </summary>
    /// <param name="state"></param>
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<ProjectilePoolElement> poolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<ShootRequest> shootRequestLookup = SystemAPI.GetBufferLookup<ShootRequest>(false);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);

        foreach ((RefRO<Projectile> projectile,
                  RefRO<ProjectileRuntimeState> runtimeState,
                  RefRO<ProjectilePerfectCircleState> perfectCircleState,
                  RefRO<ProjectileElementalPayload> elementalPayload,
                  RefRW<ProjectileSplitState> splitState,
                  RefRO<LocalTransform> projectileTransform,
                  RefRO<ProjectileOwner> owner,
                  Entity projectileEntity) in SystemAPI.Query<RefRO<Projectile>, RefRO<ProjectileRuntimeState>, RefRO<ProjectilePerfectCircleState>, RefRO<ProjectileElementalPayload>, RefRW<ProjectileSplitState>, RefRO<LocalTransform>, RefRO<ProjectileOwner>>()
                                                      .WithAll<ProjectileActive>()
                                                      .WithEntityAccess())
        {
            bool reachedRange = projectile.ValueRO.MaxRange > 0f && runtimeState.ValueRO.TraveledDistance >= projectile.ValueRO.MaxRange;
            bool reachedLifetime = projectile.ValueRO.MaxLifetime > 0f && runtimeState.ValueRO.ElapsedLifetime >= projectile.ValueRO.MaxLifetime;
            bool isPerfectCircleProjectile = perfectCircleState.ValueRO.Enabled != 0;
            bool completedOrbit = isPerfectCircleProjectile &&
                                  perfectCircleState.ValueRO.HasEnteredOrbit != 0 &&
                                  perfectCircleState.ValueRO.CompletedFullOrbit != 0;

            if (isPerfectCircleProjectile)
            {
                if (completedOrbit == false)
                    continue;
            }
            else if (reachedRange == false && reachedLifetime == false)
                continue;

            if (ProjectileSplitUtility.ShouldSplitOnDespawn(in splitState.ValueRO))
            {
                float currentScaleMultiplier = ResolveCurrentScaleMultiplier(projectileEntity,
                                                                            projectileTransform.ValueRO.Scale,
                                                                            in projectileBaseScaleLookup);
                ProjectileSplitUtility.TryEnqueueSplitRequests(in projectile.ValueRO,
                                                               in splitState.ValueRO,
                                                               in projectileTransform.ValueRO,
                                                               currentScaleMultiplier,
                                                               in elementalPayload.ValueRO,
                                                               in owner.ValueRO,
                                                               ref shootRequestLookup);
                splitState.ValueRW.CanSplit = 0;
            }

            ProjectilePoolUtility.SetProjectileParked(state.EntityManager, projectileEntity);
            state.EntityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);

            Entity shooterEntity = owner.ValueRO.ShooterEntity;

            if (poolLookup.HasBuffer(shooterEntity) == false)
                continue;

            DynamicBuffer<ProjectilePoolElement> shooterPool = poolLookup[shooterEntity];
            shooterPool.Add(new ProjectilePoolElement
            {
                ProjectileEntity = projectileEntity
            });
        }
    }
    #endregion

    #region Helpers
    private static float ResolveCurrentScaleMultiplier(Entity projectileEntity,
                                                       float currentScale,
                                                       in ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup)
    {
        if (projectileBaseScaleLookup.HasComponent(projectileEntity) == false)
            return math.max(0.01f, currentScale);

        float baseScale = math.max(0.0001f, projectileBaseScaleLookup[projectileEntity].Value);
        return math.max(0.01f, currentScale / baseScale);
    }
    #endregion


}
