using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Handles active-tool button presses, charge workflows and emits runtime actions/requests.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpRechargeSystem))]
[UpdateAfter(typeof(PlayerMovementDirectionSystem))]
[UpdateAfter(typeof(PlayerLookDirectionSystem))]
[UpdateBefore(typeof(PlayerMovementSpeedSystem))]
[UpdateBefore(typeof(PlayerDashMovementSystem))]
public partial struct PlayerPowerUpActivationSystem : ISystem
{
    #region Constants
    private const float InputPressThreshold = 0.5f;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PlayerBombSpawnRequest>();
        state.RequireForUpdate<ShootRequest>();
        state.RequireForUpdate<PlayerBulletTimeState>();
        state.RequireForUpdate<PlayerHealOverTimeState>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);
        ComponentLookup<PlayerLookState> lookLookup = SystemAPI.GetComponentLookup<PlayerLookState>(true);
        ComponentLookup<PlayerMovementState> movementLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<PlayerControllerConfig> controllerLookup = SystemAPI.GetComponentLookup<PlayerControllerConfig>(true);
        ComponentLookup<PlayerPassiveToolsState> passiveToolsLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(true);
        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<PlayerBulletTimeState> bulletTimeLookup = SystemAPI.GetComponentLookup<PlayerBulletTimeState>(false);
        ComponentLookup<PlayerHealOverTimeState> healOverTimeLookup = SystemAPI.GetComponentLookup<PlayerHealOverTimeState>(false);

        foreach ((RefRO<PlayerInputState> inputState,
                  RefRO<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  RefRW<PlayerDashState> dashState,
                  DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                  DynamicBuffer<ShootRequest> shootRequests,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerInputState>,
                                    RefRO<PlayerPowerUpsConfig>,
                                    RefRW<PlayerPowerUpsState>,
                                    RefRW<PlayerDashState>,
                                    DynamicBuffer<PlayerBombSpawnRequest>,
                                    DynamicBuffer<ShootRequest>>().WithEntityAccess())
        {
            if (!lookLookup.HasComponent(entity))
                continue;

            if (!movementLookup.HasComponent(entity))
                continue;

            if (!controllerLookup.HasComponent(entity))
                continue;

            if (!passiveToolsLookup.HasComponent(entity))
                continue;

            if (!transformLookup.HasComponent(entity))
                continue;

            if (!bulletTimeLookup.HasComponent(entity))
                continue;

            if (!healOverTimeLookup.HasComponent(entity))
                continue;

            PlayerLookState lookState = lookLookup[entity];
            PlayerMovementState movementState = movementLookup[entity];
            PlayerControllerConfig controllerConfig = controllerLookup[entity];
            PlayerPassiveToolsState passiveToolsState = passiveToolsLookup[entity];
            LocalTransform localTransform = transformLookup[entity];
            PlayerBulletTimeState bulletTimeState = bulletTimeLookup[entity];
            PlayerHealOverTimeState healOverTimeState = healOverTimeLookup[entity];
            bool primaryPressed = inputState.ValueRO.PowerUpPrimary > InputPressThreshold;
            bool secondaryPressed = inputState.ValueRO.PowerUpSecondary > InputPressThreshold;
            bool primaryPressedThisFrame = primaryPressed && powerUpsState.ValueRO.PreviousPrimaryPressed == 0;
            bool secondaryPressedThisFrame = secondaryPressed && powerUpsState.ValueRO.PreviousSecondaryPressed == 0;
            bool primaryReleasedThisFrame = !primaryPressed && powerUpsState.ValueRO.PreviousPrimaryPressed != 0;
            bool secondaryReleasedThisFrame = !secondaryPressed && powerUpsState.ValueRO.PreviousSecondaryPressed != 0;
            float3 desiredDirection = movementState.DesiredDirection;

            if (math.lengthsq(desiredDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
                powerUpsState.ValueRW.LastValidMovementDirection = math.normalizesafe(desiredDirection, new float3(0f, 0f, 1f));

            powerUpsState.ValueRW.PreviousPrimaryPressed = primaryPressed ? (byte)1 : (byte)0;
            powerUpsState.ValueRW.PreviousSecondaryPressed = secondaryPressed ? (byte)1 : (byte)0;

            float primaryEnergy = powerUpsState.ValueRO.PrimaryEnergy;
            float secondaryEnergy = powerUpsState.ValueRO.SecondaryEnergy;
            float primaryCooldownRemaining = powerUpsState.ValueRO.PrimaryCooldownRemaining;
            float secondaryCooldownRemaining = powerUpsState.ValueRO.SecondaryCooldownRemaining;
            float primaryCharge = powerUpsState.ValueRO.PrimaryCharge;
            float secondaryCharge = powerUpsState.ValueRO.SecondaryCharge;
            float primaryMaintenanceTickTimer = powerUpsState.ValueRO.PrimaryMaintenanceTickTimer;
            float secondaryMaintenanceTickTimer = powerUpsState.ValueRO.SecondaryMaintenanceTickTimer;
            byte primaryIsCharging = powerUpsState.ValueRO.PrimaryIsCharging;
            byte secondaryIsCharging = powerUpsState.ValueRO.SecondaryIsCharging;
            byte primaryIsActive = powerUpsState.ValueRO.PrimaryIsActive;
            byte secondaryIsActive = powerUpsState.ValueRO.SecondaryIsActive;
            byte isShootingSuppressed = 0;
            bool healthChanged = false;
            PlayerHealth updatedHealth = default;
            bool shieldChanged = false;
            PlayerShield updatedShield = default;

            PlayerPowerUpActivationSlotUtility.ProcessSlotInput(in powerUpsConfig.ValueRO.PrimarySlot,
                                                                in powerUpsConfig.ValueRO.SecondarySlot,
                                                                primaryPressed,
                                                                primaryPressedThisFrame,
                                                                primaryReleasedThisFrame,
                                                                deltaTime,
                                                                in localTransform,
                                                                in lookState,
                                                                in movementState,
                                                                in controllerConfig,
                                                                in passiveToolsState,
                                                                in muzzleLookup,
                                                                in transformLookup,
                                                                in localToWorldLookup,
                                                                inputState.ValueRO.Move,
                                                                powerUpsState.ValueRO.LastValidMovementDirection,
                                                                ref primaryEnergy,
                                                                ref primaryCooldownRemaining,
                                                                ref primaryCharge,
                                                                ref primaryIsCharging,
                                                                ref primaryIsActive,
                                                                ref primaryMaintenanceTickTimer,
                                                                ref secondaryCharge,
                                                                ref secondaryCooldownRemaining,
                                                                ref secondaryIsCharging,
                                                                ref secondaryIsActive,
                                                                ref secondaryMaintenanceTickTimer,
                                                                ref isShootingSuppressed,
                                                                ref dashState.ValueRW,
                                                                ref bulletTimeState,
                                                                ref healOverTimeState,
                                                                bombRequests,
                                                                shootRequests,
                                                                entity,
                                                                ref healthLookup,
                                                                ref updatedHealth,
                                                                ref healthChanged,
                                                                ref shieldLookup,
                                                                ref updatedShield,
                                                                ref shieldChanged);

            PlayerPowerUpActivationSlotUtility.ProcessSlotInput(in powerUpsConfig.ValueRO.SecondarySlot,
                                                                in powerUpsConfig.ValueRO.PrimarySlot,
                                                                secondaryPressed,
                                                                secondaryPressedThisFrame,
                                                                secondaryReleasedThisFrame,
                                                                deltaTime,
                                                                in localTransform,
                                                                in lookState,
                                                                in movementState,
                                                                in controllerConfig,
                                                                in passiveToolsState,
                                                                in muzzleLookup,
                                                                in transformLookup,
                                                                in localToWorldLookup,
                                                                inputState.ValueRO.Move,
                                                                powerUpsState.ValueRO.LastValidMovementDirection,
                                                                ref secondaryEnergy,
                                                                ref secondaryCooldownRemaining,
                                                                ref secondaryCharge,
                                                                ref secondaryIsCharging,
                                                                ref secondaryIsActive,
                                                                ref secondaryMaintenanceTickTimer,
                                                                ref primaryCharge,
                                                                ref primaryCooldownRemaining,
                                                                ref primaryIsCharging,
                                                                ref primaryIsActive,
                                                                ref primaryMaintenanceTickTimer,
                                                                ref isShootingSuppressed,
                                                                ref dashState.ValueRW,
                                                                ref bulletTimeState,
                                                                ref healOverTimeState,
                                                                bombRequests,
                                                                shootRequests,
                                                                entity,
                                                                ref healthLookup,
                                                                ref updatedHealth,
                                                                ref healthChanged,
                                                                ref shieldLookup,
                                                                ref updatedShield,
                                                                ref shieldChanged);

            if (healthChanged)
                healthLookup[entity] = updatedHealth;

            if (shieldChanged)
                shieldLookup[entity] = updatedShield;

            powerUpsState.ValueRW.PrimaryEnergy = primaryEnergy;
            powerUpsState.ValueRW.SecondaryEnergy = secondaryEnergy;
            powerUpsState.ValueRW.PrimaryCooldownRemaining = primaryCooldownRemaining;
            powerUpsState.ValueRW.SecondaryCooldownRemaining = secondaryCooldownRemaining;
            powerUpsState.ValueRW.PrimaryCharge = primaryCharge;
            powerUpsState.ValueRW.SecondaryCharge = secondaryCharge;
            powerUpsState.ValueRW.PrimaryMaintenanceTickTimer = primaryMaintenanceTickTimer;
            powerUpsState.ValueRW.SecondaryMaintenanceTickTimer = secondaryMaintenanceTickTimer;
            powerUpsState.ValueRW.PrimaryIsCharging = primaryIsCharging;
            powerUpsState.ValueRW.SecondaryIsCharging = secondaryIsCharging;
            powerUpsState.ValueRW.PrimaryIsActive = primaryIsActive;
            powerUpsState.ValueRW.SecondaryIsActive = secondaryIsActive;
            powerUpsState.ValueRW.IsShootingSuppressed = isShootingSuppressed;
            bulletTimeLookup[entity] = bulletTimeState;
            healOverTimeLookup[entity] = healOverTimeState;
        }
    }
    #endregion

    #endregion
}
