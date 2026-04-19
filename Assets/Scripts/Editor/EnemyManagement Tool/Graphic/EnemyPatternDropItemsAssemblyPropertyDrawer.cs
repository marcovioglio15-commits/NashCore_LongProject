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
        SerializedProperty modulesProperty = property.FindPropertyRelative("modules");

        if (enabledProperty == null || modulesProperty == null)
        {
            Label errorLabel = new Label("Drop Items assembly fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(root, enabledProperty, "Enable Drop Items Interaction");
        HelpBox infoBox = new HelpBox("Drop Items now supports multiple module bindings, so one pattern can combine experience drops and Extra Combo Points at the same time.", HelpBoxMessageType.Info);
        infoBox.style.marginTop = 2f;
        root.Add(infoBox);

        VisualElement settingsContainer = new VisualElement();
        settingsContainer.style.marginLeft = 12f;
        root.Add(settingsContainer);

        PropertyField modulesField = new PropertyField(modulesProperty, "Drop Items Modules");
        modulesField.BindProperty(modulesProperty);
        modulesField.tooltip = "Optional drop-items bindings resolved from the shared Drop Items catalog. Modules are compiled in list order.";
        settingsContainer.Add(modulesField);

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
