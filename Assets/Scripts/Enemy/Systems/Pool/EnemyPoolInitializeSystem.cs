using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Initializes enemy pools for spawners.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup), OrderFirst = true)]
public partial struct EnemyPoolInitializeSystem : ISystem
{
    #region Constants
    private const int MaxInitialPoolEntitiesPerFrame = 1024;
    private const int MaxInitialPoolEntitiesPerSpawnerPerFrame = 256;
    #endregion

    #region Fields
    private EntityQuery initializeQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        initializeQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemySpawner, EnemySpawnerState, EnemyPoolElement>()
            .Build(ref state);
        state.RequireForUpdate(initializeQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        int remainingInitializationBudget = MaxInitialPoolEntitiesPerFrame;
        int pendingInitializationCount = 0;
        NativeArray<Entity> spawnerEntities = initializeQuery.ToEntityArray(Allocator.Temp);

        try
        {
            for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
            {
                Entity spawnerEntity = spawnerEntities[spawnerIndex];

                if (entityManager.Exists(spawnerEntity) == false)
                    continue;

                EnemySpawnerState spawnerState = entityManager.GetComponentData<EnemySpawnerState>(spawnerEntity);

                if (spawnerState.Initialized != 0)
                    continue;

                EnemySpawner spawner = entityManager.GetComponentData<EnemySpawner>(spawnerEntity);
                Entity prefabEntity = spawner.EnemyPrefab;

                if (prefabEntity == Entity.Null)
                {
                    pendingInitializationCount++;
                    continue;
                }

                if (entityManager.Exists(prefabEntity) == false)
                {
                    pendingInitializationCount++;
                    continue;
                }

                if (entityManager.HasBuffer<EnemyPoolElement>(spawnerEntity) == false)
                {
                    pendingInitializationCount++;
                    continue;
                }

                int initialCapacity = math.max(0, spawner.InitialPoolCapacity);
                DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(spawnerEntity);
                int remainingPoolCapacity = initialCapacity - poolBuffer.Length;

                if (remainingPoolCapacity > 0)
                {
                    if (remainingInitializationBudget <= 0)
                    {
                        pendingInitializationCount++;
                        continue;
                    }

                    int cappedExpandCount = math.min(remainingPoolCapacity, MaxInitialPoolEntitiesPerSpawnerPerFrame);
                    int expandCount = math.min(cappedExpandCount, remainingInitializationBudget);

                    if (expandCount <= 0)
                    {
                        pendingInitializationCount++;
                        continue;
                    }

                    EnemyPoolUtility.ExpandPool(entityManager, spawnerEntity, prefabEntity, expandCount);
                    remainingInitializationBudget -= expandCount;

                    int refreshedPoolCount = entityManager.GetBuffer<EnemyPoolElement>(spawnerEntity).Length;
                    remainingPoolCapacity = initialCapacity - refreshedPoolCount;

                    if (remainingPoolCapacity > 0)
                    {
                        pendingInitializationCount++;
                        continue;
                    }
                }

                FinalizeSpawnerInitialization(entityManager, spawnerEntity, spawner, spawnerState, elapsedTime);
            }
        }
        finally
        {
            if (spawnerEntities.IsCreated)
                spawnerEntities.Dispose();
        }

        if (pendingInitializationCount <= 0)
            state.Enabled = false;
    }
    #endregion

    #region Helpers
    private static void FinalizeSpawnerInitialization(EntityManager entityManager,
                                                      Entity spawnerEntity,
                                                      EnemySpawner spawner,
                                                      EnemySpawnerState spawnerState,
                                                      float elapsedTime)
    {
        EnemySpawnerState nextState = spawnerState;
        nextState.Initialized = 1;
        nextState.NextSpawnTime = elapsedTime + math.max(0f, spawner.InitialSpawnDelay);

        if (nextState.RandomState == 0u)
            nextState.RandomState = 1u;

        entityManager.SetComponentData(spawnerEntity, nextState);
    }
    #endregion

    #endregion
}
