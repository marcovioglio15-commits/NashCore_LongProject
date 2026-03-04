using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides utility methods for creating and loading EnemyBrainPreset assets and their library.
/// </summary>
public static class EnemyBrainPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Enemy/EnemyBrainPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Enemy/Brain";
    #endregion

    #region Methods

    #region Public Methods
    public static EnemyBrainPresetLibrary GetOrCreateLibrary()
    {
        EnemyBrainPresetLibrary library = AssetDatabase.LoadAssetAtPath<EnemyBrainPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        EnemyBrainPresetLibrary createdLibrary = ScriptableObject.CreateInstance<EnemyBrainPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        return createdLibrary;
    }

    public static EnemyBrainPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "EnemyBrainPreset";

        EnemyBrainPreset preset = ScriptableObject.CreateInstance<EnemyBrainPreset>();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        AssetDatabase.CreateAsset(preset, assetPath);
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        preset.name = finalName;

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
