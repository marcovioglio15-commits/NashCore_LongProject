using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternWeaponInteractionAssembly.
/// /params None.
/// /returns None.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternWeaponInteractionAssembly))]
public sealed class EnemyPatternWeaponInteractionAssemblyPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the weapon-interaction assembly UI.
    /// /params property Serialized weapon-interaction assembly property.
    /// /returns The built root visual element.
    /// </summary>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty useMinimumRangeProperty = property.FindPropertyRelative("useMinimumRange");
        SerializedProperty minimumRangeProperty = property.FindPropertyRelative("minimumRange");
        SerializedProperty useMaximumRangeProperty = property.FindPropertyRelative("useMaximumRange");
        SerializedProperty maximumRangeProperty = property.FindPropertyRelative("maximumRange");
        SerializedProperty exclusiveLookDirectionControlProperty = property.FindPropertyRelative("exclusiveLookDirectionControl");
        SerializedProperty displayBehaviourEngagementTriggerProperty = property.FindPropertyRelative("displayBehaviourEngagementTrigger");
        SerializedProperty useEngagementFeedbackOverrideProperty = property.FindPropertyRelative("useEngagementFeedbackOverride");
        SerializedProperty engagementFeedbackOverrideProperty = property.FindPropertyRelative("engagementFeedbackOverride");
        SerializedProperty bindingProperty = property.FindPropertyRelative("binding");
        SerializedProperty moduleIdProperty = bindingProperty != null ? bindingProperty.FindPropertyRelative("moduleId") : null;

        if (enabledProperty == null ||
            useMinimumRangeProperty == null ||
            minimumRangeProperty == null ||
            useMaximumRangeProperty == null ||
            maximumRangeProperty == null ||
            exclusiveLookDirectionControlProperty == null ||
            displayBehaviourEngagementTriggerProperty == null ||
            useEngagementFeedbackOverrideProperty == null ||
            engagementFeedbackOverrideProperty == null ||
            bindingProperty == null)
        {
            Label errorLabel = new Label("Weapon Interaction assembly fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(root, enabledProperty, "Enable Weapon Interaction");

        VisualElement settingsContainer = new VisualElement();
        settingsContainer.style.marginLeft = 12f;
        root.Add(settingsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer, useMinimumRangeProperty, "Use Minimum Range Gate");

        VisualElement minimumRangeContainer = new VisualElement();
        minimumRangeContainer.style.marginLeft = 12f;
        settingsContainer.Add(minimumRangeContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(minimumRangeContainer, minimumRangeProperty, "Minimum Range");

        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer, useMaximumRangeProperty, "Use Maximum Range Gate");

        VisualElement maximumRangeContainer = new VisualElement();
        maximumRangeContainer.style.marginLeft = 12f;
        settingsContainer.Add(maximumRangeContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(maximumRangeContainer, maximumRangeProperty, "Maximum Range");

        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer,
                                                   exclusiveLookDirectionControlProperty,
                                                   "Exclusive Look Direction Control");

        PropertyField bindingField = new PropertyField(bindingProperty, "Weapon Module");
        bindingField.BindProperty(bindingProperty);
        bindingField.tooltip = "Binding selected after the shared minimum and maximum range gates defined in this assembly evaluate true.";
        settingsContainer.Add(bindingField);
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer,
                                                   displayBehaviourEngagementTriggerProperty,
                                                   "Display Behaviour Engagement Trigger");

        HelpBox unsupportedModuleBox = new HelpBox("Display Behaviour Engagement Trigger currently supports only Shooter in the Weapon Interaction slot.", HelpBoxMessageType.Warning);
        unsupportedModuleBox.style.marginTop = 4f;
        settingsContainer.Add(unsupportedModuleBox);

        VisualElement feedbackOptionsContainer = new VisualElement();
        feedbackOptionsContainer.style.marginLeft = 12f;
        settingsContainer.Add(feedbackOptionsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(feedbackOptionsContainer,
                                                   useEngagementFeedbackOverrideProperty,
                                                   "Use Engagement Feedback Override");

        VisualElement feedbackOverrideContainer = new VisualElement();
        feedbackOverrideContainer.style.marginLeft = 12f;
        feedbackOptionsContainer.Add(feedbackOverrideContainer);
        feedbackOverrideContainer.Add(EnemyOffensiveEngagementFeedbackDrawerUtility.BuildSettingsEditor(engagementFeedbackOverrideProperty));

        UpdateVisibility(enabledProperty,
                         useMinimumRangeProperty,
                         useMaximumRangeProperty,
                         displayBehaviourEngagementTriggerProperty,
                         useEngagementFeedbackOverrideProperty,
                         bindingProperty,
                         settingsContainer,
                         minimumRangeContainer,
                         maximumRangeContainer,
                         feedbackOptionsContainer,
                         feedbackOverrideContainer,
                         unsupportedModuleBox);
        root.TrackPropertyValue(enabledProperty, changedProperty =>
        {
            UpdateVisibility(changedProperty,
                             useMinimumRangeProperty,
                             useMaximumRangeProperty,
                             displayBehaviourEngagementTriggerProperty,
                             useEngagementFeedbackOverrideProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox);
        });
        root.TrackPropertyValue(useMinimumRangeProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             changedProperty,
                             useMaximumRangeProperty,
                             displayBehaviourEngagementTriggerProperty,
                             useEngagementFeedbackOverrideProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox);
        });
        root.TrackPropertyValue(useMaximumRangeProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             useMinimumRangeProperty,
                             changedProperty,
                             displayBehaviourEngagementTriggerProperty,
                             useEngagementFeedbackOverrideProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox);
        });
        root.TrackPropertyValue(displayBehaviourEngagementTriggerProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             useMinimumRangeProperty,
                             useMaximumRangeProperty,
                             changedProperty,
                             useEngagementFeedbackOverrideProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox);
        });
        root.TrackPropertyValue(useEngagementFeedbackOverrideProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             useMinimumRangeProperty,
                             useMaximumRangeProperty,
                             displayBehaviourEngagementTriggerProperty,
                             changedProperty,
                             bindingProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer,
                             feedbackOptionsContainer,
                             feedbackOverrideContainer,
                             unsupportedModuleBox);
        });

        if (moduleIdProperty != null)
        {
            root.TrackPropertyValue(moduleIdProperty, changedProperty =>
            {
                UpdateVisibility(enabledProperty,
                                 useMinimumRangeProperty,
                                 useMaximumRangeProperty,
                                 displayBehaviourEngagementTriggerProperty,
                                 useEngagementFeedbackOverrideProperty,
                                 bindingProperty,
                                 settingsContainer,
                                 minimumRangeContainer,
                                 maximumRangeContainer,
                                 feedbackOptionsContainer,
                                 feedbackOverrideContainer,
                                 unsupportedModuleBox);
            });
        }

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates nested settings visibility, range-gated fields, and unsupported-module warnings from the current toggle state.
    /// /params enabledProperty Serialized enabled property.
    /// /params useMinimumRangeProperty Serialized minimum-range toggle.
    /// /params useMaximumRangeProperty Serialized maximum-range toggle.
    /// /params displayTriggerProperty Serialized trigger toggle.
    /// /params useOverrideProperty Serialized override toggle.
    /// /params bindingProperty Serialized module binding.
    /// /params settingsContainer Main nested settings container.
    /// /params minimumRangeContainer Nested minimum-range field container.
    /// /params maximumRangeContainer Nested maximum-range field container.
    /// /params feedbackOptionsContainer Nested feedback options container.
    /// /params feedbackOverrideContainer Nested override settings container.
    /// /params unsupportedModuleBox Warning box shown for unsupported module kinds.
    /// /returns None.
    /// </summary>
    private static void UpdateVisibility(SerializedProperty enabledProperty,
                                         SerializedProperty useMinimumRangeProperty,
                                         SerializedProperty useMaximumRangeProperty,
                                         SerializedProperty displayTriggerProperty,
                                         SerializedProperty useOverrideProperty,
                                         SerializedProperty bindingProperty,
                                         VisualElement settingsContainer,
                                         VisualElement minimumRangeContainer,
                                         VisualElement maximumRangeContainer,
                                         VisualElement feedbackOptionsContainer,
                                         VisualElement feedbackOverrideContainer,
                                         HelpBox unsupportedModuleBox)
    {
        bool isInteractionEnabled = enabledProperty != null && enabledProperty.boolValue;
        bool useMinimumRange = isInteractionEnabled &&
                               useMinimumRangeProperty != null &&
                               useMinimumRangeProperty.boolValue;
        bool useMaximumRange = isInteractionEnabled &&
                               useMaximumRangeProperty != null &&
                               useMaximumRangeProperty.boolValue;
        bool isTriggerEnabled = isInteractionEnabled &&
                                displayTriggerProperty != null &&
                                displayTriggerProperty.boolValue;
        bool isOverrideEnabled = isTriggerEnabled &&
                                 useOverrideProperty != null &&
                                 useOverrideProperty.boolValue;
        bool showUnsupportedModuleWarning = isTriggerEnabled &&
                                            !EnemyOffensiveEngagementFeedbackDrawerUtility.SupportsDisplayTrigger(bindingProperty,
                                                                                                                 EnemyPatternModuleCatalogSection.WeaponInteraction);

        if (settingsContainer != null)
        {
            settingsContainer.style.display = isInteractionEnabled
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (minimumRangeContainer != null)
        {
            minimumRangeContainer.style.display = useMinimumRange
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (maximumRangeContainer != null)
        {
            maximumRangeContainer.style.display = useMaximumRange
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (feedbackOptionsContainer != null)
        {
            feedbackOptionsContainer.style.display = isTriggerEnabled
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (feedbackOverrideContainer != null)
        {
            feedbackOverrideContainer.style.display = isOverrideEnabled
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (unsupportedModuleBox != null)
        {
            unsupportedModuleBox.style.display = showUnsupportedModuleWarning
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
    #endregion

    #endregion
}
