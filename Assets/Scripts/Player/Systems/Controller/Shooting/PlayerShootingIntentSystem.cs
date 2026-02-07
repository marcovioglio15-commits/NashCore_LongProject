using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookDirectionSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerShootingIntentSystem : ISystem
{
    #region Constants
    private const int MaxAutomaticShotsPerFrame = 4;
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerShootingState>();
        state.RequireForUpdate<ShooterProjectilePrefab>();
        state.RequireForUpdate<ShootRequest>();
    }
    #endregion

    #region Update
    /// <summary>
    /// Processes player input and shooting state to enqueue shoot requests for each player entity based on their
    /// shooting configuration and current input.
    /// </summary>
    /// <param name="state">The current system state for the update.</param>
    public void OnUpdate(ref SystemState state)
    {
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        ComponentLookup<ShooterMuzzleAnchor> muzzleLookup = SystemAPI.GetComponentLookup<ShooterMuzzleAnchor>(true);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

        // for each player,
        // determine if they should shoot based on their input and shooting mode,
        // and if so, enqueue shoot requests with the appropriate parameters for projectile spawning
        foreach ((RefRO<PlayerInputState> inputState,
                  RefRO<PlayerLookState> lookState,
                  RefRO<PlayerControllerConfig> controllerConfig,
                  RefRO<LocalTransform> localTransform,
                  RefRW<PlayerShootingState> shootingState,
                  DynamicBuffer<ShootRequest> shootRequests,
                  Entity entity) in SystemAPI.Query<RefRO<PlayerInputState>,
                                                   RefRO<PlayerLookState>,
                                                   RefRO<PlayerControllerConfig>,
                                                   RefRO<LocalTransform>,
                                                   RefRW<PlayerShootingState>,
                                                   DynamicBuffer<ShootRequest>>().WithEntityAccess())
        {
            // if shooting is disabled in the config, skip processing shooting logic for this player
            ref ShootingConfig shootingConfig = ref controllerConfig.ValueRO.Config.Value.Shooting;
            ref ShootingValuesBlob values = ref shootingConfig.Values;
            byte inheritPlayerSpeed = shootingConfig.ProjectilesInheritPlayerSpeed;

            // if rate of fire or shoot speed is zero or negative, treat as shooting disabled and skip shooting logic
            if (values.RateOfFire <= 0f || values.ShootSpeed <= 0f)
            {
                shootingState.ValueRW.PreviousShootPressed = inputState.ValueRO.Shoot > 0.5f ? (byte)1 : (byte)0;
                continue;
            }

            // determine if the shoot button is currently pressed and if it was just pressed this frame
            bool isShootPressed = inputState.ValueRO.Shoot > 0.5f;
            bool shootPressedThisFrame = isShootPressed && shootingState.ValueRO.PreviousShootPressed == 0;
            shootingState.ValueRW.PreviousShootPressed = isShootPressed ? (byte)1 : (byte)0;

            // based on the shooting trigger mode, determine if the player should shoot this frame
            bool shouldShoot = ResolveShootingTrigger(ref shootingState.ValueRW, shootingConfig.TriggerMode, shootPressedThisFrame);

            if (shouldShoot == false)
                continue;

            // compute how many shots to fire this frame based on the elapsed time and the player's rate of fire,
            // ensuring we don't exceed the maximum allowed shots per frame for automatic fire
            float shotInterval = 1f / values.RateOfFire;
            int shotsToFire = ComputeShotsToFire(ref shootingState.ValueRW, shootingConfig.TriggerMode, elapsedTime, shotInterval);

            if (shotsToFire <= 0)
                continue;

            // compute the shoot direction based on the player's look direction,
            // falling back to their forward direction if the look direction is zero
            float3 forwardFallback = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRO.Rotation), new float3(0f, 0f, 1f));
            float3 shootDirection = PlayerControllerMath.NormalizePlanar(lookState.ValueRO.DesiredDirection, forwardFallback);
            float3 spawnPosition = ResolveSpawnPosition(entity, localTransform.ValueRO, shootingConfig.ShootOffset, muzzleLookup, transformLookup, localToWorldLookup);

            // enqueue the appropriate number of shoot requests with the resolved spawn position,
            // shoot direction, and shooting parameters from the config
            for (int shotIndex = 0; shotIndex < shotsToFire; shotIndex++)
            {
                ShootRequest request = new ShootRequest
                {
                    Position = spawnPosition,
                    Direction = shootDirection,
                    Speed = values.ShootSpeed,
                    Range = values.Range,
                    Lifetime = values.Lifetime,
                    Damage = values.Damage,
                    InheritPlayerSpeed = inheritPlayerSpeed
                };

                shootRequests.Add(request);
            }
        }
    }
    #endregion

    #region Helpers
    private static bool ResolveShootingTrigger(ref PlayerShootingState shootingState, ShootingTriggerMode triggerMode, bool shootPressedThisFrame)
    {
        switch (triggerMode)
        {
            case ShootingTriggerMode.AutomaticToggle:
                if (shootPressedThisFrame)
                    shootingState.AutomaticEnabled = shootingState.AutomaticEnabled == 0 ? (byte)1 : (byte)0;

                return shootingState.AutomaticEnabled != 0;
            case ShootingTriggerMode.ManualSingleShot:
                return shootPressedThisFrame;
            default:
                return false;
        }
    }

    private static int ComputeShotsToFire(ref PlayerShootingState shootingState, ShootingTriggerMode triggerMode, float elapsedTime, float shotInterval)
    {
        float nextShotTime = shootingState.NextShotTime;

        if (nextShotTime <= 0f)
            nextShotTime = elapsedTime;

        int shotsToFire = 0;

        switch (triggerMode)
        {
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
        }

        shootingState.NextShotTime = nextShotTime;
        return shotsToFire;
    }

    private static float3 ResolveSpawnPosition(Entity shooterEntity,
                                               in LocalTransform shooterTransform,
                                               in float3 shootOffset,
                                               in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                               in ComponentLookup<LocalTransform> transformLookup,
                                               in ComponentLookup<LocalToWorld> localToWorldLookup)
    {
        float3 referencePosition = shooterTransform.Position;
        quaternion referenceRotation = shooterTransform.Rotation;

        if (muzzleLookup.HasComponent(shooterEntity))
        {
            Entity muzzleEntity = muzzleLookup[shooterEntity].AnchorEntity;

            if (localToWorldLookup.HasComponent(muzzleEntity))
            {
                LocalToWorld localToWorld = localToWorldLookup[muzzleEntity];
                referencePosition = localToWorld.Value.c3.xyz;
                referenceRotation = quaternion.LookRotationSafe(localToWorld.Value.c2.xyz, localToWorld.Value.c1.xyz);
            }
            else if (transformLookup.HasComponent(muzzleEntity))
            {
                LocalTransform muzzleTransform = transformLookup[muzzleEntity];
                referencePosition = muzzleTransform.Position;
                referenceRotation = muzzleTransform.Rotation;
            }
        }

        float3 rotatedOffset = math.rotate(referenceRotation, shootOffset);
        return referencePosition + rotatedOffset;
    }
    #endregion
}
#endregion
