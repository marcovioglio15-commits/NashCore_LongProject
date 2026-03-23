using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Contains slot-level activation flow for active power ups, including checks, costs and healing side effects.
/// </summary>
public static class PlayerPowerUpActivationSlotUtility
{
    #region Methods

    #region Slot Processing
    public static void ProcessSlotInput(in PlayerPowerUpSlotConfig slotConfig,
                                        in PlayerPowerUpSlotConfig otherSlotConfig,
                                        bool isPressed,
                                        bool pressedThisFrame,
                                        bool releasedThisFrame,
                                        float deltaTime,
                                        in LocalTransform localTransform,
                                        in PlayerLookState lookState,
                                        in PlayerMovementState movementState,
                                        in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                        in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                        in PlayerPassiveToolsState passiveToolsState,
                                        in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                        in ComponentLookup<LocalTransform> transformLookup,
                                        in ComponentLookup<LocalToWorld> localToWorldLookup,
                                        float2 moveInput,
                                        float3 lastValidMovementDirection,
                                        ref float slotEnergy,
                                        ref float cooldownRemaining,
                                        ref float charge,
                                        ref byte isCharging,
                                        ref byte isActive,
                                        ref float maintenanceTickTimer,
                                        ref float otherSlotCharge,
                                        ref float otherSlotCooldownRemaining,
                                        ref byte otherSlotIsCharging,
                                        ref byte otherSlotIsActive,
                                        ref float otherSlotMaintenanceTickTimer,
                                        ref byte isShootingSuppressed,
                                        ref PlayerDashState dashState,
                                        ref PlayerBulletTimeState bulletTimeState,
                                        ref PlayerHealOverTimeState healOverTimeState,
                                        DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                        DynamicBuffer<ShootRequest> shootRequests,
                                        Entity playerEntity,
                                        ref ComponentLookup<PlayerHealth> healthLookup,
                                        ref PlayerHealth updatedHealth,
                                        ref bool healthChanged,
                                        ref ComponentLookup<PlayerShield> shieldLookup,
                                        ref PlayerShield updatedShield,
                                        ref bool shieldChanged)
    {
        if (slotConfig.IsDefined == 0)
            return;

        if (slotConfig.ToolKind == ActiveToolKind.ChargeShot)
        {
            PlayerPowerUpChargeAndToggleActivationUtility.ProcessChargeShotSlot(in slotConfig,
                                                                                isPressed,
                                                                                pressedThisFrame,
                                                                                releasedThisFrame,
                                                                                deltaTime,
                                                                                in localTransform,
                                                                                in lookState,
                                                                                in runtimeShootingConfig,
                                                                                in passiveToolsState,
                                                                                in muzzleLookup,
                                                                                in transformLookup,
                                                                                in localToWorldLookup,
                                                                                ref slotEnergy,
                                                                                ref cooldownRemaining,
                                                                                ref charge,
                                                                                ref isCharging,
                                                                                ref isActive,
                                                                                ref maintenanceTickTimer,
                                                                                otherSlotConfig.IsDefined != 0,
                                                                                ref otherSlotCharge,
                                                                                ref otherSlotCooldownRemaining,
                                                                                ref otherSlotIsCharging,
                                                                                ref otherSlotIsActive,
                                                                                ref otherSlotMaintenanceTickTimer,
                                                                                ref isShootingSuppressed,
                                                                                shootRequests,
                                                                                playerEntity,
                                                                                ref healthLookup,
                                                                                ref updatedHealth,
                                                                                ref healthChanged,
                                                                                ref shieldLookup,
                                                                                ref updatedShield,
                                                                                ref shieldChanged,
                                                                                ref dashState,
                                                                                ref bulletTimeState);
            return;
        }

        if (slotConfig.ToolKind == ActiveToolKind.PassiveToggle && slotConfig.Toggleable != 0)
        {
            PlayerPowerUpChargeAndToggleActivationUtility.ProcessPassiveToggleSlot(in slotConfig,
                                                                                   pressedThisFrame,
                                                                                   ref slotEnergy,
                                                                                   ref cooldownRemaining,
                                                                                   ref isActive,
                                                                                   ref maintenanceTickTimer,
                                                                                   otherSlotConfig.IsDefined != 0,
                                                                                   ref otherSlotCharge,
                                                                                   ref otherSlotCooldownRemaining,
                                                                                   ref otherSlotIsCharging,
                                                                                   ref otherSlotIsActive,
                                                                                   ref otherSlotMaintenanceTickTimer,
                                                                                   ref isShootingSuppressed,
                                                                                   playerEntity,
                                                                                   ref healthLookup,
                                                                                   ref updatedHealth,
                                                                                   ref healthChanged,
                                                                                   ref shieldLookup,
                                                                                   ref updatedShield,
                                                                                   ref shieldChanged,
                                                                                   ref dashState,
                                                                                   ref bulletTimeState);
            return;
        }

        isActive = 0;
        maintenanceTickTimer = 0f;
        bool activationTriggered;

        switch (slotConfig.ActivationInputMode)
        {
            case PowerUpActivationInputMode.OnRelease:
                activationTriggered = releasedThisFrame;
                break;
            default:
                activationTriggered = pressedThisFrame;
                break;
        }

        if (!activationTriggered)
            return;

        if (cooldownRemaining > 0f)
            return;

        if (!CanExecuteTool(in slotConfig,
                            in dashState,
                            in bulletTimeState,
                            in healOverTimeState,
                            in movementState,
                            in runtimeMovementConfig,
                            in runtimeShootingConfig,
                            in localTransform,
                            moveInput,
                            lastValidMovementDirection,
                            playerEntity,
                            ref healthLookup,
                            ref updatedHealth,
                            ref healthChanged))
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

        if (slotConfig.InterruptOtherSlotOnEnter != 0 && otherSlotConfig.IsDefined != 0)
            PlayerPowerUpChargeAndToggleActivationUtility.InterruptOtherSlot(in slotConfig,
                                                                             ref otherSlotCharge,
                                                                             ref otherSlotCooldownRemaining,
                                                                             ref otherSlotIsCharging,
                                                                             ref otherSlotIsActive,
                                                                             ref otherSlotMaintenanceTickTimer,
                                                                             ref dashState,
                                                                             ref bulletTimeState);

        if (slotConfig.ToolKind == ActiveToolKind.PortableHealthPack)
            ExecutePortableHealthPack(in slotConfig, playerEntity, ref healthLookup, ref updatedHealth, ref healthChanged, ref healOverTimeState);

        PlayerPowerUpActivationExecutionUtility.ExecuteTool(in slotConfig,
                                                            in localTransform,
                                                            in lookState,
                                                            in movementState,
                                                            in runtimeMovementConfig,
                                                            in runtimeShootingConfig,
                                                            in passiveToolsState,
                                                            in muzzleLookup,
                                                            in transformLookup,
                                                            in localToWorldLookup,
                                                            moveInput,
                                                            lastValidMovementDirection,
                                                            playerEntity,
                                                            ref dashState,
                                                            ref bulletTimeState,
                                                            bombRequests,
                                                            shootRequests);

        if (slotConfig.SuppressBaseShootingWhileActive != 0)
            isShootingSuppressed = 1;

        cooldownRemaining = math.max(0f, slotConfig.CooldownSeconds);
    }

