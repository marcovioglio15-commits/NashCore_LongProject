using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Handles prefab discovery and active GameAudioManagerAuthoring assignment for Game Management presets.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameMasterPresetsPanelAuthoringUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Finds the first prefab containing GameAudioManagerAuthoring and stores it on the panel.
    /// /params panel Owning panel that receives the selected prefab.
    /// /returns None.
    /// </summary>
    public static void FindAudioManagerPrefab(GameMasterPresetsPanel panel)
    {
        if (panel == null)
            return;

        List<GameObject> prefabs = ManagementToolPrefabUtility.FindPrefabsWithComponentInHierarchy<GameAudioManagerAuthoring>(new string[] { "Assets" });

        if (prefabs.Count <= 0)
            return;

        panel.SelectedAudioPrefab = prefabs[0];
        GameMasterPresetsPanelSidePanelUtility.SaveSelectedAudioPrefabState(panel);

        if (panel.AudioPrefabField != null)
            panel.AudioPrefabField.SetValueWithoutNotify(panel.SelectedAudioPrefab);

        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Assigns the selected master preset to the selected GameAudioManagerAuthoring prefab.
    /// /params panel Owning panel with selected preset and prefab context.
    /// /returns None.
    /// </summary>
    public static void AssignPresetToAuthoringPrefab(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null || panel.SelectedAudioPrefab == null)
            return;

        GameAudioManagerAuthoring authoring = panel.SelectedAudioPrefab.GetComponentInChildren<GameAudioManagerAuthoring>(true);

        if (authoring == null)
            return;

        Undo.RecordObject(authoring, "Set Active Game Master Preset");
        SerializedObject serializedAuthoring = new SerializedObject(authoring);
        SerializedProperty masterPresetProperty = serializedAuthoring.FindProperty("masterPreset");

        if (masterPresetProperty == null)
            return;

        serializedAuthoring.Update();
        masterPresetProperty.objectReferenceValue = panel.SelectedPreset;
        serializedAuthoring.ApplyModifiedProperties();
        EditorUtility.SetDirty(authoring);
        EditorUtility.SetDirty(panel.SelectedAudioPrefab);
        PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);
        GameManagementDraftSession.MarkDirty();
        RefreshActiveStatus(panel);
    }

    /// <summary>
    /// Updates the active authoring status label for the selected prefab.
    /// /params panel Owning panel with selected preset and status label.
    /// /returns None.
    /// </summary>
    public static void RefreshActiveStatus(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.ActiveStatusLabel == null)
            return;

        if (panel.SelectedAudioPrefab == null)
        {
            panel.ActiveStatusLabel.text = "No prefab selected.";
            return;
        }

        GameAudioManagerAuthoring authoring = panel.SelectedAudioPrefab.GetComponentInChildren<GameAudioManagerAuthoring>(true);

        if (authoring == null)
        {
            panel.ActiveStatusLabel.text = "Selected prefab has no GameAudioManagerAuthoring.";
            return;
        }

        panel.ActiveStatusLabel.text = authoring.MasterPreset == panel.SelectedPreset ? "This preset is active on the selected prefab." : "Selected prefab uses a different preset.";
    }
    #endregion

    #endregion
}
