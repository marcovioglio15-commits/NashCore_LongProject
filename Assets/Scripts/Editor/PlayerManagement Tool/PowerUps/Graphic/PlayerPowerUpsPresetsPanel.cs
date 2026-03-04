using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// UI panel for creating, editing, duplicating and deleting Player Power-Ups presets.
/// Used as side panel content by PlayerMasterPresetsPanel when Power-Ups section is opened.
/// </summary>
public sealed class PlayerPowerUpsPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.PlayerManagement.PowerUps.ActiveSection";
    private const float ModuleCardSpacing = 10f;
    private const float PowerUpCardSpacing = 10f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<PlayerPowerUpsPreset> filteredPresets = new List<PlayerPowerUpsPreset>();
    private readonly Dictionary<string, bool> moduleDefinitionFoldoutStateByKey = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> activePowerUpFoldoutStateByKey = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> passivePowerUpFoldoutStateByKey = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
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
    private string moduleIdFilterText = string.Empty;
    private string moduleDisplayNameFilterText = string.Empty;
    private string activePowerUpIdFilterText = string.Empty;
    private string activePowerUpDisplayNameFilterText = string.Empty;
    private string passivePowerUpIdFilterText = string.Empty;
    private string passivePowerUpDisplayNameFilterText = string.Empty;
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
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);

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

        string originalPath = AssetDatabase.GetAssetPath(preset);

        if (string.IsNullOrWhiteSpace(originalPath))
            return;

        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalDirectory))
            return;

        string sourceDisplayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string duplicateBaseName = PlayerManagementDraftSession.NormalizeAssetName(sourceDisplayName + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
            duplicateBaseName = "PlayerPowerUpsPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        string finalName = Path.GetFileNameWithoutExtension(duplicatedPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Power Ups Preset Asset");
        duplicatedPreset.name = finalName;

        SerializedObject duplicatedSerializedObject = new SerializedObject(duplicatedPreset);
        SerializedProperty presetIdProperty = duplicatedSerializedObject.FindProperty("presetId");
        SerializedProperty presetNameProperty = duplicatedSerializedObject.FindProperty("presetName");

        if (presetIdProperty != null)
            presetIdProperty.stringValue = Guid.NewGuid().ToString("N");

        if (presetNameProperty != null)
            presetNameProperty.stringValue = finalName;

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

        if (selectedPreset.EnsureDefaultModularSetup())
        {
            EditorUtility.SetDirty(selectedPreset);
            AssetDatabase.SaveAssetIfDirty(selectedPreset);
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
        AddSectionButton(buttonsRoot, SectionType.ModulesManagement, "Modules Management");
        AddSectionButton(buttonsRoot, SectionType.ActivePowerUps, "Active Power Ups");
        AddSectionButton(buttonsRoot, SectionType.PassivePowerUps, "Passive Power Ups");
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

    /// <summary>
    /// Sets active details section and persists the selection for reopen.
    /// Called by section tab buttons in power-ups preset details.
    /// Takes in the target section enum.
    /// </summary>
    /// <param name="sectionType">Section to activate.</param>
    private void SetActiveSection(SectionType sectionType)
    {
        // Persist selected section and rebuild detail content.
        activeSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, activeSection);
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
            case SectionType.ModulesManagement:
                BuildModulesManagementSection();
                return;
            case SectionType.ActivePowerUps:
                BuildActivePowerUpsSection();
                return;
            case SectionType.PassivePowerUps:
                BuildPassivePowerUpsSection();
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

    private void BuildModulesManagementSection()
    {
        Label header = new Label("Modules Management");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

        SerializedProperty moduleDefinitionsProperty = presetSerializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
        {
            Label missingLabel = new Label("Module definitions property is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        Label moduleInfoLabel = new Label("Reusable module catalog used by Active and Passive Power Ups.");
        moduleInfoLabel.style.marginBottom = 4f;
        sectionContentRoot.Add(moduleInfoLabel);

        VisualElement filtersRow = new VisualElement();
        filtersRow.style.flexDirection = FlexDirection.Row;
        filtersRow.style.alignItems = Align.FlexEnd;
        filtersRow.style.marginBottom = 4f;

        TextField moduleIdFilterField = new TextField("Filter Module ID");
        moduleIdFilterField.isDelayed = true;
        moduleIdFilterField.value = moduleIdFilterText;
        moduleIdFilterField.style.flexGrow = 1f;
        moduleIdFilterField.style.marginRight = 6f;
        moduleIdFilterField.tooltip = "Show only modules whose Module ID contains this text.";
        filtersRow.Add(moduleIdFilterField);

        TextField moduleDisplayNameFilterField = new TextField("Filter Display Name");
        moduleDisplayNameFilterField.isDelayed = true;
        moduleDisplayNameFilterField.value = moduleDisplayNameFilterText;
        moduleDisplayNameFilterField.style.flexGrow = 1f;
        moduleDisplayNameFilterField.tooltip = "Show only modules whose Display Name contains this text.";
        filtersRow.Add(moduleDisplayNameFilterField);

        sectionContentRoot.Add(filtersRow);

        Label moduleCountLabel = new Label();
        ScrollView moduleCardsContainer = new ScrollView();

        VisualElement moduleActionsRow = new VisualElement();
        moduleActionsRow.style.flexDirection = FlexDirection.Row;
        moduleActionsRow.style.alignItems = Align.Center;
        moduleActionsRow.style.marginBottom = 4f;

        Button addModuleButton = new Button(AddModuleDefinition);
        addModuleButton.text = "Add Module";
        addModuleButton.tooltip = "Create a new module definition entry.";
        moduleActionsRow.Add(addModuleButton);

        Button expandAllButton = new Button(() =>
        {
            SetAllModuleFoldoutStates(true);
            RebuildModuleDefinitionCards(moduleCardsContainer, moduleCountLabel);
        });
        expandAllButton.text = "Expand All";
        expandAllButton.tooltip = "Expand every visible module card.";
        expandAllButton.style.marginLeft = 4f;
        moduleActionsRow.Add(expandAllButton);

        Button collapseAllButton = new Button(() =>
        {
            SetAllModuleFoldoutStates(false);
            RebuildModuleDefinitionCards(moduleCardsContainer, moduleCountLabel);
        });
        collapseAllButton.text = "Collapse All";
        collapseAllButton.tooltip = "Collapse every visible module card.";
        collapseAllButton.style.marginLeft = 4f;
        moduleActionsRow.Add(collapseAllButton);

        Button clearFiltersButton = new Button(() =>
        {
            moduleIdFilterText = string.Empty;
            moduleDisplayNameFilterText = string.Empty;
            moduleIdFilterField.SetValueWithoutNotify(string.Empty);
            moduleDisplayNameFilterField.SetValueWithoutNotify(string.Empty);
            RebuildModuleDefinitionCards(moduleCardsContainer, moduleCountLabel);
        });
        clearFiltersButton.text = "Clear Filters";
        clearFiltersButton.tooltip = "Clear both module search filters.";
        clearFiltersButton.style.marginLeft = 4f;
        moduleActionsRow.Add(clearFiltersButton);

        sectionContentRoot.Add(moduleActionsRow);

        moduleCountLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        moduleCountLabel.style.marginBottom = 2f;
        sectionContentRoot.Add(moduleCountLabel);

        moduleCardsContainer.style.maxHeight = 520f;
        moduleCardsContainer.style.paddingRight = 2f;
        moduleCardsContainer.style.marginBottom = 2f;
        sectionContentRoot.Add(moduleCardsContainer);

        moduleIdFilterField.RegisterValueChangedCallback(evt =>
        {
            moduleIdFilterText = evt.newValue;
            RebuildModuleDefinitionCards(moduleCardsContainer, moduleCountLabel);
        });

        moduleDisplayNameFilterField.RegisterValueChangedCallback(evt =>
        {
            moduleDisplayNameFilterText = evt.newValue;
            RebuildModuleDefinitionCards(moduleCardsContainer, moduleCountLabel);
        });

        RebuildModuleDefinitionCards(moduleCardsContainer, moduleCountLabel);

        SerializedProperty elementalVfxByElementProperty = presetSerializedObject.FindProperty("elementalVfxByElement");

        if (elementalVfxByElementProperty == null)
            return;

        Label elementalVfxHeader = new Label("Elemental VFX Assignments");
        elementalVfxHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        elementalVfxHeader.style.marginTop = 8f;
        elementalVfxHeader.style.marginBottom = 2f;
        sectionContentRoot.Add(elementalVfxHeader);

        PropertyField elementalVfxField = new PropertyField(elementalVfxByElementProperty);
        elementalVfxField.BindProperty(elementalVfxByElementProperty);
        sectionContentRoot.Add(elementalVfxField);
    }

    private void RebuildModuleDefinitionCards(VisualElement moduleCardsContainer, Label moduleCountLabel)
    {
        if (moduleCardsContainer == null)
        {
            return;
        }

        moduleCardsContainer.Clear();

        if (presetSerializedObject == null)
        {
            if (moduleCountLabel != null)
            {
                moduleCountLabel.text = "Visible Modules: 0 / 0";
            }

            return;
        }

        SerializedProperty moduleDefinitionsProperty = presetSerializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
        {
            if (moduleCountLabel != null)
            {
                moduleCountLabel.text = "Visible Modules: 0 / 0";
            }

            HelpBox missingHelpBox = new HelpBox("Module definitions property is missing.", HelpBoxMessageType.Warning);
            moduleCardsContainer.Add(missingHelpBox);
            return;
        }

        int totalModules = moduleDefinitionsProperty.arraySize;
        int visibleModules = 0;
        HashSet<string> validFoldoutStateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int moduleIndex = 0; moduleIndex < moduleDefinitionsProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(moduleIndex);

            if (moduleProperty == null)
            {
                continue;
            }

            string moduleId = ResolveModuleDefinitionId(moduleProperty);
            string displayName = ResolveModuleDefinitionDisplayName(moduleProperty);
            PowerUpModuleKind moduleKind = ResolveModuleDefinitionKind(moduleProperty);
            string foldoutStateKey = BuildModuleFoldoutStateKey(moduleId, moduleIndex);
            validFoldoutStateKeys.Add(foldoutStateKey);

            if (IsMatchingModuleFilters(moduleId, displayName) == false)
            {
                continue;
            }

            VisualElement moduleCard = CreateModuleDefinitionCard(moduleDefinitionsProperty,
                                                                  moduleProperty,
                                                                  moduleIndex,
                                                                  moduleId,
                                                                  displayName,
                                                                  moduleKind);
            moduleCardsContainer.Add(moduleCard);
            visibleModules += 1;
        }

        PruneFoldoutStateMap(moduleDefinitionFoldoutStateByKey, validFoldoutStateKeys);

        if (moduleCountLabel != null)
        {
            moduleCountLabel.text = string.Format("Visible Modules: {0} / {1}", visibleModules, totalModules);
        }

        if (visibleModules > 0)
        {
            return;
        }

        HelpBox emptyHelpBox = new HelpBox("No modules match current filters.", HelpBoxMessageType.Info);
        moduleCardsContainer.Add(emptyHelpBox);
    }

    private VisualElement CreateModuleDefinitionCard(SerializedProperty moduleDefinitionsProperty,
                                                     SerializedProperty moduleProperty,
                                                     int moduleIndex,
                                                     string moduleId,
                                                     string displayName,
                                                     PowerUpModuleKind moduleKind)
    {
        VisualElement card = new VisualElement();
        card.style.marginBottom = ModuleCardSpacing;
        card.style.paddingLeft = 6f;
        card.style.paddingRight = 6f;
        card.style.paddingTop = 4f;
        card.style.paddingBottom = 4f;
        card.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
        card.style.borderBottomWidth = 1f;
        card.style.borderTopWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderBottomColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderTopColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);

        string foldoutStateKey = BuildModuleFoldoutStateKey(moduleId, moduleIndex);
        Foldout foldout = new Foldout();
        foldout.text = BuildModuleCardTitle(moduleIndex, moduleId, displayName, moduleKind);
        foldout.value = ResolveModuleFoldoutState(foldoutStateKey);
        foldout.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue)
            {
                moduleDefinitionFoldoutStateByKey[foldoutStateKey] = true;
                return;
            }

            moduleDefinitionFoldoutStateByKey.Remove(foldoutStateKey);
        });
        card.Add(foldout);

        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.marginLeft = 14f;
        actionsRow.style.marginTop = 2f;
        actionsRow.style.marginBottom = 4f;
        foldout.Add(actionsRow);

        Button duplicateButton = new Button(() =>
        {
            DuplicateModuleDefinition(moduleIndex);
        });
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate this module definition.";
        actionsRow.Add(duplicateButton);

        Button moveUpButton = new Button(() =>
        {
            MoveModuleDefinition(moduleIndex, moduleIndex - 1);
        });
        moveUpButton.text = "Up";
        moveUpButton.tooltip = "Move this module one position up.";
        moveUpButton.style.marginLeft = 4f;
        moveUpButton.SetEnabled(moduleIndex > 0);
        actionsRow.Add(moveUpButton);

        Button moveDownButton = new Button(() =>
        {
            MoveModuleDefinition(moduleIndex, moduleIndex + 1);
        });
        moveDownButton.text = "Down";
        moveDownButton.tooltip = "Move this module one position down.";
        moveDownButton.style.marginLeft = 4f;
        moveDownButton.SetEnabled(moduleIndex < moduleDefinitionsProperty.arraySize - 1);
        actionsRow.Add(moveDownButton);

        Button deleteButton = new Button(() =>
        {
            DeleteModuleDefinition(moduleIndex);
        });
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Delete this module definition.";
        deleteButton.style.marginLeft = 4f;
        actionsRow.Add(deleteButton);

        PropertyField moduleField = new PropertyField(moduleProperty);
        moduleField.BindProperty(moduleProperty);
        foldout.Add(moduleField);

        return card;
    }

    private void AddModuleDefinition()
    {
        ApplyModuleDefinitionsMutation("Add Module Definition", moduleDefinitionsProperty =>
        {
            int insertIndex = moduleDefinitionsProperty.arraySize;
            moduleDefinitionsProperty.arraySize = insertIndex + 1;
            SerializedProperty moduleProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(insertIndex);
            string uniqueModuleId = GenerateUniqueModuleId(moduleDefinitionsProperty, "ModuleCustom", insertIndex);
            InitializeNewModuleDefinition(moduleProperty, uniqueModuleId, "New Module");
            moduleDefinitionFoldoutStateByKey[BuildModuleFoldoutStateKey(uniqueModuleId, insertIndex)] = true;
        });
    }

    private void DuplicateModuleDefinition(int moduleIndex)
    {
        ApplyModuleDefinitionsMutation("Duplicate Module Definition", moduleDefinitionsProperty =>
        {
            if (moduleIndex < 0 || moduleIndex >= moduleDefinitionsProperty.arraySize)
            {
                return;
            }

            moduleDefinitionsProperty.InsertArrayElementAtIndex(moduleIndex);
            moduleDefinitionsProperty.MoveArrayElement(moduleIndex, moduleIndex + 1);
            int duplicatedIndex = moduleIndex + 1;
            SerializedProperty duplicatedProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(duplicatedIndex);
            string baseModuleId = ResolveModuleDefinitionId(duplicatedProperty);
            string copiedDisplayName = ResolveModuleDefinitionDisplayName(duplicatedProperty);
            string uniqueModuleId = GenerateUniqueModuleId(moduleDefinitionsProperty, baseModuleId, duplicatedIndex);
            SetModuleDefinitionId(duplicatedProperty, uniqueModuleId);

            if (string.IsNullOrWhiteSpace(copiedDisplayName))
            {
                copiedDisplayName = "New Module";
            }

            SetModuleDefinitionDisplayName(duplicatedProperty, copiedDisplayName + " Copy");
            moduleDefinitionFoldoutStateByKey[BuildModuleFoldoutStateKey(uniqueModuleId, duplicatedIndex)] = true;
        });
    }

    private void DeleteModuleDefinition(int moduleIndex)
    {
        ApplyModuleDefinitionsMutation("Delete Module Definition", moduleDefinitionsProperty =>
        {
            if (moduleIndex < 0 || moduleIndex >= moduleDefinitionsProperty.arraySize)
            {
                return;
            }

            moduleDefinitionsProperty.DeleteArrayElementAtIndex(moduleIndex);
        });
    }

    private void MoveModuleDefinition(int fromIndex, int toIndex)
    {
        ApplyModuleDefinitionsMutation("Move Module Definition", moduleDefinitionsProperty =>
        {
            if (fromIndex < 0 || fromIndex >= moduleDefinitionsProperty.arraySize)
            {
                return;
            }

            if (toIndex < 0 || toIndex >= moduleDefinitionsProperty.arraySize)
            {
                return;
            }

            if (fromIndex == toIndex)
            {
                return;
            }

            moduleDefinitionsProperty.MoveArrayElement(fromIndex, toIndex);
        });
    }

    private void SetAllModuleFoldoutStates(bool expanded)
    {
        if (presetSerializedObject == null)
        {
            return;
        }

        SerializedProperty moduleDefinitionsProperty = presetSerializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
        {
            return;
        }

        for (int moduleIndex = 0; moduleIndex < moduleDefinitionsProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(moduleIndex);

            if (moduleProperty == null)
                continue;

            string moduleId = ResolveModuleDefinitionId(moduleProperty);
            string displayName = ResolveModuleDefinitionDisplayName(moduleProperty);

            if (IsMatchingModuleFilters(moduleId, displayName) == false)
                continue;

            string stateKey = BuildModuleFoldoutStateKey(moduleId, moduleIndex);

            if (expanded)
            {
                moduleDefinitionFoldoutStateByKey[stateKey] = true;
                continue;
            }

            moduleDefinitionFoldoutStateByKey.Remove(stateKey);
        }
    }

    private void ApplyModuleDefinitionsMutation(string undoLabel, Action<SerializedProperty> mutation)
    {
        if (presetSerializedObject == null || mutation == null)
        {
            return;
        }

        SerializedProperty moduleDefinitionsProperty = presetSerializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
        {
            return;
        }

        if (selectedPreset != null)
        {
            Undo.RecordObject(selectedPreset, undoLabel);
        }

        presetSerializedObject.Update();
        mutation(moduleDefinitionsProperty);
        presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        BuildActiveSection();
    }

    private bool IsMatchingModuleFilters(string moduleId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(moduleIdFilterText) == false)
        {
            string resolvedModuleId = string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId;

            if (resolvedModuleId.IndexOf(moduleIdFilterText, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(moduleDisplayNameFilterText) == false)
        {
            string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;

            if (resolvedDisplayName.IndexOf(moduleDisplayNameFilterText, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildModuleCardTitle(int moduleIndex, string moduleId, string displayName, PowerUpModuleKind moduleKind)
    {
        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "<Unnamed>" : displayName.Trim();
        string resolvedModuleId = string.IsNullOrWhiteSpace(moduleId) ? "<No ID>" : moduleId.Trim();
        return string.Format("#{0:D2}  {1}  ({2})  [{3}]", moduleIndex + 1, resolvedDisplayName, resolvedModuleId, moduleKind);
    }

    private bool ResolveModuleFoldoutState(string foldoutStateKey)
    {
        if (string.IsNullOrWhiteSpace(foldoutStateKey))
        {
            return false;
        }

        bool isExpanded;

        if (moduleDefinitionFoldoutStateByKey.TryGetValue(foldoutStateKey, out isExpanded))
        {
            return isExpanded;
        }

        return false;
    }

    private static string BuildModuleFoldoutStateKey(string moduleId, int moduleIndex)
    {
        string normalizedModuleId = string.IsNullOrWhiteSpace(moduleId) ? "<NoId>" : moduleId.Trim();
        return string.Format("Module:Index_{0}:Id_{1}", moduleIndex, normalizedModuleId);
    }

    private static string ResolveModuleDefinitionId(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
        {
            return string.Empty;
        }

        SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
        {
            return string.Empty;
        }

        return moduleIdProperty.stringValue;
    }

    private static string ResolveModuleDefinitionDisplayName(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
        {
            return string.Empty;
        }

        SerializedProperty displayNameProperty = moduleProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
        {
            return string.Empty;
        }

        return displayNameProperty.stringValue;
    }

    private static PowerUpModuleKind ResolveModuleDefinitionKind(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
        {
            return default;
        }

        SerializedProperty moduleKindProperty = moduleProperty.FindPropertyRelative("moduleKind");

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
        {
            return default;
        }

        return (PowerUpModuleKind)moduleKindProperty.enumValueIndex;
    }

    private static string GenerateUniqueModuleId(SerializedProperty moduleDefinitionsProperty, string baseModuleId, int excludedIndex)
    {
        string sanitizedBaseModuleId = string.IsNullOrWhiteSpace(baseModuleId) ? "ModuleCustom" : baseModuleId.Trim();
        HashSet<string> existingModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int moduleIndex = 0; moduleIndex < moduleDefinitionsProperty.arraySize; moduleIndex++)
        {
            if (moduleIndex == excludedIndex)
            {
                continue;
            }

            SerializedProperty moduleProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(moduleIndex);
            string existingModuleId = ResolveModuleDefinitionId(moduleProperty);

            if (string.IsNullOrWhiteSpace(existingModuleId))
            {
                continue;
            }

            existingModuleIds.Add(existingModuleId.Trim());
        }

        if (existingModuleIds.Contains(sanitizedBaseModuleId) == false)
        {
            return sanitizedBaseModuleId;
        }

        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidateModuleId = sanitizedBaseModuleId + suffix.ToString();

            if (existingModuleIds.Contains(candidateModuleId))
            {
                continue;
            }

            return candidateModuleId;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static void InitializeNewModuleDefinition(SerializedProperty moduleProperty, string moduleId, string displayName)
    {
        if (moduleProperty == null)
        {
            return;
        }

        SetModuleDefinitionId(moduleProperty, moduleId);
        SetModuleDefinitionDisplayName(moduleProperty, displayName);
        SetModuleDefinitionKind(moduleProperty, PowerUpModuleKind.TriggerPress);
        SetModuleDefinitionStage(moduleProperty, PowerUpModuleStage.Trigger);
        SetModuleDefinitionNotes(moduleProperty, string.Empty);
    }

    private static void SetModuleDefinitionId(SerializedProperty moduleProperty, string moduleId)
    {
        if (moduleProperty == null)
        {
            return;
        }

        SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
        {
            return;
        }

        moduleIdProperty.stringValue = moduleId;
    }

    private static void SetModuleDefinitionDisplayName(SerializedProperty moduleProperty, string displayName)
    {
        if (moduleProperty == null)
        {
            return;
        }

        SerializedProperty displayNameProperty = moduleProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
        {
            return;
        }

        displayNameProperty.stringValue = displayName;
    }

    private static void SetModuleDefinitionKind(SerializedProperty moduleProperty, PowerUpModuleKind moduleKind)
    {
        if (moduleProperty == null)
        {
            return;
        }

        SerializedProperty moduleKindProperty = moduleProperty.FindPropertyRelative("moduleKind");

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
        {
            return;
        }

        moduleKindProperty.enumValueIndex = (int)moduleKind;
    }

    private static void SetModuleDefinitionStage(SerializedProperty moduleProperty, PowerUpModuleStage stage)
    {
        if (moduleProperty == null)
        {
            return;
        }

        SerializedProperty stageProperty = moduleProperty.FindPropertyRelative("defaultStage");

        if (stageProperty == null || stageProperty.propertyType != SerializedPropertyType.Enum)
        {
            return;
        }

        stageProperty.enumValueIndex = (int)stage;
    }

    private static void SetModuleDefinitionNotes(SerializedProperty moduleProperty, string notes)
    {
        if (moduleProperty == null)
        {
            return;
        }

        SerializedProperty notesProperty = moduleProperty.FindPropertyRelative("notes");

        if (notesProperty == null)
        {
            return;
        }

        notesProperty.stringValue = notes;
    }

    private void BuildActivePowerUpsSection()
    {
        Label header = new Label("Active Power Ups");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

        SerializedProperty activePowerUpsProperty = presetSerializedObject.FindProperty("activePowerUps");

        if (activePowerUpsProperty == null)
        {
            Label missingLabel = new Label("Active power ups property is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        Label infoLabel = new Label("Composable active entries assembled from module bindings.");
        infoLabel.style.marginBottom = 4f;
        sectionContentRoot.Add(infoLabel);

        VisualElement filtersRow = new VisualElement();
        filtersRow.style.flexDirection = FlexDirection.Row;
        filtersRow.style.alignItems = Align.FlexEnd;
        filtersRow.style.marginBottom = 4f;

        TextField powerUpIdFilterField = new TextField("Filter PowerUp ID");
        powerUpIdFilterField.isDelayed = true;
        powerUpIdFilterField.value = activePowerUpIdFilterText;
        powerUpIdFilterField.style.flexGrow = 1f;
        powerUpIdFilterField.style.marginRight = 6f;
        powerUpIdFilterField.tooltip = "Show only active power ups whose PowerUp ID contains this text.";
        filtersRow.Add(powerUpIdFilterField);

        TextField displayNameFilterField = new TextField("Filter Display Name");
        displayNameFilterField.isDelayed = true;
        displayNameFilterField.value = activePowerUpDisplayNameFilterText;
        displayNameFilterField.style.flexGrow = 1f;
        displayNameFilterField.tooltip = "Show only active power ups whose Display Name contains this text.";
        filtersRow.Add(displayNameFilterField);
        sectionContentRoot.Add(filtersRow);

        Label countLabel = new Label();
        ScrollView cardsContainer = new ScrollView();

        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.alignItems = Align.Center;
        actionsRow.style.marginBottom = 4f;

        Button addButton = new Button(() =>
        {
            AddPowerUpDefinition(true);
        });
        addButton.text = "Add Active";
        addButton.tooltip = "Create a new active power up entry.";
        actionsRow.Add(addButton);

        Button expandAllButton = new Button(() =>
        {
            SetAllPowerUpFoldoutStates(true, true);
            RebuildActivePowerUpCards(cardsContainer, countLabel);
        });
        expandAllButton.text = "Expand All";
        expandAllButton.tooltip = "Expand every visible active power up card.";
        expandAllButton.style.marginLeft = 4f;
        actionsRow.Add(expandAllButton);

        Button collapseAllButton = new Button(() =>
        {
            SetAllPowerUpFoldoutStates(true, false);
            RebuildActivePowerUpCards(cardsContainer, countLabel);
        });
        collapseAllButton.text = "Collapse All";
        collapseAllButton.tooltip = "Collapse every visible active power up card.";
        collapseAllButton.style.marginLeft = 4f;
        actionsRow.Add(collapseAllButton);

        Button clearFiltersButton = new Button(() =>
        {
            activePowerUpIdFilterText = string.Empty;
            activePowerUpDisplayNameFilterText = string.Empty;
            powerUpIdFilterField.SetValueWithoutNotify(string.Empty);
            displayNameFilterField.SetValueWithoutNotify(string.Empty);
            RebuildActivePowerUpCards(cardsContainer, countLabel);
        });
        clearFiltersButton.text = "Clear Filters";
        clearFiltersButton.tooltip = "Clear active power up search filters.";
        clearFiltersButton.style.marginLeft = 4f;
        actionsRow.Add(clearFiltersButton);
        sectionContentRoot.Add(actionsRow);

        countLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        countLabel.style.marginBottom = 2f;
        sectionContentRoot.Add(countLabel);

        cardsContainer.style.maxHeight = 620f;
        cardsContainer.style.paddingRight = 2f;
        sectionContentRoot.Add(cardsContainer);

        powerUpIdFilterField.RegisterValueChangedCallback(evt =>
        {
            activePowerUpIdFilterText = evt.newValue;
            RebuildActivePowerUpCards(cardsContainer, countLabel);
        });

        displayNameFilterField.RegisterValueChangedCallback(evt =>
        {
            activePowerUpDisplayNameFilterText = evt.newValue;
            RebuildActivePowerUpCards(cardsContainer, countLabel);
        });

        RebuildActivePowerUpCards(cardsContainer, countLabel);
    }

    private void BuildPassivePowerUpsSection()
    {
        Label header = new Label("Passive Power Ups");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

        SerializedProperty passivePowerUpsProperty = presetSerializedObject.FindProperty("passivePowerUps");

        if (passivePowerUpsProperty == null)
        {
            Label missingLabel = new Label("Passive power ups property is missing.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        Label infoLabel = new Label("Composable passive entries assembled from module bindings.");
        infoLabel.style.marginBottom = 4f;
        sectionContentRoot.Add(infoLabel);

        VisualElement filtersRow = new VisualElement();
        filtersRow.style.flexDirection = FlexDirection.Row;
        filtersRow.style.alignItems = Align.FlexEnd;
        filtersRow.style.marginBottom = 4f;

        TextField powerUpIdFilterField = new TextField("Filter PowerUp ID");
        powerUpIdFilterField.isDelayed = true;
        powerUpIdFilterField.value = passivePowerUpIdFilterText;
        powerUpIdFilterField.style.flexGrow = 1f;
        powerUpIdFilterField.style.marginRight = 6f;
        powerUpIdFilterField.tooltip = "Show only passive power ups whose PowerUp ID contains this text.";
        filtersRow.Add(powerUpIdFilterField);

        TextField displayNameFilterField = new TextField("Filter Display Name");
        displayNameFilterField.isDelayed = true;
        displayNameFilterField.value = passivePowerUpDisplayNameFilterText;
        displayNameFilterField.style.flexGrow = 1f;
        displayNameFilterField.tooltip = "Show only passive power ups whose Display Name contains this text.";
        filtersRow.Add(displayNameFilterField);
        sectionContentRoot.Add(filtersRow);

        Label countLabel = new Label();
        ScrollView cardsContainer = new ScrollView();

        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.alignItems = Align.Center;
        actionsRow.style.marginBottom = 4f;

        Button addButton = new Button(() =>
        {
            AddPowerUpDefinition(false);
        });
        addButton.text = "Add Passive";
        addButton.tooltip = "Create a new passive power up entry.";
        actionsRow.Add(addButton);

        Button expandAllButton = new Button(() =>
        {
            SetAllPowerUpFoldoutStates(false, true);
            RebuildPassivePowerUpCards(cardsContainer, countLabel);
        });
        expandAllButton.text = "Expand All";
        expandAllButton.tooltip = "Expand every visible passive power up card.";
        expandAllButton.style.marginLeft = 4f;
        actionsRow.Add(expandAllButton);

        Button collapseAllButton = new Button(() =>
        {
            SetAllPowerUpFoldoutStates(false, false);
            RebuildPassivePowerUpCards(cardsContainer, countLabel);
        });
        collapseAllButton.text = "Collapse All";
        collapseAllButton.tooltip = "Collapse every visible passive power up card.";
        collapseAllButton.style.marginLeft = 4f;
        actionsRow.Add(collapseAllButton);

        Button clearFiltersButton = new Button(() =>
        {
            passivePowerUpIdFilterText = string.Empty;
            passivePowerUpDisplayNameFilterText = string.Empty;
            powerUpIdFilterField.SetValueWithoutNotify(string.Empty);
            displayNameFilterField.SetValueWithoutNotify(string.Empty);
            RebuildPassivePowerUpCards(cardsContainer, countLabel);
        });
        clearFiltersButton.text = "Clear Filters";
        clearFiltersButton.tooltip = "Clear passive power up search filters.";
        clearFiltersButton.style.marginLeft = 4f;
        actionsRow.Add(clearFiltersButton);
        sectionContentRoot.Add(actionsRow);

        countLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        countLabel.style.marginBottom = 2f;
        sectionContentRoot.Add(countLabel);

        cardsContainer.style.maxHeight = 620f;
        cardsContainer.style.paddingRight = 2f;
        sectionContentRoot.Add(cardsContainer);

        powerUpIdFilterField.RegisterValueChangedCallback(evt =>
        {
            passivePowerUpIdFilterText = evt.newValue;
            RebuildPassivePowerUpCards(cardsContainer, countLabel);
        });

        displayNameFilterField.RegisterValueChangedCallback(evt =>
        {
            passivePowerUpDisplayNameFilterText = evt.newValue;
            RebuildPassivePowerUpCards(cardsContainer, countLabel);
        });

        RebuildPassivePowerUpCards(cardsContainer, countLabel);
    }

    private void RebuildActivePowerUpCards(VisualElement cardsContainer, Label countLabel)
    {
        RebuildPowerUpDefinitionCards(cardsContainer,
                                      countLabel,
                                      true,
                                      activePowerUpFoldoutStateByKey,
                                      activePowerUpIdFilterText,
                                      activePowerUpDisplayNameFilterText);
    }

    private void RebuildPassivePowerUpCards(VisualElement cardsContainer, Label countLabel)
    {
        RebuildPowerUpDefinitionCards(cardsContainer,
                                      countLabel,
                                      false,
                                      passivePowerUpFoldoutStateByKey,
                                      passivePowerUpIdFilterText,
                                      passivePowerUpDisplayNameFilterText);
    }

    private void RebuildPowerUpDefinitionCards(VisualElement cardsContainer,
                                               Label countLabel,
                                               bool isActiveSection,
                                               Dictionary<string, bool> foldoutStateByKey,
                                               string powerUpIdFilterValue,
                                               string displayNameFilterValue)
    {
        if (cardsContainer == null)
            return;

        cardsContainer.Clear();

        if (presetSerializedObject == null)
        {
            if (countLabel != null)
                countLabel.text = "Visible Entries: 0 / 0";

            return;
        }

        string propertyName = ResolvePowerUpsPropertyName(isActiveSection);
        SerializedProperty powerUpsProperty = presetSerializedObject.FindProperty(propertyName);

        if (powerUpsProperty == null)
        {
            if (countLabel != null)
                countLabel.text = "Visible Entries: 0 / 0";

            HelpBox missingHelpBox = new HelpBox("Power ups property is missing.", HelpBoxMessageType.Warning);
            cardsContainer.Add(missingHelpBox);
            return;
        }

        int totalCount = powerUpsProperty.arraySize;
        int visibleCount = 0;
        HashSet<string> validFoldoutStateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById = PowerUpModuleCatalogUtility.BuildCatalogById(presetSerializedObject);

        for (int powerUpIndex = 0; powerUpIndex < powerUpsProperty.arraySize; powerUpIndex++)
        {
            SerializedProperty powerUpProperty = powerUpsProperty.GetArrayElementAtIndex(powerUpIndex);

            if (powerUpProperty == null)
                continue;

            string powerUpId = ResolvePowerUpDefinitionId(powerUpProperty);
            string displayName = ResolvePowerUpDefinitionDisplayName(powerUpProperty);
            string foldoutStateKey = BuildPowerUpFoldoutStateKey(isActiveSection, powerUpId, powerUpIndex);
            validFoldoutStateKeys.Add(foldoutStateKey);

            if (IsMatchingPowerUpFilters(powerUpId, displayName, powerUpIdFilterValue, displayNameFilterValue) == false)
                continue;

            VisualElement card = CreatePowerUpDefinitionCard(powerUpsProperty,
                                                             powerUpProperty,
                                                             powerUpIndex,
                                                             isActiveSection,
                                                             foldoutStateByKey,
                                                             moduleCatalogById);
            cardsContainer.Add(card);
            visibleCount += 1;
        }

        PruneFoldoutStateMap(foldoutStateByKey, validFoldoutStateKeys);

        if (countLabel != null)
            countLabel.text = string.Format("Visible Entries: {0} / {1}", visibleCount, totalCount);

        if (visibleCount > 0)
            return;

        string emptyMessage = isActiveSection
            ? "No active power ups match current filters."
            : "No passive power ups match current filters.";
        HelpBox emptyHelpBox = new HelpBox(emptyMessage, HelpBoxMessageType.Info);
        cardsContainer.Add(emptyHelpBox);
    }

    private VisualElement CreatePowerUpDefinitionCard(SerializedProperty powerUpsProperty,
                                                      SerializedProperty powerUpProperty,
                                                      int powerUpIndex,
                                                      bool isActiveSection,
                                                      Dictionary<string, bool> foldoutStateByKey,
                                                      Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById)
    {
        VisualElement card = new VisualElement();
        card.style.marginBottom = PowerUpCardSpacing;
        card.style.paddingLeft = 6f;
        card.style.paddingRight = 6f;
        card.style.paddingTop = 4f;
        card.style.paddingBottom = 4f;
        card.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
        card.style.borderBottomWidth = 1f;
        card.style.borderTopWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderBottomColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderTopColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        card.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);

        string powerUpId = ResolvePowerUpDefinitionId(powerUpProperty);
        string displayName = ResolvePowerUpDefinitionDisplayName(powerUpProperty);
        int bindingCount = ResolvePowerUpDefinitionBindingCount(powerUpProperty);
        bool unreplaceable = ResolvePowerUpDefinitionUnreplaceable(powerUpProperty);
        string foldoutStateKey = BuildPowerUpFoldoutStateKey(isActiveSection, powerUpId, powerUpIndex);
        Foldout foldout = new Foldout();
        foldout.text = BuildPowerUpCardTitle(powerUpIndex, powerUpId, displayName, bindingCount, unreplaceable);
        foldout.value = ResolvePowerUpFoldoutState(foldoutStateByKey, foldoutStateKey);
        foldout.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue)
            {
                foldoutStateByKey[foldoutStateKey] = true;
                return;
            }

            foldoutStateByKey.Remove(foldoutStateKey);
        });
        card.Add(foldout);

        VisualElement actionsRow = new VisualElement();
        actionsRow.style.flexDirection = FlexDirection.Row;
        actionsRow.style.marginLeft = 14f;
        actionsRow.style.marginTop = 2f;
        actionsRow.style.marginBottom = 4f;
        foldout.Add(actionsRow);

        Button duplicateButton = new Button(() =>
        {
            DuplicatePowerUpDefinition(isActiveSection, powerUpIndex);
        });
        duplicateButton.text = "Duplicate";
        duplicateButton.tooltip = "Duplicate this power up entry.";
        actionsRow.Add(duplicateButton);

        Button moveUpButton = new Button(() =>
        {
            MovePowerUpDefinition(isActiveSection, powerUpIndex, powerUpIndex - 1);
        });
        moveUpButton.text = "Up";
        moveUpButton.tooltip = "Move this power up one position up.";
        moveUpButton.style.marginLeft = 4f;
        moveUpButton.SetEnabled(powerUpIndex > 0);
        actionsRow.Add(moveUpButton);

        Button moveDownButton = new Button(() =>
        {
            MovePowerUpDefinition(isActiveSection, powerUpIndex, powerUpIndex + 1);
        });
        moveDownButton.text = "Down";
        moveDownButton.tooltip = "Move this power up one position down.";
        moveDownButton.style.marginLeft = 4f;
        moveDownButton.SetEnabled(powerUpIndex < powerUpsProperty.arraySize - 1);
        actionsRow.Add(moveDownButton);

        Button deleteButton = new Button(() =>
        {
            DeletePowerUpDefinition(isActiveSection, powerUpIndex);
        });
        deleteButton.text = "Delete";
        deleteButton.tooltip = "Delete this power up entry.";
        deleteButton.style.marginLeft = 4f;
        actionsRow.Add(deleteButton);

        HelpBox coverageWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        coverageWarningBox.style.marginLeft = 14f;
        coverageWarningBox.style.marginBottom = 4f;
        foldout.Add(coverageWarningBox);

        UpdatePowerUpCardPresentation(powerUpProperty,
                                      powerUpIndex,
                                      isActiveSection,
                                      foldout,
                                      coverageWarningBox,
                                      moduleCatalogById);

        card.TrackSerializedObjectValue(powerUpProperty.serializedObject, changedObject =>
        {
            if (changedObject == null)
                return;

            Dictionary<string, PowerUpModuleCatalogEntry> updatedModuleCatalogById = PowerUpModuleCatalogUtility.BuildCatalogById(changedObject);
            UpdatePowerUpCardPresentation(powerUpProperty,
                                          powerUpIndex,
                                          isActiveSection,
                                          foldout,
                                          coverageWarningBox,
                                          updatedModuleCatalogById);
        });

        PropertyField powerUpField = new PropertyField(powerUpProperty);
        powerUpField.BindProperty(powerUpProperty);
        powerUpField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            if (evt == null)
                return;

            Dictionary<string, PowerUpModuleCatalogEntry> updatedModuleCatalogById = PowerUpModuleCatalogUtility.BuildCatalogById(powerUpProperty.serializedObject);
            UpdatePowerUpCardPresentation(powerUpProperty,
                                          powerUpIndex,
                                          isActiveSection,
                                          foldout,
                                          coverageWarningBox,
                                          updatedModuleCatalogById);
        });
        foldout.Add(powerUpField);

        return card;
    }

    private void AddPowerUpDefinition(bool isActiveSection)
    {
        string undoLabel = isActiveSection ? "Add Active Power Up" : "Add Passive Power Up";

        ApplyPowerUpDefinitionsMutation(undoLabel, isActiveSection, powerUpsProperty =>
        {
            int insertIndex = powerUpsProperty.arraySize;
            powerUpsProperty.arraySize = insertIndex + 1;
            SerializedProperty insertedProperty = powerUpsProperty.GetArrayElementAtIndex(insertIndex);
            string basePowerUpId = isActiveSection ? "ActivePowerUpCustom" : "PassivePowerUpCustom";
            string uniquePowerUpId = GenerateUniquePowerUpId(powerUpsProperty, basePowerUpId, insertIndex);
            string defaultDisplayName = isActiveSection ? "New Active Power Up" : "New Passive Power Up";
            InitializeNewPowerUpDefinition(insertedProperty, uniquePowerUpId, defaultDisplayName);
            Dictionary<string, bool> foldoutStateByKey = ResolvePowerUpFoldoutStateMap(isActiveSection);
            string foldoutKey = BuildPowerUpFoldoutStateKey(isActiveSection, uniquePowerUpId, insertIndex);
            foldoutStateByKey[foldoutKey] = true;
        });
    }

    private void DuplicatePowerUpDefinition(bool isActiveSection, int sourceIndex)
    {
        string undoLabel = isActiveSection ? "Duplicate Active Power Up" : "Duplicate Passive Power Up";

        ApplyPowerUpDefinitionsMutation(undoLabel, isActiveSection, powerUpsProperty =>
        {
            if (sourceIndex < 0 || sourceIndex >= powerUpsProperty.arraySize)
                return;

            powerUpsProperty.InsertArrayElementAtIndex(sourceIndex);
            powerUpsProperty.MoveArrayElement(sourceIndex, sourceIndex + 1);
            int duplicatedIndex = sourceIndex + 1;
            SerializedProperty duplicatedProperty = powerUpsProperty.GetArrayElementAtIndex(duplicatedIndex);
            string basePowerUpId = ResolvePowerUpDefinitionId(duplicatedProperty);
            string copiedDisplayName = ResolvePowerUpDefinitionDisplayName(duplicatedProperty);
            string uniquePowerUpId = GenerateUniquePowerUpId(powerUpsProperty, basePowerUpId, duplicatedIndex);
            SetPowerUpDefinitionId(duplicatedProperty, uniquePowerUpId);

            if (string.IsNullOrWhiteSpace(copiedDisplayName))
                copiedDisplayName = isActiveSection ? "New Active Power Up" : "New Passive Power Up";

            SetPowerUpDefinitionDisplayName(duplicatedProperty, copiedDisplayName + " Copy");
            Dictionary<string, bool> foldoutStateByKey = ResolvePowerUpFoldoutStateMap(isActiveSection);
            string foldoutKey = BuildPowerUpFoldoutStateKey(isActiveSection, uniquePowerUpId, duplicatedIndex);
            foldoutStateByKey[foldoutKey] = true;
        });
    }

    private void DeletePowerUpDefinition(bool isActiveSection, int index)
    {
        string undoLabel = isActiveSection ? "Delete Active Power Up" : "Delete Passive Power Up";

        ApplyPowerUpDefinitionsMutation(undoLabel, isActiveSection, powerUpsProperty =>
        {
            if (index < 0 || index >= powerUpsProperty.arraySize)
                return;

            powerUpsProperty.DeleteArrayElementAtIndex(index);
        });
    }

    private void MovePowerUpDefinition(bool isActiveSection, int fromIndex, int toIndex)
    {
        string undoLabel = isActiveSection ? "Move Active Power Up" : "Move Passive Power Up";

        ApplyPowerUpDefinitionsMutation(undoLabel, isActiveSection, powerUpsProperty =>
        {
            if (fromIndex < 0 || fromIndex >= powerUpsProperty.arraySize)
                return;

            if (toIndex < 0 || toIndex >= powerUpsProperty.arraySize)
                return;

            if (fromIndex == toIndex)
                return;

            powerUpsProperty.MoveArrayElement(fromIndex, toIndex);
        });
    }

    private void SetAllPowerUpFoldoutStates(bool isActiveSection, bool expanded)
    {
        if (presetSerializedObject == null)
            return;

        string propertyName = ResolvePowerUpsPropertyName(isActiveSection);
        SerializedProperty powerUpsProperty = presetSerializedObject.FindProperty(propertyName);

        if (powerUpsProperty == null)
            return;

        Dictionary<string, bool> foldoutStateByKey = ResolvePowerUpFoldoutStateMap(isActiveSection);
        string powerUpIdFilterValue = isActiveSection ? activePowerUpIdFilterText : passivePowerUpIdFilterText;
        string displayNameFilterValue = isActiveSection ? activePowerUpDisplayNameFilterText : passivePowerUpDisplayNameFilterText;

        for (int powerUpIndex = 0; powerUpIndex < powerUpsProperty.arraySize; powerUpIndex++)
        {
            SerializedProperty powerUpProperty = powerUpsProperty.GetArrayElementAtIndex(powerUpIndex);

            if (powerUpProperty == null)
                continue;

            string powerUpId = ResolvePowerUpDefinitionId(powerUpProperty);
            string displayName = ResolvePowerUpDefinitionDisplayName(powerUpProperty);

            if (IsMatchingPowerUpFilters(powerUpId, displayName, powerUpIdFilterValue, displayNameFilterValue) == false)
                continue;

            string foldoutKey = BuildPowerUpFoldoutStateKey(isActiveSection, powerUpId, powerUpIndex);

            if (expanded)
            {
                foldoutStateByKey[foldoutKey] = true;
                continue;
            }

            foldoutStateByKey.Remove(foldoutKey);
        }
    }

    private void ApplyPowerUpDefinitionsMutation(string undoLabel, bool isActiveSection, Action<SerializedProperty> mutation)
    {
        if (presetSerializedObject == null || mutation == null)
            return;

        string propertyName = ResolvePowerUpsPropertyName(isActiveSection);
        SerializedProperty powerUpsProperty = presetSerializedObject.FindProperty(propertyName);

        if (powerUpsProperty == null)
            return;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, undoLabel);

        presetSerializedObject.Update();
        mutation(powerUpsProperty);
        presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        BuildActiveSection();
    }

    private static string ResolvePowerUpsPropertyName(bool isActiveSection)
    {
        return isActiveSection ? "activePowerUps" : "passivePowerUps";
    }

    private Dictionary<string, bool> ResolvePowerUpFoldoutStateMap(bool isActiveSection)
    {
        return isActiveSection ? activePowerUpFoldoutStateByKey : passivePowerUpFoldoutStateByKey;
    }

    private static void PruneFoldoutStateMap(Dictionary<string, bool> foldoutStateByKey, HashSet<string> validStateKeys)
    {
        if (foldoutStateByKey == null || validStateKeys == null)
            return;

        List<string> keysToRemove = new List<string>();

        foreach (KeyValuePair<string, bool> entry in foldoutStateByKey)
        {
            if (validStateKeys.Contains(entry.Key))
                continue;

            keysToRemove.Add(entry.Key);
        }

        for (int index = 0; index < keysToRemove.Count; index++)
            foldoutStateByKey.Remove(keysToRemove[index]);
    }

    private static bool ResolvePowerUpFoldoutState(Dictionary<string, bool> foldoutStateByKey, string foldoutStateKey)
    {
        if (foldoutStateByKey == null || string.IsNullOrWhiteSpace(foldoutStateKey))
            return false;

        bool isExpanded;

        if (foldoutStateByKey.TryGetValue(foldoutStateKey, out isExpanded))
            return isExpanded;

        return false;
    }

    private static bool IsMatchingPowerUpFilters(string powerUpId, string displayName, string powerUpIdFilterValue, string displayNameFilterValue)
    {
        if (string.IsNullOrWhiteSpace(powerUpIdFilterValue) == false)
        {
            string resolvedPowerUpId = string.IsNullOrWhiteSpace(powerUpId) ? string.Empty : powerUpId;

            if (resolvedPowerUpId.IndexOf(powerUpIdFilterValue, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        if (string.IsNullOrWhiteSpace(displayNameFilterValue) == false)
        {
            string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;

            if (resolvedDisplayName.IndexOf(displayNameFilterValue, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    private static string BuildPowerUpCardTitle(int powerUpIndex, string powerUpId, string displayName, int bindingCount, bool unreplaceable)
    {
        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "<Unnamed>" : displayName.Trim();
        string resolvedPowerUpId = string.IsNullOrWhiteSpace(powerUpId) ? "<No ID>" : powerUpId.Trim();
        string lockTag = unreplaceable ? " | Unreplaceable" : string.Empty;
        return string.Format("#{0:D2}  {1}  ({2})  [{3} Modules{4}]",
                             powerUpIndex + 1,
                             resolvedDisplayName,
                             resolvedPowerUpId,
                             bindingCount,
                             lockTag);
    }

    private static void UpdatePowerUpCardPresentation(SerializedProperty powerUpProperty,
                                                      int powerUpIndex,
                                                      bool isActiveSection,
                                                      Foldout foldout,
                                                      HelpBox coverageWarningBox,
                                                      Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById)
    {
        if (powerUpProperty == null)
            return;

        if (foldout == null)
            return;

        string powerUpId = ResolvePowerUpDefinitionId(powerUpProperty);
        string displayName = ResolvePowerUpDefinitionDisplayName(powerUpProperty);
        int bindingCount = ResolvePowerUpDefinitionBindingCount(powerUpProperty);
        bool unreplaceable = ResolvePowerUpDefinitionUnreplaceable(powerUpProperty);
        foldout.text = BuildPowerUpCardTitle(powerUpIndex, powerUpId, displayName, bindingCount, unreplaceable);

        if (coverageWarningBox == null)
            return;

        string coverageWarning = BuildPowerUpCoverageWarning(powerUpProperty, isActiveSection, moduleCatalogById);

        if (string.IsNullOrWhiteSpace(coverageWarning))
        {
            coverageWarningBox.style.display = DisplayStyle.None;
            coverageWarningBox.text = string.Empty;
            return;
        }

        coverageWarningBox.style.display = DisplayStyle.Flex;
        coverageWarningBox.text = coverageWarning;
    }

    private static string BuildPowerUpFoldoutStateKey(bool isActiveSection, string powerUpId, int powerUpIndex)
    {
        string prefix = isActiveSection ? "Active:" : "Passive:";
        string normalizedPowerUpId = string.IsNullOrWhiteSpace(powerUpId) ? "<NoId>" : powerUpId.Trim();
        return string.Format("{0}Index_{1}:Id_{2}", prefix, powerUpIndex, normalizedPowerUpId);
    }

    private static string ResolvePowerUpDefinitionId(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return string.Empty;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");

        if (commonDataProperty == null)
            return string.Empty;

        SerializedProperty powerUpIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");

        if (powerUpIdProperty == null)
            return string.Empty;

        return powerUpIdProperty.stringValue;
    }

    private static string ResolvePowerUpDefinitionDisplayName(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return string.Empty;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");

        if (commonDataProperty == null)
            return string.Empty;

        SerializedProperty displayNameProperty = commonDataProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return string.Empty;

        return displayNameProperty.stringValue;
    }

    private static int ResolvePowerUpDefinitionBindingCount(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return 0;

        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null)
            return 0;

        return moduleBindingsProperty.arraySize;
    }

    private static bool ResolvePowerUpDefinitionUnreplaceable(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return false;

        SerializedProperty unreplaceableProperty = powerUpProperty.FindPropertyRelative("unreplaceable");

        if (unreplaceableProperty == null)
            return false;

        return unreplaceableProperty.boolValue;
    }

    private static string BuildPowerUpCoverageWarning(SerializedProperty powerUpProperty,
                                                      bool isActiveSection,
                                                      Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById)
    {
        if (powerUpProperty == null)
            return string.Empty;

        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null)
            return string.Empty;

        if (moduleBindingsProperty.arraySize <= 0)
            return string.Empty;

        HashSet<PowerUpModuleKind> moduleKinds = new HashSet<PowerUpModuleKind>();
        List<string> unresolvedModuleIds = new List<string>();
        List<string> unsupportedModuleKinds = new List<string>();

        for (int bindingIndex = 0; bindingIndex < moduleBindingsProperty.arraySize; bindingIndex++)
        {
            SerializedProperty bindingProperty = moduleBindingsProperty.GetArrayElementAtIndex(bindingIndex);

            if (bindingProperty == null)
                continue;

            SerializedProperty isEnabledProperty = bindingProperty.FindPropertyRelative("isEnabled");

            if (isEnabledProperty != null && isEnabledProperty.boolValue == false)
                continue;

            string moduleId = ModularPowerUpBindingDrawerUtility.ResolveBindingModuleId(bindingProperty);

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            string normalizedModuleId = moduleId.Trim();
            PowerUpModuleCatalogEntry moduleEntry;

            if (PowerUpModuleCatalogUtility.TryResolveModuleInfo(moduleCatalogById, normalizedModuleId, out moduleEntry) == false)
            {
                AddUniqueString(unresolvedModuleIds, normalizedModuleId);
                continue;
            }

            moduleKinds.Add(moduleEntry.ModuleKind);

            if (IsModuleKindSupportedInSection(moduleEntry.ModuleKind, isActiveSection))
                continue;

            AddUniqueString(unsupportedModuleKinds, PowerUpModuleEnumDescriptions.FormatModuleKindOption(moduleEntry.ModuleKind));
        }

        List<string> warningLines = new List<string>();

        if (unresolvedModuleIds.Count > 0)
            warningLines.Add("Missing module IDs in catalog: " + string.Join(", ", unresolvedModuleIds));

        if (unsupportedModuleKinds.Count > 0)
        {
            string unsupportedPrefix = isActiveSection
                ? "Ignored in Active runtime: "
                : "Ignored in Passive runtime: ";
            warningLines.Add(unsupportedPrefix + string.Join(", ", unsupportedModuleKinds));
        }

        string contextWarning = isActiveSection
            ? BuildActiveCoverageWarning(moduleKinds)
            : BuildPassiveCoverageWarning(moduleKinds);

        if (string.IsNullOrWhiteSpace(contextWarning) == false)
            warningLines.Add(contextWarning);

        if (warningLines.Count <= 0)
            return string.Empty;

        return string.Join("\n", warningLines);
    }

    private static bool IsModuleKindSupportedInSection(PowerUpModuleKind moduleKind, bool isActiveSection)
    {
        if (isActiveSection)
        {
            switch (moduleKind)
            {
                case PowerUpModuleKind.TriggerPress:
                case PowerUpModuleKind.TriggerRelease:
                case PowerUpModuleKind.TriggerHoldCharge:
                case PowerUpModuleKind.GateResource:
                case PowerUpModuleKind.StateSuppressShooting:
                case PowerUpModuleKind.ProjectilesPatternCone:
                case PowerUpModuleKind.ProjectilesTuning:
                case PowerUpModuleKind.SpawnObject:
                case PowerUpModuleKind.DeathExplosion:
                case PowerUpModuleKind.Dash:
                case PowerUpModuleKind.TimeDilationEnemies:
                case PowerUpModuleKind.Heal:
                    return true;
                default:
                    return false;
            }
        }

        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerEvent:
            case PowerUpModuleKind.GateResource:
            case PowerUpModuleKind.Heal:
            case PowerUpModuleKind.ProjectilesPatternCone:
            case PowerUpModuleKind.ProjectilesTuning:
            case PowerUpModuleKind.SpawnTrailSegment:
            case PowerUpModuleKind.AreaTickApplyElement:
            case PowerUpModuleKind.DeathExplosion:
            case PowerUpModuleKind.OrbitalProjectiles:
            case PowerUpModuleKind.BouncingProjectiles:
            case PowerUpModuleKind.ProjectileSplit:
                return true;
            default:
                return false;
        }
    }

    private static string BuildActiveCoverageWarning(HashSet<PowerUpModuleKind> moduleKinds)
    {
        if (moduleKinds == null || moduleKinds.Count <= 0)
            return string.Empty;

        List<string> warningLines = new List<string>();
        bool hasHoldCharge = moduleKinds.Contains(PowerUpModuleKind.TriggerHoldCharge);
        bool hasShotgunPattern = moduleKinds.Contains(PowerUpModuleKind.ProjectilesPatternCone);
        bool hasSpawnObject = moduleKinds.Contains(PowerUpModuleKind.SpawnObject);
        bool hasDash = moduleKinds.Contains(PowerUpModuleKind.Dash);
        bool hasBulletTime = moduleKinds.Contains(PowerUpModuleKind.TimeDilationEnemies);
        bool hasHeal = moduleKinds.Contains(PowerUpModuleKind.Heal);
        bool hasDeathExplosion = moduleKinds.Contains(PowerUpModuleKind.DeathExplosion);
        bool hasTriggerEvent = moduleKinds.Contains(PowerUpModuleKind.TriggerEvent);
        bool hasTriggerPress = moduleKinds.Contains(PowerUpModuleKind.TriggerPress);
        bool hasTriggerRelease = moduleKinds.Contains(PowerUpModuleKind.TriggerRelease);
        int executeKindCount = 0;

        if (hasHoldCharge)
            executeKindCount += 1;

        if (hasShotgunPattern)
            executeKindCount += 1;

        if (hasSpawnObject)
            executeKindCount += 1;

        if (hasDash)
            executeKindCount += 1;

        if (hasBulletTime)
            executeKindCount += 1;

        if (hasHeal)
            executeKindCount += 1;

        if (executeKindCount == 0)
            warningLines.Add("No execute module selected. This active power up compiles as undefined.");

        if (executeKindCount > 1)
            warningLines.Add("Multiple execute modules found. Runtime priority is: TriggerHoldCharge > ProjectilesPatternCone > SpawnObject > Dash > TimeDilationEnemies > Heal.");

        if (hasDeathExplosion && hasSpawnObject == false)
            warningLines.Add("DeathExplosion is ignored unless SpawnObject is also bound.");

        if (hasTriggerEvent)
            warningLines.Add("TriggerEvent currently has no active runtime consumer.");

        if (hasTriggerPress && hasTriggerRelease)
            warningLines.Add("TriggerPress and TriggerRelease are both bound. Runtime uses OnPress activation.");

        if (warningLines.Count <= 0)
            return string.Empty;

        return string.Join("\n", warningLines);
    }

    private static string BuildPassiveCoverageWarning(HashSet<PowerUpModuleKind> moduleKinds)
    {
        if (moduleKinds == null || moduleKinds.Count <= 0)
            return string.Empty;

        List<string> warningLines = new List<string>();
        bool hasTrail = moduleKinds.Contains(PowerUpModuleKind.SpawnTrailSegment) || moduleKinds.Contains(PowerUpModuleKind.AreaTickApplyElement);
        bool hasExplosion = moduleKinds.Contains(PowerUpModuleKind.DeathExplosion);
        bool hasOrbit = moduleKinds.Contains(PowerUpModuleKind.OrbitalProjectiles);
        bool hasBounce = moduleKinds.Contains(PowerUpModuleKind.BouncingProjectiles);
        bool hasSplit = moduleKinds.Contains(PowerUpModuleKind.ProjectileSplit);
        bool hasShotgun = moduleKinds.Contains(PowerUpModuleKind.ProjectilesPatternCone) || moduleKinds.Contains(PowerUpModuleKind.ProjectilesTuning);
        bool hasHeal = moduleKinds.Contains(PowerUpModuleKind.Heal);
        bool hasGateResource = moduleKinds.Contains(PowerUpModuleKind.GateResource);
        bool hasTriggerEvent = moduleKinds.Contains(PowerUpModuleKind.TriggerEvent);
        bool hasAnyPassiveRuntimeConsumer = hasTrail || hasExplosion || hasOrbit || hasBounce || hasSplit || hasShotgun || hasHeal;

        if (hasAnyPassiveRuntimeConsumer == false)
            warningLines.Add("No passive runtime module found. This passive power up compiles as undefined.");

        if (hasTriggerEvent && hasExplosion == false && hasSplit == false && hasHeal == false)
            warningLines.Add("TriggerEvent has no passive consumer without DeathExplosion, ProjectileSplit, or Heal.");

        if (hasGateResource && hasHeal == false)
            warningLines.Add("GateResource in Passive currently only contributes cooldown data to Heal modules.");

        if (warningLines.Count <= 0)
            return string.Empty;

        return string.Join("\n", warningLines);
    }

    private static void AddUniqueString(List<string> values, string candidateValue)
    {
        if (values == null)
            return;

        if (string.IsNullOrWhiteSpace(candidateValue))
            return;

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], candidateValue, StringComparison.OrdinalIgnoreCase))
                return;
        }

        values.Add(candidateValue);
    }

    private static string GenerateUniquePowerUpId(SerializedProperty powerUpsProperty, string basePowerUpId, int excludedIndex)
    {
        string sanitizedBasePowerUpId = string.IsNullOrWhiteSpace(basePowerUpId) ? "PowerUpCustom" : basePowerUpId.Trim();
        HashSet<string> existingPowerUpIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int powerUpIndex = 0; powerUpIndex < powerUpsProperty.arraySize; powerUpIndex++)
        {
            if (powerUpIndex == excludedIndex)
                continue;

            SerializedProperty powerUpProperty = powerUpsProperty.GetArrayElementAtIndex(powerUpIndex);
            string existingPowerUpId = ResolvePowerUpDefinitionId(powerUpProperty);

            if (string.IsNullOrWhiteSpace(existingPowerUpId))
                continue;

            existingPowerUpIds.Add(existingPowerUpId.Trim());
        }

        if (existingPowerUpIds.Contains(sanitizedBasePowerUpId) == false)
            return sanitizedBasePowerUpId;

        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidatePowerUpId = sanitizedBasePowerUpId + suffix.ToString();

            if (existingPowerUpIds.Contains(candidatePowerUpId))
                continue;

            return candidatePowerUpId;
        }

        return Guid.NewGuid().ToString("N");
    }

    private void InitializeNewPowerUpDefinition(SerializedProperty powerUpProperty, string powerUpId, string displayName)
    {
        if (powerUpProperty == null)
            return;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");
        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");
        SerializedProperty unreplaceableProperty = powerUpProperty.FindPropertyRelative("unreplaceable");

        if (commonDataProperty != null)
        {
            SetStringField(commonDataProperty.FindPropertyRelative("powerUpId"), powerUpId);
            SetStringField(commonDataProperty.FindPropertyRelative("displayName"), displayName);
            SetStringField(commonDataProperty.FindPropertyRelative("description"), string.Empty);
            SetIntField(commonDataProperty.FindPropertyRelative("dropTier"), 1);
            SetIntField(commonDataProperty.FindPropertyRelative("purchaseCost"), 0);
            CopyDropPoolCatalogIntoPowerUp(commonDataProperty.FindPropertyRelative("dropPools"));
        }

        if (moduleBindingsProperty != null)
            moduleBindingsProperty.arraySize = 0;

        if (unreplaceableProperty != null)
            unreplaceableProperty.boolValue = false;
    }

    private void CopyDropPoolCatalogIntoPowerUp(SerializedProperty dropPoolsProperty)
    {
        if (dropPoolsProperty == null)
            return;

        dropPoolsProperty.arraySize = 0;

        if (presetSerializedObject == null)
            return;

        SerializedProperty dropPoolCatalogProperty = presetSerializedObject.FindProperty("dropPoolCatalog");

        if (dropPoolCatalogProperty == null)
            return;

        for (int poolIndex = 0; poolIndex < dropPoolCatalogProperty.arraySize; poolIndex++)
        {
            SerializedProperty poolProperty = dropPoolCatalogProperty.GetArrayElementAtIndex(poolIndex);

            if (poolProperty == null)
                continue;

            string poolId = poolProperty.stringValue;

            if (string.IsNullOrWhiteSpace(poolId))
                continue;

            int insertIndex = dropPoolsProperty.arraySize;
            dropPoolsProperty.arraySize = insertIndex + 1;
            SerializedProperty insertedPoolProperty = dropPoolsProperty.GetArrayElementAtIndex(insertIndex);

            if (insertedPoolProperty == null)
                continue;

            insertedPoolProperty.stringValue = poolId;
        }
    }

    private static void SetStringField(SerializedProperty property, string value)
    {
        if (property == null)
            return;

        property.stringValue = value;
    }

    private static void SetIntField(SerializedProperty property, int value)
    {
        if (property == null)
            return;

        property.intValue = value;
    }

    private static void SetPowerUpDefinitionId(SerializedProperty powerUpProperty, string powerUpId)
    {
        if (powerUpProperty == null)
            return;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");

        if (commonDataProperty == null)
            return;

        SerializedProperty powerUpIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");

        if (powerUpIdProperty == null)
            return;

        powerUpIdProperty.stringValue = powerUpId;
    }

    private static void SetPowerUpDefinitionDisplayName(SerializedProperty powerUpProperty, string displayName)
    {
        if (powerUpProperty == null)
            return;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");

        if (commonDataProperty == null)
            return;

        SerializedProperty displayNameProperty = commonDataProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return;

        displayNameProperty.stringValue = displayName;
    }

    private void BuildLoadoutInputSection()
    {
        Label header = new Label("Loadout & Input");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

        SerializedProperty primaryToolActionIdProperty = presetSerializedObject.FindProperty("primaryToolActionId");
        SerializedProperty secondaryToolActionIdProperty = presetSerializedObject.FindProperty("secondaryToolActionId");
        SerializedProperty primaryActivePowerUpIdProperty = presetSerializedObject.FindProperty("primaryActivePowerUpId");
        SerializedProperty secondaryActivePowerUpIdProperty = presetSerializedObject.FindProperty("secondaryActivePowerUpId");
        SerializedProperty equippedPassivePowerUpIdsProperty = presetSerializedObject.FindProperty("equippedPassivePowerUpIds");
        SerializedProperty activePowerUpsProperty = presetSerializedObject.FindProperty("activePowerUps");
        SerializedProperty passivePowerUpsProperty = presetSerializedObject.FindProperty("passivePowerUps");

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

        if (primaryActivePowerUpIdProperty == null)
        {
            Label missingLabel = new Label("Loadout/input properties are missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        if (secondaryActivePowerUpIdProperty == null)
        {
            Label missingLabel = new Label("Loadout/input properties are missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        if (activePowerUpsProperty == null)
        {
            Label missingLabel = new Label("Active power ups list is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        if (equippedPassivePowerUpIdsProperty == null)
        {
            Label missingLabel = new Label("Passive loadout properties are missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            sectionContentRoot.Add(missingLabel);
            return;
        }

        if (passivePowerUpsProperty == null)
        {
            Label missingLabel = new Label("Passive power ups list is missing on this preset.");
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

        List<LoadoutPowerUpOption> loadoutOptions = BuildLoadoutOptions(activePowerUpsProperty);

        BuildLoadoutSelector("Primary Active Power Up", primaryActivePowerUpIdProperty, loadoutOptions);
        BuildLoadoutSelector("Secondary Active Power Up", secondaryActivePowerUpIdProperty, loadoutOptions);

        string primaryId = ResolveSelectedToolId(primaryActivePowerUpIdProperty.stringValue, loadoutOptions);
        string secondaryId = ResolveSelectedToolId(secondaryActivePowerUpIdProperty.stringValue, loadoutOptions);

        if (string.IsNullOrWhiteSpace(primaryId) == false &&
            string.IsNullOrWhiteSpace(secondaryId) == false &&
            string.Equals(primaryId, secondaryId, StringComparison.OrdinalIgnoreCase))
        {
            HelpBox sameSlotWarning = new HelpBox("Primary and Secondary currently reference the same active power up.", HelpBoxMessageType.Warning);
            sectionContentRoot.Add(sameSlotWarning);
        }

        Label passiveLoadoutHeader = new Label("Passive Power Ups Loadout (IDs)");
        passiveLoadoutHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        passiveLoadoutHeader.style.marginTop = 8f;
        passiveLoadoutHeader.style.marginBottom = 2f;
        sectionContentRoot.Add(passiveLoadoutHeader);

        List<string> passiveToolIds = BuildPassiveLoadoutOptions(passivePowerUpsProperty);

        if (passiveToolIds.Count == 0)
        {
            HelpBox missingPassiveToolsHelpBox = new HelpBox("No valid passive power ups found. Add passive power ups first.", HelpBoxMessageType.Warning);
            sectionContentRoot.Add(missingPassiveToolsHelpBox);
            return;
        }

        BuildPassiveLoadoutArray(equippedPassivePowerUpIdsProperty, passiveToolIds);
    }

    private void BuildLoadoutSelector(string label, SerializedProperty slotToolIdProperty, List<LoadoutPowerUpOption> loadoutOptions)
    {
        if (slotToolIdProperty == null)
            return;

        if (loadoutOptions == null || loadoutOptions.Count == 0)
            return;

        List<string> optionLabels = new List<string>();

        for (int index = 0; index < loadoutOptions.Count; index++)
            optionLabels.Add(loadoutOptions[index].DisplayLabel);

        LoadoutPowerUpOption selectedOption = ResolveSelectedOption(slotToolIdProperty.stringValue, loadoutOptions);
        int selectedIndex = 0;

        for (int index = 0; index < loadoutOptions.Count; index++)
        {
            if (string.Equals(loadoutOptions[index].PowerUpId, selectedOption.PowerUpId, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
                break;
            }
        }

        PopupField<string> selector = new PopupField<string>(label, optionLabels, selectedIndex);
        selector.tooltip = "Select the active power up assigned to this slot.";
        selector.RegisterValueChangedCallback(evt =>
        {
            int optionIndex = optionLabels.IndexOf(evt.newValue);

            if (optionIndex < 0 || optionIndex >= loadoutOptions.Count)
                return;

            string selectedToolId = loadoutOptions[optionIndex].PowerUpId;

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
        sectionContentRoot.Add(selector);
    }

    private static string ResolveSelectedToolId(string selectedToolId, List<LoadoutPowerUpOption> options)
    {
        if (options == null || options.Count == 0)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(selectedToolId) == false)
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index].PowerUpId, selectedToolId, StringComparison.OrdinalIgnoreCase))
                    return options[index].PowerUpId;
            }
        }

        return options[0].PowerUpId;
    }

    private static LoadoutPowerUpOption ResolveSelectedOption(string selectedToolId, List<LoadoutPowerUpOption> options)
    {
        if (options == null || options.Count == 0)
            return default;

        if (string.IsNullOrWhiteSpace(selectedToolId) == false)
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index].PowerUpId, selectedToolId, StringComparison.OrdinalIgnoreCase))
                    return options[index];
            }
        }

        return options[0];
    }

    private static List<LoadoutPowerUpOption> BuildLoadoutOptions(SerializedProperty activePowerUpsProperty)
    {
        List<LoadoutPowerUpOption> options = new List<LoadoutPowerUpOption>();
        options.Add(new LoadoutPowerUpOption
        {
            PowerUpId = string.Empty,
            DisplayLabel = "<None>"
        });

        if (activePowerUpsProperty == null)
            return options;

        for (int index = 0; index < activePowerUpsProperty.arraySize; index++)
        {
            SerializedProperty activePowerUpProperty = activePowerUpsProperty.GetArrayElementAtIndex(index);

            if (activePowerUpProperty == null)
                continue;

            SerializedProperty commonDataProperty = activePowerUpProperty.FindPropertyRelative("commonData");

            if (commonDataProperty == null)
                continue;

            SerializedProperty toolIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");
            SerializedProperty displayNameProperty = commonDataProperty.FindPropertyRelative("displayName");

            if (toolIdProperty == null)
                continue;

            string toolId = toolIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(toolId))
                continue;

            string displayName = displayNameProperty != null ? displayNameProperty.stringValue : string.Empty;

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = toolId;

            options.Add(new LoadoutPowerUpOption
            {
                PowerUpId = toolId,
                DisplayLabel = displayName + " (" + toolId + ")"
            });
        }

        return options;
    }

    private static List<string> BuildPassiveLoadoutOptions(SerializedProperty passivePowerUpsProperty)
    {
        List<string> options = new List<string>();

        if (passivePowerUpsProperty == null)
            return options;

        for (int index = 0; index < passivePowerUpsProperty.arraySize; index++)
        {
            SerializedProperty passivePowerUpProperty = passivePowerUpsProperty.GetArrayElementAtIndex(index);

            if (passivePowerUpProperty == null)
                continue;

            SerializedProperty commonDataProperty = passivePowerUpProperty.FindPropertyRelative("commonData");

            if (commonDataProperty == null)
                continue;

            SerializedProperty toolIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");

            if (toolIdProperty == null)
                continue;

            string toolId = toolIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(toolId))
                continue;

            if (ContainsStringIgnoreCase(options, toolId))
                continue;

            options.Add(toolId);
        }

        return options;
    }

    private static bool ContainsStringIgnoreCase(List<string> values, string candidate)
    {
        if (values == null)
            return false;

        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void BuildPassiveLoadoutArray(SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null)
            return;

        if (passiveToolIds == null || passiveToolIds.Count == 0)
            return;

        bool normalized = NormalizePassiveLoadoutArray(equippedPassiveToolIdsProperty, passiveToolIds);

        if (normalized)
        {
            presetSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            presetSerializedObject.Update();
        }

        for (int index = 0; index < equippedPassiveToolIdsProperty.arraySize; index++)
        {
            SerializedProperty passiveToolIdProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(index);

            if (passiveToolIdProperty == null)
                continue;

            string selectedToolId = ResolveSelectedPassiveToolId(passiveToolIdProperty.stringValue, passiveToolIds);
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;

            PopupField<string> passiveSelector = new PopupField<string>("Passive Power Up " + (index + 1), passiveToolIds, selectedToolId);
            passiveSelector.tooltip = "Select a passive tool by its PowerUpId.";
            passiveSelector.style.flexGrow = 1f;
            int capturedIndex = index;
            passiveSelector.RegisterValueChangedCallback(evt =>
            {
                SetPassiveLoadoutEntry(equippedPassiveToolIdsProperty, capturedIndex, evt.newValue, passiveToolIds);
            });
            row.Add(passiveSelector);

            Button removeButton = new Button(() =>
            {
                RemovePassiveLoadoutEntry(equippedPassiveToolIdsProperty, capturedIndex, passiveToolIds);
            });
            removeButton.text = "Remove";
            removeButton.tooltip = "Remove this passive tool from the startup loadout.";
            removeButton.style.marginLeft = 6f;
            row.Add(removeButton);

            sectionContentRoot.Add(row);
        }

        if (equippedPassiveToolIdsProperty.arraySize == 0)
        {
            HelpBox emptyLoadoutHelpBox = new HelpBox("No passive tools are currently equipped in startup loadout.", HelpBoxMessageType.Info);
            sectionContentRoot.Add(emptyLoadoutHelpBox);
        }

        Button addButton = new Button(() =>
        {
            AddPassiveLoadoutEntry(equippedPassiveToolIdsProperty, passiveToolIds);
        });
        addButton.text = "Add Passive Power Up";
        addButton.tooltip = "Add one passive tool ID to the startup loadout.";
        addButton.style.marginTop = 2f;
        addButton.SetEnabled(CanAddPassiveLoadoutEntry(equippedPassiveToolIdsProperty, passiveToolIds));
        sectionContentRoot.Add(addButton);
    }

    private void AddPassiveLoadoutEntry(SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null)
            return;

        if (passiveToolIds == null || passiveToolIds.Count == 0)
            return;

        string nextPassiveToolId = ResolveNextPassiveLoadoutId(equippedPassiveToolIdsProperty, passiveToolIds);

        if (string.IsNullOrWhiteSpace(nextPassiveToolId))
            return;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, "Add Passive Power Up Loadout Entry");

        presetSerializedObject.Update();
        int insertIndex = equippedPassiveToolIdsProperty.arraySize;
        equippedPassiveToolIdsProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty insertedProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(insertIndex);

        if (insertedProperty != null)
            insertedProperty.stringValue = nextPassiveToolId;

        NormalizePassiveLoadoutArray(equippedPassiveToolIdsProperty, passiveToolIds);
        presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        BuildActiveSection();
    }

    private void RemovePassiveLoadoutEntry(SerializedProperty equippedPassiveToolIdsProperty, int entryIndex, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null)
            return;

        if (entryIndex < 0 || entryIndex >= equippedPassiveToolIdsProperty.arraySize)
            return;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, "Remove Passive Power Up Loadout Entry");

        presetSerializedObject.Update();
        equippedPassiveToolIdsProperty.DeleteArrayElementAtIndex(entryIndex);
        NormalizePassiveLoadoutArray(equippedPassiveToolIdsProperty, passiveToolIds);
        presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        BuildActiveSection();
    }

    private void SetPassiveLoadoutEntry(SerializedProperty equippedPassiveToolIdsProperty, int entryIndex, string passiveToolId, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null)
            return;

        if (entryIndex < 0 || entryIndex >= equippedPassiveToolIdsProperty.arraySize)
            return;

        if (string.IsNullOrWhiteSpace(passiveToolId))
            return;

        SerializedProperty passiveToolIdProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(entryIndex);

        if (passiveToolIdProperty == null)
            return;

        if (string.Equals(passiveToolIdProperty.stringValue, passiveToolId, StringComparison.Ordinal))
            return;

        if (selectedPreset != null)
            Undo.RecordObject(selectedPreset, "Change Passive Power Up Loadout Entry");

        presetSerializedObject.Update();
        passiveToolIdProperty.stringValue = passiveToolId;
        NormalizePassiveLoadoutArray(equippedPassiveToolIdsProperty, passiveToolIds);
        presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        BuildActiveSection();
    }

    private static bool NormalizePassiveLoadoutArray(SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null)
            return false;

        if (passiveToolIds == null || passiveToolIds.Count == 0)
            return false;

        bool changed = false;
        HashSet<string> uniqueToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < equippedPassiveToolIdsProperty.arraySize; index++)
        {
            SerializedProperty passiveToolIdProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(index);
            string passiveToolId = passiveToolIdProperty != null ? passiveToolIdProperty.stringValue : string.Empty;

            if (string.IsNullOrWhiteSpace(passiveToolId))
            {
                equippedPassiveToolIdsProperty.DeleteArrayElementAtIndex(index);
                changed = true;
                index--;
                continue;
            }

            if (ContainsPassiveToolId(passiveToolIds, passiveToolId) == false)
            {
                equippedPassiveToolIdsProperty.DeleteArrayElementAtIndex(index);
                changed = true;
                index--;
                continue;
            }

            if (uniqueToolIds.Add(passiveToolId))
                continue;

            equippedPassiveToolIdsProperty.DeleteArrayElementAtIndex(index);
            changed = true;
            index--;
        }

        return changed;
    }

    private static string ResolveNextPassiveLoadoutId(SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null)
            return string.Empty;

        if (passiveToolIds == null || passiveToolIds.Count == 0)
            return string.Empty;

        for (int passiveToolIndex = 0; passiveToolIndex < passiveToolIds.Count; passiveToolIndex++)
        {
            string passiveToolId = passiveToolIds[passiveToolIndex];

            if (ContainsPassiveLoadoutId(equippedPassiveToolIdsProperty, passiveToolId))
                continue;

            return passiveToolId;
        }

        return string.Empty;
    }

    private static bool CanAddPassiveLoadoutEntry(SerializedProperty equippedPassiveToolIdsProperty, List<string> passiveToolIds)
    {
        if (equippedPassiveToolIdsProperty == null)
            return false;

        if (passiveToolIds == null || passiveToolIds.Count == 0)
            return false;

        for (int passiveToolIndex = 0; passiveToolIndex < passiveToolIds.Count; passiveToolIndex++)
        {
            string passiveToolId = passiveToolIds[passiveToolIndex];

            if (ContainsPassiveLoadoutId(equippedPassiveToolIdsProperty, passiveToolId))
                continue;

            return true;
        }

        return false;
    }

    private static bool ContainsPassiveToolId(List<string> passiveToolIds, string passiveToolId)
    {
        if (passiveToolIds == null)
            return false;

        if (string.IsNullOrWhiteSpace(passiveToolId))
            return false;

        for (int index = 0; index < passiveToolIds.Count; index++)
        {
            if (string.Equals(passiveToolIds[index], passiveToolId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ContainsPassiveLoadoutId(SerializedProperty equippedPassiveToolIdsProperty, string passiveToolId)
    {
        if (equippedPassiveToolIdsProperty == null)
            return false;

        if (string.IsNullOrWhiteSpace(passiveToolId))
            return false;

        for (int index = 0; index < equippedPassiveToolIdsProperty.arraySize; index++)
        {
            SerializedProperty passiveToolIdProperty = equippedPassiveToolIdsProperty.GetArrayElementAtIndex(index);

            if (passiveToolIdProperty == null)
                continue;

            if (string.Equals(passiveToolIdProperty.stringValue, passiveToolId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ResolveSelectedPassiveToolId(string selectedToolId, List<string> passiveToolIds)
    {
        if (passiveToolIds == null || passiveToolIds.Count == 0)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(selectedToolId) == false)
        {
            for (int index = 0; index < passiveToolIds.Count; index++)
            {
                if (string.Equals(passiveToolIds[index], selectedToolId, StringComparison.OrdinalIgnoreCase))
                    return passiveToolIds[index];
            }
        }

        return passiveToolIds[0];
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
        ModulesManagement = 2,
        ActivePowerUps = 3,
        PassivePowerUps = 4,
        LoadoutInput = 5
    }

    private struct LoadoutPowerUpOption
    {
        public string PowerUpId;
        public string DisplayLabel;
    }
    #endregion
}
