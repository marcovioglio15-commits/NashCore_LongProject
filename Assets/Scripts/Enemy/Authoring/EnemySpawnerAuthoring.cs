using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Stores one painted spawn cell inside a wave-authored enemy spawner grid.
/// </summary>
[Serializable]
public sealed class EnemySpawnWaveCellAuthoring
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Grid coordinate of the painted spawn cell. X is horizontal, Y is depth on the spawner local XZ plane.")]
    [SerializeField] private Vector2Int cellCoordinate;

    [Tooltip("Enemy master preset painted on the cell. The visual preset of this master preset resolves the concrete enemy prefab.")]
    [SerializeField] private EnemyMasterPreset masterPreset;

    [Tooltip("Total amount of enemies of this type emitted by this cell across the wave spawn duration.")]
    [SerializeField] private int enemyCount = 1;

    [Tooltip("When enabled, this cell uses the default wave distribution curve instead of its local override.")]
    [SerializeField] private bool useWaveDefaultDistribution = true;

    [Tooltip("Optional per-cell cumulative distribution curve used only when Use Wave Default Distribution is disabled.")]
    [SerializeField] private AnimationCurve distributionCurveOverride = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    #endregion

    #endregion

    #region Properties
    public Vector2Int CellCoordinate
    {
        get
        {
            return cellCoordinate;
        }
    }

    public EnemyMasterPreset MasterPreset
    {
        get
        {
            return masterPreset;
        }
    }

    public int EnemyCount
    {
        get
        {
            return enemyCount;
        }
    }

    public bool UseWaveDefaultDistribution
    {
        get
        {
            return useWaveDefaultDistribution;
        }
    }

    public AnimationCurve DistributionCurveOverride
    {
        get
        {
            return distributionCurveOverride;
        }
    }
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Updates the authored grid coordinate.
    /// Used by validation and editor painting tools.
    /// /params value: New grid coordinate.
    /// /returns None.
    /// </summary>
    internal void SetCellCoordinate(Vector2Int value)
    {
        cellCoordinate = value;
    }

    /// <summary>
    /// Updates the authored enemy count.
    /// Used by validation and dedicated cell editing UI.
    /// /params value: New total enemy count.
    /// /returns None.
    /// </summary>
    internal void SetEnemyCount(int value)
    {
        enemyCount = value;
    }

    /// <summary>
    /// Updates the authored master preset.
    /// Used by inspector painting tools.
    /// /params value: New master preset assignment.
    /// /returns None.
    /// </summary>
    internal void SetMasterPreset(EnemyMasterPreset value)
    {
        masterPreset = value;
    }

    /// <summary>
    /// Updates the curve-usage mode for the cell.
    /// Used by inspector cell editing UI.
    /// /params value: New flag controlling default-vs-override curve usage.
    /// /returns None.
    /// </summary>
    internal void SetUseWaveDefaultDistribution(bool value)
    {
        useWaveDefaultDistribution = value;
    }

    /// <summary>
    /// Updates the authored local curve override.
    /// Used by validation and dedicated cell editing UI.
    /// /params value: New local override curve.
    /// /returns None.
    /// </summary>
    internal void SetDistributionCurveOverride(AnimationCurve value)
    {
        distributionCurveOverride = value;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one finite wave authored for the enemy spawner grid.
/// </summary>
[Serializable]
public sealed class EnemySpawnWaveAuthoring
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Optional label used in the inspector to identify this wave.")]
    [SerializeField] private string waveLabel = "Wave";

    [Tooltip("When enabled, this is the only wave shown in scene previews and gizmos.")]
    [SerializeField] private bool previewInScene;

    [Tooltip("Reference event used to start the wave delay countdown.")]
    [SerializeField] private EnemyWaveStartMode startMode = EnemyWaveStartMode.FromSpawnerStart;

    [Tooltip("Delay in seconds applied after the selected start reference before this wave begins.")]
    [SerializeField] private float startDelaySeconds;

    [Tooltip("Duration in seconds over which all enemies authored in this wave are distributed.")]
    [SerializeField] private float spawnDurationSeconds = 4f;

    [Tooltip("Default cumulative distribution curve used by cells that do not override it locally.")]
    [SerializeField] private AnimationCurve defaultDistributionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Sparse list of painted spawn cells used by this wave.")]
    [SerializeField] private List<EnemySpawnWaveCellAuthoring> paintedCells = new List<EnemySpawnWaveCellAuthoring>();
    #endregion

    #endregion

    #region Properties
    public string WaveLabel
    {
        get
        {
            return waveLabel;
        }
    }

    public bool PreviewInScene
    {
        get
        {
            return previewInScene;
        }
    }

    public EnemyWaveStartMode StartMode
    {
        get
        {
            return startMode;
        }
    }

    public float StartDelaySeconds
    {
        get
        {
            return startDelaySeconds;
        }
    }

    public float SpawnDurationSeconds
    {
        get
        {
            return spawnDurationSeconds;
        }
    }

    public AnimationCurve DefaultDistributionCurve
    {
        get
        {
            return defaultDistributionCurve;
        }
    }

    public List<EnemySpawnWaveCellAuthoring> PaintedCells
    {
        get
        {
            return paintedCells;
        }
    }
    #endregion

    #region Methods

    #region Internal Methods
    /// <summary>
    /// Updates the preview flag used by scene gizmos.
    /// /params value: New preview state.
    /// /returns None.
    /// </summary>
    internal void SetPreviewInScene(bool value)
    {
        previewInScene = value;
    }

    /// <summary>
    /// Updates the authored start mode.
    /// /params value: New start mode.
    /// /returns None.
    /// </summary>
    internal void SetStartMode(EnemyWaveStartMode value)
    {
        startMode = value;
    }

    /// <summary>
    /// Updates the authored start delay.
    /// /params value: New delay in seconds.
    /// /returns None.
    /// </summary>
    internal void SetStartDelaySeconds(float value)
    {
        startDelaySeconds = value;
    }

    /// <summary>
    /// Updates the authored spawn duration.
    /// /params value: New duration in seconds.
    /// /returns None.
    /// </summary>
    internal void SetSpawnDurationSeconds(float value)
    {
        spawnDurationSeconds = value;
    }

    /// <summary>
    /// Updates the default wave curve.
    /// /params value: New cumulative distribution curve.
    /// /returns None.
    /// </summary>
    internal void SetDefaultDistributionCurve(AnimationCurve value)
    {
        defaultDistributionCurve = value;
    }
    #endregion

    #endregion
}

