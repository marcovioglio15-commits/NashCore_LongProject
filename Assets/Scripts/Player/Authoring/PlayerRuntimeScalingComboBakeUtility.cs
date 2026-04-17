using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds combo-counter baselines, runtime data, and Add Scaling metadata used by progression baking.
/// none.
/// returns none.
/// </summary>
internal static class PlayerRuntimeScalingComboBakeUtility
{
    #region Constants
    private const string ComboRanksRoot = "comboCounter.rankDefinitions.Array.data[";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Populates combo base/runtime configs, combo ranks, and flattened rank-bonus formulas from progression presets.
    /// /params scaledPreset Scaled progression preset currently used by bake.
    /// /params sourcePreset Unscaled progression preset used as immutable baseline.
    /// /params baseRanks Destination immutable combo-rank buffer.
    /// /params runtimeRanks Destination runtime combo-rank buffer initialized from the scaled preset.
    /// /params characterTuningFormulaBuffer Shared flattened Character Tuning formula buffer appended with combo rank bonuses.
    /// /params baseConfig Resolved immutable combo runtime config.
    /// /params runtimeConfig Resolved scaled combo runtime config.
    /// /returns void.
    /// </summary>
    public static void PopulateComboCounterRuntimeData(PlayerProgressionPreset scaledPreset,
                                                       PlayerProgressionPreset sourcePreset,
                                                       DynamicBuffer<PlayerBaseComboRankElement> baseRanks,
                                                       DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                                       DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer,
                                                       out PlayerBaseComboCounterConfig baseConfig,
                                                       out PlayerRuntimeComboCounterConfig runtimeConfig)
    {
        baseRanks.Clear();
        runtimeRanks.Clear();

        PlayerComboCounterDefinition sourceCombo = sourcePreset != null ? sourcePreset.ComboCounter : null;
        PlayerComboCounterDefinition scaledCombo = scaledPreset != null ? scaledPreset.ComboCounter : null;
        PlayerBaseComboCounterConfig resolvedBaseConfig = BuildComboConfig(sourceCombo);
        PlayerBaseComboCounterConfig resolvedRuntimeSourceConfig = BuildComboConfig(scaledCombo != null ? scaledCombo : sourceCombo);
        baseConfig = resolvedBaseConfig;
        runtimeConfig = new PlayerRuntimeComboCounterConfig
        {
            Enabled = resolvedRuntimeSourceConfig.Enabled,
            ComboGainPerKill = resolvedRuntimeSourceConfig.ComboGainPerKill,
            DamageBreakMode = resolvedRuntimeSourceConfig.DamageBreakMode,
            ShieldDamageBreaksCombo = resolvedRuntimeSourceConfig.ShieldDamageBreaksCombo
        };

        IReadOnlyList<PlayerComboRankDefinition> sourceRanks = sourceCombo != null ? sourceCombo.RankDefinitions : null;
        IReadOnlyList<PlayerComboRankDefinition> scaledRanks = scaledCombo != null ? scaledCombo.RankDefinitions : null;
        int sourceRankCount = sourceRanks != null ? sourceRanks.Count : 0;
        int scaledRankCount = scaledRanks != null ? scaledRanks.Count : 0;
        int rankCount = math.max(sourceRankCount, scaledRankCount);

        for (int rankIndex = 0; rankIndex < rankCount; rankIndex++)
        {
            PlayerComboRankDefinition sourceRank = sourceRanks != null && rankIndex < sourceRankCount ? sourceRanks[rankIndex] : null;
            PlayerComboRankDefinition scaledRank = scaledRanks != null && rankIndex < scaledRankCount ? scaledRanks[rankIndex] : null;
            PlayerComboRankDefinition formulaSourceRank = sourceRank != null ? sourceRank : scaledRank;
            string rankId = ResolveRankId(rankIndex, sourceRank, scaledRank);
            int requiredBaseValue = sourceRank != null
                ? sourceRank.RequiredComboValue
                : scaledRank != null ? scaledRank.RequiredComboValue : 0;
            int requiredRuntimeValue = scaledRank != null
                ? scaledRank.RequiredComboValue
                : sourceRank != null ? sourceRank.RequiredComboValue : 0;
            int formulaStartIndex = characterTuningFormulaBuffer.Length;
            int formulaCount = AppendRankBonusFormulas(formulaSourceRank, characterTuningFormulaBuffer);

            baseRanks.Add(new PlayerBaseComboRankElement
            {
                RankId = new FixedString64Bytes(rankId),
                RequiredComboValue = requiredBaseValue,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount
            });
            runtimeRanks.Add(new PlayerRuntimeComboRankElement
            {
                RankId = new FixedString64Bytes(rankId),
                RequiredComboValue = requiredRuntimeValue,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount
            });
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Populates combo Add Scaling metadata from the unscaled progression preset.
    /// /params sourcePreset Unscaled progression preset inspected for enabled Add Scaling rules.
    /// /params scalingBuffer Destination combo scaling metadata buffer.
    /// /returns void.
    /// </summary>
    public static void PopulateComboCounterScalingMetadata(PlayerProgressionPreset sourcePreset,
                                                           DynamicBuffer<PlayerRuntimeComboCounterScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();

        if (sourcePreset == null || sourcePreset.ScalingRules == null || sourcePreset.ScalingRules.Count <= 0)
        {
            return;
        }

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < sourcePreset.ScalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = sourcePreset.ScalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(scalingRule.Formula))
            {
                continue;
            }

            if (!TryMapComboFieldId(scalingRule.StatKey, out int rankIndex, out PlayerRuntimeComboCounterFieldId fieldId))
            {
                continue;
            }

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
            {
                continue;
            }

            if (!PlayerRuntimeScalingBakeUtility.TryResolveScalingBaseMetadata(property,
                                                                               out byte valueType,
                                                                               out float baseValue,
                                                                               out byte baseBooleanValue,
                                                                               out byte isInteger))
            {
                continue;
            }

            scalingBuffer.Add(new PlayerRuntimeComboCounterScalingElement
            {
                FieldId = fieldId,
                RankIndex = rankIndex,
                ValueType = valueType,
                BaseValue = baseValue,
                BaseBooleanValue = baseBooleanValue,
                IsInteger = isInteger,
                Formula = new FixedString512Bytes(PlayerRuntimeScalingBakeUtility.ResolveStoredFormula(scalingRule.Formula,
                                                                                                       property,
                                                                                                       null))
            });
        }
    }
#endif
    #endregion

