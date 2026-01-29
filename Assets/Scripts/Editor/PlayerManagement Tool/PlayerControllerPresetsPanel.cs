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
    private const float MultiplierOverlayRadiusOffset = 12f;

    // Colors for pie chart segments and overlays
    private static readonly Color SliceColorA = new Color(0.2f, 0.6f, 0.9f, 0.75f);
    private static readonly Color SliceColorB = new Color(0.1f, 0.4f, 0.7f, 0.75f);
    private static readonly Color FrontConeColor = new Color(0.2f, 0.8f, 0.4f, 0.7f);
    private static readonly Color BackConeColor = new Color(0.8f, 0.3f, 0.3f, 0.7f);
    private static readonly Color LeftConeColor = new Color(0.3f, 0.6f, 0.9f, 0.7f);
    private static readonly Color RightConeColor = new Color(0.9f, 0.7f, 0.2f, 0.7f);
    private static readonly Color DirectionMarkerColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);
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
    /// <summary>
    /// Creates and returns a left-aligned label with a left margin for use as a preset item.
    /// </summary>
    /// <returns>A VisualElement representing the styled label.</returns>
    private VisualElement MakePresetItem()
    {
        Label label = new Label();
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.marginLeft = 4f;
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
            return;
        }

        PlayerControllerPreset preset = m_FilteredPresets[index];

        if (preset == null)
        {
            label.text = "<Missing Preset>";
            label.tooltip = string.Empty;
            return;
        }

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

        Undo.RecordObject(m_Library, "Add Preset");
        m_Library.AddPreset(newPreset);
        EditorUtility.SetDirty(m_Library);
        AssetDatabase.SaveAssets();

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
        if (m_SelectedPreset == null)
            return;

        PlayerControllerPreset duplicatedPreset = ScriptableObject.CreateInstance<PlayerControllerPreset>();
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

        Undo.RecordObject(m_Library, "Duplicate Preset");
        m_Library.AddPreset(duplicatedPreset);
        EditorUtility.SetDirty(m_Library);
        AssetDatabase.SaveAssets();

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
        if (m_SelectedPreset == null)
            return;

        bool confirmed = EditorUtility.DisplayDialog("Delete Preset", "Delete the selected preset asset?", "Delete", "Cancel");

        if (confirmed == false)
            return;

        string assetPath = AssetDatabase.GetAssetPath(m_SelectedPreset);

        Undo.RecordObject(m_Library, "Delete Preset");
        m_Library.RemovePreset(m_SelectedPreset);
        EditorUtility.SetDirty(m_Library);
        AssetDatabase.SaveAssets();

        if (string.IsNullOrWhiteSpace(assetPath) == false)
            AssetDatabase.DeleteAsset(assetPath);

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

        if (m_SelectedPreset == null)
        {
            Label label = new Label("Select or create a preset to edit.");
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            m_DetailsRoot.Add(label);
            return;
        }

        m_PresetSerializedObject = new SerializedObject(m_SelectedPreset);

        BuildMetadataSection();
        BuildMovementSection();
        BuildLookSection();
        BuildCameraSection();
    }

    /// <summary>
    /// Constructs and adds UI elements for displaying and editing preset metadata, including name, version,
    /// description, and ID, to the details root container.
    /// </summary>
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

    /// <summary>
    /// Generates a new unique identifier for the selected preset and updates its serialized property.
    /// </summary>
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

    /// <summary>
    /// Renames the selected preset asset and updates its name, saving changes and refreshing the preset list.
    /// </summary>
    /// <param name="newName">The new name to assign to the selected preset.</param>
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
    #endregion

    #region Movement Section
    /// <summary>
    /// Constructs and configures the UI section for movement settings, including direction mode, direction count,
    /// offset, movement reference, input bindings, and related value fields.
    /// </summary>
    private void BuildMovementSection()
    {
        Foldout foldout = new Foldout();
        foldout.text = "Movement Settings";
        foldout.value = true;
        foldout.style.marginTop = 8f;
        m_DetailsRoot.Add(foldout);

        SerializedProperty movementProperty = m_PresetSerializedObject.FindProperty("m_MovementSettings");
        SerializedProperty modeProperty = movementProperty.FindPropertyRelative("m_DirectionsMode");
        SerializedProperty countProperty = movementProperty.FindPropertyRelative("m_DiscreteDirectionCount");
        SerializedProperty offsetProperty = movementProperty.FindPropertyRelative("m_DirectionOffsetDegrees");
        SerializedProperty referenceProperty = movementProperty.FindPropertyRelative("m_MovementReference");
        SerializedProperty valuesProperty = movementProperty.FindPropertyRelative("m_Values");

        EnumField modeField = new EnumField("Allowed Directions");
        modeField.BindProperty(modeProperty);
        foldout.Add(modeField);

        VisualElement discreteContainer = new VisualElement();
        discreteContainer.style.marginLeft = 8f;

        IntegerField countField = new IntegerField("Direction Count");
        countField.BindProperty(countProperty);
        discreteContainer.Add(countField);

        FloatField offsetField = new FloatField("Direction Offset");
        offsetField.BindProperty(offsetProperty);
        discreteContainer.Add(offsetField);

        PieChartElement pieChart = new PieChartElement();
        Slider movementZoomSlider = CreatePieZoomSlider(pieChart);
        discreteContainer.Add(pieChart);
        discreteContainer.Add(movementZoomSlider);

        foldout.Add(discreteContainer);

        EnumField referenceField = new EnumField("Movement Reference");
        referenceField.BindProperty(referenceProperty);
        foldout.Add(referenceField);

        SerializedProperty moveActionProperty = m_PresetSerializedObject.FindProperty("m_MoveActionId");
        SerializedProperty overridesProperty = m_PresetSerializedObject.FindProperty("m_InputOverrides");
        EnsureDefaultActionId(moveActionProperty, "Move");

        Foldout bindingsFoldout = BuildBindingsFoldout(m_InputAsset, m_PresetSerializedObject, moveActionProperty, overridesProperty, InputActionBindingOverridesElement.QuickBindingMode.Movement);
        foldout.Add(bindingsFoldout);

        Foldout valuesFoldout = BuildValuesFoldout(valuesProperty, new string[]
        {
            "m_BaseSpeed",
            "m_MaxSpeed",
            "m_Acceleration",
            "m_Deceleration",
            "m_InputDeadZone"
        });
        foldout.Add(valuesFoldout);

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

        offsetField.RegisterValueChangedCallback(evt =>
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
        Foldout foldout = new Foldout();
        foldout.text = "Look Settings";
        foldout.value = true;
        foldout.style.marginTop = 8f;
        m_DetailsRoot.Add(foldout);

        SerializedProperty lookProperty = m_PresetSerializedObject.FindProperty("m_LookSettings");
        SerializedProperty directionsModeProperty = lookProperty.FindPropertyRelative("m_DirectionsMode");
        SerializedProperty countProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionCount");
        SerializedProperty offsetProperty = lookProperty.FindPropertyRelative("m_DirectionOffsetDegrees");
        SerializedProperty referenceProperty = lookProperty.FindPropertyRelative("m_LookReference");
        SerializedProperty rotationModeProperty = lookProperty.FindPropertyRelative("m_RotationMode");
        SerializedProperty rotationSpeedProperty = lookProperty.FindPropertyRelative("m_RotationSpeed");
        SerializedProperty samplingProperty = lookProperty.FindPropertyRelative("m_MultiplierSampling");
        SerializedProperty maxSpeedMultipliersProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionMaxSpeedMultipliers");
        SerializedProperty accelerationMultipliersProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionAccelerationMultipliers");

        SerializedProperty frontEnabledProperty = lookProperty.FindPropertyRelative("m_FrontConeEnabled");
        SerializedProperty frontAngleProperty = lookProperty.FindPropertyRelative("m_FrontConeAngle");
        SerializedProperty frontMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_FrontConeMaxSpeedMultiplier");
        SerializedProperty frontAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_FrontConeAccelerationMultiplier");

        SerializedProperty backEnabledProperty = lookProperty.FindPropertyRelative("m_BackConeEnabled");
        SerializedProperty backAngleProperty = lookProperty.FindPropertyRelative("m_BackConeAngle");
        SerializedProperty backMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_BackConeMaxSpeedMultiplier");
        SerializedProperty backAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_BackConeAccelerationMultiplier");

        SerializedProperty leftEnabledProperty = lookProperty.FindPropertyRelative("m_LeftConeEnabled");
        SerializedProperty leftAngleProperty = lookProperty.FindPropertyRelative("m_LeftConeAngle");
        SerializedProperty leftMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_LeftConeMaxSpeedMultiplier");
        SerializedProperty leftAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_LeftConeAccelerationMultiplier");

        SerializedProperty rightEnabledProperty = lookProperty.FindPropertyRelative("m_RightConeEnabled");
        SerializedProperty rightAngleProperty = lookProperty.FindPropertyRelative("m_RightConeAngle");
        SerializedProperty rightMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_RightConeMaxSpeedMultiplier");
        SerializedProperty rightAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_RightConeAccelerationMultiplier");

        EnumField directionsModeField = new EnumField("Allowed Directions");
        directionsModeField.BindProperty(directionsModeProperty);
        foldout.Add(directionsModeField);

        VisualElement discreteContainer = new VisualElement();
        discreteContainer.style.marginLeft = 8f;

        IntegerField countField = new IntegerField("Direction Count");
        countField.BindProperty(countProperty);
        discreteContainer.Add(countField);

        FloatField offsetField = new FloatField("Direction Offset");
        offsetField.BindProperty(offsetProperty);
        discreteContainer.Add(offsetField);

        VisualElement conesContainer = new VisualElement();
        conesContainer.style.marginLeft = 8f;
        conesContainer.style.marginTop = 4f;

        List<Toggle> coneToggles = new List<Toggle>();
        List<FloatField> coneAngleFields = new List<FloatField>();

        conesContainer.Add(BuildConeRow("Front", frontEnabledProperty, frontAngleProperty, coneToggles, coneAngleFields));
        conesContainer.Add(BuildConeRow("Back", backEnabledProperty, backAngleProperty, coneToggles, coneAngleFields));
        conesContainer.Add(BuildConeRow("Left", leftEnabledProperty, leftAngleProperty, coneToggles, coneAngleFields));
        conesContainer.Add(BuildConeRow("Right", rightEnabledProperty, rightAngleProperty, coneToggles, coneAngleFields));

        PieChartElement pieChart = new PieChartElement();
        VisualElement multiplierLegend = BuildMultiplierLegend();
        Slider lookZoomSlider = CreatePieZoomSlider(pieChart);

        foldout.Add(discreteContainer);
        foldout.Add(conesContainer);
        foldout.Add(multiplierLegend);
        foldout.Add(pieChart);
        foldout.Add(lookZoomSlider);

        EnumField referenceField = new EnumField("Look Reference");
        referenceField.BindProperty(referenceProperty);
        foldout.Add(referenceField);

        EnumField rotationModeField = new EnumField("Rotation Mode");
        rotationModeField.BindProperty(rotationModeProperty);
        foldout.Add(rotationModeField);

        FloatField rotationSpeedField = new FloatField("Rotation Speed");
        rotationSpeedField.BindProperty(rotationSpeedProperty);
        foldout.Add(rotationSpeedField);

        EnumField samplingField = new EnumField("Multiplier Sampling");
        samplingField.BindProperty(samplingProperty);
        foldout.Add(samplingField);

        SerializedProperty lookActionProperty = m_PresetSerializedObject.FindProperty("m_LookActionId");
        SerializedProperty overridesProperty = m_PresetSerializedObject.FindProperty("m_InputOverrides");
        EnsureDefaultActionId(lookActionProperty, "Look");

        Foldout bindingsFoldout = BuildBindingsFoldout(m_InputAsset, m_PresetSerializedObject, lookActionProperty, overridesProperty, InputActionBindingOverridesElement.QuickBindingMode.Look);
        foldout.Add(bindingsFoldout);

        SerializedProperty valuesProperty = lookProperty.FindPropertyRelative("m_Values");
        Foldout valuesFoldout = BuildValuesFoldout(valuesProperty, new string[]
        {
            "m_RotationDamping",
            "m_RotationMaxSpeed",
            "m_RotationDeadZone"
        });
        foldout.Add(valuesFoldout);

        Action updateView = () =>
        {
            LookDirectionsMode directionsMode = (LookDirectionsMode)directionsModeProperty.enumValueIndex;
            RotationMode rotationMode = (RotationMode)rotationModeProperty.enumValueIndex;
            LookMultiplierSampling samplingMode = samplingProperty != null ? (LookMultiplierSampling)samplingProperty.enumValueIndex : LookMultiplierSampling.DirectionalBlend;

            bool isDiscrete = directionsMode == LookDirectionsMode.DiscreteCount;
            bool isCones = directionsMode == LookDirectionsMode.Cones;

            discreteContainer.style.display = isDiscrete ? DisplayStyle.Flex : DisplayStyle.None;
            conesContainer.style.display = isCones ? DisplayStyle.Flex : DisplayStyle.None;
            pieChart.style.display = directionsMode == LookDirectionsMode.AllDirections ? DisplayStyle.None : DisplayStyle.Flex;
            multiplierLegend.style.display = directionsMode == LookDirectionsMode.AllDirections ? DisplayStyle.None : DisplayStyle.Flex;
            lookZoomSlider.style.display = directionsMode == LookDirectionsMode.AllDirections ? DisplayStyle.None : DisplayStyle.Flex;
            rotationSpeedField.style.display = rotationMode == RotationMode.Continuous ? DisplayStyle.Flex : DisplayStyle.None;
            samplingField.style.display = isDiscrete ? DisplayStyle.Flex : DisplayStyle.None;

            if (isDiscrete)
                SnapOffsetToStep(offsetProperty, countProperty.intValue);

            UpdateLookPieChart(pieChart, directionsMode, countProperty.intValue, offsetProperty.floatValue, frontEnabledProperty, frontAngleProperty, backEnabledProperty, backAngleProperty, leftEnabledProperty, leftAngleProperty, rightEnabledProperty, rightAngleProperty);
            UpdateLookOverlayFields(pieChart, directionsMode, samplingMode, countProperty.intValue, offsetProperty.floatValue, maxSpeedMultipliersProperty, accelerationMultipliersProperty, frontEnabledProperty, frontMaxSpeedMultiplierProperty, frontAccelerationMultiplierProperty, backEnabledProperty, backMaxSpeedMultiplierProperty, backAccelerationMultiplierProperty, leftEnabledProperty, leftMaxSpeedMultiplierProperty, leftAccelerationMultiplierProperty, rightEnabledProperty, rightMaxSpeedMultiplierProperty, rightAccelerationMultiplierProperty);
        };

        directionsModeField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        countField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        offsetField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        rotationModeField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        samplingField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        for (int i = 0; i < coneToggles.Count; i++)
        {
            Toggle toggle = coneToggles[i];
            toggle.RegisterValueChangedCallback(evt =>
            {
                updateView();
            });
        }

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
    private VisualElement BuildConeRow(string label, SerializedProperty enabledProperty, SerializedProperty angleProperty, List<Toggle> toggles, List<FloatField> angleFields)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        Toggle enabledToggle = new Toggle(label);
        enabledToggle.style.minWidth = 120f;
        enabledToggle.BindProperty(enabledProperty);
        row.Add(enabledToggle);

        FloatField angleField = new FloatField("Angle");
        angleField.style.flexGrow = 1f;
        angleField.BindProperty(angleProperty);
        row.Add(angleField);

        if (toggles != null)
            toggles.Add(enabledToggle);

        if (angleFields != null)
            angleFields.Add(angleField);

        return row;
    }
    #endregion

    #region Camera Section
    /// <summary>
    /// Constructs and configures the camera settings section of the UI, including controls for camera behavior, follow
    /// offset, room anchor, and related values.
    /// </summary>
    private void BuildCameraSection()
    {
        Foldout foldout = new Foldout();
        foldout.text = "Camera Settings";
        foldout.value = true;
        foldout.style.marginTop = 8f;
        m_DetailsRoot.Add(foldout);

        SerializedProperty cameraProperty = m_PresetSerializedObject.FindProperty("m_CameraSettings");
        SerializedProperty behaviorProperty = cameraProperty.FindPropertyRelative("m_Behavior");
        SerializedProperty offsetProperty = cameraProperty.FindPropertyRelative("m_FollowOffset");
        SerializedProperty anchorProperty = cameraProperty.FindPropertyRelative("m_RoomAnchor");
        SerializedProperty valuesProperty = cameraProperty.FindPropertyRelative("m_Values");

        EnumField behaviorField = new EnumField("Camera Behavior");
        behaviorField.BindProperty(behaviorProperty);
        foldout.Add(behaviorField);

        Vector3Field offsetField = new Vector3Field("Follow Offset");
        offsetField.BindProperty(offsetProperty);
        foldout.Add(offsetField);

        ObjectField anchorField = new ObjectField("Room Anchor");
        anchorField.objectType = typeof(Transform);
        anchorField.BindProperty(anchorProperty);
        foldout.Add(anchorField);

        Foldout valuesFoldout = BuildValuesFoldout(valuesProperty, new string[]
        {
            "m_FollowSpeed",
            "m_CameraLag",
            "m_Damping",
            "m_MaxFollowDistance",
            "m_DeadZoneRadius"
        });
        foldout.Add(valuesFoldout);

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

    private Foldout BuildBindingsFoldout(InputActionAsset inputAsset, SerializedObject presetSerializedObject, SerializedProperty actionIdProperty, SerializedProperty overridesProperty, InputActionBindingOverridesElement.QuickBindingMode mode)
    {
        Foldout foldout = new Foldout();
        foldout.text = "Bindings";
        foldout.value = true;

        InputActionBindingOverridesElement bindingsElement = new InputActionBindingOverridesElement(inputAsset, presetSerializedObject, actionIdProperty, overridesProperty, mode);
        foldout.Add(bindingsElement);

        return foldout;
    }

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

    private VisualElement BuildMultiplierLegend()
    {
        VisualElement container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.marginTop = 4f;
        container.style.marginBottom = 2f;

        container.Add(BuildLegendItem(MaxSpeedMultiplierColor, "Max Speed"));
        container.Add(BuildLegendItem(AccelerationMultiplierColor, "Acceleration"));

        return container;
    }

    private VisualElement BuildLegendItem(Color color, string label)
    {
        VisualElement item = new VisualElement();
        item.style.flexDirection = FlexDirection.Row;
        item.style.alignItems = Align.Center;
        item.style.marginRight = 12f;

        VisualElement swatch = new VisualElement();
        swatch.style.width = 10f;
        swatch.style.height = 10f;
        swatch.style.backgroundColor = color;
        swatch.style.marginRight = 4f;
        item.Add(swatch);

        Label text = new Label(label);
        text.style.fontSize = 10f;
        item.Add(text);

        return item;
    }

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
            AddWrappedSliceByStep(slices, startAngle, step, color);
            directionAngles.Add(Mathf.Repeat(offset + (i * step), 360f));
        }

        pieChart.SetSlices(slices);
        pieChart.SetOverlayFields(null);
        pieChart.SetDirectionMarkers(directionAngles, DirectionMarkerColor, ForwardMarkerColor, 0f, true);
    }

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
                AddWrappedSliceByStep(slices, startAngle, step, color);
            }

            List<float> directionAngles = new List<float>();

            for (int i = 0; i < sliceCount; i++)
                directionAngles.Add(Mathf.Repeat(offset + (i * step), 360f));

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
            pieChart.SetDirectionMarkers(null, DirectionMarkerColor, ForwardMarkerColor, 0f, false);

        pieChart.SetSlices(slices);
    }

    private void AddConeSlices(List<PieChartElement.PieSlice> slices, float centerAngle, SerializedProperty enabledProperty, SerializedProperty angleProperty, Color color)
    {
        if (enabledProperty == null || angleProperty == null)
            return;

        if (enabledProperty.boolValue == false)
            return;

        float angle = Mathf.Clamp(angleProperty.floatValue, 1f, 360f);
        float half = angle * 0.5f;
        float start = centerAngle - half;
        float end = centerAngle + half;

        if (start < 0f)
        {
            AddSlice(slices, start + 360f, 360f, color);
            AddSlice(slices, 0f, end, color);
            return;
        }

        if (end > 360f)
        {
            AddSlice(slices, start, 360f, color);
            AddSlice(slices, 0f, end - 360f, color);
            return;
        }

        AddSlice(slices, start, end, color);
    }

    private void AddWrappedSliceByStep(List<PieChartElement.PieSlice> slices, float startAngle, float step, Color color)
    {
        if (step >= 359.99f)
        {
            AddSlice(slices, 0f, 360f, color);
            return;
        }

        float normalizedStart = Mathf.Repeat(startAngle, 360f);
        float normalizedEnd = normalizedStart + step;

        if (normalizedEnd > 360f)
        {
            AddSlice(slices, normalizedStart, 360f, color);
            AddSlice(slices, 0f, normalizedEnd - 360f, color);
            return;
        }

        AddSlice(slices, normalizedStart, normalizedEnd, color);
    }

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

    private void UpdateLookOverlayFields(PieChartElement pieChart, LookDirectionsMode mode, LookMultiplierSampling samplingMode, int count, float offset, SerializedProperty maxSpeedMultipliersProperty, SerializedProperty accelerationMultipliersProperty, SerializedProperty frontEnabled, SerializedProperty frontMaxSpeedMultiplier, SerializedProperty frontAccelerationMultiplier, SerializedProperty backEnabled, SerializedProperty backMaxSpeedMultiplier, SerializedProperty backAccelerationMultiplier, SerializedProperty leftEnabled, SerializedProperty leftMaxSpeedMultiplier, SerializedProperty leftAccelerationMultiplier, SerializedProperty rightEnabled, SerializedProperty rightMaxSpeedMultiplier, SerializedProperty rightAccelerationMultiplier)
    {
        if (mode == LookDirectionsMode.AllDirections)
        {
            pieChart.SetOverlayFields(null);
            return;
        }

        List<PieChartElement.OverlayDescriptor> descriptors = new List<PieChartElement.OverlayDescriptor>();

        if (mode == LookDirectionsMode.DiscreteCount)
        {
            if (maxSpeedMultipliersProperty == null || accelerationMultipliersProperty == null)
            {
                pieChart.SetOverlayFields(null);
                return;
            }

            int sliceCount = Mathf.Max(1, count);
            EnsureArraySize(maxSpeedMultipliersProperty, sliceCount);
            EnsureArraySize(accelerationMultipliersProperty, sliceCount);

            float step = 360f / sliceCount;
            string maxSpeedTooltip = samplingMode == LookMultiplierSampling.ArcConstant
                ? "Max speed multiplier for this look arc (constant across the arc)."
                : "Max speed multiplier for this look direction (blended by alignment).";
            string accelerationTooltip = samplingMode == LookMultiplierSampling.ArcConstant
                ? "Acceleration multiplier for this look arc (constant across the arc)."
                : "Acceleration multiplier for this look direction (blended by alignment).";

            for (int i = 0; i < sliceCount; i++)
            {
                float angle = offset + (i * step);
                SerializedProperty maxSpeedElement = maxSpeedMultipliersProperty.GetArrayElementAtIndex(i);
                SerializedProperty accelerationElement = accelerationMultipliersProperty.GetArrayElementAtIndex(i);

                descriptors.Add(new PieChartElement.OverlayDescriptor
                {
                    Angle = angle,
                    Property = maxSpeedElement,
                    Tooltip = maxSpeedTooltip,
                    RadiusOffset = -MultiplierOverlayRadiusOffset,
                    FieldColor = MaxSpeedMultiplierColor,
                    UseFieldColor = true,
                    DisplayAsPercent = true
                });

                descriptors.Add(new PieChartElement.OverlayDescriptor
                {
                    Angle = angle,
                    Property = accelerationElement,
                    Tooltip = accelerationTooltip,
                    RadiusOffset = MultiplierOverlayRadiusOffset,
                    FieldColor = AccelerationMultiplierColor,
                    UseFieldColor = true,
                    DisplayAsPercent = true
                });
            }
        }
        else if (mode == LookDirectionsMode.Cones)
        {
            AddConeDescriptors(descriptors, 0f, frontEnabled, frontMaxSpeedMultiplier, frontAccelerationMultiplier, "Max speed multiplier while look direction is inside the front cone.", "Acceleration multiplier while look direction is inside the front cone.");
            AddConeDescriptors(descriptors, 180f, backEnabled, backMaxSpeedMultiplier, backAccelerationMultiplier, "Max speed multiplier while look direction is inside the back cone.", "Acceleration multiplier while look direction is inside the back cone.");
            AddConeDescriptors(descriptors, 270f, leftEnabled, leftMaxSpeedMultiplier, leftAccelerationMultiplier, "Max speed multiplier while look direction is inside the left cone.", "Acceleration multiplier while look direction is inside the left cone.");
            AddConeDescriptors(descriptors, 90f, rightEnabled, rightMaxSpeedMultiplier, rightAccelerationMultiplier, "Max speed multiplier while look direction is inside the right cone.", "Acceleration multiplier while look direction is inside the right cone.");
        }

        pieChart.SetOverlayFields(descriptors);
    }

    private void AddConeDescriptors(List<PieChartElement.OverlayDescriptor> descriptors, float angle, SerializedProperty enabledProperty, SerializedProperty maxSpeedMultiplier, SerializedProperty accelerationMultiplier, string maxSpeedTooltip, string accelerationTooltip)
    {
        if (enabledProperty == null)
            return;

        if (enabledProperty.boolValue == false)
            return;

        if (maxSpeedMultiplier != null)
        {
            descriptors.Add(new PieChartElement.OverlayDescriptor
            {
                Angle = angle,
                Property = maxSpeedMultiplier,
                Tooltip = maxSpeedTooltip,
                RadiusOffset = -MultiplierOverlayRadiusOffset,
                FieldColor = MaxSpeedMultiplierColor,
                UseFieldColor = true,
                DisplayAsPercent = true
            });
        }

        if (accelerationMultiplier == null)
            return;

        descriptors.Add(new PieChartElement.OverlayDescriptor
        {
            Angle = angle,
            Property = accelerationMultiplier,
            Tooltip = accelerationTooltip,
            RadiusOffset = MultiplierOverlayRadiusOffset,
            FieldColor = AccelerationMultiplierColor,
            UseFieldColor = true,
            DisplayAsPercent = true
        });
    }

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
}
