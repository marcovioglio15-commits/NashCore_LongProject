using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies Character Tuning formulas to runtime scalable stats and synchronizes dependent progression state.
/// </summary>
public static class PlayerPowerUpCharacterTuningRuntimeUtility
{
    #region Fields
    private static readonly Dictionary<string, float> variableContext = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether the provided Character Tuning entry should be applied permanently on acquisition.
    ///  unlockCatalogEntry: Unlock catalog entry inspected for runtime-scoped application rules.
    /// returns True when acquisition should apply the formulas immediately; otherwise false.
    /// </summary>
    public static bool ShouldApplyOnAcquisition(in PlayerPowerUpUnlockCatalogElement unlockCatalogEntry)
    {
        if (unlockCatalogEntry.CharacterTuningFormulaCount <= 0)
            return false;

        return !IsRuntimeScopedCharacterTuning(in unlockCatalogEntry);
    }

    /// <summary>
    /// Resolves whether the provided Character Tuning entry belongs to an active that must apply formulas only while its runtime state remains active.
    ///  unlockCatalogEntry: Unlock catalog entry inspected for temporary active-state application rules.
    /// returns True when the entry is runtime-scoped; otherwise false.
    /// </summary>
    public static bool IsRuntimeScopedCharacterTuning(in PlayerPowerUpUnlockCatalogElement unlockCatalogEntry)
    {
        if (unlockCatalogEntry.CharacterTuningFormulaCount <= 0)
            return false;

        if (unlockCatalogEntry.UnlockKind == PlayerPowerUpUnlockKind.Passive)
            return true;

        if (unlockCatalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Active)
            return false;

        if (unlockCatalogEntry.ActiveSlotConfig.IsDefined == 0)
            return false;

        if (unlockCatalogEntry.ActiveSlotConfig.ToolKind == ActiveToolKind.ChargeShot)
            return true;

        return unlockCatalogEntry.ActiveSlotConfig.Toggleable != 0;
    }

    /// <summary>
    /// Applies all Character Tuning formulas referenced by one unlock catalog entry and synchronizes progression state.
    ///  unlockCatalogEntry: Catalog entry containing the flattened formula range.
    ///  characterTuningFormulas: Flattened Character Tuning formula buffer.
    ///  scalableStats: Mutable scalable-stat buffer updated in place.
    ///  progressionConfig: Runtime progression config used to synchronize level requirements and pickup radius.
    ///  playerExperience: Mutable runtime experience component synchronized after formula execution.
    ///  playerLevel: Mutable runtime level component synchronized after formula execution.
    ///  playerExperienceCollection: Mutable runtime experience-collection component synchronized after formula execution.
    ///  appliedFormulaCount: Number of formulas successfully applied.
    /// returns True when at least one formula changed runtime scalable stats; otherwise false.
    /// </summary>
    public static bool TryApplyCharacterTuning(in PlayerPowerUpUnlockCatalogElement unlockCatalogEntry,
                                               DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                               DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                               PlayerProgressionConfig progressionConfig,
                                               DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                               ref PlayerExperience playerExperience,
                                               ref PlayerLevel playerLevel,
                                               ref PlayerExperienceCollection playerExperienceCollection,
                                               out int appliedFormulaCount)
    {
        if (!TryApplyCharacterTuningFormulas(in unlockCatalogEntry,
                                             characterTuningFormulas,
                                             scalableStats,
                                             out appliedFormulaCount))
        {
            return false;
        }

        SyncProgressionRuntimeState(scalableStats,
                                    progressionConfig,
                                    runtimeGamePhases,
                                    ref playerExperience,
                                    ref playerLevel,
                                    ref playerExperienceCollection);
        return true;
    }

