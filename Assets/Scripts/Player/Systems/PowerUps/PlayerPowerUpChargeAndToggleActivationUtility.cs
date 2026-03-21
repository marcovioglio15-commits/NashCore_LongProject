using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hosts charge-shot and toggle-passive activation helpers extracted from the main slot utility to keep slot orchestration compact.
/// </summary>
internal static class PlayerPowerUpChargeAndToggleActivationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Processes the per-frame runtime for one charge-shot slot, including stored charge, release execution, and optional released-state gain or decay.
    /// /params slotConfig: Slot configuration compiled for a charge-shot active.
    /// /params isPressed: True while the bound slot input remains held.
    /// /params pressedThisFrame: True when the bound slot input was pressed during the current frame.
    /// /params releasedThisFrame: True when the bound slot input was released during the current frame.
    /// /params deltaTime: Current frame delta time.
    /// /params localTransform: Player transform used to emit the projectile burst.
    /// /params lookState: Player look state used to resolve the firing direction.
    /// /params controllerConfig: Player controller config used to resolve projectile defaults.
    /// /params passiveToolsState: Aggregated passive state used to augment spawned projectiles.
    /// /params slotEnergy: Mutable slot resource state.
    /// /params cooldownRemaining: Mutable cooldown state reused to block charge-shot input while cooling down.
    /// /params charge: Mutable stored charge amount.
    /// /params isCharging: Mutable charging flag for the slot.
    /// /params isActive: Mutable active flag reset because charge shots are not persistent toggles.
    /// /params maintenanceTickTimer: Mutable maintenance timer reset because charge shots do not use toggle maintenance.
    /// /params hasOtherSlotDefinition: True when the opposite slot currently contains one defined power-up.
    /// /params otherSlotCharge: Mutable opposite-slot charge state that can be interrupted.
    /// /params otherSlotCooldownRemaining: Mutable opposite-slot cooldown state that can be cleared on hard interruption.
    /// /params otherSlotIsCharging: Mutable opposite-slot charging flag that can be interrupted.
    /// /params otherSlotIsActive: Mutable opposite-slot active flag that can be interrupted.
    /// /params otherSlotMaintenanceTickTimer: Mutable opposite-slot maintenance accumulator that can be interrupted.
    /// /params isShootingSuppressed: Shared per-frame shooting suppression flag updated while charging.
    /// /params shootRequests: Output shoot-request buffer used to spawn the charge-shot burst.
    /// /params playerEntity: Player entity used to resolve activation resources.
    /// /params healthLookup: Health lookup used when the activation resource is Health.
    /// /params updatedHealth: Cached mutable health state reused by the caller.
    /// /params healthChanged: True when updatedHealth already contains a fetched runtime value.
    /// /params shieldLookup: Shield lookup used when the activation resource is Shield.
    /// /params updatedShield: Cached mutable shield state reused by the caller.
    /// /params shieldChanged: True when updatedShield already contains a fetched runtime value.
    /// /params dashState: Mutable dash state interrupted by hard slot interruption rules.
    /// /params bulletTimeState: Mutable bullet-time state interrupted by hard slot interruption rules.
    /// /returns void.
    /// </summary>
    public static void ProcessChargeShotSlot(in PlayerPowerUpSlotConfig slotConfig,
                                             bool isPressed,
                                             bool pressedThisFrame,
                                             bool releasedThisFrame,
                                             float deltaTime,
                                             in LocalTransform localTransform,
                                             in PlayerLookState lookState,
                                             in PlayerControllerConfig controllerConfig,
                                             in PlayerPassiveToolsState passiveToolsState,
                                             in ComponentLookup<PlayerAnimatedMuzzleWorldPose> animatedMuzzleLookup,
                                             in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                             in ComponentLookup<LocalTransform> transformLookup,
                                             in ComponentLookup<LocalToWorld> localToWorldLookup,
                                             ref float slotEnergy,
                                             ref float cooldownRemaining,
                                             ref float charge,
                                             ref byte isCharging,
                                             ref byte isActive,
                                             ref float maintenanceTickTimer,
                                             bool hasOtherSlotDefinition,
                                             ref float otherSlotCharge,
                                             ref float otherSlotCooldownRemaining,
                                             ref byte otherSlotIsCharging,
                                             ref byte otherSlotIsActive,
                                             ref float otherSlotMaintenanceTickTimer,
                                             ref byte isShootingSuppressed,
                                             DynamicBuffer<ShootRequest> shootRequests,
                                             Entity playerEntity,
                                             ref ComponentLookup<PlayerHealth> healthLookup,
                                             ref PlayerHealth updatedHealth,
                                             ref bool healthChanged,
                                             ref ComponentLookup<PlayerShield> shieldLookup,
                                             ref PlayerShield updatedShield,
                                             ref bool shieldChanged,
                                             ref PlayerDashState dashState,
                                             ref PlayerBulletTimeState bulletTimeState)
    {
        isActive = 0;
        maintenanceTickTimer = 0f;

        if (cooldownRemaining > 0f)
        {
            isCharging = 0;
            charge = 0f;
            return;
        }

        float requiredCharge = math.max(0f, slotConfig.ChargeShot.RequiredCharge);
        float maximumCharge = math.max(requiredCharge, slotConfig.ChargeShot.MaximumCharge);
        float chargeRate = math.max(0f, slotConfig.ChargeShot.ChargeRatePerSecond);

        if (requiredCharge <= 0f || maximumCharge <= 0f)
        {
            isCharging = 0;
            charge = 0f;
            return;
        }

        if (charge > maximumCharge)
            charge = maximumCharge;

        if (pressedThisFrame)
            isCharging = 1;

        if (isCharging != 0 && isPressed && chargeRate > 0f)
        {
            charge += chargeRate * math.max(0f, deltaTime);
            charge = math.min(charge, maximumCharge);
        }

        if (isCharging != 0 && isPressed)
        {
            if (slotConfig.ChargeShot.SuppressBaseShootingWhileCharging != 0)
                isShootingSuppressed = 1;

            if (slotConfig.SuppressBaseShootingWhileActive != 0)
                isShootingSuppressed = 1;
        }

        if (releasedThisFrame && isCharging != 0)
        {
            isCharging = 0;

            bool hasEnoughCharge = charge + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon >= requiredCharge;
            bool canPayActivationCost = PlayerPowerUpResourceCostUtility.CanPayActivationCost(in slotConfig,
                                                                                              slotEnergy,
                                                                                              playerEntity,
                                                                                              ref healthLookup,
                                                                                              ref updatedHealth,
                                                                                              ref healthChanged,
                                                                                              ref shieldLookup,
                                                                                              ref updatedShield,
                                                                                              ref shieldChanged);

            if (hasEnoughCharge && canPayActivationCost)
            {
                PlayerPowerUpResourceCostUtility.ConsumeActivationCost(in slotConfig,
                                                                       ref slotEnergy,
                                                                       playerEntity,
                                                                       ref healthLookup,
                                                                       ref updatedHealth,
                                                                       ref healthChanged,
                                                                       ref shieldLookup,
                                                                       ref updatedShield,
                                                                       ref shieldChanged);

                if (slotConfig.InterruptOtherSlotOnEnter != 0 && hasOtherSlotDefinition)
                    InterruptOtherSlot(in slotConfig,
                                       ref otherSlotCharge,
                                       ref otherSlotCooldownRemaining,
                                       ref otherSlotIsCharging,
                                       ref otherSlotIsActive,
                                       ref otherSlotMaintenanceTickTimer,
                                       ref dashState,
                                       ref bulletTimeState);

                PlayerPowerUpActivationExecutionUtility.ExecuteChargeShot(in slotConfig,
                                                                          in localTransform,
                                                                          in lookState,
                                                                          in controllerConfig,
                                                                          in passiveToolsState,
                                                                          playerEntity,
                                                                          in animatedMuzzleLookup,
                                                                          in muzzleLookup,
                                                                          in transformLookup,
                                                                          in localToWorldLookup,
                                                                          shootRequests);

                cooldownRemaining = math.max(0f, slotConfig.CooldownSeconds);
                charge = 0f;
                return;
            }
        }

        if (isPressed)
            return;

        TickReleasedChargeState(in slotConfig.ChargeShot,
                                deltaTime,
                                maximumCharge,
                                ref charge);
    }

    /// <summary>
    /// Processes one press-to-toggle passive slot, handling activation, deactivation, and cross-slot interruption.
    /// /params slotConfig: Slot configuration compiled as a passive toggle active tool.
    /// /params pressedThisFrame: True when the bound slot input was pressed during the current frame.
    /// /params slotEnergy: Mutable slot resource state.
    /// /params cooldownRemaining: Mutable startup-lock timer reused from the slot cooldown state.
    /// /params isActive: Mutable active flag tracking whether the passive effect is currently enabled.
    /// /params maintenanceTickTimer: Mutable maintenance accumulator reset on activation and deactivation.
    /// /params hasOtherSlotDefinition: True when the opposite slot currently contains one defined power-up.
    /// /params otherSlotCharge: Mutable opposite-slot charge state that can be interrupted.
    /// /params otherSlotCooldownRemaining: Mutable opposite-slot cooldown state that can be cleared on hard interruption.
    /// /params otherSlotIsCharging: Mutable opposite-slot charging flag that can be interrupted.
    /// /params otherSlotIsActive: Mutable opposite-slot active flag that can be interrupted.
    /// /params otherSlotMaintenanceTickTimer: Mutable opposite-slot maintenance accumulator that can be interrupted.
    /// /params isShootingSuppressed: Shared per-frame shooting suppression flag updated when the toggle remains active.
    /// /params playerEntity: Player entity used to resolve activation resources.
    /// /params healthLookup: Health lookup used when the activation resource is Health.
    /// /params updatedHealth: Cached mutable health state reused by the caller.
    /// /params healthChanged: True when updatedHealth already contains a fetched runtime value.
    /// /params shieldLookup: Shield lookup used when the activation resource is Shield.
    /// /params updatedShield: Cached mutable shield state reused by the caller.
    /// /params shieldChanged: True when updatedShield already contains a fetched runtime value.
    /// /params dashState: Mutable dash state interrupted by hard slot interruption rules.
    /// /params bulletTimeState: Mutable bullet-time state interrupted by hard slot interruption rules.
    /// /returns void.
    /// </summary>
    public static void ProcessPassiveToggleSlot(in PlayerPowerUpSlotConfig slotConfig,
                                                bool pressedThisFrame,
                                                ref float slotEnergy,
                                                ref float cooldownRemaining,
                                                ref byte isActive,
                                                ref float maintenanceTickTimer,
                                                bool hasOtherSlotDefinition,
                                                ref float otherSlotCharge,
                                                ref float otherSlotCooldownRemaining,
                                                ref byte otherSlotIsCharging,
                                                ref byte otherSlotIsActive,
                                                ref float otherSlotMaintenanceTickTimer,
                                                ref byte isShootingSuppressed,
                                                Entity playerEntity,
                                                ref ComponentLookup<PlayerHealth> healthLookup,
                                                ref PlayerHealth updatedHealth,
                                                ref bool healthChanged,
                                                ref ComponentLookup<PlayerShield> shieldLookup,
                                                ref PlayerShield updatedShield,
                                                ref bool shieldChanged,
                                                ref PlayerDashState dashState,
                                                ref PlayerBulletTimeState bulletTimeState)
    {
        if (isActive != 0)
        {
            if (slotConfig.SuppressBaseShootingWhileActive != 0)
                isShootingSuppressed = 1;

            if (!pressedThisFrame || cooldownRemaining > 0f)
                return;

            isActive = 0;
            maintenanceTickTimer = 0f;
            cooldownRemaining = 0f;
            return;
        }

        maintenanceTickTimer = 0f;

        if (!pressedThisFrame || slotConfig.TogglePassiveTool.IsDefined == 0)
            return;

        if (!PlayerPowerUpResourceCostUtility.CanPayActivationCost(in slotConfig,
                                                                   slotEnergy,
                                                                   playerEntity,
                                                                   ref healthLookup,
                                                                   ref updatedHealth,
                                                                   ref healthChanged,
                                                                   ref shieldLookup,
                                                                   ref updatedShield,
                                                                   ref shieldChanged))
            return;

        PlayerPowerUpResourceCostUtility.ConsumeActivationCost(in slotConfig,
                                                               ref slotEnergy,
                                                               playerEntity,
                                                               ref healthLookup,
                                                               ref updatedHealth,
                                                               ref healthChanged,
                                                               ref shieldLookup,
                                                               ref updatedShield,
                                                               ref shieldChanged);

        if (slotConfig.InterruptOtherSlotOnEnter != 0 && hasOtherSlotDefinition)
            InterruptOtherSlot(in slotConfig,
                               ref otherSlotCharge,
                               ref otherSlotCooldownRemaining,
                               ref otherSlotIsCharging,
                               ref otherSlotIsActive,
                               ref otherSlotMaintenanceTickTimer,
                               ref dashState,
                               ref bulletTimeState);

        isActive = 1;
        maintenanceTickTimer = 0f;
        cooldownRemaining = math.max(0f, slotConfig.CooldownSeconds);

        if (slotConfig.SuppressBaseShootingWhileActive != 0)
            isShootingSuppressed = 1;
    }

    /// <summary>
    /// Interrupts opposite-slot charging or, when configured, the full active runtime state.
    /// /params slotConfig: Slot configuration driving the interruption rules.
    /// /params otherSlotCharge: Mutable opposite-slot charge state.
    /// /params otherSlotCooldownRemaining: Mutable opposite-slot cooldown state.
    /// /params otherSlotIsCharging: Mutable opposite-slot charging flag.
    /// /params otherSlotIsActive: Mutable opposite-slot active flag.
    /// /params otherSlotMaintenanceTickTimer: Mutable opposite-slot maintenance accumulator.
    /// /params dashState: Mutable dash state interrupted by hard slot interruption rules.
    /// /params bulletTimeState: Mutable bullet-time state interrupted by hard slot interruption rules.
    /// /returns void.
    /// </summary>
    public static void InterruptOtherSlot(in PlayerPowerUpSlotConfig slotConfig,
                                          ref float otherSlotCharge,
                                          ref float otherSlotCooldownRemaining,
                                          ref byte otherSlotIsCharging,
                                          ref byte otherSlotIsActive,
                                          ref float otherSlotMaintenanceTickTimer,
                                          ref PlayerDashState dashState,
                                          ref PlayerBulletTimeState bulletTimeState)
    {
        otherSlotCharge = 0f;
        otherSlotIsCharging = 0;

        if (slotConfig.InterruptOtherSlotChargingOnly != 0)
            return;

        otherSlotCooldownRemaining = 0f;
        otherSlotIsActive = 0;
        otherSlotMaintenanceTickTimer = 0f;
        dashState.IsDashing = 0;
        dashState.Phase = 0;
        dashState.PhaseRemaining = 0f;
        dashState.HoldDuration = 0f;
        dashState.RemainingInvulnerability = 0f;
        dashState.Direction = float3.zero;
        dashState.EntryVelocity = float3.zero;
        dashState.Speed = 0f;
        dashState.TransitionInDuration = 0f;
        dashState.TransitionOutDuration = 0f;
        bulletTimeState.RemainingDuration = 0f;
        bulletTimeState.SlowPercent = 0f;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Updates released charge storage using the optional passive gain and decay settings.
    /// /params chargeShotConfig: Charge-shot payload containing released-state gain and decay options.
    /// /params deltaTime: Current frame delta time.
    /// /params maximumCharge: Maximum charge cap used to convert percentages into absolute amounts.
    /// /params charge: Mutable stored charge amount.
    /// /returns void.
    /// </summary>
    private static void TickReleasedChargeState(in ChargeShotPowerUpConfig chargeShotConfig,
                                                float deltaTime,
                                                float maximumCharge,
                                                ref float charge)
    {
        float safeDeltaTime = math.max(0f, deltaTime);
        float chargeDelta = 0f;

        if (chargeShotConfig.PassiveChargeGainWhileReleased != 0)
            chargeDelta += maximumCharge * (math.max(0f, chargeShotConfig.PassiveChargeGainPercentPerSecond) * 0.01f) * safeDeltaTime;

        if (chargeShotConfig.DecayAfterRelease != 0)
            chargeDelta -= maximumCharge * (math.max(0f, chargeShotConfig.DecayAfterReleasePercentPerSecond) * 0.01f) * safeDeltaTime;

        if (chargeDelta == 0f)
        {
            charge = 0f;
            return;
        }

        charge = math.clamp(charge + chargeDelta, 0f, maximumCharge);
    }
    #endregion

    #endregion
}
