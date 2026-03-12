using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws the root editor UI for module definitions and delegates payload-specific UI to focused utility classes.
/// </summary>
[CustomPropertyDrawer(typeof(PowerUpModuleDefinition))]
public sealed class PowerUpModuleDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float InfoIndent = 126f;
    #endregion

    #region Methods
    /// <summary>
    /// Builds the inspector UI for a module definition entry.
    /// /params property Serialized module definition property.
    /// /returns Root visual element for the inspector drawer.
    /// </summary>
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

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(root, moduleIdProperty, "Module ID");
        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(root, displayNameProperty, "Display Name");

        List<PowerUpModuleKind> moduleKindOptions = BuildModuleKindOptions();
        PowerUpModuleKind currentModuleKind = ResolveModuleKind(moduleKindProperty);
        PopupField<PowerUpModuleKind> moduleKindPopup = new PopupField<PowerUpModuleKind>("Module Kind", moduleKindOptions, currentModuleKind);
        moduleKindPopup.formatListItemCallback = PowerUpModuleEnumDescriptions.FormatModuleKindOption;
        moduleKindPopup.formatSelectedValueCallback = moduleKind =>
        {
            return moduleKind.ToString();
        };
        moduleKindPopup.tooltip = "Determines runtime behavior and payload schema. Changing this value also changes which payload fields are used by bindings.";
        root.Add(moduleKindPopup);

        HelpBox moduleKindInfoBox = new HelpBox(PowerUpModuleEnumDescriptions.GetModuleKindDescription(currentModuleKind), HelpBoxMessageType.Info);
        moduleKindInfoBox.style.marginTop = 2f;
        moduleKindInfoBox.style.marginLeft = InfoIndent;
        root.Add(moduleKindInfoBox);

        PowerUpModuleDefinitionPayloadDrawerUtility.AddField(root, notesProperty, "Notes");

        Label payloadHeader = new Label("Module Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        payloadHeader.style.marginLeft = InfoIndent;
        root.Add(payloadHeader);

        VisualElement payloadContainer = new VisualElement();
        payloadContainer.style.marginLeft = InfoIndent;
        root.Add(payloadContainer);

        RefreshModuleUi(moduleKindProperty,
                        defaultStageProperty,
                        dataProperty,
                        moduleKindPopup,
                        moduleKindInfoBox,
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
                            moduleKindInfoBox,
                            payloadContainer);
        });

        root.TrackPropertyValue(moduleKindProperty, changedProperty =>
        {
            RefreshModuleUi(changedProperty,
                            defaultStageProperty,
                            dataProperty,
                            moduleKindPopup,
                            moduleKindInfoBox,
                            payloadContainer);
        });

        return root;
    }

    /// <summary>
    /// Synchronizes module kind, stage, info box and payload UI whenever the selected kind changes.
    /// /params moduleKindProperty Serialized module kind property.
    /// /params stageProperty Serialized stage property updated to the recommended stage.
    /// /params dataProperty Serialized payload container property.
    /// /params moduleKindPopup Popup used for module kind selection.
    /// /params moduleKindInfoBox Help box showing the selected kind description.
    /// /params payloadContainer Visual container hosting payload fields.
    /// /returns void
    /// </summary>
    private static void RefreshModuleUi(SerializedProperty moduleKindProperty,
                                        SerializedProperty stageProperty,
                                        SerializedProperty dataProperty,
                                        PopupField<PowerUpModuleKind> moduleKindPopup,
                                        HelpBox moduleKindInfoBox,
                                        VisualElement payloadContainer)
    {
        PowerUpModuleKind moduleKind = ResolveModuleKind(moduleKindProperty);
        PowerUpModuleStage stage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);

        if (stageProperty != null &&
            stageProperty.propertyType == SerializedPropertyType.Enum &&
            stageProperty.enumValueIndex != (int)stage)
        {
            stageProperty.serializedObject.Update();
            stageProperty.enumValueIndex = (int)stage;
            stageProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        if (!EqualityComparer<PowerUpModuleKind>.Default.Equals(moduleKindPopup.value, moduleKind))
            moduleKindPopup.SetValueWithoutNotify(moduleKind);

        moduleKindInfoBox.text = PowerUpModuleEnumDescriptions.GetModuleKindDescription(moduleKind);
        RebuildPayloadContainer(payloadContainer, dataProperty, moduleKind);
    }

    /// <summary>
    /// Rebuilds the payload area according to the currently selected module kind.
    /// /params payloadContainer Container that hosts the payload UI.
    /// /params dataProperty Serialized payload root property.
    /// /params moduleKind Selected module kind.
    /// /returns void
    /// </summary>
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

        if (!hasPayload)
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

        BuildPayloadEditor(payloadContainer, payloadProperty, moduleKind, payloadLabel);
    }

    /// <summary>
    /// Provides the shared payload entry point used by module and binding drawers.
    /// /params payloadContainer Container that will receive the payload UI.
    /// /params payloadProperty Serialized payload property for the selected kind.
    /// /params moduleKind Kind that selects the payload drawer variant.
    /// /params payloadLabel Optional label used by the generic fallback drawer.
    /// /returns void
    /// </summary>
    public static void BuildPayloadEditor(VisualElement payloadContainer,
                                          SerializedProperty payloadProperty,
                                          PowerUpModuleKind moduleKind,
                                          string payloadLabel)
    {
        PowerUpModuleDefinitionPayloadDrawerUtility.BuildPayloadEditor(payloadContainer,
                                                                      payloadProperty,
                                                                      moduleKind,
                                                                      payloadLabel);
    }

    /// <summary>
    /// Builds the popup options list for module kind selection.
    /// /params none
    /// /returns Materialized module kind list used by the popup field.
    /// </summary>
    private static List<PowerUpModuleKind> BuildModuleKindOptions()
    {
        List<PowerUpModuleKind> options = new List<PowerUpModuleKind>();
        IReadOnlyList<PowerUpModuleKind> moduleKindOptions = PowerUpModuleEnumDescriptions.ModuleKindOptions;

        for (int index = 0; index < moduleKindOptions.Count; index++)
            options.Add(moduleKindOptions[index]);

        return options;
    }

    /// <summary>
    /// Resolves the serialized enum value to a valid module kind option.
    /// /params moduleKindProperty Serialized module kind enum property.
    /// /returns Valid module kind, or the first configured option when the property is invalid.
    /// </summary>
    private static PowerUpModuleKind ResolveModuleKind(SerializedProperty moduleKindProperty)
    {
        IReadOnlyList<PowerUpModuleKind> options = PowerUpModuleEnumDescriptions.ModuleKindOptions;

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
        {
            if (options.Count > 0)
                return options[0];

            return default;
        }

        int enumValue = moduleKindProperty.enumValueIndex;

        for (int index = 0; index < options.Count; index++)
        {
            if ((int)options[index] != enumValue)
                continue;

            return options[index];
        }

        if (options.Count > 0)
            return options[0];

        return default;
    }
    #endregion
}
