using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Simulates custom movement modules compiled from EnemyAdvancedPatternPreset.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySteeringSystem))]
[UpdateBefore(typeof(EnemyProjectileHitSystem))]
public partial struct EnemyPatternMovementSystem : ISystem
{
    #region Constants
    private static readonly float3 UpAxis = new float3(0f, 1f, 0f);
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float DirectionEpsilon = 1e-6f;
    private const float RotationSpeedEpsilon = 1e-4f;
    private const float ClearanceEpsilon = 1e-4f;
    private const float BasicClearanceBlendScale = 0.12f;
    private const float DvdClearanceBlend = 0.75f;
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
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<EnemyPatternConfig>();
        state.RequireForUpdate<EnemyPatternRuntimeState>();
        state.RequireForUpdate<EnemyCustomPatternMovementTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        Entity playerEntity = Entity.Null;
        float3 playerPosition = float3.zero;

        // Find player
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

        if (entityManager.Exists(playerEntity) == false)
            return;

        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;

        if (deltaTime <= 0f)
            return;

        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        bool wallsEnabled = wallsLayerMask != 0;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        ComponentLookup<EnemyShooterControlState> shooterControlLookup = SystemAPI.GetComponentLookup<EnemyShooterControlState>(true);
        ComponentLookup<EnemyElementalRuntimeState> elementalRuntimeLookup = SystemAPI.GetComponentLookup<EnemyElementalRuntimeState>(true);
        NativeList<Entity> occupancyEntities = new NativeList<Entity>(Allocator.Temp);
        NativeList<float3> occupancyPositions = new NativeList<float3>(Allocator.Temp);
        NativeList<float3> occupancyVelocities = new NativeList<float3>(Allocator.Temp);
        NativeList<float> occupancyRadii = new NativeList<float>(Allocator.Temp);
        NativeList<int> occupancyPriorityTiers = new NativeList<int>(Allocator.Temp);
        float occupancyMaxRadius = 0.05f;

        // Build spatial hash map of nearby enemies for efficient avoidance queries.
        // Only active non-despawning enemies
        // are considered as occupancy since they are the only ones relevant for movement.
        foreach ((RefRO<EnemyData> occupancyEnemyData,
                  RefRO<EnemyRuntimeState> occupancyEnemyRuntimeState,
                  RefRO<LocalTransform> occupancyEnemyTransform,
                  Entity occupancyEnemyEntity)
                 in SystemAPI.Query<RefRO<EnemyData>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest>()
                             .WithEntityAccess())
        {
            occupancyEntities.Add(occupancyEnemyEntity);
            occupancyPositions.Add(occupancyEnemyTransform.ValueRO.Position);

            float3 planarVelocity = occupancyEnemyRuntimeState.ValueRO.Velocity;
            planarVelocity.y = 0f;
            occupancyVelocities.Add(planarVelocity);

            float occupancyRadius = math.max(0.05f, occupancyEnemyData.ValueRO.BodyRadius);
            occupancyRadii.Add(occupancyRadius);
            occupancyPriorityTiers.Add(math.clamp(occupancyEnemyData.ValueRO.PriorityTier, -128, 128));

            if (occupancyRadius > occupancyMaxRadius)
                occupancyMaxRadius = occupancyRadius;
        }

        int occupancyCount = occupancyEntities.Length;
        float occupancyCellSize = math.max(0.25f, occupancyMaxRadius + 0.35f);
        float occupancyInverseCellSize = 1f / occupancyCellSize;
        NativeParallelMultiHashMap<int, int> occupancyCellMap = new NativeParallelMultiHashMap<int, int>(math.max(1, occupancyCount * 2), Allocator.Temp);

