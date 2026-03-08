using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Builds and initializes experience drop pools from enemy drop configurations.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup), OrderFirst = true)]
public partial struct EnemyExperienceDropPoolInitializeSystem : ISystem
{
    #region Constants
    private const int MaxDropPoolPrewarmEntitiesPerFrame = 1024;
    private const int MaxDropPoolPrewarmEntitiesPerPoolPerFrame = 256;
    #endregion

    #region Fields
    private EntityQuery registryQuery;
    private EntityQuery spawnerQuery;
    private byte poolSettingsApplied;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates registry singleton dependencies required by drop pooling.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnCreate(ref SystemState state)
    {
        registryQuery = new EntityQueryBuilder(Allocator.Temp)
                            .WithAll<EnemyExperienceDropPoolRegistry>()
                            .Build(ref state);
        spawnerQuery = new EntityQueryBuilder(Allocator.Temp)
                           .WithAll<EnemySpawner>()
                           .Build(ref state);
        poolSettingsApplied = 0;
        state.RequireForUpdate<EnemySpawner>();
        EnsureRegistrySingleton(ref state);

        if (TryGetRegistryEntity(out Entity registryEntity))
        {
            EnemyExperienceDropPoolRegistry registry = state.EntityManager.GetComponentData<EnemyExperienceDropPoolRegistry>(registryEntity);

            if (registry.Initialized != 0)
                state.Enabled = false;
        }
    }

    /// <summary>
    /// Initializes drop pools once by aggregating prefab demand from all enemy spawners.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnUpdate(ref SystemState state)
    {
        EnsureRegistrySingleton(ref state);

        if (TryGetRegistryEntity(out Entity registryEntity) == false)
            return;

        EnemyExperienceDropPoolRegistry registry = state.EntityManager.GetComponentData<EnemyExperienceDropPoolRegistry>(registryEntity);

        if (registry.Initialized != 0)
        {
            state.Enabled = false;
            return;
        }

        if (poolSettingsApplied == 0)
        {
            int spawnerCount = spawnerQuery.CalculateEntityCountWithoutFiltering();
            int dictionaryCapacity = math.max(4, spawnerCount * 2);
            Dictionary<Entity, PoolBuildSettings> poolSettingsByPrefab = new Dictionary<Entity, PoolBuildSettings>(dictionaryCapacity);

            foreach (RefRO<EnemySpawner> spawner in SystemAPI.Query<RefRO<EnemySpawner>>())
                AggregateSpawnerPoolSettings(state.EntityManager, spawner.ValueRO, poolSettingsByPrefab);

            ApplyPoolSettings(state.EntityManager, registryEntity, poolSettingsByPrefab);
            poolSettingsApplied = 1;
        }

        if (InitializePools(state.EntityManager, registryEntity, MaxDropPoolPrewarmEntitiesPerFrame) == false)
            return;

        registry.Initialized = 1;
        state.EntityManager.SetComponentData(registryEntity, registry);
        state.Enabled = false;
    }
    #endregion

