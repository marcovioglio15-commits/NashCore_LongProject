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
                                        in PlayerControllerConfig controllerConfig,
                                        in PlayerPassiveToolsState passiveToolsState,
                                        float2 moveInput,
                                        float3 lastValidMovementDirection,
                                        ref float slotEnergy,
                                        ref float cooldownRemaining,
                                        ref float charge,
                                        ref byte isCharging,
                                        ref float otherSlotCharge,
                                        ref byte otherSlotIsCharging,
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
            ProcessChargeShotSlot(in slotConfig,
                                  isPressed,
                                  pressedThisFrame,
                                  releasedThisFrame,
                                  deltaTime,
                                  in localTransform,
                                  in lookState,
                                  in movementState,
                                  in controllerConfig,
                                  in passiveToolsState,
                                  ref slotEnergy,
                                  ref cooldownRemaining,
                                  ref charge,
                                  ref isCharging,
                                  otherSlotConfig.IsDefined != 0,
                                  ref otherSlotCharge,
                                  ref otherSlotIsCharging,
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
                            in controllerConfig,
                            in localTransform,
                            moveInput,
                            lastValidMovementDirection,
                            playerEntity,
                            ref healthLookup,
                            ref updatedHealth,
                            ref healthChanged))
            return;

        if (!CanPayActivationCost(in slotConfig,
                                  slotEnergy,
                                  playerEntity,
                                  ref healthLookup,
                                  ref updatedHealth,
                                  ref healthChanged,
                                  ref shieldLookup,
                                  ref updatedShield,
                                  ref shieldChanged))
            return;

        ConsumeActivationCost(in slotConfig,
                              ref slotEnergy,
                              playerEntity,
                              ref healthLookup,
                              ref updatedHealth,
                              ref healthChanged,
                              ref shieldLookup,
                              ref updatedShield,
                              ref shieldChanged);

        if (slotConfig.InterruptOtherSlotOnEnter != 0 && otherSlotConfig.IsDefined != 0)
            InterruptOtherSlot(in slotConfig,
                               ref otherSlotCharge,
                               ref otherSlotIsCharging,
                               ref dashState,
                               ref bulletTimeState);

        if (slotConfig.ToolKind == ActiveToolKind.PortableHealthPack)
            ExecutePortableHealthPack(in slotConfig, playerEntity, ref healthLookup, ref updatedHealth, ref healthChanged, ref healOverTimeState);

        PlayerPowerUpActivationExecutionUtility.ExecuteTool(in slotConfig,
                                                            in localTransform,
                                                            in lookState,
                                                            in movementState,
                                                            in controllerConfig,
                                                            in passiveToolsState,
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

    private static void ProcessChargeShotSlot(in PlayerPowerUpSlotConfig slotConfig,
                                              bool isPressed,
                                              bool pressedThisFrame,
                                              bool releasedThisFrame,
                                              float deltaTime,
                                              in LocalTransform localTransform,
                                              in PlayerLookState lookState,
                                              in PlayerMovementState movementState,
                                              in PlayerControllerConfig controllerConfig,
                                              in PlayerPassiveToolsState passiveToolsState,
                                              ref float slotEnergy,
                                              ref float cooldownRemaining,
                                              ref float charge,
                                              ref byte isCharging,
                                              bool hasOtherSlotDefinition,
                                              ref float otherSlotCharge,
                                              ref byte otherSlotIsCharging,
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
        if (cooldownRemaining > 0f)
        {
            isCharging = 0;
            charge = 0f;
            return;
        }

        float requiredCharge = math.max(0f, slotConfig.ChargeShot.RequiredCharge);
        float maximumCharge = math.max(requiredCharge, slotConfig.ChargeShot.MaximumCharge);
        float chargeRate = math.max(0f, slotConfig.ChargeShot.ChargeRatePerSecond);

        if (requiredCharge <= 0f)
            return;

        if (chargeRate <= 0f)
            return;

        if (pressedThisFrame)
        {
            if (!CanPayActivationCost(in slotConfig,
                                      slotEnergy,
                                      playerEntity,
                                      ref healthLookup,
                                      ref updatedHealth,
                                      ref healthChanged,
                                      ref shieldLookup,
                                      ref updatedShield,
                                      ref shieldChanged))
            {
                isCharging = 0;
                charge = 0f;
                return;
            }

            isCharging = 1;
            charge = 0f;
        }

        if (isCharging != 0 && isPressed)
        {
            if (!CanPayActivationCost(in slotConfig,
                                      slotEnergy,
                                      playerEntity,
                                      ref healthLookup,
                                      ref updatedHealth,
                                      ref healthChanged,
                                      ref shieldLookup,
                                      ref updatedShield,
                                      ref shieldChanged))
            {
                isCharging = 0;
                charge = 0f;
                return;
            }

            charge += chargeRate * math.max(0f, deltaTime);

            if (charge > maximumCharge)
                charge = maximumCharge;

            if (slotConfig.ChargeShot.SuppressBaseShootingWhileCharging != 0)
                isShootingSuppressed = 1;

            if (slotConfig.SuppressBaseShootingWhileActive != 0)
                isShootingSuppressed = 1;
        }

        if (!releasedThisFrame)
            return;

        if (isCharging == 0)
            return;

        bool hasEnoughCharge = charge + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon >= requiredCharge;

        if (hasEnoughCharge &&
            CanPayActivationCost(in slotConfig,
                                 slotEnergy,
                                 playerEntity,
                                 ref healthLookup,
                                 ref updatedHealth,
                                 ref healthChanged,
                                 ref shieldLookup,
                                 ref updatedShield,
                                 ref shieldChanged))
        {
            ConsumeActivationCost(in slotConfig,
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
                                   ref otherSlotIsCharging,
                                   ref dashState,
                                   ref bulletTimeState);

            PlayerPowerUpActivationExecutionUtility.ExecuteChargeShot(in slotConfig,
                                                                      in localTransform,
                                                                      in lookState,
                                                                      in controllerConfig,
                                                                      in passiveToolsState,
                                                                      shootRequests);

            cooldownRemaining = math.max(0f, slotConfig.CooldownSeconds);
        }

        isCharging = 0;
        charge = 0f;
    }
    #endregion

    #region Checks
    private static bool CanExecuteTool(in PlayerPowerUpSlotConfig slotConfig,
                                       in PlayerDashState dashState,
                                       in PlayerBulletTimeState bulletTimeState,
                                       in PlayerHealOverTimeState healOverTimeState,
                                       in PlayerMovementState movementState,
                                       in PlayerControllerConfig controllerConfig,
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
                                                                                               in controllerConfig,
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

                if (bulletTimeState.RemainingDuration > 0f)
                    return false;

                return true;
            case ActiveToolKind.Shotgun:
                if (slotConfig.Shotgun.ProjectileCount <= 0)
                    return false;

                if (controllerConfig.Config.Value.Shooting.Values.ShootSpeed <= 0f)
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

                if (slotConfig.ChargeShot.ChargeRatePerSecond <= 0f)
                    return false;

                if (controllerConfig.Config.Value.Shooting.Values.ShootSpeed <= 0f)
                    return false;

                return true;
            default:
                return false;
        }
    }

    private static bool CanPayActivationCost(in PlayerPowerUpSlotConfig slotConfig,
                                             float slotEnergy,
                                             Entity playerEntity,
                                             ref ComponentLookup<PlayerHealth> healthLookup,
                                             ref PlayerHealth updatedHealth,
                                             ref bool healthChanged,
                                             ref ComponentLookup<PlayerShield> shieldLookup,
                                             ref PlayerShield updatedShield,
                                             ref bool shieldChanged)
    {
        float maximumEnergy = math.max(0f, slotConfig.MaximumEnergy);
        float activationCost = math.max(0f, slotConfig.ActivationCost);
        float minimumActivationEnergyPercent = math.clamp(slotConfig.MinimumActivationEnergyPercent, 0f, 100f);

        switch (slotConfig.ActivationResource)
        {
            case PowerUpResourceType.None:
                return true;
            case PowerUpResourceType.Energy:
                if (maximumEnergy <= 0f)
                    return false;

                if (minimumActivationEnergyPercent > 0f)
                {
                    float minimumEnergyRequired = maximumEnergy * (minimumActivationEnergyPercent * 0.01f);

                    if (slotEnergy + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon < minimumEnergyRequired)
                        return false;
                }

                if (activationCost <= 0f)
                    return true;

                if (slotEnergy + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon < activationCost)
                    return false;

                return true;
            case PowerUpResourceType.Health:
                if (!healthChanged)
                {
                    if (!healthLookup.HasComponent(playerEntity))
                        return false;

                    updatedHealth = healthLookup[playerEntity];
                    healthChanged = true;
                }

                if (activationCost <= 0f)
                    return true;

                if (updatedHealth.Current <= activationCost)
                    return false;

                return true;
            case PowerUpResourceType.Shield:
                if (!shieldChanged)
                {
                    if (!shieldLookup.HasComponent(playerEntity))
                        return false;

                    updatedShield = shieldLookup[playerEntity];
                    shieldChanged = true;
                }

                if (activationCost <= 0f)
                    return true;

                if (updatedShield.Current + PlayerPowerUpActivationUtilityConstants.EnergyEpsilon < activationCost)
                    return false;

                return true;
            default:
                return false;
        }
    }

    private static void ConsumeActivationCost(in PlayerPowerUpSlotConfig slotConfig,
                                              ref float slotEnergy,
                                              Entity playerEntity,
                                              ref ComponentLookup<PlayerHealth> healthLookup,
                                              ref PlayerHealth updatedHealth,
                                              ref bool healthChanged,
                                              ref ComponentLookup<PlayerShield> shieldLookup,
                                              ref PlayerShield updatedShield,
                                              ref bool shieldChanged)
    {
        float activationCost = math.max(0f, slotConfig.ActivationCost);

        switch (slotConfig.ActivationResource)
        {
            case PowerUpResourceType.Energy:
                if (activationCost <= 0f)
                    return;

                slotEnergy -= activationCost;

                if (slotEnergy < 0f)
                    slotEnergy = 0f;

                return;
            case PowerUpResourceType.Health:
                if (!healthChanged)
                {
                    if (!healthLookup.HasComponent(playerEntity))
                        return;

                    updatedHealth = healthLookup[playerEntity];
                    healthChanged = true;
                }

                if (activationCost <= 0f)
                    return;

                updatedHealth.Current -= activationCost;

                if (updatedHealth.Current < 0f)
                    updatedHealth.Current = 0f;

                return;
            case PowerUpResourceType.Shield:
                if (!shieldChanged)
                {
                    if (!shieldLookup.HasComponent(playerEntity))
                        return;

                    updatedShield = shieldLookup[playerEntity];
                    shieldChanged = true;
                }

                if (activationCost <= 0f)
                    return;

                updatedShield.Current -= activationCost;

                if (updatedShield.Current < 0f)
                    updatedShield.Current = 0f;

                return;
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

    private static void InterruptOtherSlot(in PlayerPowerUpSlotConfig slotConfig,
                                           ref float otherSlotCharge,
                                           ref byte otherSlotIsCharging,
                                           ref PlayerDashState dashState,
                                           ref PlayerBulletTimeState bulletTimeState)
    {
        otherSlotCharge = 0f;
        otherSlotIsCharging = 0;

        if (slotConfig.InterruptOtherSlotChargingOnly != 0)
            return;

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

    #endregion
}
