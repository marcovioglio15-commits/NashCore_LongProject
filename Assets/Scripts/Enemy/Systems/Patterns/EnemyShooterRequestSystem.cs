using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves Shooter module runtime and enqueues ShootRequest entries for enemy entities.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySpawnSystem))]
[UpdateBefore(typeof(EnemySteeringSystem))]
[UpdateBefore(typeof(EnemyPatternMovementSystem))]
public partial struct EnemyShooterRequestSystem : ISystem
{
    #region Constants
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();

        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate<EnemyShooterConfigElement>();
        state.RequireForUpdate<EnemyShooterRuntimeElement>();
        state.RequireForUpdate<ShootRequest>();
        state.RequireForUpdate<EnemyShooterControlState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        Entity playerEntity = Entity.Null;
        float3 playerPosition = float3.zero;

        foreach ((RefRO<LocalTransform> playerTransform,
                  Entity candidatePlayerEntity) in SystemAPI.Query<RefRO<LocalTransform>>()
                                                           .WithAll<PlayerControllerConfig>()
                                                           .WithEntityAccess())
        {
            playerEntity = candidatePlayerEntity;
            playerPosition = playerTransform.ValueRO.Position;
            break;
        }

        if (playerEntity == Entity.Null)
            return;

        if (!entityManager.Exists(playerEntity))
        {
            return;
        }

        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;

        if (deltaTime <= 0f)
            return;

