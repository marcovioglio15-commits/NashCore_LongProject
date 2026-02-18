using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI panel for creating and editing PlayerAnimationBindingsPreset assets.
/// Includes import/remap tools to transfer setups across different character models.
/// </summary>
public sealed class PlayerAnimationBindingsPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    #endregion

    #region Fields
    private readonly VisualElement root;
    private readonly List<PlayerAnimationBindingsPreset> filteredPresets = new List<PlayerAnimationBindingsPreset>();

    private ListView listView;
    private ToolbarSearchField searchField;
    private ScrollView detailsRoot;

    private PlayerAnimationBindingsPreset selectedPreset;
    private SerializedObject selectedPresetSerializedObject;

    private ObjectField sourcePresetField;
    private ObjectField targetClipsFolderField;
    private Toggle recursiveSearchToggle;
    private Toggle overwriteExistingSlotsToggle;
    private Label importStatusLabel;
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

    #region Lifecycle
    public PlayerAnimationBindingsPresetsPanel()
    {
        root = new VisualElement();
        root.style.flexGrow = 1f;

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Public Methods
    public void SelectPresetFromExternal(PlayerAnimationBindingsPreset preset)
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
        PlayerAnimationBindingsPreset previouslySelectedPreset = selectedPreset;
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
        splitView.Add(BuildLeftPane());
        splitView.Add(BuildRightPane());
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
        searchField.RegisterValueChangedCallback(evt => RefreshPresetList());
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

    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            PlayerAnimationBindingsPreset preset = label.userData as PlayerAnimationBindingsPreset;

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

        PlayerAnimationBindingsPreset preset = filteredPresets[index];

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
    #endregion

    #region Preset List
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object selectedObject in selection)
        {
            PlayerAnimationBindingsPreset preset = selectedObject as PlayerAnimationBindingsPreset;

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
        List<PlayerAnimationBindingsPreset> allPresets = PlayerAnimationBindingsPresetEditorUtility.LoadAllPresets();
        string searchText = searchField != null ? searchField.value : string.Empty;

        for (int presetIndex = 0; presetIndex < allPresets.Count; presetIndex++)
        {
            PlayerAnimationBindingsPreset preset = allPresets[presetIndex];

            if (preset == null)
                continue;

            if (MatchesSearch(preset, searchText) == false)
                continue;

            filteredPresets.Add(preset);
        }

        if (listView != null)
            listView.Rebuild();

        if (selectedPreset == null)
        {
            if (filteredPresets.Count > 0 && listView != null)
                listView.SetSelectionWithoutNotify(new int[] { 0 });

            if (filteredPresets.Count > 0)
                SelectPreset(filteredPresets[0]);
            else
                SelectPreset(null);

            return;
        }

        if (filteredPresets.Contains(selectedPreset) == false)
        {
            SelectPreset(null);
            return;
        }

        if (listView != null)
        {
            int selectedIndex = filteredPresets.IndexOf(selectedPreset);

            if (selectedIndex >= 0)
                listView.SetSelectionWithoutNotify(new int[] { selectedIndex });
        }
    }

    private static bool MatchesSearch(PlayerAnimationBindingsPreset preset, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        string search = searchText.Trim();

        if (string.IsNullOrWhiteSpace(search))
            return true;

        if (ContainsIgnoreCase(preset.PresetName, search))
            return true;

        if (ContainsIgnoreCase(preset.Description, search))
            return true;

        if (ContainsIgnoreCase(preset.name, search))
            return true;

        return false;
    }

    private static bool ContainsIgnoreCase(string value, string search)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(search))
            return false;

        return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    #endregion

    #region Selection
    private void SelectPreset(PlayerAnimationBindingsPreset preset)
    {
        selectedPreset = preset;

        if (selectedPreset == null)
        {
            selectedPresetSerializedObject = null;
            RebuildDetails();
            return;
        }

        selectedPresetSerializedObject = new SerializedObject(selectedPreset);
        RebuildDetails();
    }

    private void RebuildDetails()
    {
        if (detailsRoot == null)
            return;

        detailsRoot.Clear();

        if (selectedPreset == null || selectedPresetSerializedObject == null)
        {
            Label emptyLabel = new Label("Select an Animation Bindings preset to edit.");
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            detailsRoot.Add(emptyLabel);
            return;
        }

        selectedPresetSerializedObject.Update();

        BuildMetadataSection();
        BuildAnimatorSetupSection();
        BuildParametersSection();
        BuildProceduralSection();
        BuildClipSlotsSection();
        BuildImportSection();

        selectedPresetSerializedObject.ApplyModifiedProperties();
    }

    private void BuildMetadataSection()
    {
        VisualElement section = CreateSection("Metadata");
        section.Add(CreatePropertyField("presetName", "Preset Name"));
        section.Add(CreatePropertyField("description", "Description"));
        section.Add(CreatePropertyField("version", "Version"));
        section.Add(CreateReadOnlyText("Preset Id", selectedPreset.PresetId));
    }

    private void BuildAnimatorSetupSection()
    {
        VisualElement section = CreateSection("Animator Setup");
        section.Add(CreatePropertyField("animatorController", "Animator Controller"));
        section.Add(CreatePropertyField("upperBodyAvatarMask", "Upper Body Mask"));
        section.Add(CreatePropertyField("lowerBodyAvatarMask", "Lower Body Mask"));
        section.Add(CreatePropertyField("disableRootMotion", "Disable Root Motion"));
        section.Add(CreatePropertyField("useFloatDamping", "Use Float Damping"));
        section.Add(CreatePropertyField("floatDampTime", "Float Damp Time"));
        section.Add(CreatePropertyField("movingSpeedThreshold", "Moving Speed Threshold"));
    }

    private void BuildParametersSection()
    {
        VisualElement section = CreateSection("Animator Parameters");
        section.Add(CreatePropertyField("moveXParameter", "MoveX"));
        section.Add(CreatePropertyField("moveYParameter", "MoveY"));
        section.Add(CreatePropertyField("moveSpeedParameter", "MoveSpeed"));
        section.Add(CreatePropertyField("aimXParameter", "AimX"));
        section.Add(CreatePropertyField("aimYParameter", "AimY"));
        section.Add(CreatePropertyField("isMovingParameter", "IsMoving"));
        section.Add(CreatePropertyField("isShootingParameter", "IsShooting"));
        section.Add(CreatePropertyField("isDashingParameter", "IsDashing"));
        section.Add(CreatePropertyField("shotPulseParameter", "ShotPulse Trigger"));
    }

    private void BuildProceduralSection()
    {
        VisualElement section = CreateSection("Procedural Animation");
        section.Add(CreatePropertyField("proceduralRecoilEnabled", "Enable Recoil"));
        section.Add(CreatePropertyField("proceduralRecoilKick", "Recoil Kick"));
        section.Add(CreatePropertyField("proceduralRecoilRecoveryPerSecond", "Recoil Recovery / Sec"));
        section.Add(CreatePropertyField("proceduralRecoilParameter", "Recoil Parameter"));
        section.Add(CreatePropertyField("proceduralAimWeightEnabled", "Enable Aim Weight"));
        section.Add(CreatePropertyField("proceduralAimWeightSmoothing", "Aim Weight Smoothing"));
        section.Add(CreatePropertyField("proceduralAimWeightParameter", "Aim Weight Parameter"));
        section.Add(CreatePropertyField("proceduralLeanEnabled", "Enable Lean"));
        section.Add(CreatePropertyField("proceduralLeanSmoothing", "Lean Smoothing"));
        section.Add(CreatePropertyField("proceduralLeanParameter", "Lean Parameter"));
    }

    private void BuildClipSlotsSection()
    {
        VisualElement section = CreateSection("Clip Slots");
        section.Add(CreatePropertyField("idleClip", "Idle"));
        section.Add(CreatePropertyField("moveForwardClip", "Move Forward"));
        section.Add(CreatePropertyField("moveBackwardClip", "Move Backward"));
        section.Add(CreatePropertyField("moveLeftClip", "Move Left"));
        section.Add(CreatePropertyField("moveRightClip", "Move Right"));
        section.Add(CreatePropertyField("aimForwardClip", "Aim Forward"));
        section.Add(CreatePropertyField("aimBackwardClip", "Aim Backward"));
        section.Add(CreatePropertyField("aimLeftClip", "Aim Left"));
        section.Add(CreatePropertyField("aimRightClip", "Aim Right"));
        section.Add(CreatePropertyField("shootClip", "Shoot"));
        section.Add(CreatePropertyField("dashClip", "Dash"));
    }

    private void BuildImportSection()
    {
        VisualElement section = CreateSection("Import / Remap");

        sourcePresetField = new ObjectField("Source Preset");
        sourcePresetField.objectType = typeof(PlayerAnimationBindingsPreset);
        sourcePresetField.allowSceneObjects = false;
        sourcePresetField.tooltip = "Preset source used to import animator setup, parameters and optional clip slots.";
        section.Add(sourcePresetField);

        targetClipsFolderField = new ObjectField("Target Clips Folder");
        targetClipsFolderField.objectType = typeof(DefaultAsset);
        targetClipsFolderField.allowSceneObjects = false;
        targetClipsFolderField.tooltip = "Root folder scanned for clip remapping by semantic slot keywords.";
        section.Add(targetClipsFolderField);

        recursiveSearchToggle = new Toggle("Search Subfolders");
        recursiveSearchToggle.value = true;
        recursiveSearchToggle.tooltip = "Include all nested folders when searching clips for auto-remap.";
        section.Add(recursiveSearchToggle);

        overwriteExistingSlotsToggle = new Toggle("Overwrite Existing Slots");
        overwriteExistingSlotsToggle.value = false;
        overwriteExistingSlotsToggle.tooltip = "Replace already assigned clip slots when auto-remapping.";
        section.Add(overwriteExistingSlotsToggle);

        VisualElement buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.flexWrap = Wrap.Wrap;
        buttonsRow.style.marginTop = 4f;

        Button importSettingsButton = new Button(ImportSettingsOnly);
        importSettingsButton.text = "Import Settings";
        importSettingsButton.tooltip = "Copy animator setup and parameter names from the source preset.";
        buttonsRow.Add(importSettingsButton);

        Button importAndCopyClipsButton = new Button(ImportSettingsAndCopyClips);
        importAndCopyClipsButton.text = "Import + Copy Clips";
        importAndCopyClipsButton.style.marginLeft = 4f;
        importAndCopyClipsButton.tooltip = "Copy setup, parameters and current clip slot assignments from source preset.";
        buttonsRow.Add(importAndCopyClipsButton);

        Button importAndRemapButton = new Button(ImportSettingsAndRemapClips);
        importAndRemapButton.text = "Import + Remap Clips";
        importAndRemapButton.style.marginLeft = 4f;
        importAndRemapButton.tooltip = "Copy setup/parameters from source and remap clip slots using folder clip names.";
        buttonsRow.Add(importAndRemapButton);

        Button remapOnlyButton = new Button(AutoRemapOnly);
        remapOnlyButton.text = "Auto Remap";
        remapOnlyButton.style.marginLeft = 4f;
        remapOnlyButton.tooltip = "Remap clip slots using selected folder without importing other settings.";
        buttonsRow.Add(remapOnlyButton);

        section.Add(buttonsRow);

        importStatusLabel = new Label();
        importStatusLabel.style.marginTop = 4f;
        importStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        section.Add(importStatusLabel);
    }

    private VisualElement CreateSection(string title)
    {
        VisualElement section = new VisualElement();
        section.style.marginBottom = 10f;

        Label titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        section.Add(titleLabel);

        detailsRoot.Add(section);
        return section;
    }

    private PropertyField CreatePropertyField(string propertyName, string labelOverride)
    {
        SerializedProperty property = selectedPresetSerializedObject.FindProperty(propertyName);
        PropertyField field = new PropertyField(property, labelOverride);
        field.BindProperty(property);
        field.RegisterCallback<SerializedPropertyChangeEvent>(evt => PlayerManagementDraftSession.MarkDirty());
        return field;
    }

    private static VisualElement CreateReadOnlyText(string label, string value)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        Label labelElement = new Label(label + ": ");
        labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(labelElement);

        Label valueElement = new Label(string.IsNullOrWhiteSpace(value) ? "<empty>" : value);
        row.Add(valueElement);
        return row;
    }
    #endregion

    #region Import Actions
    private void ImportSettingsOnly()
    {
        if (selectedPreset == null)
            return;

        PlayerAnimationBindingsPreset sourcePreset = sourcePresetField != null ? sourcePresetField.value as PlayerAnimationBindingsPreset : null;

        if (sourcePreset == null)
        {
            SetImportStatus("Select a source preset first.");
            return;
        }

        PlayerAnimationBindingsPresetEditorUtility.ImportSettingsFromSource(selectedPreset, sourcePreset, false);
        SetImportStatus("Settings imported.");
        RebuildDetails();
    }

    private void ImportSettingsAndCopyClips()
    {
        if (selectedPreset == null)
            return;

        PlayerAnimationBindingsPreset sourcePreset = sourcePresetField != null ? sourcePresetField.value as PlayerAnimationBindingsPreset : null;

        if (sourcePreset == null)
        {
            SetImportStatus("Select a source preset first.");
            return;
        }

        PlayerAnimationBindingsPresetEditorUtility.ImportSettingsFromSource(selectedPreset, sourcePreset, true);
        SetImportStatus("Settings and clip slots imported.");
        RebuildDetails();
    }

    private void ImportSettingsAndRemapClips()
    {
        if (selectedPreset == null)
            return;

        PlayerAnimationBindingsPreset sourcePreset = sourcePresetField != null ? sourcePresetField.value as PlayerAnimationBindingsPreset : null;

        if (sourcePreset == null)
        {
            SetImportStatus("Select a source preset first.");
            return;
        }

        DefaultAsset clipsFolder = targetClipsFolderField != null ? targetClipsFolderField.value as DefaultAsset : null;

        if (clipsFolder == null)
        {
            SetImportStatus("Select a target clips folder first.");
            return;
        }

        PlayerAnimationBindingsPresetEditorUtility.ImportSettingsFromSource(selectedPreset, sourcePreset, false);
        int mappedCount = AutoRemapClips(clipsFolder);
        SetImportStatus(string.Format("Settings imported. Remapped slots: {0}", mappedCount));
        RebuildDetails();
    }

    private void AutoRemapOnly()
    {
        if (selectedPreset == null)
            return;

        DefaultAsset clipsFolder = targetClipsFolderField != null ? targetClipsFolderField.value as DefaultAsset : null;

        if (clipsFolder == null)
        {
            SetImportStatus("Select a target clips folder first.");
            return;
        }

        int mappedCount = AutoRemapClips(clipsFolder);
        SetImportStatus(string.Format("Remapped slots: {0}", mappedCount));
        RebuildDetails();
    }

    private int AutoRemapClips(DefaultAsset clipsFolder)
    {
        bool recursive = recursiveSearchToggle == null || recursiveSearchToggle.value;
        bool overwrite = overwriteExistingSlotsToggle != null && overwriteExistingSlotsToggle.value;
        List<AnimationClip> clips = PlayerAnimationBindingsPresetEditorUtility.LoadClipsFromFolder(clipsFolder, recursive);

        if (clips.Count == 0)
            return 0;

        return PlayerAnimationBindingsPresetEditorUtility.AutoMapClipSlots(selectedPreset, clips, overwrite);
    }

    private void SetImportStatus(string message)
    {
        if (importStatusLabel != null)
            importStatusLabel.text = message;
    }
    #endregion

    #region Preset Commands
    private void CreatePreset()
    {
        PlayerAnimationBindingsPreset newPreset = PlayerAnimationBindingsPresetEditorUtility.CreatePresetAsset("PlayerAnimationBindingsPreset");

        if (newPreset == null)
            return;

        RefreshPresetList();
        SelectPresetFromExternal(newPreset);
    }

    private void DuplicatePreset()
    {
        DuplicatePreset(selectedPreset);
    }

    private void DuplicatePreset(PlayerAnimationBindingsPreset preset)
    {
        if (preset == null)
            return;

        PlayerAnimationBindingsPreset duplicatedPreset = PlayerAnimationBindingsPresetEditorUtility.DuplicatePresetAsset(preset);

        if (duplicatedPreset == null)
            return;

        RefreshPresetList();
        SelectPresetFromExternal(duplicatedPreset);
    }

    private void DeletePreset()
    {
        DeletePreset(selectedPreset);
    }

    private void DeletePreset(PlayerAnimationBindingsPreset preset)
    {
        if (preset == null)
            return;

        bool deleted = PlayerAnimationBindingsPresetEditorUtility.DeletePresetAsset(preset);

        if (deleted == false)
            return;

        if (selectedPreset == preset)
            selectedPreset = null;

        RefreshPresetList();
    }

    private void ShowRenamePopup(VisualElement anchor, PlayerAnimationBindingsPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        PresetRenamePopup.Show(anchorRect,
                               "Rename Animation Preset",
                               preset.PresetName,
                               newName => RenamePreset(preset, newName));
    }

    private void RenamePreset(PlayerAnimationBindingsPreset preset, string newName)
    {
        if (preset == null)
            return;

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(newName);

        if (string.IsNullOrWhiteSpace(normalizedName))
            return;

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty presetNameProperty = serializedPreset.FindProperty("presetName");

        serializedPreset.Update();

        if (presetNameProperty != null)
            presetNameProperty.stringValue = normalizedName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        preset.name = normalizedName;
        EditorUtility.SetDirty(preset);
        PlayerManagementDraftSession.MarkDirty();
        RefreshPresetList();
    }
    #endregion

    #region Helpers
    private static string GetPresetDisplayName(PlayerAnimationBindingsPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string displayName = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return displayName;

        return displayName + " v. " + version;
    }
    #endregion
}
