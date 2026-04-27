using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bakes EnemySpawnerAuthoring data into finite wave buffers and prefab-specific pool requirements.
/// /params None.
/// /returns None.
/// </summary>
public sealed class EnemySpawnerAuthoringBaker : Baker<EnemySpawnerAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Converts the authored wave grid into ECS wave definitions, events and pool requirements.
    /// /params authoring Spawner authoring source component.
    /// /returns None.
    /// </summary>
    public override void Bake(EnemySpawnerAuthoring authoring)
    {
        if (authoring == null)
            return;

        DependsOn(authoring.WavePreset);

        Entity spawnerEntity = GetEntity(TransformUsageFlags.Dynamic);
        List<EnemySpawnerWaveDefinitionElement> stagedWaveDefinitions = new List<EnemySpawnerWaveDefinitionElement>();
        List<EnemySpawnerWaveRuntimeElement> stagedWaveRuntime = new List<EnemySpawnerWaveRuntimeElement>();
        List<EnemySpawnerWaveEventElement> stagedWaveEvents = new List<EnemySpawnerWaveEventElement>();
        Dictionary<Entity, int> plannedCountByPrefab = new Dictionary<Entity, int>();
        EnemySpawnWarningConfig spawnerWarningConfig = BuildSpawnerWarningConfig(authoring);

        StageWaves(authoring,
                   spawnerWarningConfig,
                   stagedWaveDefinitions,
                   stagedWaveRuntime,
                   stagedWaveEvents,
                   plannedCountByPrefab);

        AddComponent(spawnerEntity, new EnemySpawner
        {
            InitialPoolCapacityPerPrefab = math.max(0, authoring.InitialPoolCapacityPerPrefab),
            ExpandBatchPerPrefab = math.max(1, authoring.ExpandBatchPerPrefab),
            DespawnDistance = math.max(0f, authoring.DespawnDistance),
            MaximumSpawnDistanceFromCenter = ResolveMaximumSpawnDistanceFromCenter(stagedWaveEvents, authoring.CellSize),
            TotalPlannedEnemyCount = CountTotalPlannedEnemies(plannedCountByPrefab)
        });
        AddComponent(spawnerEntity, spawnerWarningConfig);
        AddComponent(spawnerEntity, new EnemySpawnerState
        {
            StartTime = 0f,
            AliveCount = 0,
            Initialized = 0
        });

        DynamicBuffer<EnemySpawnerWaveDefinitionElement> waveDefinitionBuffer = AddBuffer<EnemySpawnerWaveDefinitionElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntimeBuffer = AddBuffer<EnemySpawnerWaveRuntimeElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerWaveEventElement> waveEventBuffer = AddBuffer<EnemySpawnerWaveEventElement>(spawnerEntity);
        DynamicBuffer<EnemySpawnerPrefabRequirementElement> prefabRequirementBuffer = AddBuffer<EnemySpawnerPrefabRequirementElement>(spawnerEntity);
        AddBuffer<EnemySpawnerPrefabPoolMapElement>(spawnerEntity);

        for (int definitionIndex = 0; definitionIndex < stagedWaveDefinitions.Count; definitionIndex++)
            waveDefinitionBuffer.Add(stagedWaveDefinitions[definitionIndex]);

        for (int runtimeIndex = 0; runtimeIndex < stagedWaveRuntime.Count; runtimeIndex++)
            waveRuntimeBuffer.Add(stagedWaveRuntime[runtimeIndex]);

        for (int eventIndex = 0; eventIndex < stagedWaveEvents.Count; eventIndex++)
            waveEventBuffer.Add(stagedWaveEvents[eventIndex]);

        foreach (KeyValuePair<Entity, int> pair in plannedCountByPrefab)
        {
            prefabRequirementBuffer.Add(new EnemySpawnerPrefabRequirementElement
            {
                PrefabEntity = pair.Key,
                TotalPlannedCount = pair.Value
            });
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Stages wave definitions, runtime defaults and exact spawn events from the authored spawner data.
    /// /params authoring Spawner authoring source.
    /// /params spawnerWarningConfig Spawner-level fallback warning config used when enemy visuals do not override warning settings.
    /// /params stagedWaveDefinitions Target wave definition list.
    /// /params stagedWaveRuntime Target wave runtime default list.
    /// /params stagedWaveEvents Target exact spawn event list.
    /// /params plannedCountByPrefab Target prefab usage count map.
    /// /returns None.
    /// </summary>
    private void StageWaves(EnemySpawnerAuthoring authoring,
                            EnemySpawnWarningConfig spawnerWarningConfig,
                            List<EnemySpawnerWaveDefinitionElement> stagedWaveDefinitions,
                            List<EnemySpawnerWaveRuntimeElement> stagedWaveRuntime,
                            List<EnemySpawnerWaveEventElement> stagedWaveEvents,
                            Dictionary<Entity, int> plannedCountByPrefab)
    {
        List<EnemySpawnWaveAuthoring> waves = authoring.Waves;

        if (waves == null)
            return;

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = waves[waveIndex];

            if (wave == null)
                continue;

            List<EnemySpawnerWaveEventElement> stagedEventsForWave = new List<EnemySpawnerWaveEventElement>();
            StageWaveCells(authoring, wave, waveIndex, spawnerWarningConfig, stagedEventsForWave, plannedCountByPrefab);
            EnemySpawnerWaveBakeUtility.SortWaveEvents(stagedEventsForWave);
            int firstEventIndex = stagedWaveEvents.Count;
            float maximumSpawnWarningLeadTimeSeconds = ResolveMaximumWaveWarningLeadTime(stagedEventsForWave, spawnerWarningConfig);

            for (int eventIndex = 0; eventIndex < stagedEventsForWave.Count; eventIndex++)
                stagedWaveEvents.Add(stagedEventsForWave[eventIndex]);

            stagedWaveDefinitions.Add(new EnemySpawnerWaveDefinitionElement
            {
                StartMode = waveIndex == 0 ? EnemyWaveStartMode.FromSpawnerStart : wave.StartMode,
                StartDelaySeconds = math.max(0f, wave.StartDelaySeconds),
                SpawnDurationSeconds = math.max(0f, wave.SpawnDurationSeconds),
                MaximumSpawnWarningLeadTimeSeconds = maximumSpawnWarningLeadTimeSeconds,
                FirstEventIndex = firstEventIndex,
                EventCount = stagedEventsForWave.Count
            });
            stagedWaveRuntime.Add(CreateDefaultWaveRuntime());
        }
    }

    /// <summary>
    /// Stages exact spawn events for all painted cells of one wave.
    /// /params authoring Spawner authoring source.
    /// /params wave Wave being converted.
    /// /params waveIndex Current wave index.
    /// /params spawnerWarningConfig Spawner-level fallback warning config used for non-overridden events.
    /// /params stagedEventsForWave Target event list for the current wave.
    /// /params plannedCountByPrefab Target prefab usage count map.
    /// /returns None.
    /// </summary>
    private void StageWaveCells(EnemySpawnerAuthoring authoring,
                                EnemySpawnWaveAuthoring wave,
                                int waveIndex,
                                EnemySpawnWarningConfig spawnerWarningConfig,
                                List<EnemySpawnerWaveEventElement> stagedEventsForWave,
                                Dictionary<Entity, int> plannedCountByPrefab)
    {
        if (wave.PaintedCells == null)
            return;

        for (int cellIndex = 0; cellIndex < wave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = wave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            int enemyCount = math.max(0, cell.EnemyCount);

            if (enemyCount <= 0)
                continue;

            EnemyMasterPreset masterPreset = cell.MasterPreset;
            DependsOn(masterPreset);

            if (!TryResolveCellPrefab(authoring, masterPreset, out Entity prefabEntity))
                continue;

            EnemyVisualSpawnOverridesSettings spawnOverrides = ResolveSpawnOverrides(masterPreset);
            float3 localSpawnPosition = authoring.ResolveCellLocalCenter(cell.CellCoordinate) + ResolveSpawnOffset(spawnOverrides);
            AnimationCurve distributionCurve = cell.UseWaveDefaultDistribution
                ? wave.DefaultDistributionCurve
                : cell.DistributionCurveOverride;
            int firstInsertedEventIndex = stagedEventsForWave.Count;
            EnemySpawnerWaveBakeUtility.BuildCellEvents(waveIndex,
                                                       prefabEntity,
                                                       wave.SpawnDurationSeconds,
                                                       localSpawnPosition,
                                                       authoring.CellSize,
                                                       cell.CellCoordinate,
                                                       enemyCount,
                                                       distributionCurve,
                                                       stagedEventsForWave);
            ApplySpawnWarningOverrides(stagedEventsForWave, firstInsertedEventIndex, spawnOverrides, spawnerWarningConfig);

            int plannedCount;

            if (plannedCountByPrefab.TryGetValue(prefabEntity, out plannedCount))
                plannedCountByPrefab[prefabEntity] = plannedCount + enemyCount;
            else
                plannedCountByPrefab[prefabEntity] = enemyCount;
        }
    }

    /// <summary>
    /// Resolves the prefab entity used by one painted cell through its master and visual presets.
    /// authoring: Spawner authoring component used only for warning context.
    /// masterPreset: Enemy master preset painted on the cell.
    /// prefabEntity: Resolved prefab entity when successful.
    /// returns True when the cell references a valid enemy prefab, otherwise false.
    /// </summary>
    private bool TryResolveCellPrefab(EnemySpawnerAuthoring authoring,
                                      EnemyMasterPreset masterPreset,
                                      out Entity prefabEntity)
    {
        prefabEntity = Entity.Null;

        if (masterPreset == null)
            return false;

        EnemyVisualPreset visualPreset = masterPreset.VisualPreset;
        DependsOn(visualPreset);

        GameObject enemyPrefab = EnemySpawnerWaveBakeUtility.ResolveEnemyPrefab(masterPreset);

        if (enemyPrefab == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[EnemySpawnerAuthoringBaker] Master preset '{0}' does not resolve an enemy prefab through its visual preset. The painted cell will be ignored.",
                                           masterPreset.name),
                             authoring);
#endif
            return false;
        }

        DependsOn(enemyPrefab);

        if (enemyPrefab.scene.IsValid())
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[EnemySpawnerAuthoringBaker] Enemy prefab '{0}' referenced by master preset '{1}' is a scene object. Assign a prefab asset instead.",
                                           enemyPrefab.name,
                                           masterPreset.name),
                             authoring);
