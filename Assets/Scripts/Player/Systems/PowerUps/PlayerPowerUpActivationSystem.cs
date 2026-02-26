using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
    private const float EnergyEpsilon = 0.0001f;
    private const float DirectionLengthEpsilon = 1e-6f;
    #endregion

    #region Nested Types
    private struct ProjectileRequestTemplate
    {
        public float Speed;
        public float Damage;
        public float ExplosionRadius;
        public float Range;
        public float Lifetime;
        public float ScaleMultiplier;
        public byte InheritPlayerSpeed;
    }
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
        state.RequireForUpdate<PlayerPassiveToolsState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerLookState> lookLookup = SystemAPI.GetComponentLookup<PlayerLookState>(true);
        ComponentLookup<PlayerMovementState> movementLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<PlayerControllerConfig> controllerLookup = SystemAPI.GetComponentLookup<PlayerControllerConfig>(true);
        ComponentLookup<PlayerPassiveToolsState> passiveToolsLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PlayerBulletTimeState> bulletTimeLookup = SystemAPI.GetComponentLookup<PlayerBulletTimeState>(false);

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
            if (lookLookup.HasComponent(entity) == false)
                continue;

            if (movementLookup.HasComponent(entity) == false)
                continue;

            if (controllerLookup.HasComponent(entity) == false)
                continue;

            if (passiveToolsLookup.HasComponent(entity) == false)
                continue;

            if (transformLookup.HasComponent(entity) == false)
                continue;

            if (bulletTimeLookup.HasComponent(entity) == false)
                continue;

            PlayerLookState lookState = lookLookup[entity];
            PlayerMovementState movementState = movementLookup[entity];
            PlayerControllerConfig controllerConfig = controllerLookup[entity];
            PlayerPassiveToolsState passiveToolsState = passiveToolsLookup[entity];
            LocalTransform localTransform = transformLookup[entity];
            PlayerBulletTimeState bulletTimeState = bulletTimeLookup[entity];
            bool primaryPressed = inputState.ValueRO.PowerUpPrimary > InputPressThreshold;
            bool secondaryPressed = inputState.ValueRO.PowerUpSecondary > InputPressThreshold;
            bool primaryPressedThisFrame = primaryPressed && powerUpsState.ValueRO.PreviousPrimaryPressed == 0;
            bool secondaryPressedThisFrame = secondaryPressed && powerUpsState.ValueRO.PreviousSecondaryPressed == 0;
            bool primaryReleasedThisFrame = primaryPressed == false && powerUpsState.ValueRO.PreviousPrimaryPressed != 0;
            bool secondaryReleasedThisFrame = secondaryPressed == false && powerUpsState.ValueRO.PreviousSecondaryPressed != 0;
            float3 desiredDirection = movementState.DesiredDirection;

            if (math.lengthsq(desiredDirection) > DirectionLengthEpsilon)
                powerUpsState.ValueRW.LastValidMovementDirection = math.normalizesafe(desiredDirection, new float3(0f, 0f, 1f));

            powerUpsState.ValueRW.PreviousPrimaryPressed = primaryPressed ? (byte)1 : (byte)0;
            powerUpsState.ValueRW.PreviousSecondaryPressed = secondaryPressed ? (byte)1 : (byte)0;

            float primaryEnergy = powerUpsState.ValueRO.PrimaryEnergy;
            float secondaryEnergy = powerUpsState.ValueRO.SecondaryEnergy;
            float primaryCooldownRemaining = powerUpsState.ValueRO.PrimaryCooldownRemaining;
            float secondaryCooldownRemaining = powerUpsState.ValueRO.SecondaryCooldownRemaining;
            float primaryCharge = powerUpsState.ValueRO.PrimaryCharge;
            float secondaryCharge = powerUpsState.ValueRO.SecondaryCharge;
            byte primaryIsCharging = powerUpsState.ValueRO.PrimaryIsCharging;
            byte secondaryIsCharging = powerUpsState.ValueRO.SecondaryIsCharging;
            byte isShootingSuppressed = 0;
            bool healthChanged = false;
            PlayerHealth updatedHealth = default;

            ProcessSlotInput(in powerUpsConfig.ValueRO.PrimarySlot,
                             primaryPressed,
                             primaryPressedThisFrame,
                             primaryReleasedThisFrame,
                             deltaTime,
                             in localTransform,
                             in lookState,
                             in movementState,
                             in controllerConfig,
                             in passiveToolsState,
                             inputState.ValueRO.Move,
                             powerUpsState.ValueRO.LastValidMovementDirection,
                             ref primaryEnergy,
                             ref primaryCooldownRemaining,
                             ref primaryCharge,
                             ref primaryIsCharging,
                             ref isShootingSuppressed,
                             ref dashState.ValueRW,
                             ref bulletTimeState,
                             bombRequests,
                             shootRequests,
                             entity,
                             ref healthLookup,
                             ref updatedHealth,
                             ref healthChanged);

            ProcessSlotInput(in powerUpsConfig.ValueRO.SecondarySlot,
                             secondaryPressed,
                             secondaryPressedThisFrame,
                             secondaryReleasedThisFrame,
                             deltaTime,
                             in localTransform,
                             in lookState,
                             in movementState,
                             in controllerConfig,
                             in passiveToolsState,
                             inputState.ValueRO.Move,
                             powerUpsState.ValueRO.LastValidMovementDirection,
                             ref secondaryEnergy,
                             ref secondaryCooldownRemaining,
                             ref secondaryCharge,
                             ref secondaryIsCharging,
                             ref isShootingSuppressed,
                             ref dashState.ValueRW,
                             ref bulletTimeState,
                             bombRequests,
                             shootRequests,
                             entity,
                             ref healthLookup,
                             ref updatedHealth,
                             ref healthChanged);

            if (healthChanged)
                healthLookup[entity] = updatedHealth;

            powerUpsState.ValueRW.PrimaryEnergy = primaryEnergy;
            powerUpsState.ValueRW.SecondaryEnergy = secondaryEnergy;
            powerUpsState.ValueRW.PrimaryCooldownRemaining = primaryCooldownRemaining;
            powerUpsState.ValueRW.SecondaryCooldownRemaining = secondaryCooldownRemaining;
            powerUpsState.ValueRW.PrimaryCharge = primaryCharge;
            powerUpsState.ValueRW.SecondaryCharge = secondaryCharge;
            powerUpsState.ValueRW.PrimaryIsCharging = primaryIsCharging;
            powerUpsState.ValueRW.SecondaryIsCharging = secondaryIsCharging;
            powerUpsState.ValueRW.IsShootingSuppressed = isShootingSuppressed;
            bulletTimeLookup[entity] = bulletTimeState;
        }
    }
    #endregion

    #region Slot Processing
    private static void ProcessSlotInput(in PlayerPowerUpSlotConfig slotConfig,
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
                                         ref byte isShootingSuppressed,
                                         ref PlayerDashState dashState,
                                         ref PlayerBulletTimeState bulletTimeState,
                                         DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                         DynamicBuffer<ShootRequest> shootRequests,
                                         Entity playerEntity,
                                         ref ComponentLookup<PlayerHealth> healthLookup,
                                         ref PlayerHealth updatedHealth,
                                         ref bool healthChanged)
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
                                  ref isShootingSuppressed,
                                  shootRequests,
                                  playerEntity,
                                  ref healthLookup,
                                  ref updatedHealth,
                                  ref healthChanged);
            return;
        }

        if (pressedThisFrame == false)
            return;

        if (cooldownRemaining > 0f)
            return;

        if (CanExecuteTool(slotConfig,
                           dashState,
                           bulletTimeState,
                           movementState,
                           controllerConfig,
                           localTransform,
                           moveInput,
                           lastValidMovementDirection,
                           playerEntity,
                           ref healthLookup,
                           ref updatedHealth,
                           ref healthChanged) == false)
            return;

        if (CanPayActivationCost(slotConfig,
                                 slotEnergy,
                                 playerEntity,
                                 ref healthLookup,
                                 ref updatedHealth,
                                 ref healthChanged) == false)
            return;

        ConsumeActivationCost(slotConfig,
                              ref slotEnergy,
                              playerEntity,
                              ref healthLookup,
                              ref updatedHealth,
                              ref healthChanged);

        if (slotConfig.ToolKind == ActiveToolKind.PortableHealthPack)
            ExecutePortableHealthPack(slotConfig, playerEntity, ref healthLookup, ref updatedHealth, ref healthChanged);

        ExecuteTool(slotConfig,
                    in localTransform,
                    in lookState,
                    in movementState,
                    in controllerConfig,
                    in passiveToolsState,
                    moveInput,
                    lastValidMovementDirection,
                    ref dashState,
                    ref bulletTimeState,
                    bombRequests,
                    shootRequests);

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
                                              ref byte isShootingSuppressed,
                                              DynamicBuffer<ShootRequest> shootRequests,
                                              Entity playerEntity,
                                              ref ComponentLookup<PlayerHealth> healthLookup,
                                              ref PlayerHealth updatedHealth,
                                              ref bool healthChanged)
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
            isCharging = 1;
            charge = 0f;
        }

        if (isCharging != 0 && isPressed)
        {
            charge += chargeRate * math.max(0f, deltaTime);

            if (charge > maximumCharge)
                charge = maximumCharge;

            if (slotConfig.ChargeShot.SuppressBaseShootingWhileCharging != 0)
                isShootingSuppressed = 1;
        }

        if (releasedThisFrame == false)
            return;

        if (isCharging == 0)
            return;

        bool hasEnoughCharge = charge + EnergyEpsilon >= requiredCharge;

        if (hasEnoughCharge &&
            CanPayActivationCost(slotConfig,
                                 slotEnergy,
                                 playerEntity,
                                 ref healthLookup,
                                 ref updatedHealth,
                                 ref healthChanged))
        {
            ConsumeActivationCost(slotConfig,
                                  ref slotEnergy,
                                  playerEntity,
                                  ref healthLookup,
                                  ref updatedHealth,
                                  ref healthChanged);

            ExecuteChargeShot(slotConfig,
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

                if (TryResolveDashActivationDirection(in movementState,
                                                      in controllerConfig,
                                                      in localTransform,
                                                      moveInput,
                                                      lastValidMovementDirection,
                                                      out float3 _) == false)
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

                if (healthChanged == false)
                {
                    if (healthLookup.HasComponent(playerEntity) == false)
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
                                             ref bool healthChanged)
    {
        float maximumEnergy = math.max(0f, slotConfig.MaximumEnergy);
        float activationCost = math.max(0f, slotConfig.ActivationCost);

        if (slotConfig.FullChargeRequirement != 0 && maximumEnergy > 0f)
        {
            if (slotEnergy + EnergyEpsilon < maximumEnergy)
                return false;
        }

        switch (slotConfig.ActivationResource)
        {
            case PowerUpResourceType.None:
                return true;
            case PowerUpResourceType.Energy:
                if (activationCost <= 0f)
                    return true;

                if (maximumEnergy <= 0f)
                    return false;

                if (slotEnergy + EnergyEpsilon < activationCost)
                    return false;

                return true;
            case PowerUpResourceType.Health:
                if (healthChanged == false)
                {
                    if (healthLookup.HasComponent(playerEntity) == false)
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
                return false;
            default:
                return false;
        }
    }

    private static void ConsumeActivationCost(in PlayerPowerUpSlotConfig slotConfig,
                                              ref float slotEnergy,
                                              Entity playerEntity,
                                              ref ComponentLookup<PlayerHealth> healthLookup,
                                              ref PlayerHealth updatedHealth,
                                              ref bool healthChanged)
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
                if (healthChanged == false)
                {
                    if (healthLookup.HasComponent(playerEntity) == false)
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
        }
    }
    #endregion

    #region Execute
    private static void ExecuteTool(in PlayerPowerUpSlotConfig slotConfig,
                                    in LocalTransform localTransform,
                                    in PlayerLookState lookState,
                                    in PlayerMovementState movementState,
                                    in PlayerControllerConfig controllerConfig,
                                    in PlayerPassiveToolsState passiveToolsState,
                                    float2 moveInput,
                                    float3 lastValidMovementDirection,
                                    ref PlayerDashState dashState,
                                    ref PlayerBulletTimeState bulletTimeState,
                                    DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                    DynamicBuffer<ShootRequest> shootRequests)
    {
        switch (slotConfig.ToolKind)
        {
            case ActiveToolKind.Bomb:
                ExecuteBomb(slotConfig, in localTransform, in movementState, bombRequests);
                return;
            case ActiveToolKind.Dash:
                ExecuteDash(slotConfig,
                            in movementState,
                            in controllerConfig,
                            in localTransform,
                            moveInput,
                            lastValidMovementDirection,
                            ref dashState);
                return;
            case ActiveToolKind.BulletTime:
                ExecuteBulletTime(slotConfig, ref bulletTimeState);
                return;
            case ActiveToolKind.Shotgun:
                ExecuteShotgun(slotConfig,
                               in localTransform,
                               in lookState,
                               in controllerConfig,
                               in passiveToolsState,
                               shootRequests);
                return;
            case ActiveToolKind.PortableHealthPack:
                return;
        }
    }

    private static void ExecuteBomb(in PlayerPowerUpSlotConfig slotConfig,
                                    in LocalTransform localTransform,
                                    in PlayerMovementState movementState,
                                    DynamicBuffer<PlayerBombSpawnRequest> bombRequests)
    {
        float3 bombDirection = ResolveBombActivationDirection(in movementState, in localTransform);
        float3 worldSpawnOffset = math.rotate(localTransform.Rotation, slotConfig.Bomb.SpawnOffset);
        float3 spawnPosition = localTransform.Position + worldSpawnOffset;
        float deploySpeed = math.max(0f, slotConfig.Bomb.DeploySpeed);
        float3 initialVelocity = bombDirection * deploySpeed;

        bombRequests.Add(new PlayerBombSpawnRequest
        {
            BombPrefabEntity = slotConfig.BombPrefabEntity,
            Position = spawnPosition,
            Rotation = quaternion.LookRotationSafe(bombDirection, new float3(0f, 1f, 0f)),
            Velocity = initialVelocity,
            CollisionRadius = math.max(0.01f, slotConfig.Bomb.CollisionRadius),
            BounceOnWalls = slotConfig.Bomb.BounceOnWalls,
            BounceDamping = math.clamp(slotConfig.Bomb.BounceDamping, 0f, 1f),
            LinearDampingPerSecond = math.max(0f, slotConfig.Bomb.LinearDampingPerSecond),
            FuseSeconds = math.max(0.05f, slotConfig.Bomb.FuseSeconds),
            Radius = math.max(0.1f, slotConfig.Bomb.Radius),
            Damage = math.max(0f, slotConfig.Bomb.Damage),
            AffectAllEnemiesInRadius = slotConfig.Bomb.AffectAllEnemiesInRadius
        });
    }

    private static void ExecuteDash(in PlayerPowerUpSlotConfig slotConfig,
                                    in PlayerMovementState movementState,
                                    in PlayerControllerConfig controllerConfig,
                                    in LocalTransform localTransform,
                                    float2 moveInput,
                                    float3 lastValidMovementDirection,
                                    ref PlayerDashState dashState)
    {
        if (TryResolveDashActivationDirection(in movementState,
                                              in controllerConfig,
                                              in localTransform,
                                              moveInput,
                                              lastValidMovementDirection,
                                              out float3 dashDirection) == false)
            return;

        float dashDuration = math.max(0.01f, slotConfig.Dash.Duration);
        float dashDistance = math.max(0f, slotConfig.Dash.Distance);
        float dashTransitionIn = math.clamp(math.max(0f, slotConfig.Dash.SpeedTransitionInSeconds), 0f, dashDuration);
        float dashRemainingDuration = dashDuration - dashTransitionIn;
        float dashTransitionOut = math.clamp(math.max(0f, slotConfig.Dash.SpeedTransitionOutSeconds), 0f, dashRemainingDuration);
        float dashHoldDuration = dashDuration - dashTransitionIn - dashTransitionOut;
        float dashSpeed = dashDistance / dashDuration;

        dashState.IsDashing = 1;
        dashState.Direction = dashDirection;
        float entrySpeedAlongDash = math.max(0f, math.dot(movementState.Velocity, dashDirection));
        dashState.EntryVelocity = dashDirection * entrySpeedAlongDash;
        dashState.Speed = dashSpeed;
        dashState.TransitionInDuration = dashTransitionIn;
        dashState.TransitionOutDuration = dashTransitionOut;
        dashState.HoldDuration = dashHoldDuration;

        if (dashTransitionIn > 0f)
        {
            dashState.Phase = 1;
            dashState.PhaseRemaining = dashTransitionIn;
        }
        else if (dashHoldDuration > 0f)
        {
            dashState.Phase = 2;
            dashState.PhaseRemaining = dashHoldDuration;
        }
        else
        {
            dashState.Phase = 3;
            dashState.PhaseRemaining = dashTransitionOut;
        }

        if (slotConfig.Dash.GrantsInvulnerability != 0)
        {
            float invulnerabilityDuration = dashDuration + math.max(0f, slotConfig.Dash.InvulnerabilityExtraTime);
            dashState.RemainingInvulnerability = invulnerabilityDuration;
        }
    }

    private static void ExecuteBulletTime(in PlayerPowerUpSlotConfig slotConfig, ref PlayerBulletTimeState bulletTimeState)
    {
        bulletTimeState.RemainingDuration = math.max(0.05f, slotConfig.BulletTime.Duration);
        bulletTimeState.SlowPercent = math.clamp(slotConfig.BulletTime.EnemySlowPercent, 0f, 100f);
    }

    private static void ExecuteShotgun(in PlayerPowerUpSlotConfig slotConfig,
                                       in LocalTransform localTransform,
                                       in PlayerLookState lookState,
                                       in PlayerControllerConfig controllerConfig,
                                       in PlayerPassiveToolsState passiveToolsState,
                                       DynamicBuffer<ShootRequest> shootRequests)
    {
        int projectileCount = math.max(1, slotConfig.Shotgun.ProjectileCount);
        float coneAngleDegrees = math.max(0f, slotConfig.Shotgun.ConeAngleDegrees);
        float3 shootDirection = ResolveShootDirection(in lookState, in localTransform);
        float3 spawnPosition = ResolveShootSpawnPosition(in localTransform, in controllerConfig);
        ProjectileRequestTemplate template = BuildProjectileTemplate(in controllerConfig,
                                                                     in passiveToolsState,
                                                                     slotConfig.Shotgun.SizeMultiplier,
                                                                     slotConfig.Shotgun.DamageMultiplier,
                                                                     slotConfig.Shotgun.SpeedMultiplier,
                                                                     slotConfig.Shotgun.RangeMultiplier,
                                                                     slotConfig.Shotgun.LifetimeMultiplier);

        if (projectileCount <= 1)
        {
            AddShootRequest(ref shootRequests,
                            spawnPosition,
                            shootDirection,
                            in template,
                            slotConfig.Shotgun.PenetrationMode,
                            slotConfig.Shotgun.MaxPenetrations,
                            0);
            return;
        }

        float halfCone = coneAngleDegrees * 0.5f;
        float step = coneAngleDegrees / (projectileCount - 1);

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            float angle = -halfCone + step * projectileIndex;
            quaternion rotationOffset = quaternion.AxisAngle(new float3(0f, 1f, 0f), math.radians(angle));
            float3 spreadDirection = math.rotate(rotationOffset, shootDirection);

            AddShootRequest(ref shootRequests,
                            spawnPosition,
                            spreadDirection,
                            in template,
                            slotConfig.Shotgun.PenetrationMode,
                            slotConfig.Shotgun.MaxPenetrations,
                            0);
        }
    }

    private static void ExecuteChargeShot(in PlayerPowerUpSlotConfig slotConfig,
                                          in LocalTransform localTransform,
                                          in PlayerLookState lookState,
                                          in PlayerControllerConfig controllerConfig,
                                          in PlayerPassiveToolsState passiveToolsState,
                                          DynamicBuffer<ShootRequest> shootRequests)
    {
        float3 shootDirection = ResolveShootDirection(in lookState, in localTransform);
        float3 spawnPosition = ResolveShootSpawnPosition(in localTransform, in controllerConfig);
        ProjectileRequestTemplate template = BuildProjectileTemplate(in controllerConfig,
                                                                     in passiveToolsState,
                                                                     slotConfig.ChargeShot.SizeMultiplier,
                                                                     slotConfig.ChargeShot.DamageMultiplier,
                                                                     slotConfig.ChargeShot.SpeedMultiplier,
                                                                     slotConfig.ChargeShot.RangeMultiplier,
                                                                     slotConfig.ChargeShot.LifetimeMultiplier);

        AddShootRequest(ref shootRequests,
                        spawnPosition,
                        shootDirection,
                        in template,
                        slotConfig.ChargeShot.PenetrationMode,
                        slotConfig.ChargeShot.MaxPenetrations,
                        0);
    }

    private static void ExecutePortableHealthPack(in PlayerPowerUpSlotConfig slotConfig,
                                                  Entity playerEntity,
                                                  ref ComponentLookup<PlayerHealth> healthLookup,
                                                  ref PlayerHealth updatedHealth,
                                                  ref bool healthChanged)
    {
        if (healthChanged == false)
        {
            if (healthLookup.HasComponent(playerEntity) == false)
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

        updatedHealth.Current += math.min(missingHealth, healAmount);

        if (updatedHealth.Current > updatedHealth.Max)
            updatedHealth.Current = updatedHealth.Max;
    }
    #endregion

    #region Projectile Helpers
    private static float3 ResolveShootDirection(in PlayerLookState lookState, in LocalTransform localTransform)
    {
        float3 lookDirection = lookState.DesiredDirection;
        lookDirection.y = 0f;

        if (math.lengthsq(lookDirection) > DirectionLengthEpsilon)
            return math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));

        float3 fallbackDirection = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.Rotation), new float3(0f, 0f, 1f));
        return math.normalizesafe(fallbackDirection, new float3(0f, 0f, 1f));
    }

    private static float3 ResolveShootSpawnPosition(in LocalTransform localTransform, in PlayerControllerConfig controllerConfig)
    {
        float3 shootOffset = controllerConfig.Config.Value.Shooting.ShootOffset;
        float3 worldOffset = math.rotate(localTransform.Rotation, shootOffset);
        return localTransform.Position + worldOffset;
    }

    private static ProjectileRequestTemplate BuildProjectileTemplate(in PlayerControllerConfig controllerConfig,
                                                                     in PlayerPassiveToolsState passiveToolsState,
                                                                     float sizeMultiplier,
                                                                     float damageMultiplier,
                                                                     float speedMultiplier,
                                                                     float rangeMultiplier,
                                                                     float lifetimeMultiplier)
    {
        ref ShootingConfig shootingConfig = ref controllerConfig.Config.Value.Shooting;
        ref ShootingValuesBlob values = ref shootingConfig.Values;
        float scale = math.max(0.01f, passiveToolsState.ProjectileSizeMultiplier * math.max(0.01f, sizeMultiplier));
        float damage = math.max(0f, values.Damage * math.max(0f, passiveToolsState.ProjectileDamageMultiplier) * math.max(0f, damageMultiplier));
        float speed = math.max(0f, values.ShootSpeed * math.max(0f, passiveToolsState.ProjectileSpeedMultiplier) * math.max(0f, speedMultiplier));
        float range = values.Range;
        float lifetime = values.Lifetime;

        if (range > 0f)
            range = math.max(0f, range * math.max(0f, passiveToolsState.ProjectileLifetimeRangeMultiplier) * math.max(0f, rangeMultiplier));

        if (lifetime > 0f)
            lifetime = math.max(0f, lifetime * math.max(0f, passiveToolsState.ProjectileLifetimeSecondsMultiplier) * math.max(0f, lifetimeMultiplier));

        return new ProjectileRequestTemplate
        {
            Speed = speed,
            Damage = damage,
            ExplosionRadius = math.max(0f, values.ExplosionRadius),
            Range = range,
            Lifetime = lifetime,
            ScaleMultiplier = scale,
            InheritPlayerSpeed = shootingConfig.ProjectilesInheritPlayerSpeed
        };
    }

    private static void AddShootRequest(ref DynamicBuffer<ShootRequest> shootRequests,
                                        float3 position,
                                        float3 direction,
                                        in ProjectileRequestTemplate template,
                                        ProjectilePenetrationMode penetrationMode,
                                        int maxPenetrations,
                                        byte isSplitChild)
    {
        shootRequests.Add(new ShootRequest
        {
            Position = position,
            Direction = math.normalizesafe(direction, new float3(0f, 0f, 1f)),
            Speed = math.max(0f, template.Speed),
            ExplosionRadius = math.max(0f, template.ExplosionRadius),
            Range = template.Range,
            Lifetime = template.Lifetime,
            Damage = math.max(0f, template.Damage),
            ProjectileScaleMultiplier = math.max(0.01f, template.ScaleMultiplier),
            PenetrationMode = penetrationMode,
            MaxPenetrations = math.max(0, maxPenetrations),
            InheritPlayerSpeed = template.InheritPlayerSpeed,
            IsSplitChild = isSplitChild
        });
    }
    #endregion

    #region Movement Helpers
    private static float3 ResolveBombActivationDirection(in PlayerMovementState movementState, in LocalTransform localTransform)
    {
        float3 movementDirection = movementState.Velocity;
        movementDirection.y = 0f;

        if (math.lengthsq(movementDirection) > DirectionLengthEpsilon)
            return math.normalizesafe(-movementDirection, new float3(0f, 0f, -1f));

        movementDirection = movementState.DesiredDirection;
        movementDirection.y = 0f;

        if (math.lengthsq(movementDirection) > DirectionLengthEpsilon)
            return math.normalizesafe(-movementDirection, new float3(0f, 0f, -1f));

        float3 backwardDirection = -math.forward(localTransform.Rotation);
        backwardDirection.y = 0f;
        return math.normalizesafe(backwardDirection, new float3(0f, 0f, -1f));
    }

    private static bool TryResolveDashActivationDirection(in PlayerMovementState movementState,
                                                          in PlayerControllerConfig controllerConfig,
                                                          in LocalTransform localTransform,
                                                          float2 moveInput,
                                                          float3 lastValidMovementDirection,
                                                          out float3 dashDirection)
    {
        if (TryResolveDashDirectionFromReleaseMask(in movementState,
                                                   in controllerConfig,
                                                   in localTransform,
                                                   out dashDirection))
            return true;

        float3 desiredDirection = movementState.DesiredDirection;

        if (math.lengthsq(desiredDirection) > DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(desiredDirection, new float3(0f, 0f, 1f));
            return true;
        }

        float3 velocityDirection = movementState.Velocity;
        velocityDirection.y = 0f;

        if (math.lengthsq(velocityDirection) > DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(velocityDirection, new float3(0f, 0f, 1f));
            return true;
        }

        if (math.lengthsq(lastValidMovementDirection) > DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(lastValidMovementDirection, new float3(0f, 0f, 1f));
            return true;
        }

        return TryResolveDashDirectionFromInput(moveInput, in controllerConfig, in localTransform, out dashDirection);
    }

    private static bool TryResolveDashDirectionFromReleaseMask(in PlayerMovementState movementState,
                                                               in PlayerControllerConfig controllerConfig,
                                                               in LocalTransform localTransform,
                                                               out float3 dashDirection)
    {
        byte previousMask = movementState.PrevMoveMask;
        byte currentMask = movementState.CurrMoveMask;

        if (PlayerControllerMath.IsDiagonalMask(previousMask) == false)
        {
            dashDirection = float3.zero;
            return false;
        }

        if (PlayerControllerMath.IsSingleAxisMask(currentMask) == false)
        {
            dashDirection = float3.zero;
            return false;
        }

        if (PlayerControllerMath.IsReleaseOnly(previousMask, currentMask) == false)
        {
            dashDirection = float3.zero;
            return false;
        }

        float2 preservedInput = PlayerControllerMath.ResolveDigitalMask(previousMask, movementState.MovePressTimes);

        return TryResolveDashDirectionFromInput(preservedInput,
                                                in controllerConfig,
                                                in localTransform,
                                                out dashDirection);
    }

    private static bool TryResolveDashDirectionFromInput(float2 input,
                                                         in PlayerControllerConfig controllerConfig,
                                                         in LocalTransform localTransform,
                                                         out float3 dashDirection)
    {
        ref MovementConfig movementConfig = ref controllerConfig.Config.Value.Movement;
        float deadZone = movementConfig.Values.InputDeadZone;

        if (math.lengthsq(input) <= deadZone * deadZone)
        {
            dashDirection = float3.zero;
            return false;
        }

        Camera camera = Camera.main;
        bool hasCamera = camera != null;
        float3 cameraForward = hasCamera ? (float3)camera.transform.forward : new float3(0f, 0f, 1f);
        float3 playerForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.Rotation), new float3(0f, 0f, 1f));
        PlayerControllerMath.GetReferenceBasis(movementConfig.MovementReference, playerForward, cameraForward, hasCamera, out float3 forward, out float3 right);
        float2 inputDirection = PlayerControllerMath.NormalizeSafe(input);

        if (math.lengthsq(inputDirection) <= DirectionLengthEpsilon)
        {
            dashDirection = float3.zero;
            return false;
        }

        switch (movementConfig.DirectionsMode)
        {
            case MovementDirectionsMode.DiscreteCount:
                int count = math.max(1, movementConfig.DiscreteDirectionCount);
                float step = (math.PI * 2f) / count;
                float offset = math.radians(movementConfig.DirectionOffsetDegrees);
                float inputAngle = math.atan2(inputDirection.x, inputDirection.y);
                float snappedAngle = PlayerControllerMath.QuantizeAngle(inputAngle, step, offset);
                float3 snappedLocalDirection = PlayerControllerMath.DirectionFromAngle(snappedAngle);
                float3 snappedWorldDirection = right * snappedLocalDirection.x + forward * snappedLocalDirection.z;
                dashDirection = math.normalizesafe(snappedWorldDirection, forward);
                return math.lengthsq(dashDirection) > DirectionLengthEpsilon;
            default:
                float3 freeDirection = right * inputDirection.x + forward * inputDirection.y;
                dashDirection = math.normalizesafe(freeDirection, forward);
                return math.lengthsq(dashDirection) > DirectionLengthEpsilon;
        }
    }
    #endregion

    #endregion
}
