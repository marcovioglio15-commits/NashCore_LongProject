using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// This class manages a draft session for player-related assets in the Unity Editor. 
/// It allows tracking changes made to player presets and related assets, 
/// staging deletions, and applying or discarding changes as needed. 
/// The session captures a baseline state of relevant assets and compares it against 
/// the current state to determine if there are pending changes that need to be applied or discarded. 
/// It also handles asset renaming based on preset names and ensures that assets referenced by libraries
/// cannot be deleted without first removing the reference.
/// In details, the session provides methods to begin and end a draft session,
/// stage and unstage asset deletions, perform undo and redo operations,
/// mark the session as dirty, recompute pending changes, apply changes, and discard changes.
/// </summary>
public static class PlayerManagementDraftSession
{
    #region Constants
    private const string TrackedPlayerAssetsRoot = "Assets/Scriptable Objects/Player";
    private const string TrackedProjectRoot = "Assets";
    #endregion

    #region Fields
    // This dictionary holds the baseline JSON representation of relevant assets, keyed by their asset paths.
    // Those JSON representations are used to compare the current state of assets against the baseline to determine if there are pending changes.
    // If pending changes are detected, the session can be applied to save those changes or discarded to revert to the baseline state.
    private static readonly Dictionary<string, string> baselineJsonByPath = new Dictionary<string, string>();
    // This hash set holds the asset paths that are staged for deletion during the draft session.
    private static readonly HashSet<string> stagedDeletePaths = new HashSet<string>();

    private static bool isInitialized;
    private static bool hasPendingChanges;
    #endregion

    #region Properties
    public static bool IsInitialized
    {
        get
        {
            return isInitialized;
        }
    }

    public static bool HasPendingChanges
    {
        get
        {
            return hasPendingChanges;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// This method initializes the draft session by capturing the baseline state of relevant player assets, 
    /// setting the session as initialized, and clearing any staged deletions.
    /// </summary>
    public static void BeginSession()
    {
        CaptureBaseline();
        stagedDeletePaths.Clear();
        isInitialized = true;
        hasPendingChanges = false;
    }

    public static void EndSession()
    {
        isInitialized = false;
        hasPendingChanges = false;
        baselineJsonByPath.Clear();
        stagedDeletePaths.Clear();
    }

    public static void StageDeleteAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrWhiteSpace(assetPath))
            return;

        stagedDeletePaths.Add(assetPath);
        hasPendingChanges = true;
    }

