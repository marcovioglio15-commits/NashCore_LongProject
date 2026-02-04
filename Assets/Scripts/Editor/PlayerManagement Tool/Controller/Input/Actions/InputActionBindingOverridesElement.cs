using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

public sealed class InputActionBindingOverridesElement : VisualElement
{
    #region Nested Types
    public enum QuickBindingMode
    {
        Movement = 0,
        Look = 1
    }

    private sealed class ActionOption
    {
        public string Name;
        public string Id;
        public string MapName;
        public InputActionType ActionType;
        public string ControlType;
    }

    private sealed class OverridePresetOption
    {
        public string Name;
        public string Tooltip;
        public PlayerInputOverridePreset Preset;
    }
    #endregion

    #region Constants
    private const string OverridePathTooltip = "Override path for this binding. Use Input System control paths, e.g. <Keyboard>/w, <Gamepad>/leftStick, <Mouse>/delta.";
    private const string OverridePresetTooltip = "Apply a preset of binding overrides for the selected action.";
    private const string AnyControlTypeOption = "<Any>";
    #endregion

    #region Fields
    private readonly InputActionAsset m_InputAsset;
    private readonly PlayerInputOverridePresetLibrary m_OverridePresetLibrary;
    private readonly SerializedObject m_PresetSerializedObject;
    private readonly SerializedProperty m_ActionIdProperty;
    private readonly SerializedProperty m_OverridesProperty;
    private readonly QuickBindingMode m_QuickBindingMode;
    private InputActionType m_FilterActionType = InputActionType.Value;
    private string m_FilterControlType = "Vector2";
    private bool m_ShowAllActions;

    private readonly List<ActionOption> m_ActionOptions = new List<ActionOption>();
    private readonly List<OverridePresetOption> m_OverridePresetOptions = new List<OverridePresetOption>();
    private readonly List<string> m_ControlTypeOptions = new List<string>();
    private readonly VisualElement m_OverridesContainer;

    private EnumField m_ActionTypeFilter;
    private PopupField<string> m_ControlTypeFilter;
    private Toggle m_ShowAllToggle;
    private VisualElement m_ActionDropdownContainer;
    private PopupField<ActionOption> m_ActionDropdown;
    private Label m_NoActionsLabel;
    private PopupField<OverridePresetOption> m_OverridePresetDropdown;
    private PlayerInputOverridePreset m_SelectedOverridePreset;
    private SerializedObject m_SelectedOverridePresetSerializedObject;
    private SerializedProperty m_SelectedPresetOverridesProperty;
    private VisualElement m_EditPresetContainer;
    private VisualElement m_PresetOverridesContainer;
    private Button m_EditPresetButton;
    private bool m_IsEditMode;
    #endregion

    #region Constructors
    public InputActionBindingOverridesElement(InputActionAsset inputAsset, SerializedObject presetSerializedObject, SerializedProperty actionIdProperty, SerializedProperty overridesProperty, QuickBindingMode quickBindingMode)
    {
        m_InputAsset = inputAsset;
        m_PresetSerializedObject = presetSerializedObject;
        m_ActionIdProperty = actionIdProperty;
        m_OverridesProperty = overridesProperty;
        m_QuickBindingMode = quickBindingMode;
        m_OverridePresetLibrary = PlayerInputOverridePresetLibraryUtility.GetOrCreateLibrary();

        style.marginTop = 4f;
        style.marginBottom = 6f;
        style.flexDirection = FlexDirection.Column;

        PopulateControlTypeOptions();
        BuildActionFilterSection();
        BuildActionDropdownContainer();

        VisualElement presetsSection = BuildOverridePresetSection();
        Add(presetsSection);

        VisualElement quickButtons = BuildQuickButtons();
        Add(quickButtons);

        m_OverridesContainer = new VisualElement();
        m_OverridesContainer.style.marginTop = 6f;
        m_OverridesContainer.style.marginLeft = 8f;
        m_OverridesContainer.style.marginRight = 4f;
        m_OverridesContainer.style.flexDirection = FlexDirection.Column;
        Add(m_OverridesContainer);

        RefreshActionOptions();
        RefreshOverridePresetOptions();
        RebuildOverridesList();
    }
    #endregion