/// <summary>
/// Authoring component that defines a finite wave-based enemy spawn grid.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawnerAuthoring : MonoBehaviour
{
    #region Fields

#if UNITY_EDITOR
    private static GUIStyle sceneCoordinateLabelStyle;
    private static GUIStyle sceneCountLabelStyle;
#endif

    #region Serialized Fields
    [Header("Grid")]
    [Tooltip("Grid width in cells on the local X axis.")]
    [SerializeField] private int gridSizeX = 12;

    [Tooltip("Grid depth in cells on the local Z axis.")]
    [SerializeField] private int gridSizeZ = 12;

    [Tooltip("Square cell size in local units.")]
    [SerializeField] private float cellSize = 2f;

    [Tooltip("Local-space offset applied to the full grid origin before cell placement.")]
    [SerializeField] private Vector3 originOffset;

    [Tooltip("Additional local-space height offset applied to all baked spawn positions.")]
    [SerializeField] private float spawnHeightOffset;

    [Header("Pool")]
    [Tooltip("Amount of enemies prewarmed for each unique prefab referenced by this spawner.")]
    [SerializeField] private int initialPoolCapacityPerPrefab = 16;

    [Tooltip("Amount of enemies instantiated whenever a prefab-specific pool needs expansion.")]
    [SerializeField] private int expandBatchPerPrefab = 8;

    [Header("Lifecycle")]
    [Tooltip("Distance from the player beyond which alive enemies are returned to their pool. Set to 0 to disable.")]
    [SerializeField] private float despawnDistance = 85f;

    [Header("Waves")]
    [Tooltip("Finite sequence of authored waves emitted by this spawner.")]
    [SerializeField] private List<EnemySpawnWaveAuthoring> waves = new List<EnemySpawnWaveAuthoring>();

    [Header("Debug")]
    [Tooltip("Draw the authored grid and preview wave gizmos when the spawner is selected.")]
    [SerializeField] private bool drawGridGizmos = true;

    [Tooltip("Draw grid coordinates beside painted preview cells.")]
    [SerializeField] private bool drawCellCoordinates = true;

    [Tooltip("Draw authored enemy counts beside painted preview cells.")]
    [SerializeField] private bool drawCellCounts = true;
    #endregion

    #endregion

    #region Properties
    public int GridSizeX
    {
        get
        {
            return gridSizeX;
        }
    }

    public int GridSizeZ
    {
        get
        {
            return gridSizeZ;
        }
    }

    public float CellSize
    {
        get
        {
            return cellSize;
        }
    }

    public Vector3 OriginOffset
    {
        get
        {
            return originOffset;
        }
    }

    public float SpawnHeightOffset
    {
        get
        {
            return spawnHeightOffset;
        }
    }

    public int InitialPoolCapacityPerPrefab
    {
        get
        {
            return initialPoolCapacityPerPrefab;
        }
    }

    public int ExpandBatchPerPrefab
    {
        get
        {
            return expandBatchPerPrefab;
        }
    }

    public float DespawnDistance
    {
        get
        {
            return despawnDistance;
        }
    }

    public List<EnemySpawnWaveAuthoring> Waves
    {
        get
        {
            return waves;
        }
    }

    public bool DrawGridGizmos
    {
        get
        {
            return drawGridGizmos;
        }
    }

    public bool DrawCellCoordinates
    {
        get
        {
            return drawCellCoordinates;
        }
    }

    public bool DrawCellCounts
    {
        get
        {
            return drawCellCounts;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Sanitizes serialized values and wave data after inspector edits.
    /// /returns None.
    /// </summary>
    private void OnValidate()
    {
        if (gridSizeX < 1)
            gridSizeX = 1;

        if (gridSizeZ < 1)
            gridSizeZ = 1;

        if (cellSize < 0.1f)
            cellSize = 0.1f;

        if (initialPoolCapacityPerPrefab < 0)
            initialPoolCapacityPerPrefab = 0;

        if (expandBatchPerPrefab < 1)
            expandBatchPerPrefab = 1;

        if (despawnDistance < 0f)
            despawnDistance = 0f;

        if (waves == null)
            waves = new List<EnemySpawnWaveAuthoring>();

        EnemySpawnerWaveBakeUtility.ValidateWaves(waves, gridSizeX, gridSizeZ);
    }

    /// <summary>
    /// Draws selected-scene gizmos for the grid and currently previewed wave.
    /// /returns None.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawGridGizmos)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        DrawGridGizmoLines();
        DrawPreviewWaveGizmos();
#if UNITY_EDITOR
        DrawSceneOverlayLabels();
#endif
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Resolves the local-space center of one authored grid cell.
    /// Used by the baker and by scene preview drawing.
    /// /params cellCoordinate: Authored grid coordinate to resolve.
    /// /returns Local-space center of the requested cell.
    /// </summary>
    public float3 ResolveCellLocalCenter(Vector2Int cellCoordinate)
    {
        return EnemySpawnerWaveBakeUtility.ResolveCellLocalCenter(gridSizeX,
                                                                  gridSizeZ,
                                                                  cellSize,
                                                                  originOffset,
                                                                  spawnHeightOffset,
                                                                  cellCoordinate);
    }

    /// <summary>
    /// Tries to resolve the single wave currently flagged for scene preview.
    /// Used by gizmos and by the custom editor.
    /// /params waveIndex: Resolved preview wave index, or -1 when none is active.
    /// /returns True when a preview wave exists, otherwise false.
    /// </summary>
    public bool TryGetPreviewWaveIndex(out int waveIndex)
    {
        if (waves != null)
        {
            for (int index = 0; index < waves.Count; index++)
            {
                EnemySpawnWaveAuthoring wave = waves[index];

                if (wave == null)
                    continue;

                if (!wave.PreviewInScene)
                    continue;

                waveIndex = index;
                return true;
            }
        }

        waveIndex = -1;
        return false;
    }
    #endregion

    #region Gizmos
    /// <summary>
    /// Draws the grid wireframe in local space.
    /// /returns None.
    /// </summary>
    private void DrawGridGizmoLines()
    {
        Gizmos.color = new Color(0.35f, 0.65f, 1f, 0.45f);

        for (int x = 0; x <= gridSizeX; x++)
        {
            float offsetX = (x - gridSizeX * 0.5f) * cellSize;
            Vector3 start = originOffset + new Vector3(offsetX, spawnHeightOffset, -gridSizeZ * 0.5f * cellSize);
            Vector3 end = originOffset + new Vector3(offsetX, spawnHeightOffset, gridSizeZ * 0.5f * cellSize);
            Gizmos.DrawLine(start, end);
        }

        for (int z = 0; z <= gridSizeZ; z++)
        {
            float offsetZ = (z - gridSizeZ * 0.5f) * cellSize;
            Vector3 start = originOffset + new Vector3(-gridSizeX * 0.5f * cellSize, spawnHeightOffset, offsetZ);
            Vector3 end = originOffset + new Vector3(gridSizeX * 0.5f * cellSize, spawnHeightOffset, offsetZ);
            Gizmos.DrawLine(start, end);
        }
    }

    /// <summary>
    /// Draws painted preview cells for the currently selected wave.
    /// /returns None.
    /// </summary>
    private void DrawPreviewWaveGizmos()
    {
        int previewWaveIndex;

        if (!TryGetPreviewWaveIndex(out previewWaveIndex))
            return;

        if (previewWaveIndex < 0 || previewWaveIndex >= waves.Count)
            return;

        EnemySpawnWaveAuthoring previewWave = waves[previewWaveIndex];

        if (previewWave == null || previewWave.PaintedCells == null)
            return;

        for (int cellIndex = 0; cellIndex < previewWave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = previewWave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            float3 localCenterValue = ResolveCellLocalCenter(cell.CellCoordinate);
            Vector3 localCenter = new Vector3(localCenterValue.x, localCenterValue.y, localCenterValue.z);
            Color fillColor = EnemySpawnerWaveBakeUtility.ResolvePaintColor(cell.MasterPreset);
            fillColor.a = 0.35f;
            Gizmos.color = fillColor;
            Gizmos.DrawCube(localCenter, new Vector3(cellSize * 0.82f, 0.04f, cellSize * 0.82f));
            Gizmos.color = new Color(fillColor.r, fillColor.g, fillColor.b, 0.95f);
            Gizmos.DrawWireCube(localCenter, new Vector3(cellSize * 0.82f, 0.08f, cellSize * 0.82f));
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws screen-space overlays for grid coordinates and painted-cell counts.
    /// /returns None.
    /// </summary>
    private void DrawSceneOverlayLabels()
    {
        if (!drawCellCoordinates && !drawCellCounts)
            return;

        Handles.BeginGUI();

        if (drawCellCoordinates)
        {
            DrawGridCoordinateLabels();
        }

        if (drawCellCounts)
        {
            DrawPreviewCellCountLabels();
        }

        Handles.EndGUI();
    }

    /// <summary>
    /// Draws the coordinate label of every authored grid node while the spawner is selected.
    /// /returns None.
    /// </summary>
    private void DrawGridCoordinateLabels()
    {
        GUIStyle coordinateStyle = GetSceneCoordinateLabelStyle();

        for (int z = gridSizeZ - 1; z >= 0; z--)
        {
            for (int x = 0; x < gridSizeX; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                float3 localCenterValue = ResolveCellLocalCenter(coordinate);
                Vector3 localCenter = new Vector3(localCenterValue.x, localCenterValue.y, localCenterValue.z);
                Vector3 worldCenter = transform.TransformPoint(localCenter);
                Vector3 screenPoint = HandleUtility.WorldToGUIPointWithDepth(worldCenter);

                if (screenPoint.z < 0f)
                    continue;

                DrawSceneBadge(new Vector2(screenPoint.x, screenPoint.y + 16f),
                               "[" + x + "," + z + "]",
                               coordinateStyle,
                               new Color(0.08f, 0.13f, 0.2f, 0.88f),
                               new Color(0.4f, 0.72f, 1f, 0.92f),
                               54f,
                               20f);
            }
        }
    }

    /// <summary>
    /// Draws the optional enemy-count label for every painted preview cell.
    /// /returns None.
    /// </summary>
    private void DrawPreviewCellCountLabels()
    {
        int previewWaveIndex;

        if (!TryGetPreviewWaveIndex(out previewWaveIndex))
            return;

        if (previewWaveIndex < 0 || previewWaveIndex >= waves.Count)
            return;

        EnemySpawnWaveAuthoring previewWave = waves[previewWaveIndex];

        if (previewWave == null || previewWave.PaintedCells == null)
            return;

        GUIStyle countStyle = GetSceneCountLabelStyle();

        for (int cellIndex = 0; cellIndex < previewWave.PaintedCells.Count; cellIndex++)
        {
            EnemySpawnWaveCellAuthoring cell = previewWave.PaintedCells[cellIndex];

            if (cell == null)
                continue;

            float3 localCenterValue = ResolveCellLocalCenter(cell.CellCoordinate);
            Vector3 localCenter = new Vector3(localCenterValue.x, localCenterValue.y, localCenterValue.z);
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 screenPoint = HandleUtility.WorldToGUIPointWithDepth(worldCenter);

            if (screenPoint.z < 0f)
                continue;

            Color badgeColor = EnemySpawnerWaveBakeUtility.ResolvePaintColor(cell.MasterPreset);
            badgeColor.a = 0.92f;
            DrawSceneBadge(new Vector2(screenPoint.x, screenPoint.y - 20f),
                           "x" + math.max(0, cell.EnemyCount),
                           countStyle,
                           badgeColor,
                           new Color(1f, 1f, 1f, 0.92f),
                           40f,
                           20f);
        }
    }

    /// <summary>
    /// Draws one centered screen-space badge used by scene overlays.
    /// /params screenCenter: GUI-space center of the badge.
    /// /params label: Text displayed inside the badge.
    /// /params style: GUI style used to draw the text.
    /// /params backgroundColor: Fill color of the badge.
    /// /params borderColor: Outline color of the badge.
    /// /params minWidth: Minimum badge width in pixels.
    /// /params height: Badge height in pixels.
    /// /returns None.
    /// </summary>
    private static void DrawSceneBadge(Vector2 screenCenter,
                                       string label,
                                       GUIStyle style,
                                       Color backgroundColor,
                                       Color borderColor,
                                       float minWidth,
                                       float height)
    {
        if (string.IsNullOrEmpty(label))
            return;

        GUIContent badgeContent = new GUIContent(label);
        Vector2 textSize = style.CalcSize(badgeContent);
        float width = Mathf.Max(minWidth, textSize.x + 10f);
        Rect badgeRect = new Rect(screenCenter.x - width * 0.5f,
                                  screenCenter.y - height * 0.5f,
                                  width,
                                  height);
        EditorGUI.DrawRect(badgeRect, backgroundColor);
        EditorGUI.DrawRect(new Rect(badgeRect.xMin, badgeRect.yMin, badgeRect.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(badgeRect.xMin, badgeRect.yMax - 1f, badgeRect.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(badgeRect.xMin, badgeRect.yMin, 1f, badgeRect.height), borderColor);
        EditorGUI.DrawRect(new Rect(badgeRect.xMax - 1f, badgeRect.yMin, 1f, badgeRect.height), borderColor);
        GUI.Label(badgeRect, badgeContent, style);
    }

    /// <summary>
    /// Returns the cached style used for scene coordinate overlays.
    /// /returns GUI style used by grid coordinate labels.
    /// </summary>
    private static GUIStyle GetSceneCoordinateLabelStyle()
    {
        if (sceneCoordinateLabelStyle != null)
            return sceneCoordinateLabelStyle;

        sceneCoordinateLabelStyle = new GUIStyle(EditorStyles.whiteMiniLabel);
        sceneCoordinateLabelStyle.fontSize = 12;
        sceneCoordinateLabelStyle.fontStyle = FontStyle.Bold;
        sceneCoordinateLabelStyle.normal.textColor = new Color(0.95f, 0.98f, 1f, 0.98f);
        sceneCoordinateLabelStyle.alignment = TextAnchor.MiddleCenter;
        return sceneCoordinateLabelStyle;
    }

    /// <summary>
    /// Returns the cached style used for painted-cell enemy-count overlays.
    /// /returns GUI style used by painted-cell count labels.
    /// </summary>
    private static GUIStyle GetSceneCountLabelStyle()
    {
        if (sceneCountLabelStyle != null)
            return sceneCountLabelStyle;

        sceneCountLabelStyle = new GUIStyle(EditorStyles.whiteMiniLabel);
        sceneCountLabelStyle.fontSize = 13;
        sceneCountLabelStyle.fontStyle = FontStyle.Bold;
        sceneCountLabelStyle.normal.textColor = Color.white;
        sceneCountLabelStyle.alignment = TextAnchor.MiddleCenter;
        return sceneCountLabelStyle;
    }
#endif
    #endregion

    #endregion
}
