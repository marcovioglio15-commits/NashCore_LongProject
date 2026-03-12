using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Custom drawer for one game phase definition that routes numeric children through scaling-aware fields.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerGamePhaseDefinition))]
public sealed class PlayerGamePhaseDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds one UI Toolkit inspector for one serialized game phase definition.
    /// </summary>
    /// <param name="property">Serialized game phase property.</param>
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
        VisualElement warningsRoot = new VisualElement();
        warningsRoot.style.marginTop = 4f;
        root.Add(warningsRoot);
        PlayerMilestoneOverlapWarningUtility.RefreshWarnings(warningsRoot, property);
        root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerMilestoneOverlapWarningUtility.RefreshWarnings(warningsRoot, property);
        });
        return root;
    }
    #endregion

    #endregion
}
