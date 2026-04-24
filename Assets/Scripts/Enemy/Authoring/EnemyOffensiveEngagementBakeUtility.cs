using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Compiles offensive engagement feedback settings from the active shared pattern and visual preset into ECS runtime buffers.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyOffensiveEngagementBakeUtility
{
    #region Fields
    private static readonly EnemyOffensiveEngagementFeedbackSettings DefaultSettings = new EnemyOffensiveEngagementFeedbackSettings();
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends every supported offensive engagement feedback configuration for the currently selected shared pattern.
    /// /params authoring Enemy authoring component that resolves visual and advanced-pattern presets.
    /// /params configs Target dynamic buffer populated during bake.
    /// /returns None.
    /// </summary>
    public static void AppendConfigs(EnemyAuthoring authoring, DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs)
    {
        if (authoring == null)
            return;

        EnemyAdvancedPatternPreset advancedPatternPreset = authoring.AdvancedPatternPreset;

        if (advancedPatternPreset == null)
            return;

        EnemyModulesAndPatternsPreset sharedPreset = advancedPatternPreset.ModulesAndPatternsPreset;

        if (sharedPreset == null)
            return;

        EnemyModulesPatternDefinition selectedPattern = EnemyModulesAndPatternsSelectionUtility.ResolveSelectedPattern(advancedPatternPreset);

        if (selectedPattern == null)
            return;

        EnemyOffensiveEngagementFeedbackSettings globalSettings = authoring.OffensiveEngagementFeedbackSettings;

        if (globalSettings == null)
            globalSettings = DefaultSettings;

        AppendShortRangeConfig(selectedPattern, sharedPreset, globalSettings, configs);
        AppendWeaponConfig(selectedPattern, sharedPreset, globalSettings, configs);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Appends the short-range offensive engagement feedback configuration when the selected module kind supports timing prediction.
    /// /params pattern Selected shared pattern definition.
    /// /params sharedPreset Shared preset used to resolve the selected module kind.
    /// /params globalSettings Generic visual feedback settings resolved from the visual preset.
    /// /params configs Target dynamic buffer populated during bake.
    /// /returns None.
    /// </summary>
    private static void AppendShortRangeConfig(EnemyModulesPatternDefinition pattern,
                                               EnemyModulesAndPatternsPreset sharedPreset,
                                               EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                               DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs)
    {
        if (pattern == null || sharedPreset == null)
            return;

        EnemyPatternShortRangeInteractionAssembly interaction = pattern.ShortRangeInteraction;

        if (interaction == null ||
            !interaction.IsEnabled ||
            !interaction.DisplayBehaviourEngagementTrigger ||
            interaction.Binding == null)
        {
            return;
        }

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(interaction.Binding.ModuleId);

        if (moduleDefinition == null)
            return;

        EnemyOffensiveEngagementTimingMode timingMode = EnemyOffensiveEngagementSupportUtility.ResolveTimingMode(EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                                                                                                                 moduleDefinition.ModuleKind);

        if (timingMode == EnemyOffensiveEngagementTimingMode.None)
            return;

        EnemyOffensiveEngagementFeedbackSettings settings = ResolveSettings(globalSettings,
                                                                           interaction.UseEngagementFeedbackOverride,
                                                                           interaction.EngagementFeedbackOverride);
        AppendConfig(EnemyOffensiveEngagementTriggerSource.ShortRangeInteraction,
                     timingMode,
                     interaction.UseEngagementFeedbackOverride,
                     settings,
                     configs);
    }

    /// <summary>
    /// Appends the weapon offensive engagement feedback configuration when the selected module kind supports timing prediction.
    /// /params pattern Selected shared pattern definition.
    /// /params sharedPreset Shared preset used to resolve the selected module kind.
    /// /params globalSettings Generic visual feedback settings resolved from the visual preset.
    /// /params configs Target dynamic buffer populated during bake.
    /// /returns None.
    /// </summary>
    private static void AppendWeaponConfig(EnemyModulesPatternDefinition pattern,
                                           EnemyModulesAndPatternsPreset sharedPreset,
                                           EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                           DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs)
    {
        if (pattern == null || sharedPreset == null)
            return;

        EnemyPatternWeaponInteractionAssembly interaction = pattern.WeaponInteraction;

        if (interaction == null ||
            !interaction.IsEnabled ||
            !interaction.DisplayBehaviourEngagementTrigger ||
            interaction.Binding == null)
        {
            return;
        }

        EnemyPatternModuleDefinition moduleDefinition = sharedPreset.ResolveModuleDefinitionById(interaction.Binding.ModuleId);

        if (moduleDefinition == null)
            return;

        EnemyOffensiveEngagementTimingMode timingMode = EnemyOffensiveEngagementSupportUtility.ResolveTimingMode(EnemyPatternModuleCatalogSection.WeaponInteraction,
                                                                                                                 moduleDefinition.ModuleKind);

        if (timingMode == EnemyOffensiveEngagementTimingMode.None)
            return;

        EnemyOffensiveEngagementFeedbackSettings settings = ResolveSettings(globalSettings,
                                                                           interaction.UseEngagementFeedbackOverride,
                                                                           interaction.EngagementFeedbackOverride);
        AppendConfig(EnemyOffensiveEngagementTriggerSource.WeaponInteraction,
                     timingMode,
                     interaction.UseEngagementFeedbackOverride,
                     settings,
                     configs);
    }

    /// <summary>
    /// Resolves the authored feedback settings block that should be baked for the current interaction.
    /// /params globalSettings Generic visual feedback settings resolved from the visual preset.
    /// /params useOverrideSettings True when the interaction-specific override is enabled.
    /// /params overrideSettings Optional interaction-specific override settings.
    /// /returns The settings block that should be baked.
    /// </summary>
    private static EnemyOffensiveEngagementFeedbackSettings ResolveSettings(EnemyOffensiveEngagementFeedbackSettings globalSettings,
                                                                            bool useOverrideSettings,
                                                                            EnemyOffensiveEngagementFeedbackSettings overrideSettings)
    {
        if (useOverrideSettings && overrideSettings != null)
            return overrideSettings;

        if (globalSettings != null)
            return globalSettings;

        return DefaultSettings;
    }

    /// <summary>
    /// Converts one authored settings block into a baked offensive engagement buffer entry.
    /// /params source Interaction source currently being compiled.
    /// /params timingMode Supported timing model used to predict the behaviour commit.
    /// /params useOverrideVisualSettings True when the interaction-specific override provided the baked settings.
    /// /params settings Resolved authored settings block.
    /// /params configs Target dynamic buffer populated during bake.
    /// /returns None.
    /// </summary>
    private static void AppendConfig(EnemyOffensiveEngagementTriggerSource source,
                                     EnemyOffensiveEngagementTimingMode timingMode,
                                     bool useOverrideVisualSettings,
                                     EnemyOffensiveEngagementFeedbackSettings settings,
                                     DynamicBuffer<EnemyOffensiveEngagementConfigElement> configs)
    {
        if (settings == null)
            settings = DefaultSettings;

        bool hasVisibleChannel = settings.EnableColorBlend || settings.EnableBillboard;

        if (!hasVisibleChannel)
            return;

        Vector3 billboardOffset = settings.BillboardLocalOffset;

        configs.Add(new EnemyOffensiveEngagementConfigElement
        {
            Source = source,
            TimingMode = timingMode,
            UseOverrideVisualSettings = useOverrideVisualSettings ? (byte)1 : (byte)0,
            EnableColorBlend = settings.EnableColorBlend ? (byte)1 : (byte)0,
            ColorBlendColor = DamageFlashRuntimeUtility.ToLinearFloat4(settings.ColorBlendColor),
            ColorBlendLeadTimeSeconds = math.max(0f, settings.ColorBlendLeadTimeSeconds),
            ColorBlendFadeOutSeconds = math.max(0f, settings.ColorBlendFadeOutSeconds),
            ColorBlendMaximumBlend = math.saturate(settings.ColorBlendMaximumBlend),
            EnableBillboard = settings.EnableBillboard ? (byte)1 : (byte)0,
            BillboardColor = DamageFlashRuntimeUtility.ToLinearFloat4(settings.BillboardColor),
            BillboardOffset = new float3(billboardOffset.x, billboardOffset.y, billboardOffset.z),
            BillboardLeadTimeSeconds = math.max(0f, settings.BillboardLeadTimeSeconds),
            BillboardBaseScale = math.max(0f, settings.BillboardBaseScale),
            BillboardPulseScaleMultiplier = math.max(0f, settings.BillboardPulseScaleMultiplier),
            BillboardPulseExpandDurationSeconds = math.max(0f, settings.BillboardPulseExpandDurationSeconds),
            BillboardPulseContractDurationSeconds = math.max(0f, settings.BillboardPulseContractDurationSeconds)
        });
    }
    #endregion

    #endregion
}
