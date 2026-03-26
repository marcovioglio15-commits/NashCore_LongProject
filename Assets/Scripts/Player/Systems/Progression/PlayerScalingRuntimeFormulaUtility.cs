using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Shares runtime helpers for scalable-stat formula evaluation and translated debug output.
/// </summary>
internal static class PlayerScalingRuntimeFormulaUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds a case-insensitive variable dictionary from the runtime scalable-stat buffer.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable-stat buffer.</param>
    /// <param name="variableContext">Destination dictionary receiving the current stat values.</param>
    public static void FillVariableContext(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                           Dictionary<string, float> variableContext)
    {
        if (variableContext == null)
            return;

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
    /// Evaluates one runtime scaling formula against the current scalable-stat context.
    /// </summary>
    /// <param name="formula">Formula string to evaluate.</param>
    /// <param name="thisValue">Base value mapped to the reserved [this] token.</param>
    /// <param name="variableContext">Current scalable-stat dictionary.</param>
    /// <param name="evaluatedValue">Resolved value when evaluation succeeds.</param>
    /// <param name="errorMessage">Failure reason when evaluation fails.</param>
    /// <returns>True when evaluation succeeded; otherwise false.<returns>
    public static bool TryEvaluateFormula(string formula,
                                          float thisValue,
                                          IReadOnlyDictionary<string, float> variableContext,
                                          out float evaluatedValue,
                                          out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            evaluatedValue = thisValue;
            errorMessage = string.Empty;
            return false;
        }

        return PlayerStatFormulaEngine.TryEvaluate(formula,
                                                   thisValue,
                                                   variableContext,
                                                   out evaluatedValue,
                                                   out errorMessage);
    }

    /// <summary>
    /// Replaces [this] and named variables with their resolved numeric values for readable debug output.
    /// </summary>
    /// <param name="formula">Raw formula string.</param>
    /// <param name="variableContext">Current scalable-stat dictionary.</param>
    /// <param name="thisValue">Base value mapped to [this].</param>
    /// <returns>Readable translated expression suitable for logs.<returns>
    public static string TranslateFormula(string formula,
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
    /// Formats numeric values for concise debug output.
    /// </summary>
    /// <param name="value">Value to format.</param>
    /// <returns>Compact invariant string representation.<returns>
    public static string FormatNumber(float value)
    {
        float roundedInteger = Mathf.Round(value);

        if (Mathf.Abs(value - roundedInteger) <= 0.0001f)
            return ((int)roundedInteger).ToString(CultureInfo.InvariantCulture);

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves one named scalable-stat variable from the current runtime dictionary.
    /// </summary>
    /// <param name="variableContext">Current scalable-stat dictionary.</param>
    /// <param name="variableName">Variable to resolve.</param>
    /// <param name="resolvedValue">Resolved variable value when found.</param>
    /// <returns>True when the variable exists; otherwise false.<returns>
    private static bool TryResolveVariableValue(IReadOnlyDictionary<string, float> variableContext,
                                                string variableName,
                                                out float resolvedValue)
    {
        resolvedValue = 0f;

        if (variableContext == null || string.IsNullOrWhiteSpace(variableName))
            return false;

        return variableContext.TryGetValue(variableName, out resolvedValue);
    }
    #endregion

    #endregion
}
