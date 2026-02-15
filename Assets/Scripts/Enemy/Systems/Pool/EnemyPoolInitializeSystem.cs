using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Initializes enemy pools for spawners.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup), OrderFirst = true)]
public partial struct EnemyPoolInitializeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemySpawner>();
        state.RequireForUpdate<EnemySpawnerState>();
        state.RequireForUpdate<EnemyPoolElement>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRO<EnemySpawner> spawner,
                  RefRW<EnemySpawnerState> spawnerState,
                  Entity spawnerEntity) in SystemAPI.Query<RefRO<EnemySpawner>, RefRW<EnemySpawnerState>>().WithEntityAccess())
        {
            if (spawnerState.ValueRO.Initialized != 0)
                continue;

            Entity prefabEntity = spawner.ValueRO.EnemyPrefab;

            if (prefabEntity == Entity.Null)
                continue;

            if (entityManager.Exists(prefabEntity) == false)
                continue;

            if (entityManager.HasBuffer<EnemyPoolElement>(spawnerEntity) == false)
                continue;

            int initialCapacity = math.max(0, spawner.ValueRO.InitialPoolCapacity);

            if (initialCapacity > 0)
                EnemyPoolUtility.ExpandPool(entityManager, spawnerEntity, prefabEntity, initialCapacity);

            EnemySpawnerState nextState = spawnerState.ValueRO;
            nextState.Initialized = 1;
            nextState.NextSpawnTime = elapsedTime + math.max(0f, spawner.ValueRO.InitialSpawnDelay);

            if (nextState.RandomState == 0u)
                nextState.RandomState = 1u;

            spawnerState.ValueRW = nextState;
        }
    }
    #endregion

    #endregion
}
