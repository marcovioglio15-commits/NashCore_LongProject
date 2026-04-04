using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds detail sections and subsection tabs for enemy visual preset panels.
/// </summary>
internal static class EnemyVisualPresetsPanelSectionsUtility
{
    #region Constants
    private static readonly Color ActiveTabColor = new Color(0.18f, 0.18f, 0.18f, 0.6f);
    #endregion

    #region Methods

    #region Public Methods
    public static void BuildMetadataSection(EnemyVisualPresetsPanel panel)
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
            EnemyManagementDraftSession.MarkDirty();
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
            EnemyManagementDraftSession.MarkDirty();
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

    public static void BuildVisualSection(EnemyVisualPresetsPanel panel)
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
                               EnemyVisualPresetsPanel.VisualSubSectionType.Visibility,
                               "Visibility",
                               BuildVisibilitySubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.Outline,
                               "Outline",
                               BuildOutlineSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.DamageFeedback,
                               "Damage Feedback",
                               BuildDamageFeedbackSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.ShooterWarning,
                               "Shooter Warning",
                               BuildShooterWarningSubSection(panel));
        AddVisualSubSectionTab(panel,
                               EnemyVisualPresetsPanel.VisualSubSectionType.Prefabs,
                               "Prefabs",
                               BuildPrefabsSubSection(panel));

        if (!panel.VisualSubSectionTabs.ContainsKey(panel.ActiveVisualSubSection))
            panel.ActiveVisualSubSection = EnemyVisualPresetsPanel.VisualSubSectionType.Visibility;

        panel.SetActiveVisualSubSection(panel.ActiveVisualSubSection);
    }

    public static void ShowActiveVisualSubSection(EnemyVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        VisualElement contentHost = panel.VisualSubSectionContentHost;

        if (contentHost == null)
            return;

        EnemyVisualPresetsPanel.VisualSubSectionTabEntry tabEntry;

        if (!panel.VisualSubSectionTabs.TryGetValue(panel.ActiveVisualSubSection, out tabEntry))
            return;

        if (tabEntry == null || tabEntry.Content == null)
            return;

        contentHost.Clear();
        contentHost.Add(tabEntry.Content);
        UpdateVisualSubSectionTabStyles(panel);
        ManagementToolInteractiveElementColorUtility.RefreshRegisteredSubtree(contentHost);
    }
    #endregion

    #region Private Methods
    private static VisualElement CreateDetailsSectionContainer(EnemyVisualPresetsPanel panel, string sectionTitle)
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
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.EnemyManagement.Visual.Section." + sectionTitle);
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
        ManagementToolCategoryLabelUtility.RegisterColorContextMenu(header, "NashCore.EnemyManagement.Visual.SubSection." + sectionTitle);
        container.Add(header);
        return container;
    }

    private static void AddPropertyField(EnemyVisualPresetsPanel panel,
                                         VisualElement target,
                                         SerializedProperty parentProperty,
                                         string relativePropertyName,
                                         string label,
                                         string tooltip)
    {
        if (panel == null)
            return;

        if (target == null)
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
            panel.RefreshPresetList();
        });
        target.Add(propertyField);
    }

    private static void AddVisualSubSectionTab(EnemyVisualPresetsPanel panel,
                                               EnemyVisualPresetsPanel.VisualSubSectionType subSectionType,
                                               string tabLabel,
                                               VisualElement content)
    {
        if (panel == null)
            return;

        if (panel.VisualSubSectionTabBar == null)
            return;

        if (content == null)
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

        EnemyVisualPresetsPanel.VisualSubSectionTabEntry tabEntry = new EnemyVisualPresetsPanel.VisualSubSectionTabEntry();
        tabEntry.TabContainer = tabContainer;
        tabEntry.TabButton = tabButton;
        tabEntry.Content = content;
        panel.VisualSubSectionTabs[subSectionType] = tabEntry;
    }

    private static void UpdateVisualSubSectionTabStyles(EnemyVisualPresetsPanel panel)
    {
        if (panel == null)
            return;

        foreach (KeyValuePair<EnemyVisualPresetsPanel.VisualSubSectionType, EnemyVisualPresetsPanel.VisualSubSectionTabEntry> tabEntry in panel.VisualSubSectionTabs)
        {
            if (tabEntry.Value == null || tabEntry.Value.TabButton == null)
                continue;

            bool isActive = tabEntry.Key == panel.ActiveVisualSubSection;
            tabEntry.Value.TabButton.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            tabEntry.Value.TabButton.style.backgroundColor = isActive ? ActiveTabColor : Color.clear;
        }
    }

    private static VisualElement BuildVisibilitySubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty visibilityProperty = panel.PresetSerializedObject.FindProperty("visibility");
        VisualElement container = CreateSubSectionContainer("Visibility");

        AddPropertyField(panel, container, visibilityProperty, "visualMode", "Visual Mode", "Visual runtime path used by this enemy type.");
        AddPropertyField(panel, container, visibilityProperty, "visualAnimationSpeed", "Visual Animation Speed", "Playback speed multiplier used by both companion and GPU-baked visual paths.");
        AddPropertyField(panel, container, visibilityProperty, "gpuAnimationLoopDuration", "GPU Animation Loop Duration", "Loop duration in seconds used by GPU-baked playback time wrapping.");
        AddPropertyField(panel, container, visibilityProperty, "enableDistanceCulling", "Enable Distance Culling", "Enable distance-based visual culling while gameplay simulation remains fully active.");
        AddPropertyField(panel, container, visibilityProperty, "maxVisibleDistance", "Max Visible Distance", "Maximum planar distance from player where visuals stay visible. Set to 0 to keep always visible.");
        AddPropertyField(panel, container, visibilityProperty, "visibleDistanceHysteresis", "Visible Distance Hysteresis", "Additional distance band used to avoid visual popping when crossing the culling boundary.");
        return container;
    }

    private static VisualElement BuildOutlineSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty outlineProperty = panel.PresetSerializedObject.FindProperty("outline");
        VisualElement container = CreateSubSectionContainer("Outline");

        AddPropertyField(panel, container, outlineProperty, "enableOutline", "Enable Outline", "When enabled, compatible enemy renderers receive outline property overrides from this preset.");
        AddPropertyField(panel, container, outlineProperty, "outlineThickness", "Outline Thickness", "Outline thickness written to compatible enemy materials exposing _OutlineThickness.");
        AddPropertyField(panel, container, outlineProperty, "outlineColor", "Outline Color", "Outline color written to compatible enemy materials exposing _OutlineColor.");
        return container;
    }

    private static VisualElement BuildPrefabsSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty prefabsProperty = panel.PresetSerializedObject.FindProperty("prefabs");
        VisualElement container = CreateSubSectionContainer("Prefabs");

        AddPropertyField(panel, container, prefabsProperty, "enemyPrefab", "Enemy Prefab", "Enemy prefab associated with this enemy type.");
        AddPropertyField(panel, container, prefabsProperty, "hitVfxPrefab", "Hit VFX Prefab", "Optional one-shot VFX prefab spawned every time this enemy receives a projectile hit.");
        AddPropertyField(panel, container, prefabsProperty, "hitVfxLifetimeSeconds", "Hit VFX Lifetime Seconds", "Lifetime in seconds assigned to each spawned hit VFX instance.");
        AddPropertyField(panel, container, prefabsProperty, "hitVfxScaleMultiplier", "Hit VFX Scale Multiplier", "Uniform scale multiplier applied to the spawned hit VFX instance.");
        AddPropertyField(panel, container, prefabsProperty, "spawnPaintColor", "Spawn Paint Color", "Color used by the wave painter and scene preview for this enemy type.");
        return container;
    }

    private static VisualElement BuildDamageFeedbackSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty damageFeedbackProperty = panel.PresetSerializedObject.FindProperty("damageFeedback");
        VisualElement container = CreateSubSectionContainer("Damage Feedback");

        AddPropertyField(panel, container, damageFeedbackProperty, "flashColor", "Flash Color", "Tint color applied during the brief damage flash.");
        AddPropertyField(panel, container, damageFeedbackProperty, "flashDurationSeconds", "Flash Duration Seconds", "Flash duration in seconds. Use very small values for a 1-3 frame reaction.");
        AddPropertyField(panel, container, damageFeedbackProperty, "flashMaximumBlend", "Flash Maximum Blend", "Maximum overlay strength reached immediately after a valid hit.");
        return container;
    }

    private static VisualElement BuildShooterWarningSubSection(EnemyVisualPresetsPanel panel)
    {
        SerializedProperty shooterWarningProperty = panel.PresetSerializedObject.FindProperty("shooterWarning");
        VisualElement container = CreateSubSectionContainer("Shooter Warning");

        AddPropertyField(panel, container, shooterWarningProperty, "enableAimPulse", "Enable Aim Pulse", "When enabled, shooter enemies pulse while charging the first shot of a burst.");
        AddPropertyField(panel, container, shooterWarningProperty, "aimPulseColor", "Aim Pulse Color", "Tint color applied while the shooter aim pulse warning ramps up.");
        AddPropertyField(panel, container, shooterWarningProperty, "aimPulseLeadTimeSeconds", "Aim Pulse Lead Time Seconds", "Seconds before burst start where the shooter pulse may already begin while the enemy prepares to fire.");
        AddPropertyField(panel, container, shooterWarningProperty, "aimPulseFadeOutSeconds", "Aim Pulse Fade Out Seconds", "Seconds used to softly fade the shooter pulse after the warning intensity drops.");
        AddPropertyField(panel, container, shooterWarningProperty, "aimPulseMaximumBlend", "Aim Pulse Maximum Blend", "Maximum overlay strength reached right before the first projectile of the burst is fired.");
        return container;
    }
    #endregion

    #endregion
}
