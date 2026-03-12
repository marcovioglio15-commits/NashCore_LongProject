using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one tier entry with enum-style power-up ID selection sourced from modular power-up definitions.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpTierEntryDefinition))]
public sealed class PowerUpTierEntryDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one tier entry.
    /// </summary>
    /// <param name="property">Serialized tier-entry property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty entryKindProperty = property.FindPropertyRelative("entryKind");
        SerializedProperty powerUpIdProperty = property.FindPropertyRelative("powerUpId");
        SerializedProperty selectionWeightProperty = property.FindPropertyRelative("selectionWeight");

        if (entryKindProperty == null || powerUpIdProperty == null || selectionWeightProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Tier entry fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        PropertyField entryKindField = new PropertyField(entryKindProperty, "Power-Up Type");
        entryKindField.BindProperty(entryKindProperty);
        root.Add(entryKindField);

        VisualElement powerUpIdContainer = new VisualElement();
        root.Add(powerUpIdContainer);

        void RebuildPowerUpIdSelector()
        {
            powerUpIdContainer.Clear();
            PowerUpTierEntryKind entryKind = ResolveEntryKind(entryKindProperty);
            List<string> options = PowerUpTierOptionsUtility.BuildPowerUpIdOptions(property.serializedObject, entryKind);

            if (options.Count <= 0)
            {
                HelpBox missingOptionsHelpBox = new HelpBox("No modular power-up IDs available for this entry type.", HelpBoxMessageType.Warning);
                powerUpIdContainer.Add(missingOptionsHelpBox);
                return;
            }

            string selectedPowerUpId = ResolveSelectedPowerUpId(options, powerUpIdProperty.stringValue);

            if (!string.Equals(powerUpIdProperty.stringValue, selectedPowerUpId, System.StringComparison.Ordinal))
            {
                powerUpIdProperty.serializedObject.Update();
                powerUpIdProperty.stringValue = selectedPowerUpId;
                powerUpIdProperty.serializedObject.ApplyModifiedProperties();
            }

            PopupField<string> powerUpIdPopup = new PopupField<string>("Power-Up ID", options, selectedPowerUpId);
            powerUpIdPopup.RegisterValueChangedCallback(evt =>
            {
                powerUpIdProperty.serializedObject.Update();
                powerUpIdProperty.stringValue = evt.newValue;
                powerUpIdProperty.serializedObject.ApplyModifiedProperties();
            });
            powerUpIdContainer.Add(powerUpIdPopup);
        }

        entryKindField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            RebuildPowerUpIdSelector();
        });
        RebuildPowerUpIdSelector();

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement weightField = PlayerScalingFieldElementFactory.CreateField(selectionWeightProperty,
                                                                                 scalingRulesProperty,
                                                                                 "Selection Percentage (%)");
        root.Add(weightField);

        return root;
    }
    #endregion

    #region Private Methods
    private static PowerUpTierEntryKind ResolveEntryKind(SerializedProperty entryKindProperty)
    {
        if (entryKindProperty == null || entryKindProperty.propertyType != SerializedPropertyType.Enum)
            return PowerUpTierEntryKind.Active;

        int enumValue = entryKindProperty.enumValueIndex;

        if (!System.Enum.IsDefined(typeof(PowerUpTierEntryKind), enumValue))
            return PowerUpTierEntryKind.Active;

        return (PowerUpTierEntryKind)enumValue;
    }

    private static string ResolveSelectedPowerUpId(List<string> options, string currentValue)
    {
        if (options == null || options.Count <= 0)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(currentValue))
            return options[0];

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            string option = options[optionIndex];

            if (string.Equals(option, currentValue, System.StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return options[0];
    }
    #endregion

    #endregion
}
