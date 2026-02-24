using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared utility methods for finding and validating prefab assets used by management tools.
/// </summary>
public static class ManagementToolPrefabUtility
{
    #region Methods

    #region Public Methods
    public static bool TryFindFirstPrefabWithComponent<TComponent>(out GameObject prefabAsset) where TComponent : Component
    {
        prefabAsset = null;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        for (int index = 0; index < prefabGuids.Length; index++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
            GameObject candidatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (candidatePrefab == null)
                continue;

            if (candidatePrefab.GetComponent<TComponent>() == null)
                continue;

            prefabAsset = candidatePrefab;
            return true;
        }

        return false;
    }

    public static bool HasComponent<TComponent>(GameObject prefabAsset) where TComponent : Component
    {
        if (prefabAsset == null)
            return false;

        return prefabAsset.GetComponent<TComponent>() != null;
    }
    #endregion

    #endregion
}
