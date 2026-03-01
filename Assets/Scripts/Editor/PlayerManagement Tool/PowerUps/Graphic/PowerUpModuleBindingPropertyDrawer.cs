using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(PowerUpModuleBinding))]
public sealed class PowerUpModuleBindingPropertyDrawer : PropertyDrawer
{
    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty moduleIdProperty = property.FindPropertyRelative("moduleId");
        SerializedProperty stageProperty = property.FindPropertyRelative("stage");
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty useOverrideProperty = property.FindPropertyRelative("useOverridePayload");
        SerializedProperty overridePayloadProperty = property.FindPropertyRelative("overridePayload");

        if (moduleIdProperty == null ||
            stageProperty == null ||
            enabledProperty == null ||
            useOverrideProperty == null ||
            overridePayloadProperty == null)
        {
            Label errorLabel = new Label("PowerUpModuleBinding serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        List<string> moduleIdOptions = BuildModuleIdOptions(property.serializedObject);

        if (moduleIdOptions.Count == 0)
            moduleIdOptions.Add(string.Empty);

        string initialValue = ResolveInitialModuleId(moduleIdProperty.stringValue, moduleIdOptions);
        PopupField<string> modulePopup = new PopupField<string>("Module", moduleIdOptions, initialValue);
        modulePopup.tooltip = "Module ID reference from Modules Management.";
        root.Add(modulePopup);

        HelpBox moduleKindInfoBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        moduleKindInfoBox.style.marginTop = 2f;
        ApplyFieldAlignedBoxStyle(moduleKindInfoBox);
        root.Add(moduleKindInfoBox);

        AddField(root, enabledProperty, "Enabled");
        AddField(root, useOverrideProperty, "Use Override Payload");

        Label payloadHeader = new Label("Override Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        root.Add(payloadHeader);

        VisualElement overrideContainer = new VisualElement();
        overrideContainer.style.marginLeft = 10f;
        root.Add(overrideContainer);

        RefreshBindingUi(property.serializedObject,
                         moduleIdProperty,
                         stageProperty,
                         useOverrideProperty,
                         overridePayloadProperty,
                         modulePopup,
                         moduleKindInfoBox,
                         overrideContainer);

        modulePopup.RegisterValueChangedCallback(evt =>
        {
            if (string.Equals(moduleIdProperty.stringValue, evt.newValue, System.StringComparison.Ordinal))
                return;

            moduleIdProperty.serializedObject.Update();
            moduleIdProperty.stringValue = evt.newValue;
            moduleIdProperty.serializedObject.ApplyModifiedProperties();
            RefreshBindingUi(property.serializedObject,
                             moduleIdProperty,
                             stageProperty,
                             useOverrideProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleKindInfoBox,
                             overrideContainer);
        });

        root.TrackPropertyValue(moduleIdProperty, changedProperty =>
        {
            RefreshBindingUi(property.serializedObject,
                             changedProperty,
                             stageProperty,
                             useOverrideProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleKindInfoBox,
                             overrideContainer);
        });

        root.TrackPropertyValue(useOverrideProperty, changedProperty =>
        {
            RefreshBindingUi(property.serializedObject,
                             moduleIdProperty,
                             stageProperty,
                             changedProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleKindInfoBox,
                             overrideContainer);
        });

        return root;
    }

    private static void RefreshBindingUi(SerializedObject serializedObject,
                                         SerializedProperty moduleIdProperty,
                                         SerializedProperty stageProperty,
                                         SerializedProperty useOverrideProperty,
                                         SerializedProperty overridePayloadProperty,
                                         PopupField<string> modulePopup,
                                         HelpBox moduleKindInfoBox,
                                         VisualElement overrideContainer)
    {
        string moduleId = moduleIdProperty != null ? moduleIdProperty.stringValue : string.Empty;
        List<string> options = BuildModuleIdOptions(serializedObject);

        if (options.Count == 0)
            options.Add(string.Empty);

        string resolvedModuleId = ResolveInitialModuleId(moduleId, options);

        if (modulePopup.value != resolvedModuleId)
            modulePopup.SetValueWithoutNotify(resolvedModuleId);

        if (moduleIdProperty != null && moduleIdProperty.stringValue != resolvedModuleId)
        {
            moduleIdProperty.serializedObject.Update();
            moduleIdProperty.stringValue = resolvedModuleId;
            moduleIdProperty.serializedObject.ApplyModifiedProperties();
        }

        PowerUpModuleKind moduleKind;
        PowerUpModuleStage moduleDefaultStage;
        string moduleDisplayName;
        bool moduleResolved = TryResolveModuleInfo(serializedObject, resolvedModuleId, out moduleKind, out moduleDefaultStage, out moduleDisplayName);
        PowerUpModuleStage bindingStage = moduleResolved ? moduleDefaultStage : ResolveStage(stageProperty);

        if (stageProperty != null && stageProperty.propertyType == SerializedPropertyType.Enum && stageProperty.enumValueIndex != (int)bindingStage)
        {
            stageProperty.serializedObject.Update();
            stageProperty.enumValueIndex = (int)bindingStage;
            stageProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        UpdateModuleInfoBox(moduleKindInfoBox, moduleResolved, moduleKind, moduleDisplayName);
        RebuildOverrideContainer(overrideContainer, useOverrideProperty, overridePayloadProperty, moduleResolved, moduleKind);
    }

    private static void UpdateModuleInfoBox(HelpBox infoBox, bool moduleResolved, PowerUpModuleKind moduleKind, string moduleDisplayName)
    {
        if (infoBox == null)
            return;

        if (moduleResolved == false)
        {
            infoBox.text = "Selected module could not be resolved from Modules Management.";
            infoBox.messageType = HelpBoxMessageType.Warning;
            return;
        }

        infoBox.text = string.Format("Selected module: {0} | Kind: {1}\n{2}",
                                     moduleDisplayName,
                                     moduleKind,
                                     PowerUpModuleEnumDescriptions.GetModuleKindDescription(moduleKind));
        infoBox.messageType = HelpBoxMessageType.Info;
    }

    private static void RebuildOverrideContainer(VisualElement overrideContainer,
                                                 SerializedProperty useOverrideProperty,
                                                 SerializedProperty overridePayloadProperty,
                                                 bool moduleResolved,
                                                 PowerUpModuleKind moduleKind)
    {
        if (overrideContainer == null)
            return;

        bool showOverride = useOverrideProperty != null && useOverrideProperty.boolValue;
        overrideContainer.style.display = showOverride ? DisplayStyle.Flex : DisplayStyle.None;
        overrideContainer.Clear();

        if (showOverride == false)
            return;

        if (moduleResolved == false)
        {
            HelpBox moduleMissingBox = new HelpBox("Select a valid module to configure override payload.", HelpBoxMessageType.Warning);
            overrideContainer.Add(moduleMissingBox);
            return;
        }

        string relativePath;
        string payloadLabel;
        bool hasPayload = PowerUpModuleEnumDescriptions.TryGetPayloadProperty(moduleKind, out relativePath, out payloadLabel);

        if (hasPayload == false)
        {
            HelpBox noPayloadBox = new HelpBox("Selected module kind does not use payload data.", HelpBoxMessageType.Info);
            overrideContainer.Add(noPayloadBox);
            return;
        }

        if (overridePayloadProperty == null)
        {
            HelpBox missingOverrideBox = new HelpBox("Override payload storage is missing.", HelpBoxMessageType.Warning);
            overrideContainer.Add(missingOverrideBox);
            return;
        }

        SerializedProperty payloadProperty = overridePayloadProperty.FindPropertyRelative(relativePath);

        if (payloadProperty == null)
        {
            HelpBox missingPayloadBox = new HelpBox("Override payload property is missing for selected module kind.", HelpBoxMessageType.Warning);
            overrideContainer.Add(missingPayloadBox);
            return;
        }

        PowerUpModuleDefinitionPropertyDrawer.BuildPayloadEditor(overrideContainer, payloadProperty, moduleKind, payloadLabel);
    }

    private static bool TryResolveModuleInfo(SerializedObject serializedObject,
                                             string moduleId,
                                             out PowerUpModuleKind moduleKind,
                                             out PowerUpModuleStage defaultStage,
                                             out string displayName)
    {
        moduleKind = default;
        defaultStage = default;
        displayName = string.Empty;

        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        SerializedProperty modulesProperty = serializedObject.FindProperty("moduleDefinitions");

        if (modulesProperty == null)
            return false;

        for (int index = 0; index < modulesProperty.arraySize; index++)
        {
            SerializedProperty moduleElement = modulesProperty.GetArrayElementAtIndex(index);

            if (moduleElement == null)
                continue;

            SerializedProperty moduleIdProperty = moduleElement.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            if (string.Equals(moduleIdProperty.stringValue, moduleId, System.StringComparison.OrdinalIgnoreCase) == false)
                continue;

            SerializedProperty moduleKindProperty = moduleElement.FindPropertyRelative("moduleKind");
            SerializedProperty displayNameProperty = moduleElement.FindPropertyRelative("displayName");
            moduleKind = ResolveModuleKindFromEnumProperty(moduleKindProperty);
            defaultStage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);
            displayName = displayNameProperty != null && string.IsNullOrWhiteSpace(displayNameProperty.stringValue) == false
                ? displayNameProperty.stringValue
                : moduleId;
            return true;
        }

        return false;
    }

    private static void AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return;

        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        parent.Add(field);
    }

    private static void ApplyFieldAlignedBoxStyle(VisualElement boxElement)
    {
        if (boxElement == null)
            return;

        float leftMargin = EditorGUIUtility.labelWidth + 4f;

        if (leftMargin < 130f)
            leftMargin = 130f;

        boxElement.style.marginLeft = leftMargin;
        boxElement.style.marginRight = 2f;
    }

    private static string ResolveInitialModuleId(string currentId, List<string> options)
    {
        if (options == null || options.Count == 0)
            return string.Empty;

        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], currentId, System.StringComparison.OrdinalIgnoreCase))
                return options[index];
        }

        return options[0];
    }

    private static List<string> BuildModuleIdOptions(SerializedObject serializedObject)
    {
        List<string> options = new List<string>();

        if (serializedObject == null)
            return options;

        SerializedProperty modulesProperty = serializedObject.FindProperty("moduleDefinitions");

        if (modulesProperty == null)
            return options;

        for (int index = 0; index < modulesProperty.arraySize; index++)
        {
            SerializedProperty moduleElement = modulesProperty.GetArrayElementAtIndex(index);

            if (moduleElement == null)
                continue;

            SerializedProperty moduleIdProperty = moduleElement.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            string moduleId = moduleIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            if (ContainsOption(options, moduleId))
                continue;

            options.Add(moduleId);
        }

        return options;
    }

    private static bool ContainsOption(List<string> options, string value)
    {
        if (options == null)
            return false;

        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], value, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static PowerUpModuleKind ResolveModuleKindFromEnumProperty(SerializedProperty moduleKindProperty)
    {
        IReadOnlyList<PowerUpModuleKind> options = PowerUpModuleEnumDescriptions.ModuleKindOptions;

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return options.Count > 0 ? options[0] : default;

        int enumValue = moduleKindProperty.enumValueIndex;

        for (int index = 0; index < options.Count; index++)
        {
            if ((int)options[index] != enumValue)
                continue;

            return options[index];
        }

        return options.Count > 0 ? options[0] : default;
    }

    private static PowerUpModuleStage ResolveStage(SerializedProperty stageProperty)
    {
        IReadOnlyList<PowerUpModuleStage> options = PowerUpModuleEnumDescriptions.StageOptions;

        if (stageProperty == null || stageProperty.propertyType != SerializedPropertyType.Enum)
            return options.Count > 0 ? options[0] : default;

        int enumValue = stageProperty.enumValueIndex;

        for (int index = 0; index < options.Count; index++)
        {
            if ((int)options[index] != enumValue)
                continue;

            return options[index];
        }

        return options.Count > 0 ? options[0] : default;
    }
    #endregion
}
