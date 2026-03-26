using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Provides shared validation and bake-time helpers for authored enemy spawn waves.
/// </summary>
public static class EnemySpawnerWaveBakeUtility
{
    #region Constants
    private const int CurveSampleCount = 128;
    private static readonly Color DefaultPaintColor = new Color(1f, 0.35f, 0.35f, 1f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns the default cumulative distribution curve used by new waves and cells.
    /// returns Default normalized cumulative distribution curve.
    /// </summary>
    public static AnimationCurve CreateDefaultDistributionCurve()
    {
        return AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// Sanitizes spawner-wide wave data after inspector changes.
    /// Called by EnemySpawnerAuthoring.OnValidate.
    ///  waves: Serialized wave list owned by the spawner authoring.
    ///  gridSizeX: Grid width in cells.
    ///  gridSizeZ: Grid height in cells.
    /// returns None.
    /// </summary>
    public static void ValidateWaves(List<EnemySpawnWaveAuthoring> waves, int gridSizeX, int gridSizeZ)
    {
        if (waves == null)
            return;

        bool previewFound = false;

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            EnemySpawnWaveAuthoring wave = waves[waveIndex];

            if (wave == null)
                continue;

            ValidateWave(wave, gridSizeX, gridSizeZ, waveIndex == 0);

            if (!wave.PreviewInScene)
                continue;

            if (!previewFound)
            {
                previewFound = true;
                continue;
            }

            wave.SetPreviewInScene(false);
        }
    }

    /// <summary>
    /// Computes the local-space center of one grid cell using the spawner grid settings.
    ///  gridSizeX: Grid width in cells.
    ///  gridSizeZ: Grid height in cells.
    ///  cellSize: Square cell size in local units.
    ///  originOffset: Local-space offset applied to the full grid.
    ///  spawnHeightOffset: Local-space height offset applied to spawned enemies.
    ///  cellCoordinate: Authored grid coordinate.
    /// returns Local-space cell center position.
    /// </summary>
    public static float3 ResolveCellLocalCenter(int gridSizeX,
                                                int gridSizeZ,
                                                float cellSize,
                                                Vector3 originOffset,
                                                float spawnHeightOffset,
                                                Vector2Int cellCoordinate)
    {
        float halfGridWidth = (gridSizeX - 1) * 0.5f;
        float halfGridDepth = (gridSizeZ - 1) * 0.5f;
        float x = (cellCoordinate.x - halfGridWidth) * cellSize;
        float z = (cellCoordinate.y - halfGridDepth) * cellSize;
        return new float3(originOffset.x + x,
                          originOffset.y + spawnHeightOffset,
                          originOffset.z + z);
    }

    /// <summary>
    /// Builds exact spawn events for one painted cell from authored count and cumulative distribution curve.
    ///  waveIndex: Owning wave index.
    ///  prefabEntity: Enemy prefab entity associated with the painted master preset.
    ///  spawnDurationSeconds: Authored wave spawn duration in seconds.
    ///  localSpawnPosition: Local-space cell center used by the events.
    ///  enemyCount: Number of enemies authored for the cell.
    ///  distributionCurve: Authored cumulative distribution curve.
    ///  outputEvents: Target list receiving the staged events.
    /// returns None.
    /// </summary>
    public static void BuildCellEvents(int waveIndex,
                                       Unity.Entities.Entity prefabEntity,
                                       float spawnDurationSeconds,
                                       float3 localSpawnPosition,
                                       int enemyCount,
                                       AnimationCurve distributionCurve,
                                       List<EnemySpawnerWaveEventElement> outputEvents)
    {
        if (outputEvents == null)
            return;

        if (prefabEntity == Unity.Entities.Entity.Null)
            return;

        int sanitizedEnemyCount = math.max(0, enemyCount);

        if (sanitizedEnemyCount <= 0)
            return;

        float sanitizedDuration = math.max(0f, spawnDurationSeconds);
        float[] samples = BuildMonotonicSamples(distributionCurve);

        for (int spawnIndex = 0; spawnIndex < sanitizedEnemyCount; spawnIndex++)
        {
            float quantile = ((float)spawnIndex + 0.5f) / sanitizedEnemyCount;
            float normalizedTime = ResolveInverseSampleTime(samples, quantile);
            outputEvents.Add(new EnemySpawnerWaveEventElement
            {
                WaveIndex = waveIndex,
                RelativeTime = sanitizedDuration * normalizedTime,
                LocalSpawnPosition = localSpawnPosition,
                PrefabEntity = prefabEntity
            });
        }
    }

    /// <summary>
    /// Sorts a staged event list using deterministic time-first ordering.
    ///  stagedWaveEvents: Event list to sort in place.
    /// returns None.
    /// </summary>
    public static void SortWaveEvents(List<EnemySpawnerWaveEventElement> stagedWaveEvents)
    {
        if (stagedWaveEvents == null)
            return;

        stagedWaveEvents.Sort(CompareWaveEvents);
    }

    /// <summary>
    /// Resolves the paint color associated with one master preset.
    /// Used by scene gizmos and inspector previews.
    ///  masterPreset: Enemy master preset currently painted on a cell.
    /// returns Resolved paint color, or a default fallback when no visual preset is available.
    /// </summary>
    public static Color ResolvePaintColor(EnemyMasterPreset masterPreset)
    {
        if (masterPreset == null)
            return DefaultPaintColor;

        EnemyVisualPreset visualPreset = masterPreset.VisualPreset;

        if (visualPreset == null)
            return DefaultPaintColor;

        EnemyVisualPrefabSettings prefabSettings = visualPreset.Prefabs;

        if (prefabSettings == null)
            return DefaultPaintColor;

        return prefabSettings.SpawnPaintColor;
    }

    /// <summary>
    /// Resolves the prefab referenced by one master preset through its visual preset.
    ///  masterPreset: Enemy master preset to inspect.
    /// returns Resolved enemy prefab GameObject, or null when unavailable.
    /// </summary>
    public static GameObject ResolveEnemyPrefab(EnemyMasterPreset masterPreset)
    {
        if (masterPreset == null)
            return null;

        EnemyVisualPreset visualPreset = masterPreset.VisualPreset;

        if (visualPreset == null)
            return null;

        EnemyVisualPrefabSettings prefabSettings = visualPreset.Prefabs;

        if (prefabSettings == null)
            return null;

        return prefabSettings.EnemyPrefab;
    }

    /// <summary>
    /// Computes the total authored enemy count of one wave.
    /// Used by inspector summaries and validation logic.
    ///  wave: Wave to inspect.
    /// returns Total authored enemy count across all painted cells.
    /// </summary>
    public static int CountWaveEnemies(EnemySpawnWaveAuthoring wave)
    {
        if (wave == null || wave.PaintedCells == null)
            return 0;

        int totalEnemyCount = 0;

        for (int cellIndex = 0; cellIndex < wave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = wave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            totalEnemyCount += math.max(0, cell.EnemyCount);
        }

        return totalEnemyCount;
    }

    /// <summary>
    /// Computes the amount of distinct master presets painted inside one wave.
    /// Used by inspector summaries.
    ///  wave: Wave to inspect.
    /// returns Number of distinct enemy master presets referenced by painted cells.
    /// </summary>
    public static int CountWaveEnemyTypes(EnemySpawnWaveAuthoring wave)
    {
        if (wave == null || wave.PaintedCells == null)
            return 0;

        HashSet<EnemyMasterPreset> uniquePresets = new HashSet<EnemyMasterPreset>();

        for (int cellIndex = 0; cellIndex < wave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = wave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            if (cell.MasterPreset == null)
                continue;

            uniquePresets.Add(cell.MasterPreset);
        }

        return uniquePresets.Count;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Sanitizes one authored wave and its cells.
    ///  wave: Wave to sanitize.
    ///  gridSizeX: Grid width in cells.
    ///  gridSizeZ: Grid height in cells.
    ///  isFirstWave: Indicates whether the wave is the first authored wave in the array.
    /// returns None.
    /// </summary>
    private static void ValidateWave(EnemySpawnWaveAuthoring wave, int gridSizeX, int gridSizeZ, bool isFirstWave)
    {
        if (wave == null)
            return;

        wave.SetStartDelaySeconds(math.max(0f, wave.StartDelaySeconds));
        wave.SetSpawnDurationSeconds(math.max(0f, wave.SpawnDurationSeconds));
        wave.SetDefaultDistributionCurve(EnsureCurveReference(wave.DefaultDistributionCurve));

        if (isFirstWave && wave.StartMode != EnemyWaveStartMode.FromSpawnerStart)
            wave.SetStartMode(EnemyWaveStartMode.FromSpawnerStart);

        List<EnemySpawnWaveCellAuthoring> paintedCells = wave.PaintedCells;

        if (paintedCells == null)
            return;

        Dictionary<Vector2Int, EnemySpawnWaveCellAuthoring> lastCellByCoordinate = new Dictionary<Vector2Int, EnemySpawnWaveCellAuthoring>(paintedCells.Count);

        for (int cellIndex = 0; cellIndex < paintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = paintedCells[cellIndex];

            if (cell == null)
                continue;

            ValidateCell(cell, gridSizeX, gridSizeZ);
            lastCellByCoordinate[cell.CellCoordinate] = cell;
        }

        for (int cellIndex = paintedCells.Count - 1; cellIndex >= 0; cellIndex--)
        {
            EnemySpawnWaveCellAuthoring cell = paintedCells[cellIndex];

            if (cell == null)
            {
                paintedCells.RemoveAt(cellIndex);
                continue;
            }

            if (lastCellByCoordinate[cell.CellCoordinate] != cell)
                paintedCells.RemoveAt(cellIndex);
        }
    }

    /// <summary>
    /// Sanitizes one painted cell after inspector changes.
    ///  cell: Cell to sanitize.
    ///  gridSizeX: Grid width in cells.
    ///  gridSizeZ: Grid height in cells.
    /// returns None.
    /// </summary>
    private static void ValidateCell(EnemySpawnWaveCellAuthoring cell, int gridSizeX, int gridSizeZ)
    {
        if (cell == null)
            return;

        int maxX = math.max(0, gridSizeX - 1);
        int maxZ = math.max(0, gridSizeZ - 1);
        Vector2Int cellCoordinate = cell.CellCoordinate;
        cellCoordinate.x = math.clamp(cellCoordinate.x, 0, maxX);
        cellCoordinate.y = math.clamp(cellCoordinate.y, 0, maxZ);
        cell.SetCellCoordinate(cellCoordinate);
        cell.SetEnemyCount(math.max(1, cell.EnemyCount));
        cell.SetDistributionCurveOverride(EnsureCurveReference(cell.DistributionCurveOverride));
    }

    /// <summary>
    /// Ensures that one authored curve reference always points to a valid instance without
    /// destructively reshaping it during live inspector editing.
    ///  sourceCurve: Authored curve reference to validate.
    /// returns Original curve when valid, otherwise a default linear curve.
    /// </summary>
    private static AnimationCurve EnsureCurveReference(AnimationCurve sourceCurve)
    {
        if (sourceCurve == null || sourceCurve.length <= 0)
        {
            return CreateDefaultDistributionCurve();
        }

        return sourceCurve;
    }

    /// <summary>
    /// Returns a sanitized cumulative curve that stays inside the normalized domain.
    ///  sourceCurve: Authored curve to sanitize.
    /// returns Sanitized curve instance ready for sampling.
    /// </summary>
    private static AnimationCurve SanitizeCurve(AnimationCurve sourceCurve)
    {
        if (sourceCurve == null || sourceCurve.length <= 0)
            return CreateDefaultDistributionCurve();

        List<Keyframe> sanitizedKeys = new List<Keyframe>(sourceCurve.length + 2);
        Keyframe[] sourceKeys = sourceCurve.keys;

        for (int keyIndex = 0; keyIndex < sourceKeys.Length; keyIndex++)
        {
            Keyframe sourceKey = sourceKeys[keyIndex];
            Keyframe sanitizedKey = new Keyframe(Mathf.Clamp01(sourceKey.time), Mathf.Clamp01(sourceKey.value));
            sanitizedKeys.Add(sanitizedKey);
        }

        sanitizedKeys.Sort(CompareKeyframesByTime);

        if (sanitizedKeys.Count <= 0)
            return CreateDefaultDistributionCurve();

        if (sanitizedKeys[0].time > 0f)
            sanitizedKeys.Insert(0, new Keyframe(0f, 0f));
        else
            sanitizedKeys[0] = new Keyframe(0f, 0f);

        int lastIndex = sanitizedKeys.Count - 1;

        if (sanitizedKeys[lastIndex].time < 1f)
            sanitizedKeys.Add(new Keyframe(1f, 1f));
        else
            sanitizedKeys[lastIndex] = new Keyframe(1f, 1f);

        float previousValue = 0f;

        for (int keyIndex = 0; keyIndex < sanitizedKeys.Count; keyIndex++)
        {
            Keyframe sanitizedKey = sanitizedKeys[keyIndex];
            sanitizedKey.value = Mathf.Clamp01(Mathf.Max(previousValue, sanitizedKey.value));
            previousValue = sanitizedKey.value;
            sanitizedKeys[keyIndex] = sanitizedKey;
        }

        lastIndex = sanitizedKeys.Count - 1;
        sanitizedKeys[lastIndex] = new Keyframe(1f, 1f);
        return new AnimationCurve(sanitizedKeys.ToArray());
    }

    /// <summary>
    /// Builds a monotonic sampled cumulative curve used for inverse time lookup.
    ///  sourceCurve: Curve to sample.
    /// returns Sampled normalized cumulative values.
    /// </summary>
    private static float[] BuildMonotonicSamples(AnimationCurve sourceCurve)
    {
        float[] samples = new float[CurveSampleCount];
        AnimationCurve sanitizedCurve = SanitizeCurve(sourceCurve);
        float previousValue = 0f;

        for (int sampleIndex = 0; sampleIndex < CurveSampleCount; sampleIndex++)
        {
            float sampleTime = (float)sampleIndex / (CurveSampleCount - 1);
            float sampleValue = Mathf.Clamp01(sanitizedCurve.Evaluate(sampleTime));

            if (sampleValue < previousValue)
                sampleValue = previousValue;

            if (sampleIndex == 0)
                sampleValue = 0f;

            if (sampleIndex == CurveSampleCount - 1)
                sampleValue = 1f;

            samples[sampleIndex] = sampleValue;
            previousValue = sampleValue;
        }

        return samples;
    }

    /// <summary>
    /// Resolves the normalized time whose sampled cumulative value matches the provided target quantile.
    ///  samples: Monotonic cumulative samples in the range [0, 1].
    ///  targetValue: Normalized target quantile to invert.
    /// returns Normalized time in the range [0, 1].
    /// </summary>
    private static float ResolveInverseSampleTime(float[] samples, float targetValue)
    {
        if (samples == null || samples.Length <= 1)
            return 0f;

        float clampedTargetValue = Mathf.Clamp01(targetValue);

        for (int sampleIndex = 1; sampleIndex < samples.Length; sampleIndex++)
        {
            float currentValue = samples[sampleIndex];

            if (currentValue < clampedTargetValue)
                continue;

            float previousValue = samples[sampleIndex - 1];
            float previousTime = (float)(sampleIndex - 1) / (samples.Length - 1);
            float currentTime = (float)sampleIndex / (samples.Length - 1);

            if (currentValue <= previousValue)
                return currentTime;

            float interpolation = Mathf.InverseLerp(previousValue, currentValue, clampedTargetValue);
            return Mathf.Lerp(previousTime, currentTime, interpolation);
        }

        return 1f;
    }

    /// <summary>
    /// Compares two staged wave events using relative time first and prefab entity index second.
    ///  left: Left event.
    ///  right: Right event.
    /// returns Standard comparison result.
    /// </summary>
    private static int CompareWaveEvents(EnemySpawnerWaveEventElement left, EnemySpawnerWaveEventElement right)
    {
        int relativeTimeComparison = left.RelativeTime.CompareTo(right.RelativeTime);

        if (relativeTimeComparison != 0)
            return relativeTimeComparison;

        return left.PrefabEntity.Index.CompareTo(right.PrefabEntity.Index);
    }

    /// <summary>
    /// Compares two curve keyframes by time.
    ///  left: Left keyframe.
    ///  right: Right keyframe.
    /// returns Standard comparison result.
    /// </summary>
    private static int CompareKeyframesByTime(Keyframe left, Keyframe right)
    {
        return left.time.CompareTo(right.time);
    }
    #endregion

    #endregion
}
