using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Draws dropped power-up container settings with interaction-mode specific controls and binding pickers.
/// /params none.
/// /returns none.
/// </summary>
[CustomPropertyDrawer(typeof(PlayerPowerUpContainerInteractionSettings))]
public sealed class PlayerPowerUpContainerInteractionSettingsPropertyDrawer : PropertyDrawer
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the UI Toolkit inspector for dropped power-up container settings.
    /// /params property: Serialized settings property shown in the Player Management Tool.
    /// /returns Root visual element used by the custom drawer.
    /// </summary>
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        Foldout containerFoldout = CreateContainerFoldout();
        SerializedProperty containerPrefabProperty = property.FindPropertyRelative("containerPrefab");
        SerializedProperty interactionRadiusProperty = property.FindPropertyRelative("interactionRadius");
        SerializedProperty interactionModeProperty = property.FindPropertyRelative("interactionMode");
        SerializedProperty storedStateModeProperty = property.FindPropertyRelative("storedStateMode");
        SerializedProperty overlayResumeDurationProperty = property.FindPropertyRelative("overlayPanelTimeScaleResumeDurationSeconds");
        SerializedProperty interactActionIdProperty = property.FindPropertyRelative("interactActionId");
        SerializedProperty replacePrimaryActionIdProperty = property.FindPropertyRelative("replacePrimaryActionId");
        SerializedProperty replaceSecondaryActionIdProperty = property.FindPropertyRelative("replaceSecondaryActionId");

        if (containerPrefabProperty == null ||
            interactionRadiusProperty == null ||
            interactionModeProperty == null ||
            storedStateModeProperty == null ||
            overlayResumeDurationProperty == null ||
            interactActionIdProperty == null ||
            replacePrimaryActionIdProperty == null ||
            replaceSecondaryActionIdProperty == null)
        {
            HelpBox missingHelpBox = new HelpBox("Power-up container settings fields are missing.", HelpBoxMessageType.Warning);
            root.Add(missingHelpBox);
            return root;
        }

        InputActionAsset inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        PropertyField containerPrefabField = CreateBoundPropertyField(containerPrefabProperty, "Power-up Container");
        PropertyField interactionRadiusField = CreateBoundPropertyField(interactionRadiusProperty, "Interaction Radius");
        PropertyField interactionModeField = CreateBoundPropertyField(interactionModeProperty, "Interaction Mode");
        PropertyField storedStateModeField = CreateBoundPropertyField(storedStateModeProperty, "Stored State");
        VisualElement overlayFieldsRoot = CreateOverlayFields(property.serializedObject,
                                                              inputAsset,
                                                              overlayResumeDurationProperty,
                                                              interactActionIdProperty);
        VisualElement promptFieldsRoot = CreatePromptFields(property.serializedObject,
                                                            inputAsset,
                                                            replacePrimaryActionIdProperty,
                                                            replaceSecondaryActionIdProperty);

        containerFoldout.Add(containerPrefabField);
        containerFoldout.Add(interactionRadiusField);
        containerFoldout.Add(interactionModeField);
        containerFoldout.Add(storedStateModeField);
        containerFoldout.Add(overlayFieldsRoot);
        containerFoldout.Add(promptFieldsRoot);
        root.Add(containerFoldout);

        interactionModeField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshModeVisibility();
        });

        RefreshModeVisibility();
        return root;

        void RefreshModeVisibility()
        {
            PlayerPowerUpContainerInteractionMode interactionMode = (PlayerPowerUpContainerInteractionMode)interactionModeProperty.enumValueIndex;
            overlayFieldsRoot.style.display = interactionMode == PlayerPowerUpContainerInteractionMode.OverlayPanel
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            promptFieldsRoot.style.display = interactionMode == PlayerPowerUpContainerInteractionMode.Prompt3D
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates one standard property field already bound to the target serialized property.
    /// /params property: Serialized property backing the field.
    /// /params label: Label shown in the tool.
    /// /returns Bound property field.
    /// </summary>
    private static PropertyField CreateBoundPropertyField(SerializedProperty property, string label)
    {
        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
        });
        return field;
    }

    /// <summary>
    /// Creates the main foldout that groups every dropped-container setting under one compact entry point.
    /// /params none.
    /// /returns Foldout used as the root of the settings drawer.
    /// </summary>
    private static Foldout CreateContainerFoldout()
    {
        Foldout foldout = new Foldout();
        foldout.text = "Container";
        foldout.value = true;
        foldout.style.marginTop = 2f;
        foldout.style.marginBottom = 2f;
        return foldout;
    }

    /// <summary>
    /// Builds the field group shown only in Overlay Panel mode.
    /// /params serializedObject: Serialized object that owns the target preset.
    /// /params inputAsset: Input asset used by the binding picker.
    /// /params overlayResumeDurationProperty: Resume-duration property shown for overlay mode.
    /// /params interactActionIdProperty: Input binding property used to open the overlay.
    /// /returns Visual element containing the overlay-only controls.
    /// </summary>
    private static VisualElement CreateOverlayFields(SerializedObject serializedObject,
                                                     InputActionAsset inputAsset,
                                                     SerializedProperty overlayResumeDurationProperty,
                                                     SerializedProperty interactActionIdProperty)
    {
        VisualElement root = CreateModeSectionRoot("Overlay Panel");
        root.Add(CreateBoundPropertyField(overlayResumeDurationProperty, "Resume Time Scale In"));
        root.Add(CreateBindingPicker(inputAsset,
                                     serializedObject,
                                     interactActionIdProperty,
                                     "Overlay Interact Binding",
                                     "Binding used to open the overlay panel while the player is close enough to a dropped power-up container."));
        return root;
    }

    /// <summary>
    /// Builds the field group shown only in 3D Prompt mode.
    /// /params serializedObject: Serialized object that owns the target preset.
    /// /params inputAsset: Input asset used by the binding pickers.
    /// /params replacePrimaryActionIdProperty: Binding property used to replace the primary active slot.
    /// /params replaceSecondaryActionIdProperty: Binding property used to replace the secondary active slot.
    /// /returns Visual element containing the prompt-only controls.
    /// </summary>
    private static VisualElement CreatePromptFields(SerializedObject serializedObject,
                                                    InputActionAsset inputAsset,
                                                    SerializedProperty replacePrimaryActionIdProperty,
                                                    SerializedProperty replaceSecondaryActionIdProperty)
    {
        VisualElement root = CreateModeSectionRoot("3D Prompt");
        root.Add(CreateBindingPicker(inputAsset,
                                     serializedObject,
                                     replacePrimaryActionIdProperty,
                                     "Replace Slot 1 Binding",
                                     "Binding shown in world space and used to swap the dropped power-up with the primary active slot."));
        root.Add(CreateBindingPicker(inputAsset,
                                     serializedObject,
                                     replaceSecondaryActionIdProperty,
                                     "Replace Slot 2 Binding",
                                     "Binding shown in world space and used to swap the dropped power-up with the secondary active slot."));
        return root;
    }

    /// <summary>
    /// Creates a compact labeled section root used by interaction-mode specific controls.
    /// /params title: Section title shown above the grouped controls.
    /// /returns Visual element used as section root.
    /// </summary>
    private static VisualElement CreateModeSectionRoot(string title)
    {
        VisualElement root = new VisualElement();
        root.style.marginTop = 4f;
        root.style.marginBottom = 4f;

        Label titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 2f;
        root.Add(titleLabel);
        return root;
    }

    /// <summary>
    /// Creates one labeled input binding picker with the same filtering UI used elsewhere in the Player Management Tool.
    /// /params inputAsset: Input asset used to enumerate available actions.
    /// /params serializedObject: Serialized object that owns the target property.
    /// /params actionIdProperty: Property storing the selected action id or name.
    /// /params label: Descriptive label shown above the picker.
    /// /params tooltip: Tooltip shown on the descriptive label.
    /// /returns Visual element containing the labeled picker or a warning when no asset is available.
    /// </summary>
    private static VisualElement CreateBindingPicker(InputActionAsset inputAsset,
                                                     SerializedObject serializedObject,
                                                     SerializedProperty actionIdProperty,
                                                     string label,
                                                     string tooltip)
    {
        VisualElement root = new VisualElement();
        root.style.marginTop = 2f;
        root.style.marginBottom = 2f;

        Label headerLabel = new Label(label);
        headerLabel.tooltip = tooltip;
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        root.Add(headerLabel);

        if (inputAsset == null)
        {
            HelpBox warningBox = new HelpBox("Input Action asset not available. Open the Controller section once to generate the default input asset.", HelpBoxMessageType.Warning);
            root.Add(warningBox);
            return root;
        }

        InputActionSelectionElement selectionElement = new InputActionSelectionElement(inputAsset,
                                                                                       serializedObject,
                                                                                       actionIdProperty,
                                                                                       InputActionSelectionElement.SelectionMode.PowerUpContainers);
        root.Add(selectionElement);
        return root;
    }
    #endregion

    #endregion
}