    #endregion

    #region Checks
    private static bool CanExecuteTool(in PlayerPowerUpSlotConfig slotConfig,
                                       in PlayerDashState dashState,
                                       in PlayerBulletTimeState bulletTimeState,
                                       in PlayerHealOverTimeState healOverTimeState,
                                       in PlayerMovementState movementState,
                                       in PlayerRuntimeMovementConfig runtimeMovementConfig,
                                       in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                       in LocalTransform localTransform,
                                       float2 moveInput,
                                       float3 lastValidMovementDirection,
                                       Entity playerEntity,
                                       ref ComponentLookup<PlayerHealth> healthLookup,
                                       ref PlayerHealth updatedHealth,
                                       ref bool healthChanged)
    {
        switch (slotConfig.ToolKind)
        {
            case ActiveToolKind.Bomb:
                return slotConfig.BombPrefabEntity != Entity.Null;
            case ActiveToolKind.Dash:
                if (dashState.IsDashing != 0)
                    return false;

                if (slotConfig.Dash.Duration <= 0f)
                    return false;

                if (slotConfig.Dash.Distance <= 0f)
                    return false;

                if (!PlayerPowerUpActivationExecutionUtility.TryResolveDashActivationDirection(in movementState,
                                                                                               in runtimeMovementConfig,
                                                                                               in localTransform,
                                                                                               moveInput,
                                                                                               lastValidMovementDirection,
                                                                                               out float3 _))
                    return false;

                return true;
            case ActiveToolKind.BulletTime:
                if (slotConfig.BulletTime.Duration <= 0f)
                    return false;

                if (slotConfig.BulletTime.EnemySlowPercent <= 0f)
                    return false;

                return true;
            case ActiveToolKind.Shotgun:
                if (slotConfig.Shotgun.ProjectileCount <= 0)
                    return false;

                if (runtimeShootingConfig.Values.ShootSpeed <= 0f)
                    return false;

                return true;
            case ActiveToolKind.PortableHealthPack:
                if (slotConfig.PortableHealthPack.HealAmount <= 0f)
                    return false;

                if (slotConfig.PortableHealthPack.ApplyMode == PowerUpHealApplicationMode.OverTime &&
                    slotConfig.PortableHealthPack.StackPolicy == PowerUpHealStackPolicy.IgnoreIfActive &&
                    healOverTimeState.IsActive != 0)
                    return false;

                if (!healthChanged)
                {
                    if (!healthLookup.HasComponent(playerEntity))
                        return false;

                    updatedHealth = healthLookup[playerEntity];
                    healthChanged = true;
                }

                if (updatedHealth.Current >= updatedHealth.Max)
                    return false;

                return true;
            case ActiveToolKind.ChargeShot:
                if (slotConfig.ChargeShot.RequiredCharge <= 0f)
                    return false;

                if (slotConfig.ChargeShot.ChargeRatePerSecond <= 0f &&
                    slotConfig.ChargeShot.PassiveChargeGainWhileReleased == 0)
                    return false;

                if (runtimeShootingConfig.Values.ShootSpeed <= 0f)
                    return false;

                return true;
            case ActiveToolKind.PassiveToggle:
                return slotConfig.Toggleable != 0 && slotConfig.TogglePassiveTool.IsDefined != 0;
            default:
                return false;
        }
    }

