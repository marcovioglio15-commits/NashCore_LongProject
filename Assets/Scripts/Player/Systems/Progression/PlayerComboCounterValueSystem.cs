using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Tracks consecutive enemy kills and resets the combo when the player takes configured damage types.
/// none.
/// returns none.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpCharacterTuningInitializeSystem))]
[UpdateAfter(typeof(PlayerLevelUpSystem))]
[UpdateAfter(typeof(PlayerMilestonePowerUpSelectionResolveSystem))]
[UpdateBefore(typeof(PlayerRuntimeScalingSyncSystem))]
public partial struct PlayerComboCounterValueSystem : ISystem
{
    #region Constants
    private const float DamageComparisonEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime components required to maintain combo state.
    /// state: Current ECS system state.
    /// returns void.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerComboCounterState>();
        state.RequireForUpdate<PlayerRuntimeComboCounterConfig>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerShield>();
    }

    /// <summary>
    /// Updates combo values once per simulation tick using the previous enemy-frame kill buffer and latest survivability state.
    /// state: Current ECS system state.
    /// returns void.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        int killedEnemiesCount = 0;

        if (SystemAPI.TryGetSingletonBuffer<EnemyKilledEventElement>(out DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer))
        {
            killedEnemiesCount = killedEventsBuffer.Length;
        }

        foreach ((RefRW<PlayerComboCounterState> comboCounterState,
                  RefRO<PlayerRuntimeComboCounterConfig> runtimeComboConfig,
                  RefRO<PlayerHealth> playerHealth,
                  RefRO<PlayerShield> playerShield)
                 in SystemAPI.Query<RefRW<PlayerComboCounterState>,
                                    RefRO<PlayerRuntimeComboCounterConfig>,
                                    RefRO<PlayerHealth>,
                                    RefRO<PlayerShield>>())
        {
            UpdateComboState(ref comboCounterState.ValueRW,
                             in runtimeComboConfig.ValueRO,
                             in playerHealth.ValueRO,
                             in playerShield.ValueRO,
                             killedEnemiesCount);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies damage-break and kill-gain rules to one player combo state.
    /// comboCounterState: Mutable combo state updated in place.
    /// runtimeComboConfig: Current runtime combo config.
    /// playerHealth: Current player health values.
    /// playerShield: Current player shield values.
    /// killedEnemiesCount: Number of enemies killed during the previous enemy update.
    /// returns void.
    /// </summary>
    private static void UpdateComboState(ref PlayerComboCounterState comboCounterState,
                                         in PlayerRuntimeComboCounterConfig runtimeComboConfig,
                                         in PlayerHealth playerHealth,
                                         in PlayerShield playerShield,
                                         int killedEnemiesCount)
    {
        float currentHealth = math.max(0f, playerHealth.Current);
        float currentShield = math.max(0f, playerShield.Current);

        if (comboCounterState.Initialized == 0)
        {
            comboCounterState.CurrentValue = math.max(0, comboCounterState.CurrentValue);
            comboCounterState.PreviousObservedHealth = currentHealth;
            comboCounterState.PreviousObservedShield = currentShield;
            comboCounterState.Initialized = 1;
            return;
        }

        bool healthDamageTaken = currentHealth + DamageComparisonEpsilon < comboCounterState.PreviousObservedHealth;
        bool shieldDamageTaken = currentShield + DamageComparisonEpsilon < comboCounterState.PreviousObservedShield;
        int currentComboValue = math.max(0, comboCounterState.CurrentValue);

        if (runtimeComboConfig.Enabled == 0 || runtimeComboConfig.ComboGainPerKill <= 0 || currentHealth <= 0f)
        {
            currentComboValue = 0;
        }
        else
        {
            if (healthDamageTaken || runtimeComboConfig.ShieldDamageBreaksCombo != 0 && shieldDamageTaken)
            {
                currentComboValue = 0;
            }

            if (killedEnemiesCount > 0)
            {
                currentComboValue = AddKillGain(currentComboValue,
                                                runtimeComboConfig.ComboGainPerKill,
                                                killedEnemiesCount);
            }
        }

        comboCounterState.CurrentValue = currentComboValue;
        comboCounterState.PreviousObservedHealth = currentHealth;
        comboCounterState.PreviousObservedShield = currentShield;
    }

    /// <summary>
    /// Adds combo gain from one kill batch while protecting against integer overflow.
    /// currentComboValue: Current combo numeric value.
    /// comboGainPerKill: Gain added per enemy kill.
    /// killedEnemiesCount: Kill batch size.
    /// returns Saturated combo value after kill gain.
    /// </summary>
    private static int AddKillGain(int currentComboValue,
                                   int comboGainPerKill,
                                   int killedEnemiesCount)
    {
        long resolvedValue = (long)math.max(0, currentComboValue) + (long)math.max(0, comboGainPerKill) * math.max(0, killedEnemiesCount);

        if (resolvedValue >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)resolvedValue;
    }
    #endregion

    #endregion
}
