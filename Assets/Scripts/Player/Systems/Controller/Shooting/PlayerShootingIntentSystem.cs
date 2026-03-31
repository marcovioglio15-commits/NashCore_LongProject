using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// This system processes player input and shooting state to determine when players should shoot 
/// and enqueues shoot requests accordingly. 
/// It runs after the PlayerLookDirectionSystem to ensure that the player's look direction is updated
/// before processing shooting logic, and after the PlayerMovementApplySystem to ensure that player movement is applied before 
/// determining shooting parameters like spawn position and projectile speed inheritance. Updates after these systems allows the PlayerShootingIntentSystem to have access to the most up-to-date player state information when generating
/// shoot requests, ensuring that shooting behavior is responsive and consistent with player input 
/// and movement.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookDirectionSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerShootingIntentSystem : ISystem
{
    #region Constants
    private const int MaxAutomaticShotsPerFrame = 4;
    private const float DirectionLengthEpsilon = 1e-6f;
    #endregion

    #region Lifecycle
    /// <summary>
    /// Configures the system to require updates for player entities that have 
    /// the necessary components for processing shooting logic,
    /// as well as the ShootRequest buffer to ensure that the system only runs when 
    /// there are relevant entities to process.
    /// </summary>
    /// <param name="state"></param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerShootingState>();
        state.RequireForUpdate<PlayerRuntimeShootingConfig>();
        state.RequireForUpdate<PlayerRuntimeShootingAppliedElementSlot>();
        state.RequireForUpdate<ShooterProjectilePrefab>();
        state.RequireForUpdate<ShootRequest>();
    }

    /// <summary>
    /// Processes player input and shooting state to enqueue shoot requests for each player entity based on their
    /// shooting configuration and current input.
    /// </summary>
    /// <param name="state">The current system state for the update.</param>
    public void OnUpdate(ref SystemState state)
    {
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<PlayerPassiveToolsState> passiveToolsLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(true);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(true);

        // for each player,
        // determine if they should shoot based on their input and shooting mode,
        // and if so, enqueue shoot requests with the appropriate parameters for projectile spawning
        foreach ((RefRO<PlayerInputState> inputState,
                  RefRO<PlayerLookState> lookState,
                  RefRO<PlayerRuntimeShootingConfig> runtimeShootingConfig,
                  DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                  RefRO<LocalTransform> localTransform,
                  RefRW<PlayerShootingState> shootingState,
                  DynamicBuffer<ShootRequest> shootRequests,
                  Entity entity) in SystemAPI.Query<RefRO<PlayerInputState>,
                                                   RefRO<PlayerLookState>,
                                                   RefRO<PlayerRuntimeShootingConfig>,
                                                   DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot>,
                                                   RefRO<LocalTransform>,
                                                   RefRW<PlayerShootingState>,
                                                   DynamicBuffer<ShootRequest>>().WithEntityAccess())
        {
            // if shooting is disabled in the config, skip processing shooting logic for this player
            PlayerRuntimeShootingConfig shootingConfig = runtimeShootingConfig.ValueRO;
            ShootingValuesBlob values = shootingConfig.Values;
            byte inheritPlayerSpeed = shootingConfig.ProjectilesInheritPlayerSpeed;
            PlayerPassiveToolsState passiveToolsState = ResolvePassiveToolsState(entity, in passiveToolsLookup);
            bool isShootingSuppressed = false;

            if (powerUpsStateLookup.HasComponent(entity))
                isShootingSuppressed = powerUpsStateLookup[entity].IsShootingSuppressed != 0;

            float projectileScaleMultiplier = math.max(0.01f,
                                                       values.ProjectileSizeMultiplier * math.max(0.01f, passiveToolsState.ProjectileSizeMultiplier));
            float projectileSpeed = math.max(0f, values.ShootSpeed * math.max(0f, passiveToolsState.ProjectileSpeedMultiplier));
            float projectileDamage = math.max(0f, values.Damage * math.max(0f, passiveToolsState.ProjectileDamageMultiplier));
            float projectileExplosionRadius = math.max(0f, values.ExplosionRadius);
            float projectileLifetime = ApplyLifetimeMultiplier(values.Lifetime, passiveToolsState.ProjectileLifetimeSecondsMultiplier);
            float projectileRange = ApplyLifetimeMultiplier(values.Range, passiveToolsState.ProjectileLifetimeRangeMultiplier);
            ProjectileKnockbackSettingsBlob projectileKnockback = values.Knockback;
            bool hasPassiveShotgunPayload = passiveToolsState.HasShotgun != 0;
            int passiveShotgunProjectileCount = hasPassiveShotgunPayload ? math.max(1, passiveToolsState.Shotgun.ProjectileCount) : 1;
            float passiveShotgunConeAngle = hasPassiveShotgunPayload ? math.max(0f, passiveToolsState.Shotgun.ConeAngleDegrees) : 0f;
            ProjectilePenetrationMode basePenetrationMode = values.PenetrationMode;
            int baseMaxPenetrations = math.max(0, values.MaxPenetrations);
            bool hasDefaultElementalPayload = PlayerProjectileElementUtility.TryBuildDefaultPayload(appliedElementSlots,
                                                                                                    in values,
                                                                                                    out ProjectileElementalPayload defaultElementalPayload);
            bool isShootPressed = inputState.ValueRO.Shoot > 0.5f;
            bool usesAutomaticLatch = shootingConfig.TriggerMode == ShootingTriggerMode.AutomaticToggle;

            if (!usesAutomaticLatch && shootingState.ValueRO.AutomaticEnabled != 0)
                shootingState.ValueRW.AutomaticEnabled = 0;

            if (isShootingSuppressed)
            {
                shootingState.ValueRW.PreviousShootPressed = isShootPressed ? (byte)1 : (byte)0;
                shootingState.ValueRW.VisualShootingActive = 0;

                if (values.RateOfFire > 0f)
                    ResetShotSchedule(ref shootingState.ValueRW, elapsedTime);

                continue;
            }

            // if rate of fire or shoot speed is zero or negative, treat as shooting disabled and skip shooting logic
            if (values.RateOfFire <= 0f || projectileSpeed <= 0f)
            {
                shootingState.ValueRW.PreviousShootPressed = isShootPressed ? (byte)1 : (byte)0;
                shootingState.ValueRW.VisualShootingActive = 0;
                ResetShotSchedule(ref shootingState.ValueRW, elapsedTime);
                continue;
            }

            // determine if the shoot button is currently pressed and if it was just pressed this frame
            bool shootPressedThisFrame = isShootPressed && shootingState.ValueRO.PreviousShootPressed == 0;
            shootingState.ValueRW.PreviousShootPressed = isShootPressed ? (byte)1 : (byte)0;
            bool automaticWasEnabled = usesAutomaticLatch && shootingState.ValueRO.AutomaticEnabled != 0;
            float shotInterval = 1f / values.RateOfFire;

            // Manual continuous fire must restart from the current frame, otherwise idle time becomes a burst backlog.
            if (shootingConfig.TriggerMode == ShootingTriggerMode.ManualContinousShot && shootPressedThisFrame)
                ResetShotSchedule(ref shootingState.ValueRW, elapsedTime);

            // based on the shooting trigger mode, determine if the player should shoot this frame
            bool shouldShoot = ResolveShootingTrigger(ref shootingState.ValueRW,
                                                      shootingConfig.TriggerMode,
                                                      isShootPressed,
                                                      shootPressedThisFrame);
            bool automaticIsEnabled = usesAutomaticLatch && shootingState.ValueRW.AutomaticEnabled != 0;
            bool visualShootingActive = isShootPressed;

            if (shootingConfig.TriggerMode == ShootingTriggerMode.AutomaticToggle)
            {
                visualShootingActive = visualShootingActive || automaticIsEnabled;
                bool automaticEnabledThisFrame = !automaticWasEnabled && automaticIsEnabled;
                bool automaticDisabledThisFrame = automaticWasEnabled && !automaticIsEnabled;

                if (automaticDisabledThisFrame)
                {
                    shootingState.ValueRW.VisualShootingActive = 0;
                    shootingState.ValueRW.NextShotTime = elapsedTime + shotInterval;
                    continue;
                }

                if (automaticEnabledThisFrame)
                    ResetShotSchedule(ref shootingState.ValueRW, elapsedTime);
            }

            shootingState.ValueRW.VisualShootingActive = visualShootingActive ? (byte)1 : (byte)0;

            if (!shouldShoot)
                continue;

            // compute how many shots to fire this frame based on the elapsed time and the player's rate of fire,
            // ensuring don't exceed the maximum allowed shots per frame for automatic fire
            int shotsToFire = ComputeShotsToFire(ref shootingState.ValueRW, shootingConfig.TriggerMode, elapsedTime, shotInterval);

            if (shotsToFire <= 0)
                continue;

            // compute the shoot direction based on the player's look direction,
            // falling back to their forward direction if the look direction is zero
            float3 forwardFallback = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRO.Rotation), new float3(0f, 0f, 1f));
            float3 shootDirection = PlayerControllerMath.NormalizePlanar(lookState.ValueRO.DesiredDirection, forwardFallback);
            float3 spawnPosition = PlayerShootOriginUtility.ResolveSpawnPosition(entity,
                                                                                in localTransform.ValueRO,
                                                                                in shootingConfig.ShootOffset,
                                                                                in muzzleLookup,
                                                                                in transformLookup,
                                                                                in localToWorldLookup);

            // enqueue the appropriate number of shoot requests with the resolved spawn position,
            // shoot direction, and shooting parameters from the config
            for (int shotIndex = 0; shotIndex < shotsToFire; shotIndex++)
            {
                if (passiveShotgunProjectileCount <= 1)
                {
                    AddShootRequest(shootRequests,
                                    spawnPosition,
                                    shootDirection,
                                    projectileSpeed,
                                    projectileExplosionRadius,
                                    projectileRange,
                                    projectileLifetime,
                                    projectileDamage,
                                    projectileScaleMultiplier,
                                    in projectileKnockback,
                                    inheritPlayerSpeed,
                                    basePenetrationMode,
                                    baseMaxPenetrations,
                                    hasDefaultElementalPayload,
                                    in defaultElementalPayload);
                    continue;
                }

                float halfCone = passiveShotgunConeAngle * 0.5f;
                float spreadStep = passiveShotgunConeAngle / (passiveShotgunProjectileCount - 1);

                for (int projectileIndex = 0; projectileIndex < passiveShotgunProjectileCount; projectileIndex++)
                {
                    float spreadAngleDegrees = -halfCone + spreadStep * projectileIndex;
                    quaternion rotationOffset = quaternion.AxisAngle(new float3(0f, 1f, 0f), math.radians(spreadAngleDegrees));
                    float3 spreadDirection = math.normalizesafe(math.rotate(rotationOffset, shootDirection), shootDirection);

                    if (math.lengthsq(spreadDirection) <= DirectionLengthEpsilon)
                        spreadDirection = shootDirection;

                    AddShootRequest(shootRequests,
                                    spawnPosition,
                                    spreadDirection,
                                    projectileSpeed,
                                    projectileExplosionRadius,
                                    projectileRange,
                                    projectileLifetime,
                                    projectileDamage,
                                    projectileScaleMultiplier,
                                    in projectileKnockback,
                                    inheritPlayerSpeed,
                                    basePenetrationMode,
                                    baseMaxPenetrations,
                                    hasDefaultElementalPayload,
                                    in defaultElementalPayload);
                }
            }
        }
    }

    #endregion


    #region Helpers
    /// <summary>
    /// This method determines whether the player should shoot 
    /// based on their shooting trigger mode and current input state.
    /// </summary>
    /// <param name="shootingState"></param>
    /// <param name="triggerMode"></param>
    /// <param name="isShootPressed"></param>
    /// <param name="shootPressedThisFrame"></param>
    /// <returns><returns>
    private static bool ResolveShootingTrigger(ref PlayerShootingState shootingState,
                                               ShootingTriggerMode triggerMode,
                                               bool isShootPressed,
                                               bool shootPressedThisFrame)
    {
        switch (triggerMode)
        {
            case ShootingTriggerMode.AutomaticToggle:
                if (shootPressedThisFrame)
                    shootingState.AutomaticEnabled = shootingState.AutomaticEnabled == 0 ? (byte)1 : (byte)0;

                return shootingState.AutomaticEnabled != 0;
            case ShootingTriggerMode.ManualSingleShot:
                return shootPressedThisFrame;
            case ShootingTriggerMode.ManualContinousShot:
                shootingState.AutomaticEnabled = 0;
                return isShootPressed;
            default:
                shootingState.AutomaticEnabled = 0;
                return false;
        }
    }

    /// <summary>
    /// This method computes how many shots the player should fire 
    /// in the current frame based on their shooting state,
    /// and the elapsed time since the last shot, ensuring that the number of shots fired 
    /// does not exceed the maximum allowed for automatic fire.
    /// </summary>
    /// <param name="shootingState"></param>
    /// <param name="triggerMode"></param>
    /// <param name="elapsedTime"></param>
    /// <param name="shotInterval"></param>
    /// <returns><returns>
    private static int ComputeShotsToFire(ref PlayerShootingState shootingState, ShootingTriggerMode triggerMode, float elapsedTime, float shotInterval)
    {
        float nextShotTime = shootingState.NextShotTime;

        if (nextShotTime <= 0f)
            nextShotTime = elapsedTime;

        int shotsToFire = 0;

        switch (triggerMode)
        {
            case ShootingTriggerMode.ManualContinousShot:
            case ShootingTriggerMode.AutomaticToggle:
                if (elapsedTime < nextShotTime)
                    break;

                float lag = elapsedTime - nextShotTime;
                shotsToFire = 1 + (int)math.floor(lag / shotInterval);
                shotsToFire = math.clamp(shotsToFire, 1, MaxAutomaticShotsPerFrame);
                nextShotTime += shotInterval * shotsToFire;
                break;
            case ShootingTriggerMode.ManualSingleShot:
                if (elapsedTime < nextShotTime)
                    break;

                shotsToFire = 1;
                nextShotTime = elapsedTime + shotInterval;
                break;
            default:
                shotsToFire = 0;
                nextShotTime = elapsedTime + shotInterval;
                break;
        }

        shootingState.NextShotTime = nextShotTime;
        return shotsToFire;
    }

    /// <summary>
    /// Resets the next-shot schedule to the current frame so idle or temporarily disabled fire does not accumulate
    /// deferred automatic shots.
    /// shootingState: Mutable firing state that stores the next scheduled shot time.
    /// elapsedTime: Current world elapsed time used as the new schedule anchor.
    /// returns None.
    /// </summary>
    private static void ResetShotSchedule(ref PlayerShootingState shootingState,
                                          float elapsedTime)
    {
        shootingState.NextShotTime = elapsedTime;
    }

    private static PlayerPassiveToolsState ResolvePassiveToolsState(Entity shooterEntity,
                                                                    in ComponentLookup<PlayerPassiveToolsState> passiveToolsLookup)
    {
        if (passiveToolsLookup.HasComponent(shooterEntity))
            return passiveToolsLookup[shooterEntity];

        return new PlayerPassiveToolsState
        {
            ProjectileSizeMultiplier = 1f,
            ProjectileDamageMultiplier = 1f,
            ProjectileSpeedMultiplier = 1f,
            ProjectileLifetimeSecondsMultiplier = 1f,
            ProjectileLifetimeRangeMultiplier = 1f,
            HasShotgun = 0,
            Shotgun = default
        };
    }

    private static float ApplyLifetimeMultiplier(float baseLifetimeValue, float lifetimeMultiplier)
    {
        if (baseLifetimeValue <= 0f)
            return baseLifetimeValue;

        return math.max(0f, baseLifetimeValue * math.max(0f, lifetimeMultiplier));
    }

    private static void AddShootRequest(DynamicBuffer<ShootRequest> shootRequests,
                                        float3 spawnPosition,
                                        float3 shootDirection,
                                        float projectileSpeed,
                                        float projectileExplosionRadius,
                                        float projectileRange,
                                        float projectileLifetime,
                                        float projectileDamage,
                                        float projectileScaleMultiplier,
                                        in ProjectileKnockbackSettingsBlob projectileKnockback,
                                        byte inheritPlayerSpeed,
                                        ProjectilePenetrationMode penetrationMode,
                                        int maxPenetrations,
                                        bool hasElementalPayloadOverride,
                                        in ProjectileElementalPayload elementalPayloadOverride)
    {
        ShootRequest request = new ShootRequest
        {
            Position = spawnPosition,
            Direction = shootDirection,
            Speed = projectileSpeed,
            ExplosionRadius = projectileExplosionRadius,
            Range = projectileRange,
            Lifetime = projectileLifetime,
            Damage = projectileDamage,
            ProjectileScaleMultiplier = projectileScaleMultiplier,
            PenetrationMode = penetrationMode,
            MaxPenetrations = maxPenetrations,
            KnockbackEnabled = projectileKnockback.Enabled,
            KnockbackStrength = math.max(0f, projectileKnockback.Strength),
            KnockbackDurationSeconds = math.max(0f, projectileKnockback.DurationSeconds),
            KnockbackDirectionMode = projectileKnockback.DirectionMode,
            KnockbackStackingMode = projectileKnockback.StackingMode,
            InheritPlayerSpeed = inheritPlayerSpeed,
            IsSplitChild = 0,
            ElementalPayloadOverride = hasElementalPayloadOverride ? elementalPayloadOverride : default
        };
        shootRequests.Add(request);
    }
    #endregion
}
