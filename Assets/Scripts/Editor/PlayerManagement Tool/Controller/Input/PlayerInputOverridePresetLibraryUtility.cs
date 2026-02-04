using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlayerInputOverridePresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Player/Input/PlayerInputOverridePresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Player/Input/Override Presets";
    #endregion

    #region Public Methods
    public static PlayerInputOverridePresetLibrary GetOrCreateLibrary()
    {
        PlayerInputOverridePresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerInputOverridePresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));

        PlayerInputOverridePresetLibrary createdLibrary = ScriptableObject.CreateInstance<PlayerInputOverridePresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        AssetDatabase.SaveAssets();

        return createdLibrary;
    }

    public static PlayerInputOverridePreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        PlayerInputOverridePreset preset = ScriptableObject.CreateInstance<PlayerInputOverridePreset>();
        preset.name = presetName;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, presetName + ".asset"));
        AssetDatabase.CreateAsset(preset, assetPath);
        AssetDatabase.SaveAssets();

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("m_PresetName");

        if (nameProperty != null)
            nameProperty.stringValue = presetName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();

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
}
