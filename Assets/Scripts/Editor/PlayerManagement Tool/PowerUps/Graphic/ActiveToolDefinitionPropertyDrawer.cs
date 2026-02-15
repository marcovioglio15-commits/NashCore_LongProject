using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(ActiveToolDefinition))]
public sealed class ActiveToolDefinitionPropertyDrawer : PropertyDrawer
{
    #region Fields
    private static readonly List<ActiveToolKind> SupportedKinds = new List<ActiveToolKind>
    {
        ActiveToolKind.Bomb,
        ActiveToolKind.Dash,
        ActiveToolKind.BulletTime
    };
    #endregion

    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty commonDataProperty = property.FindPropertyRelative("commonData");
        SerializedProperty toolKindProperty = property.FindPropertyRelative("toolKind");
        SerializedProperty maximumEnergyProperty = property.FindPropertyRelative("maximumEnergy");
        SerializedProperty toggleableProperty = property.FindPropertyRelative("toggleable");
        SerializedProperty activationResourceProperty = property.FindPropertyRelative("activationResource");
        SerializedProperty activationCostProperty = property.FindPropertyRelative("activationCost");
        SerializedProperty maintenanceResourceProperty = property.FindPropertyRelative("maintenanceResource");
        SerializedProperty maintenanceCostPerSecondProperty = property.FindPropertyRelative("maintenanceCostPerSecond");
        SerializedProperty chargeTypeProperty = property.FindPropertyRelative("chargeType");
        SerializedProperty chargePerTriggerProperty = property.FindPropertyRelative("chargePerTrigger");
        SerializedProperty fullChargeRequirementProperty = property.FindPropertyRelative("fullChargeRequirement");
        SerializedProperty unreplaceableProperty = property.FindPropertyRelative("unreplaceable");
        SerializedProperty bombDataProperty = property.FindPropertyRelative("bombData");
        SerializedProperty dashDataProperty = property.FindPropertyRelative("dashData");
        SerializedProperty bulletTimeDataProperty = property.FindPropertyRelative("bulletTimeData");

        if (commonDataProperty == null ||
            toolKindProperty == null ||
            maximumEnergyProperty == null ||
            toggleableProperty == null ||
            activationResourceProperty == null ||
            activationCostProperty == null ||
            maintenanceResourceProperty == null ||
            maintenanceCostPerSecondProperty == null ||
            chargeTypeProperty == null ||
            chargePerTriggerProperty == null ||
            fullChargeRequirementProperty == null ||
            unreplaceableProperty == null ||
            bombDataProperty == null ||
            dashDataProperty == null ||
            bulletTimeDataProperty == null)
        {
            Label errorLabel = new Label("Active tool data is missing serialized fields.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        ActiveToolKind initialKind = SanitizeKind(toolKindProperty);
        PopupField<ActiveToolKind> toolKindField = new PopupField<ActiveToolKind>("Tool Type", SupportedKinds, initialKind);
        toolKindField.formatListItemCallback = FormatToolKind;
        toolKindField.formatSelectedValueCallback = FormatToolKind;

        VisualElement toolSpecificContainer = new VisualElement();
        toolSpecificContainer.style.marginTop = 2f;

        AddField(root, commonDataProperty, "Common Data");
        root.Add(toolKindField);
        AddField(root, maximumEnergyProperty);
        AddField(root, toggleableProperty);
        AddField(root, activationResourceProperty);
        AddField(root, activationCostProperty);
        AddField(root, maintenanceResourceProperty);
        AddField(root, maintenanceCostPerSecondProperty);
        AddField(root, chargeTypeProperty);
        AddField(root, chargePerTriggerProperty);
        AddField(root, fullChargeRequirementProperty);
        AddField(root, unreplaceableProperty);

        Label toolSpecificLabel = new Label("Tool Specific");
        toolSpecificLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toolSpecificLabel.style.marginTop = 4f;
        root.Add(toolSpecificLabel);
        root.Add(toolSpecificContainer);

        toolKindField.RegisterValueChangedCallback(evt =>
        {
            SetToolKind(toolKindProperty, evt.newValue);
            RefreshToolSpecific(toolSpecificContainer, toolKindProperty, bombDataProperty, dashDataProperty, bulletTimeDataProperty);
        });

        root.TrackPropertyValue(toolKindProperty, changedProperty =>
        {
            ActiveToolKind selectedKind = SanitizeKind(changedProperty);

            if (toolKindField.value != selectedKind)
                toolKindField.SetValueWithoutNotify(selectedKind);

            RefreshToolSpecific(toolSpecificContainer, changedProperty, bombDataProperty, dashDataProperty, bulletTimeDataProperty);
        });

        RefreshToolSpecific(toolSpecificContainer, toolKindProperty, bombDataProperty, dashDataProperty, bulletTimeDataProperty);
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

    private static string FormatToolKind(ActiveToolKind toolKind)
    {
        switch (toolKind)
        {
            case ActiveToolKind.Bomb:
                return "Bomb";
            case ActiveToolKind.Dash:
                return "Dash";
            case ActiveToolKind.BulletTime:
                return "Bullet Time";
            default:
                return "Bomb";
        }
    }

    private static ActiveToolKind SanitizeKind(SerializedProperty toolKindProperty)
    {
        if (toolKindProperty == null)
            return ActiveToolKind.Bomb;

        ActiveToolKind currentKind = (ActiveToolKind)toolKindProperty.enumValueIndex;

        if (currentKind == ActiveToolKind.Bomb)
            return ActiveToolKind.Bomb;

        if (currentKind == ActiveToolKind.Dash)
            return ActiveToolKind.Dash;

        if (currentKind == ActiveToolKind.BulletTime)
            return ActiveToolKind.BulletTime;

        toolKindProperty.serializedObject.Update();
        toolKindProperty.enumValueIndex = (int)ActiveToolKind.Bomb;
        toolKindProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return ActiveToolKind.Bomb;
    }

    private static void SetToolKind(SerializedProperty toolKindProperty, ActiveToolKind selectedKind)
    {
        if (toolKindProperty == null)
            return;

        ActiveToolKind sanitizedKind = selectedKind;

        if (sanitizedKind != ActiveToolKind.Bomb &&
            sanitizedKind != ActiveToolKind.Dash &&
            sanitizedKind != ActiveToolKind.BulletTime)
            sanitizedKind = ActiveToolKind.Bomb;

        if (toolKindProperty.enumValueIndex == (int)sanitizedKind)
            return;

        toolKindProperty.serializedObject.Update();
        toolKindProperty.enumValueIndex = (int)sanitizedKind;
        toolKindProperty.serializedObject.ApplyModifiedProperties();
    }

    private static void RefreshToolSpecific(VisualElement container,
                                            SerializedProperty toolKindProperty,
                                            SerializedProperty bombDataProperty,
                                            SerializedProperty dashDataProperty,
                                            SerializedProperty bulletTimeDataProperty)
    {
        if (container == null)
            return;

        container.Clear();
        ActiveToolKind selectedKind = SanitizeKind(toolKindProperty);

        switch (selectedKind)
        {
            case ActiveToolKind.Bomb:
                AddField(container, bombDataProperty, "Bomb Settings");
                return;
            case ActiveToolKind.Dash:
                AddField(container, dashDataProperty, "Dash Settings");
                return;
            case ActiveToolKind.BulletTime:
                AddField(container, bulletTimeDataProperty, "Bullet Time Settings");
                return;
        }
    }
    #endregion
}
