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

        return CompilePattern(sharedPreset, selectedPattern, result);
    }

    /// <summary>
    /// Compiles one explicit shared pattern by ID so boss presets can reuse normal enemy pattern assemblies.
    /// /params sharedPreset Source normal enemy Modules & Patterns preset.
    /// /params patternId Pattern ID to compile.
    /// /returns The compiled bake result, or a default result when the pattern is unavailable.
    /// </summary>
    public static EnemyCompiledPatternBakeResult CompilePatternById(EnemyModulesAndPatternsPreset sharedPreset, string patternId)
    {
        EnemyCompiledPatternBakeResult result = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);

        if (sharedPreset == null)
            return result;

        EnemyModulesPatternDefinition selectedPattern = sharedPreset.ResolvePatternById(patternId);

        if (selectedPattern == null)
            return result;

        return CompilePattern(sharedPreset, selectedPattern, result);
    }

    /// <summary>
    /// Applies one core movement module binding to a compiled pattern result.
    /// /params sharedPreset Shared preset used to resolve module definitions.
    /// /params binding Module binding being compiled.
    /// /params result Mutable compiled result.
    /// /returns True when a legal core movement module was applied.
    /// </summary>
    internal static bool TryApplyCoreMovementModule(EnemyModulesAndPatternsPreset sharedPreset,
                                                    EnemyPatternModuleBinding binding,
                                                    ref EnemyCompiledPatternBakeResult result)
    {
        if (sharedPreset == null || binding == null)
            return false;

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return false;

        EnemyPatternModuleKind moduleKind = EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind);

        if (moduleKind != EnemyPatternModuleKind.Stationary &&
            moduleKind != EnemyPatternModuleKind.Grunt &&
            moduleKind != EnemyPatternModuleKind.Wanderer)
        {
            return false;
        }

        EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);
        int selectedPriority = 0;
        bool hasCustomMovement = result.HasCustomMovement;
        EnemyAdvancedPatternBakeUtility.TryApplyMovementModule(moduleKind,
                                                               resolvedPayload,
                                                               ref result.PatternConfig,
                                                               ref selectedPriority,
                                                               ref hasCustomMovement);
        result.HasCustomMovement = hasCustomMovement;
        return true;
    }

    /// <summary>
    /// Applies one short-range interaction module binding to a compiled pattern config.
    /// /params sharedPreset Shared preset used to resolve module definitions.
    /// /params binding Module binding being compiled.
    /// /params activationRange Player distance that activates the interaction.
    /// /params releaseDistanceBuffer Extra release buffer added after activation.
    /// /params patternConfig Mutable compiled pattern config.
    /// /returns True when a legal short-range interaction module was applied.
    /// </summary>
    internal static bool TryApplyShortRangeInteractionModule(EnemyModulesAndPatternsPreset sharedPreset,
                                                             EnemyPatternModuleBinding binding,
                                                             float activationRange,
                                                             float releaseDistanceBuffer,
                                                             ref EnemyPatternConfig patternConfig)
    {
        if (sharedPreset == null || binding == null)
            return false;

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return false;

        EnemyPatternModuleKind moduleKind = EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind);

        if (moduleKind != EnemyPatternModuleKind.Grunt &&
            moduleKind != EnemyPatternModuleKind.Coward &&
            moduleKind != EnemyPatternModuleKind.ShortRangeDash)
        {
            return false;
        }

        patternConfig.HasShortRangeInteraction = 1;
        patternConfig.ShortRangeActivationRange = math.max(0f, activationRange);
        patternConfig.ShortRangeReleaseDistanceBuffer = math.max(0f, releaseDistanceBuffer);

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
            return true;

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

        return true;
    }

    /// <summary>
    /// Adds one weapon interaction module binding to a compiled pattern result.
    /// /params sharedPreset Shared preset used to resolve module definitions.
    /// /params binding Module binding being compiled.
    /// /params useMinimumRange True when minimum range gating should be applied.
    /// /params minimumRange Authored minimum player range.
    /// /params useMaximumRange True when maximum range gating should be applied.
    /// /params maximumRange Authored maximum player range.
    /// /params exclusiveLookDirectionControl True when this weapon controls look direction while active.
    /// /params activationGates Additional non-range activation gates.
    /// /params maximumActivationSpeed Maximum enemy speed allowed by speed gating.
    /// /params recentlyDamagedWindowSeconds Recent damage window used by damage gating.
    /// /params result Mutable compiled result.
    /// /returns True when a legal weapon interaction module was applied.
    /// </summary>
    internal static bool TryAddWeaponInteractionModule(EnemyModulesAndPatternsPreset sharedPreset,
                                                       EnemyPatternModuleBinding binding,
                                                       bool useMinimumRange,
                                                       float minimumRange,
                                                       bool useMaximumRange,
                                                       float maximumRange,
                                                       bool exclusiveLookDirectionControl,
                                                       EnemyWeaponInteractionActivationGate activationGates,
                                                       float maximumActivationSpeed,
                                                       float recentlyDamagedWindowSeconds,
                                                       ref EnemyCompiledPatternBakeResult result)
    {
        if (sharedPreset == null || binding == null)
            return false;

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(binding.ModuleId);

        if (moduleDefinition == null)
            return false;

        if (EnemyAdvancedPatternBakeUtility.ResolveModuleKind(moduleDefinition.ModuleKind) != EnemyPatternModuleKind.Shooter)
            return false;

        int previousConfigCount = result.ShooterConfigs.Count;
        EnemyPatternModulePayloadData resolvedPayload = EnemyAdvancedPatternBakeUtility.ResolveBindingPayload(moduleDefinition, binding);
        EnemyAdvancedPatternBakeUtility.TryAddShooterModule(resolvedPayload, result.ShooterConfigs, ref result);

        for (int shooterIndex = previousConfigCount; shooterIndex < result.ShooterConfigs.Count; shooterIndex++)
        {
            EnemyShooterConfigElement shooterConfig = result.ShooterConfigs[shooterIndex];
            shooterConfig.UseMinimumRange = useMinimumRange ? (byte)1 : (byte)0;
            shooterConfig.MinimumRange = math.max(0f, minimumRange);
            shooterConfig.UseMaximumRange = useMaximumRange ? (byte)1 : (byte)0;
            shooterConfig.MaximumRange = math.max(shooterConfig.MinimumRange, maximumRange);
            shooterConfig.ExclusiveLookDirectionControl = exclusiveLookDirectionControl ? (byte)1 : (byte)0;
            shooterConfig.ActivationGates = ResolveWeaponActivationGates(activationGates);
            shooterConfig.MaximumActivationSpeed = math.max(0f, maximumActivationSpeed);
            shooterConfig.RecentlyDamagedWindowSeconds = math.max(0f, recentlyDamagedWindowSeconds);
            result.ShooterConfigs[shooterIndex] = shooterConfig;
        }

        return result.ShooterConfigs.Count > previousConfigCount;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Compiles one shared pattern definition into movement, shooter and drop buffers.
    /// /params sharedPreset Shared preset used to resolve module definitions.
    /// /params selectedPattern Shared assembled pattern to compile.
    /// /params result Existing result object that receives compiled values.
    /// /returns Compiled bake result.
    /// </summary>
    private static EnemyCompiledPatternBakeResult CompilePattern(EnemyModulesAndPatternsPreset sharedPreset,
                                                                 EnemyModulesPatternDefinition selectedPattern,
                                                                 EnemyCompiledPatternBakeResult result)
    {
        ApplyCoreMovement(selectedPattern, sharedPreset, ref result);
        ApplyShortRangeInteraction(selectedPattern, sharedPreset, ref result.PatternConfig);
        ApplyWeaponInteraction(selectedPattern, sharedPreset, ref result);
        ApplyDropItems(selectedPattern, sharedPreset, ref result);
        result.HasCustomMovement = ResolveHasCustomMovement(result.PatternConfig);
        return result;
    }

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
        if (pattern == null)
            return;

        EnemyPatternCoreMovementAssembly coreMovement = pattern.CoreMovement;

        if (coreMovement == null)
            return;

        TryApplyCoreMovementModule(sharedPreset, coreMovement.Binding, ref result);
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
        if (pattern == null)
            return;

        EnemyPatternShortRangeInteractionAssembly shortRangeInteraction = pattern.ShortRangeInteraction;

        if (shortRangeInteraction == null || !shortRangeInteraction.IsEnabled)
            return;

        TryApplyShortRangeInteractionModule(sharedPreset,
                                            shortRangeInteraction.Binding,
                                            shortRangeInteraction.ActivationRange,
                                            shortRangeInteraction.ReleaseDistanceBuffer,
                                            ref patternConfig);
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
        if (pattern == null)
            return;

        EnemyPatternWeaponInteractionAssembly weaponInteraction = pattern.WeaponInteraction;

        if (weaponInteraction == null || !weaponInteraction.IsEnabled)
            return;

        TryAddWeaponInteractionModule(sharedPreset,
                                      weaponInteraction.Binding,
                                      weaponInteraction.UseMinimumRange,
                                      weaponInteraction.MinimumRange,
                                      weaponInteraction.UseMaximumRange,
                                      weaponInteraction.MaximumRange,
                                      weaponInteraction.ExclusiveLookDirectionControl,
                                      weaponInteraction.ActivationGates,
                                      weaponInteraction.MaximumActivationSpeed,
                                      weaponInteraction.RecentlyDamagedWindowSeconds,
                                      ref result);
    }

    /// <summary>
    /// Resolves legal Weapon Interaction activation gate flags authored by the shared pattern assembly.
    /// /params gates Authored gate flags.
    /// /returns Sanitized gate flags.
    /// </summary>
    internal static EnemyWeaponInteractionActivationGate ResolveWeaponActivationGates(EnemyWeaponInteractionActivationGate gates)
    {
        EnemyWeaponInteractionActivationGate legalMask = EnemyWeaponInteractionActivationGate.RequireBelowSpeed |
                                                         EnemyWeaponInteractionActivationGate.RequireRecentlyDamaged |
                                                         EnemyWeaponInteractionActivationGate.RequireWandererWait;
        return gates & legalMask;
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
