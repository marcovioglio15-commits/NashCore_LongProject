using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Contains execution helpers for active power-up runtime effects such as projectiles, bombs, dash and bullet time.
/// </summary>
public static class PlayerPowerUpActivationExecutionUtility
{
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
        public byte HasElementalPayloadOverride;
        public ElementalEffectConfig ElementalEffectOverride;
        public float ElementalStacksPerHitOverride;
    }
    #endregion

    #region Methods

    #region Execute
    public static void ExecuteTool(in PlayerPowerUpSlotConfig slotConfig,
                                   in LocalTransform localTransform,
                                   in PlayerLookState lookState,
                                   in PlayerMovementState movementState,
                                   in PlayerControllerConfig controllerConfig,
                                   in PlayerPassiveToolsState passiveToolsState,
                                   in ComponentLookup<PlayerAnimatedMuzzleWorldPose> animatedMuzzleLookup,
                                   in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                   in ComponentLookup<LocalTransform> transformLookup,
                                   in ComponentLookup<LocalToWorld> localToWorldLookup,
                                   float2 moveInput,
                                   float3 lastValidMovementDirection,
                                   Entity playerEntity,
                                   ref PlayerDashState dashState,
                                   ref PlayerBulletTimeState bulletTimeState,
                                   DynamicBuffer<PlayerBombSpawnRequest> bombRequests,
                                   DynamicBuffer<ShootRequest> shootRequests)
    {
        switch (slotConfig.ToolKind)
        {
            case ActiveToolKind.Bomb:
                ExecuteBomb(in slotConfig, in localTransform, in lookState, in movementState, playerEntity, bombRequests);
                return;
            case ActiveToolKind.Dash:
                ExecuteDash(in slotConfig,
                            in movementState,
                            in controllerConfig,
                            in localTransform,
                            moveInput,
                            lastValidMovementDirection,
                            ref dashState);
                return;
            case ActiveToolKind.BulletTime:
                ExecuteBulletTime(in slotConfig, ref bulletTimeState);
                return;
            case ActiveToolKind.Shotgun:
                ExecuteShotgun(in slotConfig,
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
                return;
            case ActiveToolKind.PortableHealthPack:
                return;
            case ActiveToolKind.PassiveToggle:
                return;
        }
    }

    public static void ExecuteChargeShot(in PlayerPowerUpSlotConfig slotConfig,
                                         in LocalTransform localTransform,
                                         in PlayerLookState lookState,
                                         in PlayerControllerConfig controllerConfig,
                                         in PlayerPassiveToolsState passiveToolsState,
                                         Entity playerEntity,
                                         in ComponentLookup<PlayerAnimatedMuzzleWorldPose> animatedMuzzleLookup,
                                         in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                         in ComponentLookup<LocalTransform> transformLookup,
                                         in ComponentLookup<LocalToWorld> localToWorldLookup,
                                         DynamicBuffer<ShootRequest> shootRequests)
    {
        bool hasPassiveShotgunPayload = passiveToolsState.HasShotgun != 0;
        int projectileCount = hasPassiveShotgunPayload ? math.max(1, passiveToolsState.Shotgun.ProjectileCount) : 1;
        float coneAngleDegrees = hasPassiveShotgunPayload ? math.max(0f, passiveToolsState.Shotgun.ConeAngleDegrees) : 0f;
        ProjectilePenetrationMode penetrationMode = slotConfig.ChargeShot.PenetrationMode;
        int maxPenetrations = math.max(0, slotConfig.ChargeShot.MaxPenetrations);

        if (hasPassiveShotgunPayload)
        {
            penetrationMode = (ProjectilePenetrationMode)math.max((int)penetrationMode, (int)passiveToolsState.Shotgun.PenetrationMode);
            maxPenetrations = math.max(maxPenetrations, math.max(0, passiveToolsState.Shotgun.MaxPenetrations));
        }

        float3 shootDirection = ResolveShootDirection(in lookState, in localTransform);
        float3 spawnPosition = ResolveShootSpawnPosition(playerEntity,
                                                         in localTransform,
                                                         in controllerConfig,
                                                         in animatedMuzzleLookup,
                                                         in muzzleLookup,
                                                         in transformLookup,
                                                         in localToWorldLookup);
        ProjectileRequestTemplate template = BuildProjectileTemplate(in controllerConfig,
                                                                     in passiveToolsState,
                                                                     slotConfig.ChargeShot.SizeMultiplier,
                                                                     slotConfig.ChargeShot.DamageMultiplier,
                                                                     slotConfig.ChargeShot.SpeedMultiplier,
                                                                     slotConfig.ChargeShot.RangeMultiplier,
                                                                     slotConfig.ChargeShot.LifetimeMultiplier,
                                                                     slotConfig.ChargeShot.HasElementalPayload != 0,
                                                                     in slotConfig.ChargeShot.ElementalEffect,
                                                                     slotConfig.ChargeShot.ElementalStacksPerHit);

        AddShotgunBurst(ref shootRequests,
                        projectileCount,
                        coneAngleDegrees,
                        spawnPosition,
                        shootDirection,
                        in template,
                        penetrationMode,
                        maxPenetrations);
    }

    private static void ExecuteBomb(in PlayerPowerUpSlotConfig slotConfig,
                                    in LocalTransform localTransform,
                                    in PlayerLookState lookState,
                                    in PlayerMovementState movementState,
                                    Entity playerEntity,
                                    DynamicBuffer<PlayerBombSpawnRequest> bombRequests)
    {
        float3 bombDirection = ResolveBombActivationDirection(in movementState, in localTransform);
        quaternion spawnOffsetRotation = ResolveSpawnOffsetRotation(in slotConfig.Bomb, in localTransform, in lookState);
        float3 worldSpawnOffset = math.rotate(spawnOffsetRotation, slotConfig.Bomb.SpawnOffset);
        float3 spawnPosition = localTransform.Position + worldSpawnOffset;
        float deploySpeed = math.max(0f, slotConfig.Bomb.DeploySpeed);
        float3 initialVelocity = bombDirection * deploySpeed;
        byte enableDamagePayload = slotConfig.Bomb.EnableDamagePayload;
        float radius = enableDamagePayload != 0 ? math.max(0.1f, slotConfig.Bomb.Radius) : 0f;
        float damage = enableDamagePayload != 0 ? math.max(0f, slotConfig.Bomb.Damage) : 0f;
        byte affectAll = enableDamagePayload != 0 ? slotConfig.Bomb.AffectAllEnemiesInRadius : (byte)0;
        Entity explosionVfxPrefabEntity = enableDamagePayload != 0 ? slotConfig.Bomb.ExplosionVfxPrefabEntity : Entity.Null;
        byte scaleVfxToRadius = enableDamagePayload != 0 ? slotConfig.Bomb.ScaleVfxToRadius : (byte)0;
        float vfxScaleMultiplier = enableDamagePayload != 0 ? math.max(0.01f, slotConfig.Bomb.VfxScaleMultiplier) : 1f;

        bombRequests.Add(new PlayerBombSpawnRequest
        {
            OwnerEntity = playerEntity,
            BombPrefabEntity = slotConfig.BombPrefabEntity,
            Position = spawnPosition,
            Rotation = quaternion.LookRotationSafe(bombDirection, new float3(0f, 1f, 0f)),
            Velocity = initialVelocity,
            CollisionRadius = math.max(0.01f, slotConfig.Bomb.CollisionRadius),
            BounceOnWalls = slotConfig.Bomb.BounceOnWalls,
            BounceDamping = math.clamp(slotConfig.Bomb.BounceDamping, 0f, 1f),
            LinearDampingPerSecond = math.max(0f, slotConfig.Bomb.LinearDampingPerSecond),
            FuseSeconds = math.max(0.05f, slotConfig.Bomb.FuseSeconds),
            Radius = radius,
            Damage = damage,
            AffectAllEnemiesInRadius = affectAll,
            ExplosionVfxPrefabEntity = explosionVfxPrefabEntity,
            ScaleVfxToRadius = scaleVfxToRadius,
            VfxScaleMultiplier = vfxScaleMultiplier
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
        if (!TryResolveDashActivationDirection(in movementState,
                                               in controllerConfig,
                                               in localTransform,
                                               moveInput,
                                               lastValidMovementDirection,
                                               out float3 dashDirection))
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
                                       Entity playerEntity,
                                       in ComponentLookup<PlayerAnimatedMuzzleWorldPose> animatedMuzzleLookup,
                                       in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                       in ComponentLookup<LocalTransform> transformLookup,
                                       in ComponentLookup<LocalToWorld> localToWorldLookup,
                                       DynamicBuffer<ShootRequest> shootRequests)
    {
        int projectileCount = math.max(1, slotConfig.Shotgun.ProjectileCount);
        float coneAngleDegrees = math.max(0f, slotConfig.Shotgun.ConeAngleDegrees);
        float3 shootDirection = ResolveShootDirection(in lookState, in localTransform);
        float3 spawnPosition = ResolveShootSpawnPosition(playerEntity,
                                                         in localTransform,
                                                         in controllerConfig,
                                                         in animatedMuzzleLookup,
                                                         in muzzleLookup,
                                                         in transformLookup,
                                                         in localToWorldLookup);
        ProjectileRequestTemplate template = BuildProjectileTemplate(in controllerConfig,
                                                                     in passiveToolsState,
                                                                     slotConfig.Shotgun.SizeMultiplier,
                                                                     slotConfig.Shotgun.DamageMultiplier,
                                                                     slotConfig.Shotgun.SpeedMultiplier,
                                                                     slotConfig.Shotgun.RangeMultiplier,
                                                                     slotConfig.Shotgun.LifetimeMultiplier,
                                                                     slotConfig.Shotgun.HasElementalPayload != 0,
                                                                     in slotConfig.Shotgun.ElementalEffect,
                                                                     slotConfig.Shotgun.ElementalStacksPerHit);

        AddShotgunBurst(ref shootRequests,
                        projectileCount,
                        coneAngleDegrees,
                        spawnPosition,
                        shootDirection,
                        in template,
                        slotConfig.Shotgun.PenetrationMode,
                        slotConfig.Shotgun.MaxPenetrations);
    }

    private static void AddShotgunBurst(ref DynamicBuffer<ShootRequest> shootRequests,
                                        int projectileCount,
                                        float coneAngleDegrees,
                                        float3 spawnPosition,
                                        float3 shootDirection,
                                        in ProjectileRequestTemplate template,
                                        ProjectilePenetrationMode penetrationMode,
                                        int maxPenetrations)
    {
        if (projectileCount <= 1)
        {
            AddShootRequest(ref shootRequests,
                            spawnPosition,
                            shootDirection,
                            in template,
                            penetrationMode,
                            maxPenetrations,
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
                            penetrationMode,
                            maxPenetrations,
                            0);
        }
    }
    #endregion

    #region Projectile Helpers
    private static float3 ResolveShootDirection(in PlayerLookState lookState, in LocalTransform localTransform)
    {
        float3 lookDirection = lookState.DesiredDirection;
        lookDirection.y = 0f;

        if (math.lengthsq(lookDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
            return math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));

        float3 fallbackDirection = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.Rotation), new float3(0f, 0f, 1f));
        return math.normalizesafe(fallbackDirection, new float3(0f, 0f, 1f));
    }

    private static float3 ResolveShootSpawnPosition(Entity playerEntity,
                                                    in LocalTransform localTransform,
                                                    in PlayerControllerConfig controllerConfig,
                                                    in ComponentLookup<PlayerAnimatedMuzzleWorldPose> animatedMuzzleLookup,
                                                    in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                                    in ComponentLookup<LocalTransform> transformLookup,
                                                    in ComponentLookup<LocalToWorld> localToWorldLookup)
    {
        float3 shootOffset = controllerConfig.Config.Value.Shooting.ShootOffset;
        return PlayerShootOriginUtility.ResolveSpawnPosition(playerEntity,
                                                             in localTransform,
                                                             in shootOffset,
                                                             in animatedMuzzleLookup,
                                                             in muzzleLookup,
                                                             in transformLookup,
                                                             in localToWorldLookup);
    }

    private static ProjectileRequestTemplate BuildProjectileTemplate(in PlayerControllerConfig controllerConfig,
                                                                     in PlayerPassiveToolsState passiveToolsState,
                                                                     float sizeMultiplier,
                                                                     float damageMultiplier,
                                                                     float speedMultiplier,
                                                                     float rangeMultiplier,
                                                                     float lifetimeMultiplier,
                                                                     bool hasElementalPayloadOverride,
                                                                     in ElementalEffectConfig elementalEffectOverride,
                                                                     float elementalStacksPerHitOverride)
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
            InheritPlayerSpeed = shootingConfig.ProjectilesInheritPlayerSpeed,
            HasElementalPayloadOverride = hasElementalPayloadOverride ? (byte)1 : (byte)0,
            ElementalEffectOverride = elementalEffectOverride,
            ElementalStacksPerHitOverride = math.max(0f, elementalStacksPerHitOverride)
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
            IsSplitChild = isSplitChild,
            HasElementalPayloadOverride = template.HasElementalPayloadOverride,
            ElementalEffectOverride = template.ElementalEffectOverride,
            ElementalStacksPerHitOverride = template.ElementalStacksPerHitOverride
        });
    }
    #endregion

    #region Movement Helpers
    private static quaternion ResolveSpawnOffsetRotation(in BombPowerUpConfig bombConfig,
                                                         in LocalTransform localTransform,
                                                         in PlayerLookState lookState)
    {
        switch (bombConfig.SpawnOffsetOrientation)
        {
            case SpawnOffsetOrientationMode.PlayerLookDirection:
                float3 lookDirection = lookState.DesiredDirection;
                lookDirection.y = 0f;

                if (math.lengthsq(lookDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
                {
                    float3 normalizedLook = math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));
                    return quaternion.LookRotationSafe(normalizedLook, new float3(0f, 1f, 0f));
                }

                return localTransform.Rotation;
            case SpawnOffsetOrientationMode.WorldForward:
                return quaternion.identity;
            default:
                return localTransform.Rotation;
        }
    }

    private static float3 ResolveBombActivationDirection(in PlayerMovementState movementState, in LocalTransform localTransform)
    {
        float3 movementDirection = movementState.Velocity;
        movementDirection.y = 0f;

        if (math.lengthsq(movementDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
            return math.normalizesafe(-movementDirection, new float3(0f, 0f, -1f));

        movementDirection = movementState.DesiredDirection;
        movementDirection.y = 0f;

        if (math.lengthsq(movementDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
            return math.normalizesafe(-movementDirection, new float3(0f, 0f, -1f));

        float3 backwardDirection = -math.forward(localTransform.Rotation);
        backwardDirection.y = 0f;
        return math.normalizesafe(backwardDirection, new float3(0f, 0f, -1f));
    }

    public static bool TryResolveDashActivationDirection(in PlayerMovementState movementState,
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

        if (math.lengthsq(desiredDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(desiredDirection, new float3(0f, 0f, 1f));
            return true;
        }

        float3 velocityDirection = movementState.Velocity;
        velocityDirection.y = 0f;

        if (math.lengthsq(velocityDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
        {
            dashDirection = math.normalizesafe(velocityDirection, new float3(0f, 0f, 1f));
            return true;
        }

        if (math.lengthsq(lastValidMovementDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
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

        if (!PlayerControllerMath.IsDiagonalMask(previousMask))
        {
            dashDirection = float3.zero;
            return false;
        }

        if (!PlayerControllerMath.IsSingleAxisMask(currentMask))
        {
            dashDirection = float3.zero;
            return false;
        }

        if (!PlayerControllerMath.IsReleaseOnly(previousMask, currentMask))
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

        if (math.lengthsq(inputDirection) <= PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon)
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
                return math.lengthsq(dashDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon;
            default:
                float3 freeDirection = right * inputDirection.x + forward * inputDirection.y;
                dashDirection = math.normalizesafe(freeDirection, forward);
                return math.lengthsq(dashDirection) > PlayerPowerUpActivationUtilityConstants.DirectionLengthEpsilon;
        }
    }
    #endregion

    #endregion
}
