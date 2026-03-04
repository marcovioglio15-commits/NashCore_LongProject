using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Custom UI Toolkit drawer for EnemyPatternModuleBinding.
/// </summary>
[CustomPropertyDrawer(typeof(EnemyPatternModuleBinding))]
public sealed class EnemyPatternModuleBindingPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the binding editor UI with module resolution and optional override payload.
    /// </summary>
    /// <param name="property">Serialized module binding property.</param>
    /// <returns>Returns the built root visual element.</returns>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty moduleIdProperty = property.FindPropertyRelative("moduleId");
        SerializedProperty enabledProperty = property.FindPropertyRelative("isEnabled");
        SerializedProperty useOverridePayloadProperty = property.FindPropertyRelative("useOverridePayload");
        SerializedProperty overridePayloadProperty = property.FindPropertyRelative("overridePayload");

        if (moduleIdProperty == null ||
            enabledProperty == null ||
            useOverridePayloadProperty == null ||
            overridePayloadProperty == null)
        {
            Label errorLabel = new Label("EnemyPatternModuleBinding serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        List<string> moduleOptions = EnemyAdvancedPatternDrawerUtility.BuildModuleIdOptions(property.serializedObject);

        if (moduleOptions.Count == 0)
            moduleOptions.Add(string.Empty);

        string selectedModuleId = EnemyAdvancedPatternDrawerUtility.ResolveInitialModuleId(moduleIdProperty.stringValue, moduleOptions);
        PopupField<string> modulePopup = new PopupField<string>("Module ID", moduleOptions, selectedModuleId);
        modulePopup.tooltip = "Module reference from Modules Definition catalog.";
        root.Add(modulePopup);

        HelpBox moduleInfoBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        moduleInfoBox.style.marginTop = 2f;
        moduleInfoBox.style.marginLeft = 126f;
        root.Add(moduleInfoBox);

        EnemyAdvancedPatternDrawerUtility.AddField(root, enabledProperty, "Enabled");
        EnemyAdvancedPatternDrawerUtility.AddField(root, useOverridePayloadProperty, "Use Override Payload");

        Label payloadHeader = new Label("Override Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        payloadHeader.style.marginLeft = 126f;
        root.Add(payloadHeader);

        VisualElement overridePayloadContainer = new VisualElement();
        overridePayloadContainer.style.marginLeft = 126f;
        root.Add(overridePayloadContainer);

        RefreshBindingUi(property.serializedObject,
                         moduleIdProperty,
                         useOverridePayloadProperty,
                         overridePayloadProperty,
                         modulePopup,
                         moduleInfoBox,
                         overridePayloadContainer);

        modulePopup.RegisterValueChangedCallback(evt =>
        {
            string currentModuleId = moduleIdProperty.stringValue;

            if (string.Equals(currentModuleId, evt.newValue, StringComparison.Ordinal))
                return;

            moduleIdProperty.serializedObject.Update();
            moduleIdProperty.stringValue = evt.newValue;
            moduleIdProperty.serializedObject.ApplyModifiedProperties();

            RefreshBindingUi(property.serializedObject,
                             moduleIdProperty,
                             useOverridePayloadProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleInfoBox,
                             overridePayloadContainer);
        });

        root.TrackPropertyValue(moduleIdProperty, changedProperty =>
        {
            RefreshBindingUi(property.serializedObject,
                             changedProperty,
                             useOverridePayloadProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleInfoBox,
                             overridePayloadContainer);
        });

        root.TrackPropertyValue(useOverridePayloadProperty, changedProperty =>
        {
            RefreshBindingUi(property.serializedObject,
                             moduleIdProperty,
                             changedProperty,
                             overridePayloadProperty,
                             modulePopup,
                             moduleInfoBox,
                             overridePayloadContainer);
        });

        return root;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Refreshes binding UI when module selection or override state changes.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing moduleDefinitions.</param>
    /// <param name="moduleIdProperty">Binding module ID property.</param>
    /// <param name="useOverridePayloadProperty">Use override toggle property.</param>
    /// <param name="overridePayloadProperty">Override payload property.</param>
    /// <param name="modulePopup">Module popup UI control.</param>
    /// <param name="moduleInfoBox">Module info help box.</param>
    /// <param name="overridePayloadContainer">Override payload host container.</param>
    private static void RefreshBindingUi(SerializedObject serializedObject,
                                         SerializedProperty moduleIdProperty,
                                         SerializedProperty useOverridePayloadProperty,
                                         SerializedProperty overridePayloadProperty,
                                         PopupField<string> modulePopup,
                                         HelpBox moduleInfoBox,
                                         VisualElement overridePayloadContainer)
    {
        List<string> moduleOptions = EnemyAdvancedPatternDrawerUtility.BuildModuleIdOptions(serializedObject);

        if (moduleOptions.Count == 0)
            moduleOptions.Add(string.Empty);

        modulePopup.choices = moduleOptions;

        string resolvedModuleId = EnemyAdvancedPatternDrawerUtility.ResolveInitialModuleId(moduleIdProperty.stringValue, moduleOptions);

        if (string.Equals(modulePopup.value, resolvedModuleId, StringComparison.Ordinal) == false)
            modulePopup.SetValueWithoutNotify(resolvedModuleId);

        if (string.Equals(moduleIdProperty.stringValue, resolvedModuleId, StringComparison.Ordinal) == false)
        {
            moduleIdProperty.serializedObject.Update();
            moduleIdProperty.stringValue = resolvedModuleId;
            moduleIdProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EnemyPatternModuleKind moduleKind;
        string displayName;
        bool moduleResolved = EnemyAdvancedPatternDrawerUtility.TryResolveModuleInfo(serializedObject,
                                                                                     resolvedModuleId,
                                                                                     out moduleKind,
                                                                                     out displayName);

        UpdateInfoBox(moduleInfoBox, moduleResolved, moduleKind, displayName);
        RebuildOverridePayload(moduleResolved,
                               moduleKind,
                               useOverridePayloadProperty,
                               overridePayloadProperty,
                               overridePayloadContainer);
    }

    /// <summary>
    /// Updates module info help box from resolution state.
    /// </summary>
    /// <param name="moduleInfoBox">Target info box.</param>
    /// <param name="moduleResolved">Module resolution result.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <param name="displayName">Resolved display name.</param>
    private static void UpdateInfoBox(HelpBox moduleInfoBox,
                                      bool moduleResolved,
                                      EnemyPatternModuleKind moduleKind,
                                      string displayName)
    {
        if (moduleInfoBox == null)
            return;

        if (moduleResolved == false)
        {
            moduleInfoBox.text = "Selected module could not be resolved from Modules Definition.";
            moduleInfoBox.messageType = HelpBoxMessageType.Warning;
            return;
        }

        moduleInfoBox.text = string.Format("Selected module: {0} | Kind: {1}", displayName, moduleKind);
        moduleInfoBox.messageType = HelpBoxMessageType.Info;
    }

    /// <summary>
    /// Rebuilds override payload block based on current toggle and resolved module kind.
    /// </summary>
    /// <param name="moduleResolved">Whether the selected module can be resolved.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <param name="useOverridePayloadProperty">Use override toggle property.</param>
    /// <param name="overridePayloadProperty">Override payload property.</param>
    /// <param name="overridePayloadContainer">Container to rebuild.</param>
    private static void RebuildOverridePayload(bool moduleResolved,
                                               EnemyPatternModuleKind moduleKind,
                                               SerializedProperty useOverridePayloadProperty,
                                               SerializedProperty overridePayloadProperty,
                                               VisualElement overridePayloadContainer)
    {
        if (overridePayloadContainer == null)
            return;

        overridePayloadContainer.Clear();

        bool showOverride = useOverridePayloadProperty != null && useOverridePayloadProperty.boolValue;
        overridePayloadContainer.style.display = showOverride ? DisplayStyle.Flex : DisplayStyle.None;

        if (showOverride == false)
            return;

        if (moduleResolved == false)
        {
            HelpBox selectModuleBox = new HelpBox("Select a valid module to edit override payload.", HelpBoxMessageType.Warning);
            overridePayloadContainer.Add(selectModuleBox);
            return;
        }

        EnemyAdvancedPatternDrawerUtility.RefreshPayloadEditor(overridePayloadProperty, moduleKind, overridePayloadContainer);
    }
    #endregion

    #endregion
}
