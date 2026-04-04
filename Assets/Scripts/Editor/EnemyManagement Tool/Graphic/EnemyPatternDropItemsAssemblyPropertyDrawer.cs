using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternDropItemsAssembly.
/// /params None.
/// /returns None.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternDropItemsAssembly))]
public sealed class EnemyPatternDropItemsAssemblyPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the drop-items assembly UI.
    /// /params property Serialized drop-items assembly property.
    /// /returns The built root visual element.
    /// </summary>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty bindingProperty = property.FindPropertyRelative("binding");

        if (enabledProperty == null || bindingProperty == null)
        {
            Label errorLabel = new Label("Drop Items assembly fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(root, enabledProperty, "Enable Drop Items Interaction");

        VisualElement settingsContainer = new VisualElement();
        settingsContainer.style.marginLeft = 12f;
        root.Add(settingsContainer);

        PropertyField bindingField = new PropertyField(bindingProperty, "Drop Items Module");
        bindingField.BindProperty(bindingProperty);
        bindingField.tooltip = "Optional drop-items binding resolved from the shared Drop Items catalog.";
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
    /// Updates the drop-items nested settings visibility from the enabled toggle.
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