#endif
            return false;
        }

        if (enemyPrefab.GetComponentInChildren<EnemyAuthoring>(true) == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[EnemySpawnerAuthoringBaker] Enemy prefab '{0}' referenced by master preset '{1}' does not contain EnemyAuthoring in hierarchy.",
                                           enemyPrefab.name,
                                           masterPreset.name),
                             authoring);
#endif
            return false;
        }

        prefabEntity = GetEntity(enemyPrefab, TransformUsageFlags.Dynamic);
        return prefabEntity != Entity.Null;
    }

    /// <summary>
    /// Builds the spawner-level fallback warning config from authoring values.
    /// /params authoring Spawner authoring source component.
    /// /returns Baked fallback warning config.
    /// </summary>
    private static EnemySpawnWarningConfig BuildSpawnerWarningConfig(EnemySpawnerAuthoring authoring)
    {
        return new EnemySpawnWarningConfig
        {
            Enabled = authoring.EnableSpawnWarning ? (byte)1 : (byte)0,
            LeadTimeSeconds = math.max(0f, authoring.SpawnWarningLeadTimeSeconds),
            FadeOutSeconds = math.max(0f, authoring.SpawnWarningFadeOutSeconds),
            RadiusScale = math.max(0.01f, authoring.SpawnWarningRadiusScale),
            RingWidth = math.max(0.01f, authoring.SpawnWarningRingWidth),
            HeightOffset = math.max(0f, authoring.SpawnWarningHeightOffset),
            MaximumAlpha = math.saturate(authoring.SpawnWarningMaximumAlpha),
            Color = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.SpawnWarningColor),
            CellSize = math.max(0.1f, authoring.CellSize)
        };
    }

    /// <summary>
    /// Resolves spawn overrides from the visual preset assigned to a painted enemy type.
    /// /params masterPreset Painted enemy master preset.
    /// /returns Spawn overrides block, or null when unavailable.
    /// </summary>
    private static EnemyVisualSpawnOverridesSettings ResolveSpawnOverrides(EnemyMasterPreset masterPreset)
    {
        if (masterPreset == null || masterPreset.VisualPreset == null)
            return null;

        return masterPreset.VisualPreset.SpawnOverrides;
    }

    /// <summary>
    /// Resolves the local-space spawn offset applied by one enemy visual preset.
    /// /params spawnOverrides Spawn override settings for the painted enemy type.
    /// /returns Local-space spawn offset.
    /// </summary>
    private static float3 ResolveSpawnOffset(EnemyVisualSpawnOverridesSettings spawnOverrides)
    {
        if (spawnOverrides == null || !spawnOverrides.OverrideSpawnOffset)
            return float3.zero;

        Vector3 offset = spawnOverrides.SpawnOffset;
        return new float3(offset.x, offset.y, offset.z);
    }

    /// <summary>
    /// Writes event-level spawn warning overrides onto newly staged events for one painted cell.
    /// /params stagedEventsForWave Event list receiving overrides.
    /// /params firstInsertedEventIndex First event index inserted for the painted cell.
    /// /params spawnOverrides Spawn override settings for the painted enemy type.
    /// /params spawnerWarningConfig Spawner-level fallback warning config.
    /// /returns None.
    /// </summary>
    private static void ApplySpawnWarningOverrides(List<EnemySpawnerWaveEventElement> stagedEventsForWave,
                                                   int firstInsertedEventIndex,
                                                   EnemyVisualSpawnOverridesSettings spawnOverrides,
                                                   EnemySpawnWarningConfig spawnerWarningConfig)
    {
        if (stagedEventsForWave == null || spawnOverrides == null || !spawnOverrides.OverrideSpawnWarning)
            return;

        EnemySpawnWarningConfig overrideConfig = BuildSpawnWarningOverrideConfig(spawnOverrides, spawnerWarningConfig.CellSize);

        for (int eventIndex = math.max(0, firstInsertedEventIndex); eventIndex < stagedEventsForWave.Count; eventIndex++)
        {
            EnemySpawnerWaveEventElement waveEvent = stagedEventsForWave[eventIndex];
            waveEvent.HasSpawnWarningOverride = 1;
            waveEvent.SpawnWarningOverride = overrideConfig;
            stagedEventsForWave[eventIndex] = waveEvent;
        }
    }

    /// <summary>
    /// Builds an event-level warning config from visual preset override settings.
    /// /params spawnOverrides Visual preset override settings.
    /// /params cellSize Baked spawner cell size.
    /// /returns Event-level warning config.
    /// </summary>
    private static EnemySpawnWarningConfig BuildSpawnWarningOverrideConfig(EnemyVisualSpawnOverridesSettings spawnOverrides, float cellSize)
    {
        return new EnemySpawnWarningConfig
        {
            Enabled = spawnOverrides.EnableSpawnWarning ? (byte)1 : (byte)0,
            LeadTimeSeconds = math.max(0f, spawnOverrides.SpawnWarningLeadTimeSeconds),
            FadeOutSeconds = math.max(0f, spawnOverrides.SpawnWarningFadeOutSeconds),
            RadiusScale = math.max(0.01f, spawnOverrides.SpawnWarningRadiusScale),
            RingWidth = math.max(0.01f, spawnOverrides.SpawnWarningRingWidth),
            HeightOffset = math.max(0f, spawnOverrides.SpawnWarningHeightOffset),
            MaximumAlpha = math.saturate(spawnOverrides.SpawnWarningMaximumAlpha),
            Color = DamageFlashRuntimeUtility.ToLinearFloat4(spawnOverrides.SpawnWarningColor),
            CellSize = math.max(0.1f, cellSize)
        };
    }

    /// <summary>
    /// Resolves the largest warning lead time needed before a wave can activate its first spawn event.
    /// /params stagedEventsForWave Sorted or unsorted events belonging to one wave.
    /// /params spawnerWarningConfig Spawner-level fallback warning config.
    /// /returns Maximum effective warning lead time in seconds.
    /// </summary>
    private static float ResolveMaximumWaveWarningLeadTime(List<EnemySpawnerWaveEventElement> stagedEventsForWave,
                                                           EnemySpawnWarningConfig spawnerWarningConfig)
    {
        if (stagedEventsForWave == null)
            return 0f;

        float maximumLeadTimeSeconds = 0f;

        for (int eventIndex = 0; eventIndex < stagedEventsForWave.Count; eventIndex++)
        {
            EnemySpawnWarningConfig eventWarningConfig = EnemySpawnWarningConfigUtility.ResolveEventWarningConfig(stagedEventsForWave[eventIndex],
                                                                                                                 spawnerWarningConfig);
            maximumLeadTimeSeconds = math.max(maximumLeadTimeSeconds,
                                              EnemySpawnWarningConfigUtility.ResolveEffectiveLeadTimeSeconds(in eventWarningConfig));
        }

        return maximumLeadTimeSeconds;
    }

    /// <summary>
    /// Creates the default runtime buffer entry for one wave.
    /// returns Default wave runtime state.
    /// </summary>
    private static EnemySpawnerWaveRuntimeElement CreateDefaultWaveRuntime()
    {
        return new EnemySpawnerWaveRuntimeElement
        {
            ScheduledStartTime = 0f,
            SpawnStartTime = 0f,
            SpawnEndTime = 0f,
            CompletionTime = 0f,
            FirstKillTime = 0f,
            NextEventIndex = 0,
            NextWarningEventIndex = 0,
            AliveCount = 0,
            SpawnedCount = 0,
            StartScheduled = 0,
            Started = 0,
            SpawnFinished = 0,
            Completed = 0,
            FirstKillRegistered = 0
        };
    }

    /// <summary>
    /// Counts the total planned enemies across all unique prefab requirements.
    /// plannedCountByPrefab: Prefab usage count map.
    /// returns Total planned enemy count for the spawner.
    /// </summary>
    private static int CountTotalPlannedEnemies(Dictionary<Entity, int> plannedCountByPrefab)
    {
        int totalPlannedEnemies = 0;

        foreach (KeyValuePair<Entity, int> pair in plannedCountByPrefab)
            totalPlannedEnemies += math.max(0, pair.Value);

        return totalPlannedEnemies;
    }

    /// <summary>
    /// Resolves the maximum planar spawn distance authored by the staged wave events.
    /// The returned radius includes half a cell diagonal so the full painted cell area stays inside the envelope.
    /// stagedWaveEvents: Fully staged exact spawn events of the spawner.
    /// cellSize: Authored square cell size used by the spawn grid.
    /// returns Maximum planar spawn distance from the spawner center.
    /// </summary>
    private static float ResolveMaximumSpawnDistanceFromCenter(List<EnemySpawnerWaveEventElement> stagedWaveEvents, float cellSize)
    {
        if (stagedWaveEvents == null || stagedWaveEvents.Count == 0)
            return 0f;

        const float HalfCellDiagonalFactor = 0.70710677f;
        float cellEnvelopePadding = math.max(0f, cellSize) * HalfCellDiagonalFactor;
        float maximumDistance = 0f;

        for (int eventIndex = 0; eventIndex < stagedWaveEvents.Count; eventIndex++)
        {
            float3 localSpawnPosition = stagedWaveEvents[eventIndex].LocalSpawnPosition;
            float planarDistance = math.length(localSpawnPosition.xz) + cellEnvelopePadding;

            if (planarDistance > maximumDistance)
                maximumDistance = planarDistance;
        }

        return maximumDistance;
    }
    #endregion

    #endregion
}
