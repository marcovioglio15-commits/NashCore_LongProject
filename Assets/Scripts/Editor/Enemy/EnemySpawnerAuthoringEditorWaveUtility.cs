using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stores immutable preview data used to draw one grid cell without keeping SerializedProperty handles alive.
/// </summary>
public readonly struct EnemySpawnerGridCellPreviewData
{
    #region Fields
    public readonly int EnemyCount;
    public readonly Color FillColor;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates one immutable preview snapshot for a painted grid cell.
    /// /params enemyCount: Authored enemy count of the cell.
    /// /params fillColor: Resolved paint color of the cell.
    /// /returns None.
    /// </summary>
    public EnemySpawnerGridCellPreviewData(int enemyCount, Color fillColor)
    {
        EnemyCount = enemyCount;
        FillColor = fillColor;
    }
    #endregion
}

/// <summary>
/// Centralizes serialized wave and cell mutations used by EnemySpawnerAuthoringEditor.
/// </summary>
public static class EnemySpawnerAuthoringEditorWaveUtility
{
    #region Methods

    #region Lookup
    /// <summary>
    /// Returns the painted-cells property of one wave.
    /// /params wavesProperty: Serialized waves array.
    /// /params waveIndex: Wave index to inspect.
    /// /returns Painted cells property, or null when the wave index is invalid.
    /// </summary>
    public static SerializedProperty GetPaintedCellsProperty(SerializedProperty wavesProperty, int waveIndex)
    {
        if (wavesProperty == null)
            return null;

        if (waveIndex < 0 || waveIndex >= wavesProperty.arraySize)
            return null;

        SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
        return waveProperty.FindPropertyRelative("paintedCells");
    }

    /// <summary>
    /// Finds one painted cell property by coordinate.
    /// /params wavesProperty: Serialized waves array.
    /// /params waveIndex: Wave index to inspect.
    /// /params coordinate: Target coordinate.
    /// /returns Serialized property representing the painted cell, or null when it does not exist.
    /// </summary>
    public static SerializedProperty FindCellProperty(SerializedProperty wavesProperty, int waveIndex, Vector2Int coordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return null;

        int existingCellIndex = FindCellIndex(paintedCellsProperty, coordinate);

        if (existingCellIndex < 0)
            return null;

        return paintedCellsProperty.GetArrayElementAtIndex(existingCellIndex);
    }

    /// <summary>
    /// Finds the array index of one painted cell by grid coordinate.
    /// /params paintedCellsProperty: Serialized array of painted cells.
    /// /params coordinate: Target coordinate.
    /// /returns Index of the painted cell, or -1 when not found.
    /// </summary>
    public static int FindCellIndex(SerializedProperty paintedCellsProperty, Vector2Int coordinate)
    {
        if (paintedCellsProperty == null)
            return -1;

        for (int cellIndex = 0; cellIndex < paintedCellsProperty.arraySize; cellIndex++)
        {
            SerializedProperty cellProperty = paintedCellsProperty.GetArrayElementAtIndex(cellIndex);

            if (cellProperty == null)
                continue;

            if (cellProperty.FindPropertyRelative("cellCoordinate").vector2IntValue == coordinate)
                return cellIndex;
        }

        return -1;
    }

    /// <summary>
    /// Builds a coordinate lookup for existing painted cells of the current wave.
    /// /params paintedCellsProperty: Serialized array of painted cells.
    /// /returns Coordinate-to-preview-data lookup.
    /// </summary>
    public static Dictionary<Vector2Int, EnemySpawnerGridCellPreviewData> BuildCellPreviewMap(SerializedProperty paintedCellsProperty)
    {
        Dictionary<Vector2Int, EnemySpawnerGridCellPreviewData> cellPreviewByCoordinate = new Dictionary<Vector2Int, EnemySpawnerGridCellPreviewData>();

        if (paintedCellsProperty == null)
            return cellPreviewByCoordinate;

        for (int cellIndex = 0; cellIndex < paintedCellsProperty.arraySize; cellIndex++)
        {
            SerializedProperty cellProperty = paintedCellsProperty.GetArrayElementAtIndex(cellIndex);

            if (cellProperty == null)
                continue;

            Vector2Int coordinate = cellProperty.FindPropertyRelative("cellCoordinate").vector2IntValue;
            int enemyCount = Mathf.Max(0, cellProperty.FindPropertyRelative("enemyCount").intValue);
            EnemyMasterPreset masterPreset = cellProperty.FindPropertyRelative("masterPreset").objectReferenceValue as EnemyMasterPreset;
            Color color = EnemySpawnerWaveBakeUtility.ResolvePaintColor(masterPreset);
            color.a = 0.9f;
            cellPreviewByCoordinate[coordinate] = new EnemySpawnerGridCellPreviewData(enemyCount, color);
        }

        return cellPreviewByCoordinate;
    }

