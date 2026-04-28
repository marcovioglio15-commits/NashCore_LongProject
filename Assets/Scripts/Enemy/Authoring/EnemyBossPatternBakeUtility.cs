using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Compiles boss pattern presets by reusing the same module slots authored by normal enemy Pattern Assemble.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyBossPatternBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles one boss pattern preset using a baker-provided prefab resolver for minion spawn rules.
    /// /params preset Boss pattern preset to compile.
    /// /params globalEngagementSettings Generic offensive engagement feedback settings resolved from the visual preset.
    /// /params minionPrefabResolver Callback that converts a minion prefab GameObject to an entity prefab.
    /// /returns Compiled boss pattern data.
    /// </summary>
    public static EnemyCompiledBossPatternBakeResult Compile(EnemyBossPatternPreset preset,
                                                             EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                                             System.Func<GameObject, Entity> minionPrefabResolver)
    {
        EnemyCompiledBossPatternBakeResult result = new EnemyCompiledBossPatternBakeResult();

        if (preset == null)
            return result;

        EnemyModulesAndPatternsPreset sharedPreset = preset.SourcePatternsPreset;

        if (sharedPreset != null)
        {
            result.BasePattern = CompileBasePattern(sharedPreset, preset.BasePattern);
            TryAssignShooterRuntimeSettings(result.BasePattern, result);
            result.BaseFirstShooterConfigIndex = AppendShooterConfigs(result.BasePattern, result);
            result.BaseShooterConfigCount = result.BasePattern.ShooterConfigs.Count;
            List<EnemyOffensiveEngagementConfigElement> baseEngagementConfigs = CompileBaseEngagementConfigs(sharedPreset,
                                                                                                             preset.BasePattern,
                                                                                                             globalEngagementSettings);
            result.BaseFirstOffensiveEngagementConfigIndex = AppendOffensiveEngagementConfigs(baseEngagementConfigs, result);
            result.BaseOffensiveEngagementConfigCount = baseEngagementConfigs.Count;
            CompileInteractions(sharedPreset, preset.Interactions, baseEngagementConfigs, globalEngagementSettings, result);
            ConfigureInitialPattern(result);
        }

        TryAppendMinionRules(preset.MinionSpawn, minionPrefabResolver, result);
        return result;
    }
    #endregion

    #region Pattern Compile
    /// <summary>
    /// Compiles the always-available base boss pattern from normal Core, Short-Range and Weapon slots.
    /// /params sharedPreset Source shared preset containing module definitions.
    /// /params basePattern Base boss assembly to compile.
    /// /returns Compiled base pattern result.
    /// </summary>
    private static EnemyCompiledPatternBakeResult CompileBasePattern(EnemyModulesAndPatternsPreset sharedPreset,
                                                                     EnemyBossPatternAssemblyDefinition basePattern)
    {
        EnemyCompiledPatternBakeResult result = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);

        if (sharedPreset == null || basePattern == null)
            return result;

        EnemyPatternCoreMovementAssembly coreMovement = basePattern.CoreMovement;

        if (coreMovement != null)
            EnemyModulesAndPatternsBakeUtility.TryApplyCoreMovementModule(sharedPreset, coreMovement.Binding, ref result);

        ApplyShortRangeSlot(sharedPreset, basePattern.ShortRangeInteraction, ref result.PatternConfig);
        ApplyWeaponSlot(sharedPreset, basePattern.WeaponInteraction, ref result);
        result.HasCustomMovement = EnemyBossPatternConfigUtility.RequiresCustomMovement(in result.PatternConfig);
        return result;
    }

    /// <summary>
    /// Compiles all ordered boss-specific interactions and appends their shooter config slices.
    /// /params sharedPreset Source shared preset containing module definitions.
    /// /params interactions Ordered boss interaction definitions.
    /// /params result Mutable boss compile result.
    /// /returns None.
    /// </summary>
    private static void CompileInteractions(EnemyModulesAndPatternsPreset sharedPreset,
                                            IReadOnlyList<EnemyBossPatternInteractionDefinition> interactions,
                                            IReadOnlyList<EnemyOffensiveEngagementConfigElement> baseEngagementConfigs,
                                            EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                            EnemyCompiledBossPatternBakeResult result)
    {
        if (sharedPreset == null || interactions == null || result == null)
            return;

        for (int interactionIndex = 0; interactionIndex < interactions.Count; interactionIndex++)
        {
            EnemyBossPatternInteractionDefinition interaction = interactions[interactionIndex];

            if (interaction == null || !interaction.Enabled)
                continue;

            EnemyCompiledPatternBakeResult interactionPattern = CompileInteractionPattern(sharedPreset, result.BasePattern, interaction);
            TryAssignShooterRuntimeSettings(interactionPattern, result);
            int firstShooterConfigIndex = AppendShooterConfigs(interactionPattern, result);
            List<EnemyOffensiveEngagementConfigElement> engagementConfigs = CompileInteractionEngagementConfigs(sharedPreset,
                                                                                                               baseEngagementConfigs,
                                                                                                               interaction,
                                                                                                               interactionIndex,
                                                                                                               globalEngagementSettings);
            int firstEngagementConfigIndex = AppendOffensiveEngagementConfigs(engagementConfigs, result);
            result.Interactions.Add(new EnemyBossPatternInteractionElement
            {
                InteractionIndex = math.max(0, interactionIndex),
                InteractionType = interaction.InteractionType,
                MinimumActiveSeconds = math.max(0f, interaction.MinimumActiveSeconds),
                MinimumMissingHealthPercent = math.saturate(interaction.MinimumMissingHealthPercent),
                MaximumMissingHealthPercent = math.saturate(interaction.MaximumMissingHealthPercent),
                MinimumElapsedSeconds = math.max(0f, interaction.MinimumElapsedSeconds),
                MaximumElapsedSeconds = math.max(0f, interaction.MaximumElapsedSeconds),
                MinimumTravelledDistance = math.max(0f, interaction.MinimumTravelledDistance),
                MaximumTravelledDistance = math.max(0f, interaction.MaximumTravelledDistance),
                MinimumPlayerDistance = math.max(0f, interaction.MinimumPlayerDistance),
                MaximumPlayerDistance = math.max(0f, interaction.MaximumPlayerDistance),
                RecentlyDamagedWindowSeconds = math.max(0f, interaction.RecentlyDamagedWindowSeconds),
                HasCustomMovement = interactionPattern.HasCustomMovement ? (byte)1 : (byte)0,
                FirstShooterConfigIndex = firstShooterConfigIndex,
                ShooterConfigCount = interactionPattern.ShooterConfigs.Count,
                FirstOffensiveEngagementConfigIndex = firstEngagementConfigIndex,
                OffensiveEngagementConfigCount = engagementConfigs.Count,
                PatternConfig = interactionPattern.PatternConfig
            });
        }
    }

    /// <summary>
    /// Compiles one boss interaction by starting from the base pattern and applying only enabled override slots.
    /// /params sharedPreset Source shared preset containing module definitions.
    /// /params basePattern Compiled base pattern used as inherited fallback.
    /// /params interaction Boss-specific interaction to compile.
    /// /returns Compiled interaction pattern result.
    /// </summary>
    private static EnemyCompiledPatternBakeResult CompileInteractionPattern(EnemyModulesAndPatternsPreset sharedPreset,
                                                                           EnemyCompiledPatternBakeResult basePattern,
                                                                           EnemyBossPatternInteractionDefinition interaction)
    {
        bool hasWeaponOverride = interaction != null &&
                                 interaction.WeaponInteraction != null &&
                                 interaction.WeaponInteraction.IsEnabled;
        EnemyCompiledPatternBakeResult result = CloneBasePattern(basePattern, !hasWeaponOverride);

        if (sharedPreset == null || interaction == null)
            return result;

        EnemyBossPatternCoreMovementOverrideAssembly coreMovement = interaction.CoreMovement;

        if (coreMovement != null && coreMovement.IsEnabled)
            EnemyModulesAndPatternsBakeUtility.TryApplyCoreMovementModule(sharedPreset, coreMovement.Binding, ref result);

        ApplyShortRangeSlot(sharedPreset, interaction.ShortRangeInteraction, ref result.PatternConfig);

        if (hasWeaponOverride)
            ApplyWeaponSlot(sharedPreset, interaction.WeaponInteraction, ref result);

        result.HasCustomMovement = EnemyBossPatternConfigUtility.RequiresCustomMovement(in result.PatternConfig);
        return result;
    }

    /// <summary>
    /// Applies one optional short-range slot to a compiled pattern config.
    /// /params sharedPreset Source shared preset containing module definitions.
    /// /params shortRangeInteraction Short-range slot to apply.
    /// /params patternConfig Mutable compiled pattern config.
    /// /returns None.
    /// </summary>
    private static void ApplyShortRangeSlot(EnemyModulesAndPatternsPreset sharedPreset,
                                            EnemyPatternShortRangeInteractionAssembly shortRangeInteraction,
                                            ref EnemyPatternConfig patternConfig)
    {
        if (sharedPreset == null || shortRangeInteraction == null || !shortRangeInteraction.IsEnabled)
            return;

        EnemyModulesAndPatternsBakeUtility.TryApplyShortRangeInteractionModule(sharedPreset,
                                                                               shortRangeInteraction.Binding,
                                                                               shortRangeInteraction.ActivationRange,
                                                                               shortRangeInteraction.ReleaseDistanceBuffer,
                                                                               ref patternConfig);
    }

    /// <summary>
    /// Applies one optional weapon slot to a compiled pattern result.
    /// /params sharedPreset Source shared preset containing module definitions.
    /// /params weaponInteraction Weapon slot to apply.
    /// /params result Mutable compiled pattern result.
    /// /returns None.
    /// </summary>
    private static void ApplyWeaponSlot(EnemyModulesAndPatternsPreset sharedPreset,
                                        EnemyPatternWeaponInteractionAssembly weaponInteraction,
                                        ref EnemyCompiledPatternBakeResult result)
    {
        if (sharedPreset == null || weaponInteraction == null || !weaponInteraction.IsEnabled)
            return;

        EnemyModulesAndPatternsBakeUtility.TryAddWeaponInteractionModule(sharedPreset,
                                                                         weaponInteraction.Binding,
                                                                         weaponInteraction.UseMinimumRange,
                                                                         weaponInteraction.MinimumRange,
                                                                         weaponInteraction.UseMaximumRange,
                                                                         weaponInteraction.MaximumRange,
                                                                         weaponInteraction.ExclusiveLookDirectionControl,
                                                                         weaponInteraction.ActivationGates,
                                                                         weaponInteraction.MaximumActivationSpeed,
                                                                         weaponInteraction.RecentlyDamagedWindowSeconds,
                                                                         ref result);
    }

    /// <summary>
    /// Clones the base pattern config and optionally copies inherited shooter configs for interaction layers.
    /// /params basePattern Compiled base pattern to clone.
    /// /params inheritWeapon True when base shooter configs should remain active for the interaction.
    /// /returns A mutable cloned pattern result.
    /// </summary>
    private static EnemyCompiledPatternBakeResult CloneBasePattern(EnemyCompiledPatternBakeResult basePattern, bool inheritWeapon)
    {
        EnemyCompiledPatternBakeResult result = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);

        if (basePattern == null)
            return result;

        result.PatternConfig = basePattern.PatternConfig;
        result.HasCustomMovement = basePattern.HasCustomMovement;
        result.DropItemsConfig = basePattern.DropItemsConfig;

        if (!inheritWeapon)
            return result;

        for (int shooterIndex = 0; shooterIndex < basePattern.ShooterConfigs.Count; shooterIndex++)
            result.ShooterConfigs.Add(basePattern.ShooterConfigs[shooterIndex]);

        result.ShooterProjectilePrefab = basePattern.ShooterProjectilePrefab;
        result.ShooterProjectilePoolInitialCapacity = basePattern.ShooterProjectilePoolInitialCapacity;
        result.ShooterProjectilePoolExpandBatch = basePattern.ShooterProjectilePoolExpandBatch;
        result.HasShooterRuntimeSettings = basePattern.HasShooterRuntimeSettings;
        return result;
    }

    /// <summary>
    /// Appends one compiled pattern shooter slice to the boss-owned source buffer.
    /// /params compiledPattern Compiled pattern providing shooter configs.
    /// /params result Mutable boss compile result.
    /// /returns First appended shooter config index.
    /// </summary>
    private static int AppendShooterConfigs(EnemyCompiledPatternBakeResult compiledPattern, EnemyCompiledBossPatternBakeResult result)
    {
        if (compiledPattern == null || result == null)
            return 0;

        int firstShooterConfigIndex = result.ShooterConfigs.Count;

        for (int shooterIndex = 0; shooterIndex < compiledPattern.ShooterConfigs.Count; shooterIndex++)
            result.ShooterConfigs.Add(compiledPattern.ShooterConfigs[shooterIndex]);

        return firstShooterConfigIndex;
    }

    /// <summary>
    /// Appends one compiled offensive engagement slice to the boss-owned source buffer.
    /// /params engagementConfigs Compiled engagement configs for one boss layer.
    /// /params result Mutable boss compile result.
    /// /returns First appended engagement config index.
    /// </summary>
    private static int AppendOffensiveEngagementConfigs(IReadOnlyList<EnemyOffensiveEngagementConfigElement> engagementConfigs,
                                                        EnemyCompiledBossPatternBakeResult result)
    {
        if (engagementConfigs == null || result == null)
            return 0;

        int firstConfigIndex = result.OffensiveEngagementConfigs.Count;

        for (int configIndex = 0; configIndex < engagementConfigs.Count; configIndex++)
            result.OffensiveEngagementConfigs.Add(engagementConfigs[configIndex]);

        return firstConfigIndex;
    }

    /// <summary>
    /// Compiles the offensive engagement configs owned by the always-available base boss pattern.
    /// /params sharedPreset Source shared preset containing module definitions.
    /// /params basePattern Base boss assembly to inspect.
    /// /params globalEngagementSettings Generic feedback settings resolved from the visual preset.
    /// /returns Ordered base engagement configs.
    /// </summary>
    private static List<EnemyOffensiveEngagementConfigElement> CompileBaseEngagementConfigs(EnemyModulesAndPatternsPreset sharedPreset,
                                                                                            EnemyBossPatternAssemblyDefinition basePattern,
                                                                                            EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings)
    {
        List<EnemyOffensiveEngagementConfigElement> configs = new List<EnemyOffensiveEngagementConfigElement>(2);

        if (sharedPreset == null || basePattern == null)
            return configs;

        AppendShortRangeEngagementConfig(sharedPreset, basePattern.ShortRangeInteraction, globalEngagementSettings, -1, configs);
        AppendWeaponEngagementConfig(sharedPreset, basePattern.WeaponInteraction, globalEngagementSettings, -1, configs);
        return configs;
    }

    /// <summary>
    /// Compiles the offensive engagement configs for one boss interaction, inheriting base configs unless the slot is overridden.
    /// /params sharedPreset Source shared preset containing module definitions.
    /// /params baseEngagementConfigs Base configs inherited by interactions without slot overrides.
    /// /params interaction Boss interaction being compiled.
    /// /params interactionIndex Authored interaction index used by managed visual override resolution.
    /// /params globalEngagementSettings Generic feedback settings resolved from the visual preset.
    /// /returns Ordered engagement configs for the interaction layer.
    /// </summary>
    private static List<EnemyOffensiveEngagementConfigElement> CompileInteractionEngagementConfigs(EnemyModulesAndPatternsPreset sharedPreset,
                                                                                                   IReadOnlyList<EnemyOffensiveEngagementConfigElement> baseEngagementConfigs,
                                                                                                   EnemyBossPatternInteractionDefinition interaction,
                                                                                                   int interactionIndex,
                                                                                                   EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings)
    {
        List<EnemyOffensiveEngagementConfigElement> configs = CloneEngagementConfigs(baseEngagementConfigs);

        if (sharedPreset == null || interaction == null)
            return configs;

        EnemyPatternShortRangeInteractionAssembly shortRangeInteraction = interaction.ShortRangeInteraction;

        if (shortRangeInteraction != null && shortRangeInteraction.IsEnabled)
        {
            RemoveConfigsBySource(configs, EnemyOffensiveEngagementTriggerSource.ShortRangeInteraction);
            AppendShortRangeEngagementConfig(sharedPreset,
                                             shortRangeInteraction,
                                             globalEngagementSettings,
                                             interactionIndex,
                                             configs);
        }

        EnemyPatternWeaponInteractionAssembly weaponInteraction = interaction.WeaponInteraction;

        if (weaponInteraction != null && weaponInteraction.IsEnabled)
        {
            RemoveConfigsBySource(configs, EnemyOffensiveEngagementTriggerSource.WeaponInteraction);
            AppendWeaponEngagementConfig(sharedPreset,
                                         weaponInteraction,
                                         globalEngagementSettings,
                                         interactionIndex,
                                         configs);
        }

        return configs;
    }

    /// <summary>
    /// Appends one short-range engagement config when the slot uses a supported module and visible feedback channel.
    /// /params sharedPreset Source shared preset containing module definitions.
    /// /params shortRangeInteraction Short-range slot being compiled.
    /// /params globalEngagementSettings Generic feedback settings resolved from the visual preset.
    /// /params visualSettingsKey Boss visual override key baked into the config.
    /// /params configs Target config list.
    /// /returns None.
    /// </summary>
    private static void AppendShortRangeEngagementConfig(EnemyModulesAndPatternsPreset sharedPreset,
                                                         EnemyPatternShortRangeInteractionAssembly shortRangeInteraction,
                                                         EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                                         int visualSettingsKey,
                                                         List<EnemyOffensiveEngagementConfigElement> configs)
    {
        if (configs == null)
            return;

        if (!EnemyOffensiveEngagementBakeUtility.TryBuildShortRangeConfig(shortRangeInteraction,
                                                                          sharedPreset,
                                                                          globalEngagementSettings,
                                                                          out EnemyOffensiveEngagementConfigElement config))
        {
            return;
        }

        config.VisualSettingsKey = visualSettingsKey;
        configs.Add(config);
    }

    /// <summary>
    /// Appends one weapon engagement config when the slot uses a supported module and visible feedback channel.
    /// /params sharedPreset Source shared preset containing module definitions.
    /// /params weaponInteraction Weapon slot being compiled.
    /// /params globalEngagementSettings Generic feedback settings resolved from the visual preset.
    /// /params visualSettingsKey Boss visual override key baked into the config.
    /// /params configs Target config list.
    /// /returns None.
    /// </summary>
    private static void AppendWeaponEngagementConfig(EnemyModulesAndPatternsPreset sharedPreset,
                                                     EnemyPatternWeaponInteractionAssembly weaponInteraction,
                                                     EnemyOffensiveEngagementFeedbackSettings globalEngagementSettings,
                                                     int visualSettingsKey,
                                                     List<EnemyOffensiveEngagementConfigElement> configs)
    {
        if (configs == null)
            return;

        if (!EnemyOffensiveEngagementBakeUtility.TryBuildWeaponConfig(weaponInteraction,
                                                                      sharedPreset,
                                                                      globalEngagementSettings,
                                                                      out EnemyOffensiveEngagementConfigElement config))
        {
            return;
        }

        config.VisualSettingsKey = visualSettingsKey;
        configs.Add(config);
    }

    /// <summary>
    /// Copies inherited engagement configs so interaction edits do not mutate the base layer list.
    /// /params sourceConfigs Source configs to copy.
    /// /returns Mutable cloned config list.
    /// </summary>
    private static List<EnemyOffensiveEngagementConfigElement> CloneEngagementConfigs(IReadOnlyList<EnemyOffensiveEngagementConfigElement> sourceConfigs)
    {
        int capacity = sourceConfigs != null ? sourceConfigs.Count : 0;
        List<EnemyOffensiveEngagementConfigElement> configs = new List<EnemyOffensiveEngagementConfigElement>(capacity);

        if (sourceConfigs == null)
            return configs;

        for (int configIndex = 0; configIndex < sourceConfigs.Count; configIndex++)
            configs.Add(sourceConfigs[configIndex]);

        return configs;
    }

    /// <summary>
    /// Removes inherited engagement configs for the source overridden by an interaction slot.
    /// /params configs Mutable config list.
    /// /params source Interaction source to remove.
    /// /returns None.
    /// </summary>
    private static void RemoveConfigsBySource(List<EnemyOffensiveEngagementConfigElement> configs,
                                              EnemyOffensiveEngagementTriggerSource source)
    {
        if (configs == null)
            return;

        for (int configIndex = configs.Count - 1; configIndex >= 0; configIndex--)
        {
            if (configs[configIndex].Source != source)
                continue;

            configs.RemoveAt(configIndex);
        }
    }

    /// <summary>
    /// Configures the normal enemy pattern output used by the authoring baker as the boss spawn baseline.
    /// /params result Mutable boss compile result.
    /// /returns None.
    /// </summary>
    private static void ConfigureInitialPattern(EnemyCompiledBossPatternBakeResult result)
    {
        if (result == null)
            return;

        result.InitialPattern = CloneBasePattern(result.BasePattern, true);
        result.InitialPattern.ShooterProjectilePrefab = result.ShooterProjectilePrefab;
        result.InitialPattern.ShooterProjectilePoolInitialCapacity = result.ShooterProjectilePoolInitialCapacity;
        result.InitialPattern.ShooterProjectilePoolExpandBatch = result.ShooterProjectilePoolExpandBatch;
        result.InitialPattern.HasShooterRuntimeSettings = result.HasShooterRuntimeSettings;
        result.InitialPattern.HasCustomMovement = result.BasePattern.HasCustomMovement || ResolveAnyInteractionHasCustomMovement(result);
    }

    /// <summary>
    /// Checks whether any compiled boss interaction requires the custom movement system.
    /// /params result Compiled boss result.
    /// /returns True when any interaction needs custom pattern movement.
    /// </summary>
    private static bool ResolveAnyInteractionHasCustomMovement(EnemyCompiledBossPatternBakeResult result)
    {
        if (result == null)
            return false;

        for (int interactionIndex = 0; interactionIndex < result.Interactions.Count; interactionIndex++)
        {
            if (result.Interactions[interactionIndex].HasCustomMovement != 0)
                return true;
        }

        return false;
    }
    #endregion

    #region Minions
    /// <summary>
    /// Converts minion authoring rules into baked spawn entries with automatic pool sizes.
    /// /params minionSpawn Source minion spawn settings.
    /// /params minionPrefabResolver Callback used to bake prefab references.
    /// /params result Mutable boss result.
    /// /returns None.
    /// </summary>
    private static void TryAppendMinionRules(EnemyBossMinionSpawnSettings minionSpawn,
                                             System.Func<GameObject, Entity> minionPrefabResolver,
                                             EnemyCompiledBossPatternBakeResult result)
    {
        if (minionSpawn == null || result == null || !minionSpawn.Enabled)
            return;

        IReadOnlyList<EnemyBossMinionSpawnRule> rules = minionSpawn.Rules;

        if (rules == null)
            return;

        for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            EnemyBossMinionSpawnRule rule = rules[ruleIndex];

            if (rule == null || !rule.Enabled || rule.MinionPrefab == null)
                continue;

            Entity prefabEntity = minionPrefabResolver != null
                ? minionPrefabResolver(rule.MinionPrefab)
                : Entity.Null;

            if (prefabEntity == Entity.Null)
                continue;

            float intervalSeconds = rule.IntervalSeconds > 0f
                ? rule.IntervalSeconds
                : minionSpawn.FallbackIntervalSeconds;

            result.MinionSpawns.Add(new EnemyBossMinionSpawnElement
            {
                PrefabEntity = prefabEntity,
                Trigger = rule.Trigger,
                IntervalSeconds = math.max(0.01f, intervalSeconds),
                BossHitCooldownSeconds = math.max(0f, rule.BossHitCooldownSeconds),
                HealthThresholdPercent = math.saturate(rule.HealthThresholdPercent),
                SpawnCount = math.max(0, rule.SpawnCount),
                MaxAliveMinions = math.max(0, rule.MaxAliveMinions),
                SpawnRadius = math.max(0f, rule.SpawnRadius),
                DespawnDistance = math.max(0f, rule.DespawnDistance),
                ExperienceDropMultiplier = math.max(0f, rule.ExperienceDropMultiplier),
                ExtraComboPointsMultiplier = math.max(0f, rule.ExtraComboPointsMultiplier),
                FutureDropsMultiplier = math.max(0f, rule.FutureDropsMultiplier),
                AutomaticPoolSize = math.max(0, rule.CalculateAutomaticPoolSize()),
                PoolExpandBatch = math.max(1, minionSpawn.PoolExpandBatch),
                KillMinionsOnBossDeath = minionSpawn.KillMinionsOnBossDeath ? (byte)1 : (byte)0,
                RequireMinionsKilledForRunCompletion = !minionSpawn.KillMinionsOnBossDeath &&
                                                        minionSpawn.RequireMinionsKilledForRunCompletion
                    ? (byte)1
                    : (byte)0,
                PoolEntity = Entity.Null,
                NextSpawnTime = 0f,
                LastObservedDamageLifetimeSeconds = 0f,
                Triggered = 0,
                Initialized = 0
            });
        }
    }
    #endregion

    #region Runtime Projectile
    /// <summary>
    /// Copies the first available shooter runtime projectile settings from a compiled pattern into the boss result.
    /// /params compiledPattern Compiled pattern that may contain shooter runtime settings.
    /// /params result Mutable boss bake result.
    /// /returns None.
    /// </summary>
    private static void TryAssignShooterRuntimeSettings(EnemyCompiledPatternBakeResult compiledPattern,
                                                        EnemyCompiledBossPatternBakeResult result)
    {
        if (compiledPattern == null || result == null)
            return;

        if (result.HasShooterRuntimeSettings || !compiledPattern.HasShooterRuntimeSettings)
            return;

        result.ShooterProjectilePrefab = compiledPattern.ShooterProjectilePrefab;
        result.ShooterProjectilePoolInitialCapacity = compiledPattern.ShooterProjectilePoolInitialCapacity;
        result.ShooterProjectilePoolExpandBatch = compiledPattern.ShooterProjectilePoolExpandBatch;
        result.HasShooterRuntimeSettings = true;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores compiled boss pattern data before it is written by EnemyAuthoringBaker.
/// /params None.
/// /returns None.
/// </summary>
internal sealed class EnemyCompiledBossPatternBakeResult
{
    #region Fields
    public EnemyCompiledPatternBakeResult BasePattern = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);
    public EnemyCompiledPatternBakeResult InitialPattern = EnemyAdvancedPatternBakeUtility.CreateDefaultResult(null);
    public readonly List<EnemyBossPatternInteractionElement> Interactions = new List<EnemyBossPatternInteractionElement>();
    public readonly List<EnemyShooterConfigElement> ShooterConfigs = new List<EnemyShooterConfigElement>();
    public readonly List<EnemyOffensiveEngagementConfigElement> OffensiveEngagementConfigs = new List<EnemyOffensiveEngagementConfigElement>();
    public readonly List<EnemyBossMinionSpawnElement> MinionSpawns = new List<EnemyBossMinionSpawnElement>();
    public GameObject ShooterProjectilePrefab;
    public int ShooterProjectilePoolInitialCapacity;
    public int ShooterProjectilePoolExpandBatch;
    public bool HasShooterRuntimeSettings;
    public int BaseFirstShooterConfigIndex;
    public int BaseShooterConfigCount;
    public int BaseFirstOffensiveEngagementConfigIndex;
    public int BaseOffensiveEngagementConfigCount;
    #endregion
}
