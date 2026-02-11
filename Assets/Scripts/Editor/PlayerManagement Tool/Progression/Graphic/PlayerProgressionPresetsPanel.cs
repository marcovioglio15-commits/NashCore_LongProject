using System;
using System.Collections.Generic;
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
        duplicatedPreset.name = preset.name + " Copy";

        string originalPath = AssetDatabase.GetAssetPath(preset);
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(originalPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Progression Preset Asset");

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");

        if (idProperty != null)
        {
            idProperty.stringValue = Guid.NewGuid().ToString("N");
        }

        if (nameProperty != null)
        {
            nameProperty.stringValue = duplicatedPreset.name;
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

    private void BuildBaseStatsSection()
    {
        VisualElement baseStatsContainer = CreateSectionContainer("Base Stats");

        SerializedProperty baseStatsProperty = presetSerializedObject.FindProperty("baseStats");

        if (baseStatsProperty == null)
        {
            Label missingLabel = new Label("Base stats data is missing on this preset.");
            missingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            baseStatsContainer.Add(missingLabel);
            return;
        }

        SerializedProperty healthProperty = baseStatsProperty.FindPropertyRelative("health");
        SerializedProperty experienceProperty = baseStatsProperty.FindPropertyRelative("experience");

        PropertyField healthField = new PropertyField(healthProperty);
        healthField.BindProperty(healthProperty);
        baseStatsContainer.Add(healthField);

        PropertyField experienceField = new PropertyField(experienceProperty);
        experienceField.BindProperty(experienceProperty);
        baseStatsContainer.Add(experienceField);
    }

    private VisualElement BuildSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddSectionButton(buttonsRoot, SectionType.BaseStats, "Base Stats");
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
            case SectionType.BaseStats:
                BuildBaseStatsSection();
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

        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        preset.name = newName;
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
        BaseStats = 1
    }
    #endregion
}
