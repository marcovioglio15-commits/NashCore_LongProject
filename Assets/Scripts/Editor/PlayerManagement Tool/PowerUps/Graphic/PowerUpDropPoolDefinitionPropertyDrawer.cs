using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws one drop-pool definition and warns when its tier percentages do not sum to 100%.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpDropPoolDefinition))]
public sealed class PowerUpDropPoolDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float PercentageWarningTolerance = 0.01f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for one drop-pool definition.
    /// </summary>
    /// <param name="property">Serialized drop-pool definition property.</param>
    /// <returns>Root UI element used by the inspector.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty poolIdProperty = property.FindPropertyRelative("poolId");
        SerializedProperty tierRollsProperty = property.FindPropertyRelative("tierRolls");

        if (poolIdProperty == null || tierRollsProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Drop-pool fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        PropertyField poolIdField = new PropertyField(poolIdProperty, "Pool ID");
        poolIdField.BindProperty(poolIdProperty);
        root.Add(poolIdField);

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
                                                      "Drop-pool tier percentages currently sum to {0:0.##}%. Set the total to 100% to match the intended extraction odds.",
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
    /// Sums all non-negative tier percentages configured inside the serialized drop-pool tier array.
    /// </summary>
    /// <param name="tierRollsProperty">Serialized tier-roll array belonging to the drop-pool definition.</param>
    /// <returns>Total configured percentage for the current drop pool.</returns>
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
