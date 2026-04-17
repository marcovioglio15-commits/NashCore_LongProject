using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one combo-rank entry with scalable threshold editing and Character Tuning validation feedback.
/// none.
/// returns none.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerComboRankDefinition))]
public sealed class PlayerComboRankDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one combo-rank entry.
    /// property Serialized combo-rank property.
    /// returns Root UI element used by the inspector.
    /// </summary>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty rankIdProperty = property.FindPropertyRelative("rankId");
        SerializedProperty requiredComboValueProperty = property.FindPropertyRelative("requiredComboValue");
        SerializedProperty rankVisualsProperty = property.FindPropertyRelative("rankVisuals");
        SerializedProperty rankBonusesProperty = property.FindPropertyRelative("rankBonuses");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (rankIdProperty == null || requiredComboValueProperty == null || rankVisualsProperty == null || rankBonusesProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Combo rank fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        HelpBox infoBox = new HelpBox("Each reached rank applies its Character Tuning formulas cumulatively together with all lower ranks that are still reached. Rank visuals authored below are picked automatically by HUDManager using the active rank index, without any extra ID mapping.", HelpBoxMessageType.Info);
        root.Add(infoBox);
        root.Add(CreateBoundField(rankIdProperty, "Rank ID"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(requiredComboValueProperty,
                                                              scalingRulesProperty,
                                                              "Required Combo Value"));
        root.Add(CreateBoundField(rankVisualsProperty, "Rank Visuals"));

        PropertyField rankBonusesField = new PropertyField(rankBonusesProperty, "Rank Bonuses");
        rankBonusesField.BindProperty(rankBonusesProperty);
        root.Add(rankBonusesField);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(warningBox);

        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshWarnings(property.serializedObject,
                            rankIdProperty,
                            requiredComboValueProperty,
                            rankBonusesProperty,
                            warningBox);
        });

        RefreshWarnings(property.serializedObject,
                        rankIdProperty,
                        requiredComboValueProperty,
                        rankBonusesProperty,
                        warningBox);
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one bound property field with the requested display label.
    /// property Serialized property bound to the field.
    /// label Inspector label shown for the bound field.
    /// returns Configured property field bound to the serialized property.
    /// </summary>
    private static PropertyField CreateBoundField(SerializedProperty property, string label)
    {
        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        return propertyField;
    }

    /// <summary>
    /// Rebuilds the warning message shown for one combo rank.
    /// serializedObject Serialized object owning the combo rank.
    /// rankIdProperty Serialized rank identifier property.
    /// requiredComboValueProperty Serialized combo threshold property.
    /// rankBonusesProperty Serialized Character Tuning payload property.
    /// warningBox Warning help box refreshed in place.
    /// returns void.
    /// </summary>
    private static void RefreshWarnings(SerializedObject serializedObject,
                                        SerializedProperty rankIdProperty,
                                        SerializedProperty requiredComboValueProperty,
                                        SerializedProperty rankBonusesProperty,
                                        HelpBox warningBox)
    {
        if (warningBox == null)
        {
            return;
        }

        List<string> warningLines = new List<string>();
        string rankId = rankIdProperty != null ? rankIdProperty.stringValue : string.Empty;

        if (string.IsNullOrWhiteSpace(rankId))
        {
            warningLines.Add("Rank ID should not be empty.");
        }

        if (requiredComboValueProperty != null && requiredComboValueProperty.intValue < 0)
        {
            warningLines.Add("Required Combo Value should be >= 0.");
        }

        SerializedProperty formulasProperty = rankBonusesProperty != null
            ? rankBonusesProperty.FindPropertyRelative("formulas")
            : null;

        if (serializedObject == null || formulasProperty == null || !formulasProperty.isArray)
        {
            warningLines.Add("Rank Bonuses formulas are not available.");
        }
        else if (formulasProperty.arraySize <= 0)
        {
            warningLines.Add("No Character Tuning formulas configured. This rank currently changes only presentation.");
        }
        else
        {
            HashSet<string> allowedVariables = PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject);
            Dictionary<string, PlayerFormulaValueType> variableTypes = PlayerScalingFormulaValidationUtility.BuildScopedVariableTypeMap(serializedObject);

            for (int formulaIndex = 0; formulaIndex < formulasProperty.arraySize; formulaIndex++)
            {
                SerializedProperty formulaEntryProperty = formulasProperty.GetArrayElementAtIndex(formulaIndex);
                SerializedProperty formulaProperty = formulaEntryProperty != null
                    ? formulaEntryProperty.FindPropertyRelative("formula")
                    : null;

                if (formulaProperty == null)
                {
                    warningLines.Add(string.Format("Formula #{0} payload is invalid.", formulaIndex + 1));
                    continue;
                }

                string formulaValue = formulaProperty.stringValue;

                if (string.IsNullOrWhiteSpace(formulaValue))
                {
                    warningLines.Add(string.Format("Formula #{0} is empty.", formulaIndex + 1));
                    continue;
                }

                if (PlayerCharacterTuningFormulaValidationUtility.TryValidateAssignmentFormula(formulaValue,
                                                                                              allowedVariables,
                                                                                              variableTypes,
                                                                                              out string warningMessage))
                {
                    continue;
                }

                warningLines.Add(string.Format("Formula #{0}: {1}", formulaIndex + 1, warningMessage));
            }
        }

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }
    #endregion

    #endregion
}
