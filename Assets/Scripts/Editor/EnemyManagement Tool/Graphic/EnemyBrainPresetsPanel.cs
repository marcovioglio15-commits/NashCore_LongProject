using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for creating, editing, duplicating and deleting enemy brain presets.
/// </summary>
public sealed class EnemyBrainPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyBrainPreset> filteredPresets = new List<EnemyBrainPreset>();

    private EnemyBrainPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement detailsSectionButtonsRoot;
    private VisualElement detailsSectionContentRoot;
    private VisualElement brainSubSectionTabBar;
    private VisualElement brainSubSectionContentHost;
    private readonly Dictionary<BrainSubSectionType, BrainSubSectionTabEntry> brainSubSectionTabs = new Dictionary<BrainSubSectionType, BrainSubSectionTabEntry>();

    private EnemyBrainPreset selectedPreset;
    private SerializedObject presetSerializedObject;
    private SectionType activeSection = SectionType.Metadata;
    private BrainSubSectionType activeBrainSubSection = BrainSubSectionType.Movement;
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
    public EnemyBrainPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = EnemyBrainPresetLibraryUtility.GetOrCreateLibrary();

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    public void RefreshFromSessionChange()
    {
        EnemyBrainPreset previouslySelectedPreset = selectedPreset;
        library = EnemyBrainPresetLibraryUtility.GetOrCreateLibrary();
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

    public void SelectPresetFromExternal(EnemyBrainPreset preset)
    {
        if (preset == null)
            return;

        if (library == null)
            library = EnemyBrainPresetLibraryUtility.GetOrCreateLibrary();

        RefreshPresetList();
        int presetIndex = filteredPresets.IndexOf(preset);

        if (presetIndex < 0 && searchField != null && string.IsNullOrWhiteSpace(searchField.value) == false)
        {
            searchField.SetValueWithoutNotify(string.Empty);
            RefreshPresetList();
            presetIndex = filteredPresets.IndexOf(preset);
        }

        if (presetIndex < 0)
            return;

        if (listView != null)
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });

        SelectPreset(preset);
    }
    #endregion

    #region UI Construction
    private void BuildUI()
    {
        TwoPaneSplitView splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);

        VisualElement leftPane = BuildLeftPane();
        VisualElement rightPane = BuildRightPane();

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
            EnemyBrainPreset preset = label.userData as EnemyBrainPreset;

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

        EnemyBrainPreset preset = filteredPresets[index];

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
            EnemyBrainPreset preset = item as EnemyBrainPreset;

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
                EnemyBrainPreset preset = library.Presets[index];

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

    private bool IsMatchingSearch(EnemyBrainPreset preset, string searchText)
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
        EnemyBrainPreset newPreset = EnemyBrainPresetLibraryUtility.CreatePresetAsset("EnemyBrainPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Brain Preset Asset");
        Undo.RecordObject(library, "Add Enemy Brain Preset");
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

    private void DuplicatePreset(EnemyBrainPreset preset)
    {
        if (preset == null)
            return;

        EnemyBrainPreset duplicatedPreset = ScriptableObject.CreateInstance<EnemyBrainPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);
        duplicatedPreset.name = preset.name + " Copy";

        string originalPath = AssetDatabase.GetAssetPath(preset);
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(originalPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy Brain Preset Asset");

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");

        if (nameProperty != null)
            nameProperty.stringValue = duplicatedPreset.name;

        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(library, "Duplicate Enemy Brain Preset");
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

    private void DeletePreset(EnemyBrainPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Enemy Brain Preset", "Delete the selected preset asset?", "Delete", "Cancel");

        if (confirmed == false)
            return;

        Undo.RecordObject(library, "Delete Enemy Brain Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    private void SelectPreset(EnemyBrainPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();
        detailsSectionButtonsRoot = null;
        detailsSectionContentRoot = null;
        brainSubSectionTabBar = null;
        brainSubSectionContentHost = null;
        brainSubSectionTabs.Clear();

        if (selectedPreset == null)
        {
            Label label = new Label("Select or create a preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(label);
            return;
        }

        presetSerializedObject = new SerializedObject(selectedPreset);
        detailsSectionButtonsRoot = BuildDetailsSectionButtons();
        detailsSectionContentRoot = new VisualElement();
        detailsSectionContentRoot.style.flexDirection = FlexDirection.Column;
        detailsSectionContentRoot.style.flexGrow = 1f;
        detailsRoot.Add(detailsSectionButtonsRoot);
        detailsRoot.Add(detailsSectionContentRoot);

        BuildActiveDetailsSection();
    }

    private VisualElement BuildDetailsSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddDetailsSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddDetailsSectionButton(buttonsRoot, SectionType.Brain, "Brain");
        return buttonsRoot;
    }

    private void AddDetailsSectionButton(VisualElement parent, SectionType sectionType, string buttonLabel)
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
        BuildActiveDetailsSection();
    }

    private void BuildActiveDetailsSection()
    {
        if (detailsSectionContentRoot == null)
            return;

        if (presetSerializedObject == null)
            return;

        presetSerializedObject.Update();
        detailsSectionContentRoot.Clear();

        switch (activeSection)
        {
            case SectionType.Metadata:
                BuildMetadataSection();
                return;

            case SectionType.Brain:
                BuildBrainSection();
                return;
        }
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

    private void BuildBrainSection()
    {
        VisualElement sectionContainer = CreateDetailsSectionContainer("Brain");

        if (sectionContainer == null)
            return;

        brainSubSectionTabs.Clear();

        brainSubSectionTabBar = new VisualElement();
        brainSubSectionTabBar.style.flexDirection = FlexDirection.Row;
        brainSubSectionTabBar.style.flexWrap = Wrap.Wrap;
        brainSubSectionTabBar.style.marginBottom = 6f;
        brainSubSectionTabBar.style.paddingTop = 4f;
        brainSubSectionTabBar.style.paddingBottom = 4f;
        brainSubSectionTabBar.style.paddingLeft = 2f;

        brainSubSectionContentHost = new VisualElement();
        brainSubSectionContentHost.style.flexDirection = FlexDirection.Column;
        brainSubSectionContentHost.style.flexGrow = 1f;

        sectionContainer.Add(brainSubSectionTabBar);
        sectionContainer.Add(brainSubSectionContentHost);

        AddBrainSubSectionTab(BrainSubSectionType.Movement, "Movement", BuildMovementSubSection());
        AddBrainSubSectionTab(BrainSubSectionType.Steering, "Steering", BuildSteeringSubSection());
        AddBrainSubSectionTab(BrainSubSectionType.PlayerContact, "Player Contact", BuildPlayerContactSubSection());
        AddBrainSubSectionTab(BrainSubSectionType.HealthStatistics, "Health Statistics", BuildHealthStatisticsSubSection());
        AddBrainSubSectionTab(BrainSubSectionType.Visual, "Visual", BuildVisualSubSection());

        if (brainSubSectionTabs.ContainsKey(activeBrainSubSection) == false)
            activeBrainSubSection = BrainSubSectionType.Movement;

        SetActiveBrainSubSection(activeBrainSubSection);
    }

    private void SetActiveBrainSubSection(BrainSubSectionType subSectionType)
    {
        activeBrainSubSection = subSectionType;
        ShowActiveBrainSubSection();
    }

    private void AddBrainSubSectionTab(BrainSubSectionType subSectionType, string tabLabel, VisualElement content)
    {
        if (brainSubSectionTabBar == null)
            return;

        if (content == null)
            return;

        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;
        tabContainer.style.marginBottom = 4f;

        Button tabButton = new Button(() => SetActiveBrainSubSection(subSectionType));
        tabButton.text = tabLabel;
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabContainer.Add(tabButton);

        brainSubSectionTabBar.Add(tabContainer);
        brainSubSectionTabs[subSectionType] = new BrainSubSectionTabEntry
        {
            TabContainer = tabContainer,
            TabButton = tabButton,
            Content = content
        };
    }

    private void ShowActiveBrainSubSection()
    {
        if (brainSubSectionContentHost == null)
            return;

        if (brainSubSectionTabs.TryGetValue(activeBrainSubSection, out BrainSubSectionTabEntry tabEntry) == false)
            return;

        if (tabEntry == null || tabEntry.Content == null)
            return;

        brainSubSectionContentHost.Clear();
        brainSubSectionContentHost.Add(tabEntry.Content);
        UpdateBrainSubSectionTabStyles();
    }

    private void UpdateBrainSubSectionTabStyles()
    {
        foreach (KeyValuePair<BrainSubSectionType, BrainSubSectionTabEntry> tabEntry in brainSubSectionTabs)
        {
            if (tabEntry.Value == null || tabEntry.Value.TabButton == null)
                continue;

            bool isActive = tabEntry.Key == activeBrainSubSection;
            tabEntry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            tabEntry.Value.TabButton.style.backgroundColor = isActive ? new Color(0.18f, 0.18f, 0.18f, 0.6f) : Color.clear;
        }
    }

    private VisualElement BuildMovementSubSection()
    {
        SerializedProperty movementProperty = presetSerializedObject.FindProperty("movement");
        VisualElement container = CreateBrainSubSectionContainer("Movement");

        if (container == null)
            return null;

        AddPropertyField(container,
                         movementProperty,
                         "moveSpeed",
                         "Move Speed",
                         "Meters per second used as baseline enemy movement speed toward the player.");
        AddPropertyField(container,
                         movementProperty,
                         "maxSpeed",
                         "Max Speed",
                         "Hard cap applied to the enemy velocity magnitude.");
        AddPropertyField(container,
                         movementProperty,
                         "acceleration",
                         "Acceleration",
                         "Meters per second squared used to accelerate toward desired velocity.");
        AddPropertyField(container,
                         movementProperty,
                         "deceleration",
                         "Deceleration",
                         "Reserved deceleration value for future braking behaviors. Currently unused at runtime.");
        return container;
    }

    private VisualElement BuildSteeringSubSection()
    {
        SerializedProperty steeringProperty = presetSerializedObject.FindProperty("steering");
        VisualElement container = CreateBrainSubSectionContainer("Steering");

        if (container == null)
            return null;

        AddPropertyField(container,
                         steeringProperty,
                         "separationRadius",
                         "Separation Radius",
                         "Radius used to search neighboring enemies for separation steering.");
        AddPropertyField(container,
                         steeringProperty,
                         "separationWeight",
                         "Separation Weight",
                         "Weight applied to the separation vector before velocity clamping.");
        AddPropertyField(container,
                         steeringProperty,
                         "bodyRadius",
                         "Body Radius",
                         "Physical body radius used for projectile hit checks and overlap handling.");
        return container;
    }

    private VisualElement BuildPlayerContactSubSection()
    {
        SerializedProperty playerContactProperty = presetSerializedObject.FindProperty("playerContact");
        VisualElement container = CreateBrainSubSectionContainer("Player Contact");

        if (container == null)
            return null;

        AddPropertyField(container,
                         playerContactProperty,
                         "contactRadius",
                         "Contact Radius",
                         "Distance from enemy center to apply contact damage to the player.");
        AddPropertyField(container,
                         playerContactProperty,
                         "contactDamage",
                         "Contact Damage",
                         "Damage applied to the player each time contact cooldown expires in range.");
        AddPropertyField(container,
                         playerContactProperty,
                         "contactInterval",
                         "Contact Interval",
                         "Cooldown in seconds between two consecutive contact damage applications.");
        return container;
    }

    private VisualElement BuildHealthStatisticsSubSection()
    {
        SerializedProperty healthStatisticsProperty = presetSerializedObject.FindProperty("healthStatistics");
        VisualElement container = CreateBrainSubSectionContainer("Health Statistics");

        if (container == null)
            return null;

        AddPropertyField(container,
                         healthStatisticsProperty,
                         "maxHealth",
                         "Max Health",
                         "Maximum and initial health assigned to this enemy when spawned from pool.");
        AddPropertyField(container,
                         healthStatisticsProperty,
                         "maxShield",
                         "Max Shield",
                         "Maximum shield reserve assigned to this enemy at spawn. Currently unused at runtime.");
        return container;
    }

    private VisualElement BuildVisualSubSection()
    {
        SerializedProperty visualProperty = presetSerializedObject.FindProperty("visual");
        VisualElement container = CreateBrainSubSectionContainer("Visual");

        if (container == null)
            return null;

        AddPropertyField(container,
                         visualProperty,
                         "visualMode",
                         "Visual Mode",
                         "Visual runtime path: managed companion Animator (few actors) or GPU-baked playback (crowd scale).");
        AddPropertyField(container,
                         visualProperty,
                         "visualAnimationSpeed",
                         "Visual Animation Speed",
                         "Playback speed multiplier used by both companion and GPU-baked visual paths.");
        AddPropertyField(container,
                         visualProperty,
                         "gpuAnimationLoopDuration",
                         "GPU Animation Loop Duration",
                         "Loop duration in seconds used by GPU-baked playback time wrapping.");
        AddPropertyField(container,
                         visualProperty,
                         "enableDistanceCulling",
                         "Enable Distance Culling",
                         "Enable distance-based visual culling while gameplay simulation remains fully active.");
        AddPropertyField(container,
                         visualProperty,
                         "maxVisibleDistance",
                         "Max Visible Distance",
                         "Maximum planar distance from player where visuals stay visible. Set to 0 to keep always visible.");
        AddPropertyField(container,
                         visualProperty,
                         "visibleDistanceHysteresis",
                         "Visible Distance Hysteresis",
                         "Additional distance band used to avoid visual popping when crossing the culling boundary.");
        return container;
    }

    private VisualElement CreateDetailsSectionContainer(string sectionTitle)
    {
        if (detailsSectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        container.Add(header);
        detailsSectionContentRoot.Add(container);
        return container;
    }

    private VisualElement CreateBrainSubSectionContainer(string sectionTitle)
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 4f;

        Label header = new Label(sectionTitle + " Settings");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        container.Add(header);

        return container;
    }

    private void AddPropertyField(VisualElement panel,
                                  SerializedProperty parentProperty,
                                  string relativePropertyName,
                                  string label,
                                  string tooltip)
    {
        if (panel == null)
            return;

        if (parentProperty == null)
            return;

        SerializedProperty property = parentProperty.FindPropertyRelative(relativePropertyName);

        if (property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        propertyField.tooltip = tooltip;
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            EnemyManagementDraftSession.MarkDirty();
        });
        panel.Add(propertyField);
    }

    private void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Enemy Brain Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
    }

    private void HandlePresetNameChanged(string newName)
    {
        RenamePreset(selectedPreset, newName);
    }

    private void RenamePreset(EnemyBrainPreset preset, string newName)
    {
        if (preset == null)
            return;

        string normalizedName = EnemyManagementDraftSession.NormalizeAssetName(newName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return;

        SerializedObject presetSerialized = new SerializedObject(preset);
        SerializedProperty presetNameProperty = presetSerialized.FindProperty("presetName");

        if (presetNameProperty != null)
        {
            presetSerialized.Update();
            presetNameProperty.stringValue = normalizedName;
            presetSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        preset.name = normalizedName;
        EditorUtility.SetDirty(preset);
        EnemyManagementDraftSession.MarkDirty();
        RefreshPresetList();
    }

    private void ShowRenamePopup(VisualElement anchor, EnemyBrainPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        string title = "Rename Enemy Brain Preset";
        PresetRenamePopup.Show(anchorRect, title, preset.PresetName, newName => RenamePreset(preset, newName));
    }

    private string GetPresetDisplayName(EnemyBrainPreset preset)
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
    private enum SectionType
    {
        Metadata = 0,
        Brain = 1
    }

    private enum BrainSubSectionType
    {
        Movement = 0,
        Steering = 1,
        PlayerContact = 2,
        HealthStatistics = 3,
        Visual = 4
    }

    private sealed class BrainSubSectionTabEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
    }
    #endregion
}
