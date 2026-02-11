using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class PlayerManagementDraftSession
{
    #region Constants
    private const string TrackedPlayerAssetsRoot = "Assets/Scriptable Objects/Player";
    private const string TrackedProjectRoot = "Assets";
    #endregion

    #region Fields
    private static readonly Dictionary<string, string> baselineJsonByPath = new Dictionary<string, string>();
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
