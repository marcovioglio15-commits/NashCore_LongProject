using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(PlayerScalableStatDefinition))]
public sealed class PlayerScalableStatDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty statNameProperty = property.FindPropertyRelative("statName");
        SerializedProperty statTypeProperty = property.FindPropertyRelative("statType");
        SerializedProperty defaultValueProperty = property.FindPropertyRelative("defaultValue");
        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        SerializedProperty scalableStatsProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalableStats")
            : null;
        HashSet<string> allowedVariables = PlayerScalingFormulaValidationUtility.BuildVariableSet(scalableStatsProperty);

        if (statNameProperty == null || statTypeProperty == null || defaultValueProperty == null)
        {
            Label missingLabel = new Label("Scalable stat entry is missing serialized fields.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(missingLabel);
            return root;
        }

        AddField(root, statNameProperty, scalingRulesProperty, "Name", allowedVariables);
        AddField(root, statTypeProperty, scalingRulesProperty, "Type", allowedVariables);
        AddField(root, defaultValueProperty, scalingRulesProperty, "Default Value", allowedVariables);
        return root;
    }
    #endregion

    #region Helpers
    private static void AddField(VisualElement parent,
                                 SerializedProperty property,
                                 SerializedProperty scalingRulesProperty,
                                 string label,
                                 ISet<string> allowedVariables)
    {
        if (parent == null || property == null)
            return;

        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property,
                                                                           scalingRulesProperty,
                                                                           label,
                                                                           allowedVariables);
        parent.Add(field);
    }
    #endregion

    #endregion
}
