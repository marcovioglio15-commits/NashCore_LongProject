using Unity.Entities;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(ProjectileSimulationSystem))]
public partial struct ProjectileDespawnSystem : ISystem
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
    public void OnUpdate(ref SystemState state)
    {
        BufferLookup<ProjectilePoolElement> poolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);

        foreach ((RefRO<Projectile> projectile,
                  RefRO<ProjectileRuntimeState> runtimeState,
                  RefRO<ProjectileOwner> owner,
                  Entity projectileEntity) in SystemAPI.Query<RefRO<Projectile>, RefRO<ProjectileRuntimeState>, RefRO<ProjectileOwner>>()
                                                      .WithAll<ProjectileActive>()
                                                      .WithEntityAccess())
        {
            bool reachedRange = projectile.ValueRO.MaxRange > 0f && runtimeState.ValueRO.TraveledDistance >= projectile.ValueRO.MaxRange;
            bool reachedLifetime = projectile.ValueRO.MaxLifetime > 0f && runtimeState.ValueRO.ElapsedLifetime >= projectile.ValueRO.MaxLifetime;

            if (reachedRange == false && reachedLifetime == false)
                continue;

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
}
#endregion