    #region Private Methods
    private void PopulateActionOptions()
    {
        m_ActionOptions.Clear();

        if (m_InputAsset == null)
            return;

        ReadOnlyArray<InputActionMap> maps = m_InputAsset.actionMaps;
        HashSet<string> actionIds = new HashSet<string>();

        for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            InputActionMap map = maps[mapIndex];
            ReadOnlyArray<InputAction> actions = map.actions;

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                InputAction action = actions[actionIndex];
                string actionId = action.id.ToString();

                if (actionIds.Add(actionId) == false)
                    continue;

                if (m_ShowAllActions == false)
                {
                    if (action.type != m_FilterActionType)
                        continue;

                    if (string.IsNullOrWhiteSpace(m_FilterControlType) == false && m_FilterControlType != AnyControlTypeOption)
                    {
                        string expectedControlType = action.expectedControlType;

                        if (string.IsNullOrWhiteSpace(expectedControlType))
                            continue;

                        if (string.Equals(expectedControlType, m_FilterControlType, StringComparison.OrdinalIgnoreCase) == false)
                            continue;
                    }
                }

                ActionOption option = new ActionOption
                {
                    Name = action.name,
                    Id = actionId,
                    MapName = map.name,
                    ActionType = action.type,
                    ControlType = action.expectedControlType
                };

                m_ActionOptions.Add(option);
            }
        }
    }

    private void PopulateControlTypeOptions()
    {
        m_ControlTypeOptions.Clear();
        m_ControlTypeOptions.Add(AnyControlTypeOption);

        if (m_InputAsset == null)
            return;

        HashSet<string> controlTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ReadOnlyArray<InputActionMap> maps = m_InputAsset.actionMaps;

        for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            InputActionMap map = maps[mapIndex];
            ReadOnlyArray<InputAction> actions = map.actions;

            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                InputAction action = actions[actionIndex];
                string expectedControlType = action.expectedControlType;

                if (string.IsNullOrWhiteSpace(expectedControlType))
                    continue;

                if (controlTypes.Add(expectedControlType))
                    m_ControlTypeOptions.Add(expectedControlType);
            }
        }

        if (m_ControlTypeOptions.Contains(m_FilterControlType) == false)
            m_FilterControlType = AnyControlTypeOption;
    }

    private void BuildActionFilterSection()
    {
        VisualElement container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexWrap = Wrap.Wrap;
        container.style.marginBottom = 4f;
        container.style.alignItems = Align.Center;

        m_ActionTypeFilter = new EnumField("Action Type", m_FilterActionType);
        m_ActionTypeFilter.RegisterValueChangedCallback(evt =>
        {
            m_FilterActionType = (InputActionType)evt.newValue;
            RefreshActionOptions();
        });
        container.Add(m_ActionTypeFilter);

        if (m_ControlTypeOptions.Count == 0)
            m_ControlTypeOptions.Add(AnyControlTypeOption);

        m_ControlTypeFilter = new PopupField<string>("Control Type", m_ControlTypeOptions, m_FilterControlType);
        m_ControlTypeFilter.RegisterValueChangedCallback(evt =>
        {
            m_FilterControlType = evt.newValue;
            RefreshActionOptions();
        });
        container.Add(m_ControlTypeFilter);

        m_ShowAllToggle = new Toggle("Show All Actions");
        m_ShowAllToggle.value = m_ShowAllActions;
        m_ShowAllToggle.style.marginLeft = 6f;
        m_ShowAllToggle.RegisterValueChangedCallback(evt =>
        {
            m_ShowAllActions = evt.newValue;
            UpdateFilterFieldsEnabledState();
            RefreshActionOptions();
        });
        container.Add(m_ShowAllToggle);

        Add(container);
        UpdateFilterFieldsEnabledState();
    }

    private void BuildActionDropdownContainer()
    {
        m_ActionDropdownContainer = new VisualElement();
        m_ActionDropdownContainer.style.marginBottom = 6f;
        Add(m_ActionDropdownContainer);
    }

    private void UpdateFilterFieldsEnabledState()
    {
        bool isEnabled = m_ShowAllActions == false;

        if (m_ActionTypeFilter != null)
            m_ActionTypeFilter.SetEnabled(isEnabled);

        if (m_ControlTypeFilter != null)
            m_ControlTypeFilter.SetEnabled(isEnabled);
    }

    private void BuildActionDropdown()
    {
        if (m_ActionDropdownContainer == null)
            return;

        m_ActionDropdownContainer.Clear();

        if (m_ActionDropdown == null)
        {
            m_ActionDropdown = new PopupField<ActionOption>("Action", m_ActionOptions, GetSelectedActionOption(), FormatActionOption, FormatActionOption);
            m_ActionDropdown.RegisterValueChangedCallback(OnActionSelectionChanged);
        }

        if (m_NoActionsLabel == null)
        {
            m_NoActionsLabel = new Label("No input actions found.");
            m_NoActionsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        }

        m_ActionDropdownContainer.Add(m_ActionDropdown);
        m_ActionDropdownContainer.Add(m_NoActionsLabel);
    }

    private void RefreshActionOptions()
    {
        if (m_PresetSerializedObject == null)
            return;

        string selectedActionId = GetSelectedActionId();
        PopulateActionOptions();

        if (m_ActionDropdownContainer == null || m_ActionDropdown == null)
            BuildActionDropdown();

        if (m_ActionOptions.Count == 0)
        {
            if (m_ActionDropdown != null)
                m_ActionDropdown.style.display = DisplayStyle.None;

            if (m_NoActionsLabel != null)
                m_NoActionsLabel.style.display = DisplayStyle.Flex;

            return;
        }

        if (m_ActionDropdown != null)
        {
            m_ActionDropdown.style.display = DisplayStyle.Flex;
            m_ActionDropdown.choices = m_ActionOptions;
        }

        if (m_NoActionsLabel != null)
            m_NoActionsLabel.style.display = DisplayStyle.None;

        ActionOption selectedOption = GetActionOptionById(selectedActionId);

        if (selectedOption == null)
            selectedOption = m_ActionOptions[0];

        if (m_ActionDropdown != null)
            m_ActionDropdown.SetValueWithoutNotify(selectedOption);

        AssignActionId(selectedOption.Id, "Assign Input Action");
        RefreshOverridePresetOptions();
        RebuildOverridesList();
    }

    private VisualElement BuildOverridePresetSection()
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 6f;
        container.style.flexDirection = FlexDirection.Column;

        Label header = new Label("Override Presets");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        container.Add(header);

        if (m_OverridePresetOptions.Count == 0)
        {
            OverridePresetOption noneOption = new OverridePresetOption
            {
                Name = "<None>",
                Tooltip = "Use input asset defaults.",
                Preset = null
            };

            m_OverridePresetOptions.Add(noneOption);
        }

        m_OverridePresetDropdown = new PopupField<OverridePresetOption>("Preset", m_OverridePresetOptions, m_OverridePresetOptions[0], FormatOverridePresetOption, FormatOverridePresetOption);
        m_OverridePresetDropdown.tooltip = OverridePresetTooltip;
        m_OverridePresetDropdown.RegisterValueChangedCallback(evt =>
        {
            m_SelectedOverridePreset = evt.newValue != null ? evt.newValue.Preset : null;
            UpdateOverridePresetTooltip(evt.newValue);
            RefreshEditModeView();
        });
        container.Add(m_OverridePresetDropdown);

        VisualElement presetButtons = new VisualElement();
        presetButtons.style.flexDirection = FlexDirection.Row;
        presetButtons.style.marginTop = 2f;

        Button applyButton = new Button();
        applyButton.text = "Apply";
        applyButton.tooltip = "Apply the selected override preset to this action.";
        applyButton.clicked += ApplySelectedOverridePreset;
        presetButtons.Add(applyButton);

        Button newButton = new Button();
        newButton.text = "New";
        newButton.tooltip = "Create a new override preset from the current overrides.";
        newButton.style.marginLeft = 4f;
        newButton.clicked += CreateOverridePresetFromCurrent;
        presetButtons.Add(newButton);

        Button deleteButton = new Button();
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Delete the selected override preset asset.";
        deleteButton.style.marginLeft = 4f;
        deleteButton.clicked += DeleteSelectedOverridePreset;
        presetButtons.Add(deleteButton);

        m_EditPresetButton = new Button();
        m_EditPresetButton.text = "Edit";
        m_EditPresetButton.tooltip = "Edit the selected override preset in place.";
        m_EditPresetButton.style.marginLeft = 4f;
        m_EditPresetButton.clicked += ToggleEditMode;
        presetButtons.Add(m_EditPresetButton);

        container.Add(presetButtons);

        m_EditPresetContainer = new VisualElement();
        m_EditPresetContainer.style.marginTop = 6f;
        m_EditPresetContainer.style.display = DisplayStyle.None;
        container.Add(m_EditPresetContainer);

        return container;
    }

    private ActionOption GetSelectedActionOption()
    {
        ActionOption option = GetActionOptionById(GetSelectedActionId());

        if (option != null)
            return option;

        if (m_ActionOptions.Count == 0)
            return null;

        return m_ActionOptions[0];
    }

    private ActionOption GetActionOptionById(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            return null;

        for (int i = 0; i < m_ActionOptions.Count; i++)
        {
            ActionOption option = m_ActionOptions[i];

            if (option.Id == actionId)
                return option;
        }

        return null;
    }

    private string FormatActionOption(ActionOption option)
    {
        if (option == null)
            return "<None>";

        string label = string.IsNullOrWhiteSpace(option.MapName) ? option.Name : option.MapName + "/" + option.Name;

        if (string.IsNullOrWhiteSpace(option.ControlType))
            return label;

        return label + " (" + option.ControlType + ")";
    }

    private void OnActionSelectionChanged(ChangeEvent<ActionOption> evt)
    {
        if (evt == null || m_ActionIdProperty == null)
            return;

        if (evt.newValue == null)
            return;

        AssignActionId(evt.newValue.Id, "Change Input Action");

        m_SelectedOverridePreset = null;
        SetEditMode(false);
        RefreshOverridePresetOptions();
        RebuildOverridesList();
    }

    private void EnsureActionIdInitialized()
    {
        if (m_ActionIdProperty == null)
            return;

        if (string.IsNullOrWhiteSpace(m_ActionIdProperty.stringValue) == false)
            return;

        if (m_ActionDropdown == null)
            return;

        ActionOption option = m_ActionDropdown.value;

        if (option == null)
            return;

        AssignActionId(option.Id, "Assign Input Action");

        RefreshOverridePresetOptions();
    }

    private void AssignActionId(string actionId, string undoLabel)
    {
        if (m_ActionIdProperty == null)
            return;

        if (string.IsNullOrWhiteSpace(actionId))
            return;

        if (m_ActionIdProperty.stringValue == actionId)
            return;

        Undo.RecordObject(m_PresetSerializedObject.targetObject, undoLabel);
        m_PresetSerializedObject.Update();
        m_ActionIdProperty.stringValue = actionId;
        m_PresetSerializedObject.ApplyModifiedProperties();
    }

    private void RefreshOverridePresetOptions()
    {
        m_OverridePresetOptions.Clear();

        OverridePresetOption noneOption = new OverridePresetOption
        {
            Name = "<None>",
            Tooltip = "Use input asset defaults.",
            Preset = null
        };

        m_OverridePresetOptions.Add(noneOption);

        string actionId = GetSelectedActionId();

        if (m_OverridePresetLibrary != null && string.IsNullOrWhiteSpace(actionId) == false)
        {
            IReadOnlyList<PlayerInputOverridePreset> presets = m_OverridePresetLibrary.Presets;

            for (int i = 0; i < presets.Count; i++)
            {
                PlayerInputOverridePreset preset = presets[i];

                if (preset == null)
                    continue;

                if (preset.ActionId != actionId)
                    continue;

                OverridePresetOption option = new OverridePresetOption
                {
                    Name = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName,
                    Tooltip = preset.Description,
                    Preset = preset
                };

                m_OverridePresetOptions.Add(option);
            }
        }

        if (m_OverridePresetDropdown == null)
            return;

        m_OverridePresetDropdown.choices = m_OverridePresetOptions;
        OverridePresetOption selectedOption = GetSelectedOverridePresetOption();
        m_OverridePresetDropdown.SetValueWithoutNotify(selectedOption);
        UpdateOverridePresetTooltip(selectedOption);
        m_SelectedOverridePreset = selectedOption != null ? selectedOption.Preset : null;
        RefreshEditModeView();
    }

    private OverridePresetOption GetSelectedOverridePresetOption()
    {
        if (m_OverridePresetOptions.Count == 0)
            return null;

        if (m_SelectedOverridePreset == null)
            return m_OverridePresetOptions[0];

        for (int i = 0; i < m_OverridePresetOptions.Count; i++)
        {
            OverridePresetOption option = m_OverridePresetOptions[i];

            if (option.Preset == m_SelectedOverridePreset)
                return option;
        }

        return m_OverridePresetOptions[0];
    }

    private string FormatOverridePresetOption(OverridePresetOption option)
    {
        if (option == null)
            return "<None>";

        return option.Name;
    }

    private void UpdateOverridePresetTooltip(OverridePresetOption option)
    {
        if (m_OverridePresetDropdown == null)
            return;

        string tooltip = option != null ? option.Tooltip : string.Empty;
        m_OverridePresetDropdown.tooltip = string.IsNullOrWhiteSpace(tooltip) ? OverridePresetTooltip : tooltip;
    }

    private VisualElement BuildQuickButtons()
    {
        VisualElement container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexWrap = Wrap.Wrap;
        container.style.marginTop = 4f;

        Button defaultButton = CreateQuickButton("Default (Asset)", ApplyDefaultBindings);
        defaultButton.tooltip = "Clear overrides for this action and use the input asset defaults.";
        container.Add(defaultButton);

        if (m_QuickBindingMode == QuickBindingMode.Movement)
        {
            Button wasdButton = CreateQuickButton("Preset: WASD", ApplyMoveWASD);
            wasdButton.tooltip = "Override to keyboard WASD.";
            container.Add(wasdButton);

            Button arrowsButton = CreateQuickButton("Preset: Arrows", ApplyMoveArrows);
            arrowsButton.tooltip = "Override to keyboard arrow keys.";
            container.Add(arrowsButton);

            Button gamepadButton = CreateQuickButton("Preset: Gamepad Left Stick", ApplyMoveGamepadStick);
            gamepadButton.tooltip = "Override to gamepad left stick.";
            container.Add(gamepadButton);
        }
        else
        {
            Button mouseButton = CreateQuickButton("Preset: Mouse Delta", ApplyLookMouseDelta);
            mouseButton.tooltip = "Override to mouse delta.";
            container.Add(mouseButton);

            Button gamepadButton = CreateQuickButton("Preset: Gamepad Right Stick", ApplyLookGamepadStick);
            gamepadButton.tooltip = "Override to gamepad right stick.";
            container.Add(gamepadButton);

            Button arrowsButton = CreateQuickButton("Preset: Arrows", ApplyLookArrows);
            arrowsButton.tooltip = "Override to keyboard arrow keys.";
            container.Add(arrowsButton);
        }

        return container;
    }

    private Button CreateQuickButton(string label, Action onClick)
    {
        Button button = new Button();
        button.text = label;
        button.clicked += onClick;
        button.style.marginRight = 4f;
        button.style.marginTop = 2f;
        return button;
    }

    private void RebuildOverridesList()
    {
        if (m_OverridesContainer == null || m_PresetSerializedObject == null)
            return;

        m_OverridesContainer.Clear();
        m_PresetSerializedObject.Update();

        InputAction action = GetSelectedAction();

        if (action == null)
        {
            Label label = new Label("No action selected or action not found.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            m_OverridesContainer.Add(label);
            return;
        }

        VisualElement headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;

        Label header = new Label("Overrides");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerRow.Add(header);

        Button resetAllButton = new Button();
        resetAllButton.text = "Reset All";
        resetAllButton.tooltip = "Clear all overrides for this action.";
        resetAllButton.style.marginLeft = 6f;
        resetAllButton.clicked += ApplyDefaultBindings;
        headerRow.Add(resetAllButton);

        m_OverridesContainer.Add(headerRow);

        IReadOnlyList<InputBinding> bindings = action.bindings;

        if (bindings == null || bindings.Count == 0)
        {
            Label emptyLabel = new Label("No bindings found for this action.");
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            m_OverridesContainer.Add(emptyLabel);
            return;
        }

        string actionId = action.id.ToString();
        bool hasAnyBinding = false;

        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];

            if (binding.isComposite)
                continue;

            hasAnyBinding = true;
            SerializedProperty overrideProperty = FindOverrideProperty(m_OverridesProperty, actionId, binding.id.ToString());
            VisualElement row = CreateOverrideRow(action, binding, overrideProperty);
            m_OverridesContainer.Add(row);
        }

        if (hasAnyBinding)
            return;

        Label noBindingsLabel = new Label("No bindings available to override.");
        noBindingsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        m_OverridesContainer.Add(noBindingsLabel);
    }

    private VisualElement CreateOverrideRow(InputAction action, InputBinding binding, SerializedProperty overrideProperty)
    {
        SerializedProperty pathProperty = overrideProperty != null ? overrideProperty.FindPropertyRelative("m_OverridePath") : null;
        string bindingLabel = GetBindingLabel(binding);
        string defaultPath = string.IsNullOrWhiteSpace(binding.path) ? "-" : binding.path;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 2f;

        Label nameLabel = new Label(bindingLabel);
        nameLabel.style.minWidth = 110f;
        row.Add(nameLabel);

        Label defaultLabel = new Label(defaultPath);
        defaultLabel.style.minWidth = 180f;
        defaultLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        row.Add(defaultLabel);

        TextField overrideField = new TextField();
        overrideField.style.flexGrow = 1f;
        overrideField.tooltip = OverridePathTooltip;
        overrideField.isDelayed = true;
        overrideField.value = pathProperty != null ? pathProperty.stringValue : string.Empty;
        overrideField.RegisterValueChangedCallback(evt =>
        {
            string actionId = action.id.ToString();
            string bindingId = binding.id.ToString();
            string newValue = evt.newValue;

            BeginOverrideEdit("Edit Input Override");

            if (string.IsNullOrWhiteSpace(newValue))
            {
                RemoveOverride(m_OverridesProperty, actionId, bindingId);
                EndOverrideEdit();
                return;
            }

            string interactions = GetOverrideString(overrideProperty, "m_OverrideInteractions");
            string processors = GetOverrideString(overrideProperty, "m_OverrideProcessors");
            SetOverride(m_OverridesProperty, actionId, bindingId, newValue, interactions, processors);
            EndOverrideEdit();
        });

        row.Add(overrideField);

        Button clearButton = new Button();
        clearButton.text = "Reset";
        clearButton.tooltip = "Clear the override for this binding.";
        clearButton.clicked += () =>
        {
            BeginOverrideEdit("Reset Input Override");
            RemoveOverride(m_OverridesProperty, action.id.ToString(), binding.id.ToString());
            EndOverrideEdit();
        };
        clearButton.style.marginLeft = 4f;
        row.Add(clearButton);

        return row;
    }

    private string GetBindingLabel(InputBinding binding)
    {
        if (binding.isPartOfComposite)
        {
            if (string.IsNullOrWhiteSpace(binding.name))
                return "Composite Part";

            return binding.name;
        }

        if (string.IsNullOrWhiteSpace(binding.name))
            return "Binding";

        return binding.name;
    }

    private void ApplySelectedOverridePreset()
    {
        if (m_OverridePresetDropdown == null)
            return;

        OverridePresetOption option = m_OverridePresetDropdown.value;

        if (option == null || option.Preset == null)
        {
            ApplyDefaultBindings();
            return;
        }

        PlayerInputOverridePreset preset = option.Preset;
        string actionId = GetSelectedActionId();

        if (string.IsNullOrWhiteSpace(actionId))
            return;

        BeginOverrideEdit("Apply Override Preset");
        RemoveOverridesForAction(actionId);

        IReadOnlyList<InputBindingOverride> overridesList = preset.Overrides;

        for (int i = 0; i < overridesList.Count; i++)
        {
            InputBindingOverride bindingOverride = overridesList[i];

            if (bindingOverride.ActionId != actionId)
                continue;

            SetOverride(bindingOverride.ActionId, bindingOverride.BindingId, bindingOverride.OverridePath, bindingOverride.OverrideInteractions, bindingOverride.OverrideProcessors);
        }

        EndOverrideEdit();
    }

    private void CreateOverridePresetFromCurrent()
    {
        InputAction action = GetSelectedAction();

        if (action == null)
        {
            EditorUtility.DisplayDialog("New Override Preset", "No input action selected.", "OK");
            return;
        }

        string presetName = action.name + " Overrides";
        PlayerInputOverridePreset newPreset = PlayerInputOverridePresetLibraryUtility.CreatePresetAsset(presetName);

        if (newPreset == null)
            return;

        WriteOverridesToPreset(newPreset, presetName, action);

        if (m_OverridePresetLibrary != null)
        {
            Undo.RecordObject(m_OverridePresetLibrary, "Add Override Preset");
            m_OverridePresetLibrary.AddPreset(newPreset);
            EditorUtility.SetDirty(m_OverridePresetLibrary);
            AssetDatabase.SaveAssets();
        }

        m_SelectedOverridePreset = newPreset;
        RefreshOverridePresetOptions();
        SetEditMode(true);
    }

    private void ToggleEditMode()
    {
        if (m_SelectedOverridePreset == null)
        {
            EditorUtility.DisplayDialog("Edit Override Preset", "Select a preset to edit first.", "OK");
            return;
        }

        SetEditMode(m_IsEditMode == false);
    }

    private void DeleteSelectedOverridePreset()
    {
        if (m_SelectedOverridePreset == null)
        {
            EditorUtility.DisplayDialog("Delete Override Preset", "Select a preset to delete first.", "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog("Delete Override Preset", "Delete the selected override preset asset?", "Delete", "Cancel");

        if (confirmed == false)
            return;

        if (m_OverridePresetLibrary != null)
        {
            Undo.RecordObject(m_OverridePresetLibrary, "Delete Override Preset");
            m_OverridePresetLibrary.RemovePreset(m_SelectedOverridePreset);
            EditorUtility.SetDirty(m_OverridePresetLibrary);
            AssetDatabase.SaveAssets();
        }

        string assetPath = AssetDatabase.GetAssetPath(m_SelectedOverridePreset);

        if (string.IsNullOrWhiteSpace(assetPath) == false)
            AssetDatabase.DeleteAsset(assetPath);

        m_SelectedOverridePreset = null;
        SetEditMode(false);
        RefreshOverridePresetOptions();
    }

    private void SetEditMode(bool enabled)
    {
        m_IsEditMode = enabled;
        RefreshEditModeView();
    }

    private void RefreshEditModeView()
    {
        if (m_EditPresetButton != null)
            m_EditPresetButton.text = m_IsEditMode ? "Done" : "Edit";

        if (m_EditPresetContainer == null)
            return;

        m_EditPresetContainer.Clear();

        if (m_IsEditMode == false || m_SelectedOverridePreset == null)
        {
            m_EditPresetContainer.style.display = DisplayStyle.None;
            return;
        }

        m_EditPresetContainer.style.display = DisplayStyle.Flex;
        m_SelectedOverridePresetSerializedObject = new SerializedObject(m_SelectedOverridePreset);
        m_SelectedPresetOverridesProperty = m_SelectedOverridePresetSerializedObject.FindProperty("m_Overrides");

        Label header = new Label("Preset Details");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 2f;
        m_EditPresetContainer.Add(header);

        SerializedProperty nameProperty = m_SelectedOverridePresetSerializedObject.FindProperty("m_PresetName");
        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        m_EditPresetContainer.Add(nameField);

        SerializedProperty descriptionProperty = m_SelectedOverridePresetSerializedObject.FindProperty("m_Description");
        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 50f;
        descriptionField.BindProperty(descriptionProperty);
        m_EditPresetContainer.Add(descriptionField);

        SerializedProperty actionNameProperty = m_SelectedOverridePresetSerializedObject.FindProperty("m_ActionName");
        TextField actionField = new TextField("Action");
        actionField.isReadOnly = true;
        actionField.SetEnabled(false);
        actionField.value = actionNameProperty != null ? actionNameProperty.stringValue : string.Empty;
        m_EditPresetContainer.Add(actionField);

        m_PresetOverridesContainer = new VisualElement();
        m_PresetOverridesContainer.style.marginTop = 6f;
        m_PresetOverridesContainer.style.marginLeft = 8f;
        m_PresetOverridesContainer.style.marginRight = 4f;
        m_PresetOverridesContainer.style.flexDirection = FlexDirection.Column;
        m_EditPresetContainer.Add(m_PresetOverridesContainer);

        RebuildPresetOverridesList();
    }

    private void RebuildPresetOverridesList()
    {
        if (m_PresetOverridesContainer == null || m_SelectedOverridePresetSerializedObject == null)
            return;

        m_PresetOverridesContainer.Clear();
        m_SelectedOverridePresetSerializedObject.Update();

        InputAction action = GetSelectedAction();

        if (action == null)
        {
            Label label = new Label("No action selected or action not found.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            m_PresetOverridesContainer.Add(label);
            return;
        }

        Label header = new Label("Preset Overrides");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 2f;
        m_PresetOverridesContainer.Add(header);

        IReadOnlyList<InputBinding> bindings = action.bindings;

        if (bindings == null || bindings.Count == 0)
        {
            Label emptyLabel = new Label("No bindings found for this action.");
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            m_PresetOverridesContainer.Add(emptyLabel);
            return;
        }

        string actionId = action.id.ToString();
        bool hasAnyBinding = false;

        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];

            if (binding.isComposite)
                continue;

            hasAnyBinding = true;
            SerializedProperty overrideProperty = FindOverrideProperty(m_SelectedPresetOverridesProperty, actionId, binding.id.ToString());
            VisualElement row = CreatePresetOverrideRow(action, binding, overrideProperty);
            m_PresetOverridesContainer.Add(row);
        }

        if (hasAnyBinding)
            return;

        Label noBindingsLabel = new Label("No bindings available to override.");
        noBindingsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        m_PresetOverridesContainer.Add(noBindingsLabel);
    }

    private VisualElement CreatePresetOverrideRow(InputAction action, InputBinding binding, SerializedProperty overrideProperty)
    {
        SerializedProperty pathProperty = overrideProperty != null ? overrideProperty.FindPropertyRelative("m_OverridePath") : null;
        string bindingLabel = GetBindingLabel(binding);
        string defaultPath = string.IsNullOrWhiteSpace(binding.path) ? "-" : binding.path;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 2f;

        Label nameLabel = new Label(bindingLabel);
        nameLabel.style.minWidth = 110f;
        row.Add(nameLabel);

        Label defaultLabel = new Label(defaultPath);
        defaultLabel.style.minWidth = 180f;
        defaultLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        row.Add(defaultLabel);

        TextField overrideField = new TextField();
        overrideField.style.flexGrow = 1f;
        overrideField.tooltip = OverridePathTooltip;
        overrideField.isDelayed = true;
        overrideField.value = pathProperty != null ? pathProperty.stringValue : string.Empty;
        overrideField.RegisterValueChangedCallback(evt =>
        {
            string actionId = action.id.ToString();
            string bindingId = binding.id.ToString();
            string newValue = evt.newValue;

            BeginPresetOverrideEdit("Edit Preset Override");

            if (string.IsNullOrWhiteSpace(newValue))
            {
                RemoveOverride(m_SelectedPresetOverridesProperty, actionId, bindingId);
                EndPresetOverrideEdit();
                return;
            }

            string interactions = GetOverrideString(overrideProperty, "m_OverrideInteractions");
            string processors = GetOverrideString(overrideProperty, "m_OverrideProcessors");
            SetOverride(m_SelectedPresetOverridesProperty, actionId, bindingId, newValue, interactions, processors);
            EndPresetOverrideEdit();
        });
        row.Add(overrideField);

        Button clearButton = new Button();
        clearButton.text = "Reset";
        clearButton.tooltip = "Clear the override for this binding.";
        clearButton.clicked += () =>
        {
            BeginPresetOverrideEdit("Reset Preset Override");
            RemoveOverride(m_SelectedPresetOverridesProperty, action.id.ToString(), binding.id.ToString());
            EndPresetOverrideEdit();
        };
        clearButton.style.marginLeft = 4f;
        row.Add(clearButton);

        return row;
    }

    private void BeginPresetOverrideEdit(string undoLabel)
    {
        if (m_SelectedOverridePreset == null || m_SelectedOverridePresetSerializedObject == null)
            return;

        Undo.RecordObject(m_SelectedOverridePreset, undoLabel);
        m_SelectedOverridePresetSerializedObject.Update();
    }

    private void EndPresetOverrideEdit()
    {
        if (m_SelectedOverridePresetSerializedObject == null)
            return;

        m_SelectedOverridePresetSerializedObject.ApplyModifiedProperties();
        RebuildPresetOverridesList();
    }

    private void WriteOverridesToPreset(PlayerInputOverridePreset preset, string presetName, InputAction action)
    {
        if (preset == null || action == null)
            return;

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("m_PresetName");
        SerializedProperty actionIdProperty = serializedPreset.FindProperty("m_ActionId");
        SerializedProperty actionNameProperty = serializedPreset.FindProperty("m_ActionName");
        SerializedProperty overridesProperty = serializedPreset.FindProperty("m_Overrides");

        if (nameProperty != null)
            nameProperty.stringValue = presetName;

        if (actionIdProperty != null)
            actionIdProperty.stringValue = action.id.ToString();

        if (actionNameProperty != null)
            actionNameProperty.stringValue = action.name;

        CopyOverridesToProperty(overridesProperty, action.id.ToString());

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
    }

    private void CopyOverridesToProperty(SerializedProperty targetOverridesProperty, string actionId)
    {
        if (targetOverridesProperty == null)
            return;

        targetOverridesProperty.arraySize = 0;

        if (m_OverridesProperty == null)
            return;

        for (int i = 0; i < m_OverridesProperty.arraySize; i++)
        {
            SerializedProperty sourceElement = m_OverridesProperty.GetArrayElementAtIndex(i);

            if (sourceElement == null)
                continue;

            SerializedProperty sourceActionId = sourceElement.FindPropertyRelative("m_ActionId");

            if (sourceActionId == null)
                continue;

            if (sourceActionId.stringValue != actionId)
                continue;

            int newIndex = targetOverridesProperty.arraySize;
            targetOverridesProperty.arraySize = newIndex + 1;

            SerializedProperty targetElement = targetOverridesProperty.GetArrayElementAtIndex(newIndex);
            CopyOverrideElement(sourceElement, targetElement);
        }
    }

    private void CopyOverrideElement(SerializedProperty source, SerializedProperty target)
    {
        if (source == null || target == null)
            return;

        SerializedProperty sourceActionId = source.FindPropertyRelative("m_ActionId");
        SerializedProperty sourceBindingId = source.FindPropertyRelative("m_BindingId");
        SerializedProperty sourcePath = source.FindPropertyRelative("m_OverridePath");
        SerializedProperty sourceInteractions = source.FindPropertyRelative("m_OverrideInteractions");
        SerializedProperty sourceProcessors = source.FindPropertyRelative("m_OverrideProcessors");

        SerializedProperty targetActionId = target.FindPropertyRelative("m_ActionId");
        SerializedProperty targetBindingId = target.FindPropertyRelative("m_BindingId");
        SerializedProperty targetPath = target.FindPropertyRelative("m_OverridePath");
        SerializedProperty targetInteractions = target.FindPropertyRelative("m_OverrideInteractions");
        SerializedProperty targetProcessors = target.FindPropertyRelative("m_OverrideProcessors");

        if (targetActionId != null)
            targetActionId.stringValue = sourceActionId != null ? sourceActionId.stringValue : string.Empty;

        if (targetBindingId != null)
            targetBindingId.stringValue = sourceBindingId != null ? sourceBindingId.stringValue : string.Empty;

        if (targetPath != null)
            targetPath.stringValue = sourcePath != null ? sourcePath.stringValue : string.Empty;

        if (targetInteractions != null)
            targetInteractions.stringValue = sourceInteractions != null ? sourceInteractions.stringValue : string.Empty;

        if (targetProcessors != null)
            targetProcessors.stringValue = sourceProcessors != null ? sourceProcessors.stringValue : string.Empty;
    }

    private void ApplyDefaultBindings()
    {
        InputAction action = GetSelectedAction();

        if (action == null)
            return;

        BeginOverrideEdit("Reset Input Overrides");
        RemoveOverridesForAction(action.id.ToString());
        EndOverrideEdit();
    }

    private void ApplyMoveWASD()
    {
        InputAction action = GetSelectedAction();

        if (action == null)
            return;

        BeginOverrideEdit("Apply WASD Preset");
        RemoveOverridesForAction(action.id.ToString());
        ApplyCompositeOverrideInternal(action, "up", "<Keyboard>/w");
        ApplyCompositeOverrideInternal(action, "down", "<Keyboard>/s");
        ApplyCompositeOverrideInternal(action, "left", "<Keyboard>/a");
        ApplyCompositeOverrideInternal(action, "right", "<Keyboard>/d");
        EndOverrideEdit();
    }

    private void ApplyMoveArrows()
    {
        InputAction action = GetSelectedAction();

        if (action == null)
            return;

        BeginOverrideEdit("Apply Arrows Preset");
        RemoveOverridesForAction(action.id.ToString());
        ApplyCompositeOverrideInternal(action, "up", "<Keyboard>/upArrow");
        ApplyCompositeOverrideInternal(action, "down", "<Keyboard>/downArrow");
        ApplyCompositeOverrideInternal(action, "left", "<Keyboard>/leftArrow");
        ApplyCompositeOverrideInternal(action, "right", "<Keyboard>/rightArrow");
        EndOverrideEdit();
    }

    private void ApplyMoveGamepadStick()
    {
        InputAction action = GetSelectedAction();

        if (action == null)
            return;

        BeginOverrideEdit("Apply Gamepad Preset");
        RemoveOverridesForAction(action.id.ToString());
        ApplyNonCompositeOverrideInternal(action, "leftStick", "<Gamepad>/leftStick", "Gamepad");
        EndOverrideEdit();
    }

    private void ApplyLookArrows()
    {
        InputAction action = GetSelectedAction();

        if (action == null)
            return;

        BeginOverrideEdit("Apply Look Arrows Preset");
        RemoveOverridesForAction(action.id.ToString());
        ApplyCompositeOverrideInternal(action, "up", "<Keyboard>/upArrow");
        ApplyCompositeOverrideInternal(action, "down", "<Keyboard>/downArrow");
        ApplyCompositeOverrideInternal(action, "left", "<Keyboard>/leftArrow");
        ApplyCompositeOverrideInternal(action, "right", "<Keyboard>/rightArrow");
        EndOverrideEdit();
    }

    private void ApplyLookGamepadStick()
    {
        InputAction action = GetSelectedAction();

        if (action == null)
            return;

        BeginOverrideEdit("Apply Look Gamepad Preset");
        RemoveOverridesForAction(action.id.ToString());
        ApplyNonCompositeOverrideInternal(action, "rightStick", "<Gamepad>/rightStick", "Gamepad");
        EndOverrideEdit();
    }

    private void ApplyLookMouseDelta()
    {
        InputAction action = GetSelectedAction();

        if (action == null)
            return;

        BeginOverrideEdit("Apply Mouse Delta Preset");
        RemoveOverridesForAction(action.id.ToString());
        ApplyNonCompositeOverrideInternal(action, "delta", "<Mouse>/delta", "Keyboard&Mouse");
        EndOverrideEdit();
    }

    private void BeginOverrideEdit(string undoLabel)
    {
        Undo.RecordObject(m_PresetSerializedObject.targetObject, undoLabel);
        m_PresetSerializedObject.Update();
    }

    private void EndOverrideEdit()
    {
        m_PresetSerializedObject.ApplyModifiedProperties();
        RebuildOverridesList();
    }

    private void ApplyCompositeOverrideInternal(InputAction action, string partName, string overridePath)
    {
        if (action == null)
            return;

        IReadOnlyList<InputBinding> bindings = action.bindings;

        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];

            if (binding.isPartOfComposite == false)
                continue;

            if (string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase) == false)
                continue;

            SetOverride(action.id.ToString(), binding.id.ToString(), overridePath, string.Empty, string.Empty);
        }
    }

    private void ApplyNonCompositeOverrideInternal(InputAction action, string pathHint, string overridePath, string requiredGroup)
    {
        if (action == null)
            return;

        IReadOnlyList<InputBinding> bindings = action.bindings;

        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];

            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            if (string.IsNullOrWhiteSpace(requiredGroup) == false)
            {
                string groups = binding.groups;
                bool matchesGroup = string.IsNullOrWhiteSpace(groups) == false && groups.Contains(requiredGroup);
                bool matchesPath = string.IsNullOrWhiteSpace(binding.path) == false && binding.path.Contains(requiredGroup);

                if (matchesGroup == false && matchesPath == false)
                    continue;
            }

            if (string.IsNullOrWhiteSpace(pathHint) == false)
            {
                if (string.IsNullOrWhiteSpace(binding.path) || binding.path.Contains(pathHint) == false)
                    continue;
            }

            SetOverride(action.id.ToString(), binding.id.ToString(), overridePath, string.Empty, string.Empty);
        }
    }

    private void RemoveOverride(SerializedProperty overridesProperty, string actionId, string bindingId)
    {
        if (overridesProperty == null)
            return;

        int index = FindOverrideIndex(overridesProperty, actionId, bindingId);

        if (index < 0)
            return;

        overridesProperty.DeleteArrayElementAtIndex(index);
    }

    private string GetOverrideString(SerializedProperty overrideProperty, string propertyName)
    {
        if (overrideProperty == null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(propertyName))
            return string.Empty;

        SerializedProperty property = overrideProperty.FindPropertyRelative(propertyName);

        if (property == null)
            return string.Empty;

        return property.stringValue;
    }

    private void RemoveOverridesForAction(string actionId)
    {
        if (m_OverridesProperty == null)
            return;

        for (int i = m_OverridesProperty.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty element = m_OverridesProperty.GetArrayElementAtIndex(i);

            if (element == null)
                continue;

            SerializedProperty actionIdProperty = element.FindPropertyRelative("m_ActionId");

            if (actionIdProperty == null)
                continue;

            if (actionIdProperty.stringValue != actionId)
                continue;

            m_OverridesProperty.DeleteArrayElementAtIndex(i);
        }
    }

    private void SetOverride(string actionId, string bindingId, string overridePath, string overrideInteractions, string overrideProcessors)
    {
        SetOverride(m_OverridesProperty, actionId, bindingId, overridePath, overrideInteractions, overrideProcessors);
    }

    private void SetOverride(SerializedProperty overridesProperty, string actionId, string bindingId, string overridePath, string overrideInteractions, string overrideProcessors)
    {
        if (overridesProperty == null)
            return;

        SerializedProperty overrideProperty = FindOverrideProperty(overridesProperty, actionId, bindingId);

        if (overrideProperty == null)
        {
            int newIndex = overridesProperty.arraySize;
            overridesProperty.arraySize = newIndex + 1;
            overrideProperty = overridesProperty.GetArrayElementAtIndex(newIndex);
        }

        if (overrideProperty == null)
            return;

        SerializedProperty actionIdProperty = overrideProperty.FindPropertyRelative("m_ActionId");
        SerializedProperty bindingIdProperty = overrideProperty.FindPropertyRelative("m_BindingId");
        SerializedProperty pathProperty = overrideProperty.FindPropertyRelative("m_OverridePath");
        SerializedProperty interactionsProperty = overrideProperty.FindPropertyRelative("m_OverrideInteractions");
        SerializedProperty processorsProperty = overrideProperty.FindPropertyRelative("m_OverrideProcessors");

        if (actionIdProperty != null)
            actionIdProperty.stringValue = actionId;

        if (bindingIdProperty != null)
            bindingIdProperty.stringValue = bindingId;

        if (pathProperty != null)
            pathProperty.stringValue = overridePath;

        if (interactionsProperty != null)
            interactionsProperty.stringValue = overrideInteractions;

        if (processorsProperty != null)
            processorsProperty.stringValue = overrideProcessors;
    }

    private SerializedProperty FindOverrideProperty(SerializedProperty overridesProperty, string actionId, string bindingId)
    {
        if (overridesProperty == null)
            return null;

        int index = FindOverrideIndex(overridesProperty, actionId, bindingId);

        if (index < 0)
            return null;

        return overridesProperty.GetArrayElementAtIndex(index);
    }

    private int FindOverrideIndex(SerializedProperty overridesProperty, string actionId, string bindingId)
    {
        if (overridesProperty == null)
            return -1;

        for (int i = 0; i < overridesProperty.arraySize; i++)
        {
            SerializedProperty element = overridesProperty.GetArrayElementAtIndex(i);

            if (element == null)
                continue;

            SerializedProperty actionIdProperty = element.FindPropertyRelative("m_ActionId");
            SerializedProperty bindingIdProperty = element.FindPropertyRelative("m_BindingId");

            if (actionIdProperty == null || bindingIdProperty == null)
                continue;

            if (actionIdProperty.stringValue == actionId && bindingIdProperty.stringValue == bindingId)
                return i;
        }

        return -1;
    }

    private InputAction GetSelectedAction()
    {
        if (m_InputAsset == null || m_ActionIdProperty == null)
            return null;

        string actionId = m_ActionIdProperty.stringValue;

        if (string.IsNullOrWhiteSpace(actionId))
            return null;

        return m_InputAsset.FindAction(actionId, false);
    }

    private string GetSelectedActionId()
    {
        return m_ActionIdProperty != null ? m_ActionIdProperty.stringValue : string.Empty;
    }
    #endregion
}
