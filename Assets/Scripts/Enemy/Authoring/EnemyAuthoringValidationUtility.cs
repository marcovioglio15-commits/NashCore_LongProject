using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Centralizes pure validation helpers used by enemy bakers so the main bake file stays focused on ECS conversion.
/// </summary>
internal static class EnemyAuthoringValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates whether the authored shooter projectile prefab can be converted safely.
    /// /params authoring Source enemy authoring component that owns the reference.
    /// /params projectilePrefabObject Candidate projectile prefab.
    /// /returns True when the prefab is invalid for bake.
    /// </summary>
    public static bool IsInvalidShooterProjectilePrefab(EnemyAuthoring authoring, GameObject projectilePrefabObject)
    {
        if (projectilePrefabObject == null)
            return true;

        if (authoring != null && projectilePrefabObject == authoring.gameObject)
            return true;

        if (projectilePrefabObject.scene.IsValid())
            return true;

        if (projectilePrefabObject.GetComponent<EnemyAuthoring>() != null)
            return true;

        if (projectilePrefabObject.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    /// <summary>
    /// Validates whether one experience drop prefab can be converted safely.
    /// /params authoring Source enemy authoring component that owns the reference.
    /// /params dropPrefabObject Candidate experience-drop prefab.
    /// /returns True when the prefab is invalid for bake.
    /// </summary>
    public static bool IsInvalidExperienceDropPrefab(EnemyAuthoring authoring, GameObject dropPrefabObject)
    {
        if (dropPrefabObject == null)
            return true;

        if (authoring != null && dropPrefabObject == authoring.gameObject)
            return true;

        if (dropPrefabObject.scene.IsValid())
            return true;

        if (dropPrefabObject.GetComponent<EnemyAuthoring>() != null)
            return true;

        if (dropPrefabObject.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    /// <summary>
    /// Validates whether one hit-VFX prefab can be converted safely.
    /// /params authoring Source enemy authoring component that owns the reference.
    /// /params hitVfxPrefabObject Candidate hit-VFX prefab.
    /// /returns True when the prefab is invalid for bake.
    /// </summary>
    public static bool IsInvalidHitVfxPrefab(EnemyAuthoring authoring, GameObject hitVfxPrefabObject)
    {
        if (hitVfxPrefabObject == null)
            return true;

        if (authoring != null && hitVfxPrefabObject == authoring.gameObject)
            return true;

        if (hitVfxPrefabObject.scene.IsValid())
            return true;

        if (hitVfxPrefabObject.GetComponent<EnemyAuthoring>() != null)
            return true;

        if (hitVfxPrefabObject.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    /// <summary>
    /// Adds one estimated count to the running summary while protecting against integer overflow.
    /// /params currentEstimatedCount Current accumulated estimated count.
    /// /params additionalEstimatedCount Additional count to append.
    /// /returns Saturated estimated count.
    /// </summary>
    public static int AddEstimatedCount(int currentEstimatedCount, int additionalEstimatedCount)
    {
        long resolvedCount = (long)math.max(0, currentEstimatedCount) + math.max(0, additionalEstimatedCount);

        if (resolvedCount >= int.MaxValue)
            return int.MaxValue;

        return (int)resolvedCount;
    }
    #endregion

    #endregion
}
