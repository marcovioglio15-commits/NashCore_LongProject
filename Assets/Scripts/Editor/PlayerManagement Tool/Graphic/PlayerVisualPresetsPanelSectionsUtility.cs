using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds detail sections and subsection tabs for player visual preset panels.
/// </summary>
internal static class PlayerVisualPresetsPanelSectionsUtility
{
    #region Constants
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    public static void BuildMetadataSection(PlayerVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Preset Details");

        if (sectionContainer == null)
            return;

        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        if (presetSerializedObject == null)
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
            panel.HandlePresetNameChanged(evt.newValue);
        });
        sectionContainer.Add(nameField);

        TextField versionField = new TextField("Version");
        versionField.isDelayed = true;
        versionField.BindProperty(versionProperty);
        versionField.RegisterValueChangedCallback(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        sectionContainer.Add(versionField);

        TextField descriptionField = new TextField("Description");
        descriptionField.multiline = true;
        descriptionField.isDelayed = true;
        descriptionField.style.height = 60f;
        descriptionField.BindProperty(descriptionProperty);
        descriptionField.RegisterValueChangedCallback(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
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

        Button regenerateButton = new Button(panel.RegeneratePresetId);
        regenerateButton.text = "Regenerate";
        regenerateButton.style.marginLeft = 6f;
        idRow.Add(regenerateButton);
        sectionContainer.Add(idRow);
    }

    public static void BuildVisualSection(PlayerVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement sectionContainer = CreateDetailsSectionContainer(panel, "Visual");

        if (sectionContainer == null)
            return;

        panel.VisualSubSectionTabs.Clear();

        VisualElement tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.flexWrap = Wrap.Wrap;
        tabBar.style.marginBottom = 6f;
        tabBar.style.paddingTop = 4f;
        tabBar.style.paddingBottom = 4f;
        tabBar.style.paddingLeft = 2f;
        panel.VisualSubSectionTabBar = tabBar;

        VisualElement contentHost = new VisualElement();
        contentHost.style.flexDirection = FlexDirection.Column;
        contentHost.style.flexGrow = 1f;
        panel.VisualSubSectionContentHost = contentHost;

        sectionContainer.Add(tabBar);
        sectionContainer.Add(contentHost);

        AddVisualSubSectionTab(panel,
                               PlayerVisualPresetsPanel.VisualSubSectionType.RuntimeBridge,
                               "Runtime Bridge",
                               BuildRuntimeBridgeSubSection(panel));
        AddVisualSubSectionTab(panel,
                               PlayerVisualPresetsPanel.VisualSubSectionType.DamageFeedback,
                               "Damage Feedback",
                               BuildDamageFeedbackSubSection(panel));
        AddVisualSubSectionTab(panel,
                               PlayerVisualPresetsPanel.VisualSubSectionType.PowerUpVfx,
                               "Power-Ups VFX",
                               BuildPowerUpVfxSubSection(panel));

        if (!panel.VisualSubSectionTabs.ContainsKey(panel.ActiveVisualSubSection))
            panel.ActiveVisualSubSection = PlayerVisualPresetsPanel.VisualSubSectionType.RuntimeBridge;

        panel.SetActiveVisualSubSection(panel.ActiveVisualSubSection);
    }

    public static void ShowActiveVisualSubSection(PlayerVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement contentHost = panel.VisualSubSectionContentHost;

        if (contentHost == null)
            return;

        PlayerVisualPresetsPanel.VisualSubSectionTabEntry tabEntry;

        if (!panel.VisualSubSectionTabs.TryGetValue(panel.ActiveVisualSubSection, out tabEntry))
            return;

        if (tabEntry == null || tabEntry.Content == null)
            return;

        contentHost.Clear();
        contentHost.Add(tabEntry.Content);
        UpdateVisualSubSectionTabStyles(panel);
    }
    #endregion

    #region Private Methods
    private static VisualElement CreateDetailsSectionContainer(PlayerVisualPresetsPanel panel, string sectionTitle)
    {
        if (panel == null)
            return null;

        VisualElement detailsSectionContentRoot = panel.DetailsSectionContentRoot;

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

    private static VisualElement CreateSubSectionContainer(string sectionTitle)
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 4f;

        Label header = new Label(sectionTitle + " Settings");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 4f;
        container.Add(header);
        return container;
    }

    private static void AddPropertyField(PlayerVisualPresetsPanel panel,
                                         VisualElement target,
                                         SerializedProperty property,
                                         string label,
                                         string tooltip)
    {
        if (panel == null)
            return;

        if (target == null || property == null)
            return;

        PropertyField propertyField = new PropertyField(property, label);
        propertyField.BindProperty(property);
        propertyField.tooltip = tooltip;
        propertyField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            PlayerManagementDraftSession.MarkDirty();
            panel.RefreshPresetList();
        });
        target.Add(propertyField);
    }

    private static void AddVisualSubSectionTab(PlayerVisualPresetsPanel panel,
                                               PlayerVisualPresetsPanel.VisualSubSectionType subSectionType,
                                               string tabLabel,
                                               VisualElement content)
    {
        if (panel == null)
            return;

        if (panel.VisualSubSectionTabBar == null || content == null)
            return;

        VisualElement tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.alignItems = Align.Center;
        tabContainer.style.marginRight = 6f;
        tabContainer.style.marginBottom = 4f;

        Button tabButton = new Button(() => panel.SetActiveVisualSubSection(subSectionType));
        tabButton.text = tabLabel;
        tabButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        tabContainer.Add(tabButton);
        panel.VisualSubSectionTabBar.Add(tabContainer);

        PlayerVisualPresetsPanel.VisualSubSectionTabEntry tabEntry = new PlayerVisualPresetsPanel.VisualSubSectionTabEntry();
        tabEntry.TabContainer = tabContainer;
        tabEntry.TabButton = tabButton;
        tabEntry.Content = content;
        panel.VisualSubSectionTabs[subSectionType] = tabEntry;
    }

    private static void UpdateVisualSubSectionTabStyles(PlayerVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<PlayerVisualPresetsPanel.VisualSubSectionType, PlayerVisualPresetsPanel.VisualSubSectionTabEntry> tabEntry in panel.VisualSubSectionTabs)
        {
            if (tabEntry.Value == null || tabEntry.Value.TabButton == null)
                continue;

            bool isActive = tabEntry.Key == panel.ActiveVisualSubSection;
            tabEntry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            tabEntry.Value.TabButton.style.backgroundColor = isActive ? ActiveTabColor : Color.clear;
        }
    }

    private static VisualElement BuildRuntimeBridgeSubSection(PlayerVisualPresetsPanel panel)
    {
        VisualElement container = CreateSubSectionContainer("Runtime Bridge");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("runtimeVisualBridgePrefab"),
                         "Runtime Visual Bridge Prefab",
                         "Visual-only prefab instantiated at runtime when no valid companion Animator exists.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("spawnRuntimeVisualBridgeWhenAnimatorMissing"),
                         "Spawn When Animator Missing",
                         "When enabled, runtime visual spawning only happens if a valid Animator companion is not already present.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("runtimeVisualBridgeSyncRotation"),
                         "Sync Rotation",
                         "When enabled, the runtime visual bridge follows the ECS player rotation.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("runtimeVisualBridgeOffset"),
                         "Runtime Visual Bridge Offset",
                         "Local-space offset applied to the runtime visual bridge relative to the ECS player transform.");
        return container;
    }

    private static VisualElement BuildDamageFeedbackSubSection(PlayerVisualPresetsPanel panel)
    {
        VisualElement container = CreateSubSectionContainer("Damage Feedback");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("damageFlashColor"),
                         "Flash Color",
                         "Tint color applied during the brief damage flash after a valid hit.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("damageFlashDurationSeconds"),
                         "Flash Duration Seconds",
                         "Flash duration in seconds. Use very small values for a 1-3 frame reaction.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("damageFlashMaximumBlend"),
                         "Flash Maximum Blend",
                         "Maximum overlay strength reached immediately after a valid hit.");
        return container;
    }

    private static VisualElement BuildPowerUpVfxSubSection(PlayerVisualPresetsPanel panel)
    {
        VisualElement container = CreateSubSectionContainer("Power-Ups VFX");
        SerializedObject presetSerializedObject = panel.PresetSerializedObject;

        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("elementalTrailAttachedVfxPrefab"),
                         "Elemental Trail Attached VFX Prefab",
                         "Optional attached VFX prefab activated while Elemental Trail passive is enabled.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("elementalTrailAttachedVfxScaleMultiplier"),
                         "Elemental Trail Attached VFX Scale Multiplier",
                         "Scale multiplier applied to the attached Elemental Trail VFX instance.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("maxIdenticalOneShotVfxPerCell"),
                         "Max Identical One-Shot VFX Per Cell",
                         "Maximum number of identical one-shot VFX allowed in the same spatial cell. Set 0 to disable this cap.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("oneShotVfxCellSize"),
                         "One-Shot VFX Cell Size",
                         "Cell size in meters used by the one-shot VFX per-cell cap.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("maxAttachedElementalVfxPerTarget"),
                         "Max Attached Elemental VFX Per Target",
                         "Maximum number of identical attached elemental VFX allowed on the same target. Set 0 to disable this cap.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("maxActiveOneShotPowerUpVfx"),
                         "Max Active One-Shot Power-Up VFX",
                         "Maximum number of active one-shot power-up VFX managed by one player. Set 0 to disable this cap.");
        AddPropertyField(panel,
                         container,
                         presetSerializedObject.FindProperty("refreshAttachedElementalVfxLifetimeOnCapHit"),
                         "Refresh Attached Elemental VFX Lifetime On Cap Hit",
                         "When enabled, hitting the attached-target cap refreshes lifetime of the existing VFX.");
        return container;
    }
    #endregion

    #endregion
}
