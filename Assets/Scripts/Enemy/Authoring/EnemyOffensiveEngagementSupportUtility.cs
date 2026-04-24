/// <summary>
/// Resolves which offensive module kinds can emit predictive engagement feedback and which timing model they use.
/// /params None.
/// /returns None.
/// </summary>
public static class EnemyOffensiveEngagementSupportUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the predictive engagement timing mode supported by one module inside the provided catalog section.
    /// /params section Catalog section that owns the module binding.
    /// /params moduleKind Resolved module kind selected in that section.
    /// /returns Supported timing mode, or None when predictive engagement feedback is not implemented for that module kind.
    /// </summary>
    public static EnemyOffensiveEngagementTimingMode ResolveTimingMode(EnemyPatternModuleCatalogSection section,
                                                                       EnemyPatternModuleKind moduleKind)
    {
        switch (section)
        {
            case EnemyPatternModuleCatalogSection.ShortRangeInteraction:
                return ResolveShortRangeTimingMode(moduleKind);

            case EnemyPatternModuleCatalogSection.WeaponInteraction:
                return ResolveWeaponTimingMode(moduleKind);

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }

    /// <summary>
    /// Returns whether the provided module kind currently supports predictive engagement feedback inside the provided catalog section.
    /// /params section Catalog section that owns the module binding.
    /// /params moduleKind Resolved module kind selected in that section.
    /// /returns True when the module kind currently maps to a supported timing mode.
    /// </summary>
    public static bool SupportsTimingMode(EnemyPatternModuleCatalogSection section,
                                          EnemyPatternModuleKind moduleKind)
    {
        return ResolveTimingMode(section, moduleKind) != EnemyOffensiveEngagementTimingMode.None;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the predictive engagement timing mode supported by one short-range module kind.
    /// /params moduleKind Selected short-range module kind.
    /// /returns Supported timing mode, or None when no predictive trigger is currently implemented.
    /// </summary>
    private static EnemyOffensiveEngagementTimingMode ResolveShortRangeTimingMode(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.ShortRangeDash:
                return EnemyOffensiveEngagementTimingMode.ShortRangeDashRelease;

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }

    /// <summary>
    /// Resolves the predictive engagement timing mode supported by one weapon module kind.
    /// /params moduleKind Selected weapon module kind.
    /// /returns Supported timing mode, or None when no predictive trigger is currently implemented.
    /// </summary>
    private static EnemyOffensiveEngagementTimingMode ResolveWeaponTimingMode(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Shooter:
                return EnemyOffensiveEngagementTimingMode.WeaponShot;

            default:
                return EnemyOffensiveEngagementTimingMode.None;
        }
    }
    #endregion

    #endregion
}