        // Populate spatial hash map with nearby enemy positions.
        for (int occupancyIndex = 0; occupancyIndex < occupancyCount; occupancyIndex++)
        {
            float3 occupancyPosition = occupancyPositions[occupancyIndex];
            int cellX = (int)math.floor(occupancyPosition.x * occupancyInverseCellSize);
            int cellY = (int)math.floor(occupancyPosition.z * occupancyInverseCellSize);
            occupancyCellMap.Add(EnemyPatternWandererUtility.EncodeCell(cellX, cellY), occupancyIndex);
        }

        EnemyPatternWandererUtility.OccupancyContext occupancyContext = new EnemyPatternWandererUtility.OccupancyContext(occupancyEntities.AsArray(),
                                                                                                                          occupancyPositions.AsArray(),
                                                                                                                          occupancyVelocities.AsArray(),
                                                                                                                          occupancyRadii.AsArray(),
                                                                                                                          occupancyPriorityTiers.AsArray(),
                                                                                                                          occupancyCellMap,
                                                                                                                          occupancyInverseCellSize,
                                                                                                                          occupancyMaxRadius);

        foreach ((RefRO<EnemyData> enemyData,
                  RefRO<EnemyPatternConfig> patternConfig,
                  RefRW<EnemyPatternRuntimeState> patternRuntimeState,
                  RefRW<EnemyRuntimeState> enemyRuntimeState,
                  RefRW<LocalTransform> enemyTransform,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyData>,
                                    RefRO<EnemyPatternConfig>,
                                    RefRW<EnemyPatternRuntimeState>,
                                    RefRW<EnemyRuntimeState>,
                                    RefRW<LocalTransform>>()
                             .WithAll<EnemyActive, EnemyCustomPatternMovementTag>()
                             .WithNone<EnemyDespawnRequest>()
                             .WithEntityAccess())
        {
            EnemyData currentEnemyData = enemyData.ValueRO;
            EnemyPatternConfig currentPatternConfig = patternConfig.ValueRO;
            EnemyPatternRuntimeState currentPatternRuntimeState = patternRuntimeState.ValueRO;
            EnemyRuntimeState currentEnemyRuntimeState = enemyRuntimeState.ValueRO;
            LocalTransform currentEnemyTransform = enemyTransform.ValueRO;

            float elementalSlowPercent = 0f;

            if (elementalRuntimeLookup.HasComponent(enemyEntity))
                elementalSlowPercent = math.clamp(elementalRuntimeLookup[enemyEntity].SlowPercent, 0f, 100f);

            float slowMultiplier = math.saturate(1f - elementalSlowPercent * 0.01f);
            float moveSpeed = math.max(0f, currentEnemyData.MoveSpeed) * slowMultiplier;
            float maxSpeed = math.max(0f, currentEnemyData.MaxSpeed) * slowMultiplier;
            float acceleration = math.max(0f, currentEnemyData.Acceleration);
            bool movementLocked = shooterControlLookup.HasComponent(enemyEntity) && shooterControlLookup[enemyEntity].MovementLocked != 0;
            bool ignoreSteeringAndPriority = ShouldIgnoreSteeringAndPriority(in currentPatternConfig);
            float3 desiredVelocity = float3.zero;

            // Determine desired velocity based on movement pattern kind and configuration.
            switch (currentPatternConfig.MovementKind)
            {
                case EnemyCompiledMovementPatternKind.Stationary:
                    desiredVelocity = float3.zero;
                    break;

                case EnemyCompiledMovementPatternKind.WandererBasic:
                    desiredVelocity = EnemyPatternWandererUtility.ResolveWandererBasicVelocity(enemyEntity,
                                                                                               in currentEnemyData,
                                                                                               in currentPatternConfig,
                                                                                               ref currentPatternRuntimeState,
                                                                                               currentEnemyTransform.Position,
                                                                                               playerPosition,
                                                                                               moveSpeed,
                                                                                               maxSpeed,
                                                                                               elapsedTime,
                                                                                               deltaTime,
                                                                                               in physicsWorldSingleton,
                                                                                               wallsLayerMask,
                                                                                               wallsEnabled,
                                                                                               in occupancyContext);
                    break;

                case EnemyCompiledMovementPatternKind.WandererDvd:
                    desiredVelocity = ResolveWandererDvdVelocity(enemyEntity,
                                                                in currentPatternConfig,
                                                                ref currentPatternRuntimeState,
                                                                moveSpeed,
                                                                maxSpeed,
                                                                elapsedTime);
                    break;

                default:
                    desiredVelocity = float3.zero;
                    break;
            }

            if (currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd)
            {
                if (ignoreSteeringAndPriority == false)
                {
                    float clearanceSpeedCap = maxSpeed > 0f ? maxSpeed : moveSpeed;
                    float minimumEnemyClearance = math.max(0f, currentPatternConfig.BasicMinimumEnemyClearance);
                    float3 clearanceVelocity = EnemyPatternWandererUtility.ResolveLocalClearanceVelocity(enemyEntity,
                                                                                                         currentEnemyData.PriorityTier,
                                                                                                         currentEnemyTransform.Position,
                                                                                                         currentEnemyData.BodyRadius,
                                                                                                         minimumEnemyClearance,
                                                                                                         clearanceSpeedCap,
                                                                                                         in occupancyContext);

                    if (currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic)
                    {
                        float clearanceBlend = math.saturate(currentPatternConfig.BasicFreeTrajectoryPreference * BasicClearanceBlendScale);
                        desiredVelocity += clearanceVelocity * clearanceBlend;
                    }
                    else
                    {
                        desiredVelocity += clearanceVelocity * DvdClearanceBlend;
                    }
                }
            }

            if (movementLocked)
                desiredVelocity = float3.zero;

            float desiredSpeed = math.length(desiredVelocity);

            if (maxSpeed > 0f && desiredSpeed > maxSpeed && desiredSpeed > DirectionEpsilon)
                desiredVelocity *= maxSpeed / desiredSpeed;

            float3 velocityDelta = desiredVelocity - currentEnemyRuntimeState.Velocity;
            float maxVelocityDelta = acceleration * deltaTime;
            float velocityDeltaMagnitude = math.length(velocityDelta);

            if (velocityDeltaMagnitude > maxVelocityDelta && velocityDeltaMagnitude > DirectionEpsilon)
                currentEnemyRuntimeState.Velocity += velocityDelta * (maxVelocityDelta / velocityDeltaMagnitude);
            else
                currentEnemyRuntimeState.Velocity = desiredVelocity;

            if (maxSpeed > 0f)
            {
                float velocityMagnitude = math.length(currentEnemyRuntimeState.Velocity);

                if (velocityMagnitude > maxSpeed && velocityMagnitude > DirectionEpsilon)
                    currentEnemyRuntimeState.Velocity *= maxSpeed / velocityMagnitude;
            }

            float3 desiredDisplacement = currentEnemyRuntimeState.Velocity * deltaTime;
            float3 nextPosition = currentEnemyTransform.Position;
            float baseCollisionRadius = math.max(0.01f, currentEnemyData.BodyRadius);
            float additionalWallClearance = ResolveWallClearanceForMovement(in currentPatternConfig);
            float wallCollisionRadius = math.max(0.01f, baseCollisionRadius + additionalWallClearance);
            bool expandedWallClearance = wallCollisionRadius > baseCollisionRadius + ClearanceEpsilon;

            // Handle wall collisions and clearance if enabled, otherwise apply desired displacement directly.
            if (wallsEnabled)
            {
                bool clearanceReachedTarget = false;
                bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                       currentEnemyTransform.Position,
                                                                                       desiredDisplacement,
                                                                                       wallCollisionRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 allowedDisplacement,
                                                                                       out float3 hitNormal);

                nextPosition += allowedDisplacement;

                // Handle wall collisions based on movement kind and pattern configuration.
                if (hitWall)
                {
                    if (currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd)
                    {
                        float3 bouncedVelocity = WorldWallCollisionUtility.ComputeBounceVelocity(currentEnemyRuntimeState.Velocity,
                                                                                                 hitNormal,
                                                                                                 currentPatternConfig.DvdBounceDamping);

                        if (math.lengthsq(bouncedVelocity) <= DirectionEpsilon)
                            bouncedVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(currentEnemyRuntimeState.Velocity, hitNormal);

                        currentEnemyRuntimeState.Velocity = bouncedVelocity;
                        currentPatternRuntimeState.DvdDirection = math.normalizesafe(new float3(bouncedVelocity.x, 0f, bouncedVelocity.z), float3.zero);

                        if (currentPatternConfig.DvdCornerNudgeDistance > 0f)
                            nextPosition += math.normalizesafe(hitNormal, float3.zero) * currentPatternConfig.DvdCornerNudgeDistance;
                    }
                    else
                    {
                        currentEnemyRuntimeState.Velocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(currentEnemyRuntimeState.Velocity, hitNormal);
                        currentPatternRuntimeState.WanderRetryTimer = math.max(currentPatternRuntimeState.WanderRetryTimer,
                                                                              currentPatternConfig.BasicBlockedPathRetryDelay);

                        if (currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic && expandedWallClearance)
                            clearanceReachedTarget = true;
                    }
                }

                if (expandedWallClearance)
                {
                    bool requiresClearanceCorrection = WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                                                             nextPosition,
                                                                                                             wallCollisionRadius,
                                                                                                             wallsLayerMask,
                                                                                                             out float3 clearanceCorrectionDisplacement,
                                                                                                             out float3 clearanceNormal);

                    if (requiresClearanceCorrection)
                    {
                        nextPosition += clearanceCorrectionDisplacement;
                        currentEnemyRuntimeState.Velocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(currentEnemyRuntimeState.Velocity, clearanceNormal);

                        if (currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic)
                            clearanceReachedTarget = true;
                    }
                }

                if (clearanceReachedTarget)
                    ConsumeWanderTargetOnClearance(ref currentPatternRuntimeState, in currentPatternConfig);
            }
            else
            {
                nextPosition += desiredDisplacement;
            }

