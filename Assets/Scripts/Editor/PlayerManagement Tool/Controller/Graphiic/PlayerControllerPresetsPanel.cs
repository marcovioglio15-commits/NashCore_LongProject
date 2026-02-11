using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Provides a UI panel for managing, creating, editing, and deleting player controller presets, including movement,
/// look, and camera settings.
/// </summary>
public sealed class PlayerControllerPresetsPanel
{
    #region Constants
    // Width of the left pane containing the preset list
    private const float LeftPaneWidth = 280f;
    private const float MultiplierFieldWidth = 70f;
    private const float MultiplierFieldHeight = 18f;
    private const float MultiplierLabelWidth = 56f;
    private const float MultiplierAngleWidth = 52f;
    private const float MultiplierRangeWidth = 110f;
    private const float MultiplierRowSpacing = 2f;

    // Sections Margin
    private const float SectionMarginTop = 8f;
    private const float SubSectionMarginLeft = 5f;


    // Colors for pie chart segments and overlays
    private static readonly Color SliceColorA = new Color(0.2f, 0.6f, 0.9f, 0.75f);
    private static readonly Color SliceColorB = new Color(0.1f, 0.4f, 0.7f, 0.75f);
    private static readonly Color FrontConeColor = new Color(0.2f, 0.8f, 0.4f, 0.7f);
    private static readonly Color BackConeColor = new Color(0.8f, 0.3f, 0.3f, 0.7f);
    private static readonly Color LeftConeColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
    private static readonly Color RightConeColor = new Color(0.9f, 0.7f, 0.2f, 0.7f);
    private static readonly Color DirectionMarkerColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);
    private static readonly Color DirectionLabelColor = new Color(0f, 0f, 0f, 1f);
    private static readonly Color ForwardMarkerColor = new Color(1f, 0.9f, 0.2f, 1f);
    private static readonly Color MaxSpeedMultiplierColor = new Color(0.2f, 0.8f, 1f, 0.85f);
    private static readonly Color AccelerationMultiplierColor = new Color(1f, 0.65f, 0.2f, 0.85f);
    #endregion

    #region Fields
    // Root visual element of the panel
    private readonly VisualElement m_Root;
    private readonly List<PlayerControllerPreset> m_FilteredPresets = new List<PlayerControllerPreset>();

    // Reference to the preset library and input action asset
    private readonly PlayerControllerPresetLibrary m_Library;
    private readonly InputActionAsset m_InputAsset;

    // UI elements
    private ListView m_ListView;
    private ToolbarSearchField m_SearchField;
    private VisualElement m_DetailsRoot;
    private VisualElement m_SectionButtonsRoot;
    private VisualElement m_SectionContentRoot;
    private SectionType m_ActiveSection = SectionType.Metadata;

    // Currently selected preset and its serialized object
    private PlayerControllerPreset m_SelectedPreset;
    private SerializedObject m_PresetSerializedObject;
    #endregion

    #region Properties
    /// <summary>
    /// Gets the root visual element.
    /// </summary>
    public VisualElement Root
    {
        get
        {
            return m_Root;
        }
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the PlayerControllerPresetsPanel class, setting up the UI and loading required
    /// assets.
    /// </summary>
    public PlayerControllerPresetsPanel()
    {
        m_Root = new VisualElement();
        m_Root.style.flexGrow = 1f;

        m_Library = PlayerControllerPresetLibraryUtility.GetOrCreateLibrary();
        m_InputAsset = PlayerInputActionsAssetUtility.LoadOrCreateAsset();

        BuildUI();
        RefreshPresetList();
    }
    #endregion

    #region Methods


    #region UI Construction
    /// <summary>
    /// Constructs the user interface by creating a horizontal split view with left and right panes and adds it to the
    /// root element.
    /// </summary>
    private void BuildUI()
    {
        TwoPaneSplitView splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);

        VisualElement leftPane = BuildLeftPane();
        VisualElement rightPane = BuildRightPane();

        splitView.Add(leftPane);
        splitView.Add(rightPane);

        m_Root.Add(splitView);
    }

    /// <summary>
    /// Constructs and returns the left pane UI containing a toolbar with Create, Duplicate, and Delete buttons, a
    /// search field, and a list view for displaying filtered presets.
    /// </summary>
    /// <returns>A VisualElement representing the left pane UI.</returns>
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

    /// <summary>
    /// Creates and configures the right pane visual element with padding and a scrollable details section.
    /// </summary>
    /// <returns>A VisualElement representing the right pane with a scroll view for details.</returns>
    private VisualElement BuildRightPane()
    {
        // Right pane container
        VisualElement rightPane = new VisualElement();
        // Make it expand to fill available space
        rightPane.style.flexGrow = 1f;
        // Add some padding for aesthetics
        rightPane.style.paddingLeft = 10f;
        rightPane.style.paddingRight = 10f;
        rightPane.style.paddingTop = 6f;

        // Scrollable details area
        m_DetailsRoot = new ScrollView();
        // Make it expand to fill available space
        m_DetailsRoot.style.flexGrow = 1f;
        rightPane.Add(m_DetailsRoot);

        return rightPane;
    }
    #endregion

    #region Preset List
    public void SelectPresetFromExternal(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        RefreshPresetList();

        int index = m_FilteredPresets.IndexOf(preset);

        if (index < 0)
            return;

        if (m_ListView == null)
        {
            SelectPreset(preset);
            return;
        }

        if (m_ListView.selectedIndex != index)
        {
            m_ListView.SetSelection(index);
            return;
        }

        SelectPreset(preset);
    }

    public void RefreshFromSessionChange()
    {
        PlayerControllerPreset previouslySelectedPreset = m_SelectedPreset;
        RefreshPresetList();

        if (previouslySelectedPreset == null)
            return;

        int presetIndex = m_FilteredPresets.IndexOf(previouslySelectedPreset);

        if (presetIndex < 0)
            return;

        if (m_ListView != null)
            m_ListView.SetSelectionWithoutNotify(new int[] { presetIndex });

        SelectPreset(previouslySelectedPreset);
    }

    /// <summary>
    /// Creates and returns a left-aligned label with a left margin for use as a preset item.
    /// </summary>
    /// <returns>A VisualElement representing the styled label.</returns>
    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
        label.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            PlayerControllerPreset preset = label.userData as PlayerControllerPreset;

            if (preset == null)
                return;

            evt.menu.AppendAction("Duplicate", action => DuplicatePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", action => DeletePreset(preset), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Rename", action => ShowRenamePopup(label, preset), DropdownMenuAction.AlwaysEnabled);
        }));
        return label;
    }

    /// <summary>
    /// Binds a preset item's display name and description to a Label element at the specified index.
    /// </summary>
    /// <param name="element">The VisualElement to bind the preset information to.</param>
    /// <param name="index">The index of the preset item in the filtered presets list.</param>
    private void BindPresetItem(VisualElement element, int index)
    {
        Label label = element as Label;

        if (label == null)
            return;

        if (index < 0 || index >= m_FilteredPresets.Count)
        {
            label.text = string.Empty;
            label.userData = null;
            return;
        }

        PlayerControllerPreset preset = m_FilteredPresets[index];

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

    /// <summary>
    /// Handles changes in preset selection by selecting the first PlayerControllerPreset found in the provided
    /// collection, or deselects if none are present.
    /// </summary>
    /// <param name="selection">A collection of selected items to evaluate for a PlayerControllerPreset.</param>
    private void OnPresetSelectionChanged(IEnumerable<object> selection)
    {
        foreach (object item in selection)
        {
            PlayerControllerPreset preset = item as PlayerControllerPreset;

            if (preset != null)
            {
                SelectPreset(preset);
                return;
            }
        }

        SelectPreset(null);
    }

    /// <summary>
    /// Updates the filtered list of presets based on the current search text and refreshes the list view selection.
    /// </summary>
    private void RefreshPresetList()
    {
        m_FilteredPresets.Clear();

        if (m_Library != null)
        {
            string searchText = m_SearchField != null ? m_SearchField.value : string.Empty;

            for (int i = 0; i < m_Library.Presets.Count; i++)
            {
                PlayerControllerPreset preset = m_Library.Presets[i];

                if (preset == null)
                    continue;

                if (PlayerManagementDraftSession.IsAssetStagedForDeletion(preset))
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

            if (m_ListView != null)
                m_ListView.SetSelectionWithoutNotify(new int[] { 0 });
        }
    }

    /// <summary>
    /// Determines whether the preset name matches the specified search text, ignoring case.
    /// </summary>
    /// <param name="preset">The player controller preset to check.</param>
    /// <param name="searchText">The search text to match against the preset name.</param>
    /// <returns>True if the preset name contains the search text or if the search text is empty; otherwise, false.</returns>
    private bool IsMatchingSearch(PlayerControllerPreset preset, string searchText)
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
    /// <summary>
    /// Creates a new player controller preset, adds it to the library, updates the asset database, refreshes the preset
    /// list, and selects the new preset in the list view.
    /// </summary>
    private void CreatePreset()
    {
        PlayerControllerPreset newPreset = PlayerControllerPresetLibraryUtility.CreatePresetAsset("PlayerControllerPreset");

        if (newPreset == null)
            return;

        Undo.RegisterCreatedObjectUndo(newPreset, "Create Controller Preset Asset");
        Undo.RecordObject(m_Library, "Add Preset");
        m_Library.AddPreset(newPreset);
        EditorUtility.SetDirty(m_Library);
        PlayerManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(newPreset);

        int index = m_FilteredPresets.IndexOf(newPreset);
        if (index >= 0)
            m_ListView.SetSelection(index);
    }

    /// <summary>
    /// Creates a duplicate of the currently selected player controller preset, assigns it a new unique ID and name,
    /// adds it to the preset library, and updates the UI selection.
    /// </summary>
    private void DuplicatePreset()
    {
        DuplicatePreset(m_SelectedPreset);
    }

    private void DuplicatePreset(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        PlayerControllerPreset duplicatedPreset = ScriptableObject.CreateInstance<PlayerControllerPreset>();
        EditorUtility.CopySerialized(preset, duplicatedPreset);
        duplicatedPreset.name = preset.name + " Copy";

        string originalPath = AssetDatabase.GetAssetPath(preset);
        string duplicatedPath = AssetDatabase.GenerateUniqueAssetPath(originalPath);

        AssetDatabase.CreateAsset(duplicatedPreset, duplicatedPath);
        Undo.RegisterCreatedObjectUndo(duplicatedPreset, "Duplicate Controller Preset Asset");

        SerializedObject duplicatedSerialized = new SerializedObject(duplicatedPreset);
        SerializedProperty idProperty = duplicatedSerialized.FindProperty("presetId");
        SerializedProperty nameProperty = duplicatedSerialized.FindProperty("presetName");
        if (idProperty != null)
            idProperty.stringValue = Guid.NewGuid().ToString("N");
        if (nameProperty != null)
            nameProperty.stringValue = duplicatedPreset.name;
        duplicatedSerialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(m_Library, "Duplicate Preset");
        m_Library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(m_Library);
        PlayerManagementDraftSession.MarkDirty();

        RefreshPresetList();
        SelectPreset(duplicatedPreset);

        int index = m_FilteredPresets.IndexOf(duplicatedPreset);
        if (index >= 0)
            m_ListView.SetSelection(index);
    }

    /// <summary>
    /// Deletes the currently selected preset after user confirmation and updates the preset list.
    /// </summary>
    private void DeletePreset()
    {
        DeletePreset(m_SelectedPreset);
    }

    private void DeletePreset(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Preset", "Delete the selected preset asset?", "Delete", "Cancel");

        if (confirmed == false)
            return;

        Undo.RecordObject(m_Library, "Delete Preset");
        m_Library.RemovePreset(preset);
        EditorUtility.SetDirty(m_Library);
        PlayerManagementDraftSession.StageDeleteAsset(preset);

        RefreshPresetList();
    }
    #endregion

    #region Preset Details
    /// <summary>
    /// Displays and initializes the details view for the specified player controller preset.
    /// </summary>
    /// <param name="preset">The player controller preset to display and edit.</param>
    private void SelectPreset(PlayerControllerPreset preset)
    {
        m_SelectedPreset = preset;
        m_DetailsRoot.Clear();
        m_SectionButtonsRoot = null;
        m_SectionContentRoot = null;

        if (m_SelectedPreset == null)
        {
            Label label = new Label("Select or create a preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            m_DetailsRoot.Add(label);
            return;
        }

        m_PresetSerializedObject = new SerializedObject(m_SelectedPreset);
        m_SectionButtonsRoot = BuildSectionButtons();
        m_SectionContentRoot = new VisualElement();
        m_SectionContentRoot.style.flexDirection = FlexDirection.Column;
        m_SectionContentRoot.style.flexGrow = 1f;

        m_DetailsRoot.Add(m_SectionButtonsRoot);
        m_DetailsRoot.Add(m_SectionContentRoot);
        BuildActiveSection();
    }

    /// <summary>
    /// Constructs and adds UI elements for displaying and editing preset metadata, including name, version,
    /// description, and ID, to the details root container.
    /// </summary>
    private void BuildMetadataSection()
    {
        if (m_SectionContentRoot == null)
            return;

        Label header = new Label("Preset Details");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        m_SectionContentRoot.Add(header);

        SerializedProperty idProperty = m_PresetSerializedObject.FindProperty("presetId");
        SerializedProperty nameProperty = m_PresetSerializedObject.FindProperty("presetName");
        SerializedProperty descriptionProperty = m_PresetSerializedObject.FindProperty("description");
        SerializedProperty versionProperty = m_PresetSerializedObject.FindProperty("version");

        TextField nameField = new TextField("Preset Name");
        nameField.isDelayed = true;
        nameField.BindProperty(nameProperty);
        nameField.RegisterValueChangedCallback(evt =>
        {
            HandlePresetNameChanged(evt.newValue);
        });
        m_SectionContentRoot.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            RefreshPresetList();
        });
        m_SectionContentRoot.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            RefreshPresetList();
        });
        m_SectionContentRoot.Add(descriptionField);

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

        m_SectionContentRoot.Add(idRow);
    }

    /// <summary>
    /// Generates a new unique identifier for the selected preset and updates its serialized property.
    /// </summary>
    private void RegeneratePresetId()
    {
        if (m_SelectedPreset == null)
            return;

        SerializedProperty idProperty = m_PresetSerializedObject.FindProperty("presetId");

        if (idProperty == null)
            return;

        Undo.RecordObject(m_SelectedPreset, "Regenerate Preset ID");
        m_PresetSerializedObject.Update();
        idProperty.stringValue = Guid.NewGuid().ToString("N");
        m_PresetSerializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Renames the selected preset asset and updates its name, saving changes and refreshing the preset list.
    /// </summary>
    /// <param name="newName">The new name to assign to the selected preset.</param>
    private void HandlePresetNameChanged(string newName)
    {
        RenamePreset(m_SelectedPreset, newName);
    }

    private void RenamePreset(PlayerControllerPreset preset, string newName)
    {
        if (preset == null)
            return;

        string normalizedName = PlayerManagementDraftSession.NormalizeAssetName(newName);

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
        PlayerManagementDraftSession.MarkDirty();
        RefreshPresetList();
    }

    private void ShowRenamePopup(VisualElement anchor, PlayerControllerPreset preset)
    {
        if (anchor == null || preset == null)
            return;

        Rect anchorRect = anchor.worldBound;
        string title = "Rename Controller Preset";
        PresetRenamePopup.Show(anchorRect, title, preset.PresetName, newName => RenamePreset(preset, newName));
    }

    /// <summary>
    /// Returns the display name for a given player controller preset, including its version if available.
    /// </summary>
    /// <param name="preset">The player controller preset to retrieve the display name for.</param>
    /// <returns>The display name of the preset, or a placeholder if the preset is missing.</returns>
    private string GetPresetDisplayName(PlayerControllerPreset preset)
    {
        if (preset == null)
            return "<Missing Preset>";

        string name = string.IsNullOrWhiteSpace(preset.PresetName) ? preset.name : preset.PresetName;
        string version = preset.Version;

        if (string.IsNullOrWhiteSpace(version))
            return name;

        return name + " v. " + version;
    }

    private VisualElement BuildSectionButtons()
    {
        VisualElement buttonsRoot = new VisualElement();
        buttonsRoot.style.flexDirection = FlexDirection.Row;
        buttonsRoot.style.flexWrap = Wrap.Wrap;
        buttonsRoot.style.marginBottom = 6f;

        AddSectionButton(buttonsRoot, SectionType.Metadata, "Metadata");
        AddSectionButton(buttonsRoot, SectionType.Movement, "Movement");
        AddSectionButton(buttonsRoot, SectionType.Look, "Look");
        AddSectionButton(buttonsRoot, SectionType.Shooting, "Shooting");
        AddSectionButton(buttonsRoot, SectionType.Camera, "Camera");
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

    private void SetActiveSection(SectionType sectionType)
    {
        m_ActiveSection = sectionType;
        BuildActiveSection();
    }

    private void BuildActiveSection()
    {
        if (m_SectionContentRoot == null)
            return;

        m_SectionContentRoot.Clear();

        switch (m_ActiveSection)
        {
            case SectionType.Metadata:
                BuildMetadataSection();
                return;
            case SectionType.Movement:
                BuildMovementSection();
                return;
            case SectionType.Look:
                BuildLookSection();
                return;
            case SectionType.Shooting:
                BuildShootingSection();
                return;
            case SectionType.Camera:
                BuildCameraSection();
                return;
        }
    }

    private VisualElement CreateSectionContainer(string sectionTitle)
    {
        if (m_SectionContentRoot == null)
            return null;

        VisualElement container = new VisualElement();
        container.style.marginTop = SectionMarginTop;

        Label header = new Label(sectionTitle);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        container.Add(header);
        m_SectionContentRoot.Add(container);
        return container;
    }
    #endregion

    #region Movement Section
    /// <summary>
    /// Constructs and configures the UI section for movement settings, including direction mode, direction count,
    /// offset, movement reference, input bindings, and related value fields.
    /// </summary>
    private void BuildMovementSection()
    {
        VisualElement section = CreateSectionContainer("Movement Settings");

        if (section == null)
            return;

        SerializedProperty movementProperty = m_PresetSerializedObject.FindProperty("movementSettings");

        if (movementProperty == null)
            return;

        SerializedProperty modeProperty = movementProperty.FindPropertyRelative("directionsMode");
        SerializedProperty countProperty = movementProperty.FindPropertyRelative("discreteDirectionCount");
        SerializedProperty offsetProperty = movementProperty.FindPropertyRelative("directionOffsetDegrees");
        SerializedProperty referenceProperty = movementProperty.FindPropertyRelative("movementReference");
        SerializedProperty valuesProperty = movementProperty.FindPropertyRelative("values");

        EnumField modeField = new EnumField("Allowed Directions");
        modeField.BindProperty(modeProperty);
        section.Add(modeField);

        VisualElement discreteContainer = new VisualElement();
        discreteContainer.style.marginLeft = 8f;

        IntegerField countField = new IntegerField("Direction Count");
        countField.BindProperty(countProperty);
        discreteContainer.Add(countField);

        PieChartElement pieChart = new PieChartElement();
        Slider movementZoomSlider = CreatePieZoomSlider(pieChart);
        discreteContainer.Add(pieChart);
        discreteContainer.Add(movementZoomSlider);

        section.Add(discreteContainer);

        EnumField referenceField = new EnumField("Movement Reference");
        referenceField.BindProperty(referenceProperty);
        section.Add(referenceField);

        SerializedProperty moveActionProperty = m_PresetSerializedObject.FindProperty("moveActionId");
        EnsureDefaultActionId(moveActionProperty, "Move");

        Foldout bindingsFoldout = BuildBindingsFoldout(m_InputAsset, m_PresetSerializedObject, moveActionProperty, InputActionSelectionElement.SelectionMode.Movement);
        section.Add(bindingsFoldout);

        Foldout valuesFoldout = BuildValuesFoldout(valuesProperty, new string[]
        {
            "baseSpeed",
            "maxSpeed",
            "acceleration",
            "deceleration",
            "oppositeDirectionBrakeMultiplier",
            "wallBounceCoefficient",
            "wallCollisionSkinWidth",
            "inputDeadZone",
            "digitalReleaseGraceSeconds"
        });
        section.Add(valuesFoldout);

        Action updateView = () =>
        {
            MovementDirectionsMode mode = (MovementDirectionsMode)modeProperty.enumValueIndex;
            bool isDiscrete = mode == MovementDirectionsMode.DiscreteCount;
            discreteContainer.style.display = isDiscrete ? DisplayStyle.Flex : DisplayStyle.None;

            if (isDiscrete)
            {
                SnapOffsetToStep(offsetProperty, countProperty.intValue);
                UpdateDiscretePieChart(pieChart, countProperty.intValue, offsetProperty.floatValue);
            }
        };

        modeField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        countField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        updateView();
    }
    #endregion

    #region Look Section
    /// <summary>
    /// Constructs and configures the UI section for look settings, including direction modes, cones, rotation options,
    /// input bindings, and related controls.
    /// </summary>
    private void BuildLookSection()
    {
        VisualElement section = CreateSectionContainer("Look Settings");

        if (section == null)
            return;

        // Look Settings Properties
        SerializedProperty lookProperty = m_PresetSerializedObject.FindProperty("lookSettings");

        if (lookProperty == null)
            return;

        SerializedProperty directionsModeProperty = lookProperty.FindPropertyRelative("m_DirectionsMode");
        SerializedProperty countProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionCount");
        SerializedProperty offsetProperty = lookProperty.FindPropertyRelative("m_DirectionOffsetDegrees");
        SerializedProperty rotationModeProperty = lookProperty.FindPropertyRelative("m_RotationMode");
        SerializedProperty rotationSpeedProperty = lookProperty.FindPropertyRelative("m_RotationSpeed");
        SerializedProperty samplingProperty = lookProperty.FindPropertyRelative("m_MultiplierSampling");
        SerializedProperty maxSpeedMultipliersProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionMaxSpeedMultipliers");
        SerializedProperty accelerationMultipliersProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionAccelerationMultipliers");

        // Cone properties
        SerializedProperty frontEnabledProperty = lookProperty.FindPropertyRelative("m_FrontConeEnabled");
        SerializedProperty frontAngleProperty = lookProperty.FindPropertyRelative("m_FrontConeAngle");
        SerializedProperty frontMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_FrontConeMaxSpeedMultiplier");
        SerializedProperty frontAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_FrontConeAccelerationMultiplier");

        // Back cone properties
        SerializedProperty backEnabledProperty = lookProperty.FindPropertyRelative("m_BackConeEnabled");
        SerializedProperty backAngleProperty = lookProperty.FindPropertyRelative("m_BackConeAngle");
        SerializedProperty backMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_BackConeMaxSpeedMultiplier");
        SerializedProperty backAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_BackConeAccelerationMultiplier");

        // Left cone properties
        SerializedProperty leftEnabledProperty = lookProperty.FindPropertyRelative("m_LeftConeEnabled");
        SerializedProperty leftAngleProperty = lookProperty.FindPropertyRelative("m_LeftConeAngle");
        SerializedProperty leftMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_LeftConeMaxSpeedMultiplier");
        SerializedProperty leftAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_LeftConeAccelerationMultiplier");

        // Right cone properties
        SerializedProperty rightEnabledProperty = lookProperty.FindPropertyRelative("m_RightConeEnabled");
        SerializedProperty rightAngleProperty = lookProperty.FindPropertyRelative("m_RightConeAngle");
        SerializedProperty rightMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_RightConeMaxSpeedMultiplier");
        SerializedProperty rightAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_RightConeAccelerationMultiplier");

        // Build Look Settings UI
        EnumField directionsModeField = new EnumField("Allowed Directions");
        directionsModeField.BindProperty(directionsModeProperty);
        section.Add(directionsModeField);

        // Discrete Directions Container
        VisualElement discreteContainer = new VisualElement();
        discreteContainer.style.marginLeft = 8f;

        // Direction Count Field
        IntegerField countField = new IntegerField("Direction Count");
        countField.BindProperty(countProperty);
        discreteContainer.Add(countField);

        // Direction Offset Field
        //FloatField offsetField = new FloatField("Direction Offset");
        //offsetField.BindProperty(offsetProperty);
        //discreteContainer.Add(offsetField);

        // Cones Container
        VisualElement conesContainer = new VisualElement();
        conesContainer.style.marginLeft = 8f;
        conesContainer.style.marginTop = 4f;

        // Lists to hold references to cone toggles and angle fields for event registration
        List<Toggle> coneToggles = new List<Toggle>();
        List<FloatField> coneAngleFields = new List<FloatField>();

        // Add cone rows
        conesContainer.Add(BuildConeRow("Front", frontEnabledProperty, frontAngleProperty, frontMaxSpeedMultiplierProperty, frontAccelerationMultiplierProperty, coneToggles, coneAngleFields));
        conesContainer.Add(BuildConeRow("Back", backEnabledProperty, backAngleProperty, backMaxSpeedMultiplierProperty, backAccelerationMultiplierProperty, coneToggles, coneAngleFields));
        conesContainer.Add(BuildConeRow("Left", leftEnabledProperty, leftAngleProperty, leftMaxSpeedMultiplierProperty, leftAccelerationMultiplierProperty, coneToggles, coneAngleFields));
        conesContainer.Add(BuildConeRow("Right", rightEnabledProperty, rightAngleProperty, rightMaxSpeedMultiplierProperty, rightAccelerationMultiplierProperty, coneToggles, coneAngleFields));

        // Pie Chart and Multiplier Legend
        PieChartElement pieChart = new PieChartElement();
        //VisualElement multiplierLegend = BuildMultiplierLegend();
        Slider lookZoomSlider = CreatePieZoomSlider(pieChart);
        VisualElement multipliersSection = BuildDiscreteMultipliersSection(out VisualElement multipliersTableRoot, out Label multipliersHeader);

        section.Add(discreteContainer);
        section.Add(conesContainer);
        //foldout.Add(multiplierLegend);
        section.Add(pieChart);
        section.Add(lookZoomSlider);
        section.Add(multipliersSection);

        // Rotation Mode Field
        EnumField rotationModeField = new EnumField("Rotation Mode");
        rotationModeField.BindProperty(rotationModeProperty);
        section.Add(rotationModeField);

        // Rotation Speed Field
        FloatField rotationSpeedField = new FloatField("Rotation Speed");
        rotationSpeedField.BindProperty(rotationSpeedProperty);
        section.Add(rotationSpeedField);

        // Multiplier Sampling Field
        EnumField samplingField = new EnumField("Multiplier Sampling");
        samplingField.BindProperty(samplingProperty);
        section.Add(samplingField);

        // Input Bindings
        SerializedProperty lookActionProperty = m_PresetSerializedObject.FindProperty("lookActionId");
        EnsureDefaultActionId(lookActionProperty, "Look");

        // Bindings Foldout
        Foldout bindingsFoldout = BuildBindingsFoldout(m_InputAsset, m_PresetSerializedObject, lookActionProperty, InputActionSelectionElement.SelectionMode.Look);
        section.Add(bindingsFoldout);

        // Values Foldout
        SerializedProperty valuesProperty = lookProperty.FindPropertyRelative("m_Values");
        Foldout valuesFoldout = BuildValuesFoldout(valuesProperty, new string[]
        {
            "m_RotationDamping",
            "m_RotationMaxSpeed",
            "m_RotationDeadZone",
            "m_DigitalReleaseGraceSeconds"
        });
        section.Add(valuesFoldout);

        // Update View Action
        Action updateView = () =>
        {
            LookDirectionsMode directionsMode = (LookDirectionsMode)directionsModeProperty.enumValueIndex;
            RotationMode rotationMode = (RotationMode)rotationModeProperty.enumValueIndex;
            LookMultiplierSampling samplingMode = samplingProperty != null ? (LookMultiplierSampling)samplingProperty.enumValueIndex : LookMultiplierSampling.DirectionalBlend;

            bool isDiscrete = directionsMode == LookDirectionsMode.DiscreteCount;
            bool isCones = directionsMode == LookDirectionsMode.Cones;
            bool followMovement = directionsMode == LookDirectionsMode.FollowMovementDirection;

            discreteContainer.style.display = isDiscrete && followMovement == false ? DisplayStyle.Flex : DisplayStyle.None;
            conesContainer.style.display = isCones && followMovement == false ? DisplayStyle.Flex : DisplayStyle.None;
            pieChart.style.display = directionsMode == LookDirectionsMode.AllDirections || followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            //multiplierLegend.style.display = directionsMode == LookDirectionsMode.AllDirections ? DisplayStyle.None : DisplayStyle.Flex;
            lookZoomSlider.style.display = directionsMode == LookDirectionsMode.AllDirections || followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            rotationModeField.style.display = followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            rotationSpeedField.style.display = rotationMode == RotationMode.Continuous && followMovement == false ? DisplayStyle.Flex : DisplayStyle.None;
            samplingField.style.display = isDiscrete && followMovement == false ? DisplayStyle.Flex : DisplayStyle.None;
            bindingsFoldout.style.display = followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            valuesFoldout.style.display = followMovement ? DisplayStyle.None : DisplayStyle.Flex;

            if (isDiscrete && followMovement == false)
                SnapOffsetToStep(offsetProperty, countProperty.intValue);

            UpdateLookPieChart(pieChart, directionsMode, countProperty.intValue, offsetProperty.floatValue, frontEnabledProperty, frontAngleProperty, backEnabledProperty, backAngleProperty, leftEnabledProperty, leftAngleProperty, rightEnabledProperty, rightAngleProperty);
            UpdateLookLabels(pieChart, directionsMode, samplingMode, countProperty.intValue, offsetProperty.floatValue);
            pieChart.SetOverlayFields(null);

            multipliersSection.style.display = isDiscrete && followMovement == false ? DisplayStyle.Flex : DisplayStyle.None;
            multipliersSection.style.marginLeft = SubSectionMarginLeft;

            if (multipliersHeader != null)
            {
                multipliersHeader.text = samplingMode == LookMultiplierSampling.ArcConstant ? "Arc Multipliers" : "Directional Multipliers";
            }

            if (isDiscrete)
            {
                UpdateDiscreteMultipliersTable(multipliersTableRoot, samplingMode, countProperty.intValue, offsetProperty.floatValue, maxSpeedMultipliersProperty, accelerationMultipliersProperty);
            }
        };

        // Register events to update the view when relevant properties change
        directionsModeField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        countField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        //offsetField.RegisterValueChangedCallback(evt =>
        //{
        //    updateView();
        //});

        rotationModeField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        samplingField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        // Register events for cone toggles and angle fields
        for (int i = 0; i < coneToggles.Count; i++)
        {
            Toggle toggle = coneToggles[i];
            toggle.RegisterValueChangedCallback(evt =>
            {
                updateView();
            });
        }

        // Register events for cone angle fields
        for (int i = 0; i < coneAngleFields.Count; i++)
        {
            FloatField angleField = coneAngleFields[i];
            angleField.RegisterValueChangedCallback(evt =>
            {
                updateView();
            });
        }

        updateView();
    }

    /// <summary>
    /// Creates a row VisualElement containing a labeled toggle and an angle float field, binding them to the specified
    /// serialized properties and optionally adding them to provided lists.
    /// </summary>
    /// <param name="label">The label text for the toggle.</param>
    /// <param name="enabledProperty">The serialized property to bind to the toggle.</param>
    /// <param name="angleProperty">The serialized property to bind to the angle float field.</param>
    /// <param name="toggles">An optional list to which the created toggle will be added.</param>
    /// <param name="angleFields">An optional list to which the created angle float field will be added.</param>
    /// <returns>A VisualElement representing the constructed row.</returns>
    private VisualElement BuildConeRow(string label, SerializedProperty enabledProperty, SerializedProperty angleProperty, SerializedProperty maxSpeedProperty, SerializedProperty accelerationProperty, List<Toggle> toggles, List<FloatField> angleFields)
    {
        // Create row container
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = MultiplierRowSpacing;

        // Create and configure enabled toggle
        Toggle enabledToggle = new Toggle(label);
        enabledToggle.style.minWidth = 120f;
        enabledToggle.BindProperty(enabledProperty);
        row.Add(enabledToggle);

        // Create and configure angle field
        FloatField angleField = new FloatField("Angle");
        angleField.style.flexGrow = 0f;
        angleField.style.width = 110f;
        angleField.BindProperty(angleProperty);
        row.Add(angleField);

        // Create and configure multiplier fields
        FloatField maxSpeedField = CreatePercentField(maxSpeedProperty, MaxSpeedMultiplierColor, "Max speed multiplier for this cone.", "Max %", MultiplierFieldWidth);
        FloatField accelerationField = CreatePercentField(accelerationProperty, AccelerationMultiplierColor, "Acceleration multiplier for this cone.", "Accel %", MultiplierFieldWidth);
        row.Add(maxSpeedField);
        row.Add(accelerationField);

        // Add to provided lists if applicable
        if (toggles != null)
        {
            toggles.Add(enabledToggle);
        }

        // Add to provided lists if applicable
        if (angleFields != null)
        {
            angleFields.Add(angleField);
        }

        // return the constructed row
        return row;
    }
    #endregion

    #region Shooting Section
    /// <summary>
    /// Constructs and configures the shooting settings section, including trigger mode, projectile references, action
    /// binding, and shooting values.
    /// </summary>
    private void BuildShootingSection()
    {
        VisualElement section = CreateSectionContainer("Shooting Settings");

        if (section == null)
            return;

        SerializedProperty shootingProperty = m_PresetSerializedObject.FindProperty("shootingSettings");

        if (shootingProperty == null)
            return;

        // Shooting Settings Properties
        SerializedProperty triggerModeProperty = shootingProperty.FindPropertyRelative("triggerMode");
        SerializedProperty inheritPlayerSpeedProperty = shootingProperty.FindPropertyRelative("projectilesInheritPlayerSpeed");
        SerializedProperty projectilePrefabProperty = shootingProperty.FindPropertyRelative("projectilePrefab");
        SerializedProperty shootOffsetProperty = shootingProperty.FindPropertyRelative("shootOffset");
        SerializedProperty valuesProperty = shootingProperty.FindPropertyRelative("values");
        SerializedProperty initialPoolCapacityProperty = shootingProperty.FindPropertyRelative("initialPoolCapacity");
        SerializedProperty poolExpandBatchProperty = shootingProperty.FindPropertyRelative("poolExpandBatch");

        // Build Shooting Settings UI
        EnumField triggerModeField = new EnumField("Trigger Mode");
        triggerModeField.BindProperty(triggerModeProperty);
        section.Add(triggerModeField);

        Toggle inheritPlayerSpeedField = new Toggle("Projectiles Inherit Player Speed");
        inheritPlayerSpeedField.tooltip = "When enabled, projectiles inherit the player's horizontal velocity while they are active.";
        inheritPlayerSpeedField.BindProperty(inheritPlayerSpeedProperty);
        section.Add(inheritPlayerSpeedField);

        ObjectField projectilePrefabField = new ObjectField("Projectile Prefab");
        projectilePrefabField.objectType = typeof(GameObject);
        projectilePrefabField.BindProperty(projectilePrefabProperty);
        section.Add(projectilePrefabField);

        Vector3Field shootOffsetField = new Vector3Field("Shoot Offset");
        shootOffsetField.tooltip = "Offset applied from the Weapon Reference set on PlayerAuthoring (fallback: player transform).";
        shootOffsetField.BindProperty(shootOffsetProperty);
        section.Add(shootOffsetField);

        SerializedProperty shootActionProperty = m_PresetSerializedObject.FindProperty("shootActionId");
        EnsureDefaultActionId(shootActionProperty, "Shoot");

        Foldout bindingsFoldout = BuildBindingsFoldout(m_InputAsset, m_PresetSerializedObject, shootActionProperty, InputActionSelectionElement.SelectionMode.Shooting);
        section.Add(bindingsFoldout);

        Foldout valuesFoldout = BuildValuesFoldout(valuesProperty, new string[]
        {
            "shootSpeed",
            "rateOfFire",
            "range",
            "lifetime",
            "damage"
        });
        section.Add(valuesFoldout);

        Foldout objectPoolFoldout = new Foldout();
        objectPoolFoldout.text = "Object Pool";
        objectPoolFoldout.value = true;

        IntegerField initialPoolCapacityField = new IntegerField("Initial Capacity");
        initialPoolCapacityField.tooltip = "Number of projectiles pre-created when the pool initializes.";
        initialPoolCapacityField.BindProperty(initialPoolCapacityProperty);
        objectPoolFoldout.Add(initialPoolCapacityField);

        IntegerField poolExpandBatchField = new IntegerField("Expand Batch");
        poolExpandBatchField.tooltip = "Number of projectiles created each time the pool needs expansion.";
        poolExpandBatchField.BindProperty(poolExpandBatchProperty);
        objectPoolFoldout.Add(poolExpandBatchField);

        section.Add(objectPoolFoldout);
    }
    #endregion

    #region Camera Section
    /// <summary>
    /// Constructs and configures the camera settings section of the UI, including controls for camera behavior, follow
    /// offset, room anchor, and related values.
    /// </summary>
    private void BuildCameraSection()
    {
        VisualElement section = CreateSectionContainer("Camera Settings");

        if (section == null)
            return;

        SerializedProperty cameraProperty = m_PresetSerializedObject.FindProperty("cameraSettings");

        if (cameraProperty == null)
            return;

        SerializedProperty behaviorProperty = cameraProperty.FindPropertyRelative("behavior");
        SerializedProperty offsetProperty = cameraProperty.FindPropertyRelative("followOffset");
        SerializedProperty anchorProperty = cameraProperty.FindPropertyRelative("roomAnchor");
        SerializedProperty valuesProperty = cameraProperty.FindPropertyRelative("values");

        EnumField behaviorField = new EnumField("Camera Behavior");
        behaviorField.BindProperty(behaviorProperty);
        section.Add(behaviorField);

        Vector3Field offsetField = new Vector3Field("Follow Offset");
        offsetField.BindProperty(offsetProperty);
        section.Add(offsetField);

        ObjectField anchorField = new ObjectField("Room Anchor");
        anchorField.objectType = typeof(Transform);
        anchorField.BindProperty(anchorProperty);
        section.Add(anchorField);

        Foldout valuesFoldout = BuildValuesFoldout(valuesProperty, new string[]
        {
            "followSpeed",
            "cameraLag",
            "damping",
            "maxFollowDistance",
            "deadZoneRadius"
        });
        section.Add(valuesFoldout);

        Action updateView = () =>
        {
            CameraBehavior behavior = (CameraBehavior)behaviorProperty.enumValueIndex;
            offsetField.style.display = behavior == CameraBehavior.FollowWithOffset ? DisplayStyle.Flex : DisplayStyle.None;
            anchorField.style.display = behavior == CameraBehavior.RoomFixed ? DisplayStyle.Flex : DisplayStyle.None;
        };

        behaviorField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        updateView();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// This method constructs a foldout containing a scrollable list of property fields 
    /// based on the provided serialized property, and binds each field to the corresponding property 
    /// in the serialized object. 
    /// The foldout is titled "Values" and is designed to display a set of related properties in a compact, 
    /// organized manner.
    /// </summary>
    /// <param name="valuesProperty"></param>
    /// <param name="fieldNames"></param>
    /// <returns></returns>
    private Foldout BuildValuesFoldout(SerializedProperty valuesProperty, string[] fieldNames)
    {
        Foldout foldout = new Foldout();
        foldout.text = "Values";
        foldout.value = true;

        ScrollView scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.maxHeight = 160f;
        scrollView.style.minHeight = 100f;

        if (valuesProperty != null)
        {
            for (int i = 0; i < fieldNames.Length; i++)
            {
                SerializedProperty fieldProperty = valuesProperty.FindPropertyRelative(fieldNames[i]);

                if (fieldProperty == null)
                    continue;

                PropertyField propertyField = new PropertyField(fieldProperty);
                propertyField.BindProperty(fieldProperty);
                scrollView.Add(propertyField);
            }
        }

        foldout.Add(scrollView);
        return foldout;
    }

    /// <summary>
    /// This method checks if the provided action ID is valid and corresponds to an existing action in the input asset.
    /// If it does not, the method attempts to find a default action by name and assigns its ID to the property. 
    /// This ensures that the preset always has a valid action reference, preventing potential issues with missing 
    /// or invalid action bindings in the UI.
    /// </summary>
    /// <param name="actionIdProperty"></param>
    /// <param name="actionName"></param>
    private void EnsureDefaultActionId(SerializedProperty actionIdProperty, string actionName)
    {
        if (actionIdProperty == null)
            return;

        if (m_InputAsset == null)
            return;

        string currentId = actionIdProperty.stringValue;

        if (string.IsNullOrWhiteSpace(currentId) == false)
        {
            InputAction existingAction = m_InputAsset.FindAction(currentId, false);

            if (existingAction != null)
                return;
        }

        InputAction defaultAction = m_InputAsset.FindAction(actionName, false);

        if (defaultAction == null)
            return;

        Undo.RecordObject(m_SelectedPreset, "Assign Default Action");
        m_PresetSerializedObject.Update();
        actionIdProperty.stringValue = defaultAction.id.ToString();
        m_PresetSerializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// This methpd constructs a foldout containing an InputActionSelectionElement, 
    /// which allows the user to select and bind input actions from the provided input asset.
    /// </summary>
    /// <param name="inputAsset"></param>
    /// <param name="presetSerializedObject"></param>
    /// <param name="actionIdProperty"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    private Foldout BuildBindingsFoldout(InputActionAsset inputAsset, SerializedObject presetSerializedObject, SerializedProperty actionIdProperty, InputActionSelectionElement.SelectionMode mode)
    {
        Foldout foldout = new Foldout();
        foldout.text = "Bindings";
        foldout.value = true;

        InputActionSelectionElement bindingsElement = new InputActionSelectionElement(inputAsset, presetSerializedObject, actionIdProperty, mode);
        foldout.Add(bindingsElement);

        return foldout;
    }

    /// <summary>
    /// This method creates a slider control for adjusting the zoom level of a pie chart, 
    /// binding its value change event to update the zoom level 
    /// of the provided PieChartElement.
    /// </summary>
    /// <param name="pieChart"></param>
    /// <returns></returns>
    private Slider CreatePieZoomSlider(PieChartElement pieChart)
    {
        Slider slider = new Slider("Pie Zoom", 0.6f, 1.6f);
        slider.value = 1f;
        slider.RegisterValueChangedCallback(evt =>
        {
            if (pieChart == null)
                return;

            pieChart.SetZoom(evt.newValue);
        });

        return slider;
    }

    //private VisualElement BuildMultiplierLegend()
    //{
    //    VisualElement container = new VisualElement();
    //    container.style.flexDirection = FlexDirection.Row;
    //    container.style.marginTop = 4f;
    //    container.style.marginBottom = 2f;

    //    container.Add(BuildLegendItem(MaxSpeedMultiplierColor, "Max Speed"));
    //    container.Add(BuildLegendItem(AccelerationMultiplierColor, "Acceleration"));

    //    return container;
    //}

    //private VisualElement BuildLegendItem(Color color, string label)
    //{
    //    VisualElement item = new VisualElement();
    //    item.style.flexDirection = FlexDirection.Row;
    //    item.style.alignItems = Align.Center;
    //    item.style.marginRight = 12f;

    //    VisualElement swatch = new VisualElement();
    //    swatch.style.width = 10f;
    //    swatch.style.height = 10f;
    //    swatch.style.backgroundColor = color;
    //    swatch.style.marginRight = 4f;
    //    item.Add(swatch);

    //    Label text = new Label(label);
    //    text.style.fontSize = 10f;
    //    item.Add(text);

    //    return item;
    //}

    /// <summary>
    /// This method constructs a UI section for displaying and editing discrete multipliers based on the specified sampling mode.
    /// in detail, it creates a container with a header label and a root element for the multipliers table, 
    /// which will be populated dynamically 
    /// based on the current settings of the preset.
    /// </summary>
    /// <param name="tableRoot"></param>
    /// <param name="headerLabel"></param>
    /// <returns></returns>
    private VisualElement BuildDiscreteMultipliersSection(out VisualElement tableRoot, out Label headerLabel)
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 4f;

        headerLabel = new Label("Directional Multipliers");
        headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerLabel.style.marginBottom = 2f;
        container.Add(headerLabel);

        tableRoot = new VisualElement();
        tableRoot.style.flexDirection = FlexDirection.Column;
        tableRoot.style.alignItems = Align.FlexStart;
        container.Add(tableRoot);

        return container;
    }

    /// <summary>
    /// This method updates the discrete multipliers table based on the current sampling mode, 
    /// count, and offset. In detail, it clears the existing table and repopulates it with rows 
    /// corresponding to each discrete direction or arc,
    /// and binds the max speed and acceleration multiplier fields to the appropriate serialized properties.
    /// </summary>
    /// <param name="tableRoot"></param>
    /// <param name="samplingMode"></param>
    /// <param name="count"></param>
    /// <param name="offset"></param>
    /// <param name="maxSpeedMultipliersProperty"></param>
    /// <param name="accelerationMultipliersProperty"></param>
    private void UpdateDiscreteMultipliersTable(VisualElement tableRoot, LookMultiplierSampling samplingMode, int count, float offset, SerializedProperty maxSpeedMultipliersProperty, SerializedProperty accelerationMultipliersProperty)
    {
        // Validate inputs
        if (tableRoot == null)
        {
            return;
        }

        tableRoot.Clear();

        if (maxSpeedMultipliersProperty == null || accelerationMultipliersProperty == null)
        {
            return;
        }

        // Ensure the multiplier arrays have the correct size based on the count
        int sliceCount = Mathf.Max(1, count);
        EnsureArraySize(maxSpeedMultipliersProperty, sliceCount);
        EnsureArraySize(accelerationMultipliersProperty, sliceCount);

        // if the properties are arrays, ensure they have the correct size
        string maxSpeedTooltip = samplingMode == LookMultiplierSampling.ArcConstant
            ? "Max speed multiplier for this look arc (constant across the arc)."
            : "Max speed multiplier for this look direction (blended by alignment).";
        string accelerationTooltip = samplingMode == LookMultiplierSampling.ArcConstant
            ? "Acceleration multiplier for this look arc (constant across the arc)."
            : "Acceleration multiplier for this look direction (blended by alignment).";

        tableRoot.Add(BuildMultipliersHeaderRow(samplingMode));

        float step = 360f / sliceCount;

        // Build rows for each discrete direction or arc
        for (int i = 0; i < sliceCount; i++)
        {
            SerializedProperty maxSpeedElement = maxSpeedMultipliersProperty.GetArrayElementAtIndex(i);
            SerializedProperty accelerationElement = accelerationMultipliersProperty.GetArrayElementAtIndex(i);

            if (samplingMode == LookMultiplierSampling.ArcConstant)
            {
                float startAngle = offset - (step * 0.5f) + (i * step);
                float endAngle = startAngle + step;
                string range = FormatAngleRange(startAngle, endAngle);
                VisualElement row = BuildArcRow(i, range, maxSpeedElement, accelerationElement, maxSpeedTooltip, accelerationTooltip);
                tableRoot.Add(row);
            }
            else
            {
                float angle = Mathf.Repeat(offset + (i * step), 360f);
                VisualElement row = BuildDirectionRow(i, angle, maxSpeedElement, accelerationElement, maxSpeedTooltip, accelerationTooltip);
                tableRoot.Add(row);
            }
        }
    }

    /// <summary>
    /// Creates a header row for multipliers based on the specified sampling mode.
    /// </summary>
    /// <param name="samplingMode">The sampling mode that determines which header labels are displayed.</param>
    /// <returns>A VisualElement representing the header row.</returns>
    private VisualElement BuildMultipliersHeaderRow(LookMultiplierSampling samplingMode)
    {
        // Create a row container for the header
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.FlexStart;
        row.style.flexWrap = Wrap.NoWrap;
        row.style.marginBottom = MultiplierRowSpacing;

        // Add header labels based on the sampling mode
        if (samplingMode == LookMultiplierSampling.ArcConstant)
        {
            row.Add(BuildHeaderLabel("Arc", MultiplierLabelWidth));
            row.Add(BuildHeaderLabel("Range", MultiplierRangeWidth));
        }
        else
        {
            row.Add(BuildHeaderLabel("Dir", MultiplierLabelWidth));
            row.Add(BuildHeaderLabel("Angle", MultiplierAngleWidth));
        }

        row.Add(BuildHeaderLabel("Max %", MultiplierFieldWidth));
        row.Add(BuildHeaderLabel("Accel %", MultiplierFieldWidth));

        return row;
    }

    /// <summary>
    /// This method constructs a UI row for a discrete direction, 
    /// including labels for the direction index and angle,
    /// and float fields for max speed and acceleration multipliers,
    /// which are bound to the provided serialized properties.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="angle"></param>
    /// <param name="maxSpeedProperty"></param>
    /// <param name="accelerationProperty"></param>
    /// <param name="maxSpeedTooltip"></param>
    /// <param name="accelerationTooltip"></param>
    /// <returns></returns>
    private VisualElement BuildDirectionRow(int index, float angle, SerializedProperty maxSpeedProperty, SerializedProperty accelerationProperty, string maxSpeedTooltip, string accelerationTooltip)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.FlexStart;
        row.style.flexWrap = Wrap.NoWrap;
        row.style.marginBottom = MultiplierRowSpacing;

        Label dirLabel = BuildRowLabel("Dir " + (index + 1), MultiplierLabelWidth);
        row.Add(dirLabel);

        Label angleLabel = BuildRowLabel(angle.ToString("0.#") + "°", MultiplierAngleWidth);
        row.Add(angleLabel);

        FloatField maxSpeedField = CreatePercentField(maxSpeedProperty, MaxSpeedMultiplierColor, maxSpeedTooltip, string.Empty, MultiplierFieldWidth);
        FloatField accelerationField = CreatePercentField(accelerationProperty, AccelerationMultiplierColor, accelerationTooltip, string.Empty, MultiplierFieldWidth);
        row.Add(maxSpeedField);
        row.Add(accelerationField);

        return row;
    }

    /// <summary>
    /// This method constructs a UI row for a look arc, including labels for the arc index and angle range,
    /// and float fields for max speed and acceleration multipliers, 
    /// which are bound to the provided serialized properties.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="range"></param>
    /// <param name="maxSpeedProperty"></param>
    /// <param name="accelerationProperty"></param>
    /// <param name="maxSpeedTooltip"></param>
    /// <param name="accelerationTooltip"></param>
    /// <returns></returns>
    private VisualElement BuildArcRow(int index, string range, SerializedProperty maxSpeedProperty, SerializedProperty accelerationProperty, string maxSpeedTooltip, string accelerationTooltip)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.FlexStart;
        row.style.flexWrap = Wrap.NoWrap;
        row.style.marginBottom = MultiplierRowSpacing;

        Label arcLabel = BuildRowLabel("Arc " + (index + 1), MultiplierLabelWidth);
        row.Add(arcLabel);

        Label rangeLabel = BuildRowLabel(range, MultiplierRangeWidth);
        row.Add(rangeLabel);

        FloatField maxSpeedField = CreatePercentField(maxSpeedProperty, MaxSpeedMultiplierColor, maxSpeedTooltip, string.Empty, MultiplierFieldWidth);
        FloatField accelerationField = CreatePercentField(accelerationProperty, AccelerationMultiplierColor, accelerationTooltip, string.Empty, MultiplierFieldWidth);
        row.Add(maxSpeedField);
        row.Add(accelerationField);

        return row;
    }

    /// <summary>
    /// Creates a styled Label element for use as a header in the multipliers table,
    /// and configures its width, font size, weight, and text alignment to ensure a consistent 
    /// appearance across the table.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="width"></param>
    /// <returns></returns>
    private Label BuildHeaderLabel(string text, float width)
    {
        Label label = new Label(text);
        label.style.width = width;
        label.style.minWidth = width;
        label.style.maxWidth = width;
        label.style.flexGrow = 0f;
        label.style.flexShrink = 0f;
        label.style.fontSize = 10f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        return label;
    }

    /// <summary>
    /// Builds a styled Label element for a row in the multipliers table, 
    /// configuring its width and text alignment
    /// </summary>
    /// <param name="text"></param>
    /// <param name="width"></param>
    /// <returns></returns>
    private Label BuildRowLabel(string text, float width)
    {
        Label label = new Label(text);
        label.style.width = width;
        label.style.minWidth = width;
        label.style.maxWidth = width;
        label.style.flexGrow = 0f;
        label.style.flexShrink = 0f;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        return label;
    }

    /// <summary>
    /// This method creates a FloatField configured to represent a percentage value, 
    /// binding it to the specified serialized property.
    /// </summary>
    /// <param name="property"></param>
    /// <param name="color"></param>
    /// <param name="tooltip"></param>
    /// <param name="label"></param>
    /// <param name="width"></param>
    /// <returns></returns>
    private FloatField CreatePercentField(SerializedProperty property, Color color, string tooltip, string label, float width)
    {
        FloatField field = new FloatField(label);
        field.isDelayed = true;
        field.style.width = width;
        field.style.minWidth = width;
        field.style.maxWidth = width;
        field.style.flexGrow = 0f;
        field.style.flexShrink = 0f;
        field.style.height = MultiplierFieldHeight;
        field.style.fontSize = 10f;
        field.style.unityTextAlign = TextAnchor.MiddleCenter;
        field.style.color = color;
        field.tooltip = tooltip;

        if (string.IsNullOrWhiteSpace(label))
        {
            field.labelElement.style.display = DisplayStyle.None;
        }

        if (property != null)
        {
            float value = property.floatValue;
            field.value = value * 100f;

            field.RegisterValueChangedCallback(evt =>
            {
                if (property == null)
                {
                    return;
                }

                float inputValue = evt.newValue;
                float storedValue = Mathf.Clamp(inputValue / 100f, 0f, 1f);
                property.floatValue = storedValue;
                property.serializedObject.ApplyModifiedProperties();
                field.SetValueWithoutNotify(storedValue * 100f);
            });
        }

        return field;
    }

    /// <summary>
    /// Formats a range of angles into a human-readable string, normalizing the angles to the [0, 360) range 
    /// and indicating if the range wraps around the 360-degree point.
    /// </summary>
    /// <param name="startAngle"></param>
    /// <param name="endAngle"></param>
    /// <returns></returns>
    private string FormatAngleRange(float startAngle, float endAngle)
    {
        float normalizedStart = Mathf.Repeat(startAngle, 360f);
        float normalizedEnd = Mathf.Repeat(endAngle, 360f);
        string startText = normalizedStart.ToString("0.#") + "°";
        string endText = normalizedEnd.ToString("0.#") + "°";

        if (normalizedEnd < normalizedStart)
        {
            return startText + " - " + endText + " (wrap)";
        }

        return startText + " - " + endText;
    }

    /// <summary>
    /// This method updates the segment labels of the provided pie chart based
    /// on the current look directions mode and sampling mode.
    /// </summary>
    /// <param name="pieChart"></param>
    /// <param name="directionsMode"></param>
    /// <param name="samplingMode"></param>
    /// <param name="count"></param>
    /// <param name="offset"></param>
    private void UpdateLookLabels(PieChartElement pieChart, LookDirectionsMode directionsMode, LookMultiplierSampling samplingMode, int count, float offset)
    {
        if (pieChart == null)
        {
            return;
        }

        if (directionsMode != LookDirectionsMode.DiscreteCount)
        {
            pieChart.SetSegmentLabels(null);
            return;
        }

        string prefix = samplingMode == LookMultiplierSampling.ArcConstant ? "Arc" : "Dir";
        pieChart.SetSegmentLabels(BuildDirectionalLabels(count, offset, prefix));
    }

    /// <summary>
    /// Builds a list of label descriptors for directional labels based on the specified count, 
    /// offset, and prefix.
    /// </summary>
    /// <param name="count"></param>
    /// <param name="offset"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    private List<PieChartElement.LabelDescriptor> BuildDirectionalLabels(int count, float offset, string prefix)
    {
        int sliceCount = Mathf.Max(1, count);
        float step = 360f / sliceCount;
        List<PieChartElement.LabelDescriptor> labels = new List<PieChartElement.LabelDescriptor>();

        for (int i = 0; i < sliceCount; i++)
        {
            float angle = Mathf.Repeat(offset + (i * step), 360f);
            PieChartElement.LabelDescriptor descriptor = new PieChartElement.LabelDescriptor
            {
                Angle = angle,
                Text = prefix + " " + (i + 1),
                RadiusOffset = 0f,
                TextColor = DirectionLabelColor,
                UseTextColor = true
            };

            labels.Add(descriptor);
        }

        return labels;
    }

    /// <summary>
    /// This method snaps the provided offset property to the nearest step based on the count of discrete directions,
    /// and applies the change to the serialized object if necessary. This ensures that the offset aligns with the discrete
    /// and symmetrical nature of the look directions when in DiscreteCount mode, 
    /// preventing misalignment between the visual representations.
    /// </summary>
    /// <param name="offsetProperty"></param>
    /// <param name="count"></param>
    private void SnapOffsetToStep(SerializedProperty offsetProperty, int count)
    {
        if (offsetProperty == null)
            return;

        int clampedCount = Mathf.Max(1, count);
        float step = 360f / clampedCount;
        float normalized = Mathf.Repeat(offsetProperty.floatValue, 360f);
        float snapped = Mathf.Round(normalized / step) * step;
        snapped = Mathf.Repeat(snapped, 360f);

        if (Mathf.Abs(snapped - offsetProperty.floatValue) < 0.001f)
            return;

        if (m_SelectedPreset != null)
        {
            Undo.RecordObject(m_SelectedPreset, "Snap Direction Offset");
            m_PresetSerializedObject.Update();
        }

        offsetProperty.floatValue = snapped;
        m_PresetSerializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Updates the specified pie chart with discrete slices, alternating colors, directional labels, and direction
    /// markers based on the given count and offset.
    /// </summary>
    /// <param name="pieChart">The pie chart element to update.</param>
    /// <param name="count">The number of slices to display in the pie chart.</param>
    /// <param name="offset">The angular offset to apply to the starting position of the slices.</param>
    private void UpdateDiscretePieChart(PieChartElement pieChart, int count, float offset)
    {
        int sliceCount = Mathf.Max(1, count);
        float step = 360f / sliceCount;

        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();
        List<float> directionAngles = new List<float>();

        for (int i = 0; i < sliceCount; i++)
        {
            float startAngle = offset - (step * 0.5f) + (i * step);
            Color color = i % 2 == 0 ? SliceColorA : SliceColorB;
            AddSliceByStep(slices, startAngle, step, color);
            directionAngles.Add(Mathf.Repeat(offset + (i * step), 360f));
        }

        pieChart.SetSlices(slices);
        pieChart.SetOverlayFields(null);
        pieChart.SetSegmentLabels(BuildDirectionalLabels(sliceCount, offset, "Dir"));
        pieChart.SetDirectionMarkers(directionAngles, DirectionMarkerColor, ForwardMarkerColor, 0f, true);
    }

    /// <summary>
    /// Updates the pie chart visualization to reflect look direction settings based on the specified mode, count,
    /// offset, and cone parameters.
    /// </summary>
    /// <param name="pieChart">The pie chart element to update.</param>
    /// <param name="mode">The mode determining how look directions are represented.</param>
    /// <param name="count">The number of discrete look directions.</param>
    /// <param name="offset">The angular offset for direction placement.</param>
    /// <param name="frontEnabled">Serialized property indicating if the front cone is enabled.</param>
    /// <param name="frontAngle">Serialized property specifying the angle of the front cone.</param>
    /// <param name="backEnabled">Serialized property indicating if the back cone is enabled.</param>
    /// <param name="backAngle">Serialized property specifying the angle of the back cone.</param>
    /// <param name="leftEnabled">Serialized property indicating if the left cone is enabled.</param>
    /// <param name="leftAngle">Serialized property specifying the angle of the left cone.</param>
    /// <param name="rightEnabled">Serialized property indicating if the right cone is enabled.</param>
    /// <param name="rightAngle">Serialized property specifying the angle of the right cone.</param>
    private void UpdateLookPieChart(PieChartElement pieChart, LookDirectionsMode mode, int count, float offset, SerializedProperty frontEnabled, SerializedProperty frontAngle, SerializedProperty backEnabled, SerializedProperty backAngle, SerializedProperty leftEnabled, SerializedProperty leftAngle, SerializedProperty rightEnabled, SerializedProperty rightAngle)
    {
        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();

        if (mode == LookDirectionsMode.DiscreteCount)
        {
            int sliceCount = Mathf.Max(1, count);
            float step = 360f / sliceCount;

            for (int i = 0; i < sliceCount; i++)
            {
                float startAngle = offset - (step * 0.5f) + (i * step);
                Color color = i % 2 == 0 ? SliceColorA : SliceColorB;
                AddSliceByStep(slices, startAngle, step, color);
            }

            List<float> directionAngles = new List<float>();

            for (int i = 0; i < sliceCount; i++)
            {
                directionAngles.Add(Mathf.Repeat(offset + (i * step), 360f));
            }

            pieChart.SetDirectionMarkers(directionAngles, DirectionMarkerColor, ForwardMarkerColor, 0f, true);
        }
        else if (mode == LookDirectionsMode.Cones)
        {
            AddConeSlices(slices, 0f, frontEnabled, frontAngle, FrontConeColor);
            AddConeSlices(slices, 180f, backEnabled, backAngle, BackConeColor);
            AddConeSlices(slices, 270f, leftEnabled, leftAngle, LeftConeColor);
            AddConeSlices(slices, 90f, rightEnabled, rightAngle, RightConeColor);
            pieChart.SetDirectionMarkers(null, DirectionMarkerColor, ForwardMarkerColor, 0f, false);
        }
        else
        {
            pieChart.SetDirectionMarkers(null, DirectionMarkerColor, ForwardMarkerColor, 0f, false);
        }

        pieChart.SetSlices(slices);
    }

    /// <summary>
    /// Adds a pie slice to the collection representing a cone segment if enabled, using the specified center angle,
    /// angle property, and color.
    /// </summary>
    /// <param name="slices">The list to which the new pie slice will be added.</param>
    /// <param name="centerAngle">The central angle around which the cone slice is positioned.</param>
    /// <param name="enabledProperty">Serialized property indicating whether the cone slice is enabled.</param>
    /// <param name="angleProperty">Serialized property specifying the angle span of the cone slice.</param>
    /// <param name="color">The color to assign to the cone slice.</param>
    private void AddConeSlices(List<PieChartElement.PieSlice> slices, float centerAngle, SerializedProperty enabledProperty, SerializedProperty angleProperty, Color color)
    {
        if (enabledProperty == null || angleProperty == null)
        {
            return;
        }

        if (enabledProperty.boolValue == false)
        {
            return;
        }

        float angle = Mathf.Clamp(angleProperty.floatValue, 1f, 360f);
        float half = angle * 0.5f;
        float start = centerAngle - half;
        float end = centerAngle + half;

        AddSlice(slices, start, end, color);
    }



    /// <summary>
    /// This method adds a pie slice to the provided list of slices based on a starting angle and a step value, 
    /// which determines the size of the slice.
    /// </summary>
    /// <param name="slices"></param>
    /// <param name="startAngle"></param>
    /// <param name="step"></param>
    /// <param name="color"></param>
    private void AddSliceByStep(List<PieChartElement.PieSlice> slices, float startAngle, float step, Color color)
    {
        if (step >= 359.99f)
        {
            AddSlice(slices, 0f, 360f, color);
            return;
        }

        float endAngle = startAngle + step;
        AddSlice(slices, startAngle, endAngle, color);
    }

    /// <summary>
    /// Adds a pie slice to the provided list of slices with the specified start and end angles, and color.
    /// </summary>
    /// <param name="slices"></param>
    /// <param name="startAngle"></param>
    /// <param name="endAngle"></param>
    /// <param name="color"></param>
    private void AddSlice(List<PieChartElement.PieSlice> slices, float startAngle, float endAngle, Color color)
    {
        PieChartElement.PieSlice slice = new PieChartElement.PieSlice
        {
            StartAngle = startAngle,
            EndAngle = endAngle,
            MidAngle = startAngle + ((endAngle - startAngle) * 0.5f),
            Color = color
        };

        slices.Add(slice);
    }

    /// <summary>
    /// Ensures that the provided serialized property, which is expected to be an array, 
    /// has the specified size.
    /// </summary>
    /// <param name="arrayProperty"></param>
    /// <param name="size"></param>
    private void EnsureArraySize(SerializedProperty arrayProperty, int size)
    {
        if (arrayProperty == null)
            return;

        if (arrayProperty.arraySize == size)
            return;

        if (m_SelectedPreset != null)
        {
            Undo.RecordObject(m_SelectedPreset, "Resize Direction Multipliers");
            m_PresetSerializedObject.Update();
        }

        if (arrayProperty.arraySize < size)
        {
            int startIndex = arrayProperty.arraySize;
            arrayProperty.arraySize = size;

            for (int i = startIndex; i < size; i++)
            {
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(i);
                if (element != null)
                    element.floatValue = 1f;
            }

            m_PresetSerializedObject.ApplyModifiedProperties();
            return;
        }

        if (arrayProperty.arraySize > size)
        {
            arrayProperty.arraySize = size;
            m_PresetSerializedObject.ApplyModifiedProperties();
        }
    }
    #endregion

    #endregion

    #region Nested Types
    private enum SectionType
    {
        Metadata = 0,
        Movement = 1,
        Look = 2,
        Shooting = 3,
        Camera = 4
    }
    #endregion
}
