using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// This system initializes projectile pools for shooter entities by instantiating 
/// projectile entities from a specified prefab and adding them to the pool buffer.
/// It runs after the PlayerControllerInitializeSystem to ensure that shooter entities 
/// are set up before attempting to initialize their projectile pools.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerControllerInitializeSystem))]
public partial struct ProjectilePoolInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery poolCandidateQuery;
    #endregion

    #region Nested Types
    private struct PoolInitializationRequest
    {
        public Entity ShooterEntity;
        public Entity ProjectilePrefab;
        public int InitialCapacity;
    }
    #endregion

    #region Lifecycle
    /// <summary>
    /// Configures the system to require updates for shooter entities that have a ShooterProjectilePrefab, 
    /// ProjectilePoolState, and ProjectilePoolElement buffer.
    /// </summary>
    /// <param name="state"></param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ShooterProjectilePrefab>();
        state.RequireForUpdate<ProjectilePoolState>();
        state.RequireForUpdate<ProjectilePoolElement>();

        poolCandidateQuery = SystemAPI.QueryBuilder()
            .WithAll<ShooterProjectilePrefab, ProjectilePoolState, ProjectilePoolElement>()
            .Build();
    }
    #endregion

    #region Update
    /// <summary>
    /// Initializes projectile pools for shooter entities that have not yet been initialized, expanding their capacity
    /// as needed based on configuration.
    /// </summary>
    /// <param name="state">The current system state used to access entities and components.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        int candidateCount = poolCandidateQuery.CalculateEntityCountWithoutFiltering();

        if (candidateCount <= 0)
            return;

        Allocator frameAllocator = state.WorldUpdateAllocator;
        NativeList<PoolInitializationRequest> initializationRequests = new NativeList<PoolInitializationRequest>(math.max(4, candidateCount), frameAllocator);

        // Gather initialization requests for shooter entities that have not yet initialized their projectile pools.
        foreach ((RefRW<ProjectilePoolState> poolState,
                  RefRO<ShooterProjectilePrefab> projectilePrefab,
                  DynamicBuffer<ProjectilePoolElement> _,
                  Entity shooterEntity) in SystemAPI.Query<RefRW<ProjectilePoolState>,
                                                           RefRO<ShooterProjectilePrefab>,
                                                           DynamicBuffer<ProjectilePoolElement>>().WithEntityAccess())
        {
            if (entityManager.HasComponent<EnemyActive>(shooterEntity) &&
                entityManager.IsComponentEnabled<EnemyActive>(shooterEntity) == false)
                continue;

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

        if (initializationRequests.Length <= 0)
            return;

        // Process each initialization request, expanding the projectile pool for each shooter entity as needed.
        for (int requestIndex = 0; requestIndex < initializationRequests.Length; requestIndex++)
        {
            PoolInitializationRequest request = initializationRequests[requestIndex];

            if (entityManager.Exists(request.ShooterEntity) == false)
                continue;

            if (entityManager.HasComponent<Projectile>(request.ShooterEntity))
                continue;

            if (request.InitialCapacity > 0)
                ProjectilePoolUtility.ExpandPool(entityManager, request.ShooterEntity, request.ProjectilePrefab, request.InitialCapacity);

            ProjectilePoolState poolState = entityManager.GetComponentData<ProjectilePoolState>(request.ShooterEntity);
            poolState.Initialized = 1;
            entityManager.SetComponentData(request.ShooterEntity, poolState);
        }
    }
    #endregion
}
