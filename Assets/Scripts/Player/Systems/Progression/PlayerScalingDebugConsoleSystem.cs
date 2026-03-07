#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Logs one ordered debug line per scaling rule, including formula, resolved math expression, and final value.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerProgressionInitializeSystem))]
public partial struct PlayerScalingDebugConsoleSystem : ISystem
{
    #region Fields
    private EntityQuery ruleDebugQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the query used to detect entities with scaling debug snapshots and scalable stat values.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnCreate(ref SystemState state)
    {
        ruleDebugQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerScalingDebugRuleElement, PlayerScalableStatElement>()
            .Build();
        state.RequireForUpdate(ruleDebugQuery);
    }

    /// <summary>
    /// Emits one ordered colorized log line per debug-enabled scaling rule on each editor-play-mode frame.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnUpdate(ref SystemState state)
    {
        if (Application.isPlaying == false)
            return;

        if (ruleDebugQuery.IsEmptyIgnoreFilter)
            return;

        Dictionary<string, float> variableContext = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        foreach ((DynamicBuffer<PlayerScalingDebugRuleElement> debugRules,
                  DynamicBuffer<PlayerScalableStatElement> scalableStats) in SystemAPI.Query<DynamicBuffer<PlayerScalingDebugRuleElement>,
                                                                                           DynamicBuffer<PlayerScalableStatElement>>())
        {
            if (debugRules.Length == 0)
                continue;

            FillVariableContext(scalableStats, variableContext);

            for (int ruleIndex = 0; ruleIndex < debugRules.Length; ruleIndex++)
            {
                PlayerScalingDebugRuleElement debugRule = debugRules[ruleIndex];
                string targetDisplayName = ResolveTargetDisplayName(debugRule);
                string formulaText = debugRule.Formula.ToString();
                string translatedFormula = TranslateFormula(formulaText, variableContext, debugRule.ThisValue);
                float evaluatedValue = debugRule.FinalValue;
                Color debugColor = ResolveDebugColor(debugRule);
                string debugColorHex = ColorUtility.ToHtmlStringRGBA(debugColor);

                if (PlayerStatFormulaEngine.TryEvaluate(formulaText,
                                                        debugRule.ThisValue,
                                                        variableContext,
                                                        out float runtimeEvaluatedValue,
                                                        out string _))
                {
                    evaluatedValue = runtimeEvaluatedValue;
                }

                string logMessage = string.Format(CultureInfo.InvariantCulture,
                                                  "<color=#{4}>{0} = {1} = {2} = {3}</color>",
                                                  targetDisplayName,
                                                  formulaText,
                                                  translatedFormula,
                                                  FormatNumber(evaluatedValue),
                                                  debugColorHex);
                Debug.Log(logMessage);
            }
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Fills a case-insensitive variable dictionary from runtime scalable stat buffer values.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable stat buffer.</param>
    /// <param name="variableContext">Mutable dictionary receiving variable values.</param>
    /// <returns>Void.</returns>
    private static void FillVariableContext(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                            Dictionary<string, float> variableContext)
    {
        variableContext.Clear();

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (scalableStat.Name.Length == 0)
                continue;

            string variableName = scalableStat.Name.ToString();

            if (string.IsNullOrWhiteSpace(variableName))
                continue;

            variableContext[variableName] = scalableStat.Value;
        }
    }

    /// <summary>
    /// Resolves target display name with fallback to stat key.
    /// </summary>
    /// <param name="debugRule">Debug-rule snapshot entry.</param>
    /// <returns>Resolved display name used in the output message.</returns>
    private static string ResolveTargetDisplayName(in PlayerScalingDebugRuleElement debugRule)
    {
        string targetDisplayName = debugRule.TargetDisplayName.ToString();

        if (string.IsNullOrWhiteSpace(targetDisplayName) == false)
            return targetDisplayName;

        string statKey = debugRule.StatKey.ToString();

        if (string.IsNullOrWhiteSpace(statKey) == false)
            return statKey;

        return "Scaled Stat";
    }

    /// <summary>
    /// Builds a readable mathematical translation by replacing [variables] with resolved numeric values.
    /// </summary>
    /// <param name="formula">Raw scaling formula text.</param>
    /// <param name="variableContext">Runtime variable dictionary for scalable stats.</param>
    /// <param name="thisValue">Input value mapped to [this].</param>
    /// <returns>Resolved formula expression ready for console output.</returns>
    private static string TranslateFormula(string formula,
                                           IReadOnlyDictionary<string, float> variableContext,
                                           float thisValue)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return string.Empty;

        StringBuilder translatedFormulaBuilder = new StringBuilder(formula.Length + 32);
        int parseIndex = 0;

        while (parseIndex < formula.Length)
        {
            int openBracketIndex = formula.IndexOf('[', parseIndex);

            if (openBracketIndex < 0)
            {
                translatedFormulaBuilder.Append(formula.Substring(parseIndex));
                break;
            }

            if (openBracketIndex > parseIndex)
                translatedFormulaBuilder.Append(formula.Substring(parseIndex, openBracketIndex - parseIndex));

            int closeBracketIndex = formula.IndexOf(']', openBracketIndex + 1);

            if (closeBracketIndex < 0)
            {
                translatedFormulaBuilder.Append(formula.Substring(openBracketIndex));
                break;
            }

            string variableToken = formula.Substring(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1).Trim();

            if (string.Equals(variableToken, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
            {
                translatedFormulaBuilder.Append(FormatNumber(thisValue));
                parseIndex = closeBracketIndex + 1;
                continue;
            }

            if (TryResolveVariableValue(variableContext, variableToken, out float resolvedValue))
            {
                translatedFormulaBuilder.Append(FormatNumber(resolvedValue));
                parseIndex = closeBracketIndex + 1;
                continue;
            }

            translatedFormulaBuilder.Append('[');
            translatedFormulaBuilder.Append(variableToken);
            translatedFormulaBuilder.Append(']');
            parseIndex = closeBracketIndex + 1;
        }

        return translatedFormulaBuilder.ToString();
    }

    /// <summary>
    /// Resolves one variable value from runtime scalable stat dictionary using case-insensitive keys.
    /// </summary>
    /// <param name="variableContext">Runtime variable dictionary.</param>
    /// <param name="variableName">Variable name to resolve.</param>
    /// <param name="resolvedValue">Resolved value when found.</param>
    /// <returns>True when the variable exists in the buffer, otherwise false.</returns>
    private static bool TryResolveVariableValue(IReadOnlyDictionary<string, float> variableContext,
                                                string variableName,
                                                out float resolvedValue)
    {
        resolvedValue = 0f;

        if (variableContext == null || string.IsNullOrWhiteSpace(variableName))
            return false;

        return variableContext.TryGetValue(variableName, out resolvedValue);
    }

    /// <summary>
    /// Resolves the per-rule debug color with safe clamping and fallback to default yellow for invalid/legacy data.
    /// </summary>
    /// <param name="debugRule">Current debug-rule snapshot.</param>
    /// <returns>Resolved color used for this log line.</returns>
    private static Color ResolveDebugColor(in PlayerScalingDebugRuleElement debugRule)
    {
        float red = Mathf.Clamp01(debugRule.DebugColor.x);
        float green = Mathf.Clamp01(debugRule.DebugColor.y);
        float blue = Mathf.Clamp01(debugRule.DebugColor.z);
        float alpha = Mathf.Clamp01(debugRule.DebugColor.w);

        if (alpha <= 0.0001f)
            return PlayerStatScalingRule.GetDefaultDebugColor();

        return new Color(red, green, blue, alpha);
    }

    /// <summary>
    /// Formats numeric values for compact formula-output readability.
    /// </summary>
    /// <param name="value">Numeric value to format.</param>
    /// <returns>Formatted numeric text using invariant culture.</returns>
    private static string FormatNumber(float value)
    {
        float roundedInteger = Mathf.Round(value);

        if (Mathf.Abs(value - roundedInteger) <= 0.0001f)
            return ((int)roundedInteger).ToString(CultureInfo.InvariantCulture);

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
    #endregion

    #endregion
}
#endif
