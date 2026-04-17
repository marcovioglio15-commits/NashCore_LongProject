using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds combo runtime data and applies active combo-rank bonuses onto the effective scalable-stat view.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerRuntimeScalingComboApplyUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the mutable combo runtime config and rank thresholds from immutable baselines plus Add Scaling metadata.
    /// /params baseComboConfig Immutable combo baseline.
    /// /params runtimeComboConfig Mutable runtime combo config rebuilt in place.
    /// /params baseComboRanks Immutable combo-rank baseline buffer.
    /// /params runtimeComboRanks Mutable runtime combo-rank buffer rebuilt in place.
    /// /params comboScaling Combo scaling metadata baked from Add Scaling rules.
    /// /params variableContext Current typed scalable-stat context used to evaluate formulas.
    /// /returns void.
    /// </summary>
    public static void RebuildRuntimeComboCounter(in PlayerBaseComboCounterConfig baseComboConfig,
                                                  ref PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                                  DynamicBuffer<PlayerBaseComboRankElement> baseComboRanks,
                                                  DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                                  DynamicBuffer<PlayerRuntimeComboCounterScalingElement> comboScaling,
                                                  IReadOnlyDictionary<string, PlayerFormulaValue> variableContext)
    {
        runtimeComboConfig = new PlayerRuntimeComboCounterConfig
        {
            Enabled = baseComboConfig.Enabled,
            ComboGainPerKill = baseComboConfig.ComboGainPerKill,
            DamageBreakMode = baseComboConfig.DamageBreakMode,
            ShieldDamageBreaksCombo = baseComboConfig.ShieldDamageBreaksCombo
        };

        if (!runtimeComboRanks.IsCreated)
        {
            return;
        }

        runtimeComboRanks.Clear();

        if (baseComboRanks.IsCreated)
        {
            for (int rankIndex = 0; rankIndex < baseComboRanks.Length; rankIndex++)
            {
                PlayerBaseComboRankElement baseRank = baseComboRanks[rankIndex];
                runtimeComboRanks.Add(new PlayerRuntimeComboRankElement
                {
                    RankId = baseRank.RankId,
                    RequiredComboValue = baseRank.RequiredComboValue,
                    BonusFormulaStartIndex = baseRank.BonusFormulaStartIndex,
                    BonusFormulaCount = baseRank.BonusFormulaCount
                });
            }
        }

        if (!comboScaling.IsCreated)
        {
            return;
        }

        for (int scalingIndex = 0; scalingIndex < comboScaling.Length; scalingIndex++)
        {
            PlayerRuntimeComboCounterScalingElement scalingElement = comboScaling[scalingIndex];

            if ((PlayerFormulaValueType)scalingElement.ValueType == PlayerFormulaValueType.Boolean)
            {
                if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateBooleanValue(scalingElement.Formula.ToString(),
                                                                                          scalingElement.BaseBooleanValue != 0,
                                                                                          variableContext,
                                                                                          out bool resolvedBoolean))
                {
                    continue;
                }

                ApplyComboBooleanValue(scalingElement.FieldId,
                                       resolvedBoolean,
                                       ref runtimeComboConfig);
                continue;
            }

            if (!PlayerRuntimeScalingFormulaEvaluationUtility.TryEvaluateNumericValue(scalingElement.Formula.ToString(),
                                                                                      scalingElement.BaseValue,
                                                                                      scalingElement.IsInteger != 0,
                                                                                      variableContext,
                                                                                      out float resolvedValue))
            {
                continue;
            }

            ApplyComboNumericValue(scalingElement.FieldId,
                                   scalingElement.RankIndex,
                                   resolvedValue,
                                   ref runtimeComboConfig,
                                   runtimeComboRanks);
        }
    }

    /// <summary>
    /// Copies the current scalable-stat buffer into the mutable list that receives temporary combo rank bonuses.
    /// /params scalableStats Source scalable-stat buffer.
    /// /params destination Mutable list reused as effective scalable-stat state.
    /// /returns void.
    /// </summary>
    public static void CopyBaseScalableStats(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                             List<PlayerScalableStatElement> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();

        if (!scalableStats.IsCreated || scalableStats.Length <= 0)
        {
            return;
        }

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            destination.Add(scalableStats[statIndex]);
        }
    }

    /// <summary>
    /// Applies cumulative Character Tuning formulas from every active combo rank onto the effective scalable-stat list.
    /// /params activeRankIndex Highest currently active combo-rank index, or -1 when no rank is active.
    /// /params runtimeComboRanks Current runtime combo-rank buffer.
    /// /params characterTuningFormulas Shared Character Tuning formula buffer.
    /// /params mutableScalableStats Mutable effective scalable-stat list updated in place.
    /// /returns void.
    /// </summary>
    public static void ApplyActiveComboRankBonuses(int activeRankIndex,
                                                   DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks,
                                                   DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas,
                                                   List<PlayerScalableStatElement> mutableScalableStats)
    {
        if (activeRankIndex < 0)
        {
            return;
        }

        if (mutableScalableStats == null || mutableScalableStats.Count <= 0)
        {
            return;
        }

        for (int rankIndex = 0; rankIndex <= activeRankIndex && rankIndex < runtimeComboRanks.Length; rankIndex++)
        {
            PlayerRuntimeComboRankElement runtimeRank = runtimeComboRanks[rankIndex];
            PlayerPowerUpCharacterTuningRuntimeUtility.TryApplyCharacterTuningRange(runtimeRank.BonusFormulaStartIndex,
                                                                                    runtimeRank.BonusFormulaCount,
                                                                                    characterTuningFormulas,
                                                                                    mutableScalableStats,
                                                                                    out int _);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one boolean combo scaling result to the runtime combo config.
    /// /params fieldId Target combo field identifier.
    /// /params resolvedBoolean Evaluated boolean value.
    /// /params runtimeComboConfig Mutable runtime combo config updated in place.
    /// /returns void.
    /// </summary>
    private static void ApplyComboBooleanValue(PlayerRuntimeComboCounterFieldId fieldId,
                                               bool resolvedBoolean,
                                               ref PlayerRuntimeComboCounterConfig runtimeComboConfig)
    {
        switch (fieldId)
        {
            case PlayerRuntimeComboCounterFieldId.Enabled:
                runtimeComboConfig.Enabled = resolvedBoolean ? (byte)1 : (byte)0;
                break;
            case PlayerRuntimeComboCounterFieldId.ShieldDamageBreaksCombo:
                runtimeComboConfig.ShieldDamageBreaksCombo = resolvedBoolean ? (byte)1 : (byte)0;
                break;
        }
    }

    /// <summary>
    /// Applies one numeric combo scaling result to the runtime combo config or one runtime combo rank.
    /// /params fieldId Target combo field identifier.
    /// /params rankIndex Runtime rank index addressed by the scaling metadata.
    /// /params resolvedValue Evaluated numeric value.
    /// /params runtimeComboConfig Mutable runtime combo config updated in place.
    /// /params runtimeComboRanks Mutable runtime combo-rank buffer updated in place.
    /// /returns void.
    /// </summary>
    private static void ApplyComboNumericValue(PlayerRuntimeComboCounterFieldId fieldId,
                                               int rankIndex,
                                               float resolvedValue,
                                               ref PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                               DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks)
    {
        switch (fieldId)
        {
            case PlayerRuntimeComboCounterFieldId.ComboGainPerKill:
                runtimeComboConfig.ComboGainPerKill = math.max(0, (int)math.round(resolvedValue));
                break;
            case PlayerRuntimeComboCounterFieldId.DamageBreakMode:
                runtimeComboConfig.DamageBreakMode = PlayerRuntimeScalingEnumUtility.ResolveComboDamageBreakMode(resolvedValue);
                break;
            case PlayerRuntimeComboCounterFieldId.RankRequiredComboValue:
                if (rankIndex < 0 || rankIndex >= runtimeComboRanks.Length)
                {
                    return;
                }

                PlayerRuntimeComboRankElement runtimeRank = runtimeComboRanks[rankIndex];
                runtimeRank.RequiredComboValue = math.max(0, (int)math.round(resolvedValue));
                runtimeComboRanks[rankIndex] = runtimeRank;
                break;
        }
    }
    #endregion

    #endregion
}
