using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies maintenance and passive-state aggregation for active slots that toggle passive-compatible effects.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
[UpdateBefore(typeof(PlayerBulletTimeUpdateSystem))]
[UpdateBefore(typeof(PlayerMovementSpeedSystem))]
public partial struct PlayerPowerUpTogglePassiveSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime data required by toggleable passive power-ups.
    /// /params state: Current ECS system state.
    /// /returns void.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<PlayerBulletTimeState>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
    }

    /// <summary>
    /// Updates toggle startup timers, maintenance ticks, and the aggregated passive state snapshot.
    /// /params state: Current ECS system state.
    /// /returns void.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);

        foreach ((RefRO<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                  RefRW<PlayerPassiveToolsState> passiveToolsState,
                  RefRW<PlayerBulletTimeState> bulletTimeState,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerPowerUpsConfig>,
                                    RefRW<PlayerPowerUpsState>,
                                    DynamicBuffer<EquippedPassiveToolElement>,
                                    RefRW<PlayerPassiveToolsState>,
                                    RefRW<PlayerBulletTimeState>>()
                             .WithEntityAccess())
        {
            PlayerPassiveToolsState aggregatedPassiveToolsState = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(equippedPassiveTools);
            PlayerBulletTimeState currentBulletTimeState = bulletTimeState.ValueRO;
            byte isShootingSuppressed = powerUpsState.ValueRO.IsShootingSuppressed;
            bool healthChanged = false;
            PlayerHealth updatedHealth = default;
            bool shieldChanged = false;
            PlayerShield updatedShield = default;
            float primaryEnergy = powerUpsState.ValueRO.PrimaryEnergy;
            float secondaryEnergy = powerUpsState.ValueRO.SecondaryEnergy;
            float primaryCooldownRemaining = powerUpsState.ValueRO.PrimaryCooldownRemaining;
            float secondaryCooldownRemaining = powerUpsState.ValueRO.SecondaryCooldownRemaining;
            byte primaryIsActive = powerUpsState.ValueRO.PrimaryIsActive;
            byte secondaryIsActive = powerUpsState.ValueRO.SecondaryIsActive;
            float primaryMaintenanceTickTimer = powerUpsState.ValueRO.PrimaryMaintenanceTickTimer;
            float secondaryMaintenanceTickTimer = powerUpsState.ValueRO.SecondaryMaintenanceTickTimer;
            float toggleBulletTimeSlowPercent = 0f;
            float toggleBulletTimeTransitionTimeSeconds = 0f;

            ProcessTogglePassiveSlot(in powerUpsConfig.ValueRO.PrimarySlot,
                                     deltaTime,
                                     playerEntity,
                                     ref primaryEnergy,
                                     ref primaryCooldownRemaining,
                                     ref primaryIsActive,
                                     ref primaryMaintenanceTickTimer,
                                     ref aggregatedPassiveToolsState,
                                     ref isShootingSuppressed,
                                     ref healthLookup,
                                     ref updatedHealth,
                                     ref healthChanged,
                                     ref shieldLookup,
                                     ref updatedShield,
                                     ref shieldChanged,
                                     ref toggleBulletTimeSlowPercent,
                                     ref toggleBulletTimeTransitionTimeSeconds);
            ProcessTogglePassiveSlot(in powerUpsConfig.ValueRO.SecondarySlot,
                                     deltaTime,
                                     playerEntity,
                                     ref secondaryEnergy,
                                     ref secondaryCooldownRemaining,
                                     ref secondaryIsActive,
                                     ref secondaryMaintenanceTickTimer,
                                     ref aggregatedPassiveToolsState,
                                     ref isShootingSuppressed,
                                     ref healthLookup,
                                     ref updatedHealth,
                                     ref healthChanged,
                                     ref shieldLookup,
                                     ref updatedShield,
                                     ref shieldChanged,
                                     ref toggleBulletTimeSlowPercent,
                                     ref toggleBulletTimeTransitionTimeSeconds);

            if (healthChanged)
                healthLookup[playerEntity] = updatedHealth;

            if (shieldChanged)
                shieldLookup[playerEntity] = updatedShield;

            powerUpsState.ValueRW.PrimaryEnergy = primaryEnergy;
            powerUpsState.ValueRW.SecondaryEnergy = secondaryEnergy;
            powerUpsState.ValueRW.PrimaryCooldownRemaining = primaryCooldownRemaining;
            powerUpsState.ValueRW.SecondaryCooldownRemaining = secondaryCooldownRemaining;
            powerUpsState.ValueRW.PrimaryIsActive = primaryIsActive;
            powerUpsState.ValueRW.SecondaryIsActive = secondaryIsActive;
            powerUpsState.ValueRW.PrimaryMaintenanceTickTimer = primaryMaintenanceTickTimer;
            powerUpsState.ValueRW.SecondaryMaintenanceTickTimer = secondaryMaintenanceTickTimer;
            powerUpsState.ValueRW.IsShootingSuppressed = isShootingSuppressed;
            passiveToolsState.ValueRW = aggregatedPassiveToolsState;

            if (toggleBulletTimeSlowPercent <= 0f && currentBulletTimeState.ToggleSlowPercent > 0f)
                toggleBulletTimeTransitionTimeSeconds = math.max(toggleBulletTimeTransitionTimeSeconds,
                                                                 currentBulletTimeState.ToggleTransitionTimeSeconds);

            currentBulletTimeState.ToggleSlowPercent = toggleBulletTimeSlowPercent;
            currentBulletTimeState.ToggleTransitionTimeSeconds = math.max(0f, toggleBulletTimeTransitionTimeSeconds);
            bulletTimeState.ValueRW = currentBulletTimeState;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Applies one slot toggle runtime step including startup timing, maintenance, and passive aggregation.
    /// /params slotConfig: Slot configuration inspected for toggle maintenance settings.
    /// /params deltaTime: Current frame delta time.
    /// /params playerEntity: Player entity used for health and shield resource access.
    /// /params slotEnergy: Mutable slot energy state.
    /// /params cooldownRemaining: Mutable slot timer used as toggle startup lock while active.
    /// /params isActive: Mutable toggle-active flag for the slot.
    /// /params maintenanceTickTimer: Mutable accumulated maintenance timer.
    /// /params passiveToolsState: Aggregated passive state updated with the slot payload when active.
    /// /params isShootingSuppressed: Mutable shared shooting suppression flag for the current player frame.
    /// /params healthLookup: Health lookup used for non-energy maintenance costs.
    /// /params updatedHealth: Cached mutable health value reused within the current caller.
    /// /params healthChanged: True when updatedHealth already contains a fetched runtime value.
    /// /params shieldLookup: Shield lookup used for shield maintenance costs.
    /// /params updatedShield: Cached mutable shield value reused within the current caller.
    /// /params shieldChanged: True when updatedShield already contains a fetched runtime value.
    /// /returns void.
    /// </summary>
    private static void ProcessTogglePassiveSlot(in PlayerPowerUpSlotConfig slotConfig,
                                                 float deltaTime,
                                                 Entity playerEntity,
                                                 ref float slotEnergy,
                                                 ref float cooldownRemaining,
                                                 ref byte isActive,
                                                 ref float maintenanceTickTimer,
                                                 ref PlayerPassiveToolsState passiveToolsState,
                                                 ref byte isShootingSuppressed,
                                                 ref ComponentLookup<PlayerHealth> healthLookup,
                                                 ref PlayerHealth updatedHealth,
                                                 ref bool healthChanged,
                                                 ref ComponentLookup<PlayerShield> shieldLookup,
                                                 ref PlayerShield updatedShield,
                                                 ref bool shieldChanged,
                                                 ref float toggleBulletTimeSlowPercent,
                                                 ref float toggleBulletTimeTransitionTimeSeconds)
    {
        if (slotConfig.IsDefined == 0 || slotConfig.ToolKind != ActiveToolKind.PassiveToggle || slotConfig.Toggleable == 0)
        {
            isActive = 0;
            maintenanceTickTimer = 0f;
            return;
        }

        if (isActive == 0)
        {
            maintenanceTickTimer = 0f;
            return;
        }

        if (slotConfig.SuppressBaseShootingWhileActive != 0)
            isShootingSuppressed = 1;

        if (cooldownRemaining <= 0f)
            ApplyMaintenanceTicks(in slotConfig,
                                  deltaTime,
                                  playerEntity,
                                  ref slotEnergy,
                                  ref cooldownRemaining,
                                  ref isActive,
                                  ref maintenanceTickTimer,
                                  ref healthLookup,
                                  ref updatedHealth,
                                  ref healthChanged,
                                  ref shieldLookup,
                                  ref updatedShield,
                                  ref shieldChanged);

        if (isActive == 0 || slotConfig.TogglePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig togglePassiveTool = slotConfig.TogglePassiveTool;

        if (togglePassiveTool.HasBulletTime != 0 && togglePassiveTool.BulletTime.EnemySlowPercent > 0f)
        {
            float slowPercent = math.clamp(togglePassiveTool.BulletTime.EnemySlowPercent, 0f, 100f);
            float transitionTimeSeconds = math.max(0f, togglePassiveTool.BulletTime.TransitionTimeSeconds);

            if (slowPercent > toggleBulletTimeSlowPercent)
            {
                toggleBulletTimeSlowPercent = slowPercent;
                toggleBulletTimeTransitionTimeSeconds = transitionTimeSeconds;
            }
            else if (math.abs(slowPercent - toggleBulletTimeSlowPercent) <= 0.0001f)
            {
                toggleBulletTimeTransitionTimeSeconds = math.max(toggleBulletTimeTransitionTimeSeconds, transitionTimeSeconds);
            }

            togglePassiveTool.HasBulletTime = 0;
            togglePassiveTool.BulletTime = default;
        }

        PlayerPassiveToolsAggregationUtility.AccumulatePassiveTool(ref passiveToolsState, in togglePassiveTool);
    }

    /// <summary>
    /// Applies maintenance ticks after the startup interval has elapsed and deactivates the slot when payment fails.
    /// /params slotConfig: Slot configuration containing maintenance settings.
    /// /params deltaTime: Current frame delta time.
    /// /params playerEntity: Player entity used for health and shield resource access.
    /// /params slotEnergy: Mutable slot energy state.
    /// /params cooldownRemaining: Mutable startup timer reset when the slot deactivates.
    /// /params isActive: Mutable toggle-active flag for the slot.
    /// /params maintenanceTickTimer: Mutable accumulated maintenance timer.
    /// /params healthLookup: Health lookup used for non-energy maintenance costs.
    /// /params updatedHealth: Cached mutable health value reused within the current caller.
    /// /params healthChanged: True when updatedHealth already contains a fetched runtime value.
    /// /params shieldLookup: Shield lookup used for shield maintenance costs.
    /// /params updatedShield: Cached mutable shield value reused within the current caller.
    /// /params shieldChanged: True when updatedShield already contains a fetched runtime value.
    /// /returns void.
    /// </summary>
    private static void ApplyMaintenanceTicks(in PlayerPowerUpSlotConfig slotConfig,
                                              float deltaTime,
                                              Entity playerEntity,
                                              ref float slotEnergy,
                                              ref float cooldownRemaining,
                                              ref byte isActive,
                                              ref float maintenanceTickTimer,
                                              ref ComponentLookup<PlayerHealth> healthLookup,
                                              ref PlayerHealth updatedHealth,
                                              ref bool healthChanged,
                                              ref ComponentLookup<PlayerShield> shieldLookup,
                                              ref PlayerShield updatedShield,
                                              ref bool shieldChanged)
    {
        float maintenanceCostPerSecond = math.max(0f, slotConfig.MaintenanceCostPerSecond);
        float maintenanceTicksPerSecond = math.max(0f, slotConfig.MaintenanceTicksPerSecond);

        if (maintenanceCostPerSecond <= 0f || maintenanceTicksPerSecond <= 0f || slotConfig.MaintenanceResource == PowerUpResourceType.None)
            return;

        float tickIntervalSeconds = 1f / maintenanceTicksPerSecond;
        float maintenanceCostPerTick = maintenanceCostPerSecond / maintenanceTicksPerSecond;
        maintenanceTickTimer += math.max(0f, deltaTime);

        while (maintenanceTickTimer + 1e-6f >= tickIntervalSeconds)
        {
            if (!PlayerPowerUpResourceCostUtility.CanPayFlatResourceCost(slotConfig.MaintenanceResource,
                                                                         maintenanceCostPerTick,
                                                                         slotEnergy,
                                                                         playerEntity,
                                                                         ref healthLookup,
                                                                         ref updatedHealth,
                                                                         ref healthChanged,
                                                                         ref shieldLookup,
                                                                         ref updatedShield,
                                                                         ref shieldChanged))
            {
                isActive = 0;
                cooldownRemaining = 0f;
                maintenanceTickTimer = 0f;
                return;
            }

            PlayerPowerUpResourceCostUtility.ConsumeFlatResourceCost(slotConfig.MaintenanceResource,
                                                                     maintenanceCostPerTick,
                                                                     ref slotEnergy,
                                                                     playerEntity,
                                                                     ref healthLookup,
                                                                     ref updatedHealth,
                                                                     ref healthChanged,
                                                                     ref shieldLookup,
                                                                     ref updatedShield,
                                                                     ref shieldChanged);
            maintenanceTickTimer -= tickIntervalSeconds;
        }
    }
    #endregion

    #endregion
}
