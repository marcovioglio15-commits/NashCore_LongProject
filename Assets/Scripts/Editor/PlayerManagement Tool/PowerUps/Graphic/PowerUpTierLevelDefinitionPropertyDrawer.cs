using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws one tier definition and warns when its entry percentages do not sum to 100%.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpTierLevelDefinition))]
public sealed class PowerUpTierLevelDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float PercentageWarningTolerance = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one tier definition.
    /// </summary>
    /// <param name="property">Serialized tier definition property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty tierIdProperty = property.FindPropertyRelative("tierId");
        SerializedProperty entriesProperty = property.FindPropertyRelative("entries");

        if (tierIdProperty == null || entriesProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Tier level fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        PropertyField tierIdField = new PropertyField(tierIdProperty, "Tier ID");
        tierIdField.BindProperty(tierIdProperty);
        root.Add(tierIdField);

        PropertyField entriesField = new PropertyField(entriesProperty, "Entries");
        entriesField.BindProperty(entriesProperty);
        root.Add(entriesField);

        HelpBox percentageWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        root.Add(percentageWarningBox);

        void RefreshPercentageWarning()
        {
            float totalPercentage = CalculateEntryPercentageSum(entriesProperty);

            if (Mathf.Abs(totalPercentage - 100f) <= PercentageWarningTolerance)
            {
                percentageWarningBox.style.display = DisplayStyle.None;
                return;
            }

            percentageWarningBox.text = string.Format(CultureInfo.InvariantCulture,
                                                      "Tier entry percentages currently sum to {0:0.##}%. Set the total to 100% to match the intended extraction odds.",
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
    /// Sums all non-negative entry percentages configured inside the serialized tier entry array.
    /// </summary>
    /// <param name="entriesProperty">Serialized entries array belonging to the tier definition.</param>
    /// <returns>Total configured percentage for the current tier.</returns>
    private static float CalculateEntryPercentageSum(SerializedProperty entriesProperty)
    {
        if (entriesProperty == null || !entriesProperty.isArray)
            return 0f;

        float totalPercentage = 0f;

        for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
        {
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);

            if (entryProperty == null)
                continue;

            SerializedProperty selectionWeightProperty = entryProperty.FindPropertyRelative("selectionWeight");

            if (selectionWeightProperty == null)
                continue;

            totalPercentage += Mathf.Max(0f, selectionWeightProperty.floatValue);
        }

        return totalPercentage;
    }
    #endregion

    #endregion
}
