using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes combo-rank resolution, HUD presentation data, and runtime-scaling signatures.
/// none.
/// returns none.
/// </summary>
internal static class PlayerComboCounterRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the highest currently active combo rank from the current runtime combo value.
    /// comboValue: Current combo numeric value.
    /// runtimeConfig: Current runtime combo config.
    /// runtimeRanks: Current runtime combo-rank thresholds.
    /// returns Highest active rank index, or -1 when no rank is active.
    /// </summary>
    public static int ResolveActiveRankIndex(int comboValue,
                                             in PlayerRuntimeComboCounterConfig runtimeConfig,
                                             DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        if (runtimeConfig.Enabled == 0)
        {
            return -1;
        }

        if (!runtimeRanks.IsCreated || runtimeRanks.Length <= 0)
        {
            return -1;
        }

        int sanitizedComboValue = math.max(0, comboValue);
        int activeRankIndex = -1;

        for (int rankIndex = 0; rankIndex < runtimeRanks.Length; rankIndex++)
        {
            PlayerRuntimeComboRankElement rankElement = runtimeRanks[rankIndex];

            if (sanitizedComboValue < math.max(0, rankElement.RequiredComboValue))
            {
                continue;
            }

            activeRankIndex = rankIndex;
        }

        return activeRankIndex;
    }

    /// <summary>
    /// Updates cached combo HUD data from the latest runtime combo config, thresholds, and combo value.
    /// comboCounterState: Mutable combo runtime state receiving presentation fields.
    /// runtimeConfig: Current runtime combo config.
    /// runtimeRanks: Current runtime combo-rank thresholds.
    /// returns void.
    /// </summary>
    public static void UpdatePresentation(ref PlayerComboCounterState comboCounterState,
                                          in PlayerRuntimeComboCounterConfig runtimeConfig,
                                          DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        int sanitizedComboValue = math.max(0, comboCounterState.CurrentValue);
        comboCounterState.CurrentValue = sanitizedComboValue;
        comboCounterState.CurrentRankIndex = -1;
        comboCounterState.CurrentRankId = default;
        comboCounterState.CurrentRankRequiredValue = 0;
        comboCounterState.NextRankRequiredValue = 0;
        comboCounterState.ProgressNormalized = 0f;

        if (runtimeConfig.Enabled == 0)
        {
            return;
        }

        if (!runtimeRanks.IsCreated || runtimeRanks.Length <= 0)
        {
            return;
        }

        int activeRankIndex = ResolveActiveRankIndex(sanitizedComboValue, in runtimeConfig, runtimeRanks);
        int nextRankIndex = ResolveNextRankIndex(sanitizedComboValue, runtimeRanks);

        if (activeRankIndex >= 0)
        {
            PlayerRuntimeComboRankElement activeRank = runtimeRanks[activeRankIndex];
            comboCounterState.CurrentRankIndex = activeRankIndex;
            comboCounterState.CurrentRankId = activeRank.RankId;
            comboCounterState.CurrentRankRequiredValue = math.max(0, activeRank.RequiredComboValue);
        }

        if (nextRankIndex >= 0)
        {
            comboCounterState.NextRankRequiredValue = math.max(0, runtimeRanks[nextRankIndex].RequiredComboValue);
        }

        comboCounterState.ProgressNormalized = ResolveProgressNormalized(sanitizedComboValue,
                                                                        comboCounterState.CurrentRankRequiredValue,
                                                                        comboCounterState.NextRankRequiredValue,
                                                                        activeRankIndex >= 0);
    }

    /// <summary>
    /// Combines the permanent scalable-stat signature with the currently active combo-rank signature used by runtime bonuses.
    /// scalableStatsHash: Hash built from permanent scalable stats.
    /// activeRankIndex: Currently active combo-rank index, or -1 when no combo bonus is active.
    /// returns Combined runtime-scaling signature.
    /// </summary>
    public static uint ComputeRuntimeScalingHash(uint scalableStatsHash, int activeRankIndex)
    {
        uint sanitizedActiveRankSignature = (uint)(math.max(-1, activeRankIndex) + 1);
        return math.hash(new uint2(scalableStatsHash, sanitizedActiveRankSignature));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the next unreached rank threshold after the current combo value.
    /// comboValue: Current combo numeric value.
    /// runtimeRanks: Current runtime combo-rank thresholds.
    /// returns Next unreached rank index, or -1 when the top rank is already active.
    /// </summary>
    private static int ResolveNextRankIndex(int comboValue,
                                            DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks)
    {
        int sanitizedComboValue = math.max(0, comboValue);

        for (int rankIndex = 0; rankIndex < runtimeRanks.Length; rankIndex++)
        {
            if (sanitizedComboValue >= math.max(0, runtimeRanks[rankIndex].RequiredComboValue))
            {
                continue;
            }

            return rankIndex;
        }

        return -1;
    }

    /// <summary>
    /// Resolves the normalized progress shown by the HUD bar for the current combo value.
    /// comboValue: Current combo numeric value.
    /// currentRankRequiredValue: Threshold of the currently active rank, or zero when none is active.
    /// nextRankRequiredValue: Threshold of the next rank, or zero when already at the top rank.
    /// hasActiveRank: True when at least one combo rank is active.
    /// returns Normalized progress in the 0..1 range.
    /// </summary>
    private static float ResolveProgressNormalized(int comboValue,
                                                   int currentRankRequiredValue,
                                                   int nextRankRequiredValue,
                                                   bool hasActiveRank)
    {
        int sanitizedComboValue = math.max(0, comboValue);
        int sanitizedCurrentRequiredValue = math.max(0, currentRankRequiredValue);
        int sanitizedNextRequiredValue = math.max(0, nextRankRequiredValue);

        if (sanitizedNextRequiredValue <= 0)
        {
            return hasActiveRank ? 1f : 0f;
        }

        if (!hasActiveRank)
        {
            return math.saturate((float)sanitizedComboValue / sanitizedNextRequiredValue);
        }

        int range = sanitizedNextRequiredValue - sanitizedCurrentRequiredValue;

        if (range <= 0)
        {
            return 1f;
        }

        return math.saturate((float)(sanitizedComboValue - sanitizedCurrentRequiredValue) / range);
    }
    #endregion

    #endregion
}
