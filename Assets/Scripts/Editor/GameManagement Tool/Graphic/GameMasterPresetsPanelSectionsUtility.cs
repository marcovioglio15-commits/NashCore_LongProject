using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds selected game master preset detail sections and sub-preset creation flows.
/// /params None.
/// /returns None.
/// </summary>
internal static class GameMasterPresetsPanelSectionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Selects one master preset and rebuilds the active detail section.
    /// /params panel Owning panel with detail roots.
    /// /params preset Preset to select, or null to clear details.
    /// /returns None.
    /// </summary>
    public static void SelectPreset(GameMasterPresetsPanel panel, GameMasterPreset preset)
    {
        if (panel == null || panel.DetailsRoot == null)
            return;

        panel.SelectedPreset = preset;
        panel.DetailsRoot.Clear();

        if (panel.PresetListView != null && preset != null)
        {
            int selectedIndex = panel.FilteredPresets.IndexOf(preset);

            if (selectedIndex >= 0)
                panel.PresetListView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }

        if (panel.SelectedPreset == null)
        {
            panel.DetailsRoot.Add(new Label("Select or create a game master preset to edit."));
            return;
        }

        panel.PresetSerializedObject = new SerializedObject(panel.SelectedPreset);
        panel.DetailSectionButtonsRoot = BuildDetailsSectionButtons(panel);
        panel.DetailSectionContentRoot = new VisualElement();
        panel.DetailSectionContentRoot.style.flexGrow = 1f;
        panel.DetailsRoot.Add(panel.DetailSectionButtonsRoot);
        panel.DetailsRoot.Add(panel.DetailSectionContentRoot);
        BuildActiveDetailsSection(panel);
        GameMasterPresetsPanelSidePanelUtility.SyncOpenSidePanels(panel);
    }

    /// <summary>
    /// Rebuilds the currently active detail section.
    /// /params panel Owning panel with serialized preset context.
    /// /returns None.
    /// </summary>
    public static void BuildActiveDetailsSection(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.DetailSectionContentRoot == null || panel.PresetSerializedObject == null)
            return;

        panel.PresetSerializedObject.Update();
        panel.DetailSectionContentRoot.Clear();

        switch (panel.ActiveDetailsSection)
        {
            case GameMasterPresetsPanel.DetailsSectionType.SubPresets:
                BuildSubPresetsSection(panel);
                break;
            case GameMasterPresetsPanel.DetailsSectionType.ActiveAuthoring:
                BuildActiveAuthoringSection(panel);
                break;
            case GameMasterPresetsPanel.DetailsSectionType.Navigation:
                BuildNavigationSection(panel);
                break;
            default:
                BuildMetadataSection(panel);
                break;
        }

        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(panel.DetailSectionContentRoot);
    }

    /// <summary>
    /// Creates, registers and assigns a new Audio Manager preset to the selected master preset.
    /// /params panel Owning panel with selected master preset context.
    /// /returns None.
    /// </summary>
    public static void CreateAudioManagerPreset(GameMasterPresetsPanel panel)
    {
        if (panel == null || panel.SelectedPreset == null)
            return;

        GameAudioManagerPreset newPreset = GameAudioManagerPresetLibraryUtility.CreatePresetAsset("GameAudioManagerPreset");

        if (newPreset == null)
            return;

        GameAudioManagerPresetLibrary audioLibrary = GameAudioManagerPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Audio Manager Preset");
        Undo.RecordObject(audioLibrary, "Add Audio Manager Preset");
        audioLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(audioLibrary);

        AssignSubPreset(panel, "audioManagerPreset", newPreset);
        panel.OpenSidePanel(GameManagementWindow.PanelType.AudioManager);
    }

    /// <summary>
    /// Assigns one sub-preset reference to the selected master preset.
    /// /params panel Owning panel with serialized master preset.
    /// /params propertyName Serialized property receiving the object reference.
    /// /params preset Preset object to assign.
    /// /returns None.
    /// </summary>
    public static void AssignSubPreset(GameMasterPresetsPanel panel, string propertyName, UnityEngine.Object preset)
    {
        if (panel == null || panel.SelectedPreset == null || panel.PresetSerializedObject == null)
            return;

        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        Undo.RecordObject(panel.SelectedPreset, "Assign Game Sub Preset");
        panel.PresetSerializedObject.Update();
        property.objectReferenceValue = preset;
        panel.PresetSerializedObject.ApplyModifiedProperties();
        GameManagementDraftSession.MarkDirty();
        GameMasterPresetsPanelSidePanelUtility.SyncOpenSidePanels(panel);
        BuildActiveDetailsSection(panel);
    }
    #endregion

    #region Section Builders
    /// <summary>
    /// Builds metadata fields for the selected master preset.
    /// /params panel Owning panel with serialized preset context.
    /// /returns None.
    /// </summary>
    private static void BuildMetadataSection(GameMasterPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Preset Details");
        AddBoundTextField(panel, section, "Preset Name", "presetName", true, false);
        AddBoundTextField(panel, section, "Version", "version", false, false);
        AddBoundTextField(panel, section, "Description", "description", false, true);

        SerializedProperty idProperty = panel.PresetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        PropertyField idField = new PropertyField(idProperty, "Preset ID");
        idField.tooltip = "Stable ID used by Game Management Tool for this master preset.";
        idField.BindProperty(idProperty);
        idField.SetEnabled(false);
        section.Add(idField);
    }

    /// <summary>
    /// Builds Audio Manager sub-preset assignment controls.
    /// /params panel Owning panel with selected master preset context.
    /// /returns None.
    /// </summary>
    private static void BuildSubPresetsSection(GameMasterPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Sub Presets");
        SerializedProperty audioProperty = panel.PresetSerializedObject.FindProperty("audioManagerPreset");

        if (audioProperty == null)
            return;

        ObjectField audioField = new ObjectField("Audio Manager Preset");
        audioField.objectType = typeof(GameAudioManagerPreset);
        audioField.tooltip = "Audio Manager preset used for FMOD gameplay event bindings.";
        audioField.BindProperty(audioProperty);
        audioField.RegisterValueChangedCallback(evt =>
        {
            GameManagementDraftSession.MarkDirty();
            GameMasterPresetsPanelSidePanelUtility.SyncOpenSidePanels(panel);
        });
        section.Add(audioField);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        Button openButton = new Button(() => panel.OpenSidePanel(GameManagementWindow.PanelType.AudioManager));
        openButton.text = "Open Section";
        openButton.tooltip = "Open the Audio Manager section.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(openButton, 108f);
        row.Add(openButton);

        Button newButton = new Button(panel.CreateAudioManagerPreset);
        newButton.text = "New";
        newButton.tooltip = "Create and assign a new Audio Manager preset.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(newButton, 48f);
        newButton.style.marginLeft = 4f;
        row.Add(newButton);
        section.Add(row);
    }

    /// <summary>
    /// Builds prefab authoring controls for the selected game master preset.
    /// /params panel Owning panel that stores selected prefab state.
    /// /returns None.
    /// </summary>
    private static void BuildActiveAuthoringSection(GameMasterPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Active on Game Audio Authoring");

        ObjectField audioPrefabField = new ObjectField("Audio Manager Prefab");
        audioPrefabField.objectType = typeof(GameObject);
        audioPrefabField.tooltip = "Prefab containing GameAudioManagerAuthoring to receive this Game Master preset.";
        audioPrefabField.SetValueWithoutNotify(panel.SelectedAudioPrefab);
        audioPrefabField.RegisterValueChangedCallback(evt =>
        {
            panel.SelectedAudioPrefab = evt.newValue as GameObject;
            GameMasterPresetsPanelSidePanelUtility.SaveSelectedAudioPrefabState(panel);
            panel.RefreshActiveStatus();
        });
        panel.AudioPrefabField = audioPrefabField;
        section.Add(audioPrefabField);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        Button findButton = new Button(panel.FindAudioManagerPrefab);
        findButton.text = "Find";
        findButton.tooltip = "Find a prefab with GameAudioManagerAuthoring.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(findButton, 48f);
        row.Add(findButton);

        Button assignButton = new Button(panel.AssignPresetToAuthoringPrefab);
        assignButton.text = "Set Active Preset";
        assignButton.tooltip = "Assign this game master preset to the selected authoring prefab.";
        GameManagementPanelLayoutUtility.ConfigureToolbarButton(assignButton, 128f);
        assignButton.style.marginLeft = 4f;
        row.Add(assignButton);
        section.Add(row);

        Label activeStatusLabel = new Label();
        activeStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        panel.ActiveStatusLabel = activeStatusLabel;
        section.Add(activeStatusLabel);
        panel.RefreshActiveStatus();
    }

    /// <summary>
    /// Builds quick navigation controls for implemented game sub-sections.
    /// /params panel Owning panel that opens side panels.
    /// /returns None.
    /// </summary>
    private static void BuildNavigationSection(GameMasterPresetsPanel panel)
    {
        VisualElement section = CreateSection(panel, "Open Sections");
        Button audioButton = new Button(() => panel.OpenSidePanel(GameManagementWindow.PanelType.AudioManager));
        audioButton.text = "Open Audio Manager";
        audioButton.tooltip = "Open the Audio Manager preset panel.";
        audioButton.style.flexShrink = 0f;
        audioButton.style.minWidth = 144f;
        section.Add(audioButton);
    }
    #endregion

    #region Detail Helpers
    /// <summary>
    /// Builds detail section selector buttons.
    /// /params panel Owning panel that stores the active section state.
    /// /returns Detail section button row.
    /// </summary>
    private static VisualElement BuildDetailsSectionButtons(GameMasterPresetsPanel panel)
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;
        AddDetailsSectionButton(panel, buttonsRoot, GameMasterPresetsPanel.DetailsSectionType.Metadata, "Metadata");
        AddDetailsSectionButton(panel, buttonsRoot, GameMasterPresetsPanel.DetailsSectionType.SubPresets, "Sub Presets");
        AddDetailsSectionButton(panel, buttonsRoot, GameMasterPresetsPanel.DetailsSectionType.ActiveAuthoring, "Active Authoring");
        AddDetailsSectionButton(panel, buttonsRoot, GameMasterPresetsPanel.DetailsSectionType.Navigation, "Navigation");
        return buttonsRoot;
    }

    /// <summary>
    /// Adds one detail section selector button.
    /// /params panel Owning panel that receives the selected section.
    /// /params parent Parent row.
    /// /params sectionType Section activated by the button.
    /// /params label Visible button label.
    /// /returns None.
    /// </summary>
    private static void AddDetailsSectionButton(GameMasterPresetsPanel panel, VisualElement parent, GameMasterPresetsPanel.DetailsSectionType sectionType, string label)
    {
        Button button = new Button(() =>
        {
            panel.ActiveDetailsSection = sectionType;
            GameMasterPresetsPanelSidePanelUtility.SaveActiveDetailsSection(panel);
            BuildActiveDetailsSection(panel);
        });
        button.text = label;
        button.tooltip = "Show the " + label + " section.";
        button.style.flexShrink = 0f;
        button.style.minWidth = ResolveDetailsSectionButtonWidth(sectionType);
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Resolves a stable minimum width for Game Master detail section buttons.
    /// /params sectionType Section represented by the selector button.
    /// /returns Minimum width that keeps the label readable before wrapping to a new row.
    /// </summary>
    private static float ResolveDetailsSectionButtonWidth(GameMasterPresetsPanel.DetailsSectionType sectionType)
    {
        switch (sectionType)
        {
            case GameMasterPresetsPanel.DetailsSectionType.SubPresets:
                return 96f;
            case GameMasterPresetsPanel.DetailsSectionType.ActiveAuthoring:
                return 124f;
            case GameMasterPresetsPanel.DetailsSectionType.Navigation:
                return 92f;
            default:
                return 84f;
        }
    }

    /// <summary>
    /// Creates a styled section container and registers its heading for recolor utilities.
    /// /params panel Owning panel with active details root.
    /// /params title Section title.
    /// /returns Section container.
    /// </summary>
    private static VisualElement CreateSection(GameMasterPresetsPanel panel, string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label label = new Label(title);
        label.tooltip = "Section header: " + title + ".";
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(label, "NashCore.GameManagement.Master." + title);
        section.Add(label);
        panel.DetailSectionContentRoot.Add(section);
        return section;
    }

    /// <summary>
    /// Adds one bound text field and marks the draft dirty on edits.
    /// /params panel Owning panel with serialized preset context.
    /// /params parent Parent section.
    /// /params label Display label.
    /// /params propertyName Serialized property name.
    /// /params refreshList True when list labels should update after change.
    /// /params multiline True when the field should use multiline editing.
    /// /returns None.
    /// </summary>
    private static void AddBoundTextField(GameMasterPresetsPanel panel, VisualElement parent, string label, string propertyName, bool refreshList, bool multiline)
    {
        SerializedProperty property = panel.PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        TextField field = new TextField(label);
        field.tooltip = "Edit " + label + " for this game master preset.";
        field.isDelayed = true;
        field.multiline = multiline;
        field.BindProperty(property);
        field.RegisterValueChangedCallback(evt =>
        {
            if (panel.SelectedPreset != null)
                Undo.RecordObject(panel.SelectedPreset, "Edit Game Master Preset");

            panel.PresetSerializedObject.ApplyModifiedProperties();
            GameManagementDraftSession.MarkDirty();

            if (refreshList)
                panel.RefreshPresetList();
        });
        parent.Add(field);
    }
    #endregion

    #endregion
}
