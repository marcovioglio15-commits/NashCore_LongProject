using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(PowerUpCommonData))]
public sealed class PowerUpCommonDataPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty powerUpIdProperty = property.FindPropertyRelative("powerUpId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty descriptionProperty = property.FindPropertyRelative("description");
        SerializedProperty iconProperty = property.FindPropertyRelative("icon");
        SerializedProperty dropPoolsProperty = property.FindPropertyRelative("dropPools");
        SerializedProperty dropTierProperty = property.FindPropertyRelative("dropTier");
        SerializedProperty purchaseCostProperty = property.FindPropertyRelative("purchaseCost");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;

        if (powerUpIdProperty == null ||
            displayNameProperty == null ||
            descriptionProperty == null ||
            iconProperty == null ||
            dropPoolsProperty == null ||
            dropTierProperty == null ||
            purchaseCostProperty == null)
        {
            Label missingLabel = new Label("Power-up common data fields are missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(missingLabel);
            return root;
        }

        AddField(root, powerUpIdProperty, scalingRulesProperty, "Power Up ID");
        AddField(root, displayNameProperty, scalingRulesProperty, "Display Name");
        AddField(root, descriptionProperty, scalingRulesProperty, "Description");
        AddField(root, iconProperty, scalingRulesProperty, "Icon");
        AddField(root, dropPoolsProperty, scalingRulesProperty, "Drop Pools");
        AddField(root, dropTierProperty, scalingRulesProperty, "Drop Tier");
        AddField(root, purchaseCostProperty, scalingRulesProperty, "Purchase Cost");
        return root;
    }
    #endregion

    #region Helpers
    private static void AddField(VisualElement parent,
                                 SerializedProperty property,
                                 SerializedProperty scalingRulesProperty,
                                 string label)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);
        parent.Add(field);
    }
    #endregion

    #endregion
}
