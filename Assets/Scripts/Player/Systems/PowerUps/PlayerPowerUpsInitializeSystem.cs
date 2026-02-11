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

        missingDashQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerDashState>()
            .Build();

        missingBombRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerBombSpawnRequest>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        bool hasMissingState = missingStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingDash = missingDashQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingBombRequestBuffer = missingBombRequestBufferQuery.IsEmptyIgnoreFilter == false;

        if (hasMissingState == false &&
            hasMissingDash == false &&
            hasMissingBombRequestBuffer == false)
            return;

        uint currentKillCount = 0u;

        if (SystemAPI.TryGetSingleton<GlobalEnemyKillCounter>(out GlobalEnemyKillCounter killCounter))
            currentKillCount = killCounter.TotalKilled;

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        if (hasMissingState)
            AddMissingState(ref commandBuffer, currentKillCount);

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