            nextPosition.y = currentEnemyTransform.Position.y;
            currentEnemyTransform.Position = nextPosition;

            float planarVelocitySquared = currentEnemyRuntimeState.Velocity.x * currentEnemyRuntimeState.Velocity.x +
                                          currentEnemyRuntimeState.Velocity.z * currentEnemyRuntimeState.Velocity.z;
            bool freezeRotation = currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Stationary &&
                                  currentPatternConfig.StationaryFreezeRotation != 0;

            if (freezeRotation == false)
            {
                float rotationSpeedDegreesPerSecond = currentEnemyData.RotationSpeedDegreesPerSecond;
                bool hasSelfRotation = math.abs(rotationSpeedDegreesPerSecond) > RotationSpeedEpsilon;

                if (hasSelfRotation)
                {
                    float deltaYawRadians = math.radians(rotationSpeedDegreesPerSecond) * deltaTime;
                    quaternion deltaRotation = quaternion.RotateY(deltaYawRadians);
                    currentEnemyTransform.Rotation = math.normalize(math.mul(currentEnemyTransform.Rotation, deltaRotation));
                }
                else if (planarVelocitySquared > DirectionEpsilon)
                {
                    float3 forward = math.normalizesafe(new float3(currentEnemyRuntimeState.Velocity.x, 0f, currentEnemyRuntimeState.Velocity.z), ForwardAxis);
                    currentEnemyTransform.Rotation = quaternion.LookRotationSafe(forward, UpAxis);
                }
            }

