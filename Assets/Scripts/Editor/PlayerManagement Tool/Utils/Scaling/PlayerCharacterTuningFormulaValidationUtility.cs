using System.Collections.Generic;

/// <summary>
/// Provides editor-side validation helpers for Character Tuning assignment formulas.
/// </summary>
public static class PlayerCharacterTuningFormulaValidationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one Character Tuning assignment formula using the scalable-stat parser and formula engine.
    ///  assignmentFormula Assignment string entered by designers.
    ///  allowedVariables Optional scalable-stat whitelist for the current preset scope.
    ///  warningMessage Failure reason when validation fails.
    /// returns True when the assignment is valid.
    /// </summary>
    public static bool TryValidateAssignmentFormula(string assignmentFormula,
                                                    ISet<string> allowedVariables,
                                                    out string warningMessage)
    {
        warningMessage = string.Empty;

        if (!PlayerCharacterTuningFormulaUtility.TryParseAssignmentFormula(assignmentFormula,
                                                                           out string targetStatName,
                                                                           out string expression,
                                                                           out warningMessage))
        {
            return false;
        }

        if (allowedVariables != null && !allowedVariables.Contains(targetStatName))
        {
            warningMessage = string.Format("Unknown assignment target scalable stat [{0}].", targetStatName);
            return false;
        }

        return PlayerScalingFormulaValidationUtility.TryValidateFormula(expression,
                                                                        allowedVariables,
                                                                        out warningMessage);
    }
    #endregion

    #endregion
}