    /// <summary>
    /// Applies all Character Tuning formulas referenced by one unlock catalog entry without synchronizing dependent progression state.
    ///  unlockCatalogEntry: Catalog entry containing the flattened formula range.
    ///  characterTuningFormulas: Flattened Character Tuning formula buffer.
    ///  scalableStats: Mutable scalable-stat buffer updated in place.
    ///  appliedFormulaCount: Number of formulas successfully applied.
    /// returns True when at least one formula changed runtime scalable stats; otherwise false.
    /// </summary>
    public static bool TryApplyCharacterTuningFormulas(in PlayerPowerUpUnlockCatalogElement unlockCatalogEntry,
                                                       DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                       DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                       out int appliedFormulaCount)
    {
        return TryApplyCharacterTuningRange(unlockCatalogEntry.CharacterTuningFormulaStartIndex,
                                            unlockCatalogEntry.CharacterTuningFormulaCount,
                                            characterTuningFormulas,
                                            scalableStats,
                                            out appliedFormulaCount);
    }

    /// <summary>
    /// Synchronizes progression runtime components and reserved scalable stats after Character Tuning changes.
    ///  scalableStats: Mutable scalable-stat buffer containing the latest values.
    ///  progressionConfig: Runtime progression config used to resolve the current level requirement and pickup radius.
    ///  playerExperience: Mutable runtime experience component.
    ///  playerLevel: Mutable runtime level component.
    ///  playerExperienceCollection: Mutable runtime experience-collection component.
    /// returns void.
    /// </summary>
    public static void SyncProgressionRuntimeState(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                   PlayerProgressionConfig progressionConfig,
                                                   DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                   ref PlayerExperience playerExperience,
                                                   ref PlayerLevel playerLevel,
                                                   ref PlayerExperienceCollection playerExperienceCollection)
    {
        float resolvedExperience = math.max(0f, ResolveScalableStatValue(scalableStats, "experience", playerExperience.Current));
        int levelCap = PlayerProgressionPhaseUtility.ResolveLevelCap(progressionConfig);
        int resolvedLevel = math.clamp((int)Math.Round(ResolveScalableStatValue(scalableStats, "level", playerLevel.Current),
                                                      MidpointRounding.AwayFromZero),
                                       0,
                                       levelCap);
        int activeGamePhaseIndex = PlayerProgressionPhaseUtility.ResolveActiveGamePhaseIndex(progressionConfig, resolvedLevel);
        float requiredExperienceForNextLevel = 0f;

        if (resolvedLevel < levelCap)
        {
            requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig,
                                                                                                             runtimeGamePhases,
                                                                                                             resolvedLevel,
                                                                                                             out activeGamePhaseIndex,
                                                                                                             out bool _,
                                                                                                             out int _);
        }

        playerExperience = new PlayerExperience
        {
            Current = resolvedExperience
        };
        playerLevel = new PlayerLevel
        {
            Current = resolvedLevel,
            ActiveGamePhaseIndex = activeGamePhaseIndex,
            RequiredExperienceForNextLevel = requiredExperienceForNextLevel
        };

