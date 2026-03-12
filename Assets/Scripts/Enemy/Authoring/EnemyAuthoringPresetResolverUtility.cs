using UnityEngine;

/// <summary>
/// Resolves fallback and preset-derived enemy authoring settings without duplicating lookup code in the authoring component.
/// </summary>
public static class EnemyAuthoringPresetResolverUtility
{
    #region Methods

    #region Preset Resolution
    public static EnemyBrainPreset ResolveBrainPreset(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        if (masterPreset != null && masterPreset.BrainPreset != null)
            return masterPreset.BrainPreset;

        return fallbackBrainPreset;
    }

    public static EnemyAdvancedPatternPreset ResolveAdvancedPatternPreset(EnemyMasterPreset masterPreset, EnemyAdvancedPatternPreset fallbackAdvancedPatternPreset)
    {
        if (masterPreset != null && masterPreset.AdvancedPatternPreset != null)
            return masterPreset.AdvancedPatternPreset;

        return fallbackAdvancedPatternPreset;
    }

    public static EnemyBrainMovementSettings ResolveMovementSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Movement;
    }

    public static EnemyBrainSteeringSettings ResolveSteeringSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Steering;
    }

    public static EnemyBrainDamageSettings ResolveDamageSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Damage;
    }

    public static EnemyBrainHealthStatisticsSettings ResolveHealthStatisticsSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.HealthStatistics;
    }

    public static EnemyBrainVisualSettings ResolveVisualSettings(EnemyMasterPreset masterPreset, EnemyBrainPreset fallbackBrainPreset)
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset(masterPreset, fallbackBrainPreset);

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Visual;
    }
    #endregion

    #endregion
}
