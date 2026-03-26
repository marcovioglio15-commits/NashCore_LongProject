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
    #region Constants
    private const float WarningTimeEpsilon = 0.0001f;
    private const float WarningPositionEpsilon = 0.0001f;
    private static readonly float3 DefaultUpAxis = new float3(0f, 1f, 0f);
    #endregion

    #region Fields
    private EntityQuery spawnerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Caches the query used to iterate initialized wave spawners.
    ///  state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        spawnerQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EnemySpawner,
                     EnemySpawnWarningConfig,
                     EnemySpawnerState,
                     EnemySpawnerWaveDefinitionElement,
                     EnemySpawnerWaveRuntimeElement,
                     EnemySpawnerWaveEventElement>()
            .WithAll<EnemySpawnWarningRequestElement,
                     EnemySpawnerPrefabPoolMapElement,
                     LocalToWorld>()
            .Build(ref state);
        state.RequireForUpdate(spawnerQuery);
    }

    /// <summary>
    /// Schedules wave starts, emits due events and activates pooled enemies at their baked positions.
    ///  state: Current ECS system state.
    /// returns None.
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
    ///  entityManager: Entity manager used to access components and buffers.
    ///  spawnerEntity: Spawner entity being processed.
    ///  elapsedTime: Current elapsed world time.
    /// returns None.
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
        DynamicBuffer<EnemySpawnWarningRequestElement> spawnWarningRequests = entityManager.GetBuffer<EnemySpawnWarningRequestElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerPrefabPoolMapElement> poolMap = entityManager.GetBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);
        EnemySpawnWarningConfig spawnWarningConfig = entityManager.GetComponentData<EnemySpawnWarningConfig>(spawnerEntity);
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
            {
                EmitUpcomingSpawnWarnings(spawnWarningRequests,
                                          waveEvents,
                                          localToWorld,
                                          elapsedTime,
                                          definition,
                                          spawnWarningConfig,
                                          ref runtime);
                SpawnDueEvents(entityManager,
                               spawnerEntity,
                               poolMap,
                               waveEvents,
                               localToWorld,
                               elapsedTime,
                               definition,
                               ref runtime,
                               ref spawnerState);
            }

            TryFinalizeWave(elapsedTime, definition, ref runtime);
            waveRuntime[waveIndex] = runtime;
        }

        entityManager.SetComponentData(spawnerEntity, spawnerState);
    }

    /// <summary>
    /// Assigns a deterministic scheduled start time to a wave as soon as its prerequisite state is available.
    ///  spawnerState: Global mutable spawner state.
    ///  waveDefinitions: Immutable wave definition buffer.
    ///  waveRuntime: Mutable wave runtime buffer.
    ///  waveIndex: Wave index to schedule.
    ///  runtime: Current wave runtime state to update.
    /// returns None.
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
    ///  elapsedTime: Current elapsed world time.
    ///  definition: Immutable definition of the wave.
    ///  runtime: Current wave runtime state to update.
    /// returns None.
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
    /// Emits warning requests for upcoming spawn events that entered the authored warning lead-time window.
    ///  spawnWarningRequests: Transient request buffer consumed later by presentation systems.
    ///  waveEvents: Full baked event buffer for this spawner.
    ///  localToWorld: Current spawner local-to-world matrix.
    ///  elapsedTime: Current elapsed world time.
    ///  definition: Immutable definition of the wave.
    ///  spawnWarningConfig: Immutable warning settings baked from the spawner authoring component.
    ///  runtime: Mutable runtime state for the wave.
    /// returns None.
    /// </summary>
    private static void EmitUpcomingSpawnWarnings(DynamicBuffer<EnemySpawnWarningRequestElement> spawnWarningRequests,
                                                  DynamicBuffer<EnemySpawnerWaveEventElement> waveEvents,
                                                  float4x4 localToWorld,
                                                  float elapsedTime,
                                                  EnemySpawnerWaveDefinitionElement definition,
                                                  EnemySpawnWarningConfig spawnWarningConfig,
                                                  ref EnemySpawnerWaveRuntimeElement runtime)
    {
        runtime.NextWarningEventIndex = math.max(runtime.NextWarningEventIndex, runtime.NextEventIndex);

        if (spawnWarningConfig.Enabled == 0)
            return;

        float leadTimeSeconds = math.max(0f, spawnWarningConfig.LeadTimeSeconds);

        if (leadTimeSeconds <= 0f)
            return;

        if (spawnWarningConfig.RadiusScale <= 0f || spawnWarningConfig.RingWidth <= 0f || spawnWarningConfig.MaximumAlpha <= 0f)
            return;

        while (runtime.NextWarningEventIndex < definition.EventCount)
        {
            int eventIndex = definition.FirstEventIndex + runtime.NextWarningEventIndex;

            if (eventIndex < 0 || eventIndex >= waveEvents.Length)
            {
                runtime.NextWarningEventIndex = definition.EventCount;
                break;
            }

            EnemySpawnerWaveEventElement waveEvent = waveEvents[eventIndex];
            float dueTime = runtime.SpawnStartTime + math.max(0f, waveEvent.RelativeTime);

            if (dueTime <= elapsedTime)
            {
                runtime.NextWarningEventIndex++;
                continue;
            }

            float remainingSeconds = dueTime - elapsedTime;

            if (remainingSeconds > leadTimeSeconds)
                break;

            float3 groupedLocalPosition = waveEvent.LocalSpawnPosition;
            int nextWarningIndex = runtime.NextWarningEventIndex + 1;

            while (nextWarningIndex < definition.EventCount)
            {
                int groupedEventIndex = definition.FirstEventIndex + nextWarningIndex;

                if (groupedEventIndex < 0 || groupedEventIndex >= waveEvents.Length)
                    break;

                EnemySpawnerWaveEventElement groupedEvent = waveEvents[groupedEventIndex];
                float groupedDueTime = runtime.SpawnStartTime + math.max(0f, groupedEvent.RelativeTime);

                if (math.abs(groupedDueTime - dueTime) > WarningTimeEpsilon)
                    break;

                if (math.distancesq(groupedEvent.LocalSpawnPosition, groupedLocalPosition) > WarningPositionEpsilon)
                    break;

                nextWarningIndex++;
            }

            EnqueueSpawnWarningRequest(spawnWarningRequests,
                                       localToWorld,
                                       groupedLocalPosition,
                                       remainingSeconds,
                                       spawnWarningConfig);
            runtime.NextWarningEventIndex = nextWarningIndex;
        }
    }

    /// <summary>
    /// Adds one concrete warning request to the transient spawner buffer.
    ///  spawnWarningRequests: Buffer receiving the request.
    ///  localToWorld: Current spawner local-to-world matrix.
    ///  localSpawnPosition: Local spawn position resolved from the baked wave event.
    ///  durationSeconds: Remaining warning duration before the spawn becomes active.
    ///  spawnWarningConfig: Immutable warning settings baked from the spawner authoring component.
    /// returns None.
    /// </summary>
    private static void EnqueueSpawnWarningRequest(DynamicBuffer<EnemySpawnWarningRequestElement> spawnWarningRequests,
                                                   float4x4 localToWorld,
                                                   float3 localSpawnPosition,
                                                   float durationSeconds,
                                                   EnemySpawnWarningConfig spawnWarningConfig)
    {
        float3 worldPosition = math.transform(localToWorld, localSpawnPosition);
        float3 worldUp = math.normalizesafe(localToWorld.c1.xyz, DefaultUpAxis);
        worldPosition += worldUp * math.max(0f, spawnWarningConfig.HeightOffset);

        spawnWarningRequests.Add(new EnemySpawnWarningRequestElement
        {
            WorldPosition = worldPosition,
            DurationSeconds = math.max(0.01f, durationSeconds),
            FadeOutSeconds = math.max(0f, spawnWarningConfig.FadeOutSeconds),
            Radius = math.max(0.05f, spawnWarningConfig.CellSize * math.max(0.01f, spawnWarningConfig.RadiusScale)),
            RingWidth = math.max(0.01f, spawnWarningConfig.RingWidth),
            MaximumAlpha = math.saturate(spawnWarningConfig.MaximumAlpha),
            Color = spawnWarningConfig.Color
        });
    }

    /// <summary>
    /// Emits all currently due spawn events for a started wave.
    ///  entityManager: Entity manager used to access pools and enemy instances.
    ///  spawnerEntity: Spawner that owns the wave.
    ///  poolMap: Prefab-to-pool map for this spawner.
    ///  waveEvents: Full baked event buffer for this spawner.
    ///  localToWorld: Current spawner local-to-world matrix.
    ///  elapsedTime: Current elapsed world time.
    ///  definition: Immutable definition of the wave.
    ///  runtime: Mutable runtime state for the wave.
    ///  spawnerState: Mutable global spawner state.
    /// returns None.
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
    ///  elapsedTime: Current elapsed world time.
    ///  definition: Immutable definition of the wave.
    ///  runtime: Mutable runtime state for the wave.
    /// returns None.
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
    ///  spawnerState: Global mutable spawner state.
    ///  previousWaveDefinition: Immutable definition of the previous wave.
    ///  previousWaveRuntime: Mutable runtime state of the previous wave.
    ///  startMode: Requested start mode for the current wave.
    ///  referenceTime: Resolved reference time when the prerequisite is satisfied.
    /// returns True when the prerequisite is satisfied and the reference time is valid, otherwise false.
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
    ///  poolMap: Prefab-to-pool map buffer stored on the spawner.
    ///  prefabEntity: Referenced prefab to resolve.
    ///  poolEntity: Resolved pool entity when found.
    /// returns True when the mapping exists, otherwise false.
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
    ///  entityManager: Entity manager used to access the pool entity.
    ///  poolEntity: Concrete pool that should provide an enemy instance.
    ///  spawnerEntity: Spawner that owns the pool.
    ///  prefabEntity: Prefab associated with the pool.
    ///  enemyEntity: Resolved pooled enemy instance when acquisition succeeds.
    /// returns True when an instance was acquired, otherwise false.
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
