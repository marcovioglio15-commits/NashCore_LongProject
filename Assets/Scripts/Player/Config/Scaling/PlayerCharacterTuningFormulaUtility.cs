using System;

/// <summary>
/// Parses assignment-style scalable-stat formulas used by Character Tuning power-up modules.
/// </summary>
public static class PlayerCharacterTuningFormulaUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Splits one Character Tuning assignment into its target scalable stat and right-hand expression.
    /// /params formula Raw assignment string entered by designers.
    /// /params targetStatName Parsed left-hand scalable stat name.
    /// /params expression Parsed right-hand mathematical expression.
    /// /params errorMessage Failure reason when parsing fails.
    /// /returns True when the assignment syntax is valid.
    /// </summary>
    public static bool TryParseAssignmentFormula(string formula,
                                                 out string targetStatName,
                                                 out string expression,
                                                 out string errorMessage)
    {
        targetStatName = string.Empty;
        expression = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(formula))
        {
            errorMessage = "Assignment formula is empty.";
            return false;
        }

        int assignmentIndex = formula.IndexOf('=');

        if (assignmentIndex < 0)
        {
            errorMessage = "Assignment formula is missing '='.";
            return false;
        }

        if (assignmentIndex != formula.LastIndexOf('='))
        {
            errorMessage = "Assignment formula can contain only one '='.";
            return false;
        }

        string leftHandSide = formula.Substring(0, assignmentIndex).Trim();
        string rightHandSide = formula.Substring(assignmentIndex + 1).Trim();

        if (!TryParseAssignmentTarget(leftHandSide, out targetStatName, out errorMessage))
            return false;

        if (string.IsNullOrWhiteSpace(rightHandSide))
        {
            errorMessage = "Assignment formula is missing the right-hand expression.";
            return false;
        }

        expression = rightHandSide;
        return true;
    }

    /// <summary>
    /// Parses one left-hand assignment token and validates that it targets a named scalable stat.
    /// /params targetToken Raw left-hand text before '='.
    /// /params targetStatName Parsed scalable stat name.
    /// /params errorMessage Failure reason when parsing fails.
    /// /returns True when the target token is valid.
    /// </summary>
    public static bool TryParseAssignmentTarget(string targetToken,
                                                out string targetStatName,
                                                out string errorMessage)
    {
        targetStatName = string.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(targetToken))
        {
            errorMessage = "Assignment target is empty.";
            return false;
        }

        string trimmedTargetToken = targetToken.Trim();

        if (!trimmedTargetToken.StartsWith("[", StringComparison.Ordinal) ||
            !trimmedTargetToken.EndsWith("]", StringComparison.Ordinal))
        {
            errorMessage = "Assignment target must use [ScalableStatName] syntax.";
            return false;
        }

        int closingBracketIndex = trimmedTargetToken.IndexOf(']');

        if (closingBracketIndex != trimmedTargetToken.Length - 1)
        {
            errorMessage = "Assignment target must contain exactly one bracketed scalable stat token.";
            return false;
        }

        targetStatName = trimmedTargetToken.Substring(1, trimmedTargetToken.Length - 2).Trim();

        if (string.IsNullOrWhiteSpace(targetStatName))
        {
            errorMessage = "Assignment target cannot be empty.";
            return false;
        }

        if (string.Equals(targetStatName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Assignment target cannot use [this].";
            return false;
        }

        if (!PlayerScalableStatNameUtility.IsValid(targetStatName))
        {
            errorMessage = string.Format("Assignment target [{0}] is not a valid scalable stat name.", targetStatName);
            return false;
        }

        return true;
    }
    #endregion

    #endregion
}
