using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public sealed class PlayerPowerUpsPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<PlayerPowerUpsPreset> filteredPresets = new List<PlayerPowerUpsPreset>();
    private readonly PlayerPowerUpsPresetLibrary library;
    private readonly InputActionAsset inputAsset;

    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement sectionButtonsRoot;
    private VisualElement sectionContentRoot;

    private PlayerPowerUpsPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private SectionType activeSection = SectionType.Metadata;
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return root;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    public PlayerPowerUpsPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = PlayerPowerUpsPresetLibraryUtility.GetOrCreateLibrary();
        inputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();

        BuildUI();
        RefreshPresetList();
    }

    public void SelectPresetFromExternal(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        RefreshPresetList();

        int presetIndex = filteredPresets.IndexOf(preset);

        if (presetIndex < 0)
            return;

        if (listView == null)
        {
            SelectPreset(preset);
            return;
        }

        if (listView.selectedIndex != presetIndex)
        {
            listView.SetSelection(presetIndex);
            return;
        }

        SelectPreset(preset);
    }

    public void RefreshFromSessionChange()
    {
        PlayerPowerUpsPreset previouslySelectedPreset = selectedPreset;
        RefreshPresetList();

        if (previouslySelectedPreset == null)
            return;

        int presetIndex = filteredPresets.IndexOf(previouslySelectedPreset);

        if (presetIndex < 0)
            return;

        if (listView != null)
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });

        SelectPreset(previouslySelectedPreset);
    }
    #endregion

    #region UI
    private void BuildUI()
    {
        TwoPaneSplitView splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
        VisualElement leftPane = BuildLeftPane();
        VisualElement rightPane = BuildRightPanel();

        splitView.Add(leftPane);
        splitView.Add(rightPane);
        root.Add(splitView);
    }

    private VisualElement BuildLeftPane()
    {
        VisualElement leftPane = new VisualElement();
        leftPane.style.flexGrow = 1f;
        leftPane.style.paddingLeft = 6f;
        leftPane.style.paddingRight = 6f;
        leftPane.style.paddingTop = 6f;
        leftPane.style.overflow = Overflow.Hidden;

        Toolbar toolbar = new Toolbar();
        toolbar.style.marginBottom = 4f;

        Button createButton = new Button(CreatePreset);
        createButton.text = "Create";
        toolbar.Add(createButton);

        Button duplicateButton = new Button(DuplicatePreset);
        duplicateButton.text = "Duplicate";
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button(DeletePreset);
        deleteButton.text = "Delete";
        toolbar.Add(deleteButton);

        leftPane.Add(toolbar);

        searchField = new ToolbarSearchField();
        searchField.style.width = Length.Percent(100f);
        searchField.style.maxWidth = Length.Percent(100f);
        searchField.style.flexShrink = 1f;
        searchField.style.marginBottom = 4f;
        searchField.RegisterValueChangedCallback(evt =>
        {
            RefreshPresetList();
        });
        leftPane.Add(searchField);

        listView = new ListView();
        listView.style.flexGrow = 1f;
        listView.itemsSource = filteredPresets;
        listView.selectionType = SelectionType.Single;
        listView.makeItem = MakePresetItem;
        listView.bindItem = BindPresetItem;
        listView.selectionChanged += OnPresetSelectionChanged;
        leftPane.Add(listView);

        return leftPane;
    }

    private VisualElement BuildRightPanel()
    {
        VisualElement rightPane = new VisualElement();
        rightPane.style.flexGrow = 1f;
        rightPane.style.paddingLeft = 10f;
        rightPane.style.paddingRight = 10f;
        rightPane.style.paddingTop = 6f;

        detailsRoot = new ScrollView();
        detailsRoot.style.flexGrow = 1f;
        rightPane.Add(detailsRoot);

        return rightPane;
    }

    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            PlayerPowerUpsPreset preset = label.userData as PlayerPowerUpsPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Rename", action => ShowRenamePopup(label, preset), DropdownMenuAction.AlwaysEnabled);
        }));

        return label;
    }

    private void BindPresetItem(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= filteredPresets.Count)
        {
            label.text = string.Empty;
            label.userData = null;
            return;
        }

        PlayerPowerUpsPreset preset = filteredPresets[index];

        if (preset == null)
        {
            label.text = "<Missing Preset>";
            label.userData = null;
            return;
        }

        label.userData = preset;
        label.text = GetPresetDisplayName(preset);
        label.tooltip = string.IsNullOrWhiteSpace(preset.Description) ? string.Empty : preset.Description;
    }
    #endregion

    #region Preset List
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            PlayerPowerUpsPreset preset = selectedObject as PlayerPowerUpsPreset;

            if (preset == null)
                continue;

            SelectPreset(preset);
            return;
        }

        SelectPreset(null);
    }

    private void RefreshPresetList()
    {
        filteredPresets.Clear();

        if (library != null)
        {
            string searchText = searchField != null ? searchField.value : string.Empty;

            for (int index = 0; index < library.Presets.Count; index++)
            {
                PlayerPowerUpsPreset preset = library.Presets[index];

                if (preset == null)
                    continue;

                if (PlayerManagementDraftSession.IsAssetStagedForDeletion(preset))
                    continue;

                if (IsMatchingSearch(preset, searchText))
                    filteredPresets.Add(preset);
            }
        }

        if (listView != null)
            listView.Rebuild();

        if (filteredPresets.Count == 0)
        {
            SelectPreset(null);
            return;
        }

        if (selectedPreset == null || filteredPresets.Contains(selectedPreset) == false)
        {
            SelectPreset(filteredPresets[0]);

            if (listView != null)
                listView.SetSelectionWithoutNotify(new int[] { 0 });
        }
    }

    private bool IsMatchingSearch(PlayerPowerUpsPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string presetName = preset.PresetName;

        if (string.IsNullOrWhiteSpace(presetName))
            return false;

        return presetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #region Preset Actions
    private void CreatePreset()
    {
        PlayerPowerUpsPreset newPreset = PlayerPowerUpsPresetLibraryUtility.CreatePresetAsset("PlayerPowerUpsPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Power Ups Preset Asset");
        Undo.RecordObject(library, "Add Power Ups Preset");
        library.AddPreset(newPreset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.MarkDirty();

        if (searchField != null)
            searchField.SetValueWithoutNotify(string.Empty);

        RefreshPresetList();
        SelectPresetInList(newPreset);
    }

    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    private void DuplicatePreset(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        PlayerPowerUpsPreset duplicatedPreset = ScriptableObject.CreateInstance<PlayerPowerUpsPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);
        duplicatedPreset.name = preset.name + " Copy";

        string originalPath = AssetDatabase.GetAssetPath(preset);
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(originalPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Power Ups Preset Asset");

        SerializedObject duplicatedSerializedObject = new SerializedObject(duplicatedPreset);
        SerializedProperty presetIdProperty = duplicatedSerializedObject.FindProperty("presetId");
        SerializedProperty presetNameProperty = duplicatedSerializedObject.FindProperty("presetName");

        if (presetIdProperty != null)
            presetIdProperty.stringValue = Guid.NewGuid().ToString("N");

        if (presetNameProperty != null)
            presetNameProperty.stringValue = duplicatedPreset.name;

        duplicatedSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(library, "Duplicate Power Ups Preset");
        library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.MarkDirty();

        if (searchField != null)
            searchField.SetValueWithoutNotify(string.Empty);

        RefreshPresetList();
        SelectPresetInList(duplicatedPreset);
    }

    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    private void DeletePreset(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Power Ups Preset", "Delete the selected power ups preset?", "Delete", "Cancel");

        if (confirmed == false)
            return;

        Undo.RecordObject(library, "Delete Power Ups Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }

    private void RenamePreset(PlayerPowerUpsPreset preset, string newName)
    {
        if (preset == null)
            return;

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(newName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return;

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty presetNameProperty = serializedPreset.FindProperty("presetName");

        if (presetNameProperty != null)
        {
            serializedPreset.Update();
            presetNameProperty.stringValue = normalizedName;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        }

        preset.name = normalizedName;
        EditorUtility.SetDirty(preset);
        PlayerManagementDraftSession.MarkDirty();
        RefreshPresetList();
    }

    private void ShowRenamePopup(VisualElement anchor, PlayerPowerUpsPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        PresetRenamePopup.Show(anchorRect, "Rename Power Ups Preset", preset.PresetName, newName => RenamePreset(preset, newName));
    }
    #endregion

    #region Details
    private void SelectPreset(PlayerPowerUpsPreset preset)
    {
        selectedPreset = preset;
        sectionButtonsRoot = null;
        sectionContentRoot = null;

        if (detailsRoot == null)
            return;

        detailsRoot.Clear();

        if (selectedPreset == null)
        {
            Label emptyLabel = new Label("Select or create a power ups preset to edit.");
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(emptyLabel);
            return;
        }

        presetSerializedObject = new SerializedObject(selectedPreset);

        sectionButtonsRoot = BuildSectionButtons();
        sectionContentRoot = new VisualElement();
        sectionContentRoot.style.flexDirection = FlexDirection.Column;
        sectionContentRoot.style.flexGrow = 1f;

        detailsRoot.Add(sectionButtonsRoot);
        detailsRoot.Add(sectionContentRoot);
        sectionContentRoot.TrackSerializedObjectValue(presetSerializedObject, OnPresetSerializedChanged);

        BuildActiveSection();
    }

    private void SelectPresetInList(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        int presetIndex = filteredPresets.IndexOf(preset);

        if (presetIndex >= 0 && listView != null)
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });

        SelectPreset(preset);
    }

    private VisualElement BuildSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddSectionButton(buttonsRoot, SectionType.DropPools, "Drop Pools");
        AddSectionButton(buttonsRoot, SectionType.PassiveModifiers, "Passive Modifiers");
        AddSectionButton(buttonsRoot, SectionType.ActiveTools, "Active Tools");
        AddSectionButton(buttonsRoot, SectionType.LoadoutInput, "Loadout & Input");

        return buttonsRoot;
    }

    private void AddSectionButton(VisualElement parent, SectionType sectionType, string buttonLabel)
    {
        Button sectionButton = new Button(() => SetActiveSection(sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    private void SetActiveSection(SectionType sectionType)
    {
        activeSection = sectionType;
        BuildActiveSection();
    }

    private void BuildActiveSection()
    {
        if (sectionContentRoot == null)
            return;

        sectionContentRoot.Clear();

        switch (activeSection)
        {
            case SectionType.Metadata:
                BuildMetadataSection();
                return;
            case SectionType.DropPools:
                BuildDropPoolsSection();
                return;
            case SectionType.PassiveModifiers:
                BuildPassiveModifiersSection();
                return;
            case SectionType.ActiveTools:
                BuildActiveToolsSection();
                return;
            case SectionType.LoadoutInput:
                BuildLoadoutInputSection();
                return;
        }
    }

    private void BuildMetadataSection()
    {
        Label header = new Label("Preset Details");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

        SerializedProperty presetIdProperty = presetSerializedObject.FindProperty("presetId");
        SerializedProperty presetNameProperty = presetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = presetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = presetSerializedObject.FindProperty("version");

        if (presetIdProperty == null || presetNameProperty == null || descriptionProperty == null || versionProperty == null)
        {
            Label missingLabel = new Label("Power ups preset metadata properties are missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(presetNameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            RenamePreset(selectedPreset, evt.newValue);
        });
        sectionContentRoot.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        sectionContentRoot.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 64f;
        descriptionField.BindProperty(descriptionProperty);
        sectionContentRoot.Add(descriptionField);

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Preset ID");
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.BindProperty(presetIdProperty);
        idRow.Add(idField);

        Button regenerateButton = new Button(RegeneratePresetId);
        regenerateButton.text = "Regenerate";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);

        sectionContentRoot.Add(idRow);
    }

    private void BuildDropPoolsSection()
    {
        Label header = new Label("Drop Pool Catalog");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

        SerializedProperty dropPoolCatalogProperty = presetSerializedObject.FindProperty("dropPoolCatalog");

        if (dropPoolCatalogProperty == null)
        {
            Label missingLabel = new Label("Drop pool catalog property is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        PropertyField catalogField = new PropertyField(dropPoolCatalogProperty);
        catalogField.BindProperty(dropPoolCatalogProperty);
        sectionContentRoot.Add(catalogField);
    }

    private void BuildPassiveModifiersSection()
    {
        Label header = new Label("Passive Modifiers");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

        SerializedProperty passiveModifiersProperty = presetSerializedObject.FindProperty("passiveModifiers");

        if (passiveModifiersProperty == null)
        {
            Label missingLabel = new Label("Passive modifiers property is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        PropertyField passiveModifiersField = new PropertyField(passiveModifiersProperty);
        passiveModifiersField.BindProperty(passiveModifiersProperty);
        sectionContentRoot.Add(passiveModifiersField);
    }

    private void BuildActiveToolsSection()
    {
        Label header = new Label("Active Tools");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

        SerializedProperty activeToolsProperty = presetSerializedObject.FindProperty("activeTools");

        if (activeToolsProperty == null)
        {
            Label missingLabel = new Label("Active tools property is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        PropertyField activeToolsField = new PropertyField(activeToolsProperty);
        activeToolsField.BindProperty(activeToolsProperty);
        sectionContentRoot.Add(activeToolsField);
    }

    private void BuildLoadoutInputSection()
    {
        Label header = new Label("Loadout & Input");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

        SerializedProperty primaryToolActionIdProperty = presetSerializedObject.FindProperty("primaryToolActionId");
        SerializedProperty secondaryToolActionIdProperty = presetSerializedObject.FindProperty("secondaryToolActionId");
        SerializedProperty primaryActiveToolIdProperty = presetSerializedObject.FindProperty("primaryActiveToolId");
        SerializedProperty secondaryActiveToolIdProperty = presetSerializedObject.FindProperty("secondaryActiveToolId");
        SerializedProperty activeToolsProperty = presetSerializedObject.FindProperty("activeTools");

        if (primaryToolActionIdProperty == null)
        {
            Label missingLabel = new Label("Loadout/input properties are missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        if (secondaryToolActionIdProperty == null)
        {
            Label missingLabel = new Label("Loadout/input properties are missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        if (primaryActiveToolIdProperty == null)
        {
            Label missingLabel = new Label("Loadout/input properties are missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        if (secondaryActiveToolIdProperty == null)
        {
            Label missingLabel = new Label("Loadout/input properties are missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        if (activeToolsProperty == null)
        {
            Label missingLabel = new Label("Active tools list is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        EnsureDefaultActionId(primaryToolActionIdProperty, "PowerUpPrimary");
        EnsureDefaultActionId(secondaryToolActionIdProperty, "PowerUpSecondary");

        Label bindingsHeader = new Label("Bindings");
        bindingsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        bindingsHeader.style.marginTop = 6f;
        bindingsHeader.style.marginBottom = 2f;
        sectionContentRoot.Add(bindingsHeader);

        Foldout primaryBindingFoldout = BuildBindingsFoldout("Primary Tool Input", primaryToolActionIdProperty);
        sectionContentRoot.Add(primaryBindingFoldout);

        Foldout secondaryBindingFoldout = BuildBindingsFoldout("Secondary Tool Input", secondaryToolActionIdProperty);
        sectionContentRoot.Add(secondaryBindingFoldout);

        Label loadoutHeader = new Label("Loadout");
        loadoutHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        loadoutHeader.style.marginTop = 6f;
        loadoutHeader.style.marginBottom = 2f;
        sectionContentRoot.Add(loadoutHeader);

        List<LoadoutToolOption> loadoutOptions = BuildLoadoutOptions(activeToolsProperty);

        if (loadoutOptions.Count == 0)
        {
            HelpBox missingToolsHelpBox = new HelpBox("No valid active tools found. Add Bomb or Dash tools in Active Tools first.", HelpBoxMessageType.Warning);
            sectionContentRoot.Add(missingToolsHelpBox);
            return;
        }

        BuildLoadoutSelector("Primary Tool", primaryActiveToolIdProperty, loadoutOptions);
        BuildLoadoutSelector("Secondary Tool", secondaryActiveToolIdProperty, loadoutOptions);

        ActiveToolKind primarySelectedKind = ResolveSelectedKind(primaryActiveToolIdProperty.stringValue, loadoutOptions);
        ActiveToolKind secondarySelectedKind = ResolveSelectedKind(secondaryActiveToolIdProperty.stringValue, loadoutOptions);

        if (primarySelectedKind == secondarySelectedKind)
        {
            HelpBox sameSlotWarning = new HelpBox("Primary and Secondary currently use the same tool type.", HelpBoxMessageType.Warning);
            sectionContentRoot.Add(sameSlotWarning);
        }
    }

    private void BuildLoadoutSelector(string label, SerializedProperty slotToolIdProperty, List<LoadoutToolOption> loadoutOptions)
    {
        if (slotToolIdProperty == null)
            return;

        if (loadoutOptions == null || loadoutOptions.Count == 0)
            return;

        List<ActiveToolKind> availableKinds = new List<ActiveToolKind>();

        for (int index = 0; index < loadoutOptions.Count; index++)
            availableKinds.Add(loadoutOptions[index].Kind);

        ActiveToolKind selectedKind = ResolveSelectedKind(slotToolIdProperty.stringValue, loadoutOptions);
        PopupField<ActiveToolKind> kindSelector = new PopupField<ActiveToolKind>(label, availableKinds, selectedKind);
        kindSelector.formatListItemCallback = FormatToolKind;
        kindSelector.formatSelectedValueCallback = FormatToolKind;
        kindSelector.tooltip = "Select the active tool type assigned to this slot.";
        kindSelector.RegisterValueChangedCallback(evt =>
        {
            string selectedToolId = ResolveToolIdByKind(loadoutOptions, evt.newValue);

            if (string.IsNullOrWhiteSpace(selectedToolId))
                return;

            if (string.Equals(slotToolIdProperty.stringValue, selectedToolId, StringComparison.Ordinal))
                return;

            if (selectedPreset != null)
                Undo.RecordObject(selectedPreset, "Change Power Ups Loadout Slot");

            presetSerializedObject.Update();
            slotToolIdProperty.stringValue = selectedToolId;
            presetSerializedObject.ApplyModifiedProperties();
            PlayerManagementDraftSession.MarkDirty();
            BuildActiveSection();
        });
        sectionContentRoot.Add(kindSelector);
    }

    private static string FormatToolKind(ActiveToolKind toolKind)
    {
        switch (toolKind)
        {
            case ActiveToolKind.Bomb:
                return "Bomb";
            case ActiveToolKind.Dash:
                return "Dash";
            default:
                return "Bomb";
        }
    }

    private static List<LoadoutToolOption> BuildLoadoutOptions(SerializedProperty activeToolsProperty)
    {
        List<LoadoutToolOption> options = new List<LoadoutToolOption>();

        if (activeToolsProperty == null)
            return options;

        for (int index = 0; index < activeToolsProperty.arraySize; index++)
        {
            SerializedProperty activeToolProperty = activeToolsProperty.GetArrayElementAtIndex(index);

            if (activeToolProperty == null)
                continue;

            SerializedProperty toolKindProperty = activeToolProperty.FindPropertyRelative("toolKind");
            SerializedProperty commonDataProperty = activeToolProperty.FindPropertyRelative("commonData");

            if (toolKindProperty == null || commonDataProperty == null)
                continue;

            SerializedProperty toolIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");

            if (toolIdProperty == null)
                continue;

            ActiveToolKind toolKind = (ActiveToolKind)toolKindProperty.enumValueIndex;

            if (toolKind != ActiveToolKind.Bomb && toolKind != ActiveToolKind.Dash)
                continue;

            string toolId = toolIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(toolId))
                continue;

            if (HasOptionForKind(options, toolKind))
                continue;

            options.Add(new LoadoutToolOption
            {
                Kind = toolKind,
                ToolId = toolId
            });
        }

        return options;
    }

    private static bool HasOptionForKind(List<LoadoutToolOption> options, ActiveToolKind toolKind)
    {
        if (options == null)
            return false;

        for (int index = 0; index < options.Count; index++)
        {
            if (options[index].Kind != toolKind)
                continue;

            return true;
        }

        return false;
    }

    private static ActiveToolKind ResolveSelectedKind(string selectedToolId, List<LoadoutToolOption> options)
    {
        if (options == null || options.Count == 0)
            return ActiveToolKind.Bomb;

        if (string.IsNullOrWhiteSpace(selectedToolId) == false)
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index].ToolId, selectedToolId, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                return options[index].Kind;
            }
        }

        return options[0].Kind;
    }

    private static string ResolveToolIdByKind(List<LoadoutToolOption> options, ActiveToolKind selectedKind)
    {
        if (options == null)
            return string.Empty;

        for (int index = 0; index < options.Count; index++)
        {
            LoadoutToolOption option = options[index];

            if (option.Kind != selectedKind)
                continue;

            return option.ToolId;
        }

        return string.Empty;
    }

    private Foldout BuildBindingsFoldout(string foldoutTitle, SerializedProperty actionIdProperty)
    {
        Foldout foldout = new Foldout();
        foldout.text = foldoutTitle;
        foldout.value = true;

        if (actionIdProperty == null)
        {
            Label missingLabel = new Label("Missing action binding property.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            foldout.Add(missingLabel);
            return foldout;
        }

        if (presetSerializedObject == null)
        {
            Label missingLabel = new Label("Serialized preset object is not available.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            foldout.Add(missingLabel);
            return foldout;
        }

        if (inputAsset == null)
        {
            Label missingLabel = new Label("Input Action Asset is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            foldout.Add(missingLabel);
            return foldout;
        }

        InputActionSelectionElement bindingsElement = new InputActionSelectionElement(inputAsset, presetSerializedObject, actionIdProperty, InputActionSelectionElement.SelectionMode.PowerUps);
        foldout.Add(bindingsElement);
        return foldout;
    }

    private void EnsureDefaultActionId(SerializedProperty actionIdProperty, string actionName)
    {
        if (actionIdProperty == null)
            return;

        if (inputAsset == null)
            return;

        string currentActionId = actionIdProperty.stringValue;

        if (string.IsNullOrWhiteSpace(currentActionId) == false)
        {
            InputAction existingAction = inputAsset.FindAction(currentActionId, false);

            if (existingAction != null)
                return;
        }

        InputAction defaultAction = inputAsset.FindAction(actionName, false);

        if (defaultAction == null)
            return;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, "Assign Default Power Up Action");

        presetSerializedObject.Update();
        actionIdProperty.stringValue = defaultAction.id.ToString();
        presetSerializedObject.ApplyModifiedProperties();
    }

    private void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        if (presetSerializedObject == null)
            return;

        SerializedProperty presetIdProperty = presetSerializedObject.FindProperty("presetId");

        if (presetIdProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Power Ups Preset ID");
        presetSerializedObject.Update();
        presetIdProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
    }

    private void OnPresetSerializedChanged(SerializedObject changedObject)
    {
        if (changedObject == null)
            return;

        PlayerManagementDraftSession.MarkDirty();
        RefreshPresetList();
    }

    private string GetPresetDisplayName(PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string currentName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string currentVersion = preset.Version;

        if (string.IsNullOrWhiteSpace(currentVersion))
            return currentName;

        return currentName + " v. " + currentVersion;
    }
    #endregion

    #endregion

    #region Nested Types
    private enum SectionType
    {
        Metadata = 0,
        DropPools = 1,
        PassiveModifiers = 2,
        ActiveTools = 3,
        LoadoutInput = 4
    }

    private struct LoadoutToolOption
    {
        public ActiveToolKind Kind;
        public string ToolId;
    }
    #endregion
}
