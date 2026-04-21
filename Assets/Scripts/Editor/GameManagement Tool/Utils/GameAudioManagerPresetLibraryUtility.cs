using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor asset factory for GameAudioManagerPresetLibrary and GameAudioManagerPreset assets.
/// /params None.
/// /returns None.
/// </summary>
public static class GameAudioManagerPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Game/Audio/GameAudioManagerPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Game/Audio";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the audio manager preset library or creates it at the default path.
    /// /params None.
    /// /returns Existing or newly created library asset.
    /// </summary>
    public static GameAudioManagerPresetLibrary GetOrCreateLibrary()
    {
        GameAudioManagerPresetLibrary library = AssetDatabase.LoadAssetAtPath<GameAudioManagerPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        GameManagementAssetUtility.EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        GameAudioManagerPresetLibrary createdLibrary = ScriptableObject.CreateInstance<GameAudioManagerPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        EditorUtility.SetDirty(createdLibrary);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one audio manager preset asset with complete default event bindings.
    /// /params presetName Requested preset display name.
    /// /returns Created preset asset or null when asset creation fails.
    /// </summary>
    public static GameAudioManagerPreset CreatePresetAsset(string presetName)
    {
        GameManagementAssetUtility.EnsureFolder(DefaultPresetsFolder);
        string normalizedName = GameManagementAssetUtility.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "GameAudioManagerPreset";

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        GameAudioManagerPreset preset = ScriptableObject.CreateInstance<GameAudioManagerPreset>();
        preset.name = finalName;
        preset.ResetEventBindingsToDefaults();
        AssetDatabase.CreateAsset(preset, assetPath);
        SynchronizePresetName(preset, finalName);
        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes the serialized preset name so list display and asset filename start synchronized.
    /// /params preset Preset asset to update.
    /// /params finalName Asset filename without extension.
    /// /returns None.
    /// </summary>
    private static void SynchronizePresetName(GameAudioManagerPreset preset, string finalName)
    {
        if (preset == null)
            return;

        SerializedObject serializedObject = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedObject.FindProperty("presetName");

        if (nameProperty != null)
        {
            serializedObject.Update();
            nameProperty.stringValue = finalName;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        preset.EnsureInitialized();
        EditorUtility.SetDirty(preset);
    }
    #endregion

    #endregion
}
