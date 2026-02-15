using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(PassiveToolDefinition))]
public sealed class PassiveToolDefinitionPropertyDrawer : PropertyDrawer
{
    #region Fields
    private static readonly List<PassiveToolKind> SupportedKinds = new List<PassiveToolKind>
    {
        PassiveToolKind.ProjectileSize
    };
    #endregion

    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty commonDataProperty = property.FindPropertyRelative("commonData");
        SerializedProperty toolKindProperty = property.FindPropertyRelative("toolKind");
        SerializedProperty projectileSizeDataProperty = property.FindPropertyRelative("projectileSizeData");

        if (commonDataProperty == null ||
            toolKindProperty == null ||
            projectileSizeDataProperty == null)
        {
            Label errorLabel = new Label("Passive tool data is missing serialized fields.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        PassiveToolKind initialKind = SanitizeKind(toolKindProperty);
        PopupField<PassiveToolKind> toolKindField = new PopupField<PassiveToolKind>("Tool Type", SupportedKinds, initialKind);
        toolKindField.formatListItemCallback = FormatToolKind;
        toolKindField.formatSelectedValueCallback = FormatToolKind;

        VisualElement toolSpecificContainer = new VisualElement();
        toolSpecificContainer.style.marginTop = 2f;

        AddField(root, commonDataProperty, "Common Data");
        root.Add(toolKindField);

        Label toolSpecificLabel = new Label("Tool Specific");
        toolSpecificLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toolSpecificLabel.style.marginTop = 4f;
        root.Add(toolSpecificLabel);
        root.Add(toolSpecificContainer);

        toolKindField.RegisterValueChangedCallback(evt =>
        {
            SetToolKind(toolKindProperty, evt.newValue);
            RefreshToolSpecific(toolSpecificContainer, toolKindProperty, projectileSizeDataProperty);
        });

        root.TrackPropertyValue(toolKindProperty, changedProperty =>
        {
            PassiveToolKind selectedKind = SanitizeKind(changedProperty);

            if (toolKindField.value != selectedKind)
                toolKindField.SetValueWithoutNotify(selectedKind);

            RefreshToolSpecific(toolSpecificContainer, changedProperty, projectileSizeDataProperty);
        });

        RefreshToolSpecific(toolSpecificContainer, toolKindProperty, projectileSizeDataProperty);
        return root;
    }

    private static void AddField(VisualElement parent, SerializedProperty property, string labelOverride = null)
    {
        if (parent == null || property == null)
            return;

        PropertyField field = string.IsNullOrWhiteSpace(labelOverride)
            ? new PropertyField(property)
            : new PropertyField(property, labelOverride);
        parent.Add(field);
    }

    private static string FormatToolKind(PassiveToolKind toolKind)
    {
        switch (toolKind)
        {
            case PassiveToolKind.ProjectileSize:
                return "Projectile Size";
            default:
                return "Projectile Size";
        }
    }

    private static PassiveToolKind SanitizeKind(SerializedProperty toolKindProperty)
    {
        if (toolKindProperty == null)
            return PassiveToolKind.ProjectileSize;

        PassiveToolKind currentKind = (PassiveToolKind)toolKindProperty.enumValueIndex;

        if (currentKind == PassiveToolKind.ProjectileSize)
            return PassiveToolKind.ProjectileSize;

        toolKindProperty.serializedObject.Update();
        toolKindProperty.enumValueIndex = (int)PassiveToolKind.ProjectileSize;
        toolKindProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return PassiveToolKind.ProjectileSize;
    }

    private static void SetToolKind(SerializedProperty toolKindProperty, PassiveToolKind selectedKind)
    {
        if (toolKindProperty == null)
            return;

        PassiveToolKind sanitizedKind = selectedKind;

        if (sanitizedKind != PassiveToolKind.ProjectileSize)
            sanitizedKind = PassiveToolKind.ProjectileSize;

        if (toolKindProperty.enumValueIndex == (int)sanitizedKind)
            return;

        toolKindProperty.serializedObject.Update();
        toolKindProperty.enumValueIndex = (int)sanitizedKind;
        toolKindProperty.serializedObject.ApplyModifiedProperties();
    }

    private static void RefreshToolSpecific(VisualElement container,
                                            SerializedProperty toolKindProperty,
                                            SerializedProperty projectileSizeDataProperty)
    {
        if (container == null)
            return;

        container.Clear();
        PassiveToolKind selectedKind = SanitizeKind(toolKindProperty);

        switch (selectedKind)
        {
            case PassiveToolKind.ProjectileSize:
                AddField(container, projectileSizeDataProperty, "Projectile Size Settings");
                return;
        }
    }
    #endregion
}
