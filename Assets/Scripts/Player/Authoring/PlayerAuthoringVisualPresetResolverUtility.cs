using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Resolves player visual settings from the active master visual preset with hidden authoring values as fallback.
/// </summary>
public static class PlayerAuthoringVisualPresetResolverUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the active player visual preset referenced by a master preset.
    /// masterPreset: Master preset assigned to the player authoring.
    /// returns Active PlayerVisualPreset when assigned; otherwise null.
    /// </summary>
    public static PlayerVisualPreset ResolveVisualPreset(PlayerMasterPreset masterPreset)
    {
        if (masterPreset == null)
            return null;

        return masterPreset.VisualPreset;
    }

    /// <summary>
    /// Resolves the runtime visual bridge prefab using the master visual preset first and the hidden authoring field as fallback.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackPrefab: Hidden authoring fallback prefab kept for compatibility.
    /// returns Resolved visual bridge prefab.
    /// </summary>
    public static GameObject ResolveRuntimeVisualBridgePrefab(PlayerMasterPreset masterPreset, GameObject fallbackPrefab)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null && visualPreset.RuntimeVisualBridgePrefab != null)
            return visualPreset.RuntimeVisualBridgePrefab;

        return fallbackPrefab;
    }

    /// <summary>
    /// Resolves whether the runtime bridge should spawn only when no Animator companion is available.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved spawn policy.
    /// </summary>
    public static bool ResolveSpawnRuntimeVisualBridgeWhenAnimatorMissing(PlayerMasterPreset masterPreset, bool fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.SpawnRuntimeVisualBridgeWhenAnimatorMissing;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves whether the runtime bridge should copy ECS rotation.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved sync rotation flag.
    /// </summary>
    public static bool ResolveRuntimeVisualBridgeSyncRotation(PlayerMasterPreset masterPreset, bool fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.RuntimeVisualBridgeSyncRotation;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the local runtime bridge offset.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved runtime bridge offset.
    /// </summary>
    public static Vector3 ResolveRuntimeVisualBridgeOffset(PlayerMasterPreset masterPreset, Vector3 fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.RuntimeVisualBridgeOffset;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the authored damage flash color.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved damage flash color.
    /// </summary>
    public static Color ResolveDamageFlashColor(PlayerMasterPreset masterPreset, Color fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.DamageFlashColor;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the damage flash duration.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved damage flash duration in seconds.
    /// </summary>
    public static float ResolveDamageFlashDurationSeconds(PlayerMasterPreset masterPreset, float fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.DamageFlashDurationSeconds;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the maximum damage flash blend.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved maximum flash blend.
    /// </summary>
    public static float ResolveDamageFlashMaximumBlend(PlayerMasterPreset masterPreset, float fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.DamageFlashMaximumBlend;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the Elemental Trail attached VFX prefab.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackPrefab: Hidden authoring fallback prefab kept for compatibility.
    /// returns Resolved Elemental Trail attached VFX prefab.
    /// </summary>
    public static GameObject ResolveElementalTrailAttachedVfxPrefab(PlayerMasterPreset masterPreset, GameObject fallbackPrefab)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null && visualPreset.ElementalTrailAttachedVfxPrefab != null)
            return visualPreset.ElementalTrailAttachedVfxPrefab;

        return fallbackPrefab;
    }

    /// <summary>
    /// Resolves the Elemental Trail attached VFX scale multiplier.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved attached VFX scale multiplier.
    /// </summary>
    public static float ResolveElementalTrailAttachedVfxScaleMultiplier(PlayerMasterPreset masterPreset, float fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.ElementalTrailAttachedVfxScaleMultiplier;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the one-shot VFX per-cell cap.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved one-shot VFX per-cell cap.
    /// </summary>
    public static int ResolveMaxIdenticalOneShotVfxPerCell(PlayerMasterPreset masterPreset, int fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.MaxIdenticalOneShotVfxPerCell;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the one-shot VFX spatial cell size.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved one-shot VFX cell size.
    /// </summary>
    public static float ResolveOneShotVfxCellSize(PlayerMasterPreset masterPreset, float fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.OneShotVfxCellSize;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the attached elemental VFX per-target cap.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved attached elemental VFX per-target cap.
    /// </summary>
    public static int ResolveMaxAttachedElementalVfxPerTarget(PlayerMasterPreset masterPreset, int fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.MaxAttachedElementalVfxPerTarget;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the total active one-shot VFX cap.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved active one-shot VFX cap.
    /// </summary>
    public static int ResolveMaxActiveOneShotPowerUpVfx(PlayerMasterPreset masterPreset, int fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.MaxActiveOneShotPowerUpVfx;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves whether the lifetime of capped attached elemental VFX should be refreshed.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackValue: Hidden authoring fallback value kept for compatibility.
    /// returns Resolved refresh-on-cap policy.
    /// </summary>
    public static bool ResolveRefreshAttachedElementalVfxLifetimeOnCapHit(PlayerMasterPreset masterPreset, bool fallbackValue)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null)
            return visualPreset.RefreshAttachedElementalVfxLifetimeOnCapHit;

        return fallbackValue;
    }

    /// <summary>
    /// Resolves the per-element enemy VFX assignments used by elemental player bullets and trails.
    /// masterPreset: Master preset assigned to the player authoring.
    /// fallbackPreset: Legacy power-ups preset kept as a fallback source for migrated projects.
    /// returns Resolved assignment list, or null when no source is available.
    /// </summary>
    public static IReadOnlyList<ElementalVfxByElementData> ResolveElementalEnemyVfxAssignments(PlayerMasterPreset masterPreset,
                                                                                                PlayerPowerUpsPreset fallbackPreset)
    {
        PlayerVisualPreset visualPreset = ResolveVisualPreset(masterPreset);

        if (visualPreset != null && PlayerElementalVfxAssignmentUtility.HasAnyConfiguredVfx(visualPreset.ElementalEnemyVfxByElement))
            return visualPreset.ElementalEnemyVfxByElement;

        if (fallbackPreset != null)
            return fallbackPreset.ElementalVfxByElement;

        if (visualPreset != null)
            return visualPreset.ElementalEnemyVfxByElement;

        return null;
    }
    #endregion

    #endregion
}
