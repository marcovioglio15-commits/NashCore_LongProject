using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides utility methods for creating and loading player visual preset assets and their library.
/// </summary>
public static class PlayerVisualPresetLibraryUtility
{
    #region Constants
    public const string DefaultLibraryPath = "Assets/Scriptable Objects/Player/PlayerVisualPresetLibrary.asset";
    public const string DefaultPresetsFolder = "Assets/Scriptable Objects/Player/Visual";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Loads the shared player visual preset library or creates it when missing.
    /// None.
    /// returns Resolved PlayerVisualPresetLibrary asset.
    /// </summary>
    public static PlayerVisualPresetLibrary GetOrCreateLibrary()
    {
        PlayerVisualPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerVisualPresetLibrary>(DefaultLibraryPath);

        if (library != null)
            return library;

        EnsureFolder(Path.GetDirectoryName(DefaultLibraryPath));
        PlayerVisualPresetLibrary createdLibrary = ScriptableObject.CreateInstance<PlayerVisualPresetLibrary>();
        AssetDatabase.CreateAsset(createdLibrary, DefaultLibraryPath);
        return createdLibrary;
    }

    /// <summary>
    /// Creates one new player visual preset asset inside the default preset folder.
    /// presetName: Requested asset name before normalization.
    /// returns Newly created PlayerVisualPreset asset.
    /// </summary>
    public static PlayerVisualPreset CreatePresetAsset(string presetName)
    {
        EnsureFolder(DefaultPresetsFolder);

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(presetName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "PlayerVisualPreset";

        PlayerVisualPreset preset = ScriptableObject.CreateInstance<PlayerVisualPreset>();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(DefaultPresetsFolder, normalizedName + ".asset"));
        AssetDatabase.CreateAsset(preset, assetPath);
        string finalName = Path.GetFileNameWithoutExtension(assetPath);
        preset.name = finalName;

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("presetName");
        SerializedProperty laserBeamProperty = serializedPreset.FindProperty("laserBeam");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

        if (laserBeamProperty != null)
        {
            SerializedProperty beamMaterialProperty = laserBeamProperty.FindPropertyRelative("beamMaterial");
            SerializedProperty sourceBubbleMaterialProperty = laserBeamProperty.FindPropertyRelative("sourceBubbleMaterial");
            SerializedProperty impactSplashMaterialProperty = laserBeamProperty.FindPropertyRelative("impactSplashMaterial");
            Material laserBeamMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultBodyMaterialPath);
            Material sourceBubbleMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultSourceBubbleMaterialPath);
            Material impactSplashMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultImpactSplashMaterialPath);

            if (beamMaterialProperty != null && laserBeamMaterial != null)
                beamMaterialProperty.objectReferenceValue = laserBeamMaterial;

            if (sourceBubbleMaterialProperty != null && sourceBubbleMaterial != null)
                sourceBubbleMaterialProperty.objectReferenceValue = sourceBubbleMaterial;

            if (impactSplashMaterialProperty != null && impactSplashMaterial != null)
                impactSplashMaterialProperty.objectReferenceValue = impactSplashMaterial;
        }

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        preset.LaserBeam.Validate();
        EditorUtility.SetDirty(preset);
        return preset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures that the target folder hierarchy exists inside the project.
    /// folderPath: Folder path to create when missing.
    /// returns None.
    /// </summary>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endregion

    #endregion
}
