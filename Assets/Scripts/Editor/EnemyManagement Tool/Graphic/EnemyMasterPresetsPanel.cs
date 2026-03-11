using System;
using System.Collections.Generic;
using System.IO;
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
    private const string ActivePanelStateKey = "NashCore.EnemyManagement.Master.ActivePanel";
    private const string OpenPanelsStateKey = "NashCore.EnemyManagement.Master.OpenPanels";
    private const string ActiveDetailsSectionStateKey = "NashCore.EnemyManagement.Master.ActiveDetailsSection";
    private const string SelectedPrefabPathStateKey = "NashCore.EnemyManagement.Master.SelectedPrefabPath";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyMasterPreset> filteredPresets = new List<EnemyMasterPreset>();
    private readonly Dictionary<EnemyManagementWindow.PanelType, SidePanelEntry> sidePanels = new Dictionary<EnemyManagementWindow.PanelType, SidePanelEntry>();
    private readonly List<GameObject> availableEnemyPrefabs = new List<GameObject>();



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


    private PopupField<GameObject> enemyPrefabPopup;
    private Label activeStatusLabel;
    private Label testUiStatusLabel;
    private GameObject selectedEnemyPrefab;
    private bool suppressStateWrite;
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
        RestorePersistedState();

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

        suppressStateWrite = true;
        AddTab(EnemyManagementWindow.PanelType.EnemyMasterPresets,
               "Enemy Master Presets",
               mainContentRoot,
               null,
               null);
        RestoreOpenSidePanels();

        if (!sidePanels.ContainsKey(activePanel))
            activePanel = EnemyManagementWindow.PanelType.EnemyMasterPresets;

        SetActivePanel(activePanel);
        suppressStateWrite = false;
        SaveOpenPanelsState();
        ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, activePanel);
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

        if (selectedPreset == null || !filteredPresets.Contains(selectedPreset))
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

        string originalPath = AssetDatabase.GetAssetPath(preset);

        if (string.IsNullOrWhiteSpace(originalPath))
            return;

        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalDirectory))
            return;

        string sourceDisplayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string duplicateBaseName = EnemyManagementDraftSession.NormalizeAssetName(sourceDisplayName + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
            duplicateBaseName = "EnemyMasterPreset Copy";

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy Master Preset Asset");
        string finalName = Path.GetFileNameWithoutExtension(duplicatedPath);
        duplicatedPreset.name = finalName;

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");

        if (nameProperty != null)
            nameProperty.stringValue = finalName;

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

        if (!confirmed)
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

        SerializedProperty advancedPatternProperty = presetSerializedObject.FindProperty("advancedPatternPreset");
        sectionContainer.Add(BuildSubPresetRow("Advanced Pattern Preset",
                                               typeof(EnemyAdvancedPatternPreset),
                                               advancedPatternProperty,
                                               CreateAdvancedPatternPreset,
                                               () => OpenSidePanel(EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets),
                                               EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets));
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

            if (panelType != EnemyManagementWindow.PanelType.EnemyMasterPresets)
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

    /// <summary>
    /// Builds the Active Preset section for assigning selected master preset to an enemy prefab.
    /// Called by BuildActiveDetailsSection when ActivePreset tab is selected.
    /// </summary>
    private void BuildActivePresetSection()
    {
        VisualElement sectionContainer = CreateDetailsSectionContainer("Active on Enemy Prefab");

        if (sectionContainer == null)
            return;

        RefreshAvailableEnemyPrefabs();
        int selectedPrefabIndex = ResolveSelectedEnemyPrefabIndex();
        enemyPrefabPopup = new PopupField<GameObject>("Enemy Prefab",
                                                      availableEnemyPrefabs,
                                                      selectedPrefabIndex,
                                                      ResolveEnemyPrefabDisplayName,
                                                      ResolveEnemyPrefabDisplayName);
        enemyPrefabPopup.tooltip = "Project prefab selector filtered to assets containing EnemyAuthoring in hierarchy.";
        enemyPrefabPopup.RegisterValueChangedCallback(evt =>
        {
            selectedEnemyPrefab = evt.newValue;
            SaveSelectedPrefabState();
            BuildActiveDetailsSection();
        });
        sectionContainer.Add(enemyPrefabPopup);

        VisualElement buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.marginTop = 2f;

        Button refreshPrefabsButton = new Button();
        refreshPrefabsButton.text = "Refresh Prefabs";
        refreshPrefabsButton.tooltip = "Rescan project prefabs containing EnemyAuthoring.";
        refreshPrefabsButton.clicked += RefreshEnemyPrefabSelection;
        buttonRow.Add(refreshPrefabsButton);

        Button pingPrefabButton = new Button();
        pingPrefabButton.text = "Ping";
        pingPrefabButton.tooltip = "Highlight selected enemy prefab asset in Project window.";
        pingPrefabButton.style.marginLeft = 4f;
        pingPrefabButton.SetEnabled(selectedEnemyPrefab != null);
        pingPrefabButton.clicked += PingSelectedEnemyPrefab;
        buttonRow.Add(pingPrefabButton);

        Button setActiveButton = new Button();
        setActiveButton.text = "Set Active Preset";
        setActiveButton.tooltip = "Assign selected Master Preset and its sub-presets to this enemy prefab only.";
        setActiveButton.style.marginLeft = 4f;
        setActiveButton.clicked += AssignPresetToPrefab;
        setActiveButton.SetEnabled(selectedEnemyPrefab != null);
        buttonRow.Add(setActiveButton);

        sectionContainer.Add(buttonRow);

        activeStatusLabel = new Label();
        activeStatusLabel.style.marginTop = 2f;
        activeStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        sectionContainer.Add(activeStatusLabel);
        RefreshActiveStatus();

        BuildTestUiActionsSection(sectionContainer);
        BuildTestUiSettingsSection(sectionContainer);
    }

    private void BuildTestUiActionsSection(VisualElement sectionContainer)
    {
        if (sectionContainer == null)
            return;

        VisualElement actionsContainer = new VisualElement();
        actionsContainer.style.marginTop = 10f;
        sectionContainer.Add(actionsContainer);

        Label headerLabel = new Label("Test UI Actions");
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerLabel.style.marginBottom = 4f;
        actionsContainer.Add(headerLabel);

        if (selectedEnemyPrefab == null)
        {
            Label noPrefabLabel = new Label("Select an enemy prefab to enable Test UI actions.");
            noPrefabLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            actionsContainer.Add(noPrefabLabel);
            return;
        }

        EnemyAuthoring enemyAuthoring = selectedEnemyPrefab.GetComponentInChildren<EnemyAuthoring>(true);

        if (enemyAuthoring == null)
        {
            Label missingAuthoringLabel = new Label("EnemyAuthoring component not found on selected prefab.");
            missingAuthoringLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            actionsContainer.Add(missingAuthoringLabel);
            return;
        }

        VisualElement buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.marginTop = 2f;

        Button generateButton = new Button();
        generateButton.text = "Generate Test UI";
        generateButton.tooltip = "Generate world-space health and shield bars on selected prefab and assign them to EnemyAuthoring.";
        generateButton.clicked += GenerateTestUiOnPrefab;
        buttonsRow.Add(generateButton);

        bool hasGeneratedTestUi = EnemyStatusBarsTestUiPrefabUtility.HasGeneratedTestUi(selectedEnemyPrefab);

        Button deleteButton = new Button();
        deleteButton.text = "Delete Test UI";
        deleteButton.tooltip = "Delete generated test status bars from selected prefab and clear assignment if generated.";
        deleteButton.style.marginLeft = 4f;
        deleteButton.SetEnabled(hasGeneratedTestUi);
        deleteButton.clicked += DeleteTestUiOnPrefab;
        buttonsRow.Add(deleteButton);

        actionsContainer.Add(buttonsRow);

        testUiStatusLabel = new Label();
        testUiStatusLabel.style.marginTop = 2f;
        testUiStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        actionsContainer.Add(testUiStatusLabel);
        RefreshTestUiStatus();
    }

    private void BuildTestUiSettingsSection(VisualElement sectionContainer)
    {
        if (sectionContainer == null)
            return;

        SerializedProperty testUiSettingsProperty = presetSerializedObject.FindProperty("testUiSettings");

        if (testUiSettingsProperty == null)
            return;

        VisualElement settingsContainer = new VisualElement();
        settingsContainer.style.marginTop = 10f;
        sectionContainer.Add(settingsContainer);

        Label headerLabel = new Label("Test UI Settings");
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerLabel.style.marginBottom = 4f;
        settingsContainer.Add(headerLabel);

        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("worldOffset"), "World Offset");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("rootWidthPixels"), "Root Width Pixels");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("rootHeightPixels"), "Root Height Pixels");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("worldScale"), "World Scale");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("canvasSortingOrder"), "Canvas Sorting Order");

        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthBarWidthPixels"), "Health Bar Width");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthBarHeightPixels"), "Health Bar Height");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthBarYOffsetPixels"), "Health Bar Y Offset");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldBarWidthPixels"), "Shield Bar Width");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldBarHeightPixels"), "Shield Bar Height");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldBarYOffsetPixels"), "Shield Bar Y Offset");

        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthFillColor"), "Health Fill Color");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("healthBackgroundColor"), "Health Background Color");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldFillColor"), "Shield Fill Color");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldBackgroundColor"), "Shield Background Color");

        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("hideShieldWhenEmpty"), "Hide Shield When Empty");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("hideWhenEnemyInactive"), "Hide When Enemy Inactive");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("hideWhenEnemyCulled"), "Hide When Enemy Culled");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("smoothingSeconds"), "Smoothing Seconds");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("shieldSmoothingSeconds"), "Shield Smoothing Seconds");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("billboardToCamera"), "Billboard To Camera");
        AddTrackedPropertyField(settingsContainer, testUiSettingsProperty.FindPropertyRelative("billboardYawOnly"), "Billboard Yaw Only");
    }

    private void AddTrackedPropertyField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return;

        if (property == null)
            return;

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        field.RegisterValueChangeCallback(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
        });
        parent.Add(field);
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

        Button openAdvancedPatternButton = new Button();
        openAdvancedPatternButton.text = "Open Advanced Pattern";
        openAdvancedPatternButton.style.marginLeft = 4f;
        openAdvancedPatternButton.clicked += () => OpenSidePanel(EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets);
        row.Add(openAdvancedPatternButton);

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

    /// <summary>
    /// Sets the currently active details subsection and persists it for future reopen.
    /// Called by details tab buttons.
    /// Takes in the target details section enum.
    /// </summary>
    /// <param name="sectionType">Details subsection to display.</param>
    private void SetActiveDetailsSection(DetailsSectionType sectionType)
    {
        // Persist active subsection and rebuild details content.
        activeDetailsSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveDetailsSectionStateKey, activeDetailsSection);
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

    private void CreateAdvancedPatternPreset()
    {
        EnemyAdvancedPatternPreset newPreset = EnemyAdvancedPatternPresetLibraryUtility.CreatePresetAsset("EnemyAdvancedPatternPreset");

        if (newPreset == null)
            return;

        EnemyAdvancedPatternPresetLibrary advancedPatternLibrary = EnemyAdvancedPatternPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Advanced Pattern Preset Asset");
        Undo.RecordObject(advancedPatternLibrary, "Add Enemy Advanced Pattern Preset");
        advancedPatternLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(advancedPatternLibrary);
        EnemyManagementDraftSession.MarkDirty();

        AssignSubPreset("advancedPatternPreset", newPreset);
        OpenSidePanel(EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets);
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
    private void RefreshAvailableEnemyPrefabs()
    {
        string selectedPrefabPath = string.Empty;

        if (selectedEnemyPrefab != null)
            selectedPrefabPath = AssetDatabase.GetAssetPath(selectedEnemyPrefab);

        availableEnemyPrefabs.Clear();
        availableEnemyPrefabs.Add(null);

        string[] searchFolders = new string[] { "Assets" };
        List<GameObject> prefabAssets = ManagementToolPrefabUtility.FindPrefabsWithComponentInHierarchy<EnemyAuthoring>(searchFolders);

        for (int prefabIndex = 0; prefabIndex < prefabAssets.Count; prefabIndex++)
        {
            GameObject prefabAsset = prefabAssets[prefabIndex];

            if (prefabAsset == null)
                continue;

            availableEnemyPrefabs.Add(prefabAsset);
        }

        if (string.IsNullOrWhiteSpace(selectedPrefabPath))
        {
            selectedEnemyPrefab = null;
            return;
        }

        for (int prefabIndex = 1; prefabIndex < availableEnemyPrefabs.Count; prefabIndex++)
        {
            GameObject prefabAsset = availableEnemyPrefabs[prefabIndex];
            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);

            if (string.Equals(prefabPath, selectedPrefabPath, StringComparison.Ordinal))
            {
                selectedEnemyPrefab = prefabAsset;
                return;
            }
        }

        selectedEnemyPrefab = null;
        SaveSelectedPrefabState();
    }

    private int ResolveSelectedEnemyPrefabIndex()
    {
        if (selectedEnemyPrefab == null)
            return 0;

        string selectedPrefabPath = AssetDatabase.GetAssetPath(selectedEnemyPrefab);

        if (string.IsNullOrWhiteSpace(selectedPrefabPath))
            return 0;

        for (int prefabIndex = 1; prefabIndex < availableEnemyPrefabs.Count; prefabIndex++)
        {
            GameObject prefabAsset = availableEnemyPrefabs[prefabIndex];
            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);

            if (string.Equals(prefabPath, selectedPrefabPath, StringComparison.Ordinal))
                return prefabIndex;
        }

        selectedEnemyPrefab = null;
        SaveSelectedPrefabState();
        return 0;
    }

    private static string ResolveEnemyPrefabDisplayName(GameObject prefabAsset)
    {
        if (prefabAsset == null)
            return "<None>";

        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);

        if (string.IsNullOrWhiteSpace(prefabPath))
            return prefabAsset.name;

        return string.Format("{0} ({1})", prefabAsset.name, prefabPath);
    }

    private static EnemyAuthoring ResolveEnemyAuthoringInPrefab(GameObject prefabAsset)
    {
        if (prefabAsset == null)
            return null;

        EnemyAuthoring directAuthoring = prefabAsset.GetComponent<EnemyAuthoring>();

        if (directAuthoring != null)
            return directAuthoring;

        return prefabAsset.GetComponentInChildren<EnemyAuthoring>(true);
    }

    private void RefreshEnemyPrefabSelection()
    {
        RefreshAvailableEnemyPrefabs();

        if (activeDetailsSection == DetailsSectionType.ActivePreset)
        {
            BuildActiveDetailsSection();
            return;
        }

        RefreshActiveStatus();
        RefreshTestUiStatus();
    }

    private void PingSelectedEnemyPrefab()
    {
        if (selectedEnemyPrefab == null)
            return;

        Selection.activeObject = selectedEnemyPrefab;
        EditorGUIUtility.PingObject(selectedEnemyPrefab);
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

        EnemyAuthoring authoring = ResolveEnemyAuthoringInPrefab(selectedEnemyPrefab);

        if (authoring == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "EnemyAuthoring component not found on the selected prefab.", "OK");
            return;
        }

        SerializedObject serializedAuthoring = new SerializedObject(authoring);
        SerializedProperty masterPresetProperty = serializedAuthoring.FindProperty("masterPreset");
        SerializedProperty brainPresetProperty = serializedAuthoring.FindProperty("brainPreset");
        SerializedProperty advancedPatternPresetProperty = serializedAuthoring.FindProperty("advancedPatternPreset");

        if (masterPresetProperty == null || brainPresetProperty == null || advancedPatternPresetProperty == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset",
                                        "One or more preset properties are missing on EnemyAuthoring (masterPreset, brainPreset, advancedPatternPreset).",
                                        "OK");
            return;
        }

        Undo.RecordObject(authoring, "Set Active Enemy Master Preset");
        serializedAuthoring.Update();
        masterPresetProperty.objectReferenceValue = selectedPreset;
        brainPresetProperty.objectReferenceValue = selectedPreset.BrainPreset;
        advancedPatternPresetProperty.objectReferenceValue = selectedPreset.AdvancedPatternPreset;
        serializedAuthoring.ApplyModifiedProperties();
        EditorUtility.SetDirty(authoring);

        if (PrefabUtility.IsPartOfPrefabInstance(authoring))
            PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);

        EnemyManagementDraftSession.MarkDirty();
        RefreshActiveStatus();
        RefreshTestUiStatus();
    }

    private void GenerateTestUiOnPrefab()
    {
        if (selectedEnemyPrefab == null)
        {
            EditorUtility.DisplayDialog("Generate Test UI", "Select an enemy prefab first.", "OK");
            return;
        }

        EnemyAuthoring authoring = selectedEnemyPrefab.GetComponentInChildren<EnemyAuthoring>(true);

        if (authoring == null)
        {
            EditorUtility.DisplayDialog("Generate Test UI", "EnemyAuthoring component not found on selected prefab.", "OK");
            return;
        }

        EnemyWorldSpaceStatusBarsView assignedView = authoring.WorldSpaceStatusBarsView;

        if (assignedView != null && !EnemyStatusBarsTestUiPrefabUtility.IsGeneratedTestUiView(assignedView))
        {
            bool replaceCustomView = EditorUtility.DisplayDialog("Generate Test UI",
                                                                 "Selected prefab already has a custom world-space status bars view assigned. Replace it with generated test UI?",
                                                                 "Replace",
                                                                 "Cancel");

            if (!replaceCustomView)
                return;
        }

        EnemyTestUiSettings testUiSettings = selectedPreset != null ? selectedPreset.TestUiSettings : null;
        bool generated = EnemyStatusBarsTestUiPrefabUtility.TryGenerateTestUi(selectedEnemyPrefab, testUiSettings, out string message);

        if (!generated)
        {
            EditorUtility.DisplayDialog("Generate Test UI", message, "OK");
            return;
        }

        ReloadSelectedPrefabReference();
        EnemyManagementDraftSession.MarkDirty();
        RefreshActiveStatus();
        BuildActiveDetailsSection();
    }

    private void DeleteTestUiOnPrefab()
    {
        if (selectedEnemyPrefab == null)
        {
            EditorUtility.DisplayDialog("Delete Test UI", "Select an enemy prefab first.", "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog("Delete Test UI",
                                                     "Delete generated test UI from selected enemy prefab?",
                                                     "Delete",
                                                     "Cancel");

        if (!confirmed)
            return;

        bool deleted = EnemyStatusBarsTestUiPrefabUtility.TryDeleteTestUi(selectedEnemyPrefab, out string message);

        if (!deleted)
        {
            EditorUtility.DisplayDialog("Delete Test UI", message, "OK");
            return;
        }

        ReloadSelectedPrefabReference();
        EnemyManagementDraftSession.MarkDirty();
        RefreshActiveStatus();
        BuildActiveDetailsSection();
    }

    private void RefreshTestUiStatus()
    {
        if (testUiStatusLabel == null)
            return;

        if (selectedEnemyPrefab == null)
        {
            testUiStatusLabel.text = "No enemy prefab selected.";
            return;
        }

        EnemyAuthoring authoring = selectedEnemyPrefab.GetComponentInChildren<EnemyAuthoring>(true);

        if (authoring == null)
        {
            testUiStatusLabel.text = "EnemyAuthoring component not found on prefab.";
            return;
        }

        EnemyWorldSpaceStatusBarsView assignedView = authoring.WorldSpaceStatusBarsView;

        if (assignedView == null)
        {
            testUiStatusLabel.text = "No world-space status bars view assigned on selected prefab.";
            return;
        }

        bool hasGeneratedTestUi = EnemyStatusBarsTestUiPrefabUtility.IsGeneratedTestUiView(assignedView);

        if (hasGeneratedTestUi)
        {
            testUiStatusLabel.text = "Generated test UI is assigned on selected prefab.";
            return;
        }

        testUiStatusLabel.text = "A custom world-space status bars view is assigned on selected prefab.";
    }

    private void ReloadSelectedPrefabReference()
    {
        if (selectedEnemyPrefab == null)
            return;

        string prefabPath = AssetDatabase.GetAssetPath(selectedEnemyPrefab);

        if (string.IsNullOrWhiteSpace(prefabPath))
            return;

        GameObject reloadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (reloadedPrefab == null)
            return;

        selectedEnemyPrefab = reloadedPrefab;

        if (enemyPrefabPopup != null)
            enemyPrefabPopup.SetValueWithoutNotify(selectedEnemyPrefab);

        SaveSelectedPrefabState();
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

        EnemyAuthoring authoring = ResolveEnemyAuthoringInPrefab(selectedEnemyPrefab);

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
        EnemyAdvancedPatternPresetsPanel advancedPatternPanel;
        VisualElement content = BuildSidePanelContent(panelType, out brainPanel, out advancedPatternPanel);

        if (content == null)
            return;

        AddTab(panelType, GetPanelTitle(panelType), content, brainPanel, advancedPatternPanel);
        SetActivePanel(panelType);
        SyncSidePanelSelection(panelType, sidePanels[panelType]);
    }

    /// <summary>
    /// Closes an opened side panel tab and updates persisted open panels state.
    /// Called by side tab close buttons.
    /// Takes in the panel enum to close.
    /// </summary>
    /// <param name="panelType">Side panel to close.</param>
    private void CloseSidePanel(EnemyManagementWindow.PanelType panelType)
    {
        // Prevent closing the root panel tab.
        if (panelType == EnemyManagementWindow.PanelType.EnemyMasterPresets)
            return;

        // Resolve side panel entry and remove its tab UI.
        if (!sidePanels.TryGetValue(panelType, out SidePanelEntry entry))
            return;

        if (entry != null && entry.TabContainer != null)
            entry.TabContainer.RemoveFromHierarchy();

        sidePanels.Remove(panelType);
        SaveOpenPanelsState();

        // Return to root panel if the currently active side panel was closed.
        if (activePanel == panelType)
            SetActivePanel(EnemyManagementWindow.PanelType.EnemyMasterPresets);
    }

    private string GetPanelTitle(EnemyManagementWindow.PanelType panelType)
    {
        if (panelType == EnemyManagementWindow.PanelType.EnemyBrainPresets)
            return "Enemy Brain Presets";

        if (panelType == EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets)
            return "Enemy Advanced Pattern Presets";

        return "Enemy Master Presets";
    }

    private VisualElement BuildSidePanelContent(EnemyManagementWindow.PanelType panelType,
                                                out EnemyBrainPresetsPanel brainPanel,
                                                out EnemyAdvancedPatternPresetsPanel advancedPatternPanel)
    {
        brainPanel = null;
        advancedPatternPanel = null;

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

        if (panelType == EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets)
        {
            advancedPatternPanel = new EnemyAdvancedPatternPresetsPanel();
            panelRoot.Add(advancedPatternPanel.Root);
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

    /// <summary>
    /// Adds a new tab entry for a panel and persists updated open tabs set.
    /// Called when creating root tab and opening side panels.
    /// Takes in panel identity, label, content root and optional panel controller.
    /// </summary>
    /// <param name="panelType">Panel enum represented by this tab.</param>
    /// <param name="label">Tab label.</param>
    /// <param name="content">Panel content root.</param>
    /// <param name="brainPanel">Optional brain panel controller instance.</param>
    /// <param name="advancedPatternPanel">Optional advanced pattern panel controller instance.</param>
    private void AddTab(EnemyManagementWindow.PanelType panelType,
                        string label,
                        VisualElement content,
                        EnemyBrainPresetsPanel brainPanel,
                        EnemyAdvancedPatternPresetsPanel advancedPatternPanel)
    {
        // Guard against missing tab bar during initialization.
        if (tabBar == null)
            return;

        // Build tab button container and bind panel activation.
        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;

        Button tabButton = new Button();
        tabButton.text = label;
        tabButton.clicked += () => SetActivePanel(panelType);
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabContainer.Add(tabButton);

        // Register side panel entry for selection and refresh synchronization.
        tabBar.Add(tabContainer);
        sidePanels[panelType] = new SidePanelEntry
        {
            TabContainer = tabContainer,
            TabButton = tabButton,
            Content = content,
            BrainPanel = brainPanel,
            AdvancedPatternPanel = advancedPatternPanel
        };
        SaveOpenPanelsState();
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
            return;
        }

        if (panelType == EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets)
        {
            if (entry.AdvancedPatternPanel == null)
                return;

            EnemyAdvancedPatternPreset advancedPatternPreset = selectedPreset.AdvancedPatternPreset;

            if (advancedPatternPreset == null)
                return;

            entry.AdvancedPatternPanel.SelectPresetFromExternal(advancedPatternPreset);
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

            if (entry.AdvancedPatternPanel != null)
                entry.AdvancedPatternPanel.RefreshFromSessionChange();
        }

        SyncOpenSidePanels();
    }

    /// <summary>
    /// Activates a panel tab, updates persisted active panel state, and swaps visible content.
    /// Called when clicking tabs and during state restore.
    /// Takes in panel enum to display.
    /// </summary>
    /// <param name="panelType">Panel to activate.</param>
    private void SetActivePanel(EnemyManagementWindow.PanelType panelType)
    {
        // Resolve target entry and guard against missing content host.
        if (!sidePanels.TryGetValue(panelType, out SidePanelEntry entry))
            return;

        if (contentHost == null)
            return;

        // Persist active panel unless initialization restore is suppressing writes.
        activePanel = panelType;

        if (!suppressStateWrite)
            ManagementToolStateUtility.SaveEnumValue(ActivePanelStateKey, activePanel);

        // Swap visible content and refresh tab visuals.
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

    /// <summary>
    /// Restores persisted panel/detail/prefab selections from EditorPrefs.
    /// Called by constructor before UI tree creation.
    /// </summary>
    private void RestorePersistedState()
    {
        // Restore active tab and details subsection selections.
        activePanel = ManagementToolStateUtility.LoadEnumValue(ActivePanelStateKey,
                                                                EnemyManagementWindow.PanelType.EnemyMasterPresets);
        activeDetailsSection = ManagementToolStateUtility.LoadEnumValue(ActiveDetailsSectionStateKey,
                                                                         DetailsSectionType.Metadata);
        // Restore last selected enemy prefab reference.
        selectedEnemyPrefab = ManagementToolStateUtility.LoadGameObjectAsset(SelectedPrefabPathStateKey);
    }

    /// <summary>
    /// Reopens side panels that were persisted as open in the previous session.
    /// Called during tab container initialization.
    /// </summary>
    private void RestoreOpenSidePanels()
    {
        // Load and replay open side panel list.
        List<EnemyManagementWindow.PanelType> openPanels = ManagementToolStateUtility.LoadEnumList<EnemyManagementWindow.PanelType>(OpenPanelsStateKey);

        for (int index = 0; index < openPanels.Count; index++)
        {
            EnemyManagementWindow.PanelType openPanel = openPanels[index];

            if (openPanel == EnemyManagementWindow.PanelType.EnemyMasterPresets)
                continue;

            OpenSidePanel(openPanel);
        }
    }

    /// <summary>
    /// Persists the current set of open tabs to EditorPrefs.
    /// Called after opening/closing side panels and after initialization restore.
    /// </summary>
    private void SaveOpenPanelsState()
    {
        // Skip writes while restoring startup state.
        if (suppressStateWrite)
            return;

        // Build deterministic list including root panel and currently opened side panels.
        List<EnemyManagementWindow.PanelType> openPanels = new List<EnemyManagementWindow.PanelType>();
        openPanels.Add(EnemyManagementWindow.PanelType.EnemyMasterPresets);

        if (sidePanels.ContainsKey(EnemyManagementWindow.PanelType.EnemyBrainPresets))
            openPanels.Add(EnemyManagementWindow.PanelType.EnemyBrainPresets);

        if (sidePanels.ContainsKey(EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets))
            openPanels.Add(EnemyManagementWindow.PanelType.EnemyAdvancedPatternPresets);

        ManagementToolStateUtility.SaveEnumList(OpenPanelsStateKey, openPanels);
    }

    /// <summary>
    /// Persists currently selected enemy prefab asset path.
    /// Called on prefab field changes and on "Find" action.
    /// </summary>
    private void SaveSelectedPrefabState()
    {
        ManagementToolStateUtility.SaveAssetPath(SelectedPrefabPathStateKey, selectedEnemyPrefab);
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
        public EnemyAdvancedPatternPresetsPanel AdvancedPatternPanel;
    }
    #endregion
}
