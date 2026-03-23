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
        state.RequireForUpdate<PlayerChargeCharacterTuningState>();
        state.RequireForUpdate<PlayerChargeCharacterTuningBaseStatElement>();
        state.RequireForUpdate<PlayerPowerUpUnlockCatalogElement>();
        state.RequireForUpdate<PlayerPowerUpCharacterTuningFormulaElement>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerBaseMovementConfig>();
        state.RequireForUpdate<PlayerRuntimeMovementConfig>();
        state.RequireForUpdate<PlayerBaseLookConfig>();
        state.RequireForUpdate<PlayerRuntimeLookConfig>();
        state.RequireForUpdate<PlayerBaseCameraConfig>();
        state.RequireForUpdate<PlayerRuntimeCameraConfig>();
        state.RequireForUpdate<PlayerBaseShootingConfig>();
        state.RequireForUpdate<PlayerRuntimeShootingConfig>();
        state.RequireForUpdate<PlayerBaseHealthStatisticsConfig>();
        state.RequireForUpdate<PlayerRuntimeHealthStatisticsConfig>();
        state.RequireForUpdate<PlayerProgressionConfig>();
        state.RequireForUpdate<PlayerRuntimeScalingState>();
        state.RequireForUpdate<PlayerRuntimeControllerScalingElement>();
        state.RequireForUpdate<PlayerRuntimeProgressionScalingElement>();
        state.RequireForUpdate<PlayerBaseGamePhaseElement>();
        state.RequireForUpdate<PlayerRuntimeGamePhaseElement>();
        state.RequireForUpdate<PlayerPowerUpBaseConfigElement>();
        state.RequireForUpdate<PlayerRuntimePowerUpScalingElement>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
        state.RequireForUpdate<PlayerExperience>();
        state.RequireForUpdate<PlayerLevel>();
        state.RequireForUpdate<PlayerExperienceCollection>();
        state.RequireForUpdate<PlayerScalableStatElement>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerShield>();
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
        ComponentLookup<PlayerPowerUpsConfig> powerUpsConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsConfig>(false);
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> shieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);
        ComponentLookup<PlayerLookState> lookLookup = SystemAPI.GetComponentLookup<PlayerLookState>(true);
        ComponentLookup<PlayerMovementState> movementLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<PlayerBaseMovementConfig> baseMovementLookup = SystemAPI.GetComponentLookup<PlayerBaseMovementConfig>(true);
        ComponentLookup<PlayerRuntimeMovementConfig> runtimeMovementLookup = SystemAPI.GetComponentLookup<PlayerRuntimeMovementConfig>(false);
        ComponentLookup<PlayerBaseLookConfig> baseLookLookup = SystemAPI.GetComponentLookup<PlayerBaseLookConfig>(true);
        ComponentLookup<PlayerRuntimeLookConfig> runtimeLookLookup = SystemAPI.GetComponentLookup<PlayerRuntimeLookConfig>(false);
        ComponentLookup<PlayerBaseCameraConfig> baseCameraLookup = SystemAPI.GetComponentLookup<PlayerBaseCameraConfig>(true);
        ComponentLookup<PlayerRuntimeCameraConfig> runtimeCameraLookup = SystemAPI.GetComponentLookup<PlayerRuntimeCameraConfig>(false);
        ComponentLookup<PlayerBaseShootingConfig> baseShootingLookup = SystemAPI.GetComponentLookup<PlayerBaseShootingConfig>(true);
        ComponentLookup<PlayerRuntimeShootingConfig> runtimeShootingLookup = SystemAPI.GetComponentLookup<PlayerRuntimeShootingConfig>(false);
        ComponentLookup<PlayerBaseHealthStatisticsConfig> baseHealthLookup = SystemAPI.GetComponentLookup<PlayerBaseHealthStatisticsConfig>(true);
        ComponentLookup<PlayerRuntimeHealthStatisticsConfig> runtimeHealthLookup = SystemAPI.GetComponentLookup<PlayerRuntimeHealthStatisticsConfig>(false);
        ComponentLookup<PlayerPassiveToolsState> passiveToolsLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(false);
        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<PlayerBulletTimeState> bulletTimeLookup = SystemAPI.GetComponentLookup<PlayerBulletTimeState>(false);
        ComponentLookup<PlayerHealOverTimeState> healOverTimeLookup = SystemAPI.GetComponentLookup<PlayerHealOverTimeState>(false);
        ComponentLookup<PlayerChargeCharacterTuningState> chargeCharacterTuningStateLookup = SystemAPI.GetComponentLookup<PlayerChargeCharacterTuningState>(false);
        ComponentLookup<PlayerProgressionConfig> progressionConfigLookup = SystemAPI.GetComponentLookup<PlayerProgressionConfig>(true);
        ComponentLookup<PlayerExperience> playerExperienceLookup = SystemAPI.GetComponentLookup<PlayerExperience>(false);
        ComponentLookup<PlayerLevel> playerLevelLookup = SystemAPI.GetComponentLookup<PlayerLevel>(false);
        ComponentLookup<PlayerExperienceCollection> playerExperienceCollectionLookup = SystemAPI.GetComponentLookup<PlayerExperienceCollection>(false);
        ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup = SystemAPI.GetComponentLookup<PlayerRuntimeScalingState>(false);
        BufferLookup<PlayerChargeCharacterTuningBaseStatElement> chargeCharacterTuningBaseStatsLookup = SystemAPI.GetBufferLookup<PlayerChargeCharacterTuningBaseStatElement>(false);
        BufferLookup<PlayerRuntimeControllerScalingElement> controllerScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeControllerScalingElement>(true);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false);
        BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaLookup = SystemAPI.GetBufferLookup<PlayerPowerUpCharacterTuningFormulaElement>(true);
        BufferLookup<PlayerScalableStatElement> scalableStatsLookup = SystemAPI.GetBufferLookup<PlayerScalableStatElement>(false);
        BufferLookup<PlayerRuntimeProgressionScalingElement> progressionScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimeProgressionScalingElement>(true);
        BufferLookup<PlayerBaseGamePhaseElement> baseGamePhasesLookup = SystemAPI.GetBufferLookup<PlayerBaseGamePhaseElement>(true);
        BufferLookup<PlayerRuntimeGamePhaseElement> runtimeGamePhasesLookup = SystemAPI.GetBufferLookup<PlayerRuntimeGamePhaseElement>(false);
        BufferLookup<PlayerPowerUpBaseConfigElement> basePowerUpConfigsLookup = SystemAPI.GetBufferLookup<PlayerPowerUpBaseConfigElement>(true);
        BufferLookup<PlayerRuntimePowerUpScalingElement> powerUpScalingLookup = SystemAPI.GetBufferLookup<PlayerRuntimePowerUpScalingElement>(true);

        foreach ((RefRO<PlayerInputState> inputState,
                  RefRW<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  RefRW<PlayerDashState> dashState,
                  DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                  DynamicBuffer<ShootRequest> shootRequests,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerInputState>,
                                    RefRW<PlayerPowerUpsConfig>,
                                    RefRW<PlayerPowerUpsState>,
                                    RefRW<PlayerDashState>,
                                    DynamicBuffer<PlayerBombSpawnRequest>,
                                    DynamicBuffer<ShootRequest>>().WithEntityAccess())
        {
            if (!lookLookup.HasComponent(entity))
                continue;

            if (!movementLookup.HasComponent(entity))
                continue;

            if (!runtimeMovementLookup.HasComponent(entity))
                continue;

            if (!runtimeShootingLookup.HasComponent(entity))
                continue;

            if (!passiveToolsLookup.HasComponent(entity))
                continue;

            if (!transformLookup.HasComponent(entity))
                continue;

            if (!bulletTimeLookup.HasComponent(entity))
                continue;

            if (!healOverTimeLookup.HasComponent(entity))
                continue;

            if (!chargeCharacterTuningStateLookup.HasComponent(entity))
                continue;

            if (!progressionConfigLookup.HasComponent(entity))
                continue;

            if (!playerExperienceLookup.HasComponent(entity))
                continue;

            if (!playerLevelLookup.HasComponent(entity))
                continue;

            if (!playerExperienceCollectionLookup.HasComponent(entity))
                continue;

            if (!chargeCharacterTuningBaseStatsLookup.HasBuffer(entity))
                continue;

            if (!unlockCatalogLookup.HasBuffer(entity))
                continue;

            if (!characterTuningFormulaLookup.HasBuffer(entity))
                continue;

            if (!scalableStatsLookup.HasBuffer(entity))
                continue;

            if (!runtimeGamePhasesLookup.HasBuffer(entity))
                continue;

            PlayerLookState lookState = lookLookup[entity];
            PlayerMovementState movementState = movementLookup[entity];
            PlayerRuntimeMovementConfig runtimeMovementConfig = runtimeMovementLookup[entity];
            PlayerRuntimeShootingConfig runtimeShootingConfig = runtimeShootingLookup[entity];
            PlayerPassiveToolsState passiveToolsState = passiveToolsLookup[entity];
            LocalTransform localTransform = transformLookup[entity];
            PlayerBulletTimeState bulletTimeState = bulletTimeLookup[entity];
            PlayerHealOverTimeState healOverTimeState = healOverTimeLookup[entity];
            PlayerChargeCharacterTuningState chargeCharacterTuningState = chargeCharacterTuningStateLookup[entity];
            PlayerProgressionConfig progressionConfig = progressionConfigLookup[entity];
            PlayerExperience playerExperience = playerExperienceLookup[entity];
            PlayerLevel playerLevel = playerLevelLookup[entity];
            PlayerExperienceCollection playerExperienceCollection = playerExperienceCollectionLookup[entity];
            DynamicBuffer<PlayerChargeCharacterTuningBaseStatElement> chargeCharacterTuningBaseStats = chargeCharacterTuningBaseStatsLookup[entity];
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = unlockCatalogLookup[entity];
            DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas = characterTuningFormulaLookup[entity];
            DynamicBuffer<PlayerScalableStatElement> scalableStats = scalableStatsLookup[entity];
            DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases = runtimeGamePhasesLookup[entity];
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

            PlayerPowerUpSlotConfig primarySlotConfig = powerUpsConfig.ValueRO.PrimarySlot;
            PlayerPowerUpSlotConfig secondarySlotConfig = powerUpsConfig.ValueRO.SecondarySlot;
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
            bool primaryScopedCharacterTuningShouldBeActiveBeforePrimary = ShouldScopedCharacterTuningBeActiveBeforeSlotProcessing(in primarySlotConfig,
                                                                                                                                     primaryPressedThisFrame,
                                                                                                                                     primaryIsCharging,
                                                                                                                                     primaryIsActive,
                                                                                                                                     primaryCooldownRemaining);
            bool secondaryScopedCharacterTuningShouldBeActiveBeforePrimary = ShouldScopedCharacterTuningRemainActive(in secondarySlotConfig,
                                                                                                                       secondaryIsCharging,
                                                                                                                       secondaryIsActive);

            bool scopedCharacterTuningChangedBeforePrimary = PlayerPowerUpChargeCharacterTuningRuntimeUtility.ReconcileScopedCharacterTuning(in primarySlotConfig,
                                                                                                                                               in secondarySlotConfig,
                                                                                                                                               primaryScopedCharacterTuningShouldBeActiveBeforePrimary,
                                                                                                                                               secondaryScopedCharacterTuningShouldBeActiveBeforePrimary,
                                                                                                                                               unlockCatalog,
                                                                                                                                               characterTuningFormulas,
                                                                                                                                               scalableStats,
                                                                                                                                               progressionConfig,
                                                                                                                                               runtimeGamePhases,
                                                                                                                                               ref chargeCharacterTuningState,
                                                                                                                                               chargeCharacterTuningBaseStats,
                                                                                                                                               ref playerExperience,
                                                                                                                                               ref playerLevel,
                                                                                                                                               ref playerExperienceCollection);

            if (scopedCharacterTuningChangedBeforePrimary)
            {
                RefreshRuntimeScaledState(entity,
                                          scalableStatsLookup,
                                          controllerScalingLookup,
                                          baseMovementLookup,
                                          runtimeMovementLookup,
                                          baseLookLookup,
                                          runtimeLookLookup,
                                          baseCameraLookup,
                                          runtimeCameraLookup,
                                          baseShootingLookup,
                                          runtimeShootingLookup,
                                          baseHealthLookup,
                                          runtimeHealthLookup,
                                          progressionScalingLookup,
                                          baseGamePhasesLookup,
                                          runtimeGamePhasesLookup,
                                          basePowerUpConfigsLookup,
                                          powerUpScalingLookup,
                                          powerUpsConfigLookup,
                                          unlockCatalogLookup,
                                          equippedPassiveToolsLookup,
                                          passiveToolsLookup,
                                          healthLookup,
                                          shieldLookup,
                                          progressionConfigLookup,
                                          playerExperienceLookup,
                                          playerLevelLookup,
                                          playerExperienceCollectionLookup,
                                          runtimeScalingStateLookup,
                                          ref primarySlotConfig,
                                          ref secondarySlotConfig,
                                          ref runtimeMovementConfig,
                                          ref runtimeShootingConfig,
                                          ref passiveToolsState,
                                          ref playerExperience,
                                          ref playerLevel,
                                          ref playerExperienceCollection);
            }

            PlayerPowerUpActivationSlotUtility.ProcessSlotInput(in primarySlotConfig,
                                                                in secondarySlotConfig,
                                                                primaryPressed,
                                                                primaryPressedThisFrame,
                                                                primaryReleasedThisFrame,
                                                                deltaTime,
                                                                in localTransform,
                                                                in lookState,
                                                                in movementState,
                                                                in runtimeMovementConfig,
                                                                in runtimeShootingConfig,
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

            bool primaryScopedCharacterTuningShouldBeActiveBeforeSecondary = ShouldScopedCharacterTuningRemainActive(in primarySlotConfig,
                                                                                                                         primaryIsCharging,
                                                                                                                         primaryIsActive);
            bool secondaryScopedCharacterTuningShouldBeActiveBeforeSecondary = ShouldScopedCharacterTuningBeActiveBeforeSlotProcessing(in secondarySlotConfig,
                                                                                                                                         secondaryPressedThisFrame,
                                                                                                                                         secondaryIsCharging,
                                                                                                                                         secondaryIsActive,
                                                                                                                                         secondaryCooldownRemaining);

            bool scopedCharacterTuningChangedBeforeSecondary = PlayerPowerUpChargeCharacterTuningRuntimeUtility.ReconcileScopedCharacterTuning(in primarySlotConfig,
                                                                                                                                                 in secondarySlotConfig,
                                                                                                                                                 primaryScopedCharacterTuningShouldBeActiveBeforeSecondary,
                                                                                                                                                 secondaryScopedCharacterTuningShouldBeActiveBeforeSecondary,
                                                                                                                                                 unlockCatalog,
                                                                                                                                                 characterTuningFormulas,
                                                                                                                                                 scalableStats,
                                                                                                                                                 progressionConfig,
                                                                                                                                                 runtimeGamePhases,
                                                                                                                                                 ref chargeCharacterTuningState,
                                                                                                                                                 chargeCharacterTuningBaseStats,
                                                                                                                                                 ref playerExperience,
                                                                                                                                                 ref playerLevel,
                                                                                                                                                 ref playerExperienceCollection);

            if (scopedCharacterTuningChangedBeforeSecondary)
            {
                RefreshRuntimeScaledState(entity,
                                          scalableStatsLookup,
                                          controllerScalingLookup,
                                          baseMovementLookup,
                                          runtimeMovementLookup,
                                          baseLookLookup,
                                          runtimeLookLookup,
                                          baseCameraLookup,
                                          runtimeCameraLookup,
                                          baseShootingLookup,
                                          runtimeShootingLookup,
                                          baseHealthLookup,
                                          runtimeHealthLookup,
                                          progressionScalingLookup,
                                          baseGamePhasesLookup,
                                          runtimeGamePhasesLookup,
                                          basePowerUpConfigsLookup,
                                          powerUpScalingLookup,
                                          powerUpsConfigLookup,
                                          unlockCatalogLookup,
                                          equippedPassiveToolsLookup,
                                          passiveToolsLookup,
                                          healthLookup,
                                          shieldLookup,
                                          progressionConfigLookup,
                                          playerExperienceLookup,
                                          playerLevelLookup,
                                          playerExperienceCollectionLookup,
                                          runtimeScalingStateLookup,
                                          ref primarySlotConfig,
                                          ref secondarySlotConfig,
                                          ref runtimeMovementConfig,
                                          ref runtimeShootingConfig,
                                          ref passiveToolsState,
                                          ref playerExperience,
                                          ref playerLevel,
                                          ref playerExperienceCollection);
            }

            PlayerPowerUpActivationSlotUtility.ProcessSlotInput(in secondarySlotConfig,
                                                                in primarySlotConfig,
                                                                secondaryPressed,
                                                                secondaryPressedThisFrame,
                                                                secondaryReleasedThisFrame,
                                                                deltaTime,
                                                                in localTransform,
                                                                in lookState,
                                                                in movementState,
                                                                in runtimeMovementConfig,
                                                                in runtimeShootingConfig,
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

            bool primaryScopedCharacterTuningShouldBeActiveFinal = ShouldScopedCharacterTuningRemainActive(in primarySlotConfig,
                                                                                                              primaryIsCharging,
                                                                                                              primaryIsActive);
            bool secondaryScopedCharacterTuningShouldBeActiveFinal = ShouldScopedCharacterTuningRemainActive(in secondarySlotConfig,
                                                                                                                secondaryIsCharging,
                                                                                                                secondaryIsActive);

            bool scopedCharacterTuningChangedFinal = PlayerPowerUpChargeCharacterTuningRuntimeUtility.ReconcileScopedCharacterTuning(in primarySlotConfig,
                                                                                                                                       in secondarySlotConfig,
                                                                                                                                       primaryScopedCharacterTuningShouldBeActiveFinal,
                                                                                                                                       secondaryScopedCharacterTuningShouldBeActiveFinal,
                                                                                                                                       unlockCatalog,
                                                                                                                                       characterTuningFormulas,
                                                                                                                                       scalableStats,
                                                                                                                                       progressionConfig,
                                                                                                                                       runtimeGamePhases,
                                                                                                                                       ref chargeCharacterTuningState,
                                                                                                                                       chargeCharacterTuningBaseStats,
                                                                                                                                       ref playerExperience,
                                                                                                                                       ref playerLevel,
                                                                                                                                       ref playerExperienceCollection);

            if (scopedCharacterTuningChangedFinal)
            {
                RefreshRuntimeScaledState(entity,
                                          scalableStatsLookup,
                                          controllerScalingLookup,
                                          baseMovementLookup,
                                          runtimeMovementLookup,
                                          baseLookLookup,
                                          runtimeLookLookup,
                                          baseCameraLookup,
                                          runtimeCameraLookup,
                                          baseShootingLookup,
                                          runtimeShootingLookup,
                                          baseHealthLookup,
                                          runtimeHealthLookup,
                                          progressionScalingLookup,
                                          baseGamePhasesLookup,
                                          runtimeGamePhasesLookup,
                                          basePowerUpConfigsLookup,
                                          powerUpScalingLookup,
                                          powerUpsConfigLookup,
                                          unlockCatalogLookup,
                                          equippedPassiveToolsLookup,
                                          passiveToolsLookup,
                                          healthLookup,
                                          shieldLookup,
                                          progressionConfigLookup,
                                          playerExperienceLookup,
                                          playerLevelLookup,
                                          playerExperienceCollectionLookup,
                                          runtimeScalingStateLookup,
                                          ref primarySlotConfig,
                                          ref secondarySlotConfig,
                                          ref runtimeMovementConfig,
                                          ref runtimeShootingConfig,
                                          ref passiveToolsState,
                                          ref playerExperience,
                                          ref playerLevel,
                                          ref playerExperienceCollection);
            }

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
            chargeCharacterTuningStateLookup[entity] = chargeCharacterTuningState;
            playerExperienceLookup[entity] = playerExperience;
            playerLevelLookup[entity] = playerLevel;
            playerExperienceCollectionLookup[entity] = playerExperienceCollection;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether one runtime-scoped Character Tuning overlay must already be active before the current slot starts processing this frame.
    /// /params slotConfig: Slot config inspected for temporary Character Tuning semantics.
    /// /params pressedThisFrame: True when the slot input was freshly pressed during the current frame.
    /// /params isCharging: Current charging flag before slot processing mutates it.
    /// /params isActive: Current toggle-active flag before slot processing mutates it.
    /// /params cooldownRemaining: Current cooldown or startup-lock value before slot processing mutates it.
    /// /returns True when the temporary Character Tuning overlay should be active while the current slot is processed.
    /// </summary>
    private static bool ShouldScopedCharacterTuningBeActiveBeforeSlotProcessing(in PlayerPowerUpSlotConfig slotConfig,
                                                                                bool pressedThisFrame,
                                                                                byte isCharging,
                                                                                byte isActive,
                                                                                float cooldownRemaining)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        if (slotConfig.ToolKind == ActiveToolKind.ChargeShot)
        {
            if (isCharging != 0)
                return true;

            if (!pressedThisFrame)
                return false;

            if (cooldownRemaining > 0f)
                return false;

            if (slotConfig.ChargeShot.RequiredCharge <= 0f)
                return false;

            return slotConfig.ChargeShot.MaximumCharge > 0f;
        }

        if (slotConfig.Toggleable == 0)
            return false;

        return isActive != 0;
    }

    /// <summary>
    /// Rebuilds runtime-scaled configs immediately after scalable-stat changes triggered inside the activation flow and refreshes cached local copies.
    /// /params entity: Player entity being refreshed.
    /// /params scalableStatsLookup: Runtime scalable-stat buffer lookup.
    /// /params controllerScalingLookup: Controller scaling metadata lookup.
    /// /params baseMovementLookup: Immutable movement baseline lookup.
    /// /params runtimeMovementLookup: Mutable runtime movement config lookup.
    /// /params baseLookLookup: Immutable look baseline lookup.
    /// /params runtimeLookLookup: Mutable runtime look config lookup.
    /// /params baseCameraLookup: Immutable camera baseline lookup.
    /// /params runtimeCameraLookup: Mutable runtime camera config lookup.
    /// /params baseShootingLookup: Immutable shooting baseline lookup.
    /// /params runtimeShootingLookup: Mutable runtime shooting config lookup.
    /// /params baseHealthLookup: Immutable health baseline lookup.
    /// /params runtimeHealthLookup: Mutable runtime health config lookup.
    /// /params progressionScalingLookup: Progression scaling metadata lookup.
    /// /params baseGamePhasesLookup: Immutable runtime-phase baseline lookup.
    /// /params runtimeGamePhasesLookup: Mutable runtime-phase buffer lookup.
    /// /params basePowerUpConfigsLookup: Immutable modular power-up baseline lookup.
    /// /params powerUpScalingLookup: Runtime power-up scaling metadata lookup.
    /// /params powerUpsConfigLookup: Mutable power-up slot config lookup.
    /// /params unlockCatalogLookup: Mutable unlock catalog lookup.
    /// /params equippedPassiveToolsLookup: Mutable equipped-passive buffer lookup.
    /// /params passiveToolsLookup: Mutable passive aggregate lookup.
    /// /params healthLookup: Mutable health lookup.
    /// /params shieldLookup: Mutable shield lookup.
    /// /params progressionConfigLookup: Runtime progression config lookup.
    /// /params experienceLookup: Mutable experience lookup.
    /// /params levelLookup: Mutable level lookup.
    /// /params experienceCollectionLookup: Mutable experience-collection lookup.
    /// /params runtimeScalingStateLookup: Mutable runtime-scaling sync state lookup.
    /// /params primarySlotConfig: Cached primary slot config refreshed from runtime state.
    /// /params secondarySlotConfig: Cached secondary slot config refreshed from runtime state.
    /// /params runtimeMovementConfig: Cached runtime movement config refreshed from runtime state.
    /// /params runtimeShootingConfig: Cached runtime shooting config refreshed from runtime state.
    /// /params passiveToolsState: Cached passive aggregate refreshed from runtime state.
    /// /params playerExperience: Cached experience component refreshed from runtime state.
    /// /params playerLevel: Cached level component refreshed from runtime state.
    /// /params playerExperienceCollection: Cached experience-collection component refreshed from runtime state.
    /// /returns void.
    /// </summary>
    private static void RefreshRuntimeScaledState(Entity entity,
                                                  BufferLookup<PlayerScalableStatElement> scalableStatsLookup,
                                                  BufferLookup<PlayerRuntimeControllerScalingElement> controllerScalingLookup,
                                                  ComponentLookup<PlayerBaseMovementConfig> baseMovementLookup,
                                                  ComponentLookup<PlayerRuntimeMovementConfig> runtimeMovementLookup,
                                                  ComponentLookup<PlayerBaseLookConfig> baseLookLookup,
                                                  ComponentLookup<PlayerRuntimeLookConfig> runtimeLookLookup,
                                                  ComponentLookup<PlayerBaseCameraConfig> baseCameraLookup,
                                                  ComponentLookup<PlayerRuntimeCameraConfig> runtimeCameraLookup,
                                                  ComponentLookup<PlayerBaseShootingConfig> baseShootingLookup,
                                                  ComponentLookup<PlayerRuntimeShootingConfig> runtimeShootingLookup,
                                                  ComponentLookup<PlayerBaseHealthStatisticsConfig> baseHealthLookup,
                                                  ComponentLookup<PlayerRuntimeHealthStatisticsConfig> runtimeHealthLookup,
                                                  BufferLookup<PlayerRuntimeProgressionScalingElement> progressionScalingLookup,
                                                  BufferLookup<PlayerBaseGamePhaseElement> baseGamePhasesLookup,
                                                  BufferLookup<PlayerRuntimeGamePhaseElement> runtimeGamePhasesLookup,
                                                  BufferLookup<PlayerPowerUpBaseConfigElement> basePowerUpConfigsLookup,
                                                  BufferLookup<PlayerRuntimePowerUpScalingElement> powerUpScalingLookup,
                                                  ComponentLookup<PlayerPowerUpsConfig> powerUpsConfigLookup,
                                                  BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup,
                                                  BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup,
                                                  ComponentLookup<PlayerPassiveToolsState> passiveToolsLookup,
                                                  ComponentLookup<PlayerHealth> healthLookup,
                                                  ComponentLookup<PlayerShield> shieldLookup,
                                                  ComponentLookup<PlayerProgressionConfig> progressionConfigLookup,
                                                  ComponentLookup<PlayerExperience> experienceLookup,
                                                  ComponentLookup<PlayerLevel> levelLookup,
                                                  ComponentLookup<PlayerExperienceCollection> experienceCollectionLookup,
                                                  ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup,
                                                  ref PlayerPowerUpSlotConfig primarySlotConfig,
                                                  ref PlayerPowerUpSlotConfig secondarySlotConfig,
                                                  ref PlayerRuntimeMovementConfig runtimeMovementConfig,
                                                  ref PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                  ref PlayerPassiveToolsState passiveToolsState,
                                                  ref PlayerExperience playerExperience,
                                                  ref PlayerLevel playerLevel,
                                                  ref PlayerExperienceCollection playerExperienceCollection)
    {
        PlayerRuntimeScalingRefreshUtility.TryApplyForEntity(entity,
                                                             scalableStatsLookup,
                                                             controllerScalingLookup,
                                                             baseMovementLookup,
                                                             runtimeMovementLookup,
                                                             baseLookLookup,
                                                             runtimeLookLookup,
                                                             baseCameraLookup,
                                                             runtimeCameraLookup,
                                                             baseShootingLookup,
                                                             runtimeShootingLookup,
                                                             baseHealthLookup,
                                                             runtimeHealthLookup,
                                                             progressionScalingLookup,
                                                             baseGamePhasesLookup,
                                                             runtimeGamePhasesLookup,
                                                             basePowerUpConfigsLookup,
                                                             powerUpScalingLookup,
                                                             powerUpsConfigLookup,
                                                             unlockCatalogLookup,
                                                             equippedPassiveToolsLookup,
                                                             passiveToolsLookup,
                                                             healthLookup,
                                                             shieldLookup,
                                                             progressionConfigLookup,
                                                             experienceLookup,
                                                             levelLookup,
                                                             experienceCollectionLookup,
                                                             runtimeScalingStateLookup,
                                                             false);

        if (!powerUpsConfigLookup.HasComponent(entity))
            return;

        PlayerPowerUpsConfig powerUpsConfig = powerUpsConfigLookup[entity];
        primarySlotConfig = powerUpsConfig.PrimarySlot;
        secondarySlotConfig = powerUpsConfig.SecondarySlot;

        if (runtimeMovementLookup.HasComponent(entity))
            runtimeMovementConfig = runtimeMovementLookup[entity];

        if (runtimeShootingLookup.HasComponent(entity))
            runtimeShootingConfig = runtimeShootingLookup[entity];

        if (passiveToolsLookup.HasComponent(entity))
            passiveToolsState = passiveToolsLookup[entity];

        if (experienceLookup.HasComponent(entity))
            playerExperience = experienceLookup[entity];

        if (levelLookup.HasComponent(entity))
            playerLevel = levelLookup[entity];

        if (experienceCollectionLookup.HasComponent(entity))
            playerExperienceCollection = experienceCollectionLookup[entity];
    }

    /// <summary>
    /// Resolves whether one runtime-scoped Character Tuning overlay must remain applied outside the slot currently being processed.
    /// /params slotConfig: Slot config inspected for temporary Character Tuning semantics.
    /// /params isCharging: Current charging flag after the latest slot mutation.
    /// /params isActive: Current toggle-active flag after the latest slot mutation.
    /// /returns True when the temporary Character Tuning overlay should remain applied.
    /// </summary>
    private static bool ShouldScopedCharacterTuningRemainActive(in PlayerPowerUpSlotConfig slotConfig,
                                                                byte isCharging,
                                                                byte isActive)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        if (slotConfig.ToolKind == ActiveToolKind.ChargeShot)
            return isCharging != 0;

        if (slotConfig.Toggleable == 0)
            return false;

        return isActive != 0;
    }
    #endregion

    #endregion
}
