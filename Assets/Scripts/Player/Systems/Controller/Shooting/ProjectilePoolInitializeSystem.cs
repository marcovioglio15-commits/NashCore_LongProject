using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerControllerInitializeSystem))]
public partial struct ProjectilePoolInitializeSystem : ISystem
{
    #region Nested Types
    private struct PoolInitializationRequest
    {
        public Entity ShooterEntity;
        public Entity ProjectilePrefab;
        public int InitialCapacity;
    }
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ShooterProjectilePrefab>();
        state.RequireForUpdate<ProjectilePoolState>();
        state.RequireForUpdate<ProjectilePoolElement>();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        NativeList<PoolInitializationRequest> initializationRequests = new NativeList<PoolInitializationRequest>(Allocator.Temp);

        foreach ((RefRW<ProjectilePoolState> poolState,
                  RefRO<ShooterProjectilePrefab> projectilePrefab,
                  Entity shooterEntity) in SystemAPI.Query<RefRW<ProjectilePoolState>,
                                                           RefRO<ShooterProjectilePrefab>>().WithEntityAccess())
        {
            if (poolState.ValueRO.Initialized != 0)
                continue;

            Entity prefabEntity = projectilePrefab.ValueRO.PrefabEntity;

            if (prefabEntity == Entity.Null || entityManager.Exists(prefabEntity) == false)
                continue;

            initializationRequests.Add(new PoolInitializationRequest
            {
                ShooterEntity = shooterEntity,
                ProjectilePrefab = prefabEntity,
                InitialCapacity = math.max(0, poolState.ValueRO.InitialCapacity)
            });
        }

        for (int requestIndex = 0; requestIndex < initializationRequests.Length; requestIndex++)
        {
            PoolInitializationRequest request = initializationRequests[requestIndex];

            if (entityManager.Exists(request.ShooterEntity) == false)
                continue;

            if (entityManager.HasComponent<Projectile>(request.ShooterEntity))
                continue;

            if (entityManager.HasBuffer<ProjectilePoolElement>(request.ShooterEntity) == false)
                continue;

            if (request.InitialCapacity > 0)
                ProjectilePoolUtility.ExpandPool(entityManager, request.ShooterEntity, request.ProjectilePrefab, request.InitialCapacity);

            if (entityManager.HasComponent<ProjectilePoolState>(request.ShooterEntity))
            {
                ProjectilePoolState poolState = entityManager.GetComponentData<ProjectilePoolState>(request.ShooterEntity);
                poolState.Initialized = 1;
                entityManager.SetComponentData(request.ShooterEntity, poolState);
            }
        }

        initializationRequests.Dispose();
    }
    #endregion
}
#endregion
