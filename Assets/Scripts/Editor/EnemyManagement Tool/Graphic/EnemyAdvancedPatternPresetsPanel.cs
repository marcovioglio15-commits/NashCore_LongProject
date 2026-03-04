using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for creating, editing, duplicating and deleting enemy advanced pattern presets.
/// </summary>
public sealed class EnemyAdvancedPatternPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ActiveSectionStateKey = "NashCore.EnemyManagement.AdvancedPattern.ActiveSection";
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<EnemyAdvancedPatternPreset> filteredPresets = new List<EnemyAdvancedPatternPreset>();

    private EnemyAdvancedPatternPresetLibrary library;
    private ListView listView;
    private ToolbarSearchField searchField;
    private VisualElement detailsRoot;
    private VisualElement detailsSectionButtonsRoot;
    private VisualElement detailsSectionContentRoot;

    private EnemyAdvancedPatternPreset selectedPreset;
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
    public EnemyAdvancedPatternPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        library = EnemyAdvancedPatternPresetLibraryUtility.GetOrCreateLibrary();
        activeSection = ManagementToolStateUtility.LoadEnumValue(ActiveSectionStateKey, SectionType.Metadata);

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods

    #region Public Methods
    public void RefreshFromSessionChange()
    {
        EnemyAdvancedPatternPreset previouslySelectedPreset = selectedPreset;
        library = EnemyAdvancedPatternPresetLibraryUtility.GetOrCreateLibrary();
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

    public void SelectPresetFromExternal(EnemyAdvancedPatternPreset preset)
    {
        if (preset == null)
            return;

        if (library == null)
            library = EnemyAdvancedPatternPresetLibraryUtility.GetOrCreateLibrary();

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
            EnemyAdvancedPatternPreset preset = label.userData as EnemyAdvancedPatternPreset;

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

        EnemyAdvancedPatternPreset preset = filteredPresets[index];

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
            EnemyAdvancedPatternPreset preset = item as EnemyAdvancedPatternPreset;

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
                EnemyAdvancedPatternPreset preset = library.Presets[index];

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

    private static bool IsMatchingSearch(EnemyAdvancedPatternPreset preset, string searchText)
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
        EnemyAdvancedPatternPreset newPreset = EnemyAdvancedPatternPresetLibraryUtility.CreatePresetAsset("EnemyAdvancedPatternPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Enemy Advanced Pattern Preset Asset");
        Undo.RecordObject(library, "Add Enemy Advanced Pattern Preset");
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

    private void DuplicatePreset(EnemyAdvancedPatternPreset preset)
    {
        if (preset == null)
            return;

        EnemyAdvancedPatternPreset duplicatedPreset = ScriptableObject.CreateInstance<EnemyAdvancedPatternPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);
        duplicatedPreset.name = preset.name + " Copy";

        string originalPath = AssetDatabase.GetAssetPath(preset);
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(originalPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Enemy Advanced Pattern Preset Asset");

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");

        if (nameProperty != null)
            nameProperty.stringValue = duplicatedPreset.name;

        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(library, "Duplicate Enemy Advanced Pattern Preset");
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

    private void DeletePreset(EnemyAdvancedPatternPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Enemy Advanced Pattern Preset", "Delete the selected preset asset?", "Delete", "Cancel");

        if (confirmed == false)
            return;

        Undo.RecordObject(library, "Delete Enemy Advanced Pattern Preset");
        library.RemovePreset(preset);
        EditorUtility.SetDirty(library);
        EnemyManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    private void SelectPreset(EnemyAdvancedPatternPreset preset)
    {
        selectedPreset = preset;
        detailsRoot.Clear();
        detailsSectionButtonsRoot = null;
        detailsSectionContentRoot = null;

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
        AddDetailsSectionButton(buttonsRoot, SectionType.ModulesDefinition, "Modules Definition");
        AddDetailsSectionButton(buttonsRoot, SectionType.PatternAssemble, "Pattern Assemble");
        AddDetailsSectionButton(buttonsRoot, SectionType.PatternLoadout, "Pattern Loadout");
        return buttonsRoot;
    }

    private static void AddDetailsSectionButton(VisualElement parent, SectionType sectionType, string buttonLabel)
    {
        Button sectionButton = new Button(() => SetSectionFromButton(parent, sectionType));
        sectionButton.text = buttonLabel;
        sectionButton.style.marginRight = 4f;
        sectionButton.style.marginBottom = 4f;
        parent.Add(sectionButton);
    }

    private static void SetSectionFromButton(VisualElement parent, SectionType sectionType)
    {
        if (parent == null)
            return;

        EnemyAdvancedPatternPresetsPanel panel = parent.userData as EnemyAdvancedPatternPresetsPanel;

        if (panel == null)
            return;

        panel.SetActiveSection(sectionType);
    }

    private void SetActiveSection(SectionType sectionType)
    {
        activeSection = sectionType;
        ManagementToolStateUtility.SaveEnumValue(ActiveSectionStateKey, activeSection);
        BuildActiveDetailsSection();
    }

    private void BuildActiveDetailsSection()
    {
        if (detailsSectionButtonsRoot != null)
            detailsSectionButtonsRoot.userData = this;

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

            case SectionType.ModulesDefinition:
                BuildModulesDefinitionSection();
                return;

            case SectionType.PatternAssemble:
                BuildPatternAssembleSection();
                return;

            case SectionType.PatternLoadout:
                BuildPatternLoadoutSection();
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

        AddTrackedPropertyField(sectionContainer, nameProperty, "Preset Name");
        AddTrackedPropertyField(sectionContainer, versionProperty, "Version");

        PropertyField descriptionField = new PropertyField(descriptionProperty, "Description");
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangeCallback(evt =>
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

    private void BuildModulesDefinitionSection()
    {
        VisualElement sectionContainer = CreateDetailsSectionContainer("Modules Definition");

        if (sectionContainer == null)
            return;

        Label infoLabel = new Label("Reusable module catalog used by Pattern Assemble entries.");
        infoLabel.style.marginBottom = 4f;
        sectionContainer.Add(infoLabel);

        SerializedProperty property = presetSerializedObject.FindProperty("moduleDefinitions");
        AddTrackedPropertyField(sectionContainer, property, "Modules");
    }

    private void BuildPatternAssembleSection()
    {
        VisualElement sectionContainer = CreateDetailsSectionContainer("Pattern Assemble");

        if (sectionContainer == null)
            return;

        Label infoLabel = new Label("Assembled patterns built from module bindings.");
        infoLabel.style.marginBottom = 4f;
        sectionContainer.Add(infoLabel);

        SerializedProperty property = presetSerializedObject.FindProperty("patterns");
        AddTrackedPropertyField(sectionContainer, property, "Patterns");
    }

    private void BuildPatternLoadoutSection()
    {
        VisualElement sectionContainer = CreateDetailsSectionContainer("Pattern Loadout");

        if (sectionContainer == null)
        {
            return;
        }

        Label loadoutHeader = new Label("Active Pattern IDs");
        loadoutHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        loadoutHeader.style.marginTop = 2f;
        loadoutHeader.style.marginBottom = 2f;
        sectionContainer.Add(loadoutHeader);

        SerializedProperty patternsProperty = presetSerializedObject.FindProperty("patterns");
        SerializedProperty activePatternIdsProperty = presetSerializedObject.FindProperty("activePatternIds");

        if (patternsProperty == null || activePatternIdsProperty == null)
        {
            HelpBox missingPropertiesBox = new HelpBox("Pattern assemble or loadout properties are missing on this preset.", HelpBoxMessageType.Warning);
            sectionContainer.Add(missingPropertiesBox);
            return;
        }

        List<PatternLoadoutOption> loadoutOptions = BuildPatternLoadoutOptions(patternsProperty);

        if (loadoutOptions.Count <= 0)
        {
            HelpBox noOptionsBox = new HelpBox("No valid patterns found. Add patterns in Pattern Assemble first.", HelpBoxMessageType.Warning);
            sectionContainer.Add(noOptionsBox);
            return;
        }

        BuildPatternLoadoutArray(activePatternIdsProperty, loadoutOptions, sectionContainer);
    }

    private void BuildPatternLoadoutArray(SerializedProperty activePatternIdsProperty,
                                          List<PatternLoadoutOption> loadoutOptions,
                                          VisualElement sectionContainer)
    {
        if (activePatternIdsProperty == null)
        {
            return;
        }

        if (loadoutOptions == null || loadoutOptions.Count <= 0)
        {
            return;
        }

        bool normalized = NormalizePatternLoadoutArray(activePatternIdsProperty, loadoutOptions);

        if (normalized)
        {
            presetSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            presetSerializedObject.Update();
        }

        List<string> optionLabels = new List<string>();

        for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
        {
            optionLabels.Add(loadoutOptions[optionIndex].DisplayLabel);
        }

        for (int index = 0; index < activePatternIdsProperty.arraySize; index++)
        {
            SerializedProperty patternIdProperty = activePatternIdsProperty.GetArrayElementAtIndex(index);

            if (patternIdProperty == null)
            {
                continue;
            }

            string selectedPatternId = ResolveSelectedPatternId(patternIdProperty.stringValue, loadoutOptions);
            int selectedOptionIndex = 0;

            for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
            {
                if (string.Equals(loadoutOptions[optionIndex].PatternId, selectedPatternId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedOptionIndex = optionIndex;
                    break;
                }
            }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;

            PopupField<string> selector = new PopupField<string>("Pattern " + (index + 1), optionLabels, selectedOptionIndex);
            selector.tooltip = "Select one assembled pattern ID for active runtime loadout.";
            selector.style.flexGrow = 1f;
            int capturedIndex = index;
            selector.RegisterValueChangedCallback(evt =>
            {
                int optionIndex = optionLabels.IndexOf(evt.newValue);

                if (optionIndex < 0 || optionIndex >= loadoutOptions.Count)
                {
                    return;
                }

                string patternId = loadoutOptions[optionIndex].PatternId;
                SetPatternLoadoutEntry(activePatternIdsProperty, capturedIndex, patternId, loadoutOptions);
            });
            row.Add(selector);

            Button removeButton = new Button(() =>
            {
                RemovePatternLoadoutEntry(activePatternIdsProperty, capturedIndex, loadoutOptions);
            });
            removeButton.text = "Remove";
            removeButton.tooltip = "Remove this pattern entry from active loadout.";
            removeButton.style.marginLeft = 6f;
            removeButton.SetEnabled(activePatternIdsProperty.arraySize > 1);
            row.Add(removeButton);

            sectionContainer.Add(row);
        }

        if (activePatternIdsProperty.arraySize <= 0)
        {
            HelpBox emptyLoadoutBox = new HelpBox("No active patterns in loadout. Add one entry.", HelpBoxMessageType.Info);
            sectionContainer.Add(emptyLoadoutBox);
        }

        Button addButton = new Button(() =>
        {
            AddPatternLoadoutEntry(activePatternIdsProperty, loadoutOptions);
        });
        addButton.text = "Add Pattern";
        addButton.tooltip = "Add one more active pattern entry to the loadout.";
        addButton.style.marginTop = 2f;
        addButton.SetEnabled(CanAddPatternLoadoutEntry(activePatternIdsProperty, loadoutOptions));
        sectionContainer.Add(addButton);
    }

    private void AddPatternLoadoutEntry(SerializedProperty activePatternIdsProperty, List<PatternLoadoutOption> loadoutOptions)
    {
        if (activePatternIdsProperty == null)
        {
            return;
        }

        if (loadoutOptions == null || loadoutOptions.Count <= 0)
        {
            return;
        }

        string nextPatternId = ResolveNextPatternLoadoutId(activePatternIdsProperty, loadoutOptions);

        if (string.IsNullOrWhiteSpace(nextPatternId))
        {
            return;
        }

        if (selectedPreset != null)
        {
            Undo.RecordObject(selectedPreset, "Add Enemy Pattern Loadout Entry");
        }

        presetSerializedObject.Update();
        int insertIndex = activePatternIdsProperty.arraySize;
        activePatternIdsProperty.InsertArrayElementAtIndex(insertIndex);
        SerializedProperty insertedProperty = activePatternIdsProperty.GetArrayElementAtIndex(insertIndex);

        if (insertedProperty != null)
        {
            insertedProperty.stringValue = nextPatternId;
        }

        NormalizePatternLoadoutArray(activePatternIdsProperty, loadoutOptions);
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
        RefreshPresetList();
        BuildActiveDetailsSection();
    }

    private void RemovePatternLoadoutEntry(SerializedProperty activePatternIdsProperty,
                                           int entryIndex,
                                           List<PatternLoadoutOption> loadoutOptions)
    {
        if (activePatternIdsProperty == null)
        {
            return;
        }

        if (entryIndex < 0 || entryIndex >= activePatternIdsProperty.arraySize)
        {
            return;
        }

        if (selectedPreset != null)
        {
            Undo.RecordObject(selectedPreset, "Remove Enemy Pattern Loadout Entry");
        }

        presetSerializedObject.Update();
        activePatternIdsProperty.DeleteArrayElementAtIndex(entryIndex);
        NormalizePatternLoadoutArray(activePatternIdsProperty, loadoutOptions);
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
        RefreshPresetList();
        BuildActiveDetailsSection();
    }

    private void SetPatternLoadoutEntry(SerializedProperty activePatternIdsProperty,
                                        int entryIndex,
                                        string patternId,
                                        List<PatternLoadoutOption> loadoutOptions)
    {
        if (activePatternIdsProperty == null)
        {
            return;
        }

        if (entryIndex < 0 || entryIndex >= activePatternIdsProperty.arraySize)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        SerializedProperty entryProperty = activePatternIdsProperty.GetArrayElementAtIndex(entryIndex);

        if (entryProperty == null)
        {
            return;
        }

        if (string.Equals(entryProperty.stringValue, patternId, StringComparison.Ordinal))
        {
            return;
        }

        if (selectedPreset != null)
        {
            Undo.RecordObject(selectedPreset, "Change Enemy Pattern Loadout Entry");
        }

        presetSerializedObject.Update();
        entryProperty.stringValue = patternId;
        NormalizePatternLoadoutArray(activePatternIdsProperty, loadoutOptions);
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
        RefreshPresetList();
        BuildActiveDetailsSection();
    }

    private static List<PatternLoadoutOption> BuildPatternLoadoutOptions(SerializedProperty patternsProperty)
    {
        List<PatternLoadoutOption> options = new List<PatternLoadoutOption>();

        if (patternsProperty == null)
        {
            return options;
        }

        for (int index = 0; index < patternsProperty.arraySize; index++)
        {
            SerializedProperty patternProperty = patternsProperty.GetArrayElementAtIndex(index);

            if (patternProperty == null)
            {
                continue;
            }

            SerializedProperty patternIdProperty = patternProperty.FindPropertyRelative("patternId");
            SerializedProperty displayNameProperty = patternProperty.FindPropertyRelative("displayName");

            if (patternIdProperty == null)
            {
                continue;
            }

            string patternId = patternIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(patternId))
            {
                continue;
            }

            if (ContainsPatternOption(options, patternId))
            {
                continue;
            }

            string displayName = string.Empty;

            if (displayNameProperty != null)
            {
                displayName = displayNameProperty.stringValue;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = patternId;
            }

            options.Add(new PatternLoadoutOption
            {
                PatternId = patternId,
                DisplayLabel = displayName + " (" + patternId + ")"
            });
        }

        return options;
    }

    private static bool NormalizePatternLoadoutArray(SerializedProperty activePatternIdsProperty, List<PatternLoadoutOption> loadoutOptions)
    {
        if (activePatternIdsProperty == null)
        {
            return false;
        }

        if (loadoutOptions == null || loadoutOptions.Count <= 0)
        {
            return false;
        }

        HashSet<string> validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
        {
            validIds.Add(loadoutOptions[optionIndex].PatternId);
        }

        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool changed = false;

        for (int index = 0; index < activePatternIdsProperty.arraySize; index++)
        {
            SerializedProperty entryProperty = activePatternIdsProperty.GetArrayElementAtIndex(index);

            if (entryProperty == null)
            {
                activePatternIdsProperty.DeleteArrayElementAtIndex(index);
                index--;
                changed = true;
                continue;
            }

            string patternId = entryProperty.stringValue;

            if (string.IsNullOrWhiteSpace(patternId))
            {
                activePatternIdsProperty.DeleteArrayElementAtIndex(index);
                index--;
                changed = true;
                continue;
            }

            if (validIds.Contains(patternId) == false)
            {
                activePatternIdsProperty.DeleteArrayElementAtIndex(index);
                index--;
                changed = true;
                continue;
            }

            if (visited.Add(patternId) == false)
            {
                activePatternIdsProperty.DeleteArrayElementAtIndex(index);
                index--;
                changed = true;
            }
        }

        if (activePatternIdsProperty.arraySize <= 0)
        {
            activePatternIdsProperty.InsertArrayElementAtIndex(0);
            SerializedProperty firstEntry = activePatternIdsProperty.GetArrayElementAtIndex(0);

            if (firstEntry != null)
            {
                firstEntry.stringValue = loadoutOptions[0].PatternId;
            }

            changed = true;
        }

        return changed;
    }

    private static bool CanAddPatternLoadoutEntry(SerializedProperty activePatternIdsProperty, List<PatternLoadoutOption> loadoutOptions)
    {
        if (activePatternIdsProperty == null)
        {
            return false;
        }

        if (loadoutOptions == null || loadoutOptions.Count <= 0)
        {
            return false;
        }

        for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
        {
            string patternId = loadoutOptions[optionIndex].PatternId;

            if (ContainsPatternLoadoutId(activePatternIdsProperty, patternId) == false)
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveNextPatternLoadoutId(SerializedProperty activePatternIdsProperty, List<PatternLoadoutOption> loadoutOptions)
    {
        if (activePatternIdsProperty == null)
        {
            return string.Empty;
        }

        if (loadoutOptions == null || loadoutOptions.Count <= 0)
        {
            return string.Empty;
        }

        for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
        {
            string patternId = loadoutOptions[optionIndex].PatternId;

            if (ContainsPatternLoadoutId(activePatternIdsProperty, patternId))
            {
                continue;
            }

            return patternId;
        }

        return string.Empty;
    }

    private static bool ContainsPatternLoadoutId(SerializedProperty activePatternIdsProperty, string patternId)
    {
        if (activePatternIdsProperty == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < activePatternIdsProperty.arraySize; index++)
        {
            SerializedProperty entryProperty = activePatternIdsProperty.GetArrayElementAtIndex(index);

            if (entryProperty == null)
            {
                continue;
            }

            if (string.Equals(entryProperty.stringValue, patternId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveSelectedPatternId(string selectedPatternId, List<PatternLoadoutOption> loadoutOptions)
    {
        if (loadoutOptions == null || loadoutOptions.Count <= 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(selectedPatternId) == false)
        {
            for (int optionIndex = 0; optionIndex < loadoutOptions.Count; optionIndex++)
            {
                if (string.Equals(loadoutOptions[optionIndex].PatternId, selectedPatternId, StringComparison.OrdinalIgnoreCase))
                {
                    return loadoutOptions[optionIndex].PatternId;
                }
            }
        }

        return loadoutOptions[0].PatternId;
    }

    private static bool ContainsPatternOption(List<PatternLoadoutOption> options, string patternId)
    {
        if (options == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(patternId))
        {
            return false;
        }

        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index].PatternId, patternId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
            RefreshPresetList();
        });
        parent.Add(field);
    }

    private void RegeneratePresetId()
    {
        if (selectedPreset == null)
            return;

        SerializedProperty idProperty = presetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(selectedPreset, "Regenerate Enemy Advanced Pattern Preset ID");
        presetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        presetSerializedObject.ApplyModifiedProperties();
        EnemyManagementDraftSession.MarkDirty();
    }

    private void RenamePreset(EnemyAdvancedPatternPreset preset, string newName)
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

    private void ShowRenamePopup(VisualElement anchor, EnemyAdvancedPatternPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        string title = "Rename Enemy Advanced Pattern Preset";
        PresetRenamePopup.Show(anchorRect, title, preset.PresetName, newName => RenamePreset(preset, newName));
    }

    private static string GetPresetDisplayName(EnemyAdvancedPatternPreset preset)
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
        ModulesDefinition = 1,
        PatternAssemble = 2,
        PatternLoadout = 3
    }

    private struct PatternLoadoutOption
    {
        public string PatternId;
        public string DisplayLabel;
    }
    #endregion
}
