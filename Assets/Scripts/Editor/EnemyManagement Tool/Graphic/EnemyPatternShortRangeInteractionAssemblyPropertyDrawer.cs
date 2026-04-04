using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternShortRangeInteractionAssembly.
/// /params None.
/// /returns None.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternShortRangeInteractionAssembly))]
public sealed class EnemyPatternShortRangeInteractionAssemblyPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the short-range interaction assembly UI.
    /// /params property Serialized short-range assembly property.
    /// /returns The built root visual element.
    /// </summary>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty activationRangeProperty = property.FindPropertyRelative("activationRange");
        SerializedProperty releaseDistanceBufferProperty = property.FindPropertyRelative("releaseDistanceBuffer");
        SerializedProperty bindingProperty = property.FindPropertyRelative("binding");

        if (enabledProperty == null ||
            activationRangeProperty == null ||
            releaseDistanceBufferProperty == null ||
            bindingProperty == null)
        {
            Label errorLabel = new Label("Short-Range Interaction assembly fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(root, enabledProperty, "Enable Short-Range Interaction");

        VisualElement settingsContainer = new VisualElement();
        settingsContainer.style.marginLeft = 12f;
        root.Add(settingsContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer, activationRangeProperty, "Activation Range");
        EnemyAdvancedPatternDrawerUtility.AddField(settingsContainer, releaseDistanceBufferProperty, "Release Distance Buffer");

        PropertyField bindingField = new PropertyField(bindingProperty, "Short-Range Module");
        bindingField.BindProperty(bindingProperty);
        bindingField.tooltip = "Binding selected only while the player stays inside Activation Range plus Release Distance Buffer.";
        settingsContainer.Add(bindingField);

        UpdateVisibility(enabledProperty, settingsContainer);
        root.TrackPropertyValue(enabledProperty, changedProperty =>
        {
            UpdateVisibility(changedProperty, settingsContainer);
        });

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates the nested settings visibility from the enabled toggle.
    /// /params enabledProperty Serialized enabled property.
    /// /params settingsContainer Nested settings container.
    /// /returns None.
    /// </summary>
    private static void UpdateVisibility(SerializedProperty enabledProperty, VisualElement settingsContainer)
    {
        if (settingsContainer == null)
            return;

        settingsContainer.style.display = enabledProperty != null && enabledProperty.boolValue
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }
    #endregion

    #endregion
}