    #region Private Methods
    /// <summary>
    /// Converts one authored combo definition into the runtime config struct used by base/runtime buffers.
    /// /params comboDefinition Authored combo definition inspected for runtime values.
    /// /returns Resolved combo runtime config.
    /// </summary>
    private static PlayerBaseComboCounterConfig BuildComboConfig(PlayerComboCounterDefinition comboDefinition)
    {
        return new PlayerBaseComboCounterConfig
        {
            Enabled = comboDefinition != null && comboDefinition.IsEnabled ? (byte)1 : (byte)0,
            ComboGainPerKill = comboDefinition != null ? comboDefinition.ComboGainPerKill : 0,
            DamageBreakMode = comboDefinition != null ? comboDefinition.DamageBreakMode : PlayerComboDamageBreakMode.ResetCombo,
            ShieldDamageBreaksCombo = comboDefinition != null && comboDefinition.ShieldDamageBreaksCombo ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Resolves the stable runtime rank identifier without mutating authoring data.
    /// /params rankIndex Zero-based authored rank index.
    /// /params sourceRank Rank entry taken from the unscaled preset when available.
    /// /params scaledRank Rank entry taken from the scaled preset when available.
    /// /returns Stable runtime rank identifier used for presentation and Add Scaling keys.
    /// </summary>
    private static string ResolveRankId(int rankIndex,
                                        PlayerComboRankDefinition sourceRank,
                                        PlayerComboRankDefinition scaledRank)
    {
        string resolvedRankId = sourceRank != null && !string.IsNullOrWhiteSpace(sourceRank.RankId)
            ? sourceRank.RankId.Trim()
            : scaledRank != null && !string.IsNullOrWhiteSpace(scaledRank.RankId)
                ? scaledRank.RankId.Trim()
                : string.Empty;

        if (!string.IsNullOrWhiteSpace(resolvedRankId))
        {
            return resolvedRankId;
        }

        return string.Format("Rank{0:00}", rankIndex + 1);
    }

    /// <summary>
    /// Appends all valid Character Tuning formulas defined by one combo rank into the shared flattened runtime buffer.
    /// /params rankDefinition Authored combo rank inspected for bonus formulas.
    /// /params characterTuningFormulaBuffer Shared flattened Character Tuning formula buffer.
    /// /returns Number of formulas appended for the provided rank.
    /// </summary>
    private static int AppendRankBonusFormulas(PlayerComboRankDefinition rankDefinition,
                                               DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer)
    {
        PowerUpCharacterTuningModuleData rankBonuses = rankDefinition != null ? rankDefinition.RankBonuses : null;
        IReadOnlyList<PowerUpCharacterTuningFormulaData> formulas = rankBonuses != null ? rankBonuses.Formulas : null;

        if (formulas == null)
        {
            return 0;
        }

        int appendedFormulaCount = 0;

        for (int formulaIndex = 0; formulaIndex < formulas.Count; formulaIndex++)
        {
            PowerUpCharacterTuningFormulaData formulaData = formulas[formulaIndex];
            string formula = formulaData != null ? formulaData.Formula : string.Empty;

            if (string.IsNullOrWhiteSpace(formula))
            {
                continue;
            }

            characterTuningFormulaBuffer.Add(new PlayerPowerUpCharacterTuningFormulaElement
            {
                Formula = new FixedString128Bytes(formula.Trim())
            });
            appendedFormulaCount += 1;
        }

        return appendedFormulaCount;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Maps one progression Add Scaling stat key to the combo runtime field targeted by that rule.
    /// /params statKey Stable Add Scaling stat key emitted by the progression preset.
    /// /params rankIndex Resolved combo rank index when the mapping targets one rank milestone.
    /// /params fieldId Resolved combo runtime field identifier.
    /// /returns True when the stat key targets the combo module; otherwise false.
    /// </summary>
    private static bool TryMapComboFieldId(string statKey,
                                           out int rankIndex,
                                           out PlayerRuntimeComboCounterFieldId fieldId)
    {
        rankIndex = -1;
        fieldId = default;

        if (string.IsNullOrWhiteSpace(statKey))
        {
            return false;
        }

        if (string.Equals(statKey, "comboCounter.isEnabled", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.Enabled;
            return true;
        }

        if (string.Equals(statKey, "comboCounter.comboGainPerKill", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.ComboGainPerKill;
            return true;
        }

        if (string.Equals(statKey, "comboCounter.shieldDamageBreaksCombo", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.ShieldDamageBreaksCombo;
            return true;
        }

        if (string.Equals(statKey, "comboCounter.damageBreakMode", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.DamageBreakMode;
            return true;
        }

        if (!statKey.StartsWith(ComboRanksRoot, StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryParseStableArrayIndex(statKey, out rankIndex))
        {
            rankIndex = -1;
            return false;
        }

        if (!statKey.EndsWith(".requiredComboValue", StringComparison.Ordinal))
        {
            rankIndex = -1;
            return false;
        }

        fieldId = PlayerRuntimeComboCounterFieldId.RankRequiredComboValue;
        return true;
    }

    /// <summary>
    /// Extracts the authored array index from one stable Add Scaling key token such as data[2|rankId:S].
    /// /params statKey Stable Add Scaling stat key containing one combo-rank array token.
    /// /params rankIndex Parsed authored rank index.
    /// /returns True when the array token was parsed successfully; otherwise false.
    /// </summary>
    private static bool TryParseStableArrayIndex(string statKey, out int rankIndex)
    {
        rankIndex = -1;

        int dataStartIndex = statKey.IndexOf("data[", StringComparison.Ordinal);
        int dataEndIndex = statKey.IndexOf(']', dataStartIndex);

        if (dataStartIndex < 0 || dataEndIndex <= dataStartIndex)
        {
            return false;
        }

        string token = statKey.Substring(dataStartIndex + 5, dataEndIndex - dataStartIndex - 5);
        int separatorIndex = token.IndexOf('|');
        string indexText = separatorIndex >= 0 ? token.Substring(0, separatorIndex) : token;
        return int.TryParse(indexText, out rankIndex);
    }
#endif
    #endregion

    #endregion
}
