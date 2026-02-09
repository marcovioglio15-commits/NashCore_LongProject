using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Initializes player progression runtime components from the baked progression config.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerProgressionInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingHealthQuery;
    private EntityQuery missingExperienceQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProgressionConfig>();

        missingHealthQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerProgressionConfig>()
            .WithNone<PlayerHealth>()
            .Build();

        missingExperienceQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerProgressionConfig>()
            .WithNone<PlayerExperience>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        bool hasMissingHealth = missingHealthQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingExperience = missingExperienceQuery.IsEmptyIgnoreFilter == false;

        if (hasMissingHealth == false && hasMissingExperience == false)
            return;

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        if (hasMissingHealth)
            AddMissingHealth(ref state, ref commandBuffer);

        if (hasMissingExperience)
            AddMissingExperience(ref state, ref commandBuffer);

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Initialization
    private void AddMissingHealth(ref SystemState state, ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingHealthQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerProgressionConfig> configs = missingHealthQuery.ToComponentDataArray<PlayerProgressionConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerProgressionConfig config = configs[index];

            if (config.Config.IsCreated == false)
                continue;

            float maxHealth = config.Config.Value.BaseStats.Health;

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

    private void AddMissingExperience(ref SystemState state, ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingExperienceQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerProgressionConfig> configs = missingExperienceQuery.ToComponentDataArray<PlayerProgressionConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerProgressionConfig config = configs[index];

            if (config.Config.IsCreated == false)
                continue;

            float experience = config.Config.Value.BaseStats.Experience;

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
    #endregion

    #endregion
}
