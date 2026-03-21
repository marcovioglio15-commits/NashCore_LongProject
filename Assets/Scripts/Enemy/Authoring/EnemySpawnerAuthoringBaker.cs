using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bakes EnemySpawnerAuthoring data into finite wave buffers and prefab-specific pool requirements.
/// </summary>
public sealed class EnemySpawnerAuthoringBaker : Baker<EnemySpawnerAuthoring>
{
    #region Methods

    #region Bake
    /// <summary>
    /// Converts the authored wave grid into ECS wave definitions, events and pool requirements.
    /// /params authoring: Spawner authoring source component.
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

        StageWaves(authoring,
                   stagedWaveDefinitions,
                   stagedWaveRuntime,
                   stagedWaveEvents,
                   plannedCountByPrefab);

        AddComponent(spawnerEntity, new EnemySpawner
        {
            InitialPoolCapacityPerPrefab = math.max(0, authoring.InitialPoolCapacityPerPrefab),
            ExpandBatchPerPrefab = math.max(1, authoring.ExpandBatchPerPrefab),
            DespawnDistance = math.max(0f, authoring.DespawnDistance),
            TotalPlannedEnemyCount = CountTotalPlannedEnemies(plannedCountByPrefab)
        });
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
    /// /params authoring: Spawner authoring source.
    /// /params stagedWaveDefinitions: Target wave definition list.
    /// /params stagedWaveRuntime: Target wave runtime default list.
    /// /params stagedWaveEvents: Target exact spawn event list.
    /// /params plannedCountByPrefab: Target prefab usage count map.
    /// /returns None.
    /// </summary>
    private void StageWaves(EnemySpawnerAuthoring authoring,
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
            StageWaveCells(authoring, wave, waveIndex, stagedEventsForWave, plannedCountByPrefab);
            EnemySpawnerWaveBakeUtility.SortWaveEvents(stagedEventsForWave);
            int firstEventIndex = stagedWaveEvents.Count;

            for (int eventIndex = 0; eventIndex < stagedEventsForWave.Count; eventIndex++)
                stagedWaveEvents.Add(stagedEventsForWave[eventIndex]);

            stagedWaveDefinitions.Add(new EnemySpawnerWaveDefinitionElement
            {
                StartMode = waveIndex == 0 ? EnemyWaveStartMode.FromSpawnerStart : wave.StartMode,
                StartDelaySeconds = math.max(0f, wave.StartDelaySeconds),
                SpawnDurationSeconds = math.max(0f, wave.SpawnDurationSeconds),
                FirstEventIndex = firstEventIndex,
                EventCount = stagedEventsForWave.Count
            });
            stagedWaveRuntime.Add(CreateDefaultWaveRuntime());
        }
    }

    /// <summary>
    /// Stages exact spawn events for all painted cells of one wave.
    /// /params authoring: Spawner authoring source.
    /// /params wave: Wave being converted.
    /// /params waveIndex: Current wave index.
    /// /params stagedEventsForWave: Target event list for the current wave.
    /// /params plannedCountByPrefab: Target prefab usage count map.
    /// /returns None.
    /// </summary>
    private void StageWaveCells(EnemySpawnerAuthoring authoring,
                                EnemySpawnWaveAuthoring wave,
                                int waveIndex,
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

            float3 localSpawnPosition = authoring.ResolveCellLocalCenter(cell.CellCoordinate);
            AnimationCurve distributionCurve = cell.UseWaveDefaultDistribution
                ? wave.DefaultDistributionCurve
                : cell.DistributionCurveOverride;
            EnemySpawnerWaveBakeUtility.BuildCellEvents(waveIndex,
                                                       prefabEntity,
                                                       wave.SpawnDurationSeconds,
                                                       localSpawnPosition,
                                                       enemyCount,
                                                       distributionCurve,
                                                       stagedEventsForWave);

            int plannedCount;

            if (plannedCountByPrefab.TryGetValue(prefabEntity, out plannedCount))
                plannedCountByPrefab[prefabEntity] = plannedCount + enemyCount;
            else
                plannedCountByPrefab[prefabEntity] = enemyCount;
        }
    }

    /// <summary>
    /// Resolves the prefab entity used by one painted cell through its master and visual presets.
    /// /params authoring: Spawner authoring component used only for warning context.
    /// /params masterPreset: Enemy master preset painted on the cell.
    /// /params prefabEntity: Resolved prefab entity when successful.
    /// /returns True when the cell references a valid enemy prefab, otherwise false.
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
    /// Creates the default runtime buffer entry for one wave.
    /// /returns Default wave runtime state.
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
    /// /params plannedCountByPrefab: Prefab usage count map.
    /// /returns Total planned enemy count for the spawner.
    /// </summary>
    private static int CountTotalPlannedEnemies(Dictionary<Entity, int> plannedCountByPrefab)
    {
        int totalPlannedEnemies = 0;

        foreach (KeyValuePair<Entity, int> pair in plannedCountByPrefab)
            totalPlannedEnemies += math.max(0, pair.Value);

        return totalPlannedEnemies;
    }
    #endregion

    #endregion
}
