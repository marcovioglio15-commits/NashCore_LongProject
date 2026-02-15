using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Initializes runtime components required by power-up systems.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerPowerUpsInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingStateQuery;
    private EntityQuery missingPassiveToolsStateQuery;
    private EntityQuery missingDashQuery;
    private EntityQuery missingBombRequestBufferQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfig>();

        missingStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpsState>()
            .Build();

        missingPassiveToolsStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPassiveToolsState>()
            .Build();

        missingDashQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerDashState>()
            .Build();

        missingBombRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerBombSpawnRequest>()
            .Build();
    }

    /// <summary>
    /// Updates the system, adding missing power-up runtime states and buffers
    /// to entities with a PlayerPowerUpsConfig.
    /// </summary>
    /// <param name="state"></param>
    public void OnUpdate(ref SystemState state)
    {
        bool hasMissingState = missingStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPassiveToolsState = missingPassiveToolsStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingDash = missingDashQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingBombRequestBuffer = missingBombRequestBufferQuery.IsEmptyIgnoreFilter == false;

        if (hasMissingState == false &&
            hasMissingPassiveToolsState == false &&
            hasMissingDash == false &&
            hasMissingBombRequestBuffer == false)
            return;

        uint currentKillCount = 0u;

        // if the total kill count is available (meaning the GlobalEnemyKillCounter singleton exists),
        // use it to initialize the LastObservedGlobalKillCount in PlayerPowerUpsState,
        if (SystemAPI.TryGetSingleton<GlobalEnemyKillCounter>(out GlobalEnemyKillCounter killCounter))
            currentKillCount = killCounter.TotalKilled;

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(true);

        if (hasMissingState)
            AddMissingState(ref commandBuffer, currentKillCount);

        if (hasMissingPassiveToolsState)
            AddMissingPassiveToolsState(ref commandBuffer, in equippedPassiveToolsLookup);

        if (hasMissingDash)
            AddMissingDashState(ref commandBuffer);

        if (hasMissingBombRequestBuffer)
            AddMissingBombRequestBuffers(ref commandBuffer);

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private void AddMissingState(ref EntityCommandBuffer commandBuffer, uint currentKillCount)
    {
        NativeArray<Entity> entities = missingStateQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerPowerUpsConfig> configs = missingStateQuery.ToComponentDataArray<PlayerPowerUpsConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            PlayerPowerUpsConfig config = configs[index];
            float primaryMaximumEnergy = math.max(0f, config.PrimarySlot.MaximumEnergy);
            float secondaryMaximumEnergy = math.max(0f, config.SecondarySlot.MaximumEnergy);

            commandBuffer.AddComponent(entities[index], new PlayerPowerUpsState
            {
                PrimaryEnergy = primaryMaximumEnergy,
                SecondaryEnergy = secondaryMaximumEnergy,
                PreviousPrimaryPressed = 0,
                PreviousSecondaryPressed = 0,
                LastObservedGlobalKillCount = currentKillCount,
                LastValidMovementDirection = float3.zero
            });
        }

        entities.Dispose();
        configs.Dispose();
    }

    private void AddMissingPassiveToolsState(ref EntityCommandBuffer commandBuffer, in BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup)
    {
        NativeArray<Entity> entities = missingPassiveToolsStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerPassiveToolsState passiveToolsState = BuildPassiveToolsState(entity, in equippedPassiveToolsLookup);
            commandBuffer.AddComponent(entity, passiveToolsState);
        }

        entities.Dispose();
    }

    private static PlayerPassiveToolsState BuildPassiveToolsState(Entity entity, in BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup)
    {
        PlayerPassiveToolsState passiveToolsState = new PlayerPassiveToolsState
        {
            ProjectileSizeMultiplier = 1f,
            ProjectileDamageMultiplier = 1f,
            ProjectileSpeedMultiplier = 1f,
            ProjectileLifetimeSecondsMultiplier = 1f,
            ProjectileLifetimeRangeMultiplier = 1f
        };

        if (equippedPassiveToolsLookup.HasBuffer(entity) == false)
            return passiveToolsState;

        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = equippedPassiveToolsLookup[entity];

        for (int passiveToolIndex = 0; passiveToolIndex < equippedPassiveToolsBuffer.Length; passiveToolIndex++)
        {
            EquippedPassiveToolElement equippedPassiveTool = equippedPassiveToolsBuffer[passiveToolIndex];
            AccumulatePassiveTool(ref passiveToolsState, in equippedPassiveTool.Tool);
        }

        return passiveToolsState;
    }

    private static void AccumulatePassiveTool(ref PlayerPassiveToolsState passiveToolsState, in PlayerPassiveToolConfig passiveToolConfig)
    {
        if (passiveToolConfig.IsDefined == 0)
            return;

        switch (passiveToolConfig.ToolKind)
        {
            case PassiveToolKind.ProjectileSize:
                passiveToolsState.ProjectileSizeMultiplier *= math.max(0.01f, passiveToolConfig.ProjectileSize.SizeMultiplier);
                passiveToolsState.ProjectileDamageMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.DamageMultiplier);
                passiveToolsState.ProjectileSpeedMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.SpeedMultiplier);
                passiveToolsState.ProjectileLifetimeSecondsMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.LifetimeSecondsMultiplier);
                passiveToolsState.ProjectileLifetimeRangeMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.LifetimeRangeMultiplier);
                return;
        }
    }

    private void AddMissingDashState(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingDashQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerDashState
            {
                IsDashing = 0,
                Phase = 0,
                PhaseRemaining = 0f,
                HoldDuration = 0f,
                RemainingInvulnerability = 0f,
                Direction = float3.zero,
                EntryVelocity = float3.zero,
                Speed = 0f,
                TransitionInDuration = 0f,
                TransitionOutDuration = 0f
            });
        }

        entities.Dispose();
    }

    private void AddMissingBombRequestBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingBombRequestBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<PlayerBombSpawnRequest>(entities[index]);

        entities.Dispose();
    }
    #endregion

    #endregion
}
