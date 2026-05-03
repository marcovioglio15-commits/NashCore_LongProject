using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
    /// /params basePassiveUnlocks Destination immutable combo passive-unlock buffer.
    /// /params runtimePassiveUnlocks Destination runtime combo passive-unlock buffer initialized from the scaled preset.
    /// /params characterTuningFormulaBuffer Shared flattened Character Tuning formula buffer appended with combo rank bonuses.
    /// /params baseConfig Resolved immutable combo runtime config.
    /// /params runtimeConfig Resolved scaled combo runtime config.
    /// /returns void.
    /// </summary>
    public static void PopulateComboCounterRuntimeData(PlayerProgressionPreset scaledPreset,
                                                       PlayerProgressionPreset sourcePreset,
                                                       DynamicBuffer<PlayerBaseComboRankElement> baseRanks,
                                                       DynamicBuffer<PlayerRuntimeComboRankElement> runtimeRanks,
                                                       DynamicBuffer<PlayerBaseComboPassiveUnlockElement> basePassiveUnlocks,
                                                       DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks,
                                                       DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaBuffer,
                                                       out PlayerBaseComboCounterConfig baseConfig,
                                                       out PlayerRuntimeComboCounterConfig runtimeConfig)
    {
        baseRanks.Clear();
        runtimeRanks.Clear();
        basePassiveUnlocks.Clear();
        runtimePassiveUnlocks.Clear();

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
            ShieldDamageBreaksCombo = resolvedRuntimeSourceConfig.ShieldDamageBreaksCombo,
            PreventDecayIntoNonDecayingRanks = resolvedRuntimeSourceConfig.PreventDecayIntoNonDecayingRanks
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
            float pointsDecayPerSecondBaseValue = sourceRank != null
                ? sourceRank.PointsDecayPerSecond
                : scaledRank != null ? scaledRank.PointsDecayPerSecond : 0f;
            float pointsDecayPerSecondRuntimeValue = scaledRank != null
                ? scaledRank.PointsDecayPerSecond
                : sourceRank != null ? sourceRank.PointsDecayPerSecond : 0f;
            float progressiveBoostPercentBaseValue = sourceRank != null
                ? sourceRank.ProgressiveBoostPercent
                : scaledRank != null ? scaledRank.ProgressiveBoostPercent : 0f;
            float progressiveBoostPercentRuntimeValue = scaledRank != null
                ? scaledRank.ProgressiveBoostPercent
                : sourceRank != null ? sourceRank.ProgressiveBoostPercent : 0f;
            int formulaStartIndex = characterTuningFormulaBuffer.Length;
            int formulaCount = AppendRankBonusFormulas(formulaSourceRank, characterTuningFormulaBuffer);
            int passiveUnlockStartIndex = basePassiveUnlocks.Length;
            int passiveUnlockCount = AppendPassiveUnlocks(sourceRank,
                                                          scaledRank,
                                                          basePassiveUnlocks,
                                                          runtimePassiveUnlocks);

            baseRanks.Add(new PlayerBaseComboRankElement
            {
                RankId = new FixedString64Bytes(rankId),
                RequiredComboValue = requiredBaseValue,
                PointsDecayPerSecond = pointsDecayPerSecondBaseValue,
                ProgressiveBoostPercent = progressiveBoostPercentBaseValue,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount,
                PassiveUnlockStartIndex = passiveUnlockStartIndex,
                PassiveUnlockCount = passiveUnlockCount
            });
            runtimeRanks.Add(new PlayerRuntimeComboRankElement
            {
                RankId = new FixedString64Bytes(rankId),
                RequiredComboValue = requiredRuntimeValue,
                PointsDecayPerSecond = pointsDecayPerSecondRuntimeValue,
                ProgressiveBoostPercent = progressiveBoostPercentRuntimeValue,
                BonusFormulaStartIndex = formulaStartIndex,
                BonusFormulaCount = formulaCount,
                PassiveUnlockStartIndex = passiveUnlockStartIndex,
                PassiveUnlockCount = passiveUnlockCount
            });
        }
    }

    /// <summary>
    /// Populates the baked combo-rank visual buffer used by the HUD runtime.
    /// /params preset Progression preset that owns the authored combo-rank visuals.
    /// /params rankVisuals Destination visual buffer indexed like the runtime combo-rank buffer.
    /// /returns void.
    /// </summary>
    public static void PopulateComboCounterRankVisuals(PlayerProgressionPreset preset,
                                                       DynamicBuffer<PlayerComboRankVisualElement> rankVisuals)
    {
        rankVisuals.Clear();

        PlayerComboCounterDefinition comboCounter = preset != null ? preset.ComboCounter : null;
        IReadOnlyList<PlayerComboRankDefinition> rankDefinitions = comboCounter != null ? comboCounter.RankDefinitions : null;

        if (rankDefinitions == null)
        {
            return;
        }

        for (int rankIndex = 0; rankIndex < rankDefinitions.Count; rankIndex++)
        {
            PlayerComboRankDefinition rankDefinition = rankDefinitions[rankIndex];
            PlayerComboRankVisualDefinition rankVisual = rankDefinition != null ? rankDefinition.RankVisuals : null;
            Sprite badgeSprite = rankVisual != null ? rankVisual.BadgeSprite : null;
            Color badgeTint = rankVisual != null ? rankVisual.BadgeTint : Color.white;
            Color rankTextColor = rankVisual != null ? rankVisual.RankTextColor : Color.white;
            Color comboValueTextColor = rankVisual != null ? rankVisual.ComboValueTextColor : Color.white;
            Color progressFillColor = rankVisual != null ? rankVisual.ProgressFillColor : Color.white;
            Color progressBackgroundColor = rankVisual != null ? rankVisual.ProgressBackgroundColor : Color.white;

            rankVisuals.Add(new PlayerComboRankVisualElement
            {
                BadgeSprite = badgeSprite,
                BadgeTint = ToFloat4(badgeTint),
                RankTextColor = ToFloat4(rankTextColor),
                ComboValueTextColor = ToFloat4(comboValueTextColor),
                ProgressFillColor = ToFloat4(progressFillColor),
                ProgressBackgroundColor = ToFloat4(progressBackgroundColor)
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

            if (!TryMapComboFieldId(scalingRule.StatKey,
                                    out int rankIndex,
                                    out int passiveUnlockIndex,
                                    out PlayerRuntimeComboCounterFieldId fieldId))
            {
                continue;
            }

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
            {
                continue;
            }

            if (!TryResolveComboScalingBaseMetadata(property,
                                                    out byte valueType,
                                                    out float baseValue,
                                                    out byte baseBooleanValue,
                                                    out byte isInteger,
                                                    out FixedString64Bytes baseTokenValue))
            {
                continue;
            }

            scalingBuffer.Add(new PlayerRuntimeComboCounterScalingElement
            {
                FieldId = fieldId,
                RankIndex = rankIndex,
                PassiveUnlockIndex = passiveUnlockIndex,
                ValueType = valueType,
                BaseValue = baseValue,
                BaseBooleanValue = baseBooleanValue,
                IsInteger = isInteger,
                BaseTokenValue = baseTokenValue,
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
            ShieldDamageBreaksCombo = comboDefinition != null && comboDefinition.ShieldDamageBreaksCombo ? (byte)1 : (byte)0,
            PreventDecayIntoNonDecayingRanks = comboDefinition != null && comboDefinition.PreventDecayIntoNonDecayingRanks ? (byte)1 : (byte)0
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

    /// <summary>
    /// Appends base and runtime passive unlock entries authored under one combo rank.
    /// /params sourceRank Unscaled rank used for immutable baseline values.
    /// /params scaledRank Scaled rank used for initial runtime values.
    /// /params basePassiveUnlocks Destination immutable passive unlock buffer.
    /// /params runtimePassiveUnlocks Destination mutable passive unlock buffer.
    /// /returns Number of unlock entries appended for the rank.
    /// </summary>
    private static int AppendPassiveUnlocks(PlayerComboRankDefinition sourceRank,
                                            PlayerComboRankDefinition scaledRank,
                                            DynamicBuffer<PlayerBaseComboPassiveUnlockElement> basePassiveUnlocks,
                                            DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimePassiveUnlocks)
    {
        IReadOnlyList<PlayerComboPassivePowerUpUnlockDefinition> sourceUnlocks = sourceRank != null ? sourceRank.PassivePowerUpUnlocks : null;
        IReadOnlyList<PlayerComboPassivePowerUpUnlockDefinition> scaledUnlocks = scaledRank != null ? scaledRank.PassivePowerUpUnlocks : null;
        int sourceUnlockCount = sourceUnlocks != null ? sourceUnlocks.Count : 0;
        int scaledUnlockCount = scaledUnlocks != null ? scaledUnlocks.Count : 0;
        int unlockCount = math.max(sourceUnlockCount, scaledUnlockCount);

        for (int unlockIndex = 0; unlockIndex < unlockCount; unlockIndex++)
        {
            PlayerComboPassivePowerUpUnlockDefinition sourceUnlock = sourceUnlocks != null && unlockIndex < sourceUnlockCount ? sourceUnlocks[unlockIndex] : null;
            PlayerComboPassivePowerUpUnlockDefinition scaledUnlock = scaledUnlocks != null && unlockIndex < scaledUnlockCount ? scaledUnlocks[unlockIndex] : null;
            basePassiveUnlocks.Add(new PlayerBaseComboPassiveUnlockElement
            {
                PassivePowerUpId = new FixedString64Bytes(ResolvePassivePowerUpId(sourceUnlock, scaledUnlock)),
                IsEnabled = ResolvePassiveUnlockEnabled(sourceUnlock, scaledUnlock)
            });
            runtimePassiveUnlocks.Add(new PlayerRuntimeComboPassiveUnlockElement
            {
                PassivePowerUpId = new FixedString64Bytes(ResolvePassivePowerUpId(scaledUnlock, sourceUnlock)),
                IsEnabled = ResolvePassiveUnlockEnabled(scaledUnlock, sourceUnlock)
            });
        }

        return unlockCount;
    }

    /// <summary>
    /// Resolves one passive PowerUpId from a preferred unlock entry with fallback support.
    /// /params preferredUnlock Preferred unlock entry.
    /// /params fallbackUnlock Fallback unlock entry.
    /// /returns Trimmed PowerUpId or an empty string when no valid ID is authored.
    /// </summary>
    private static string ResolvePassivePowerUpId(PlayerComboPassivePowerUpUnlockDefinition preferredUnlock,
                                                  PlayerComboPassivePowerUpUnlockDefinition fallbackUnlock)
    {
        if (preferredUnlock != null && !string.IsNullOrWhiteSpace(preferredUnlock.PassivePowerUpId))
        {
            return preferredUnlock.PassivePowerUpId.Trim();
        }

        if (fallbackUnlock != null && !string.IsNullOrWhiteSpace(fallbackUnlock.PassivePowerUpId))
        {
            return fallbackUnlock.PassivePowerUpId.Trim();
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves one passive unlock enable flag from a preferred unlock entry with fallback support.
    /// /params preferredUnlock Preferred unlock entry.
    /// /params fallbackUnlock Fallback unlock entry.
    /// /returns One when the resolved unlock is enabled; otherwise zero.
    /// </summary>
    private static byte ResolvePassiveUnlockEnabled(PlayerComboPassivePowerUpUnlockDefinition preferredUnlock,
                                                    PlayerComboPassivePowerUpUnlockDefinition fallbackUnlock)
    {
        if (preferredUnlock != null)
        {
            return preferredUnlock.IsEnabled ? (byte)1 : (byte)0;
        }

        if (fallbackUnlock != null)
        {
            return fallbackUnlock.IsEnabled ? (byte)1 : (byte)0;
        }

        return 0;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Resolves combo scaling baseline metadata, including token-backed passive PowerUpId fields.
    /// /params property Serialized property targeted by Add Scaling.
    /// /params valueType Runtime formula value type.
    /// /params baseValue Numeric base value when applicable.
    /// /params baseBooleanValue Boolean base value when applicable.
    /// /params isInteger True when numeric values should be rounded before assignment.
    /// /params baseTokenValue Token base value when applicable.
    /// /returns True when the serialized property can be converted to combo scaling metadata.
    /// </summary>
    private static bool TryResolveComboScalingBaseMetadata(SerializedProperty property,
                                                           out byte valueType,
                                                           out float baseValue,
                                                           out byte baseBooleanValue,
                                                           out byte isInteger,
                                                           out FixedString64Bytes baseTokenValue)
    {
        baseTokenValue = default;

        if (property != null && property.propertyType == SerializedPropertyType.String)
        {
            valueType = (byte)PlayerFormulaValueType.Token;
            baseValue = 0f;
            baseBooleanValue = 0;
            isInteger = 0;
            string tokenValue = string.IsNullOrWhiteSpace(property.stringValue)
                ? string.Empty
                : property.stringValue.Trim();
            baseTokenValue = new FixedString64Bytes(tokenValue);
            return true;
        }

        return PlayerRuntimeScalingBakeUtility.TryResolveScalingBaseMetadata(property,
                                                                             out valueType,
                                                                             out baseValue,
                                                                             out baseBooleanValue,
                                                                             out isInteger);
    }

    /// <summary>
    /// Maps one progression Add Scaling stat key to the combo runtime field targeted by that rule.
    /// /params statKey Stable Add Scaling stat key emitted by the progression preset.
    /// /params rankIndex Resolved combo rank index when the mapping targets one rank milestone.
    /// /params passiveUnlockIndex Resolved passive unlock index when the mapping targets one nested passive unlock.
    /// /params fieldId Resolved combo runtime field identifier.
    /// /returns True when the stat key targets the combo module; otherwise false.
    /// </summary>
    private static bool TryMapComboFieldId(string statKey,
                                           out int rankIndex,
                                           out int passiveUnlockIndex,
                                           out PlayerRuntimeComboCounterFieldId fieldId)
    {
        rankIndex = -1;
        passiveUnlockIndex = -1;
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

        if (string.Equals(statKey, "comboCounter.preventDecayIntoNonDecayingRanks", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.PreventDecayIntoNonDecayingRanks;
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

        if (!TryParseStableArrayIndex(statKey, 0, out rankIndex))
        {
            rankIndex = -1;
            return false;
        }

        if (statKey.Contains(".passivePowerUpUnlocks.Array.data[", StringComparison.Ordinal))
        {
            if (!TryParseStableArrayIndex(statKey, 1, out passiveUnlockIndex))
            {
                rankIndex = -1;
                passiveUnlockIndex = -1;
                return false;
            }

            if (statKey.EndsWith(".isEnabled", StringComparison.Ordinal))
            {
                fieldId = PlayerRuntimeComboCounterFieldId.RankPassiveUnlockEnabled;
                return true;
            }

            if (statKey.EndsWith(".passivePowerUpId", StringComparison.Ordinal))
            {
                fieldId = PlayerRuntimeComboCounterFieldId.RankPassiveUnlockPowerUpId;
                return true;
            }

            rankIndex = -1;
            passiveUnlockIndex = -1;
            return false;
        }

        if (statKey.EndsWith(".requiredComboValue", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.RankRequiredComboValue;
            return true;
        }

        if (statKey.EndsWith(".pointsDecayPerSecond", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.RankPointsDecayPerSecond;
            return true;
        }

        if (statKey.EndsWith(".progressiveBoostPercent", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeComboCounterFieldId.RankProgressiveBoostPercent;
            return true;
        }

        rankIndex = -1;
        passiveUnlockIndex = -1;
        return false;
    }

    /// <summary>
    /// Extracts one authored array index from a stable Add Scaling key token such as data[2|rankId:S].
    /// /params statKey Stable Add Scaling stat key containing array tokens.
    /// /params occurrenceIndex Zero-based data[] occurrence to parse.
    /// /params arrayIndex Parsed authored array index.
    /// /returns True when the requested array token was parsed successfully; otherwise false.
    /// </summary>
    private static bool TryParseStableArrayIndex(string statKey, int occurrenceIndex, out int arrayIndex)
    {
        arrayIndex = -1;

        int dataStartIndex = -1;
        int searchStartIndex = 0;

        for (int currentOccurrenceIndex = 0; currentOccurrenceIndex <= occurrenceIndex; currentOccurrenceIndex++)
        {
            dataStartIndex = statKey.IndexOf("data[", searchStartIndex, StringComparison.Ordinal);

            if (dataStartIndex < 0)
            {
                return false;
            }

            searchStartIndex = dataStartIndex + 5;
        }

        int dataEndIndex = statKey.IndexOf(']', dataStartIndex);

        if (dataStartIndex < 0 || dataEndIndex <= dataStartIndex)
        {
            return false;
        }

        string token = statKey.Substring(dataStartIndex + 5, dataEndIndex - dataStartIndex - 5);
        int separatorIndex = token.IndexOf('|');
        string indexText = separatorIndex >= 0 ? token.Substring(0, separatorIndex) : token;
        return int.TryParse(indexText, out arrayIndex);
    }
#endif

    /// <summary>
    /// Converts one Unity color to the float4 layout stored inside ECS buffers.
    /// /params color Source color.
    /// /returns Float4 representation of the provided color.
    /// </summary>
    private static float4 ToFloat4(Color color)
    {
        return new float4(color.r, color.g, color.b, color.a);
    }
    #endregion

    #endregion
}
