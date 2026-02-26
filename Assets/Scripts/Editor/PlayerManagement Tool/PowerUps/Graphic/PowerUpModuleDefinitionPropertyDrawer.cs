using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(PowerUpModuleDefinition))]
public sealed class PowerUpModuleDefinitionPropertyDrawer : PropertyDrawer
{
    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty moduleIdProperty = property.FindPropertyRelative("moduleId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty moduleKindProperty = property.FindPropertyRelative("moduleKind");
        SerializedProperty defaultStageProperty = property.FindPropertyRelative("defaultStage");
        SerializedProperty notesProperty = property.FindPropertyRelative("notes");
        SerializedProperty dataProperty = property.FindPropertyRelative("data");

        if (moduleIdProperty == null ||
            displayNameProperty == null ||
            moduleKindProperty == null ||
            defaultStageProperty == null ||
            notesProperty == null ||
            dataProperty == null)
        {
            Label errorLabel = new Label("PowerUpModuleDefinition serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        AddField(root, moduleIdProperty, "Module ID");
        AddField(root, displayNameProperty, "Display Name");

        List<PowerUpModuleKind> moduleKindOptions = BuildModuleKindOptions();
        PowerUpModuleKind currentModuleKind = ResolveModuleKind(moduleKindProperty);
        PopupField<PowerUpModuleKind> moduleKindPopup = new PopupField<PowerUpModuleKind>("Module Kind", moduleKindOptions, currentModuleKind);
        moduleKindPopup.tooltip = "Behavior implemented by this module.";
        root.Add(moduleKindPopup);

        HelpBox moduleKindInfoBox = new HelpBox(PowerUpModuleEnumDescriptions.GetModuleKindDescription(currentModuleKind), HelpBoxMessageType.Info);
        moduleKindInfoBox.style.marginTop = 2f;
        root.Add(moduleKindInfoBox);

        List<PowerUpModuleStage> stageOptions = BuildStageOptions();
        PowerUpModuleStage currentStage = ResolveStage(defaultStageProperty);
        PopupField<PowerUpModuleStage> stagePopup = new PopupField<PowerUpModuleStage>("Default Stage", stageOptions, currentStage);
        stagePopup.tooltip = "Default execution stage for this module.";
        root.Add(stagePopup);

        HelpBox stageInfoBox = new HelpBox(PowerUpModuleEnumDescriptions.GetStageDescription(currentStage), HelpBoxMessageType.Info);
        stageInfoBox.style.marginTop = 2f;
        root.Add(stageInfoBox);

        HelpBox stageCoherenceBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        stageCoherenceBox.style.marginTop = 2f;
        root.Add(stageCoherenceBox);

        AddField(root, notesProperty, "Notes");

        Label payloadHeader = new Label("Module Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        root.Add(payloadHeader);

        VisualElement payloadContainer = new VisualElement();
        root.Add(payloadContainer);

        RefreshModuleUi(moduleKindProperty,
                        defaultStageProperty,
                        dataProperty,
                        moduleKindPopup,
                        stagePopup,
                        moduleKindInfoBox,
                        stageInfoBox,
                        stageCoherenceBox,
                        payloadContainer);

        moduleKindPopup.RegisterValueChangedCallback(evt =>
        {
            if ((int)evt.newValue == moduleKindProperty.enumValueIndex)
                return;

            moduleKindProperty.serializedObject.Update();
            moduleKindProperty.enumValueIndex = (int)evt.newValue;
            moduleKindProperty.serializedObject.ApplyModifiedProperties();
            RefreshModuleUi(moduleKindProperty,
                            defaultStageProperty,
                            dataProperty,
                            moduleKindPopup,
                            stagePopup,
                            moduleKindInfoBox,
                            stageInfoBox,
                            stageCoherenceBox,
                            payloadContainer);
        });

        stagePopup.RegisterValueChangedCallback(evt =>
        {
            if ((int)evt.newValue == defaultStageProperty.enumValueIndex)
                return;

            defaultStageProperty.serializedObject.Update();
            defaultStageProperty.enumValueIndex = (int)evt.newValue;
            defaultStageProperty.serializedObject.ApplyModifiedProperties();
            RefreshModuleUi(moduleKindProperty,
                            defaultStageProperty,
                            dataProperty,
                            moduleKindPopup,
                            stagePopup,
                            moduleKindInfoBox,
                            stageInfoBox,
                            stageCoherenceBox,
                            payloadContainer);
        });

        root.TrackPropertyValue(moduleKindProperty, changedProperty =>
        {
            RefreshModuleUi(changedProperty,
                            defaultStageProperty,
                            dataProperty,
                            moduleKindPopup,
                            stagePopup,
                            moduleKindInfoBox,
                            stageInfoBox,
                            stageCoherenceBox,
                            payloadContainer);
        });

        root.TrackPropertyValue(defaultStageProperty, changedProperty =>
        {
            RefreshModuleUi(moduleKindProperty,
                            changedProperty,
                            dataProperty,
                            moduleKindPopup,
                            stagePopup,
                            moduleKindInfoBox,
                            stageInfoBox,
                            stageCoherenceBox,
                            payloadContainer);
        });

        return root;
    }

    private static void RefreshModuleUi(SerializedProperty moduleKindProperty,
                                        SerializedProperty stageProperty,
                                        SerializedProperty dataProperty,
                                        PopupField<PowerUpModuleKind> moduleKindPopup,
                                        PopupField<PowerUpModuleStage> stagePopup,
                                        HelpBox moduleKindInfoBox,
                                        HelpBox stageInfoBox,
                                        HelpBox stageCoherenceBox,
                                        VisualElement payloadContainer)
    {
        PowerUpModuleKind moduleKind = ResolveModuleKind(moduleKindProperty);
        PowerUpModuleStage stage = ResolveStage(stageProperty);

        if (EqualityComparer<PowerUpModuleKind>.Default.Equals(moduleKindPopup.value, moduleKind) == false)
            moduleKindPopup.SetValueWithoutNotify(moduleKind);

        if (EqualityComparer<PowerUpModuleStage>.Default.Equals(stagePopup.value, stage) == false)
            stagePopup.SetValueWithoutNotify(stage);

        moduleKindInfoBox.text = PowerUpModuleEnumDescriptions.GetModuleKindDescription(moduleKind);
        stageInfoBox.text = PowerUpModuleEnumDescriptions.GetStageDescription(stage);
        UpdateStageCoherenceBox(stageCoherenceBox, moduleKind, stage);
        RebuildPayloadContainer(payloadContainer, dataProperty, moduleKind);
    }

    private static void RebuildPayloadContainer(VisualElement payloadContainer, SerializedProperty dataProperty, PowerUpModuleKind moduleKind)
    {
        if (payloadContainer == null)
            return;

        payloadContainer.Clear();

        if (dataProperty == null)
            return;

        string relativePath;
        string payloadLabel;
        bool hasPayload = PowerUpModuleEnumDescriptions.TryGetPayloadProperty(moduleKind, out relativePath, out payloadLabel);

        if (hasPayload == false)
        {
            HelpBox infoBox = new HelpBox("No payload is required for this module kind.", HelpBoxMessageType.Info);
            payloadContainer.Add(infoBox);
            return;
        }

        SerializedProperty payloadProperty = dataProperty.FindPropertyRelative(relativePath);

        if (payloadProperty == null)
        {
            HelpBox warningBox = new HelpBox("Payload property is missing for the selected module kind.", HelpBoxMessageType.Warning);
            payloadContainer.Add(warningBox);
            return;
        }

        PropertyField payloadField = new PropertyField(payloadProperty, payloadLabel);
        payloadField.BindProperty(payloadProperty);
        payloadContainer.Add(payloadField);
    }

    private static void UpdateStageCoherenceBox(HelpBox coherenceBox, PowerUpModuleKind moduleKind, PowerUpModuleStage stage)
    {
        if (coherenceBox == null)
            return;

        bool isCoherent = PowerUpModuleEnumDescriptions.IsStageCoherent(moduleKind, stage);

        if (isCoherent)
        {
            coherenceBox.style.display = DisplayStyle.None;
            coherenceBox.text = string.Empty;
            return;
        }

        PowerUpModuleStage recommendedStage = PowerUpModuleEnumDescriptions.GetRecommendedStage(moduleKind);
        coherenceBox.text = string.Format("Default Stage '{0}' is not coherent with Module Kind '{1}'. Recommended stage: '{2}'.",
                                          stage,
                                          moduleKind,
                                          recommendedStage);
        coherenceBox.style.display = DisplayStyle.Flex;
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

    private static List<PowerUpModuleKind> BuildModuleKindOptions()
    {
        List<PowerUpModuleKind> options = new List<PowerUpModuleKind>();
        IReadOnlyList<PowerUpModuleKind> moduleKindOptions = PowerUpModuleEnumDescriptions.ModuleKindOptions;

        for (int index = 0; index < moduleKindOptions.Count; index++)
            options.Add(moduleKindOptions[index]);

        return options;
    }

    private static List<PowerUpModuleStage> BuildStageOptions()
    {
        List<PowerUpModuleStage> options = new List<PowerUpModuleStage>();
        IReadOnlyList<PowerUpModuleStage> stageOptions = PowerUpModuleEnumDescriptions.StageOptions;

        for (int index = 0; index < stageOptions.Count; index++)
            options.Add(stageOptions[index]);

        return options;
    }

    private static PowerUpModuleKind ResolveModuleKind(SerializedProperty moduleKindProperty)
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
