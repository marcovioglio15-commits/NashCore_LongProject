using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws the combo-counter preset module with scalable global settings and list-level validation warnings.
/// none.
/// returns none.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerComboCounterDefinition))]
public sealed class PlayerComboCounterDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for the combo-counter definition.
    /// property Serialized combo-counter property.
    /// returns Root UI element used by the inspector.
    /// </summary>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty isEnabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty comboGainPerKillProperty = property.FindPropertyRelative("comboGainPerKill");
        SerializedProperty shieldDamageBreaksComboProperty = property.FindPropertyRelative("shieldDamageBreaksCombo");
        SerializedProperty rankDefinitionsProperty = property.FindPropertyRelative("rankDefinitions");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (isEnabledProperty == null ||
            comboGainPerKillProperty == null ||
            shieldDamageBreaksComboProperty == null ||
            rankDefinitionsProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Combo counter fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        HelpBox infoBox = new HelpBox("Health damage always breaks the combo. Shield damage can optionally break it too, and all combo settings below support Add Scaling where meaningful.", HelpBoxMessageType.Info);
        root.Add(infoBox);
        root.Add(PlayerScalingFieldElementFactory.CreateField(isEnabledProperty,
                                                              scalingRulesProperty,
                                                              "Enabled"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(comboGainPerKillProperty,
                                                              scalingRulesProperty,
                                                              "Combo Gain Per Kill"));
        root.Add(PlayerScalingFieldElementFactory.CreateField(shieldDamageBreaksComboProperty,
                                                              scalingRulesProperty,
                                                              "Shield Damage Breaks Combo"));

        PropertyField rankDefinitionsField = new PropertyField(rankDefinitionsProperty, "Rank Definitions");
        rankDefinitionsField.BindProperty(rankDefinitionsProperty);
        root.Add(rankDefinitionsField);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(warningBox);

        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshWarnings(isEnabledProperty,
                            comboGainPerKillProperty,
                            rankDefinitionsProperty,
                            warningBox);
        });

        RefreshWarnings(isEnabledProperty,
                        comboGainPerKillProperty,
                        rankDefinitionsProperty,
                        warningBox);
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Rebuilds the warning message shown for the combo-counter module.
    /// isEnabledProperty Serialized combo enabled property.
    /// comboGainPerKillProperty Serialized kill gain property.
    /// rankDefinitionsProperty Serialized combo-rank list property.
    /// warningBox Warning help box refreshed in place.
    /// returns void.
    /// </summary>
    private static void RefreshWarnings(SerializedProperty isEnabledProperty,
                                        SerializedProperty comboGainPerKillProperty,
                                        SerializedProperty rankDefinitionsProperty,
                                        HelpBox warningBox)
    {
        if (warningBox == null)
        {
            return;
        }

        List<string> warningLines = new List<string>();

        if (isEnabledProperty != null &&
            isEnabledProperty.propertyType == SerializedPropertyType.Boolean &&
            isEnabledProperty.boolValue &&
            comboGainPerKillProperty != null &&
            comboGainPerKillProperty.intValue <= 0)
        {
            warningLines.Add("Combo Gain Per Kill should be > 0 while the combo counter is enabled.");
        }

        if (rankDefinitionsProperty == null || !rankDefinitionsProperty.isArray)
        {
            warningLines.Add("Rank Definitions are not available.");
        }
        else if (rankDefinitionsProperty.arraySize <= 0)
        {
            warningLines.Add("No ranks configured. The combo counter can still count kills, but it cannot grant rank bonuses.");
        }
        else
        {
            HashSet<string> visitedRankIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int previousRequiredValue = int.MinValue;

            for (int rankIndex = 0; rankIndex < rankDefinitionsProperty.arraySize; rankIndex++)
            {
                SerializedProperty rankProperty = rankDefinitionsProperty.GetArrayElementAtIndex(rankIndex);
                SerializedProperty rankIdProperty = rankProperty != null ? rankProperty.FindPropertyRelative("rankId") : null;
                SerializedProperty requiredComboValueProperty = rankProperty != null ? rankProperty.FindPropertyRelative("requiredComboValue") : null;
                string rankId = rankIdProperty != null && !string.IsNullOrWhiteSpace(rankIdProperty.stringValue)
                    ? rankIdProperty.stringValue.Trim()
                    : string.Empty;
                int requiredComboValue = requiredComboValueProperty != null ? requiredComboValueProperty.intValue : 0;

                if (string.IsNullOrWhiteSpace(rankId))
                {
                    warningLines.Add(string.Format("Rank #{0} should define a non-empty Rank ID.", rankIndex + 1));
                }
                else if (!visitedRankIds.Add(rankId))
                {
                    warningLines.Add(string.Format("Rank ID '{0}' is duplicated. Stable Add Scaling keys and HUD presentation can become ambiguous.", rankId));
                }

                if (requiredComboValue < 0)
                {
                    warningLines.Add(string.Format("Rank '{0}' should use a Required Combo Value >= 0.", string.IsNullOrWhiteSpace(rankId) ? "#" + (rankIndex + 1) : rankId));
                }

                if (rankIndex > 0 && requiredComboValue < previousRequiredValue)
                {
                    warningLines.Add(string.Format("Rank '{0}' should not require less combo than the previous rank.", string.IsNullOrWhiteSpace(rankId) ? "#" + (rankIndex + 1) : rankId));
                }

                previousRequiredValue = requiredComboValue;
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
