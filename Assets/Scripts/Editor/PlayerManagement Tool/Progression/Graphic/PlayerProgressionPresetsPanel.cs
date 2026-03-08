using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for managing, creating, editing, and deleting player progression presets.
/// </summary>
public sealed class PlayerProgressionPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.PlayerManagement.Progression.ActiveSection";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<PlayerProgressionPreset> filteredPresets = new List<PlayerProgressionPreset>();
    private readonly PlayerProgressionPresetLibrary library;

    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement sectionButtonsRoot;
    private VisualElement sectionContentRoot;

    private PlayerProgressionPreset selectedPreset;
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

    #region Constructors
    public PlayerProgressionPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = PlayerProgressionPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    public void SelectPresetFromExternal(PlayerProgressionPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        RefreshPresetList();

        int index = filteredPresets.IndexOf(preset);

        if (index < 0)
        {
            return;
        }

        if (listView == null)
        {
            SelectPreset(preset);
            return;
        }

        if (listView.selectedIndex != index)
        {
            listView.SetSelection(index);
            return;
        }

        SelectPreset(preset);
    }

    public void RefreshFromSessionChange()
    {
        PlayerProgressionPreset previouslySelectedPreset = selectedPreset;
        RefreshPresetList();

        if (previouslySelectedPreset == null)
        {
            return;
        }

        int presetIndex = filteredPresets.IndexOf(previouslySelectedPreset);

        if (presetIndex < 0)
        {
            return;
        }

        if (listView != null)
        {
            listView.SetSelectionWithoutNotify(new int[] { presetIndex });
        }

        SelectPreset(previouslySelectedPreset);
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
            PlayerProgressionPreset preset = label.userData as PlayerProgressionPreset;

            if (preset == null)
            {
                return;
            }

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
        {
            return;
        }

        if (index < 0 || index >= filteredPresets.Count)
        {
            label.text = string.Empty;
            label.userData = null;
            return;
        }

        PlayerProgressionPreset preset = filteredPresets[index];

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
            PlayerProgressionPreset preset = item as PlayerProgressionPreset;

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
                PlayerProgressionPreset preset = library.Presets[index];

                if (preset == null)
                {
                    continue;
                }

                if (PlayerManagementDraftSession.IsAssetStagedForDeletion(preset))
                {
                    continue;
                }

                if (IsMatchingSearch(preset, searchText))
                {
                    filteredPresets.Add(preset);
                }
            }
        }

        if (listView != null)
        {
            listView.Rebuild();
        }

        if (filteredPresets.Count == 0)
        {
            SelectPreset(null);
            return;
        }

        if (selectedPreset == null || filteredPresets.Contains(selectedPreset) == false)
        {
            SelectPreset(filteredPresets[0]);

            if (listView != null)
            {
                listView.SetSelectionWithoutNotify(new int[] { 0 });
            }
        }
    }

    private bool IsMatchingSearch(PlayerProgressionPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        string currentPresetName = preset.PresetName;

        if (string.IsNullOrWhiteSpace(currentPresetName))
        {
            return false;
        }

        return currentPresetName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #region Preset Actions
    private void CreatePreset()
    {
        PlayerProgressionPreset newPreset = PlayerProgressionPresetLibraryUtility.CreatePresetAsset("PlayerProgressionPreset");

        if (newPreset == null)
        {
            return;
        }

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Progression Preset Asset");
        Undo.RecordObject(library, "Add Progression Preset");
        library.AddPreset(newPreset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(newPreset);

        int index = filteredPresets.IndexOf(newPreset);

        if (index >= 0)
        {
            listView.SetSelection(index);
        }
    }

    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    private void DuplicatePreset(PlayerProgressionPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        PlayerProgressionPreset duplicatedPreset = ScriptableObject.CreateInstance<PlayerProgressionPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);

        string originalPath = AssetDatabase.GetAssetPath(preset);

        if (string.IsNullOrWhiteSpace(originalPath))
        {
            return;
        }

        string originalDirectory = Path.GetDirectoryName(originalPath);

        if (string.IsNullOrWhiteSpace(originalDirectory))
        {
            return;
        }

        string sourceDisplayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string duplicateBaseName = PlayerManagementDraftSession.NormalizeAssetName(sourceDisplayName + " Copy");

        if (string.IsNullOrWhiteSpace(duplicateBaseName))
        {
            duplicateBaseName = "PlayerProgressionPreset Copy";
        }

        string requestedPath = Path.Combine(originalDirectory, duplicateBaseName + ".asset").Replace('\\', '/');
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        string finalName = Path.GetFileNameWithoutExtension(duplicatedPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Progression Preset Asset");
        duplicatedPreset.name = finalName;

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
        {
            idProperty.stringValue = Guid.NewGuid().ToString("N");
        }

        if (nameProperty != null)
        {
            nameProperty.stringValue = finalName;
        }

        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(library, "Duplicate Progression Preset");
        library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(duplicatedPreset);

        int index = filteredPresets.IndexOf(duplicatedPreset);

        if (index >= 0)
        {
            listView.SetSelection(index);
        }
    }

    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    private void DeletePreset(PlayerProgressionPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog("Delete Progression Preset", "Delete the selected progression preset asset?", "Delete", "Cancel");

        if (confirmed == false)
        {
            return;
        }

        Undo.RecordObject(library, "Delete Progression Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        PlayerManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    private void SelectPreset(PlayerProgressionPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();
        sectionButtonsRoot = null;
        sectionContentRoot = null;

        if (selectedPreset == null)
        {
            Label label = new Label("Select or create a progression preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(label);
            return;
        }

        presetSerializedObject = new SerializedObject(selectedPreset);
        sectionButtonsRoot = BuildSectionButtons();
        sectionContentRoot = new VisualElement();
        sectionContentRoot.style.flexGrow = 1f;

        detailsRoot.Add(sectionButtonsRoot);
        detailsRoot.Add(sectionContentRoot);

        BuildActiveSection();
    }

    private void BuildMetadataSection()
    {
        Label header = new Label("Preset Details");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        sectionContentRoot.Add(header);

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
        sectionContentRoot.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            RefreshPresetList();
        });
        sectionContentRoot.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            RefreshPresetList();
        });
        sectionContentRoot.Add(descriptionField);

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

        sectionContentRoot.Add(idRow);
    }

    private void BuildScalableStatsSection()
    {
        VisualElement scalableStatsContainer = CreateSectionContainer("Scalable Stats");
        SerializedProperty scalableStatsProperty = presetSerializedObject.FindProperty("scalableStats");
        SerializedProperty scalingRulesProperty = presetSerializedObject.FindProperty("scalingRules");

        if (scalableStatsProperty == null || scalingRulesProperty == null)
        {
            Label missingLabel = new Label("Scalable stats data is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            scalableStatsContainer.Add(missingLabel);
            return;
        }

        Label infoLabel = new Label("Manage custom variables used by formulas. Use [this] to reference current field value.");
        infoLabel.style.marginBottom = 4f;
        scalableStatsContainer.Add(infoLabel);

        VisualElement warningsRoot = new VisualElement();
        warningsRoot.style.marginBottom = 4f;
        scalableStatsContainer.Add(warningsRoot);
        RefreshScalableStatsWarnings(warningsRoot, scalableStatsProperty, scalingRulesProperty);

        PropertyField scalableStatsField = new PropertyField(scalableStatsProperty, "Scalable Stats");
        scalableStatsField.BindProperty(scalableStatsProperty);
        scalableStatsField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            RefreshScalableStatsWarnings(warningsRoot, scalableStatsProperty, scalingRulesProperty);
        });
        scalableStatsContainer.Add(scalableStatsField);

        scalableStatsContainer.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            RefreshScalableStatsWarnings(warningsRoot, scalableStatsProperty, scalingRulesProperty);
        });

        scalableStatsContainer.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            RefreshScalableStatsWarnings(warningsRoot, scalableStatsProperty, scalingRulesProperty);
        });
    }

    private void BuildMilestonesSection()
    {
        VisualElement milestonesContainer = CreateSectionContainer("Milestones");
        SerializedProperty gamePhasesDefinitionProperty = presetSerializedObject.FindProperty("gamePhasesDefinition");
        SerializedProperty experiencePickupRadiusProperty = presetSerializedObject.FindProperty("experiencePickupRadius");

        if (gamePhasesDefinitionProperty == null || experiencePickupRadiusProperty == null)
        {
            Label missingLabel = new Label("Milestones data is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            milestonesContainer.Add(missingLabel);
            return;
        }

        Label infoLabel = new Label("Define game phases, linear growth, and milestone spikes used by runtime level-up.");
        infoLabel.style.marginBottom = 4f;
        milestonesContainer.Add(infoLabel);

        Foldout gamePhasesFoldout = new Foldout();
        gamePhasesFoldout.text = "Game Phases Definition";
        gamePhasesFoldout.value = true;
        gamePhasesFoldout.style.marginBottom = 4f;
        milestonesContainer.Add(gamePhasesFoldout);

        PropertyField gamePhasesField = new PropertyField(gamePhasesDefinitionProperty, "Phases");
        gamePhasesField.BindProperty(gamePhasesDefinitionProperty);
        gamePhasesField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
        });
        gamePhasesFoldout.Add(gamePhasesField);

        PropertyField pickupRadiusField = new PropertyField(experiencePickupRadiusProperty, "Experience Pickup Radius");
        pickupRadiusField.BindProperty(experiencePickupRadiusProperty);
        pickupRadiusField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
        });
        milestonesContainer.Add(pickupRadiusField);
    }

    private void RefreshScalableStatsWarnings(VisualElement warningsRoot,
                                              SerializedProperty scalableStatsProperty,
                                              SerializedProperty scalingRulesProperty)
    {
        if (warningsRoot == null || scalableStatsProperty == null)
            return;

        warningsRoot.Clear();

        for (int statIndex = 0; statIndex < scalableStatsProperty.arraySize; statIndex++)
        {
            SerializedProperty statElementProperty = scalableStatsProperty.GetArrayElementAtIndex(statIndex);
            SerializedProperty statNameProperty = statElementProperty != null ? statElementProperty.FindPropertyRelative("statName") : null;
            string statName = statNameProperty != null ? statNameProperty.stringValue : string.Empty;
            string warningText = ValidateScalableStatEntry(statName, statIndex, scalableStatsProperty);

            if (string.IsNullOrWhiteSpace(warningText))
                continue;

            HelpBox warningBox = new HelpBox(string.Format("Stat {0}: {1}", statIndex + 1, warningText), HelpBoxMessageType.Warning);
            warningBox.style.marginBottom = 2f;
            warningsRoot.Add(warningBox);
        }

        List<string> dependencyWarnings = PlayerScalingDependencyValidationUtility.BuildScalableStatsDependencyWarnings(scalableStatsProperty,
                                                                                                                         scalingRulesProperty);

        for (int warningIndex = 0; warningIndex < dependencyWarnings.Count; warningIndex++)
        {
            string dependencyWarning = dependencyWarnings[warningIndex];

            if (string.IsNullOrWhiteSpace(dependencyWarning))
                continue;

            HelpBox warningBox = new HelpBox(dependencyWarning, HelpBoxMessageType.Warning);
            warningBox.style.marginBottom = 2f;
            warningsRoot.Add(warningBox);
        }
    }

    private string ValidateScalableStatEntry(string statName, int statIndex, SerializedProperty scalableStatsProperty)
    {
        if (PlayerScalableStatNameUtility.IsValid(statName) == false)
            return "Invalid name. Use letters/digits/underscore, start with letter or underscore, and avoid 'this'.";

        for (int index = 0; index < scalableStatsProperty.arraySize; index++)
        {
            if (index == statIndex)
                continue;

            SerializedProperty otherStatElement = scalableStatsProperty.GetArrayElementAtIndex(index);
            SerializedProperty otherStatNameProperty = otherStatElement != null ? otherStatElement.FindPropertyRelative("statName") : null;

            if (otherStatNameProperty == null)
                continue;

            if (string.Equals(otherStatNameProperty.stringValue, statName, StringComparison.OrdinalIgnoreCase) == false)
                continue;

            return "Duplicate name.";
        }

        return string.Empty;
    }

    private VisualElement BuildSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddSectionButton(buttonsRoot, SectionType.Milestones, "Milestones");
        AddSectionButton(buttonsRoot, SectionType.ScalableStats, "Scalable Stats");
        return buttonsRoot;
    }

    private void AddSectionButton(VisualElement parent, SectionType sectionType, string label)
    {
        Button button = new Button(() => SetActiveSection(sectionType));
        button.text = label;
        button.style.marginRight = 4f;
        button.style.marginBottom = 4f;
        parent.Add(button);
    }

    /// <summary>
    /// Sets active details section and persists the selection for reopen.
    /// Called by section buttons in progression preset details.
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
            case SectionType.Milestones:
                BuildMilestonesSection();
                return;
            case SectionType.ScalableStats:
                BuildScalableStatsSection();
                return;
        }
    }

    private VisualElement CreateSectionContainer(string sectionTitle)
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 8f;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        container.Add(header);

        if (sectionContentRoot != null)
            sectionContentRoot.Add(container);

        return container;
    }

    private void RegeneratePresetId()
    {
        if (selectedPreset == null)
        {
            return;
        }

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
        {
            return;
        }

        Undo.RecordObject(selectedPreset, "Regenerate Progression Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
    }

    private void HandlePresetNameChanged(string newName)
    {
        RenamePreset(selectedPreset, newName);
    }

    private void RenamePreset(PlayerProgressionPreset preset, string newName)
    {
        if (preset == null)
        {
            return;
        }

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(newName);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

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

    private void ShowRenamePopup(VisualElement anchor, PlayerProgressionPreset preset)
    {
        if (anchor == null || preset == null)
        {
            return;
        }

        Rect anchorRect = anchor.worldBound;
        PresetRenamePopup.Show(anchorRect, "Rename Progression Preset", preset.PresetName, newName => RenamePreset(preset, newName));
    }

    private string GetPresetDisplayName(PlayerProgressionPreset preset)
    {
        if (preset == null)
        {
            return "<Missing Preset>";
        }

        string currentPresetName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string currentVersion = preset.Version;

        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return currentPresetName;
        }

        return currentPresetName + " v. " + currentVersion;
    }
    #endregion

    #endregion

    #region Nested Types
    private enum SectionType
    {
        Metadata = 0,
        Milestones = 1,
        ScalableStats = 2
    }
    #endregion
}