            enemyRuntimeState.ValueRW = currentEnemyRuntimeState;
            patternRuntimeState.ValueRW = currentPatternRuntimeState;
            enemyTransform.ValueRW = currentEnemyTransform;
        }

        occupancyCellMap.Dispose();
        occupancyRadii.Dispose();
        occupancyPriorityTiers.Dispose();
        occupancyVelocities.Dispose();
        occupancyPositions.Dispose();
        occupancyEntities.Dispose();
    }
    #endregion

    #region Movement Helpers
    /// <summary>
    /// Resolves whether current movement pattern ignores steering and priority interactions.
    /// </summary>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <returns>True when DVD movement requests steering and priority bypass.</returns>
    private static bool ShouldIgnoreSteeringAndPriority(in EnemyPatternConfig patternConfig)
    {
        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererDvd)
            return false;

        return patternConfig.DvdIgnoreSteeringAndPriority != 0;
    }

    /// <summary>
    /// Resolves additional wall-clearance radius to enforce during runtime displacement.
    /// </summary>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <returns>Additional wall-clearance radius in meters.</returns>
    private static float ResolveWallClearanceForMovement(in EnemyPatternConfig patternConfig)
    {
        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererBasic)
            return 0f;

        return math.max(0f, patternConfig.BasicMinimumWallDistance);
    }

    /// <summary>
    /// Consumes the active Wanderer target when wall-clearance blocks remaining progress.
    /// </summary>
    /// <param name="patternRuntimeState">Mutable Wanderer runtime state.</param>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    private static void ConsumeWanderTargetOnClearance(ref EnemyPatternRuntimeState patternRuntimeState,
                                                       in EnemyPatternConfig patternConfig)
    {
        if (patternRuntimeState.WanderHasTarget == 0)
            return;

        patternRuntimeState.WanderHasTarget = 0;
        patternRuntimeState.WanderWaitTimer = math.max(0f, patternConfig.BasicWaitCooldownSeconds);
        patternRuntimeState.WanderRetryTimer = 0f;
    }

    /// <summary>
    /// Resolves current Wanderer DVD desired velocity and initializes first direction.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="patternConfig">Current compiled pattern configuration.</param>
    /// <param name="patternRuntimeState">Mutable Wanderer runtime state.</param>
    /// <param name="moveSpeed">Resolved movement speed.</param>
    /// <param name="maxSpeed">Resolved max speed.</param>
    /// <param name="elapsedTime">Elapsed world time in seconds.</param>
    /// <returns>Desired planar velocity for this frame.</returns>
    private static float3 ResolveWandererDvdVelocity(Entity enemyEntity,
                                                     in EnemyPatternConfig patternConfig,
                                                     ref EnemyPatternRuntimeState patternRuntimeState,
                                                     float moveSpeed,
                                                     float maxSpeed,
                                                     float elapsedTime)
    {
        if (patternRuntimeState.DvdInitialized == 0)
        {
            float angleDegrees = patternConfig.DvdFixedInitialDirectionDegrees;

            if (patternConfig.DvdRandomizeInitialDirection != 0)
            {
                uint seed = math.hash(new int3(enemyEntity.Index, enemyEntity.Version, (int)(elapsedTime * 13f)));
                int diagonalIndex = (int)(seed % 4u);

                switch (diagonalIndex)
                {
                    case 0:
                        angleDegrees = 45f;
                        break;

                    case 1:
                        angleDegrees = 135f;
                        break;

                    case 2:
                        angleDegrees = 225f;
                        break;

                    default:
                        angleDegrees = 315f;
                        break;
                }
            }

            float radians = math.radians(angleDegrees);
            patternRuntimeState.DvdDirection = math.normalizesafe(new float3(math.sin(radians), 0f, math.cos(radians)), ForwardAxis);
            patternRuntimeState.DvdInitialized = 1;
        }

        float movementSpeed = maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed;
        movementSpeed *= math.max(0f, patternConfig.DvdSpeedMultiplier);
        return patternRuntimeState.DvdDirection * movementSpeed;
    }

    #endregion

    #endregion
}
