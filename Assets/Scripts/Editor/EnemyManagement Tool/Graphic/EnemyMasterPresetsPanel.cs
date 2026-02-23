using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for managing enemy master presets and linked sub presets.
/// </summary>
public sealed class EnemyMasterPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyMasterPreset> filteredPresets = new List<EnemyMasterPreset>();
    private readonly Dictionary<EnemyManagementWindow.PanelType, SidePanelEntry> sidePanels = new Dictionary<EnemyManagementWindow.PanelType, SidePanelEntry>();

    private EnemyMasterPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement detailSectionButtonsRoot;
    private VisualElement detailSectionContentRoot;
    private VisualElement mainContentRoot;
    private VisualElement tabBar;
    private VisualElement contentHost;
    private EnemyManagementWindow.PanelType activePanel = EnemyManagementWindow.PanelType.EnemyMasterPresets;
    private DetailsSectionType activeDetailsSection = DetailsSectionType.Metadata;

    private EnemyMasterPreset selectedPreset;
    private SerializedObject presetSerializedObject;

    private ObjectField enemyPrefabField;
    private Label activeStatusLabel;
    private GameObject selectedEnemyPrefab;
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

    #region Constructors
    public EnemyMasterPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.flexDirection = FlexDirection.Column;

        library = EnemyMasterPresetLibraryUtility.GetOrCreateLibrary();

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    public void RefreshFromSessionChange()
    {
        EnemyMasterPreset previouslySelectedPreset = selectedPreset;
        library = EnemyMasterPresetLibraryUtility.GetOrCreateLibrary();
        RefreshPresetList();

        if (previouslySelectedPreset != null)
        {
            int presetIndex = filteredPresets.IndexOf(previouslySelectedPreset);

            if (presetIndex >= 0)
            {
                if (listView != null)
                    listView.SetSelectionWithoutNotify(new int[] { presetIndex });

                SelectPreset(previouslySelectedPreset);
            }
        }

        RefreshOpenSidePanels();
    }
    #endregion

    #region UI Construction
    private void BuildUI()
    {
        mainContentRoot = BuildMainContent();
        BuildPanelsContainer();
    }

    private VisualElement BuildMainContent()
    {
        VisualElement container = new VisualElement();
        container.style.flexGrow = 1f;
        container.style.flexShrink = 1f;

        TwoPaneSplitView splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);

        VisualElement leftPane = BuildLeftPane();
        VisualElement rightPane = BuildRightPane();

        splitView.Add(leftPane);
        splitView.Add(rightPane);
        container.Add(splitView);

        return container;
    }

    private void BuildPanelsContainer()
    {
        tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.flexWrap = Wrap.Wrap;
        tabBar.style.marginBottom = 4f;
        tabBar.style.paddingLeft = 6f;
        tabBar.style.paddingRight = 6f;
        tabBar.style.paddingTop = 4f;
        tabBar.style.paddingBottom = 4f;

        contentHost = new VisualElement();
        contentHost.style.flexGrow = 1f;

        root.Add(tabBar);
        root.Add(contentHost);

        AddTab(EnemyManagementWindow.PanelType.EnemyMasterPresets,
               "Enemy Master Presets",
               mainContentRoot,
               null);
        SetActivePanel(EnemyManagementWindow.PanelType.EnemyMasterPresets);
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

        Button createButton = new Button();
        createButton.text = "Create";
        createButton.clicked += CreatePreset;
        toolbar.Add(createButton);

        Button duplicateButton = new Button();
        duplicateButton.text = "Duplicate";
        duplicateButton.clicked += DuplicatePreset;
        toolbar.Add(duplicateButton);

        Button deleteButton = new Button();
        deleteButton.text = "Delete";
        deleteButton.clicked += DeletePreset;
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

    private VisualElement BuildRightPane()
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
    #endregion

    #region Preset List
    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            EnemyMasterPreset preset = label.userData as EnemyMasterPreset;

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

        EnemyMasterPreset preset = filteredPresets[index];

        if (preset == null)
        {
            label.text = "<Missing Preset>";
            label.tooltip = string.Empty;
            label.userData = null;
            return;
        }

        label.userData = preset;
        label.text = GetPresetDisplayName(preset);
        label.tooltip = string.IsNullOrWhiteSpace(preset.Description) ? string.Empty : preset.Description;
    }

    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            EnemyMasterPreset preset = item as EnemyMasterPreset;

            if (preset != null)
            {
                SelectPreset(preset);
                return;
            }
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
                EnemyMasterPreset preset = library.Presets[index];

                if (preset == null)
                    continue;

                if (EnemyManagementDraftSession.IsAssetStagedForDeletion(preset))
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

    private bool IsMatchingSearch(EnemyMasterPreset preset, string searchText)
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
        EnemyMasterPreset newPreset = EnemyMasterPresetLibraryUtility.CreatePresetAsset("EnemyMasterPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Master Preset Asset");
        Undo.RecordObject(library, "Add Enemy Master Preset");
        library.AddPreset(newPreset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(newPreset);

        int index = filteredPresets.IndexOf(newPreset);

        if (index >= 0)
            listView.SetSelection(index);
    }

    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    private void DuplicatePreset(EnemyMasterPreset preset)
    {
        if (preset == null)
            return;

        EnemyMasterPreset duplicatedPreset = ScriptableObject.CreateInstance<EnemyMasterPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);
        duplicatedPreset.name = preset.name + " Copy";

        string originalPath = AssetDatabase.GetAssetPath(preset);
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(originalPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy Master Preset Asset");

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");

        if (nameProperty != null)
            nameProperty.stringValue = duplicatedPreset.name;

        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(library, "Duplicate Enemy Master Preset");
        library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(duplicatedPreset);

        int index = filteredPresets.IndexOf(duplicatedPreset);

        if (index >= 0)
            listView.SetSelection(index);
    }

    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    private void DeletePreset(EnemyMasterPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Enemy Master Preset", "Delete the selected enemy master preset asset?", "Delete", "Cancel");

        if (confirmed == false)
            return;

        Undo.RecordObject(library, "Delete Enemy Master Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    private void SelectPreset(EnemyMasterPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();
        detailSectionButtonsRoot = null;
        detailSectionContentRoot = null;

        if (selectedPreset == null)
        {
            Label label = new Label("Select or create an enemy master preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(label);
            RefreshActiveStatus();
            return;
        }

        presetSerializedObject = new SerializedObject(selectedPreset);
        detailSectionButtonsRoot = BuildDetailsSectionButtons();
        detailSectionContentRoot = new VisualElement();
        detailSectionContentRoot.style.flexDirection = FlexDirection.Column;
        detailSectionContentRoot.style.flexGrow = 1f;
        detailsRoot.Add(detailSectionButtonsRoot);
        detailsRoot.Add(detailSectionContentRoot);

        BuildActiveDetailsSection();
        RefreshActiveStatus();
        SyncOpenSidePanels();
    }

    private void BuildMetadataSection()
    {
        VisualElement sectionContainer = CreateDetailsSectionContainer("Preset Details");

        if (sectionContainer == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");
        SerializedProperty nameProperty = presetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = presetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = presetSerializedObject.FindProperty("version");

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            HandlePresetNameChanged(evt.newValue);
        });
        sectionContainer.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
            RefreshPresetList();
        });
        sectionContainer.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
            RefreshPresetList();
        });
        sectionContainer.Add(descriptionField);

        VisualElement idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;

        TextField idField = new TextField("Preset ID");
        idField.isReadOnly = true;
        idField.SetEnabled(false);
        idField.style.flexGrow = 1f;
        idField.BindProperty(idProperty);
        idRow.Add(idField);

        Button regenerateButton = new Button();
        regenerateButton.text = "Regenerate";
        regenerateButton.clicked += RegeneratePresetId;
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);

        sectionContainer.Add(idRow);
    }

    private void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Enemy Master Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
    }

    private void HandlePresetNameChanged(string newName)
    {
        RenamePreset(selectedPreset, newName);
    }

    private void RenamePreset(EnemyMasterPreset preset, string newName)
    {
        if (preset == null)
            return;

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(newName);

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
        EnemyManagementDraftSession.MarkDirty();
        RefreshPresetList();
    }

    private void ShowRenamePopup(VisualElement anchor, EnemyMasterPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        string title = "Rename Enemy Master Preset";
        PresetRenamePopup.Show(anchorRect, title, preset.PresetName, newName => RenamePreset(preset, newName));
    }

    private void BuildSubPresetsSection()
    {
        VisualElement sectionContainer = CreateDetailsSectionContainer("Sub Presets");

        if (sectionContainer == null)
            return;

        SerializedProperty brainProperty = presetSerializedObject.FindProperty("brainPreset");
        sectionContainer.Add(BuildSubPresetRow("Brain Preset",
                                               typeof(EnemyBrainPreset),
                                               brainProperty,
                                               CreateBrainPreset,
                                               () => OpenSidePanel(EnemyManagementWindow.PanelType.EnemyBrainPresets),
                                               EnemyManagementWindow.PanelType.EnemyBrainPresets));
    }

    private VisualElement BuildSubPresetRow(string label,
                                            Type presetType,
                                            SerializedProperty presetProperty,
                                            Action createAction,
                                            Action openSectionAction,
                                            EnemyManagementWindow.PanelType panelType)
    {
        VisualElement container = new VisualElement();
        container.style.marginBottom = 6f;

        ObjectField presetField = new ObjectField(label);
        presetField.objectType = presetType;
        presetField.BindProperty(presetProperty);
        presetField.RegisterValueChangedCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();

            if (panelType == EnemyManagementWindow.PanelType.EnemyBrainPresets)
                SyncOpenSidePanels();
        });
        container.Add(presetField);

        VisualElement buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.marginTop = 2f;

        Button openButton = new Button();
        openButton.text = "Open Section";
        openButton.tooltip = "Open the corresponding sub preset section.";
        openButton.clicked += openSectionAction;
        buttonsRow.Add(openButton);

        Button newButton = new Button();
        newButton.text = "New";
        newButton.tooltip = "Create and assign a new sub preset.";
        newButton.style.marginLeft = 4f;
        newButton.clicked += createAction;
        buttonsRow.Add(newButton);

        Button selectButton = new Button();
        selectButton.text = "Select in Project";
        selectButton.tooltip = "Select the assigned sub preset in the Project window.";
        selectButton.style.marginLeft = 4f;
        selectButton.clicked += () =>
        {
            if (presetProperty == null)
                return;

            UnityEngine.Object target = presetProperty.objectReferenceValue;

            if (target == null)
                return;

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        };
        buttonsRow.Add(selectButton);

        container.Add(buttonsRow);
        return container;
    }

    private void BuildActivePresetSection()
    {
        VisualElement sectionContainer = CreateDetailsSectionContainer("Active on Enemy Prefab");

        if (sectionContainer == null)
            return;

        enemyPrefabField = new ObjectField("Enemy Prefab");
        enemyPrefabField.objectType = typeof(GameObject);
        enemyPrefabField.RegisterValueChangedCallback(evt =>
        {
            selectedEnemyPrefab = evt.newValue as GameObject;
            RefreshActiveStatus();
        });
        sectionContainer.Add(enemyPrefabField);

        VisualElement buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.marginTop = 2f;

        Button findButton = new Button();
        findButton.text = "Find";
        findButton.tooltip = "Search the project for a prefab with EnemyAuthoring.";
        findButton.clicked += FindEnemyPrefab;
        buttonRow.Add(findButton);

        Button setActiveButton = new Button();
        setActiveButton.text = "Set Active Preset";
        setActiveButton.tooltip = "Assign this enemy master preset to the selected enemy prefab.";
        setActiveButton.style.marginLeft = 4f;
        setActiveButton.clicked += AssignPresetToPrefab;
        buttonRow.Add(setActiveButton);

        sectionContainer.Add(buttonRow);

        activeStatusLabel = new Label();
        activeStatusLabel.style.marginTop = 2f;
        activeStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        sectionContainer.Add(activeStatusLabel);
    }

    private void BuildNavigationSection()
    {
        VisualElement sectionContainer = CreateDetailsSectionContainer("Open Sections");

        if (sectionContainer == null)
            return;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        Button openBrainButton = new Button();
        openBrainButton.text = "Open Brain";
        openBrainButton.clicked += () => OpenSidePanel(EnemyManagementWindow.PanelType.EnemyBrainPresets);
        row.Add(openBrainButton);

        sectionContainer.Add(row);
    }

    private VisualElement BuildDetailsSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddDetailsSectionButton(buttonsRoot, DetailsSectionType.Metadata, "Metadata");
        AddDetailsSectionButton(buttonsRoot, DetailsSectionType.SubPresets, "Sub Presets");
        AddDetailsSectionButton(buttonsRoot, DetailsSectionType.ActivePreset, "Active Preset");
        AddDetailsSectionButton(buttonsRoot, DetailsSectionType.Navigation, "Navigation");
        return buttonsRoot;
    }

    private void AddDetailsSectionButton(VisualElement parent, DetailsSectionType sectionType, string buttonLabel)
    {
        Button sectionButton = new Button(() => SetActiveDetailsSection(sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    private void SetActiveDetailsSection(DetailsSectionType sectionType)
    {
        activeDetailsSection = sectionType;
        BuildActiveDetailsSection();
    }

    private void BuildActiveDetailsSection()
    {
        if (detailSectionContentRoot == null)
            return;

        if (presetSerializedObject == null)
            return;

        presetSerializedObject.Update();
        detailSectionContentRoot.Clear();

        switch (activeDetailsSection)
        {
            case DetailsSectionType.Metadata:
                BuildMetadataSection();
                return;
            case DetailsSectionType.SubPresets:
                BuildSubPresetsSection();
                return;
            case DetailsSectionType.ActivePreset:
                BuildActivePresetSection();
                return;
            case DetailsSectionType.Navigation:
                BuildNavigationSection();
                return;
        }
    }

    private VisualElement CreateDetailsSectionContainer(string sectionTitle)
    {
        if (detailSectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        container.Add(header);
        detailSectionContentRoot.Add(container);
        return container;
    }
    #endregion

    #region Sub Preset Creation
    private void CreateBrainPreset()
    {
        EnemyBrainPreset newPreset = EnemyBrainPresetLibraryUtility.CreatePresetAsset("EnemyBrainPreset");

        if (newPreset == null)
            return;

        EnemyBrainPresetLibrary brainLibrary = EnemyBrainPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Brain Preset Asset");
        Undo.RecordObject(brainLibrary, "Add Enemy Brain Preset");
        brainLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(brainLibrary);
        EnemyManagementDraftSession.MarkDirty();

        AssignSubPreset("brainPreset", newPreset);
        OpenSidePanel(EnemyManagementWindow.PanelType.EnemyBrainPresets);
    }

    private void AssignSubPreset(string propertyName, UnityEngine.Object preset)
    {
        if (selectedPreset == null)
            return;

        SerializedProperty property = presetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        Undo.RecordObject(selectedPreset, "Assign Enemy Sub Preset");
        presetSerializedObject.Update();
        property.objectReferenceValue = preset;
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
        SyncOpenSidePanels();
    }
    #endregion

    #region Prefab Activation
    private void FindEnemyPrefab()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            EnemyAuthoring authoring = prefab.GetComponent<EnemyAuthoring>();

            if (authoring == null)
                continue;

            selectedEnemyPrefab = prefab;

            if (enemyPrefabField != null)
                enemyPrefabField.SetValueWithoutNotify(prefab);

            RefreshActiveStatus();
            return;
        }

        EditorUtility.DisplayDialog("Find Enemy Prefab", "No prefab with EnemyAuthoring was found.", "OK");
    }

    private void AssignPresetToPrefab()
    {
        if (selectedPreset == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "Select an enemy master preset first.", "OK");
            return;
        }

        if (selectedEnemyPrefab == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "Select an enemy prefab first.", "OK");
            return;
        }

        EnemyAuthoring authoring = selectedEnemyPrefab.GetComponent<EnemyAuthoring>();

        if (authoring == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "EnemyAuthoring component not found on the selected prefab.", "OK");
            return;
        }

        SerializedObject serializedAuthoring = new SerializedObject(authoring);
        SerializedProperty presetProperty = serializedAuthoring.FindProperty("masterPreset");

        if (presetProperty == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "Property 'masterPreset' not found on EnemyAuthoring.", "OK");
            return;
        }

        Undo.RecordObject(authoring, "Set Active Enemy Master Preset");
        serializedAuthoring.Update();
        presetProperty.objectReferenceValue = selectedPreset;
        serializedAuthoring.ApplyModifiedProperties();
        EditorUtility.SetDirty(authoring);

        if (PrefabUtility.IsPartOfPrefabInstance(authoring))
            PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);

        EnemyManagementDraftSession.MarkDirty();
        RefreshActiveStatus();
    }

    private void RefreshActiveStatus()
    {
        if (activeStatusLabel == null)
            return;

        if (selectedPreset == null)
        {
            activeStatusLabel.text = "No enemy master preset selected.";
            return;
        }

        if (selectedEnemyPrefab == null)
        {
            activeStatusLabel.text = "No enemy prefab selected.";
            return;
        }

        EnemyAuthoring authoring = selectedEnemyPrefab.GetComponent<EnemyAuthoring>();

        if (authoring == null)
        {
            activeStatusLabel.text = "EnemyAuthoring component not found on prefab.";
            return;
        }

        bool isActive = authoring.MasterPreset == selectedPreset;
        activeStatusLabel.text = isActive ? "Active on selected prefab." : "Not active on selected prefab.";
    }
    #endregion

    #region Helpers
    private void OpenSidePanel(EnemyManagementWindow.PanelType panelType)
    {
        if (sidePanels.ContainsKey(panelType))
        {
            SidePanelEntry existingEntry = sidePanels[panelType];

            if (existingEntry != null)
                SetActivePanel(panelType);

            SyncSidePanelSelection(panelType, existingEntry);
            return;
        }

        EnemyBrainPresetsPanel brainPanel;
        VisualElement content = BuildSidePanelContent(panelType, out brainPanel);

        if (content == null)
            return;

        AddTab(panelType, GetPanelTitle(panelType), content, brainPanel);
        SetActivePanel(panelType);
        SyncSidePanelSelection(panelType, sidePanels[panelType]);
    }

    private void CloseSidePanel(EnemyManagementWindow.PanelType panelType)
    {
        if (panelType == EnemyManagementWindow.PanelType.EnemyMasterPresets)
            return;

        if (sidePanels.TryGetValue(panelType, out SidePanelEntry entry) == false)
            return;

        if (entry != null && entry.TabContainer != null)
            entry.TabContainer.RemoveFromHierarchy();

        sidePanels.Remove(panelType);

        if (activePanel == panelType)
            SetActivePanel(EnemyManagementWindow.PanelType.EnemyMasterPresets);
    }

    private string GetPanelTitle(EnemyManagementWindow.PanelType panelType)
    {
        if (panelType == EnemyManagementWindow.PanelType.EnemyBrainPresets)
            return "Enemy Brain Presets";

        return "Enemy Master Presets";
    }

    private VisualElement BuildSidePanelContent(EnemyManagementWindow.PanelType panelType, out EnemyBrainPresetsPanel brainPanel)
    {
        brainPanel = null;

        VisualElement panelRoot = new VisualElement();
        panelRoot.style.flexDirection = FlexDirection.Column;
        panelRoot.style.flexGrow = 1f;

        VisualElement headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.justifyContent = Justify.SpaceBetween;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginBottom = 4f;

        Label title = new Label(GetPanelTitle(panelType));
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerRow.Add(title);

        Button closeButton = new Button();
        closeButton.text = "X";
        closeButton.tooltip = "Close this section.";
        closeButton.clicked += () => CloseSidePanel(panelType);
        headerRow.Add(closeButton);

        panelRoot.Add(headerRow);

        if (panelType == EnemyManagementWindow.PanelType.EnemyBrainPresets)
        {
            brainPanel = new EnemyBrainPresetsPanel();
            panelRoot.Add(brainPanel.Root);
            return panelRoot;
        }

        VisualElement placeholder = new VisualElement();
        placeholder.style.flexGrow = 1f;
        placeholder.style.minHeight = 220f;
        placeholder.style.justifyContent = Justify.Center;
        placeholder.style.alignItems = Align.Center;
        Label placeholderLabel = new Label("Section not implemented yet.");
        placeholderLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        placeholder.Add(placeholderLabel);
        panelRoot.Add(placeholder);
        return panelRoot;
    }

    private void AddTab(EnemyManagementWindow.PanelType panelType,
                        string label,
                        VisualElement content,
                        EnemyBrainPresetsPanel brainPanel)
    {
        if (tabBar == null)
            return;

        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;

        Button tabButton = new Button();
        tabButton.text = label;
        tabButton.clicked += () => SetActivePanel(panelType);
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabContainer.Add(tabButton);

        tabBar.Add(tabContainer);
        sidePanels[panelType] = new SidePanelEntry
        {
            TabContainer = tabContainer,
            TabButton = tabButton,
            Content = content,
            BrainPanel = brainPanel
        };
    }

    private void SyncSidePanelSelection(EnemyManagementWindow.PanelType panelType, SidePanelEntry entry)
    {
        if (entry == null)
            return;

        if (selectedPreset == null)
            return;

        if (panelType == EnemyManagementWindow.PanelType.EnemyBrainPresets)
        {
            if (entry.BrainPanel == null)
                return;

            EnemyBrainPreset brainPreset = selectedPreset.BrainPreset;

            if (brainPreset == null)
                return;

            entry.BrainPanel.SelectPresetFromExternal(brainPreset);
        }
    }

    private void SyncOpenSidePanels()
    {
        foreach (KeyValuePair<EnemyManagementWindow.PanelType, SidePanelEntry> sidePanelEntry in sidePanels)
            SyncSidePanelSelection(sidePanelEntry.Key, sidePanelEntry.Value);
    }

    private void RefreshOpenSidePanels()
    {
        foreach (KeyValuePair<EnemyManagementWindow.PanelType, SidePanelEntry> sidePanelEntry in sidePanels)
        {
            SidePanelEntry entry = sidePanelEntry.Value;

            if (entry == null)
                continue;

            if (entry.BrainPanel != null)
                entry.BrainPanel.RefreshFromSessionChange();
        }

        SyncOpenSidePanels();
    }

    private void SetActivePanel(EnemyManagementWindow.PanelType panelType)
    {
        if (sidePanels.TryGetValue(panelType, out SidePanelEntry entry) == false)
            return;

        if (contentHost == null)
            return;

        activePanel = panelType;
        contentHost.Clear();
        contentHost.Add(entry.Content);
        UpdateTabStyles();
    }

    private void UpdateTabStyles()
    {
        foreach (KeyValuePair<EnemyManagementWindow.PanelType, SidePanelEntry> sidePanelEntry in sidePanels)
        {
            if (sidePanelEntry.Value == null || sidePanelEntry.Value.TabButton == null)
                continue;

            bool isActive = sidePanelEntry.Key == activePanel;
            sidePanelEntry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            sidePanelEntry.Value.TabButton.style.backgroundColor = isActive ? new Color(0.18f, 0.18f, 0.18f, 0.6f) : Color.clear;
        }
    }

    private string GetPresetDisplayName(EnemyMasterPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string presetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return presetName;

        return presetName + " v. " + version;
    }
    #endregion

    #endregion

    #region Nested Types
    private enum DetailsSectionType
    {
        Metadata = 0,
        SubPresets = 1,
        ActivePreset = 2,
        Navigation = 3
    }

    private sealed class SidePanelEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
        public EnemyBrainPresetsPanel BrainPanel;
    }
    #endregion
}