    #endregion

    #region Side Effects
    private static void ExecutePortableHealthPack(in PlayerPowerUpSlotConfig slotConfig,
                                                  Entity playerEntity,
                                                  ref ComponentLookup<PlayerHealth> healthLookup,
                                                  ref PlayerHealth updatedHealth,
                                                  ref bool healthChanged,
                                                  ref PlayerHealOverTimeState healOverTimeState)
    {
        if (!healthChanged)
        {
            if (!healthLookup.HasComponent(playerEntity))
                return;

            updatedHealth = healthLookup[playerEntity];
            healthChanged = true;
        }

        float healAmount = math.max(0f, slotConfig.PortableHealthPack.HealAmount);

        if (healAmount <= 0f)
            return;

        float missingHealth = math.max(0f, updatedHealth.Max - updatedHealth.Current);

        if (missingHealth <= 0f)
            return;

        if (slotConfig.PortableHealthPack.ApplyMode == PowerUpHealApplicationMode.OverTime)
        {
            ApplyHealOverTime(in slotConfig,
                              healAmount,
                              missingHealth,
                              ref healOverTimeState);
            return;
        }

        updatedHealth.Current += math.min(missingHealth, healAmount);

        if (updatedHealth.Current > updatedHealth.Max)
            updatedHealth.Current = updatedHealth.Max;
    }

    private static void ApplyHealOverTime(in PlayerPowerUpSlotConfig slotConfig,
                                          float requestedHealAmount,
                                          float currentMissingHealth,
                                          ref PlayerHealOverTimeState healOverTimeState)
    {
        float clampedRequestedHeal = math.max(0f, requestedHealAmount);
        float clampedMissingHealth = math.max(0f, currentMissingHealth);
        float totalHeal = math.min(clampedRequestedHeal, clampedMissingHealth);

        if (totalHeal <= 0f)
            return;

        float durationSeconds = math.max(0.05f, slotConfig.PortableHealthPack.DurationSeconds);
        float tickIntervalSeconds = math.max(0.01f, slotConfig.PortableHealthPack.TickIntervalSeconds);
        float healPerSecond = totalHeal / durationSeconds;
        bool hasActiveHot = healOverTimeState.IsActive != 0;

        switch (slotConfig.PortableHealthPack.StackPolicy)
        {
            case PowerUpHealStackPolicy.IgnoreIfActive:
                if (hasActiveHot)
                    return;

                break;
            case PowerUpHealStackPolicy.Additive:
                if (hasActiveHot)
                {
                    healOverTimeState.RemainingTotalHeal += totalHeal;
                    healOverTimeState.RemainingDuration = math.max(healOverTimeState.RemainingDuration, durationSeconds);
                    healOverTimeState.TickIntervalSeconds = math.min(healOverTimeState.TickIntervalSeconds, tickIntervalSeconds);
                    healOverTimeState.HealPerSecond += healPerSecond;
                    healOverTimeState.IsActive = 1;
                    return;
                }

                break;
        }

        healOverTimeState.IsActive = 1;
        healOverTimeState.HealPerSecond = healPerSecond;
        healOverTimeState.RemainingTotalHeal = totalHeal;
        healOverTimeState.RemainingDuration = durationSeconds;
        healOverTimeState.TickIntervalSeconds = tickIntervalSeconds;
        healOverTimeState.TickTimer = 0f;
    }

    #endregion

    #endregion
}
