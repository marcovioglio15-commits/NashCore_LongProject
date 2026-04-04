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
        SerializedProperty bindingProperty = property.FindPropertyRelative("binding");

        if (enabledProperty == null ||
            useMinimumRangeProperty == null ||
            minimumRangeProperty == null ||
            useMaximumRangeProperty == null ||
            maximumRangeProperty == null ||
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

        PropertyField bindingField = new PropertyField(bindingProperty, "Weapon Module");
        bindingField.BindProperty(bindingProperty);
        bindingField.tooltip = "Binding selected after the shared minimum and maximum range gates defined in this assembly evaluate true.";
        settingsContainer.Add(bindingField);

        UpdateVisibility(enabledProperty,
                         useMinimumRangeProperty,
                         useMaximumRangeProperty,
                         settingsContainer,
                         minimumRangeContainer,
                         maximumRangeContainer);
        root.TrackPropertyValue(enabledProperty, changedProperty =>
        {
            UpdateVisibility(changedProperty,
                             useMinimumRangeProperty,
                             useMaximumRangeProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer);
        });
        root.TrackPropertyValue(useMinimumRangeProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             changedProperty,
                             useMaximumRangeProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer);
        });
        root.TrackPropertyValue(useMaximumRangeProperty, changedProperty =>
        {
            UpdateVisibility(enabledProperty,
                             useMinimumRangeProperty,
                             changedProperty,
                             settingsContainer,
                             minimumRangeContainer,
                             maximumRangeContainer);
        });

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates the nested settings and gated range fields visibility from the current toggle values.
    /// /params enabledProperty Serialized enabled property.
    /// /params useMinimumRangeProperty Serialized minimum-range toggle.
    /// /params useMaximumRangeProperty Serialized maximum-range toggle.
    /// /params settingsContainer Main nested settings container.
    /// /params minimumRangeContainer Nested minimum-range field container.
    /// /params maximumRangeContainer Nested maximum-range field container.
    /// /returns None.
    /// </summary>
    private static void UpdateVisibility(SerializedProperty enabledProperty,
                                         SerializedProperty useMinimumRangeProperty,
                                         SerializedProperty useMaximumRangeProperty,
                                         VisualElement settingsContainer,
                                         VisualElement minimumRangeContainer,
                                         VisualElement maximumRangeContainer)
    {
        if (settingsContainer != null)
        {
            settingsContainer.style.display = enabledProperty != null && enabledProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (minimumRangeContainer != null)
        {
            minimumRangeContainer.style.display = enabledProperty != null &&
                                                 enabledProperty.boolValue &&
                                                 useMinimumRangeProperty != null &&
                                                 useMinimumRangeProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (maximumRangeContainer != null)
        {
            maximumRangeContainer.style.display = enabledProperty != null &&
                                                 enabledProperty.boolValue &&
                                                 useMaximumRangeProperty != null &&
                                                 useMaximumRangeProperty.boolValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
    #endregion

    #endregion
}
