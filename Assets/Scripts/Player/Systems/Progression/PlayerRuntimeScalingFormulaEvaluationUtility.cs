using System.Collections.Generic;
using Unity.Mathematics;

/// <summary>
/// Evaluates runtime scaling formulas against the current typed scalable-stat context.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerRuntimeScalingFormulaEvaluationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Evaluates one numeric scaling formula and optionally rounds the result for integer-backed fields.
    /// /params formula Runtime formula text baked for the target field.
    /// /params baseValue Immutable baseline value passed as the reserved [this] token.
    /// /params isInteger True when the resolved value must be rounded to the nearest integer.
    /// /params variableContext Current typed scalable-stat context.
    /// /params resolvedValue Evaluated numeric result when the formula succeeds.
    /// /returns True when the formula evaluated successfully.
    /// </summary>
    public static bool TryEvaluateNumericValue(string formula,
                                               float baseValue,
                                               bool isInteger,
                                               IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                               out float resolvedValue)
    {
        resolvedValue = baseValue;

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(formula,
                                                                   baseValue,
                                                                   variableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
        {
            return false;
        }

        resolvedValue = isInteger ? math.round(evaluatedValue) : evaluatedValue;
        return true;
    }

    /// <summary>
    /// Evaluates one boolean scaling formula against the current typed scalable-stat context.
    /// /params formula Runtime formula text baked for the target field.
    /// /params baseValue Immutable baseline boolean passed as the reserved [this] token.
    /// /params variableContext Current typed scalable-stat context.
    /// /params resolvedValue Evaluated boolean result when the formula succeeds.
    /// /returns True when the formula evaluated successfully and resolved to a boolean.
    /// </summary>
    public static bool TryEvaluateBooleanValue(string formula,
                                               bool baseValue,
                                               IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                               out bool resolvedValue)
    {
        resolvedValue = baseValue;

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(formula,
                                                                   PlayerFormulaValue.CreateBoolean(baseValue),
                                                                   variableContext,
                                                                   out PlayerFormulaValue evaluatedValue,
                                                                   out string _))
        {
            return false;
        }

        if (evaluatedValue.Type != PlayerFormulaValueType.Boolean)
        {
            return false;
        }

        resolvedValue = evaluatedValue.BooleanValue;
        return true;
    }

    /// <summary>
    /// Evaluates one token scaling formula against the current typed scalable-stat context.
    /// /params formula Runtime formula text baked for the target field.
    /// /params baseValue Immutable baseline token passed as the reserved [this] token.
    /// /params variableContext Current typed scalable-stat context.
    /// /params resolvedValue Evaluated token result when the formula succeeds.
    /// /returns True when the formula evaluated successfully and resolved to a token.
    /// </summary>
    public static bool TryEvaluateTokenValue(string formula,
                                             string baseValue,
                                             IReadOnlyDictionary<string, PlayerFormulaValue> variableContext,
                                             out string resolvedValue)
    {
        resolvedValue = string.IsNullOrWhiteSpace(baseValue) ? string.Empty : baseValue.Trim();

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(formula,
                                                                   PlayerFormulaValue.CreateToken(resolvedValue),
                                                                   variableContext,
                                                                   out PlayerFormulaValue evaluatedValue,
                                                                   out string _))
        {
            return false;
        }

        if (evaluatedValue.Type != PlayerFormulaValueType.Token)
        {
            return false;
        }

        resolvedValue = string.IsNullOrWhiteSpace(evaluatedValue.TokenValue)
            ? string.Empty
            : evaluatedValue.TokenValue.Trim();
        return true;
    }
    #endregion

    #endregion
}