    /// <summary>
    /// This method removes the specified asset from the staged deletions, 
    /// allowing it to be retained in the project.
    /// </summary>
    /// <param name="asset"></param>
    public static void UnstageDeleteAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrWhiteSpace(assetPath))
            return;

        if (stagedDeletePaths.Contains(assetPath))
            stagedDeletePaths.Remove(assetPath);
    }

    public static bool IsAssetStagedForDeletion(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        string assetPath = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        return stagedDeletePaths.Contains(assetPath);
    }

    public static void PerformUndo()
    {
        Undo.PerformUndo();
        RecomputePendingChanges();
    }

    public static void PerformRedo()
    {
        Undo.PerformRedo();
        RecomputePendingChanges();
    }

    public static void MarkDirty()
    {
        hasPendingChanges = true;
    }

    public static void RecomputePendingChanges()
    {
        if (isInitialized == false)
        {
            hasPendingChanges = false;
            return;
        }

        SyncStagedDeletePaths();

        if (stagedDeletePaths.Count > 0)
        {
            hasPendingChanges = true;
            return;
        }

        Dictionary<string, string> currentState = BuildStateDictionary();

        if (currentState.Count != baselineJsonByPath.Count)
        {
            hasPendingChanges = true;
            return;
        }

        foreach (KeyValuePair<string, string> baselineEntry in baselineJsonByPath)
        {
            if (currentState.TryGetValue(baselineEntry.Key, out string currentJson) == false)
            {
                hasPendingChanges = true;
                return;
            }

            if (string.Equals(baselineEntry.Value, currentJson, StringComparison.Ordinal) == false)
            {
                hasPendingChanges = true;
                return;
            }
        }

        hasPendingChanges = false;
    }

    public static void Apply()
    {
        ExecuteStagedDeletions();
        ExecutePresetAssetRenames();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CaptureBaseline();
        stagedDeletePaths.Clear();
        hasPendingChanges = false;
    }

    public static void Discard()
    {
        if (isInitialized == false)
            return;

        RestoreBaselineAssets();
        DeleteAssetsCreatedAfterBaseline();
        stagedDeletePaths.Clear();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CaptureBaseline();
        hasPendingChanges = false;
    }
    #endregion

    #region Session Helpers
    /// <summary>
    /// This method captures the baseline state of relevant player assets by building 
    /// a dictionary that maps asset paths to their serialized JSON representations.
    /// </summary>
    private static void CaptureBaseline()
    {
        baselineJsonByPath.Clear();

        Dictionary<string, string> currentState = BuildStateDictionary();

        foreach (KeyValuePair<string, string> stateEntry in currentState)
            baselineJsonByPath[stateEntry.Key] = stateEntry.Value;
    }

    private static Dictionary<string, string> BuildStateDictionary()
    {
        Dictionary<string, string> stateByPath = new Dictionary<string, string>();
        List<string> assetPaths = CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++)
        {
            string assetPath = assetPaths[pathIndex];

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            UnityEngine.Object assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (assetObject == null)
                continue;

            string serializedJson = EditorJsonUtility.ToJson(assetObject, true);
            stateByPath[assetPath] = serializedJson;
        }

        return stateByPath;
    }

    private static List<string> CollectTrackedAssetPaths()
    {
        HashSet<string> uniquePaths = new HashSet<string>();
        AddAssetPathsOfType<PlayerMasterPresetLibrary>(uniquePaths, TrackedPlayerAssetsRoot);
        AddAssetPathsOfType<PlayerMasterPreset>(uniquePaths, TrackedPlayerAssetsRoot);
        AddAssetPathsOfType<PlayerControllerPresetLibrary>(uniquePaths, TrackedPlayerAssetsRoot);
        AddAssetPathsOfType<PlayerControllerPreset>(uniquePaths, TrackedPlayerAssetsRoot);
        AddAssetPathsOfType<PlayerProgressionPresetLibrary>(uniquePaths, TrackedPlayerAssetsRoot);
        AddAssetPathsOfType<PlayerProgressionPreset>(uniquePaths, TrackedPlayerAssetsRoot);
        AddAssetPathsOfType<PlayerPowerUpsPresetLibrary>(uniquePaths, TrackedPlayerAssetsRoot);
        AddAssetPathsOfType<PlayerPowerUpsPreset>(uniquePaths, TrackedPlayerAssetsRoot);
        AddAssetPathsOfType<PlayerAnimationBindingsPreset>(uniquePaths, TrackedPlayerAssetsRoot);
        AddAssetPathsOfType<InputActionAsset>(uniquePaths, TrackedProjectRoot);
        AddPlayerPrefabPaths(uniquePaths);

        List<string> paths = new List<string>(uniquePaths);
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static void AddAssetPathsOfType<TAsset>(HashSet<string> uniquePaths, string searchRoot) where TAsset : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(searchRoot))
            return;

        string[] searchFolders = new string[] { searchRoot };
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(TAsset).Name, searchFolders);

        for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
        {
            string guid = guids[guidIndex];
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrWhiteSpace(path))
                continue;

            uniquePaths.Add(path);
        }
    }

    private static void AddPlayerPrefabPaths(HashSet<string> uniquePaths)
    {
        string[] searchFolders = new string[] { TrackedProjectRoot };
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab PlayerAuthoring", searchFolders);

        for (int guidIndex = 0; guidIndex < prefabGuids.Length; guidIndex++)
        {
            string prefabGuid = prefabGuids[guidIndex];
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);

            if (string.IsNullOrWhiteSpace(prefabPath))
                continue;

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefabAsset == null)
                continue;

            if (prefabAsset.GetComponent<PlayerAuthoring>() == null)
                continue;

            uniquePaths.Add(prefabPath);
        }
    }

    private static void RestoreBaselineAssets()
    {
        foreach (KeyValuePair<string, string> baselineEntry in baselineJsonByPath)
        {
            string assetPath = baselineEntry.Key;
            UnityEngine.Object assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (assetObject == null)
                continue;

            EditorJsonUtility.FromJsonOverwrite(baselineEntry.Value, assetObject);
            EditorUtility.SetDirty(assetObject);
        }
    }

    private static void DeleteAssetsCreatedAfterBaseline()
    {
        List<string> currentPaths = CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < currentPaths.Count; pathIndex++)
        {
            string currentPath = currentPaths[pathIndex];

            if (currentPath.StartsWith(TrackedPlayerAssetsRoot, StringComparison.Ordinal) == false)
                continue;

            if (baselineJsonByPath.ContainsKey(currentPath))
                continue;

            AssetDatabase.DeleteAsset(currentPath);
        }
    }

    private static void ExecuteStagedDeletions()
    {
        if (stagedDeletePaths.Count == 0)
            return;

        List<string> stagedPaths = new List<string>(stagedDeletePaths);

        for (int pathIndex = 0; pathIndex < stagedPaths.Count; pathIndex++)
        {
            string stagedPath = stagedPaths[pathIndex];

            if (string.IsNullOrWhiteSpace(stagedPath))
                continue;

            if (AssetDatabase.LoadMainAssetAtPath(stagedPath) == null)
                continue;

            AssetDatabase.DeleteAsset(stagedPath);
        }
    }

    private static void ExecutePresetAssetRenames()
    {
        List<string> assetPaths = CollectTrackedAssetPaths();

        for (int pathIndex = 0; pathIndex < assetPaths.Count; pathIndex++)
        {
            string assetPath = assetPaths[pathIndex];

            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            if (stagedDeletePaths.Contains(assetPath))
                continue;

            UnityEngine.Object assetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (IsRenamablePresetAsset(assetObject) == false)
                continue;

            string currentFileName = Path.GetFileNameWithoutExtension(assetPath);
            string targetFileName = NormalizeAssetName(assetObject.name);

            if (string.IsNullOrWhiteSpace(targetFileName))
                continue;

            if (string.Equals(currentFileName, targetFileName, StringComparison.Ordinal))
                continue;

            string directoryPath = Path.GetDirectoryName(assetPath);

            if (string.IsNullOrWhiteSpace(directoryPath))
                continue;

            string normalizedDirectoryPath = directoryPath.Replace('\\', '/');
            string extension = Path.GetExtension(assetPath);
            string requestedPath = Path.Combine(normalizedDirectoryPath, targetFileName + extension).Replace('\\', '/');
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
            string renameError = AssetDatabase.MoveAsset(assetPath, uniquePath);

            if (string.IsNullOrWhiteSpace(renameError))
                continue;

            Debug.LogWarning(string.Format("PlayerManagementDraftSession: failed to rename asset '{0}' to '{1}'. Error: {2}", assetPath, targetFileName, renameError));
        }
    }

    private static bool IsRenamablePresetAsset(UnityEngine.Object assetObject)
    {
        if (assetObject == null)
            return false;

        if (assetObject is PlayerMasterPreset)
            return true;

        if (assetObject is PlayerControllerPreset)
            return true;

        if (assetObject is PlayerProgressionPreset)
            return true;

        if (assetObject is PlayerPowerUpsPreset)
            return true;

        if (assetObject is PlayerAnimationBindingsPreset)
            return true;

        return false;
    }

    public static string NormalizeAssetName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string trimmedName = rawName.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            return string.Empty;

        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder(trimmedName.Length);

        for (int charIndex = 0; charIndex < trimmedName.Length; charIndex++)
        {
            char currentChar = trimmedName[charIndex];
            bool isInvalidCharacter = false;

            for (int invalidIndex = 0; invalidIndex < invalidFileNameChars.Length; invalidIndex++)
            {
                if (currentChar != invalidFileNameChars[invalidIndex])
                    continue;

                isInvalidCharacter = true;
                break;
            }

            if (isInvalidCharacter)
            {
                builder.Append('_');
                continue;
            }

            builder.Append(currentChar);
        }

        string normalizedName = builder.ToString().Trim();

        while (normalizedName.EndsWith(".", StringComparison.Ordinal))
            normalizedName = normalizedName.Substring(0, normalizedName.Length - 1).TrimEnd();

        if (string.IsNullOrWhiteSpace(normalizedName))
            return string.Empty;

        return normalizedName;
    }

    private static void SyncStagedDeletePaths()
    {
        if (stagedDeletePaths.Count == 0)
            return;

        List<string> stagedPaths = new List<string>(stagedDeletePaths);

        for (int pathIndex = 0; pathIndex < stagedPaths.Count; pathIndex++)
        {
            string stagedPath = stagedPaths[pathIndex];

            if (IsPathReferencedByLibraries(stagedPath) == false)
                continue;

            stagedDeletePaths.Remove(stagedPath);
        }
    }

    private static bool IsPathReferencedByLibraries(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        PlayerMasterPresetLibrary masterLibrary = PlayerMasterPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(masterLibrary.Presets, assetPath))
            return true;

        PlayerControllerPresetLibrary controllerLibrary = PlayerControllerPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(controllerLibrary.Presets, assetPath))
            return true;

        PlayerProgressionPresetLibrary progressionLibrary = PlayerProgressionPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(progressionLibrary.Presets, assetPath))
            return true;

        PlayerPowerUpsPresetLibrary powerUpsLibrary = PlayerPowerUpsPresetLibraryUtility.GetOrCreateLibrary();

        if (LibraryContainsPath(powerUpsLibrary.Presets, assetPath))
            return true;

        return false;
    }

    private static bool LibraryContainsPath<TAsset>(IReadOnlyList<TAsset> presets, string assetPath) where TAsset : UnityEngine.Object
    {
        for (int index = 0; index < presets.Count; index++)
        {
            TAsset preset = presets[index];

            if (preset == null)
                continue;

            string presetPath = AssetDatabase.GetAssetPath(preset);

            if (string.Equals(presetPath, assetPath, StringComparison.Ordinal) == false)
                continue;

            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
