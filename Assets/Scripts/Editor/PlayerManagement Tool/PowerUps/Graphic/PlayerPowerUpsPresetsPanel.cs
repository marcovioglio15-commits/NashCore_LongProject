using System;
using System.Collections.Generic;
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

        PropertyField modulesField = new PropertyField(moduleDefinitionsProperty);
        modulesField.BindProperty(moduleDefinitionsProperty);
        sectionContentRoot.Add(modulesField);

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

        PropertyField activePowerUpsField = new PropertyField(activePowerUpsProperty);
        activePowerUpsField.BindProperty(activePowerUpsProperty);
        sectionContentRoot.Add(activePowerUpsField);
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

        PropertyField passivePowerUpsField = new PropertyField(passivePowerUpsProperty);
        passivePowerUpsField.BindProperty(passivePowerUpsProperty);
        sectionContentRoot.Add(passivePowerUpsField);
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

        if (loadoutOptions.Count == 0)
        {
            HelpBox missingToolsHelpBox = new HelpBox("No valid active power ups found. Add entries in Active Power Ups first.", HelpBoxMessageType.Warning);
            sectionContentRoot.Add(missingToolsHelpBox);
        }
        else
        {
            BuildLoadoutSelector("Primary Active Power Up", primaryActivePowerUpIdProperty, loadoutOptions);
            BuildLoadoutSelector("Secondary Active Power Up", secondaryActivePowerUpIdProperty, loadoutOptions);

            string primaryId = ResolveSelectedToolId(primaryActivePowerUpIdProperty.stringValue, loadoutOptions);
            string secondaryId = ResolveSelectedToolId(secondaryActivePowerUpIdProperty.stringValue, loadoutOptions);

            if (string.Equals(primaryId, secondaryId, StringComparison.OrdinalIgnoreCase))
            {
                HelpBox sameSlotWarning = new HelpBox("Primary and Secondary currently reference the same active power up.", HelpBoxMessageType.Warning);
                sectionContentRoot.Add(sameSlotWarning);
            }
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
