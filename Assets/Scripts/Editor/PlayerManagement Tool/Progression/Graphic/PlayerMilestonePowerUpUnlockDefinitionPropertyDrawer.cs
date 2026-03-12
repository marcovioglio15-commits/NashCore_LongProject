using System.Globalization;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws one milestone power-up extraction entry with its custom tier-roll collection.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerMilestonePowerUpUnlockDefinition))]
public sealed class PlayerMilestonePowerUpUnlockDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float PercentageWarningTolerance = 0.01f;
    #endregion

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

        HelpBox percentageWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(percentageWarningBox);

        void RefreshPercentageWarning()
        {
            float totalPercentage = CalculateTierRollPercentageSum(tierRollsProperty);

            if (Mathf.Abs(totalPercentage - 100f) <= PercentageWarningTolerance)
            {
                percentageWarningBox.style.display = DisplayStyle.None;
                return;
            }

            percentageWarningBox.text = string.Format(CultureInfo.InvariantCulture,
                                                      "Tier roll percentages currently sum to {0:0.##}%. Set the total to 100% to match the intended milestone extraction odds.",
                                                      totalPercentage);
            percentageWarningBox.style.display = DisplayStyle.Flex;
        }

        root.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshPercentageWarning());
        RefreshPercentageWarning();
        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Sums all non-negative tier-roll percentages configured inside the serialized unlock definition.
    /// </summary>
    /// <param name="tierRollsProperty">Serialized tier-roll array belonging to one milestone unlock definition.</param>
    /// <returns>Total configured percentage across all tier-roll entries.</returns>
    private static float CalculateTierRollPercentageSum(SerializedProperty tierRollsProperty)
    {
        if (tierRollsProperty == null || !tierRollsProperty.isArray)
            return 0f;

        float totalPercentage = 0f;

        for (int tierRollIndex = 0; tierRollIndex < tierRollsProperty.arraySize; tierRollIndex++)
        {
            SerializedProperty tierRollProperty = tierRollsProperty.GetArrayElementAtIndex(tierRollIndex);

            if (tierRollProperty == null)
                continue;

            SerializedProperty selectionPercentageProperty = tierRollProperty.FindPropertyRelative("selectionPercentage");

            if (selectionPercentageProperty == null)
                continue;

            totalPercentage += Mathf.Max(0f, selectionPercentageProperty.floatValue);
        }

        return totalPercentage;
    }
    #endregion

    #endregion
}
