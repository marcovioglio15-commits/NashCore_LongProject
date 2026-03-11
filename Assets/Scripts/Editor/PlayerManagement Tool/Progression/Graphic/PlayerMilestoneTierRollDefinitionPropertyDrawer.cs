using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one milestone tier-roll entry with enum-style tier selection sourced from power-ups presets.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerMilestoneTierRollDefinition))]
public sealed class PlayerMilestoneTierRollDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one milestone tier-roll entry.
    /// </summary>
    /// <param name="property">Serialized milestone tier-roll property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty tierIdProperty = property.FindPropertyRelative("tierId");
        SerializedProperty selectionWeightProperty = property.FindPropertyRelative("selectionWeight");

        if (tierIdProperty == null || selectionWeightProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Milestone tier roll fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        List<string> tierIdOptions = PlayerProgressionTierOptionsUtility.BuildTierIdOptionsFromPowerUpsLibrary();

        if (tierIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No Power-Up tier IDs found. Create tiers in a Power-Ups preset first.", HelpBoxMessageType.Warning);
            root.Add(warningBox);
            TextField tierIdPreviewField = new TextField("Tier ID");
            tierIdPreviewField.value = "<No tiers available>";
            tierIdPreviewField.SetEnabled(false);
            root.Add(tierIdPreviewField);
        }
        else
        {
            string selectedTierId = ResolveSelectedTierId(tierIdOptions, tierIdProperty.stringValue);

            if (!string.Equals(tierIdProperty.stringValue, selectedTierId, System.StringComparison.Ordinal))
            {
                tierIdProperty.serializedObject.Update();
                tierIdProperty.stringValue = selectedTierId;
                tierIdProperty.serializedObject.ApplyModifiedProperties();
            }

            PopupField<string> tierIdPopup = new PopupField<string>("Tier ID", tierIdOptions, selectedTierId);
            tierIdPopup.RegisterValueChangedCallback(evt =>
            {
                tierIdProperty.serializedObject.Update();
                tierIdProperty.stringValue = evt.newValue;
                tierIdProperty.serializedObject.ApplyModifiedProperties();
            });
            root.Add(tierIdPopup);
        }

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement selectionWeightField = PlayerScalingFieldElementFactory.CreateField(selectionWeightProperty,
                                                                                          scalingRulesProperty,
                                                                                          "Selection Weight");
        root.Add(selectionWeightField);

        return root;
    }
    #endregion

    #region Private Methods
    private static string ResolveSelectedTierId(List<string> options, string currentTierId)
    {
        if (options == null || options.Count <= 0)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(currentTierId))
            return options[0];

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            string option = options[optionIndex];

            if (string.Equals(option, currentTierId, System.StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return options[0];
    }
    #endregion

    #endregion
}
