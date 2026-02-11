using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Handles active-tool button presses and emits Bomb/Dash runtime actions.
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

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PlayerBombSpawnRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach ((RefRO<PlayerInputState> inputState,
                  RefRO<PlayerMovementState> movementState,
                  RefRO<PlayerControllerConfig> controllerConfig,
                  RefRO<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  RefRW<PlayerDashState> dashState,
                  DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerInputState>,
                                    RefRO<PlayerMovementState>,
                                    RefRO<PlayerControllerConfig>,
                                    RefRO<PlayerPowerUpsConfig>,
                                    RefRW<PlayerPowerUpsState>,
                                    RefRW<PlayerDashState>,
                                    DynamicBuffer<PlayerBombSpawnRequest>>().WithEntityAccess())
        {
            LocalTransform localTransform = default;

            if (localTransformLookup.HasComponent(entity) == false)
                continue;

            localTransform = localTransformLookup[entity];

            bool primaryPressed = inputState.ValueRO.PowerUpPrimary > InputPressThreshold;
            bool secondaryPressed = inputState.ValueRO.PowerUpSecondary > InputPressThreshold;
            bool primaryPressedThisFrame = primaryPressed && powerUpsState.ValueRO.PreviousPrimaryPressed == 0;
            bool secondaryPressedThisFrame = secondaryPressed && powerUpsState.ValueRO.PreviousSecondaryPressed == 0;
            float3 desiredDirection = movementState.ValueRO.DesiredDirection;

            if (math.lengthsq(desiredDirection) > DirectionLengthEpsilon)
                powerUpsState.ValueRW.LastValidMovementDirection = math.normalizesafe(desiredDirection, new float3(0f, 0f, 1f));

            powerUpsState.ValueRW.PreviousPrimaryPressed = primaryPressed ? (byte)1 : (byte)0;
            powerUpsState.ValueRW.PreviousSecondaryPressed = secondaryPressed ? (byte)1 : (byte)0;

            float primaryEnergy = powerUpsState.ValueRO.PrimaryEnergy;
            float secondaryEnergy = powerUpsState.ValueRO.SecondaryEnergy;
            bool healthChanged = false;
            PlayerHealth updatedHealth = default;

            if (primaryPressedThisFrame)
            {
                TryActivateSlot(in powerUpsConfig.ValueRO.PrimarySlot,
                                ref primaryEnergy,
                                in localTransform,
                                in movementState.ValueRO,
                                in controllerConfig.ValueRO,
                                inputState.ValueRO.Move,
                                powerUpsState.ValueRO.LastValidMovementDirection,
                                ref dashState.ValueRW,
                                bombRequests,
                                entity,
                                ref healthLookup,
                                ref updatedHealth,
                                ref healthChanged);
            }

            if (secondaryPressedThisFrame)
            {
                TryActivateSlot(in powerUpsConfig.ValueRO.SecondarySlot,
                                ref secondaryEnergy,
                                in localTransform,
                                in movementState.ValueRO,
                                in controllerConfig.ValueRO,
                                inputState.ValueRO.Move,
                                powerUpsState.ValueRO.LastValidMovementDirection,
                                ref dashState.ValueRW,
                                bombRequests,
                                entity,
                                ref healthLookup,
                                ref updatedHealth,
                                ref healthChanged);
            }

            if (healthChanged)
                healthLookup[entity] = updatedHealth;

            powerUpsState.ValueRW.PrimaryEnergy = primaryEnergy;
            powerUpsState.ValueRW.SecondaryEnergy = secondaryEnergy;
        }
    }
    #endregion

    #region Helpers
    private static void TryActivateSlot(in PlayerPowerUpSlotConfig slotConfig,
                                        ref float slotEnergy,
                                        in LocalTransform localTransform,
                                        in PlayerMovementState movementState,
                                        in PlayerControllerConfig controllerConfig,
                                        float2 moveInput,
                                        float3 lastValidMovementDirection,
                                        ref PlayerDashState dashState,
                                        DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                        Entity playerEntity,
                                        ref ComponentLookup<PlayerHealth> healthLookup,
                                        ref PlayerHealth updatedHealth,
                                        ref bool healthChanged)
    {
        if (slotConfig.IsDefined == 0)
            return;

        if (CanExecuteTool(slotConfig,
                           dashState,
                           movementState,
                           controllerConfig,
                           localTransform,
                           moveInput,
                           lastValidMovementDirection) == false)
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

        ExecuteTool(slotConfig,
                    in localTransform,
                    in movementState,
                    in controllerConfig,
                    moveInput,
                    lastValidMovementDirection,
                    ref dashState,
                    bombRequests);
    }

    private static bool CanExecuteTool(in PlayerPowerUpSlotConfig slotConfig,
                                       in PlayerDashState dashState,
                                       in PlayerMovementState movementState,
                                       in PlayerControllerConfig controllerConfig,
                                       in LocalTransform localTransform,
                                       float2 moveInput,
                                       float3 lastValidMovementDirection)
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

    private static void ExecuteTool(in PlayerPowerUpSlotConfig slotConfig,
                                    in LocalTransform localTransform,
                                    in PlayerMovementState movementState,
                                    in PlayerControllerConfig controllerConfig,
                                    float2 moveInput,
                                    float3 lastValidMovementDirection,
                                    ref PlayerDashState dashState,
                                    DynamicBuffer<PlayerBombSpawnRequest> bombRequests)
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
