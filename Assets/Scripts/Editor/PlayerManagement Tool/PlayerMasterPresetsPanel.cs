using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for managing, creating, editing, and deleting player master presets, including sub-preset
/// assignments and prefab activation.
/// </summary>
public sealed class PlayerMasterPresetsPanel
{
    #region Constants
    private const float LeftPaneWidth = 280f;
    private const string ProgressionPresetsFolder = "Assets/Scriptable Objects/Player/Progression";
    private const string PowerUpsPresetsFolder = "Assets/Scriptable Objects/Player/Power-Ups";
    private const string AnimationPresetsFolder = "Assets/Scriptable Objects/Player/Animation Bindings";
    #endregion

    #region Fields
    private readonly VisualElement m_Root;
    private readonly List<PlayerMasterPreset> m_FilteredPresets = new List<PlayerMasterPreset>();
    private readonly PlayerMasterPresetLibrary m_Library;
    private readonly Dictionary<PlayerManagementWindow.PanelType, SidePanelEntry> m_SidePanels = new Dictionary<PlayerManagementWindow.PanelType, SidePanelEntry>();

    private ListView m_ListView;
    private ToolbarSearchField m_SearchField;
    private VisualElement m_DetailsRoot;
    private VisualElement m_MainContentRoot;
    private VisualElement m_TabBar;
    private VisualElement m_ContentHost;
    private PlayerManagementWindow.PanelType m_ActivePanel;

    private PlayerMasterPreset m_SelectedPreset;
    private SerializedObject m_PresetSerializedObject;

    private ObjectField m_PlayerPrefabField;
    private Label m_ActiveStatusLabel;
    private GameObject m_SelectedPlayerPrefab;
    #endregion

    #region Nested Types
    private sealed class SidePanelEntry
    {
        public VisualElement TabContainer;
        public Button TabButton;
        public VisualElement Content;
    }
    #endregion

    #region Properties
    public VisualElement Root
    {
        get
        {
            return m_Root;
        }
    }
    #endregion

    #region Constructors
    public PlayerMasterPresetsPanel()
    {
        m_Root = new VisualElement();
        m_Root.style.flexGrow = 1f;
        m_Root.style.flexDirection = FlexDirection.Column;

        m_Library = PlayerMasterPresetLibraryUtility.GetOrCreateLibrary();

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region UI Construction
    private void BuildUI()
    {
        m_MainContentRoot = BuildMainContent();
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
        m_TabBar = new VisualElement();
        m_TabBar.style.flexDirection = FlexDirection.Row;
        m_TabBar.style.flexWrap = Wrap.Wrap;
        m_TabBar.style.marginBottom = 4f;
        m_TabBar.style.paddingLeft = 6f;
        m_TabBar.style.paddingRight = 6f;
        m_TabBar.style.paddingTop = 4f;
        m_TabBar.style.paddingBottom = 4f;

        m_ContentHost = new VisualElement();
        m_ContentHost.style.flexGrow = 1f;

        m_Root.Add(m_TabBar);
        m_Root.Add(m_ContentHost);

        AddTab(PlayerManagementWindow.PanelType.PlayerMasterPresets, "Player Master Presets", m_MainContentRoot);
        SetActivePanel(PlayerManagementWindow.PanelType.PlayerMasterPresets);
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

        m_SearchField = new ToolbarSearchField();
        m_SearchField.style.width = Length.Percent(100f);
        m_SearchField.style.maxWidth = Length.Percent(100f);
        m_SearchField.style.flexShrink = 1f;
        m_SearchField.style.marginBottom = 4f;
        m_SearchField.RegisterValueChangedCallback(evt =>
        {
            RefreshPresetList();
        });
        leftPane.Add(m_SearchField);

        m_ListView = new ListView();
        m_ListView.style.flexGrow = 1f;
        m_ListView.itemsSource = m_FilteredPresets;
        m_ListView.selectionType = SelectionType.Single;
        m_ListView.makeItem = MakePresetItem;
        m_ListView.bindItem = BindPresetItem;
        m_ListView.selectionChanged += OnPresetSelectionChanged;
        leftPane.Add(m_ListView);

        return leftPane;
    }

    private VisualElement BuildRightPane()
    {
        VisualElement rightPane = new VisualElement();
        rightPane.style.flexGrow = 1f;
        rightPane.style.paddingLeft = 10f;
        rightPane.style.paddingRight = 10f;
        rightPane.style.paddingTop = 6f;

        m_DetailsRoot = new ScrollView();
        m_DetailsRoot.style.flexGrow = 1f;
        rightPane.Add(m_DetailsRoot);

        return rightPane;
    }
    #endregion

    #region Preset List
    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        return label;
    }

