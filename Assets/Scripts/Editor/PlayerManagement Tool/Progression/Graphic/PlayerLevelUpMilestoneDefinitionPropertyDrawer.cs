using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Custom drawer for one level-up milestone definition with scaling-aware numeric controls.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerLevelUpMilestoneDefinition))]
public sealed class PlayerLevelUpMilestoneDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one UI Toolkit inspector for one serialized milestone definition.
    /// </summary>
    /// <param name="property">Serialized milestone property.</param>
    /// <returns>Generated root visual element for the property drawer.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty scalingRulesProperty = null;

        if (property != null && property.serializedObject != null)
            scalingRulesProperty = property.serializedObject.FindProperty("scalingRules");

        PlayerProgressionScalingDrawerUtility.PopulateDirectChildFields(root,
                                                                        property,
                                                                        scalingRulesProperty);
        return root;
    }
    #endregion

    #endregion
}
