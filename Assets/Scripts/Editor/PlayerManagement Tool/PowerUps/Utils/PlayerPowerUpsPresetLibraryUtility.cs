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

        string normalizedName = string.IsNullOrWhiteSpace(presetName) ? "PlayerPowerUpsPreset" : PlayerManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "PlayerPowerUpsPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        PlayerPowerUpsPreset preset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();
        preset.name = finalName;
        AssetDatabase.CreateAsset(preset, assetPath);

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("presetName");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

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
