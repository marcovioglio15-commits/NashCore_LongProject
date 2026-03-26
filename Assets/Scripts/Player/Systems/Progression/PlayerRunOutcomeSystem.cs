using Unity.Entities;

/// <summary>
/// Computes the authoritative terminal outcome for the current player run without reloading the gameplay scene immediately.
/// Defeat is triggered by zero health, while victory requires every authored enemy wave to complete.
///  None.
/// returns None.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnemySystemGroup))]
public partial struct PlayerRunOutcomeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime state required by run-outcome evaluation.
    ///  state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerRunOutcomeState>();
    }

    /// <summary>
    /// Finalizes defeat or victory once the corresponding gameplay condition is satisfied.
    ///  state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;

        foreach ((RefRW<PlayerRunOutcomeState> runOutcomeState,
                  RefRO<PlayerHealth> playerHealth,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRW<PlayerRunOutcomeState>,
                                    RefRO<PlayerHealth>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            if (runOutcomeState.ValueRO.IsFinalized != 0)
                continue;

            if (entityManager.HasComponent<PlayerRunTimerConfig>(playerEntity) &&
                entityManager.HasComponent<PlayerRunTimerState>(playerEntity))
            {
                PlayerRunTimerConfig timerConfig = entityManager.GetComponentData<PlayerRunTimerConfig>(playerEntity);
                PlayerRunTimerState timerState = entityManager.GetComponentData<PlayerRunTimerState>(playerEntity);

                if (timerConfig.Direction == PlayerRunTimerDirection.Backward && timerState.Expired != 0)
                {
                    FinalizeOutcome(ref runOutcomeState.ValueRW, PlayerRunOutcome.Defeat);
                    continue;
                }
            }

            if (playerHealth.ValueRO.Current <= 0f)
            {
                FinalizeOutcome(ref runOutcomeState.ValueRW, PlayerRunOutcome.Defeat);
                continue;
            }

            bool anySpawnerFound = false;
            bool allSpawnersCompleted = true;
            bool anyWaveFound = false;

            foreach ((RefRO<EnemySpawner> _,
                      RefRO<EnemySpawnerState> spawnerState,
                      DynamicBuffer<EnemySpawnerWaveRuntimeElement> waveRuntimeBuffer)
                     in SystemAPI.Query<RefRO<EnemySpawner>,
                                        RefRO<EnemySpawnerState>,
                                        DynamicBuffer<EnemySpawnerWaveRuntimeElement>>())
            {
                anySpawnerFound = true;

                if (spawnerState.ValueRO.Initialized == 0)
                {
                    allSpawnersCompleted = false;
                    break;
                }

                if (spawnerState.ValueRO.AliveCount > 0)
                {
                    allSpawnersCompleted = false;
                    break;
                }

                if (waveRuntimeBuffer.Length <= 0)
                {
                    allSpawnersCompleted = false;
                    break;
                }

                for (int waveIndex = 0; waveIndex < waveRuntimeBuffer.Length; waveIndex++)
                {
                    anyWaveFound = true;

                    if (waveRuntimeBuffer[waveIndex].Completed != 0)
                        continue;

                    allSpawnersCompleted = false;
                    break;
                }

                if (!allSpawnersCompleted)
                    break;
            }

            if (!anySpawnerFound)
                continue;

            if (!anyWaveFound)
                continue;

            if (!allSpawnersCompleted)
                continue;

            FinalizeOutcome(ref runOutcomeState.ValueRW, PlayerRunOutcome.Victory);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Writes the resolved terminal run outcome once and marks the state as finalized.
    ///  runOutcomeState: Mutable runtime state stored on the local player entity.
    ///  outcome: Terminal outcome that should be exposed to UI.
    /// returns None.
    /// </summary>
    private static void FinalizeOutcome(ref PlayerRunOutcomeState runOutcomeState, PlayerRunOutcome outcome)
    {
        runOutcomeState.Outcome = outcome;
        runOutcomeState.IsFinalized = 1;
        runOutcomeState.RuntimeFreezeApplied = 0;
    }
    #endregion

    #endregion
}
