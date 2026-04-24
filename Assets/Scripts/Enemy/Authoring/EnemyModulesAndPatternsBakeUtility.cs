using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// Compiles the shared Modules & Patterns preset referenced by one enemy advanced-pattern preset.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyModulesAndPatternsBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles the active shared pattern loadout referenced by one enemy advanced-pattern preset.
    /// /params preset Enemy-specific advanced-pattern preset that owns the shared preset reference and loadout.
    /// /returns The compiled bake result ready for ECS authoring.
    /// </summary>
    public static EnemyCompiledPatternBakeResult Compile(EnemyAdvancedPatternPreset preset)
    {
        EnemyCompiledPatternBakeResult result = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(preset);

        if (preset == null)
            return result;

        EnemyModulesAndPatternsPreset sharedPreset = preset.ModulesAndPatternsPreset;

        if (sharedPreset == null)
            return result;

        EnemyModulesPatternDefinition selectedPattern = EnemyModulesAndPatternsSelectionUtility.ResolveSelectedPattern(preset);

        if (selectedPattern == null)
            return result;

        ApplyCoreMovement(selectedPattern, sharedPreset, ref result);
        ApplyShortRangeInteraction(selectedPattern, sharedPreset, ref result.PatternConfig);
        ApplyWeaponInteraction(selectedPattern, sharedPreset, ref result);
        ApplyDropItems(selectedPattern, sharedPreset, ref result);
        result.HasCustomMovement = ResolveHasCustomMovement(result.PatternConfig);
        return result;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies the core movement selection to the compiled result.
    /// /params pattern Shared pattern definition currently being compiled.
    /// /params sharedPreset Shared preset used to resolve module definitions.
    /// /params result Mutable compiled result.
    /// /returns None.
    /// </summary>
    private static void ApplyCoreMovement(EnemyModulesPatternDefinition pattern,
                                          EnemyModulesAndPatternsPreset sharedPreset,
                                          ref EnemyCompiledPatternBakeResult result)
    {
        if (pattern == null || sharedPreset == null)
            return;

        EnemyPatternCoreMovementAssembly coreMovement = pattern.CoreMovement;

        if (coreMovement == null || coreMovement.Binding == null)
            return;

        EnemyPatternModuleBinding binding = coreMovement.Binding;
        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return;

        EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);
        int selectedPriority = 0;
        bool hasCustomMovement = result.HasCustomMovement;
        EnemyAdvancedPatternBakeUtility.TryApplyMovementModule(moduleDefinition.ModuleKind,
                                                               resolvedPayload,
                                                               ref result.PatternConfig,
                                                               ref selectedPriority,
                                                               ref hasCustomMovement);
        result.HasCustomMovement = hasCustomMovement;
    }

    /// <summary>
    /// Applies the optional short-range interaction selection to the compiled pattern config.
    /// /params pattern Shared pattern definition currently being compiled.
    /// /params sharedPreset Shared preset used to resolve module definitions.
    /// /params patternConfig Mutable compiled pattern config.
    /// /returns None.
    /// </summary>
    private static void ApplyShortRangeInteraction(EnemyModulesPatternDefinition pattern,
                                                   EnemyModulesAndPatternsPreset sharedPreset,
                                                   ref EnemyPatternConfig patternConfig)
    {
        if (pattern == null || sharedPreset == null)
            return;

        EnemyPatternShortRangeInteractionAssembly shortRangeInteraction = pattern.ShortRangeInteraction;

        if (shortRangeInteraction == null || !shortRangeInteraction.IsEnabled || shortRangeInteraction.Binding == null)
            return;

        EnemyPatternModuleBinding binding = shortRangeInteraction.Binding;
        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return;

        EnemyPatternModuleKind moduleKind = EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind);

        if (moduleKind != EnemyPatternModuleKind.Grunt &&
            moduleKind != EnemyPatternModuleKind.Coward &&
            moduleKind != EnemyPatternModuleKind.ShortRangeDash)
            return;

        patternConfig.HasShortRangeInteraction = 1;
        patternConfig.ShortRangeActivationRange = math.max(0f, shortRangeInteraction.ActivationRange);
        patternConfig.ShortRangeReleaseDistanceBuffer = math.max(0f, shortRangeInteraction.ReleaseDistanceBuffer);

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Coward:
                patternConfig.ShortRangeMovementKind = EnemyCompiledMovementPatternKind.Coward;
                break;

            case EnemyPatternModuleKind.ShortRangeDash:
                patternConfig.ShortRangeMovementKind = EnemyCompiledMovementPatternKind.ShortRangeDash;
                break;

            default:
                patternConfig.ShortRangeMovementKind = EnemyCompiledMovementPatternKind.Grunt;
                break;
        }

        if (moduleKind == EnemyPatternModuleKind.Grunt)
            return;

        EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Coward:
                ApplyShortRangeCowardPayload(resolvedPayload, ref patternConfig);
                break;

            case EnemyPatternModuleKind.ShortRangeDash:
                EnemyAdvancedPatternBakeUtility.ApplyShortRangeDashPayload(resolvedPayload, ref patternConfig);
                break;
        }
    }

    /// <summary>
    /// Applies one optional weapon interaction to the compiled result.
    /// /params pattern Shared pattern definition currently being compiled.
    /// /params sharedPreset Shared preset used to resolve module definitions.
    /// /params result Mutable compiled result.
    /// /returns None.
    /// </summary>
    private static void ApplyWeaponInteraction(EnemyModulesPatternDefinition pattern,
                                               EnemyModulesAndPatternsPreset sharedPreset,
                                               ref EnemyCompiledPatternBakeResult result)
    {
        if (pattern == null || sharedPreset == null)
            return;

        EnemyPatternWeaponInteractionAssembly weaponInteraction = pattern.WeaponInteraction;

        if (weaponInteraction == null || !weaponInteraction.IsEnabled || weaponInteraction.Binding == null)
            return;

        EnemyPatternModuleBinding binding = weaponInteraction.Binding;
        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return;

        if (EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind) != EnemyPatternModuleKind.Shooter)
            return;

        int previousConfigCount = result.ShooterConfigs.Count;
        EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);
        EnemyAdvancedPatternBakeUtility.TryAddShooterModule(resolvedPayload, result.ShooterConfigs, ref result);

        for (int shooterIndex = previousConfigCount; shooterIndex < result.ShooterConfigs.Count; shooterIndex++)
        {
            EnemyShooterConfigElement shooterConfig = result.ShooterConfigs[shooterIndex];
            shooterConfig.UseMinimumRange = weaponInteraction.UseMinimumRange ? (byte)1 : (byte)0;
            shooterConfig.MinimumRange = math.max(0f, weaponInteraction.MinimumRange);
            shooterConfig.UseMaximumRange = weaponInteraction.UseMaximumRange ? (byte)1 : (byte)0;
            shooterConfig.MaximumRange = math.max(shooterConfig.MinimumRange, weaponInteraction.MaximumRange);
            shooterConfig.ExclusiveLookDirectionControl = weaponInteraction.ExclusiveLookDirectionControl ? (byte)1 : (byte)0;
            result.ShooterConfigs[shooterIndex] = shooterConfig;
        }
    }

    /// <summary>
    /// Applies the optional drop-items selection to the compiled result.
    /// /params pattern Shared pattern definition currently being compiled.
    /// /params sharedPreset Shared preset used to resolve module definitions.
    /// /params result Mutable compiled result.
    /// /returns None.
    /// </summary>
    private static void ApplyDropItems(EnemyModulesPatternDefinition pattern,
                                       EnemyModulesAndPatternsPreset sharedPreset,
                                       ref EnemyCompiledPatternBakeResult result)
    {
        if (pattern == null || sharedPreset == null)
            return;

        EnemyPatternDropItemsAssembly dropItems = pattern.DropItems;

        if (dropItems == null || !dropItems.IsEnabled || dropItems.Modules == null)
            return;

        IReadOnlyList<EnemyPatternModuleBinding> moduleBindings = dropItems.Modules;

        for (int moduleIndex = 0; moduleIndex < moduleBindings.Count; moduleIndex++)
        {
            EnemyPatternModuleBinding binding = moduleBindings[moduleIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

            if (moduleDefinition == null)
                continue;

            if (EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind) != EnemyPatternModuleKind.DropItems)
                continue;

            EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);
            EnemyDropItemsBakeUtility.TryAppendModule(resolvedPayload, ref result);
        }
    }

    /// <summary>
    /// Copies the short-range coward payload into the short-range section of the compiled pattern config.
    /// /params payload Resolved module payload for the short-range coward module.
    /// /params patternConfig Mutable compiled pattern config.
    /// /returns None.
    /// </summary>
    private static void ApplyShortRangeCowardPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        if (payload == null || payload.Coward == null)
            return;

        EnemyCowardModuleData coward = payload.Coward;
        patternConfig.ShortRangeSearchRadius = math.max(0.5f, coward.SearchRadius);
        patternConfig.ShortRangeMinimumTravelDistance = math.max(0f, coward.MinimumRetreatDistance);
        patternConfig.ShortRangeMaximumTravelDistance = math.max(patternConfig.ShortRangeMinimumTravelDistance, coward.MaximumRetreatDistance);
        patternConfig.ShortRangeArrivalTolerance = math.max(0.05f, coward.ArrivalTolerance);
        patternConfig.ShortRangeCandidateSampleCount = math.clamp(math.max(1, coward.CandidateSampleCount), 1, 64);
        patternConfig.ShortRangeUseInfiniteDirectionSampling = coward.UseInfiniteDirectionSampling ? (byte)1 : (byte)0;
        patternConfig.ShortRangeInfiniteDirectionStepDegrees = math.clamp(coward.InfiniteDirectionStepDegrees, 0.5f, 90f);
        patternConfig.ShortRangeMinimumEnemyClearance = math.max(0f, coward.MinimumEnemyClearance);
        patternConfig.ShortRangeTrajectoryPredictionTime = math.max(0f, coward.TrajectoryPredictionTime);
        patternConfig.ShortRangeFreeTrajectoryPreference = math.lerp(1f, 5f, math.saturate(coward.FreeTrajectoryPreference));
        patternConfig.ShortRangeBlockedPathRetryDelay = math.max(0f, coward.BlockedPathRetryDelay);
        patternConfig.ShortRangeRetreatDirectionPreference = math.saturate(coward.RetreatDirectionPreference);
        patternConfig.ShortRangeOpenSpacePreference = math.saturate(coward.OpenSpacePreference);
        patternConfig.ShortRangeNavigationPreference = math.saturate(coward.NavigationRetreatPreference);
        patternConfig.ShortRangeRetreatSpeedMultiplierFar = math.max(0f, coward.RetreatSpeedMultiplierFar);
        patternConfig.ShortRangeRetreatSpeedMultiplierNear = math.max(patternConfig.ShortRangeRetreatSpeedMultiplierFar,
                                                                      coward.RetreatSpeedMultiplierNear);
    }

    /// <summary>
    /// Resolves whether the compiled pattern still requires the custom pattern movement system after the new category split.
    /// /params patternConfig Compiled pattern config.
    /// /returns True when the pattern should keep the custom movement tag.
    /// </summary>
    private static bool ResolveHasCustomMovement(EnemyPatternConfig patternConfig)
    {
        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.Grunt)
            return true;

        if (patternConfig.HasShortRangeInteraction == 0)
            return false;

        return patternConfig.ShortRangeMovementKind != EnemyCompiledMovementPatternKind.Grunt;
    }
    #endregion

    #endregion
}