        foreach ((DynamicBuffer<EnemyShooterConfigElement> shooterConfigs,
                  DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                  DynamicBuffer<ShootRequest> shootRequests,
                  RefRW<EnemyShooterControlState> shooterControlState,
                  RefRO<LocalTransform> enemyTransform)
                 in SystemAPI.Query<DynamicBuffer<EnemyShooterConfigElement>,
                                    DynamicBuffer<EnemyShooterRuntimeElement>,
                                    DynamicBuffer<ShootRequest>,
                                    RefRW<EnemyShooterControlState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>())
        {
            DynamicBuffer<EnemyShooterRuntimeElement> mutableShooterRuntime = shooterRuntime;
            DynamicBuffer<ShootRequest> mutableShootRequests = shootRequests;

            if (shooterConfigs.Length <= 0)
            {
                shooterControlState.ValueRW = new EnemyShooterControlState
                {
                    MovementLocked = 0
                };
                continue;
            }

            if (mutableShooterRuntime.Length != shooterConfigs.Length)
                SynchronizeShooterRuntime(mutableShooterRuntime, shooterConfigs.Length);

            float3 enemyPosition = enemyTransform.ValueRO.Position;
            float3 toPlayer = playerPosition - enemyPosition;
            toPlayer.y = 0f;
            float playerDistance = math.length(toPlayer);
            bool movementLocked = false;
            float3 resolvedAimDirection = float3.zero;
            bool hasResolvedAimDirection = false;
            int aimPriority = int.MinValue;

            for (int shooterIndex = 0; shooterIndex < shooterConfigs.Length; shooterIndex++)
            {
                EnemyShooterConfigElement shooterConfig = shooterConfigs[shooterIndex];
                EnemyShooterRuntimeElement runtime = mutableShooterRuntime[shooterIndex];

                runtime.NextBurstTimer = math.max(0f, runtime.NextBurstTimer - deltaTime);
                runtime.NextShotInBurstTimer = math.max(0f, runtime.NextShotInBurstTimer - deltaTime);
                runtime.IsPlayerInRange = IsInRange(playerDistance, in shooterConfig) ? (byte)1 : (byte)0;

                if (runtime.IsPlayerInRange == 0)
                {
                    runtime.RemainingBurstShots = 0;
                    runtime.ShotsFiredInCurrentBurst = 0;
                    runtime.BurstWindupDurationSeconds = 0f;
                    runtime.NextShotInBurstTimer = 0f;
                    runtime.HasLockedAimDirection = 0;
                    mutableShooterRuntime[shooterIndex] = runtime;
                    continue;
                }

                if (runtime.RemainingBurstShots <= 0 && runtime.NextBurstTimer <= 0f)
                {
                    runtime.RemainingBurstShots = math.max(1, shooterConfig.BurstCount);
                    runtime.ShotsFiredInCurrentBurst = 0;
                    runtime.BurstWindupDurationSeconds = math.max(0f, shooterConfig.AimWindupSeconds);
                    runtime.NextShotInBurstTimer = runtime.BurstWindupDurationSeconds;
                    runtime.NextBurstTimer = math.max(0.01f, shooterConfig.FireInterval);

                    if (shooterConfig.AimPolicy == EnemyShooterAimPolicy.LockOnFireStart)
                    {
                        runtime.LockedAimDirection = math.normalizesafe(toPlayer, ResolveForward(enemyTransform.ValueRO.Rotation));
                        runtime.HasLockedAimDirection = 1;
                    }
                }

                if (runtime.RemainingBurstShots > 0 &&
                    shooterConfig.MovementPolicy == EnemyShooterMovementPolicy.StopWhileAiming)
                {
                    movementLocked = true;
                }

                if (runtime.RemainingBurstShots > 0)
                {
                    float3 activeAimDirection = ResolveAimDirection(in shooterConfig,
                                                                    in runtime,
                                                                    toPlayer,
                                                                    enemyTransform.ValueRO.Rotation);
                    TryCaptureAimDirection(activeAimDirection,
                                           shooterConfig.MovementPolicy,
                                           ref resolvedAimDirection,
                                           ref hasResolvedAimDirection,
                                           ref aimPriority);
                }

                if (runtime.RemainingBurstShots > 0 && runtime.NextShotInBurstTimer <= 0f)
                {
                    float3 aimDirection = ResolveAimDirection(in shooterConfig,
                                                              in runtime,
                                                              toPlayer,
                                                              enemyTransform.ValueRO.Rotation);

                    EnqueueShotRequests(mutableShootRequests,
                                        enemyPosition,
                                        aimDirection,
                                        in shooterConfig);

                    runtime.RemainingBurstShots -= 1;
                    runtime.ShotsFiredInCurrentBurst += 1;

                    if (runtime.RemainingBurstShots > 0)
                    {
                        runtime.NextShotInBurstTimer = math.max(0f, shooterConfig.IntraBurstDelay);
                    }
                    else
                    {
                        runtime.NextShotInBurstTimer = 0f;
                        runtime.ShotsFiredInCurrentBurst = 0;
                        runtime.BurstWindupDurationSeconds = 0f;
                        runtime.HasLockedAimDirection = 0;
                    }
                }

                mutableShooterRuntime[shooterIndex] = runtime;
            }

            shooterControlState.ValueRW = new EnemyShooterControlState
            {
                MovementLocked = movementLocked ? (byte)1 : (byte)0,
                AimDirection = hasResolvedAimDirection ? resolvedAimDirection : float3.zero,
                HasAimDirection = hasResolvedAimDirection ? (byte)1 : (byte)0
            };
        }
    }
    #endregion

    #region Helpers
    private static void SynchronizeShooterRuntime(DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime, int count)
    {
        shooterRuntime.Clear();

        for (int index = 0; index < count; index++)
        {
            shooterRuntime.Add(new EnemyShooterRuntimeElement
            {
                NextBurstTimer = 0f,
                NextShotInBurstTimer = 0f,
                RemainingBurstShots = 0,
                ShotsFiredInCurrentBurst = 0,
                BurstWindupDurationSeconds = 0f,
                IsPlayerInRange = 0,
                LockedAimDirection = float3.zero,
                HasLockedAimDirection = 0
            });
        }
    }

    private static bool IsInRange(float playerDistance, in EnemyShooterConfigElement shooterConfig)
    {
        if (shooterConfig.UseMinimumRange != 0 && playerDistance < math.max(0f, shooterConfig.MinimumRange))
            return false;

        if (shooterConfig.UseMaximumRange != 0 && playerDistance > math.max(0f, shooterConfig.MaximumRange))
            return false;

        return true;
    }

    private static float3 ResolveAimDirection(in EnemyShooterConfigElement shooterConfig,
                                              in EnemyShooterRuntimeElement runtime,
                                              float3 toPlayer,
                                              quaternion enemyRotation)
    {
        float3 fallbackDirection = ResolveForward(enemyRotation);

        switch (shooterConfig.AimPolicy)
        {
            case EnemyShooterAimPolicy.LockOnFireStart:
                if (runtime.HasLockedAimDirection != 0)
                    return math.normalizesafe(runtime.LockedAimDirection, fallbackDirection);

                return math.normalizesafe(toPlayer, fallbackDirection);

            default:
                return math.normalizesafe(toPlayer, fallbackDirection);
        }
    }

    private static float3 ResolveForward(quaternion rotation)
    {
        float3 forward = math.forward(rotation);
        forward.y = 0f;
        return math.normalizesafe(forward, ForwardAxis);
    }

    /// <summary>
    /// Captures the best current aim direction used to orient the shooter visuals before projectile spawn.
    /// </summary>
    /// <param name="candidateDirection">Current shooter aim direction candidate.</param>
    /// <param name="movementPolicy">Movement policy associated with the current shooter module.</param>
    /// <param name="resolvedAimDirection">Best resolved aim direction retained across modules.</param>
    /// <param name="hasResolvedAimDirection">Whether a valid aim direction has already been captured.</param>
    /// <param name="aimPriority">Priority of the currently captured aim direction.</param>
    /// <returns>None.<returns>
    private static void TryCaptureAimDirection(float3 candidateDirection,
                                               EnemyShooterMovementPolicy movementPolicy,
                                               ref float3 resolvedAimDirection,
                                               ref bool hasResolvedAimDirection,
                                               ref int aimPriority)
    {
        if (math.lengthsq(candidateDirection) <= DirectionEpsilon)
            return;

        int candidatePriority = movementPolicy == EnemyShooterMovementPolicy.StopWhileAiming ? 1 : 0;

        if (hasResolvedAimDirection && candidatePriority < aimPriority)
            return;

        resolvedAimDirection = math.normalizesafe(candidateDirection, ForwardAxis);
        hasResolvedAimDirection = true;
        aimPriority = candidatePriority;
    }

    private static void EnqueueShotRequests(DynamicBuffer<ShootRequest> shootRequests,
                                            float3 shooterPosition,
                                            float3 baseDirection,
                                            in EnemyShooterConfigElement shooterConfig)
    {
        int projectilesPerShot = math.max(1, shooterConfig.ProjectilesPerShot);

        if (projectilesPerShot <= 1)
        {
            AddShootRequest(shootRequests,
                            shooterPosition,
                            baseDirection,
                            in shooterConfig);
            return;
        }

        float spread = math.max(0f, shooterConfig.SpreadAngleDegrees);
        float step = projectilesPerShot > 1 ? spread / (projectilesPerShot - 1) : 0f;
        float startOffset = -spread * 0.5f;

        for (int projectileIndex = 0; projectileIndex < projectilesPerShot; projectileIndex++)
        {
            float angleOffset = startOffset + step * projectileIndex;
            float3 direction = RotateDirectionByDegrees(baseDirection, angleOffset);
            AddShootRequest(shootRequests,
                            shooterPosition,
                            direction,
                            in shooterConfig);
        }
    }

    private static float3 RotateDirectionByDegrees(float3 direction, float angleDegrees)
    {
        float radians = math.radians(angleDegrees);
        float sine = math.sin(radians);
        float cosine = math.cos(radians);
        float3 normalizedDirection = math.normalizesafe(direction, ForwardAxis);
        float x = normalizedDirection.x * cosine - normalizedDirection.z * sine;
        float z = normalizedDirection.x * sine + normalizedDirection.z * cosine;
        return math.normalizesafe(new float3(x, 0f, z), ForwardAxis);
    }

    private static void AddShootRequest(DynamicBuffer<ShootRequest> shootRequests,
                                        float3 shooterPosition,
                                        float3 direction,
                                        in EnemyShooterConfigElement shooterConfig)
    {
        float3 spawnOffset = math.normalizesafe(direction, ForwardAxis) * 0.35f;

        shootRequests.Add(new ShootRequest
        {
            Position = shooterPosition + spawnOffset,
            Direction = math.normalizesafe(direction, ForwardAxis),
            Speed = math.max(0f, shooterConfig.ProjectileSpeed),
            ExplosionRadius = math.max(0f, shooterConfig.ProjectileExplosionRadius),
            Range = math.max(0f, shooterConfig.ProjectileRange),
            Lifetime = math.max(0f, shooterConfig.ProjectileLifetime),
            Damage = math.max(0f, shooterConfig.ProjectileDamage),
            ProjectileScaleMultiplier = math.max(0.01f, shooterConfig.ProjectileScaleMultiplier),
            PenetrationMode = shooterConfig.PenetrationMode,
            MaxPenetrations = math.max(0, shooterConfig.MaxPenetrations),
            InheritPlayerSpeed = shooterConfig.InheritShooterSpeed,
            IsSplitChild = 0,
            ElementalPayloadOverride = shooterConfig.HasElementalPayload != 0
                ? ProjectileElementalPayloadUtility.BuildSingle(in shooterConfig.ElementalEffect,
                                                                math.max(0f, shooterConfig.ElementalStacksPerHit))
                : default
        });
    }
    #endregion

    #endregion
}