    /// <summary>
    /// Creates a deep clone of an animation curve while preserving wrap modes.
    /// /params sourceCurve: Source curve to duplicate.
    /// /returns Cloned curve, or a default linear curve when the source is null.
    /// </summary>
    public static AnimationCurve CloneAnimationCurve(AnimationCurve sourceCurve)
    {
        AnimationCurve clonedCurve = sourceCurve == null
            ? EnemySpawnerWaveBakeUtility.CreateDefaultDistributionCurve()
            : new AnimationCurve(sourceCurve.keys);

        if (sourceCurve != null)
        {
            clonedCurve.preWrapMode = sourceCurve.preWrapMode;
            clonedCurve.postWrapMode = sourceCurve.postWrapMode;
        }

        return clonedCurve;
    }
    #endregion

    #region Cell Mutation
    /// <summary>
    /// Paints or erases one cell depending on the current brush mode.
    /// /params serializedObject: Serialized object backing the editor.
    /// /params targetObject: Unity object marked dirty after mutation.
    /// /params wavesProperty: Serialized waves array.
    /// /params waveIndex: Wave index receiving the change.
    /// /params coordinate: Target grid coordinate.
    /// /params eraseMode: True to erase instead of paint.
    /// /params brushMasterPreset: Master preset assigned while painting.
    /// /params brushEnemyCount: Enemy count assigned while painting.
    /// /params brushDistributionCurve: Default curve copied into new cells.
    /// /params selectedWaveIndex: Current selected wave index, updated by the mutation.
    /// /params selectedCellCoordinate: Current selected coordinate, updated by the mutation.
    /// /returns True when the serialized data changed, otherwise false.
    /// </summary>
    public static bool PaintCell(SerializedObject serializedObject,
                                 Object targetObject,
                                 SerializedProperty wavesProperty,
                                 int waveIndex,
                                 Vector2Int coordinate,
                                 bool eraseMode,
                                 EnemyMasterPreset brushMasterPreset,
                                 int brushEnemyCount,
                                 AnimationCurve brushDistributionCurve,
                                 ref int selectedWaveIndex,
                                 ref Vector2Int selectedCellCoordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return false;

        int existingCellIndex = FindCellIndex(paintedCellsProperty, coordinate);

        if (eraseMode)
        {
            if (existingCellIndex < 0)
                return false;

            paintedCellsProperty.DeleteArrayElementAtIndex(existingCellIndex);

            if (selectedWaveIndex == waveIndex && selectedCellCoordinate == coordinate)
            {
                selectedWaveIndex = -1;
                selectedCellCoordinate = new Vector2Int(-1, -1);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
            return true;
        }

        if (brushMasterPreset == null)
            return false;

        SerializedProperty cellProperty;

        if (existingCellIndex >= 0)
            cellProperty = paintedCellsProperty.GetArrayElementAtIndex(existingCellIndex);
        else
        {
            int newIndex = paintedCellsProperty.arraySize;
            paintedCellsProperty.InsertArrayElementAtIndex(newIndex);
            cellProperty = paintedCellsProperty.GetArrayElementAtIndex(newIndex);
        }

        cellProperty.FindPropertyRelative("cellCoordinate").vector2IntValue = coordinate;
        cellProperty.FindPropertyRelative("masterPreset").objectReferenceValue = brushMasterPreset;
        cellProperty.FindPropertyRelative("enemyCount").intValue = Mathf.Max(1, brushEnemyCount);
        cellProperty.FindPropertyRelative("useWaveDefaultDistribution").boolValue = false;
        cellProperty.FindPropertyRelative("distributionCurveOverride").animationCurveValue = CloneAnimationCurve(brushDistributionCurve);

        selectedWaveIndex = waveIndex;
        selectedCellCoordinate = coordinate;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
        return true;
    }

    /// <summary>
    /// Selects one painted cell for detailed editing.
    /// /params wavesProperty: Serialized waves array.
    /// /params waveIndex: Wave index containing the cell.
    /// /params coordinate: Grid coordinate of the selected cell.
    /// /params selectedWaveIndex: Current selected wave index, updated by the selection.
    /// /params selectedCellCoordinate: Current selected coordinate, updated by the selection.
    /// /returns True when the requested cell exists, otherwise false.
    /// </summary>
    public static bool SelectCell(SerializedProperty wavesProperty,
                                  int waveIndex,
                                  Vector2Int coordinate,
                                  ref int selectedWaveIndex,
                                  ref Vector2Int selectedCellCoordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return false;

        int existingCellIndex = FindCellIndex(paintedCellsProperty, coordinate);

        if (existingCellIndex < 0)
        {
            selectedWaveIndex = -1;
            selectedCellCoordinate = new Vector2Int(-1, -1);
            return false;
        }

        selectedWaveIndex = waveIndex;
        selectedCellCoordinate = coordinate;
        return true;
    }

    /// <summary>
    /// Removes one painted cell from the requested wave.
    /// /params serializedObject: Serialized object backing the editor.
    /// /params targetObject: Unity object marked dirty after mutation.
    /// /params wavesProperty: Serialized waves array.
    /// /params waveIndex: Wave index containing the cell.
    /// /params coordinate: Grid coordinate to remove.
    /// /params selectedWaveIndex: Current selected wave index, updated by the mutation.
    /// /params selectedCellCoordinate: Current selected coordinate, updated by the mutation.
    /// /returns True when the cell existed and was removed, otherwise false.
    /// </summary>
    public static bool RemoveCell(SerializedObject serializedObject,
                                  Object targetObject,
                                  SerializedProperty wavesProperty,
                                  int waveIndex,
                                  Vector2Int coordinate,
                                  ref int selectedWaveIndex,
                                  ref Vector2Int selectedCellCoordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return false;

        int existingCellIndex = FindCellIndex(paintedCellsProperty, coordinate);

        if (existingCellIndex < 0)
            return false;

        paintedCellsProperty.DeleteArrayElementAtIndex(existingCellIndex);
        selectedWaveIndex = -1;
        selectedCellCoordinate = new Vector2Int(-1, -1);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
        return true;
    }
    #endregion

    #region Wave Mutation
    /// <summary>
    /// Appends one new empty wave to the serialized wave array.
    /// /params serializedObject: Serialized object backing the editor.
    /// /params targetObject: Unity object marked dirty after mutation.
    /// /params wavesProperty: Serialized waves array.
    /// /params waveFoldoutState: Foldout-state cache updated for the new wave.
    /// /returns None.
    /// </summary>
    public static void AddWave(SerializedObject serializedObject,
                               Object targetObject,
                               SerializedProperty wavesProperty,
                               Dictionary<int, bool> waveFoldoutState)
    {
        if (wavesProperty == null)
            return;

        int newWaveIndex = wavesProperty.arraySize;
        wavesProperty.InsertArrayElementAtIndex(newWaveIndex);
        SerializedProperty newWaveProperty = wavesProperty.GetArrayElementAtIndex(newWaveIndex);
        newWaveProperty.FindPropertyRelative("waveLabel").stringValue = "Wave " + (newWaveIndex + 1);
        newWaveProperty.FindPropertyRelative("previewInScene").boolValue = wavesProperty.arraySize == 1;
        newWaveProperty.FindPropertyRelative("startMode").enumValueIndex = newWaveIndex == 0
            ? (int)EnemyWaveStartMode.FromSpawnerStart
            : (int)EnemyWaveStartMode.AfterPreviousWaveCompleted;
        newWaveProperty.FindPropertyRelative("startDelaySeconds").floatValue = 0f;
        newWaveProperty.FindPropertyRelative("spawnDurationSeconds").floatValue = 4f;
        newWaveProperty.FindPropertyRelative("defaultDistributionCurve").animationCurveValue = EnemySpawnerWaveBakeUtility.CreateDefaultDistributionCurve();
        SerializedProperty paintedCellsProperty = newWaveProperty.FindPropertyRelative("paintedCells");
        paintedCellsProperty.ClearArray();
        waveFoldoutState[newWaveIndex] = true;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
    }

    /// <summary>
    /// Deletes one wave from the serialized array.
    /// /params serializedObject: Serialized object backing the editor.
    /// /params targetObject: Unity object marked dirty after mutation.
    /// /params wavesProperty: Serialized waves array.
    /// /params waveIndex: Index of the wave to delete.
    /// /params selectedWaveIndex: Current selected wave index, updated by the mutation.
    /// /params selectedCellCoordinate: Current selected coordinate, updated by the mutation.
    /// /returns True when the wave existed and was removed, otherwise false.
    /// </summary>
    public static bool DeleteWave(SerializedObject serializedObject,
                                  Object targetObject,
                                  SerializedProperty wavesProperty,
                                  int waveIndex,
                                  ref int selectedWaveIndex,
                                  ref Vector2Int selectedCellCoordinate)
    {
        if (wavesProperty == null || waveIndex < 0 || waveIndex >= wavesProperty.arraySize)
            return false;

        wavesProperty.DeleteArrayElementAtIndex(waveIndex);

        if (selectedWaveIndex == waveIndex)
        {
            selectedWaveIndex = -1;
            selectedCellCoordinate = new Vector2Int(-1, -1);
        }
        else if (selectedWaveIndex > waveIndex)
            selectedWaveIndex--;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
        return true;
    }

    /// <summary>
    /// Clears all painted cells from the requested wave.
    /// /params serializedObject: Serialized object backing the editor.
    /// /params targetObject: Unity object marked dirty after mutation.
    /// /params wavesProperty: Serialized waves array.
    /// /params waveIndex: Index of the wave to clear.
    /// /params selectedWaveIndex: Current selected wave index, updated by the mutation.
    /// /params selectedCellCoordinate: Current selected coordinate, updated by the mutation.
    /// /returns True when the wave existed and was cleared, otherwise false.
    /// </summary>
    public static bool ClearWaveCells(SerializedObject serializedObject,
                                      Object targetObject,
                                      SerializedProperty wavesProperty,
                                      int waveIndex,
                                      ref int selectedWaveIndex,
                                      ref Vector2Int selectedCellCoordinate)
    {
        SerializedProperty paintedCellsProperty = GetPaintedCellsProperty(wavesProperty, waveIndex);

        if (paintedCellsProperty == null)
            return false;

        paintedCellsProperty.ClearArray();

        if (selectedWaveIndex == waveIndex)
        {
            selectedWaveIndex = -1;
            selectedCellCoordinate = new Vector2Int(-1, -1);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
        return true;
    }

    /// <summary>
    /// Enables preview on one wave and disables it on all others.
    /// /params serializedObject: Serialized object backing the editor.
    /// /params targetObject: Unity object marked dirty after mutation.
    /// /params wavesProperty: Serialized waves array.
    /// /params previewWaveIndex: Wave index that should remain previewed.
    /// /returns None.
    /// </summary>
    public static void SetWavePreview(SerializedObject serializedObject,
                                      Object targetObject,
                                      SerializedProperty wavesProperty,
                                      int previewWaveIndex)
    {
        if (wavesProperty == null)
            return;

        for (int waveIndex = 0; waveIndex < wavesProperty.arraySize; waveIndex++)
        {
            SerializedProperty waveProperty = wavesProperty.GetArrayElementAtIndex(waveIndex);
            waveProperty.FindPropertyRelative("previewInScene").boolValue = waveIndex == previewWaveIndex;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
    }

    /// <summary>
    /// Terminates paint-drag mode when the mouse button is released.
    /// /params currentEvent: Current IMGUI event.
    /// /params paintDragActive: Current paint-drag flag, updated by the method.
    /// /params paintDragWaveIndex: Current paint-drag wave index, updated by the method.
    /// /params lastPaintedCoordinate: Last drag-painted coordinate, updated by the method.
    /// /returns None.
    /// </summary>
    public static void HandlePaintDragTermination(Event currentEvent,
                                                  ref bool paintDragActive,
                                                  ref int paintDragWaveIndex,
                                                  ref Vector2Int lastPaintedCoordinate)
    {
        if (currentEvent.type != EventType.MouseUp)
            return;

        paintDragActive = false;
        paintDragWaveIndex = -1;
        lastPaintedCoordinate = new Vector2Int(int.MinValue, int.MinValue);
    }
    #endregion

    #endregion
}
