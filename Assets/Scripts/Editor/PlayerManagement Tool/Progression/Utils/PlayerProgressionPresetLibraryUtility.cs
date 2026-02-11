using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides utility methods for managing PlayerProgressionPresetLibrary and PlayerProgressionPreset assets within the Unity Editor.
/// </summary>
public static class PlayerProgressionPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Player/PlayerProgressionPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Player/Progression";
    #endregion

    #region Methods

    #region Public Methods
    public static PlayerProgressionPresetLibrary GetOrCreateLibrary()
    {
        PlayerProgressionPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerProgressionPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));

        PlayerProgressionPresetLibrary createdLibrary = ScriptableObject.CreateInstance<PlayerProgressionPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);

        return createdLibrary;
    }

    public static PlayerProgressionPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        PlayerProgressionPreset preset = ScriptableObject.CreateInstance<PlayerProgressionPreset>();
        preset.name = presetName;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, presetName + ".asset"));
        AssetDatabase.CreateAsset(preset, assetPath);

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("presetName");

        if (nameProperty != null)
            nameProperty.stringValue = presetName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);

        return preset;
    }
    #endregion

    #region Private Methods
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (string.IsNullOrWhiteSpace(parentFolder) == false && AssetDatabase.IsValidFolder(parentFolder) == false)
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endregion

    #endregion
}
