using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Custom drawer for one level-up milestone definition with tier-roll selection support.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerLevelUpMilestoneDefinition))]
public sealed class PlayerLevelUpMilestoneDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one UI Toolkit inspector for one serialized milestone definition.
    /// </summary>
    /// <param name="property">Serialized milestone property.</param>
    /// <returns>Generated root visual element for the property drawer.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty milestoneLevelProperty = property.FindPropertyRelative("milestoneLevel");
        SerializedProperty specialExpRequirementProperty = property.FindPropertyRelative("specialExpRequirement");
        SerializedProperty powerUpUnlockRollCountProperty = property.FindPropertyRelative("powerUpUnlockRollCount");
        SerializedProperty milestoneTierRollsProperty = property.FindPropertyRelative("milestoneTierRolls");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (milestoneLevelProperty == null ||
            specialExpRequirementProperty == null ||
            powerUpUnlockRollCountProperty == null ||
            milestoneTierRollsProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Milestone fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        VisualElement milestoneLevelField = PlayerScalingFieldElementFactory.CreateField(milestoneLevelProperty,
                                                                                         scalingRulesProperty,
                                                                                         "Milestone Level");
        root.Add(milestoneLevelField);

        VisualElement specialRequirementField = PlayerScalingFieldElementFactory.CreateField(specialExpRequirementProperty,
                                                                                              scalingRulesProperty,
                                                                                              "Special Exp Requirement");
        root.Add(specialRequirementField);

        VisualElement unlockRollCountField = PlayerScalingFieldElementFactory.CreateField(powerUpUnlockRollCountProperty,
                                                                                           scalingRulesProperty,
                                                                                           "Power-Up Unlock Rolls");
        root.Add(unlockRollCountField);

        List<string> tierIdOptions = PlayerProgressionTierOptionsUtility.BuildTierIdOptionsFromPowerUpsLibrary();

        if (tierIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No tier IDs found in Power-Ups presets. Milestone tier rolls cannot be configured yet.", HelpBoxMessageType.Warning);
            root.Add(warningBox);
        }

        PropertyField tierRollsField = new PropertyField(milestoneTierRollsProperty, "Tier Rolls");
        tierRollsField.BindProperty(milestoneTierRollsProperty);
        root.Add(tierRollsField);
        return root;
    }
    #endregion

    #endregion
}
