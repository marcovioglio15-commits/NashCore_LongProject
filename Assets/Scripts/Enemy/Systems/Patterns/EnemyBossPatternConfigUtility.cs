/// <summary>
/// Provides shared helpers for composing boss movement categories into one runtime pattern config.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyBossPatternConfigUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a complete pattern config from independently selected boss core and short-range rules.
    /// /params defaultConfig Baseline config used when a category has no active rule.
    /// /params hasCoreConfig True when coreConfig contains the selected core movement rule.
    /// /params coreConfig Selected core movement config.
    /// /params hasShortRangeConfig True when shortRangeConfig contains the selected short-range interaction rule.
    /// /params shortRangeConfig Selected short-range interaction config.
    /// /returns Merged pattern config ready for runtime assignment.
    /// </summary>
    public static EnemyPatternConfig BuildMergedConfig(EnemyPatternConfig defaultConfig,
                                                       bool hasCoreConfig,
                                                       in EnemyPatternConfig coreConfig,
                                                       bool hasShortRangeConfig,
                                                       in EnemyPatternConfig shortRangeConfig)
    {
        EnemyPatternConfig mergedConfig = hasCoreConfig ? coreConfig : defaultConfig;

        if (hasShortRangeConfig)
            CopyShortRangeInteraction(ref mergedConfig, in shortRangeConfig);
        else
            ClearShortRangeInteraction(ref mergedConfig);

        return mergedConfig;
    }

    /// <summary>
    /// Resolves whether a merged pattern config needs custom pattern movement systems.
    /// /params patternConfig Pattern config to inspect.
    /// /returns True when the config uses non-default movement behaviour.
    /// </summary>
    public static bool RequiresCustomMovement(in EnemyPatternConfig patternConfig)
    {
        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.Grunt)
            return true;

        if (patternConfig.HasShortRangeInteraction == 0)
            return false;

        return patternConfig.ShortRangeMovementKind != EnemyCompiledMovementPatternKind.Grunt;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Copies only the short-range interaction fields from a source config into the target config.
    /// /params targetConfig Pattern config receiving short-range fields.
    /// /params sourceConfig Pattern config containing compiled short-range fields.
    /// /returns None.
    /// </summary>
    private static void CopyShortRangeInteraction(ref EnemyPatternConfig targetConfig, in EnemyPatternConfig sourceConfig)
    {
        targetConfig.HasShortRangeInteraction = sourceConfig.HasShortRangeInteraction;
        targetConfig.ShortRangeMovementKind = sourceConfig.ShortRangeMovementKind;
        targetConfig.ShortRangeActivationRange = sourceConfig.ShortRangeActivationRange;
        targetConfig.ShortRangeReleaseDistanceBuffer = sourceConfig.ShortRangeReleaseDistanceBuffer;
        targetConfig.ShortRangeSearchRadius = sourceConfig.ShortRangeSearchRadius;
        targetConfig.ShortRangeMinimumTravelDistance = sourceConfig.ShortRangeMinimumTravelDistance;
        targetConfig.ShortRangeMaximumTravelDistance = sourceConfig.ShortRangeMaximumTravelDistance;
        targetConfig.ShortRangeArrivalTolerance = sourceConfig.ShortRangeArrivalTolerance;
        targetConfig.ShortRangeCandidateSampleCount = sourceConfig.ShortRangeCandidateSampleCount;
        targetConfig.ShortRangeUseInfiniteDirectionSampling = sourceConfig.ShortRangeUseInfiniteDirectionSampling;
        targetConfig.ShortRangeInfiniteDirectionStepDegrees = sourceConfig.ShortRangeInfiniteDirectionStepDegrees;
        targetConfig.ShortRangeMinimumEnemyClearance = sourceConfig.ShortRangeMinimumEnemyClearance;
        targetConfig.ShortRangeTrajectoryPredictionTime = sourceConfig.ShortRangeTrajectoryPredictionTime;
        targetConfig.ShortRangeFreeTrajectoryPreference = sourceConfig.ShortRangeFreeTrajectoryPreference;
        targetConfig.ShortRangeBlockedPathRetryDelay = sourceConfig.ShortRangeBlockedPathRetryDelay;
        targetConfig.ShortRangeRetreatDirectionPreference = sourceConfig.ShortRangeRetreatDirectionPreference;
        targetConfig.ShortRangeOpenSpacePreference = sourceConfig.ShortRangeOpenSpacePreference;
        targetConfig.ShortRangeNavigationPreference = sourceConfig.ShortRangeNavigationPreference;
        targetConfig.ShortRangeRetreatSpeedMultiplierFar = sourceConfig.ShortRangeRetreatSpeedMultiplierFar;
        targetConfig.ShortRangeRetreatSpeedMultiplierNear = sourceConfig.ShortRangeRetreatSpeedMultiplierNear;
        targetConfig.ShortRangeDashAimDuration = sourceConfig.ShortRangeDashAimDuration;
        targetConfig.ShortRangeDashAimMoveSpeedMultiplier = sourceConfig.ShortRangeDashAimMoveSpeedMultiplier;
        targetConfig.ShortRangeDashCooldownSeconds = sourceConfig.ShortRangeDashCooldownSeconds;
        targetConfig.ShortRangeDashDuration = sourceConfig.ShortRangeDashDuration;
        targetConfig.ShortRangeDashDistanceSource = sourceConfig.ShortRangeDashDistanceSource;
        targetConfig.ShortRangeDashDistanceMultiplier = sourceConfig.ShortRangeDashDistanceMultiplier;
        targetConfig.ShortRangeDashDistanceOffset = sourceConfig.ShortRangeDashDistanceOffset;
        targetConfig.ShortRangeDashFixedDistance = sourceConfig.ShortRangeDashFixedDistance;
        targetConfig.ShortRangeDashMinimumTravelDistance = sourceConfig.ShortRangeDashMinimumTravelDistance;
        targetConfig.ShortRangeDashMaximumTravelDistance = sourceConfig.ShortRangeDashMaximumTravelDistance;
        targetConfig.ShortRangeDashLateralAmplitude = sourceConfig.ShortRangeDashLateralAmplitude;
        targetConfig.ShortRangeDashMirrorMode = sourceConfig.ShortRangeDashMirrorMode;
        targetConfig.ShortRangeDashPathSamples = sourceConfig.ShortRangeDashPathSamples;
    }

    /// <summary>
    /// Clears short-range fields when no boss short-range rule is currently active.
    /// /params targetConfig Pattern config receiving the cleared short-range state.
    /// /returns None.
    /// </summary>
    private static void ClearShortRangeInteraction(ref EnemyPatternConfig targetConfig)
    {
        targetConfig.HasShortRangeInteraction = 0;
        targetConfig.ShortRangeMovementKind = EnemyCompiledMovementPatternKind.Grunt;
    }
    #endregion

    #endregion
}
