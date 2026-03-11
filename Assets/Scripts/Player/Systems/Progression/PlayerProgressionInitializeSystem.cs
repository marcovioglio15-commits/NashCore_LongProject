using System;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Initializes player progression runtime components from baked controller/progression config data.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerProgressionInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingHealthQuery;
    private EntityQuery missingShieldQuery;
    private EntityQuery missingExperienceQuery;
    private EntityQuery missingLevelQuery;
    private EntityQuery missingExperienceCollectionQuery;
    private EntityQuery missingScalableStatsBufferQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates initialization queries for missing progression components.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();

        missingHealthQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig>()
            .WithNone<PlayerHealth>()
            .Build();

        missingShieldQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig>()
            .WithNone<PlayerShield>()
            .Build();

        missingExperienceQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerProgressionConfig>()
            .WithNone<PlayerExperience>()
            .Build();

        missingLevelQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerProgressionConfig>()
            .WithNone<PlayerLevel>()
            .Build();

        missingExperienceCollectionQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerProgressionConfig>()
            .WithNone<PlayerExperienceCollection>()
            .Build();

        missingScalableStatsBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerProgressionConfig>()
            .WithNone<PlayerScalableStatElement>()
            .Build();
    }

    /// <summary>
    /// Adds missing progression runtime components only once for new player entities.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnUpdate(ref SystemState state)
    {
        bool hasMissingHealth = !missingHealthQuery.IsEmptyIgnoreFilter;
        bool hasMissingShield = !missingShieldQuery.IsEmptyIgnoreFilter;
        bool hasMissingExperience = !missingExperienceQuery.IsEmptyIgnoreFilter;
        bool hasMissingLevel = !missingLevelQuery.IsEmptyIgnoreFilter;
        bool hasMissingExperienceCollection = !missingExperienceCollectionQuery.IsEmptyIgnoreFilter;
        bool hasMissingScalableStatsBuffer = !missingScalableStatsBufferQuery.IsEmptyIgnoreFilter;

        if (!hasMissingHealth &&
            !hasMissingShield &&
            !hasMissingExperience &&
            !hasMissingLevel &&
            !hasMissingExperienceCollection &&
            !hasMissingScalableStatsBuffer)
        {
            state.Enabled = false;
            return;
        }

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        if (hasMissingHealth)
            AddMissingHealth(ref commandBuffer);

        if (hasMissingShield)
            AddMissingShield(ref commandBuffer);

        if (hasMissingExperience)
            AddMissingExperience(ref commandBuffer);

        if (hasMissingLevel)
            AddMissingLevel(ref commandBuffer);

        if (hasMissingExperienceCollection)
            AddMissingExperienceCollection(ref commandBuffer);

        if (hasMissingScalableStatsBuffer)
            AddMissingScalableStatsBuffer(ref commandBuffer);

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();

        bool hasRemainingMissingHealth = !missingHealthQuery.IsEmptyIgnoreFilter;
        bool hasRemainingMissingShield = !missingShieldQuery.IsEmptyIgnoreFilter;
        bool hasRemainingMissingExperience = !missingExperienceQuery.IsEmptyIgnoreFilter;
        bool hasRemainingMissingLevel = !missingLevelQuery.IsEmptyIgnoreFilter;
        bool hasRemainingMissingExperienceCollection = !missingExperienceCollectionQuery.IsEmptyIgnoreFilter;
        bool hasRemainingMissingScalableStatsBuffer = !missingScalableStatsBufferQuery.IsEmptyIgnoreFilter;

        if (!hasRemainingMissingHealth &&
            !hasRemainingMissingShield &&
            !hasRemainingMissingExperience &&
            !hasRemainingMissingLevel &&
            !hasRemainingMissingExperienceCollection &&
            !hasRemainingMissingScalableStatsBuffer)
            state.Enabled = false;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Adds PlayerHealth components using controller health statistics config.
    /// </summary>
    /// <param name="commandBuffer">Command buffer used for deferred entity writes.</param>
    /// <returns>Void.</returns>
    private void AddMissingHealth(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingHealthQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerControllerConfig> configs = missingHealthQuery.ToComponentDataArray<PlayerControllerConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerControllerConfig controllerConfig = configs[index];

            if (!controllerConfig.Config.IsCreated)
                continue;

            float maxHealth = controllerConfig.Config.Value.HealthStatistics.MaxHealth;

            if (maxHealth < 1f)
                maxHealth = 1f;

            commandBuffer.AddComponent(entity, new PlayerHealth
            {
                Current = maxHealth,
                Max = maxHealth
            });
        }

        entities.Dispose();
        configs.Dispose();
    }

    /// <summary>
    /// Adds PlayerShield components using controller shield statistics config.
    /// </summary>
    /// <param name="commandBuffer">Command buffer used for deferred entity writes.</param>
    /// <returns>Void.</returns>
    private void AddMissingShield(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingShieldQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerControllerConfig> configs = missingShieldQuery.ToComponentDataArray<PlayerControllerConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerControllerConfig controllerConfig = configs[index];

            if (!controllerConfig.Config.IsCreated)
                continue;

            float maxShield = controllerConfig.Config.Value.HealthStatistics.MaxShield;

            if (maxShield < 0f)
                maxShield = 0f;

            commandBuffer.AddComponent(entity, new PlayerShield
            {
                Current = maxShield,
                Max = maxShield
            });
        }

        entities.Dispose();
        configs.Dispose();
    }

    /// <summary>
    /// Adds PlayerExperience from scalable stat default value named "experience", with zero fallback.
    /// </summary>
    /// <param name="commandBuffer">Command buffer used for deferred entity writes.</param>
    /// <returns>Void.</returns>
    private void AddMissingExperience(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingExperienceQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerProgressionConfig> configs = missingExperienceQuery.ToComponentDataArray<PlayerProgressionConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerProgressionConfig progressionConfig = configs[index];
            float experience = ResolveScalableStatDefaultValue(progressionConfig, "experience", 0f);

            if (experience < 0f)
                experience = 0f;

            commandBuffer.AddComponent(entity, new PlayerExperience
            {
                Current = experience
            });
        }

        entities.Dispose();
        configs.Dispose();
    }

    /// <summary>
    /// Adds PlayerLevel runtime data from progression milestones with safe defaults.
    /// </summary>
    /// <param name="commandBuffer">Command buffer used for deferred entity writes.</param>
    /// <returns>Void.</returns>
    private void AddMissingLevel(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingLevelQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerProgressionConfig> configs = missingLevelQuery.ToComponentDataArray<PlayerProgressionConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerProgressionConfig progressionConfig = configs[index];
            float requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig,
                                                                                                                    1,
                                                                                                                    out int activeGamePhaseIndex,
                                                                                                                    out bool _,
                                                                                                                    out int _);

            commandBuffer.AddComponent(entity, new PlayerLevel
            {
                Current = 1,
                ActiveGamePhaseIndex = activeGamePhaseIndex,
                RequiredExperienceForNextLevel = requiredExperienceForNextLevel
            });
        }

        entities.Dispose();
        configs.Dispose();
    }

    /// <summary>
    /// Adds PlayerExperienceCollection runtime data from progression milestones with safe defaults.
    /// </summary>
    /// <param name="commandBuffer">Command buffer used for deferred entity writes.</param>
    /// <returns>Void.</returns>
    private void AddMissingExperienceCollection(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingExperienceCollectionQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerProgressionConfig> configs = missingExperienceCollectionQuery.ToComponentDataArray<PlayerProgressionConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerProgressionConfig progressionConfig = configs[index];
            float pickupRadius = ResolveExperiencePickupRadius(progressionConfig, 0f);

            if (pickupRadius < 0f)
                pickupRadius = 0f;

            commandBuffer.AddComponent(entity, new PlayerExperienceCollection
            {
                PickupRadius = pickupRadius
            });
        }

        entities.Dispose();
        configs.Dispose();
    }

    /// <summary>
    /// Adds PlayerScalableStatElement buffer populated from progression scalable stat defaults.
    /// </summary>
    /// <param name="commandBuffer">Command buffer used for deferred entity writes.</param>
    /// <returns>Void.</returns>
    private void AddMissingScalableStatsBuffer(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingScalableStatsBufferQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerProgressionConfig> configs = missingScalableStatsBufferQuery.ToComponentDataArray<PlayerProgressionConfig>(Allocator.Temp);

        for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            Entity entity = entities[entityIndex];
            PlayerProgressionConfig progressionConfig = configs[entityIndex];
            DynamicBuffer<PlayerScalableStatElement> scalableStatsBuffer = commandBuffer.AddBuffer<PlayerScalableStatElement>(entity);

            if (!progressionConfig.Config.IsCreated)
                continue;

            ref BlobArray<PlayerScalableStatBlob> scalableStats = ref progressionConfig.Config.Value.ScalableStats;

            for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
            {
                ref PlayerScalableStatBlob scalableStat = ref scalableStats[statIndex];
                string statNameString = scalableStat.Name.ToString();
                FixedString64Bytes statName = new FixedString64Bytes(statNameString);
                float resolvedValue = scalableStat.DefaultValue;

                if ((PlayerScalableStatType)scalableStat.Type == PlayerScalableStatType.Integer)
                    resolvedValue = (float)Math.Round(resolvedValue, MidpointRounding.AwayFromZero);

                scalableStatsBuffer.Add(new PlayerScalableStatElement
                {
                    Name = statName,
                    Type = scalableStat.Type,
                    Value = resolvedValue
                });
            }
        }

        entities.Dispose();
        configs.Dispose();
    }

    private static float ResolveScalableStatDefaultValue(PlayerProgressionConfig progressionConfig, string statName, float fallbackValue)
    {
        if (!progressionConfig.Config.IsCreated)
            return fallbackValue;

        ref BlobArray<PlayerScalableStatBlob> scalableStats = ref progressionConfig.Config.Value.ScalableStats;

        for (int index = 0; index < scalableStats.Length; index++)
        {
            ref PlayerScalableStatBlob scalableStat = ref scalableStats[index];
            string scalableStatName = scalableStat.Name.ToString();

            if (!string.Equals(scalableStatName, statName, StringComparison.OrdinalIgnoreCase))
                continue;

            float resolvedValue = scalableStat.DefaultValue;

            if ((PlayerScalableStatType)scalableStat.Type == PlayerScalableStatType.Integer)
                resolvedValue = (float)Math.Round(resolvedValue, MidpointRounding.AwayFromZero);

            return resolvedValue;
        }

        return fallbackValue;
    }

    private static float ResolveExperiencePickupRadius(PlayerProgressionConfig progressionConfig, float fallbackValue)
    {
        if (!progressionConfig.Config.IsCreated)
            return fallbackValue;

        float pickupRadius = progressionConfig.Config.Value.ExperiencePickupRadius;

        if (pickupRadius < 0f)
            return 0f;

        return pickupRadius;
    }
    #endregion

    #endregion
}
