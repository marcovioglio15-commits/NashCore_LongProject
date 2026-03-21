using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Consumes baked wave events and activates pooled enemies when their scheduled time becomes due.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyPoolInitializeSystem))]
public partial struct EnemySpawnSystem : ISystem
{
    #region Fields
    private EntityQuery spawnerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the query used to iterate initialized wave spawners.
    /// /params state: Current ECS system state.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        spawnerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemySpawner,
                     EnemySpawnerState,
                     EnemySpawnerWaveDefinitionElement,
                     EnemySpawnerWaveRuntimeElement,
                     EnemySpawnerWaveEventElement,
                     EnemySpawnerPrefabPoolMapElement,
                     LocalToWorld>()
            .Build(ref state);
        state.RequireForUpdate(spawnerQuery);
    }

    /// <summary>
    /// Schedules wave starts, emits due events and activates pooled enemies at their baked positions.
    /// /params state: Current ECS system state.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        NativeArray<Entity> spawnerEntities = spawnerQuery.ToEntityArray(Allocator.Temp);

        try
        {
            for (int spawnerIndex = 0; spawnerIndex < spawnerEntities.Length; spawnerIndex++)
                ProcessSpawner(entityManager, spawnerEntities[spawnerIndex], elapsedTime);
        }
        finally
        {
            if (spawnerEntities.IsCreated)
                spawnerEntities.Dispose();
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Processes one spawner by updating wave scheduling and consuming due events.
    /// /params entityManager: Entity manager used to access components and buffers.
    /// /params spawnerEntity: Spawner entity being processed.
    /// /params elapsedTime: Current elapsed world time.
    /// /returns None.
    /// </summary>
    private static void ProcessSpawner(EntityManager entityManager, Entity spawnerEntity, float elapsedTime)
    {
        if (!entityManager.Exists(spawnerEntity))
            return;

        EnemySpawnerState spawnerState = entityManager.GetComponentData<EnemySpawnerState>(spawnerEntity);

        if (spawnerState.Initialized == 0)
            return;

        DynamicBuffer<EnemySpawnerWaveDefinitionElement> waveDefinitions = entityManager.GetBuffer<EnemySpawnerWaveDefinitionElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntime = entityManager.GetBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWaveEventElement> waveEvents = entityManager.GetBuffer<EnemySpawnerWaveEventElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);
        float4x4 localToWorld = entityManager.GetComponentData<LocalToWorld>(spawnerEntity).Value;

        for (int waveIndex = 0; waveIndex < waveDefinitions.Length; waveIndex++)
        {
            EnemySpawnerWaveDefinitionElement definition = waveDefinitions[waveIndex];
            EnemySpawnerWaveRuntimeElement runtime = waveRuntime[waveIndex];

            if (runtime.Completed != 0)
                continue;

            TryScheduleWaveStart(spawnerState, waveDefinitions, waveRuntime, waveIndex, ref runtime);
            TryStartWave(elapsedTime, definition, ref runtime);

            if (runtime.Started != 0 && runtime.SpawnFinished == 0)
                SpawnDueEvents(entityManager,
                               spawnerEntity,
                               poolMap,
                               waveEvents,
                               localToWorld,
                               elapsedTime,
                               definition,
                               ref runtime,
                               ref spawnerState);

            TryFinalizeWave(elapsedTime, definition, ref runtime);
            waveRuntime[waveIndex] = runtime;
        }

        entityManager.SetComponentData(spawnerEntity, spawnerState);
    }

    /// <summary>
    /// Assigns a deterministic scheduled start time to a wave as soon as its prerequisite state is available.
    /// /params spawnerState: Global mutable spawner state.
    /// /params waveDefinitions: Immutable wave definition buffer.
    /// /params waveRuntime: Mutable wave runtime buffer.
    /// /params waveIndex: Wave index to schedule.
    /// /params runtime: Current wave runtime state to update.
    /// /returns None.
    /// </summary>
    private static void TryScheduleWaveStart(EnemySpawnerState spawnerState,
                                             DynamicBuffer<EnemySpawnerWaveDefinitionElement> waveDefinitions,
                                             DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntime,
                                             int waveIndex,
                                             ref EnemySpawnerWaveRuntimeElement runtime)
    {
        if (runtime.StartScheduled != 0)
            return;

        EnemySpawnerWaveDefinitionElement definition = waveDefinitions[waveIndex];
        float referenceTime = spawnerState.StartTime;

        if (waveIndex > 0)
        {
            EnemySpawnerWaveRuntimeElement previousWaveRuntime = waveRuntime[waveIndex - 1];
            EnemySpawnerWaveDefinitionElement previousWaveDefinition = waveDefinitions[waveIndex - 1];

            if (!TryResolveReferenceTime(spawnerState,
                                         previousWaveDefinition,
                                         previousWaveRuntime,
                                         definition.StartMode,
                                         out referenceTime))
                return;
        }

        runtime.ScheduledStartTime = referenceTime + math.max(0f, definition.StartDelaySeconds);
        runtime.StartScheduled = 1;
    }

    /// <summary>
    /// Starts a wave once the current world time reaches its scheduled start timestamp.
    /// /params elapsedTime: Current elapsed world time.
    /// /params definition: Immutable definition of the wave.
    /// /params runtime: Current wave runtime state to update.
    /// /returns None.
    /// </summary>
    private static void TryStartWave(float elapsedTime,
                                     EnemySpawnerWaveDefinitionElement definition,
                                     ref EnemySpawnerWaveRuntimeElement runtime)
    {
        if (runtime.StartScheduled == 0)
            return;

        if (runtime.Started != 0)
            return;

        if (elapsedTime < runtime.ScheduledStartTime)
            return;

        runtime.Started = 1;
        runtime.SpawnStartTime = runtime.ScheduledStartTime;
        runtime.SpawnEndTime = runtime.SpawnStartTime + math.max(0f, definition.SpawnDurationSeconds);

        if (definition.EventCount <= 0 && runtime.SpawnEndTime <= elapsedTime)
            runtime.SpawnFinished = 1;
    }

    /// <summary>
    /// Emits all currently due spawn events for a started wave.
    /// /params entityManager: Entity manager used to access pools and enemy instances.
    /// /params spawnerEntity: Spawner that owns the wave.
    /// /params poolMap: Prefab-to-pool map for this spawner.
    /// /params waveEvents: Full baked event buffer for this spawner.
    /// /params localToWorld: Current spawner local-to-world matrix.
    /// /params elapsedTime: Current elapsed world time.
    /// /params definition: Immutable definition of the wave.
    /// /params runtime: Mutable runtime state for the wave.
    /// /params spawnerState: Mutable global spawner state.
    /// /returns None.
    /// </summary>
    private static void SpawnDueEvents(EntityManager entityManager,
                                       Entity spawnerEntity,
                                       DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap,
                                       DynamicBuffer<EnemySpawnerWaveEventElement> waveEvents,
                                       float4x4 localToWorld,
                                       float elapsedTime,
                                       EnemySpawnerWaveDefinitionElement definition,
                                       ref EnemySpawnerWaveRuntimeElement runtime,
                                       ref EnemySpawnerState spawnerState)
    {
        while (runtime.NextEventIndex < definition.EventCount)
        {
            int eventIndex = definition.FirstEventIndex + runtime.NextEventIndex;

            if (eventIndex < 0 || eventIndex >= waveEvents.Length)
            {
                runtime.NextEventIndex = definition.EventCount;
                break;
            }

            EnemySpawnerWaveEventElement waveEvent = waveEvents[eventIndex];
            float dueTime = runtime.SpawnStartTime + math.max(0f, waveEvent.RelativeTime);

            if (elapsedTime < dueTime)
                break;

            Entity poolEntity;

            if (!TryGetPoolEntity(poolMap, waveEvent.PrefabEntity, out poolEntity))
                break;

            Entity enemyEntity;

            if (!TryAcquireEnemy(entityManager, poolEntity, spawnerEntity, waveEvent.PrefabEntity, out enemyEntity))
                break;

            float3 worldPosition = math.transform(localToWorld, waveEvent.LocalSpawnPosition);
            EnemyPoolUtility.ActivateEnemy(entityManager,
                                           enemyEntity,
                                           spawnerEntity,
                                           poolEntity,
                                           waveEvent.WaveIndex,
                                           worldPosition);
            runtime.NextEventIndex++;
            runtime.AliveCount++;
            runtime.SpawnedCount++;
            spawnerState.AliveCount++;
        }
    }

    /// <summary>
    /// Marks a wave as spawn-finished or completed once its conditions are satisfied.
    /// /params elapsedTime: Current elapsed world time.
    /// /params definition: Immutable definition of the wave.
    /// /params runtime: Mutable runtime state for the wave.
    /// /returns None.
    /// </summary>
    private static void TryFinalizeWave(float elapsedTime,
                                        EnemySpawnerWaveDefinitionElement definition,
                                        ref EnemySpawnerWaveRuntimeElement runtime)
    {
        if (runtime.Started == 0)
            return;

        if (runtime.SpawnFinished == 0)
        {
            bool allEventsConsumed = runtime.NextEventIndex >= definition.EventCount;
            bool spawnWindowFinished = elapsedTime >= runtime.SpawnEndTime;

            if (allEventsConsumed && spawnWindowFinished)
                runtime.SpawnFinished = 1;
        }

        if (runtime.Completed != 0)
            return;

        if (runtime.SpawnFinished == 0)
            return;

        if (runtime.AliveCount > 0)
            return;

        runtime.Completed = 1;
        runtime.CompletionTime = math.max(runtime.SpawnEndTime, elapsedTime);
    }

    /// <summary>
    /// Resolves the prerequisite reference time that drives scheduling for a non-first wave.
    /// /params spawnerState: Global mutable spawner state.
    /// /params previousWaveDefinition: Immutable definition of the previous wave.
    /// /params previousWaveRuntime: Mutable runtime state of the previous wave.
    /// /params startMode: Requested start mode for the current wave.
    /// /params referenceTime: Resolved reference time when the prerequisite is satisfied.
    /// /returns True when the prerequisite is satisfied and the reference time is valid, otherwise false.
    /// </summary>
    private static bool TryResolveReferenceTime(EnemySpawnerState spawnerState,
                                                EnemySpawnerWaveDefinitionElement previousWaveDefinition,
                                                EnemySpawnerWaveRuntimeElement previousWaveRuntime,
                                                EnemyWaveStartMode startMode,
                                                out float referenceTime)
    {
        switch (startMode)
        {
            case EnemyWaveStartMode.FromSpawnerStart:
                referenceTime = spawnerState.StartTime;
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveStart:
                if (previousWaveRuntime.Started == 0)
                    break;

                referenceTime = previousWaveRuntime.SpawnStartTime;
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveSpawnEnd:
                if (previousWaveRuntime.Started == 0)
                    break;

                referenceTime = previousWaveRuntime.SpawnStartTime + math.max(0f, previousWaveDefinition.SpawnDurationSeconds);
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveCompleted:
                if (previousWaveRuntime.Completed == 0)
                    break;

                referenceTime = previousWaveRuntime.CompletionTime;
                return true;

            case EnemyWaveStartMode.AfterPreviousWaveFirstKill:
                if (previousWaveRuntime.FirstKillRegistered == 0)
                    break;

                referenceTime = previousWaveRuntime.FirstKillTime;
                return true;
        }

        referenceTime = 0f;
        return false;
    }

    /// <summary>
    /// Resolves the concrete pool entity associated with the provided prefab.
    /// /params poolMap: Prefab-to-pool map buffer stored on the spawner.
    /// /params prefabEntity: Referenced prefab to resolve.
    /// /params poolEntity: Resolved pool entity when found.
    /// /returns True when the mapping exists, otherwise false.
    /// </summary>
    private static bool TryGetPoolEntity(DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap,
                                         Entity prefabEntity,
                                         out Entity poolEntity)
    {
        for (int mapIndex = 0; mapIndex < poolMap.Length; mapIndex++)
        {
            EnemySpawnerPrefabPoolMapElement mapEntry = poolMap[mapIndex];

            if (mapEntry.PrefabEntity != prefabEntity)
                continue;

            poolEntity = mapEntry.PoolEntity;
            return true;
        }

        poolEntity = Entity.Null;
        return false;
    }

    /// <summary>
    /// Acquires one pooled enemy instance from the requested pool, expanding it on demand.
    /// /params entityManager: Entity manager used to access the pool entity.
    /// /params poolEntity: Concrete pool that should provide an enemy instance.
    /// /params spawnerEntity: Spawner that owns the pool.
    /// /params prefabEntity: Prefab associated with the pool.
    /// /params enemyEntity: Resolved pooled enemy instance when acquisition succeeds.
    /// /returns True when an instance was acquired, otherwise false.
    /// </summary>
    private static bool TryAcquireEnemy(EntityManager entityManager,
                                        Entity poolEntity,
                                        Entity spawnerEntity,
                                        Entity prefabEntity,
                                        out Entity enemyEntity)
    {
        enemyEntity = Entity.Null;

        if (!entityManager.Exists(poolEntity))
            return false;

        if (!entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
            return false;

        DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);

        if (poolBuffer.Length <= 0)
        {
            if (!entityManager.HasComponent<EnemyPoolState>(poolEntity))
                return false;

            EnemyPoolState poolState = entityManager.GetComponentData<EnemyPoolState>(poolEntity);
            int expandCount = math.max(1, poolState.ExpandBatch);
            EnemyPoolUtility.ExpandPool(entityManager,
                                        poolEntity,
                                        spawnerEntity,
                                        prefabEntity,
                                        expandCount);
            poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);
        }

        while (poolBuffer.Length > 0)
        {
            int lastPoolIndex = poolBuffer.Length - 1;
            enemyEntity = poolBuffer[lastPoolIndex].EnemyEntity;
            poolBuffer.RemoveAt(lastPoolIndex);

            if (entityManager.Exists(enemyEntity))
                return true;
        }

        enemyEntity = Entity.Null;
        return false;
    }
    #endregion

    #endregion
}