    #region Setup
    /// <summary>
    /// Ensures that a singleton registry entity for experience drop pools exists.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    private void EnsureRegistrySingleton(ref SystemState state)
    {
        if (registryQuery.IsEmptyIgnoreFilter == false)
            return;

        Entity registryEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(registryEntity, new EnemyExperienceDropPoolRegistry
        {
            Initialized = 0
        });
        state.EntityManager.AddBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);
    }

    /// <summary>
    /// Tries to resolve the singleton registry entity from the cached registry query.
    /// </summary>
    /// <param name="registryEntity">Resolved registry entity when present; Entity.Null otherwise.</param>
    /// <returns>True when the registry exists; otherwise false.</returns>
    private bool TryGetRegistryEntity(out Entity registryEntity)
    {
        if (registryQuery.IsEmptyIgnoreFilter)
        {
            registryEntity = Entity.Null;
            return false;
        }

        registryEntity = registryQuery.GetSingletonEntity();
        return true;
    }
    #endregion

    #region Aggregation
    /// <summary>
    /// Aggregates pool sizing requirements from a single spawner into per-prefab build settings.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read spawner and prefab data.</param>
    /// <param name="spawner">Spawner source configuration.</param>
    /// <param name="poolSettingsByPrefab">Accumulated pool build settings grouped by prefab entity.</param>
    /// <returns>Void.</returns>
    private static void AggregateSpawnerPoolSettings(EntityManager entityManager,
                                                     EnemySpawner spawner,
                                                     Dictionary<Entity, PoolBuildSettings> poolSettingsByPrefab)
    {
        if (spawner.EnemyPrefab == Entity.Null)
            return;

        if (entityManager.Exists(spawner.EnemyPrefab) == false)
            return;

        if (entityManager.HasComponent<EnemyDropItemsConfig>(spawner.EnemyPrefab) == false)
            return;

        EnemyDropItemsConfig dropItemsConfig = entityManager.GetComponentData<EnemyDropItemsConfig>(spawner.EnemyPrefab);

        if (dropItemsConfig.PayloadKind != EnemyDropItemsPayloadKind.Experience)
            return;

        if (dropItemsConfig.MaximumTotalExperienceDrop <= 0f)
            return;

        if (entityManager.HasBuffer<EnemyExperienceDropDefinitionElement>(spawner.EnemyPrefab) == false)
            return;

        DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions = entityManager.GetBuffer<EnemyExperienceDropDefinitionElement>(spawner.EnemyPrefab);

        if (definitions.Length <= 0)
            return;

        int estimatedDropsPerDeath = math.max(1, dropItemsConfig.EstimatedDropsPerDeath);

        if (dropItemsConfig.EstimatedDropsPerDeath <= 0)
        {
            float deliveredExperience;
            float absoluteError;
            estimatedDropsPerDeath = EnemyExperienceDropDistributionUtility.EstimateDropsPerDeath(definitions,
                                                                                                   dropItemsConfig.MaximumTotalExperienceDrop,
                                                                                                   dropItemsConfig.Distribution,
                                                                                                   out deliveredExperience,
                                                                                                   out absoluteError);
            estimatedDropsPerDeath = math.max(1, estimatedDropsPerDeath);
        }

        int initialCapacityPerPrefab = EstimateInitialCapacity(spawner.MaxAliveCount, estimatedDropsPerDeath);
        int expandBatchPerPrefab = EstimateExpandBatch(spawner.SpawnPerTick, estimatedDropsPerDeath);

        for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[definitionIndex];

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (definition.ExperienceAmount <= 0f)
                continue;

            PoolBuildSettings currentSettings;

            if (poolSettingsByPrefab.TryGetValue(definition.PrefabEntity, out currentSettings))
            {
                currentSettings.InitialCapacity += initialCapacityPerPrefab;

                if (expandBatchPerPrefab > currentSettings.ExpandBatch)
                    currentSettings.ExpandBatch = expandBatchPerPrefab;

                poolSettingsByPrefab[definition.PrefabEntity] = currentSettings;
                continue;
            }

            poolSettingsByPrefab[definition.PrefabEntity] = new PoolBuildSettings
            {
                InitialCapacity = initialCapacityPerPrefab,
                ExpandBatch = expandBatchPerPrefab
            };
        }
    }

    /// <summary>
    /// Computes the initial pool capacity for a prefab from max alive enemies and expected drop count.
    /// </summary>
    /// <param name="maxAliveCount">Maximum alive count configured on the spawner.</param>
    /// <param name="estimatedDropsPerDeath">Estimated drop instances generated per enemy death.</param>
    /// <returns>Resolved initial pool capacity clamped to a safe runtime range.</returns>
    private static int EstimateInitialCapacity(int maxAliveCount, int estimatedDropsPerDeath)
    {
        int sanitizedMaxAliveCount = math.max(1, maxAliveCount);
        int sanitizedEstimatedDrops = math.max(1, estimatedDropsPerDeath);
        float baseCapacity = sanitizedMaxAliveCount * sanitizedEstimatedDrops;
        float safetyCapacity = baseCapacity * 0.1f;
        int resolvedCapacity = (int)math.ceil(baseCapacity + safetyCapacity) + 4;
        return math.clamp(math.max(4, resolvedCapacity), 4, 262144);
    }

    /// <summary>
    /// Computes the pool expansion batch size from spawn burst size and expected drop count.
    /// </summary>
    /// <param name="spawnPerTick">Spawn burst size configured on the spawner.</param>
    /// <param name="estimatedDropsPerDeath">Estimated drop instances generated per enemy death.</param>
    /// <returns>Resolved expansion batch size clamped to a safe runtime range.</returns>
    private static int EstimateExpandBatch(int spawnPerTick, int estimatedDropsPerDeath)
    {
        int sanitizedSpawnPerTick = math.max(1, spawnPerTick);
        int sanitizedEstimatedDrops = math.max(1, estimatedDropsPerDeath);
        float baseBatch = sanitizedSpawnPerTick * sanitizedEstimatedDrops * 0.5f;
        int resolvedBatch = (int)math.ceil(baseBatch) + 2;
        return math.clamp(math.max(4, resolvedBatch), 4, 16384);
    }
    #endregion

    #region Pool Setup
    /// <summary>
    /// Applies aggregated pool settings by updating existing pools or creating new pool entities.
    /// </summary>
    /// <param name="entityManager">Entity manager used to mutate pool entities.</param>
    /// <param name="registryEntity">Registry singleton entity that stores the pool map buffer.</param>
    /// <param name="poolSettingsByPrefab">Resolved pool sizing settings grouped by prefab entity.</param>
    /// <returns>Void.</returns>
    private static void ApplyPoolSettings(EntityManager entityManager,
                                          Entity registryEntity,
                                          Dictionary<Entity, PoolBuildSettings> poolSettingsByPrefab)
    {
        foreach (KeyValuePair<Entity, PoolBuildSettings> pair in poolSettingsByPrefab)
        {
            Entity prefabEntity = pair.Key;
            PoolBuildSettings settings = pair.Value;
            DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);
            Entity poolEntity;

            if (EnemyExperienceDropPoolUtility.TryResolvePoolEntity(poolMap, prefabEntity, out poolEntity))
            {
                if (poolEntity == Entity.Null || entityManager.Exists(poolEntity) == false)
                    continue;

                if (entityManager.HasComponent<EnemyExperienceDropPoolState>(poolEntity) == false)
                    continue;

                EnemyExperienceDropPoolState poolState = entityManager.GetComponentData<EnemyExperienceDropPoolState>(poolEntity);
                poolState.InitialCapacity = math.max(poolState.InitialCapacity, settings.InitialCapacity);
                poolState.ExpandBatch = math.max(poolState.ExpandBatch, settings.ExpandBatch);
                entityManager.SetComponentData(poolEntity, poolState);
                continue;
            }

            Entity createdPoolEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(createdPoolEntity, new EnemyExperienceDropPoolState
            {
                PrefabEntity = prefabEntity,
                InitialCapacity = math.max(0, settings.InitialCapacity),
                ExpandBatch = math.max(1, settings.ExpandBatch),
                Initialized = 0
            });
            entityManager.AddBuffer<EnemyExperienceDropPoolElement>(createdPoolEntity);
            DynamicBuffer<EnemyExperienceDropPoolMapElement> refreshedPoolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);
            refreshedPoolMap.Add(new EnemyExperienceDropPoolMapElement
            {
                PrefabEntity = prefabEntity,
                PoolEntity = createdPoolEntity
            });
        }
    }

    /// <summary>
    /// Performs progressive prewarm expansion for non-initialized pool entities in the registry map.
    /// </summary>
    /// <param name="entityManager">Entity manager used to read and mutate pool entities.</param>
    /// <param name="registryEntity">Registry singleton entity that stores the pool map buffer.</param>
    /// <param name="frameBudget">Maximum pooled instances that can be prewarmed in the current frame.</param>
    /// <returns>True when all pools are initialized; otherwise false.</returns>
    private static bool InitializePools(EntityManager entityManager, Entity registryEntity, int frameBudget)
    {
        int remainingBudget = math.max(0, frameBudget);
        int poolMapLength = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity).Length;

        for (int mapIndex = 0; mapIndex < poolMapLength; mapIndex++)
        {
            DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);

            if (mapIndex >= poolMap.Length)
                break;

            EnemyExperienceDropPoolMapElement mapElement = poolMap[mapIndex];
            Entity poolEntity = mapElement.PoolEntity;

            if (poolEntity == Entity.Null || entityManager.Exists(poolEntity) == false)
                continue;

            if (entityManager.HasComponent<EnemyExperienceDropPoolState>(poolEntity) == false)
                continue;

            EnemyExperienceDropPoolState poolState = entityManager.GetComponentData<EnemyExperienceDropPoolState>(poolEntity);

            if (poolState.Initialized != 0)
                continue;

            int initialCapacity = math.max(0, poolState.InitialCapacity);
            int currentCapacity = 0;

            if (entityManager.HasBuffer<EnemyExperienceDropPoolElement>(poolEntity))
                currentCapacity = entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity).Length;

            int remainingCapacity = initialCapacity - currentCapacity;

            if (remainingCapacity > 0)
            {
                if (remainingBudget <= 0)
                    break;

                int perPoolBudget = math.min(MaxDropPoolPrewarmEntitiesPerPoolPerFrame, remainingBudget);
                int expandCount = math.min(remainingCapacity, perPoolBudget);

                if (poolState.ExpandBatch > 0)
                    expandCount = math.min(expandCount, poolState.ExpandBatch);

                if (expandCount > 0)
                {
                    EnemyExperienceDropPoolUtility.ExpandPool(entityManager, poolEntity, poolState.PrefabEntity, expandCount);
                    remainingBudget -= expandCount;
                }

                int refreshedCapacity = entityManager.GetBuffer<EnemyExperienceDropPoolElement>(poolEntity).Length;
                remainingCapacity = initialCapacity - refreshedCapacity;

                if (remainingCapacity > 0)
                    continue;
            }

            poolState.Initialized = 1;
            entityManager.SetComponentData(poolEntity, poolState);
        }

        return AreAllPoolsInitialized(entityManager, registryEntity);
    }

    private static bool AreAllPoolsInitialized(EntityManager entityManager, Entity registryEntity)
    {
        if (entityManager.HasBuffer<EnemyExperienceDropPoolMapElement>(registryEntity) == false)
            return true;

        DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);

        for (int mapIndex = 0; mapIndex < poolMap.Length; mapIndex++)
        {
            Entity poolEntity = poolMap[mapIndex].PoolEntity;

            if (poolEntity == Entity.Null || entityManager.Exists(poolEntity) == false)
                continue;

            if (entityManager.HasComponent<EnemyExperienceDropPoolState>(poolEntity) == false)
                continue;

            if (entityManager.GetComponentData<EnemyExperienceDropPoolState>(poolEntity).Initialized == 0)
                return false;
        }

        return true;
    }
    #endregion

    #endregion

    #region Nested Types
    private struct PoolBuildSettings
    {
        public int InitialCapacity;
        public int ExpandBatch;
    }
    #endregion
}