        TryWriteReservedStatValue(scalableStats, "experience", resolvedExperience);
        TryWriteReservedStatValue(scalableStats, "level", resolvedLevel);
        PlayerExperiencePickupRadiusRuntimeUtility.SyncRuntimeComponent(ref playerExperienceCollection,
                                                                        progressionConfig,
                                                                        scalableStats);
    }

    /// <summary>
    /// Resolves one Character Tuning assignment target stat name from the raw formula string.
    ///  formula: Raw Character Tuning formula string.
    ///  targetStatName: Parsed target scalable-stat name when successful.
    /// returns True when the assignment target is valid; otherwise false.
    /// </summary>
    public static bool TryResolveTargetStatName(string formula, out string targetStatName)
    {
        targetStatName = string.Empty;

        if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(formula,
                                                                           out targetStatName,
                                                                           out string _,
                                                                           out string _))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(targetStatName);
    }

    /// <summary>
    /// Resolves one scalable-stat buffer index by name using case-insensitive lookup semantics.
    ///  scalableStats: Runtime scalable-stat buffer to scan.
    ///  statName: Requested scalable-stat identifier.
    /// returns Buffer index when found; otherwise -1.
    /// </summary>
    public static int FindScalableStatIndex(DynamicBuffer<PlayerScalableStatElement> scalableStats, string statName)
    {
        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (!string.Equals(scalableStat.Name.ToString(), statName, StringComparison.OrdinalIgnoreCase))
                continue;

            return statIndex;
        }

        return -1;
    }

    /// <summary>
    /// Normalizes one evaluated formula result according to the target scalable-stat type.
    ///  scalableStat: Target scalable-stat metadata.
    ///  evaluatedValue: Raw evaluated formula result.
    /// returns Stored runtime value after type normalization.
    /// </summary>
    public static float ResolveStatValue(in PlayerScalableStatElement scalableStat, float evaluatedValue)
    {
        return PlayerScalableStatClampUtility.ResolveNormalizedValue(in scalableStat, evaluatedValue);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one flattened Character Tuning formula range without synchronizing dependent progression state.
    ///  startIndex: Inclusive start index inside the flattened formula buffer.
    ///  formulaCount: Number of formulas to evaluate from startIndex.
    ///  characterTuningFormulas: Flattened Character Tuning formula buffer.
    ///  scalableStats: Mutable scalable-stat buffer updated in place.
    ///  appliedFormulaCount: Number of formulas successfully applied.
    /// returns True when at least one formula changed runtime scalable stats; otherwise false.
    /// </summary>
    private static bool TryApplyCharacterTuningRange(int startIndex,
                                                     int formulaCount,
                                                     DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                     DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                     out int appliedFormulaCount)
    {
        appliedFormulaCount = 0;

        if (!scalableStats.IsCreated || scalableStats.Length <= 0)
            return false;

        if (!characterTuningFormulas.IsCreated || formulaCount <= 0)
            return false;

        int clampedStartIndex = math.max(0, startIndex);
        int endIndex = math.min(characterTuningFormulas.Length, clampedStartIndex + math.max(0, formulaCount));

        if (clampedStartIndex >= endIndex)
            return false;

        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);

        for (int formulaIndex = clampedStartIndex; formulaIndex < endIndex; formulaIndex++)
        {
            string formula = characterTuningFormulas[formulaIndex].Formula.ToString();

            if (string.IsNullOrWhiteSpace(formula))
                continue;

            if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(formula,
                                                                               out string targetStatName,
                                                                               out string expression,
                                                                               out string _))
            {
                continue;
            }

            int scalableStatIndex = FindScalableStatIndex(scalableStats, targetStatName);

            if (scalableStatIndex < 0)
                continue;

            PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
            float currentValue = scalableStat.Value;

            if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(expression,
                                                                       currentValue,
                                                                       variableContext,
                                                                       out float evaluatedValue,
                                                                       out string _))
            {
                continue;
            }

            float resolvedValue = ResolveStatValue(in scalableStat, evaluatedValue);
            scalableStat.Value = resolvedValue;
            scalableStats[scalableStatIndex] = scalableStat;
            variableContext[targetStatName] = resolvedValue;
            appliedFormulaCount++;
        }

        return appliedFormulaCount > 0;
    }

    /// <summary>
    /// Resolves one scalable-stat value or returns a fallback when the stat is not present.
    ///  scalableStats: Runtime scalable-stat buffer to scan.
    ///  statName: Requested scalable-stat identifier.
    ///  fallbackValue: Fallback value returned when the stat does not exist.
    /// returns Resolved scalable-stat value or the provided fallback.
    /// </summary>
    private static float ResolveScalableStatValue(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                  string statName,
                                                  float fallbackValue)
    {
        int scalableStatIndex = FindScalableStatIndex(scalableStats, statName);

        if (scalableStatIndex < 0)
            return fallbackValue;

        return scalableStats[scalableStatIndex].Value;
    }

    /// <summary>
    /// Writes one reserved scalable-stat value back to the runtime buffer when the stat exists.
    ///  scalableStats: Mutable scalable-stat buffer updated in place.
    ///  statName: Reserved scalable-stat identifier to update.
    ///  value: New runtime value written to the buffer.
    /// returns void.
    /// </summary>
    private static void TryWriteReservedStatValue(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                  string statName,
                                                  float value)
    {
        int scalableStatIndex = FindScalableStatIndex(scalableStats, statName);

        if (scalableStatIndex < 0)
            return;

        PlayerScalableStatElement scalableStat = scalableStats[scalableStatIndex];
        scalableStat.Value = ResolveStatValue(in scalableStat, value);
        scalableStats[scalableStatIndex] = scalableStat;
    }
    #endregion

    #endregion
}
