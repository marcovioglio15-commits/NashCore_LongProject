using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlayerPowerUpsPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Player/PlayerPowerUpsPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Player/Power-Ups";
    #endregion

    #region Methods

    #region Public Methods
    public static PlayerPowerUpsPresetLibrary GetOrCreateLibrary()
    {
        PlayerPowerUpsPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerPowerUpsPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));

        PlayerPowerUpsPresetLibrary createdLibrary = ScriptableObject.CreateInstance<PlayerPowerUpsPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);

        return createdLibrary;
    }

    public static PlayerPowerUpsPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        PlayerPowerUpsPreset preset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();
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