    private void BindPresetItem(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= m_FilteredPresets.Count)
        {
            label.text = string.Empty;
            return;
        }

        PlayerMasterPreset preset = m_FilteredPresets[index];

        if (preset == null)
        {
            label.text = "<Missing Preset>";
            label.tooltip = string.Empty;
            return;
        }

        label.text = GetPresetDisplayName(preset);
        label.tooltip = string.IsNullOrWhiteSpace(preset.Description) ? string.Empty : preset.Description;
    }

    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            PlayerMasterPreset preset = item as PlayerMasterPreset;

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
        m_FilteredPresets.Clear();

        if (m_Library != null)
        {
            string searchText = m_SearchField != null ? m_SearchField.value : string.Empty;

            for (int i = 0; i < m_Library.Presets.Count; i++)
            {
                PlayerMasterPreset preset = m_Library.Presets[i];

                if (preset == null)
                    continue;

                if (IsMatchingSearch(preset, searchText))
                    m_FilteredPresets.Add(preset);
            }
        }

        if (m_ListView != null)
            m_ListView.Rebuild();

        if (m_FilteredPresets.Count == 0)
        {
            SelectPreset(null);
            return;
        }

        if (m_SelectedPreset == null || m_FilteredPresets.Contains(m_SelectedPreset) == false)
        {
            SelectPreset(m_FilteredPresets[0]);
            m_ListView.SetSelection(0);
        }
    }

    private bool IsMatchingSearch(PlayerMasterPreset preset, string searchText)
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
        PlayerMasterPreset newPreset = PlayerMasterPresetLibraryUtility.CreatePresetAsset("PlayerMasterPreset");

        if (newPreset == null)
            return;

        Undo.RecordObject(m_Library, "Add Master Preset");
        m_Library.AddPreset(newPreset);
        EditorUtility.SetDirty(m_Library);
        AssetDatabase.SaveAssets();

        RefreshPresetList();
        SelectPreset(newPreset);

        int index = m_FilteredPresets.IndexOf(newPreset);
        if (index >= 0)
            m_ListView.SetSelection(index);
    }

    private void DuplicatePreset()
    {
        if (m_SelectedPreset == null)
            return;

        PlayerMasterPreset duplicatedPreset = ScriptableObject.CreateInstance<PlayerMasterPreset>();
        EditorUtility.CopySerialized(m_SelectedPreset, duplicatedPreset);
        duplicatedPreset.name = m_SelectedPreset.name + " Copy";

        string originalPath = AssetDatabase.GetAssetPath(m_SelectedPreset);
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(originalPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        AssetDatabase.SaveAssets();

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("m_PresetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("m_PresetName");
        if (idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");
        if (nameProperty != null)
            nameProperty.stringValue = duplicatedPreset.name;
        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(m_Library, "Duplicate Master Preset");
        m_Library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(m_Library);
        AssetDatabase.SaveAssets();

        RefreshPresetList();
        SelectPreset(duplicatedPreset);

        int index = m_FilteredPresets.IndexOf(duplicatedPreset);
        if (index >= 0)
            m_ListView.SetSelection(index);
    }

    private void DeletePreset()
    {
        if (m_SelectedPreset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Master Preset", "Delete the selected master preset asset?", "Delete", "Cancel");

        if (confirmed == false)
            return;

        string assetPath = AssetDatabase.GetAssetPath(m_SelectedPreset);

        Undo.RecordObject(m_Library, "Delete Master Preset");
        m_Library.RemovePreset(m_SelectedPreset);
        EditorUtility.SetDirty(m_Library);
        AssetDatabase.SaveAssets();

        if (string.IsNullOrWhiteSpace(assetPath) == false)
            AssetDatabase.DeleteAsset(assetPath);

        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    private void SelectPreset(PlayerMasterPreset preset)
    {
        m_SelectedPreset = preset;
        m_DetailsRoot.Clear();

        if (m_SelectedPreset == null)
        {
            Label label = new Label("Select or create a master preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            m_DetailsRoot.Add(label);
            RefreshActiveStatus();
            return;
        }

        m_PresetSerializedObject = new SerializedObject(m_SelectedPreset);

        BuildMetadataSection();
        BuildSubPresetsSection();
        BuildActivePresetSection();
        BuildNavigationSection();
        RefreshActiveStatus();
    }

    private void BuildMetadataSection()
    {
        Label header = new Label("Preset Details");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        m_DetailsRoot.Add(header);

        SerializedProperty idProperty = m_PresetSerializedObject.FindProperty("m_PresetId");
        SerializedProperty nameProperty = m_PresetSerializedObject.FindProperty("m_PresetName");
        SerializedProperty descriptionProperty = m_PresetSerializedObject.FindProperty("m_Description");
        SerializedProperty versionProperty = m_PresetSerializedObject.FindProperty("m_Version");

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            HandlePresetNameChanged(evt.newValue);
        });
        m_DetailsRoot.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            RefreshPresetList();
        });
        m_DetailsRoot.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            RefreshPresetList();
        });
        m_DetailsRoot.Add(descriptionField);

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

        m_DetailsRoot.Add(idRow);
    }

    private void RegeneratePresetId()
    {
        if (m_SelectedPreset == null)
            return;

        SerializedProperty idProperty = m_PresetSerializedObject.FindProperty("m_PresetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(m_SelectedPreset, "Regenerate Preset ID");
        m_PresetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        m_PresetSerializedObject.ApplyModifiedProperties();
    }

    private void HandlePresetNameChanged(string newName)
    {
        if (m_SelectedPreset == null)
            return;

        if (string.IsNullOrWhiteSpace(newName))
            return;

        string assetPath = AssetDatabase.GetAssetPath(m_SelectedPreset);

        if (string.IsNullOrWhiteSpace(assetPath) == false)
        {
            string error = AssetDatabase.RenameAsset(assetPath, newName);

            if (string.IsNullOrWhiteSpace(error) == false)
                Debug.LogWarning("Preset rename failed: " + error);
        }

        m_SelectedPreset.name = newName;
        EditorUtility.SetDirty(m_SelectedPreset);
        AssetDatabase.SaveAssets();
        RefreshPresetList();
    }

    private void BuildSubPresetsSection()
    {
        Label header = new Label("Sub Presets");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginTop = 8f;
        header.style.marginBottom = 4f;
        m_DetailsRoot.Add(header);

        SerializedProperty controllerProperty = m_PresetSerializedObject.FindProperty("m_ControllerPreset");
        SerializedProperty progressionProperty = m_PresetSerializedObject.FindProperty("m_ProgressionPreset");
        SerializedProperty powerUpsProperty = m_PresetSerializedObject.FindProperty("m_PowerUpsPreset");
        SerializedProperty animationProperty = m_PresetSerializedObject.FindProperty("m_AnimationBindingsPreset");

        m_DetailsRoot.Add(BuildSubPresetRow("Controller Preset", typeof(PlayerControllerPreset), controllerProperty, CreateControllerPreset, () => OpenSidePanel(PlayerManagementWindow.PanelType.PlayerControllerPresets)));
        m_DetailsRoot.Add(BuildSubPresetRow("Level-Up & Progression Preset", typeof(PlayerProgressionPreset), progressionProperty, CreateProgressionPreset, () => OpenSidePanel(PlayerManagementWindow.PanelType.LevelUpProgression)));
        m_DetailsRoot.Add(BuildSubPresetRow("Craftable Power-Ups Preset", typeof(PlayerCraftablePowerUpsPreset), powerUpsProperty, CreatePowerUpsPreset, () => OpenSidePanel(PlayerManagementWindow.PanelType.CraftablePowerUps)));
        m_DetailsRoot.Add(BuildSubPresetRow("Animation Bindings Preset", typeof(PlayerAnimationBindingsPreset), animationProperty, CreateAnimationPreset, () => OpenSidePanel(PlayerManagementWindow.PanelType.AnimationBindings)));
    }

    private VisualElement BuildSubPresetRow(string label, Type presetType, SerializedProperty presetProperty, Action createAction, Action openSectionAction)
    {
        VisualElement container = new VisualElement();
        container.style.marginBottom = 6f;

        ObjectField presetField = new ObjectField(label);
        presetField.objectType = presetType;
        presetField.BindProperty(presetProperty);
        container.Add(presetField);

        VisualElement buttonsRow = new VisualElement();
        buttonsRow.style.flexDirection = FlexDirection.Row;
        buttonsRow.style.marginTop = 2f;

        Button openButton = new Button();
        openButton.text = "Open Section";
        openButton.tooltip = "Open the corresponding section.";
        openButton.clicked += openSectionAction;
        buttonsRow.Add(openButton);

        Button newButton = new Button();
        newButton.text = "New";
        newButton.tooltip = "Create and assign a new preset.";
        newButton.style.marginLeft = 4f;
        newButton.clicked += createAction;
        buttonsRow.Add(newButton);

        Button selectButton = new Button();
        selectButton.text = "Select in Project";
        selectButton.tooltip = "Select the assigned preset in the Project window.";
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
        Label header = new Label("Active on Player Prefab");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginTop = 8f;
        header.style.marginBottom = 4f;
        m_DetailsRoot.Add(header);

        m_PlayerPrefabField = new ObjectField("Player Prefab");
        m_PlayerPrefabField.objectType = typeof(GameObject);
        m_PlayerPrefabField.RegisterValueChangedCallback(evt =>
        {
            m_SelectedPlayerPrefab = evt.newValue as GameObject;
            RefreshActiveStatus();
        });
        m_DetailsRoot.Add(m_PlayerPrefabField);

        VisualElement buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.marginTop = 2f;

        Button findButton = new Button();
        findButton.text = "Find";
        findButton.tooltip = "Search the project for a prefab with PlayerAuthoring.";
        findButton.clicked += FindPlayerPrefab;
        buttonRow.Add(findButton);

        Button setActiveButton = new Button();
        setActiveButton.text = "Set Active Preset";
        setActiveButton.tooltip = "Assign this master preset to the selected player prefab.";
        setActiveButton.style.marginLeft = 4f;
        setActiveButton.clicked += AssignPresetToPrefab;
        buttonRow.Add(setActiveButton);

        m_DetailsRoot.Add(buttonRow);

        m_ActiveStatusLabel = new Label();
        m_ActiveStatusLabel.style.marginTop = 2f;
        m_ActiveStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        m_DetailsRoot.Add(m_ActiveStatusLabel);
    }

    private void BuildNavigationSection()
    {
        Label header = new Label("Open Sections");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginTop = 8f;
        header.style.marginBottom = 4f;
        m_DetailsRoot.Add(header);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        Button controllerButton = new Button();
        controllerButton.text = "Open Controller";
        controllerButton.clicked += () => OpenSidePanel(PlayerManagementWindow.PanelType.PlayerControllerPresets);
        row.Add(controllerButton);

        Button progressionButton = new Button();
        progressionButton.text = "Open Progression";
        progressionButton.style.marginLeft = 4f;
        progressionButton.clicked += () => OpenSidePanel(PlayerManagementWindow.PanelType.LevelUpProgression);
        row.Add(progressionButton);

        Button powerUpsButton = new Button();
        powerUpsButton.text = "Open Power-Ups";
        powerUpsButton.style.marginLeft = 4f;
        powerUpsButton.clicked += () => OpenSidePanel(PlayerManagementWindow.PanelType.CraftablePowerUps);
        row.Add(powerUpsButton);

        Button animationButton = new Button();
        animationButton.text = "Open Animations";
        animationButton.style.marginLeft = 4f;
        animationButton.clicked += () => OpenSidePanel(PlayerManagementWindow.PanelType.AnimationBindings);
        row.Add(animationButton);

        m_DetailsRoot.Add(row);
    }
    #endregion

    #region Sub Preset Creation
    private void CreateControllerPreset()
    {
        PlayerControllerPreset newPreset = PlayerControllerPresetLibraryUtility.CreatePresetAsset("PlayerControllerPreset");

        if (newPreset == null)
            return;

        PlayerControllerPresetLibrary controllerLibrary = PlayerControllerPresetLibraryUtility.GetOrCreateLibrary();
        Undo.RecordObject(controllerLibrary, "Add Controller Preset");
        controllerLibrary.AddPreset(newPreset);
        EditorUtility.SetDirty(controllerLibrary);
        AssetDatabase.SaveAssets();

        AssignSubPreset("m_ControllerPreset", newPreset);
    }

    private void CreateProgressionPreset()
    {
        PlayerProgressionPreset newPreset = CreateSubPresetAsset<PlayerProgressionPreset>("PlayerProgressionPreset", ProgressionPresetsFolder);
        AssignSubPreset("m_ProgressionPreset", newPreset);
    }

    private void CreatePowerUpsPreset()
    {
        PlayerCraftablePowerUpsPreset newPreset = CreateSubPresetAsset<PlayerCraftablePowerUpsPreset>("PlayerCraftablePowerUpsPreset", PowerUpsPresetsFolder);
        AssignSubPreset("m_PowerUpsPreset", newPreset);
    }

    private void CreateAnimationPreset()
    {
        PlayerAnimationBindingsPreset newPreset = CreateSubPresetAsset<PlayerAnimationBindingsPreset>("PlayerAnimationBindingsPreset", AnimationPresetsFolder);
        AssignSubPreset("m_AnimationBindingsPreset", newPreset);
    }

    private T CreateSubPresetAsset<T>(string presetName, string folderPath) where T : ScriptableObject
    {
        EnsureFolder(folderPath);

        T preset = ScriptableObject.CreateInstance<T>();
        preset.name = presetName;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, presetName + ".asset"));
        AssetDatabase.CreateAsset(preset, assetPath);
        AssetDatabase.SaveAssets();

        SerializedObject serializedPreset = new SerializedObject(preset);
        SerializedProperty nameProperty = serializedPreset.FindProperty("m_PresetName");

        if (nameProperty != null)
            nameProperty.stringValue = presetName;

        serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();

        return preset;
    }

    private void AssignSubPreset(string propertyName, UnityEngine.Object preset)
    {
        if (m_SelectedPreset == null)
            return;

        SerializedProperty property = m_PresetSerializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        Undo.RecordObject(m_SelectedPreset, "Assign Sub Preset");
        m_PresetSerializedObject.Update();
        property.objectReferenceValue = preset;
        m_PresetSerializedObject.ApplyModifiedProperties();
    }
    #endregion

    #region Prefab Activation
    private void FindPlayerPrefab()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            PlayerAuthoring authoring = prefab.GetComponent<PlayerAuthoring>();

            if (authoring == null)
                continue;

            m_SelectedPlayerPrefab = prefab;

            if (m_PlayerPrefabField != null)
                m_PlayerPrefabField.SetValueWithoutNotify(prefab);

            RefreshActiveStatus();
            return;
        }

        EditorUtility.DisplayDialog("Find Player Prefab", "No prefab with PlayerAuthoring was found.", "OK");
    }

    private void AssignPresetToPrefab()
    {
        if (m_SelectedPreset == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "Select a master preset first.", "OK");
            return;
        }

        if (m_SelectedPlayerPrefab == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "Select a player prefab first.", "OK");
            return;
        }

        PlayerAuthoring authoring = m_SelectedPlayerPrefab.GetComponent<PlayerAuthoring>();

        if (authoring == null)
        {
            EditorUtility.DisplayDialog("Set Active Preset", "PlayerAuthoring component not found on the selected prefab.", "OK");
            return;
        }

        SerializedObject serializedAuthoring = new SerializedObject(authoring);
        SerializedProperty presetProperty = serializedAuthoring.FindProperty("m_MasterPreset");

        if (presetProperty == null)
            return;

        Undo.RecordObject(authoring, "Set Active Master Preset");
        serializedAuthoring.Update();
        presetProperty.objectReferenceValue = m_SelectedPreset;
        serializedAuthoring.ApplyModifiedProperties();
        EditorUtility.SetDirty(authoring);

        if (PrefabUtility.IsPartOfPrefabInstance(authoring))
            PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);

        AssetDatabase.SaveAssets();
        RefreshActiveStatus();
    }

    private void RefreshActiveStatus()
    {
        if (m_ActiveStatusLabel == null)
            return;

        if (m_SelectedPreset == null)
        {
            m_ActiveStatusLabel.text = "No master preset selected.";
            return;
        }

        if (m_SelectedPlayerPrefab == null)
        {
            m_ActiveStatusLabel.text = "No player prefab selected.";
            return;
        }

        PlayerAuthoring authoring = m_SelectedPlayerPrefab.GetComponent<PlayerAuthoring>();

        if (authoring == null)
        {
            m_ActiveStatusLabel.text = "PlayerAuthoring component not found on prefab.";
            return;
        }

        bool isActive = authoring.MasterPreset == m_SelectedPreset;
        m_ActiveStatusLabel.text = isActive ? "Active on selected prefab." : "Not active on selected prefab.";
    }
    #endregion

    #region Helpers
    private void OpenSidePanel(PlayerManagementWindow.PanelType panelType)
    {
        if (m_SidePanels.ContainsKey(panelType))
        {
            SidePanelEntry existingEntry = m_SidePanels[panelType];

            if (existingEntry != null)
                SetActivePanel(panelType);
            return;
        }

        VisualElement content = BuildSidePanelContent(panelType);

        if (content == null)
            return;

        AddTab(panelType, GetPanelTitle(panelType), content);
        SetActivePanel(panelType);
    }

    private void CloseSidePanel(PlayerManagementWindow.PanelType panelType)
    {
        if (panelType == PlayerManagementWindow.PanelType.PlayerMasterPresets)
            return;

        if (m_SidePanels.TryGetValue(panelType, out SidePanelEntry entry) == false)
            return;

        if (entry != null && entry.TabContainer != null)
            entry.TabContainer.RemoveFromHierarchy();

        m_SidePanels.Remove(panelType);

        if (m_ActivePanel == panelType)
            SetActivePanel(PlayerManagementWindow.PanelType.PlayerMasterPresets);
    }

    private string GetPanelTitle(PlayerManagementWindow.PanelType panelType)
    {
        if (panelType == PlayerManagementWindow.PanelType.PlayerControllerPresets)
            return "Player Controller Presets";

        if (panelType == PlayerManagementWindow.PanelType.LevelUpProgression)
            return "Level-Up & Progression";

        if (panelType == PlayerManagementWindow.PanelType.CraftablePowerUps)
            return "Craftable Power-Ups";

        return "Animations Bindings";
    }

    private VisualElement BuildSidePanelContent(PlayerManagementWindow.PanelType panelType)
    {
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

        if (panelType == PlayerManagementWindow.PanelType.PlayerControllerPresets)
        {
            PlayerControllerPresetsPanel controllerPanel = new PlayerControllerPresetsPanel();
            panelRoot.Add(controllerPanel.Root);
            return panelRoot;
        }

        VisualElement placeholder = new VisualElement();
        placeholder.style.flexGrow = 1f;
        placeholder.style.minHeight = 220f;
        placeholder.style.justifyContent = Justify.Center;
        placeholder.style.alignItems = Align.Center;
        Label label = new Label("Section not implemented yet.");
        label.style.unityFontStyleAndWeight = FontStyle.Italic;
        placeholder.Add(label);
        panelRoot.Add(placeholder);
        return panelRoot;
    }

    private void AddTab(PlayerManagementWindow.PanelType panelType, string label, VisualElement content)
    {
        if (m_TabBar == null)
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

        m_TabBar.Add(tabContainer);
        m_SidePanels[panelType] = new SidePanelEntry
        {
            TabContainer = tabContainer,
            TabButton = tabButton,
            Content = content
        };
    }

    private void SetActivePanel(PlayerManagementWindow.PanelType panelType)
    {
        if (m_SidePanels.TryGetValue(panelType, out SidePanelEntry entry) == false)
            return;

        if (m_ContentHost == null)
            return;

        m_ActivePanel = panelType;
        m_ContentHost.Clear();
        m_ContentHost.Add(entry.Content);
        UpdateTabStyles();
    }

    private void UpdateTabStyles()
    {
        foreach (KeyValuePair<PlayerManagementWindow.PanelType, SidePanelEntry> entry in m_SidePanels)
        {
            if (entry.Value == null || entry.Value.TabButton == null)
                continue;

            bool isActive = entry.Key == m_ActivePanel;
            entry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            entry.Value.TabButton.style.backgroundColor = isActive ? new Color(0.18f, 0.18f, 0.18f, 0.6f) : Color.clear;
        }
    }

    private string GetPresetDisplayName(PlayerMasterPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string name = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return name;

        return name + " v. " + version;
    }

    private void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (string.IsNullOrWhiteSpace(parentFolder) == false && AssetDatabase.IsValidFolder(parentFolder) == false)
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endregion
}
