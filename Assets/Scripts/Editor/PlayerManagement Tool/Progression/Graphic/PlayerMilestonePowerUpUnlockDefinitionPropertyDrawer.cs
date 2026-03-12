using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Draws one milestone power-up extraction entry with its custom tier-roll collection.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerMilestonePowerUpUnlockDefinition))]
public sealed class PlayerMilestonePowerUpUnlockDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one milestone power-up extraction definition.
    /// </summary>
    /// <param name="property">Serialized milestone power-up extraction property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty tierRollsProperty = property.FindPropertyRelative("tierRolls");

        if (tierRollsProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Milestone power-up unlock fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        List<string> tierIdOptions = PlayerProgressionTierOptionsUtility.BuildTierIdOptionsFromPowerUpsLibrary();

        if (tierIdOptions.Count <= 0)
        {
            HelpBox warningBox = new HelpBox("No Power-Up tier IDs found. Create tiers in a Power-Ups preset first.", HelpBoxMessageType.Warning);
            root.Add(warningBox);
        }

        PropertyField tierRollsField = new PropertyField(tierRollsProperty, "Tier Rolls");
        tierRollsField.BindProperty(tierRollsProperty);
        root.Add(tierRollsField);
        return root;
    }
    #endregion

    #endregion
}
