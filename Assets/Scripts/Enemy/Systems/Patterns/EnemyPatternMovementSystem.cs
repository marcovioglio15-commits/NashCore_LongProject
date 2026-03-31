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
    private const float DirectionEpsilon = 1e-6f;
    private const float RotationSpeedEpsilon = 1e-4f;
    private const float ClearanceEpsilon = 1e-4f;
    private const float BasicClearanceBlendScale = 0.12f;
    private const float DvdClearanceBlend = 0.75f;
    private const float BasicMinimumForwardSpeedRatio = 0.62f;
    private const float DvdMinimumForwardSpeedRatio = 0.8f;
    private const float WallBlockedRepathThreshold = 0.72f;
    private const float WallClearanceCorrectionRepathThreshold = 0.3f;
    private const float WallComfortRepathThreshold = 0.58f;
    private const float PriorityYieldMaxSpeedBoost = 0.65f;
    private const float PriorityYieldMaxAccelerationBoost = 1.9f;
    private const float PriorityYieldGapSpeedScaleMin = 0.62f;
    private const float PriorityYieldGapSpeedScaleMax = 1.4f;
    private const float PriorityYieldGapAccelerationScaleMin = 0.72f;
    private const float PriorityYieldGapAccelerationScaleMax = 1.58f;
    private const float MinimumSteeringAggressiveness = 0f;
    private const float MaximumSteeringAggressiveness = 2.5f;
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

        if (!entityManager.Exists(playerEntity))
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
        EnemyNavigationGridState navigationGridState = default;
        DynamicBuffer<EnemyNavigationCellElement> navigationCells = default;
        bool navigationFlowReady = false;

        if (SystemAPI.TryGetSingleton<EnemyNavigationGridState>(out navigationGridState) &&
            SystemAPI.TryGetSingletonBuffer<EnemyNavigationCellElement>(out navigationCells))
        {
            navigationFlowReady = navigationGridState.FlowReady != 0 && navigationCells.Length > 0;
        }

        NativeList<Entity> occupancyEntities = new NativeList<Entity>(Allocator.Temp);
        NativeList<float3> occupancyPositions = new NativeList<float3>(Allocator.Temp);
        NativeList<float3> occupancyVelocities = new NativeList<float3>(Allocator.Temp);
        NativeList<float> occupancyRadii = new NativeList<float>(Allocator.Temp);
        NativeList<byte> occupancyDvdFlags = new NativeList<byte>(Allocator.Temp);
        NativeList<float> occupancyDvdBounceDamping = new NativeList<float>(Allocator.Temp);
        NativeList<int> occupancyPriorityTiers = new NativeList<int>(Allocator.Temp);
        float occupancyMaxRadius = 0.05f;
        float occupancyMaxPlanarSpeed = 0f;

        // Build spatial hash map of nearby enemies for efficient avoidance queries.
        // Only active non-despawning enemies
        // are considered as occupancy since they are the only ones relevant for movement.
        foreach ((RefRO<EnemyData> occupancyEnemyData,
                  RefRO<EnemyPatternConfig> occupancyPatternConfig,
                  RefRO<EnemyRuntimeState> occupancyEnemyRuntimeState,
                  RefRO<EnemyKnockbackState> occupancyEnemyKnockbackState,
                  RefRO<LocalTransform> occupancyEnemyTransform,
                  Entity occupancyEnemyEntity)
                 in SystemAPI.Query<RefRO<EnemyData>,
                                    RefRO<EnemyPatternConfig>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<EnemyKnockbackState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithNone<EnemyDespawnRequest>()
                             .WithEntityAccess())
        {
            occupancyEntities.Add(occupancyEnemyEntity);
            occupancyPositions.Add(occupancyEnemyTransform.ValueRO.Position);

            float3 planarVelocity = EnemyKnockbackRuntimeUtility.ResolveCombinedVelocity(occupancyEnemyRuntimeState.ValueRO.Velocity,
                                                                                        in occupancyEnemyKnockbackState.ValueRO);
            planarVelocity.y = 0f;
            occupancyVelocities.Add(planarVelocity);
            occupancyDvdFlags.Add(occupancyPatternConfig.ValueRO.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd ? (byte)1 : (byte)0);
            occupancyDvdBounceDamping.Add(math.clamp(occupancyPatternConfig.ValueRO.DvdBounceDamping, 0f, 1f));

            float occupancyRadius = math.max(0.05f, occupancyEnemyData.ValueRO.BodyRadius);
            occupancyRadii.Add(occupancyRadius);
            occupancyPriorityTiers.Add(math.clamp(occupancyEnemyData.ValueRO.PriorityTier, -128, 128));
            float occupancySpeed = math.length(planarVelocity);

            if (occupancyRadius > occupancyMaxRadius)
                occupancyMaxRadius = occupancyRadius;

            if (occupancySpeed > occupancyMaxPlanarSpeed)
                occupancyMaxPlanarSpeed = occupancySpeed;
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
        EnemyPatternDvdCollisionUtility.OccupancyContext dvdCollisionContext = new EnemyPatternDvdCollisionUtility.OccupancyContext(occupancyEntities.AsArray(),
                                                                                                                                    occupancyPositions.AsArray(),
                                                                                                                                    occupancyVelocities.AsArray(),
                                                                                                                                    occupancyRadii.AsArray(),
                                                                                                                                    occupancyDvdFlags.AsArray(),
                                                                                                                                    occupancyDvdBounceDamping.AsArray(),
                                                                                                                                    occupancyCellMap,
                                                                                                                                    occupancyInverseCellSize,
                                                                                                                                    occupancyMaxRadius,
                                                                                                                                    occupancyMaxPlanarSpeed);

        foreach ((RefRO<EnemyData> enemyData,
                  RefRO<EnemyPatternConfig> patternConfig,
                  RefRW<EnemyPatternRuntimeState> patternRuntimeState,
                  RefRW<EnemyRuntimeState> enemyRuntimeState,
                  RefRW<EnemyKnockbackState> enemyKnockbackState,
                  RefRW<LocalTransform> enemyTransform,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyData>,
                                    RefRO<EnemyPatternConfig>,
                                    RefRW<EnemyPatternRuntimeState>,
                                    RefRW<EnemyRuntimeState>,
                                    RefRW<EnemyKnockbackState>,
                                    RefRW<LocalTransform>>()
                             .WithAll<EnemyActive, EnemyCustomPatternMovementTag>()
                             .WithNone<EnemyDespawnRequest, EnemySpawnInactivityLock>()
                             .WithEntityAccess())
        {
            EnemyData currentEnemyData = enemyData.ValueRO;
            EnemyPatternConfig currentPatternConfig = patternConfig.ValueRO;
            EnemyPatternRuntimeState currentPatternRuntimeState = patternRuntimeState.ValueRO;
            EnemyRuntimeState currentEnemyRuntimeState = enemyRuntimeState.ValueRO;
            EnemyKnockbackState currentEnemyKnockbackState = enemyKnockbackState.ValueRO;
            LocalTransform currentEnemyTransform = enemyTransform.ValueRO;

            float elementalSlowPercent = 0f;

            if (elementalRuntimeLookup.HasComponent(enemyEntity))
                elementalSlowPercent = math.clamp(elementalRuntimeLookup[enemyEntity].SlowPercent, 0f, 100f);

            float slowMultiplier = math.saturate(1f - elementalSlowPercent * 0.01f);
            float moveSpeed = math.max(0f, currentEnemyData.MoveSpeed) * slowMultiplier;
            float maxSpeed = math.max(0f, currentEnemyData.MaxSpeed) * slowMultiplier;
            float acceleration = math.max(0f, currentEnemyData.Acceleration);
            float deceleration = math.max(0f, currentEnemyData.Deceleration);
            float steeringAggressiveness = ResolveSteeringAggressiveness(currentEnemyData.SteeringAggressiveness);
            float minimumWallDistance = math.max(0f, currentEnemyData.MinimumWallDistance);
            EnemyShooterControlState shooterControlState = default;

            if (shooterControlLookup.HasComponent(enemyEntity))
                shooterControlState = shooterControlLookup[enemyEntity];

            bool movementLocked = shooterControlState.MovementLocked != 0;
            bool ignoreSteeringAndPriority = ShouldIgnoreSteeringAndPriority(in currentPatternConfig);
            float3 desiredVelocity = float3.zero;
            float priorityYieldUrgency = 0f;
            float priorityYieldGapNormalized = 0f;

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
                                                                                               minimumWallDistance,
                                                                                               moveSpeed,
                                                                                               maxSpeed,
                                                                                               steeringAggressiveness,
                                                                                               elapsedTime,
                                                                                               deltaTime,
                                                                                               in physicsWorldSingleton,
                                                                                               wallsLayerMask,
                                                                                               wallsEnabled,
                                                                                               in occupancyContext);
                    break;

                case EnemyCompiledMovementPatternKind.Coward:
                    desiredVelocity = EnemyPatternCowardUtility.ResolveCowardVelocity(enemyEntity,
                                                                                      in currentEnemyData,
                                                                                      in currentPatternConfig,
                                                                                      ref currentPatternRuntimeState,
                                                                                      currentEnemyTransform.Position,
                                                                                      playerPosition,
                                                                                      minimumWallDistance,
                                                                                      moveSpeed,
                                                                                      maxSpeed,
                                                                                      steeringAggressiveness,
                                                                                      elapsedTime,
                                                                                      deltaTime,
                                                                                      in physicsWorldSingleton,
                                                                                      wallsLayerMask,
                                                                                      wallsEnabled,
                                                                                      navigationFlowReady,
                                                                                      in navigationGridState,
                                                                                      navigationCells,
                                                                                      in occupancyContext);
                    break;

                case EnemyCompiledMovementPatternKind.WandererDvd:
                    desiredVelocity = EnemyPatternMovementRuntimeUtility.ResolveWandererDvdVelocity(enemyEntity,
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
                currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd ||
                currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward)
            {
                if (!ignoreSteeringAndPriority)
                {
                    float clearanceSpeedCap = maxSpeed > 0f ? maxSpeed : moveSpeed;
                    float minimumEnemyClearance = math.max(0f, currentPatternConfig.BasicMinimumEnemyClearance);
                    float3 clearanceVelocity = EnemyPatternWandererUtility.ResolveLocalClearanceVelocity(enemyEntity,
                                                                                                         currentEnemyData.PriorityTier,
                                                                                                         currentEnemyTransform.Position,
                                                                                                         currentEnemyData.BodyRadius,
                                                                                                         minimumEnemyClearance,
                                                                                                         clearanceSpeedCap,
                                                                                                         steeringAggressiveness,
                                                                                                         out float clearanceYieldUrgency,
                                                                                                         out float clearanceYieldGapNormalized,
                                                                                                         in occupancyContext);

                    if (clearanceYieldUrgency > priorityYieldUrgency)
                        priorityYieldUrgency = clearanceYieldUrgency;

                    if (clearanceYieldGapNormalized > priorityYieldGapNormalized)
                        priorityYieldGapNormalized = clearanceYieldGapNormalized;

                    if (currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                        currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward)
                    {
                        float clearanceBlendScale = ResolveAggressivenessScale(steeringAggressiveness, 0.78f, 1.25f);
                        float clearanceBlend = math.saturate(currentPatternConfig.BasicFreeTrajectoryPreference * BasicClearanceBlendScale * clearanceBlendScale);
                        desiredVelocity = ComposeDesiredVelocityWithClearance(desiredVelocity,
                                                                              clearanceVelocity,
                                                                              clearanceBlend,
                                                                              BasicMinimumForwardSpeedRatio);
                    }
                    else
                    {
                        float dvdClearanceBlend = math.saturate(DvdClearanceBlend * ResolveAggressivenessScale(steeringAggressiveness, 0.85f, 1.35f));
                        desiredVelocity = ComposeDesiredVelocityWithClearance(desiredVelocity,
                                                                              clearanceVelocity,
                                                                              dvdClearanceBlend,
                                                                              DvdMinimumForwardSpeedRatio);
                    }
                }
            }

            if (movementLocked)
                desiredVelocity = float3.zero;
            else if (wallsEnabled &&
                     (currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                      currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward))
            {
                float patternDesiredSpeed = math.length(desiredVelocity);
                float wallComfortDesiredSpeed = math.max(patternDesiredSpeed, maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed);
                desiredVelocity = EnemyPatternMovementRuntimeUtility.ResolveWallComfortVelocity(desiredVelocity,
                                                                                                currentEnemyTransform.Position,
                                                                                                currentEnemyData.BodyRadius,
                                                                                                minimumWallDistance,
                                                                                                wallComfortDesiredSpeed,
                                                                                                in physicsWorldSingleton,
                                                                                                wallsLayerMask,
                                                                                                wallsEnabled,
                                                                                                out float wallComfortPressure);

                if (wallComfortPressure >= WallComfortRepathThreshold)
                    EnemyPatternMovementRuntimeUtility.ConsumeWanderTargetOnClearance(ref currentPatternRuntimeState, in currentPatternConfig);
            }

            float effectiveMaxSpeed = maxSpeed;

            if (!movementLocked && effectiveMaxSpeed > 0f && priorityYieldUrgency > 0f)
            {
                float speedBoost = ResolvePriorityYieldSpeedBoost(priorityYieldUrgency, priorityYieldGapNormalized, steeringAggressiveness);
                effectiveMaxSpeed *= 1f + speedBoost;
            }

            float desiredSpeed = math.length(desiredVelocity);

            if (effectiveMaxSpeed > 0f && desiredSpeed > effectiveMaxSpeed && desiredSpeed > DirectionEpsilon)
                desiredVelocity *= effectiveMaxSpeed / desiredSpeed;

            float3 velocityDelta = desiredVelocity - currentEnemyRuntimeState.Velocity;
            float accelerationRate = ResolveVelocityChangeRate(currentEnemyRuntimeState.Velocity,
                                                               desiredVelocity,
                                                               acceleration,
                                                               deceleration);

            if (!movementLocked && priorityYieldUrgency > 0f)
            {
                float accelerationBoost = ResolvePriorityYieldAccelerationBoost(priorityYieldUrgency, priorityYieldGapNormalized, steeringAggressiveness);
                accelerationRate *= 1f + accelerationBoost;
            }

            float maxVelocityDelta = accelerationRate * deltaTime;
            float velocityDeltaMagnitude = math.length(velocityDelta);

            if (velocityDeltaMagnitude > maxVelocityDelta && velocityDeltaMagnitude > DirectionEpsilon)
                currentEnemyRuntimeState.Velocity += velocityDelta * (maxVelocityDelta / velocityDeltaMagnitude);
            else
                currentEnemyRuntimeState.Velocity = desiredVelocity;

            if (effectiveMaxSpeed > 0f)
            {
                float velocityMagnitude = math.length(currentEnemyRuntimeState.Velocity);

                if (velocityMagnitude > effectiveMaxSpeed && velocityMagnitude > DirectionEpsilon)
                    currentEnemyRuntimeState.Velocity *= effectiveMaxSpeed / velocityMagnitude;
            }

            float3 desiredDisplacement = currentEnemyRuntimeState.Velocity * deltaTime;
            float3 nextPosition = currentEnemyTransform.Position;
            float baseCollisionRadius = math.max(0.01f, currentEnemyData.BodyRadius);
            float additionalWallClearance = EnemyPatternMovementRuntimeUtility.ResolveWallClearanceForMovement(in currentPatternConfig,
                                                                                                               in currentEnemyData);
            float wallCollisionRadius = math.max(0.01f, baseCollisionRadius + additionalWallClearance);
            bool expandedWallClearance = wallCollisionRadius > baseCollisionRadius + ClearanceEpsilon;

            if (currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd &&
                EnemyPatternDvdCollisionUtility.TryResolveBounceVelocity(enemyEntity,
                                                                        currentEnemyTransform.Position,
                                                                        currentEnemyRuntimeState.Velocity,
                                                                        baseCollisionRadius,
                                                                        currentPatternConfig.DvdBounceDamping,
                                                                        deltaTime,
                                                                        in dvdCollisionContext,
                                                                        out float3 bouncedVelocity,
                                                                        out float collisionTimeSeconds))
            {
                float preCollisionTime = math.clamp(collisionTimeSeconds, 0f, deltaTime);
                float postCollisionTime = math.max(0f, deltaTime - preCollisionTime);
                float3 preCollisionVelocity = currentEnemyRuntimeState.Velocity;
                currentEnemyRuntimeState.Velocity = bouncedVelocity;
                currentPatternRuntimeState.DvdDirection = math.normalizesafe(new float3(bouncedVelocity.x, 0f, bouncedVelocity.z), currentPatternRuntimeState.DvdDirection);
                desiredDisplacement = preCollisionVelocity * preCollisionTime + bouncedVelocity * postCollisionTime;
            }

            // Handle wall collisions and clearance if enabled, otherwise apply desired displacement directly.
            if (wallsEnabled)
            {
                bool clearanceReachedTarget = false;
                float requestedDisplacementDistance = math.length(desiredDisplacement);
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
                        float3 wallBouncedVelocity = WorldWallCollisionUtility.ComputeBounceVelocity(currentEnemyRuntimeState.Velocity,
                                                                                                     hitNormal,
                                                                                                     currentPatternConfig.DvdBounceDamping);

                        if (math.lengthsq(wallBouncedVelocity) <= DirectionEpsilon)
                            wallBouncedVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(currentEnemyRuntimeState.Velocity, hitNormal);

                        currentEnemyRuntimeState.Velocity = wallBouncedVelocity;
                        currentPatternRuntimeState.DvdDirection = math.normalizesafe(new float3(wallBouncedVelocity.x, 0f, wallBouncedVelocity.z), float3.zero);

                        if (currentPatternConfig.DvdCornerNudgeDistance > 0f)
                            nextPosition += math.normalizesafe(hitNormal, float3.zero) * currentPatternConfig.DvdCornerNudgeDistance;
                    }
                    else
                    {
                        currentEnemyRuntimeState.Velocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(currentEnemyRuntimeState.Velocity, hitNormal);
                        currentPatternRuntimeState.WanderRetryTimer = math.max(currentPatternRuntimeState.WanderRetryTimer,
                                                                              currentPatternConfig.BasicBlockedPathRetryDelay);

                        if ((currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                             currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward) &&
                            expandedWallClearance)
                        {
                            float allowedDisplacementDistance = math.length(allowedDisplacement);
                            float blockedDisplacementRatio = EnemyPatternMovementRuntimeUtility.ResolveBlockedDisplacementRatio(requestedDisplacementDistance, allowedDisplacementDistance);

                            if (blockedDisplacementRatio >= WallBlockedRepathThreshold)
                                clearanceReachedTarget = true;
                        }
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

                        if (currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                            currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Coward)
                        {
                            float clearanceCorrectionDistance = math.length(clearanceCorrectionDisplacement);
                            float clearanceCorrectionRatio = math.saturate(clearanceCorrectionDistance / math.max(0.01f, wallCollisionRadius));

                            if (clearanceCorrectionRatio >= WallClearanceCorrectionRepathThreshold)
                                clearanceReachedTarget = true;
                        }
                    }
                }

                if (clearanceReachedTarget)
                    EnemyPatternMovementRuntimeUtility.ConsumeWanderTargetOnClearance(ref currentPatternRuntimeState, in currentPatternConfig);
            }
            else
            {
                nextPosition += desiredDisplacement;
            }

            EnemyKnockbackRuntimeUtility.ApplyDisplacement(ref currentEnemyKnockbackState,
                                                           ref nextPosition,
                                                           wallCollisionRadius,
                                                           in physicsWorldSingleton,
                                                           wallsLayerMask,
                                                           wallsEnabled,
                                                           deltaTime);
            nextPosition.y = currentEnemyTransform.Position.y;
            currentEnemyTransform.Position = nextPosition;

            bool freezeRotation = currentPatternConfig.MovementKind == EnemyCompiledMovementPatternKind.Stationary &&
                                  currentPatternConfig.StationaryFreezeRotation != 0;

            if (!freezeRotation)
            {
                float rotationSpeedDegreesPerSecond = currentEnemyData.RotationSpeedDegreesPerSecond;
                bool hasSelfRotation = math.abs(rotationSpeedDegreesPerSecond) > RotationSpeedEpsilon;

                if (hasSelfRotation)
                {
                    float deltaYawRadians = math.radians(rotationSpeedDegreesPerSecond) * deltaTime;
                    quaternion deltaRotation = quaternion.RotateY(deltaYawRadians);
                    currentEnemyTransform.Rotation = math.normalize(math.mul(currentEnemyTransform.Rotation, deltaRotation));
                }
                else
                {
                    float3 facingVelocity = EnemyKnockbackRuntimeUtility.ResolveCombinedVelocity(currentEnemyRuntimeState.Velocity,
                                                                                                in currentEnemyKnockbackState);
                    currentEnemyTransform.Rotation = EnemySteeringUtility.ResolveDynamicLookRotation(currentEnemyTransform.Rotation,
                                                                                                      facingVelocity,
                                                                                                      math.max(math.max(moveSpeed, effectiveMaxSpeed), math.length(facingVelocity)),
                                                                                                      in shooterControlState,
                                                                                                      steeringAggressiveness,
                                                                                                      deltaTime);
                }
            }

            enemyRuntimeState.ValueRW = currentEnemyRuntimeState;
            enemyKnockbackState.ValueRW = currentEnemyKnockbackState;
            patternRuntimeState.ValueRW = currentPatternRuntimeState;
            enemyTransform.ValueRW = currentEnemyTransform;
        }

        occupancyCellMap.Dispose();
        occupancyDvdBounceDamping.Dispose();
        occupancyDvdFlags.Dispose();
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
    /// <returns>True when DVD movement requests steering and priority bypass.<returns>
    private static bool ShouldIgnoreSteeringAndPriority(in EnemyPatternConfig patternConfig)
    {
        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererDvd)
            return false;

        return patternConfig.DvdIgnoreSteeringAndPriority != 0;
    }

    /// <summary>
    /// Blends clearance with the current desired velocity while preserving stable forward speed.
    /// </summary>
    /// <param name="baseVelocity">Current desired planar velocity before clearance.</param>
    /// <param name="clearanceVelocity">Planar clearance contribution.</param>
    /// <param name="clearanceBlend">Clearance blend scalar.</param>
    /// <param name="minimumForwardSpeedRatio">Minimum retained forward speed ratio in [0..1].</param>
    /// <returns>Blended desired velocity.<returns>
    private static float3 ComposeDesiredVelocityWithClearance(float3 baseVelocity,
                                                              float3 clearanceVelocity,
                                                              float clearanceBlend,
                                                              float minimumForwardSpeedRatio)
    {
        float blend = math.max(0f, clearanceBlend);

        if (blend <= 0f)
            return baseVelocity;

        float3 blendedClearance = clearanceVelocity * blend;
        float baseSpeed = math.length(baseVelocity);

        if (baseSpeed <= DirectionEpsilon)
            return baseVelocity + blendedClearance;

        float3 forwardDirection = baseVelocity / math.max(baseSpeed, DirectionEpsilon);
        float forwardDelta = math.dot(blendedClearance, forwardDirection);
        float minimumForwardSpeed = baseSpeed * math.clamp(minimumForwardSpeedRatio, 0f, 1f);
        float maximumForwardSpeed = baseSpeed * 1.15f;
        float forwardSpeed = math.clamp(baseSpeed + forwardDelta, minimumForwardSpeed, maximumForwardSpeed);
        float3 lateralClearance = blendedClearance - forwardDirection * forwardDelta;
        return forwardDirection * forwardSpeed + lateralClearance;
    }

    /// <summary>
    /// Resolves per-frame velocity change rate using acceleration for speed-up and deceleration for slow-down.
    /// </summary>
    /// <param name="currentVelocity">Current planar velocity.</param>
    /// <param name="desiredVelocity">Target planar velocity.</param>
    /// <param name="acceleration">Configured acceleration.</param>
    /// <param name="deceleration">Configured deceleration.</param>
    /// <returns>Velocity delta rate in units per second.<returns>
    private static float ResolveVelocityChangeRate(float3 currentVelocity,
                                                   float3 desiredVelocity,
                                                   float acceleration,
                                                   float deceleration)
    {
        float currentSpeed = math.length(currentVelocity);
        float desiredSpeed = math.length(desiredVelocity);

        if (desiredSpeed + DirectionEpsilon >= currentSpeed)
            return math.max(0f, acceleration);

        if (deceleration > 0f)
            return deceleration;

        return math.max(0f, acceleration);
    }

    /// <summary>
    /// Resolves one steering aggressiveness value with safe defaults and clamps.
    /// </summary>
    /// <param name="rawAggressiveness">Serialized aggressiveness value.</param>
    /// <returns>Resolved aggressiveness value ready for runtime use.<returns>
    private static float ResolveSteeringAggressiveness(float rawAggressiveness)
    {
        if (rawAggressiveness < 0f)
            return MinimumSteeringAggressiveness;

        return math.clamp(rawAggressiveness, MinimumSteeringAggressiveness, MaximumSteeringAggressiveness);
    }

    /// <summary>
    /// Maps steering aggressiveness to a configurable scalar range.
    /// </summary>
    /// <param name="aggressiveness">Resolved aggressiveness value.</param>
    /// <param name="minimumScale">Output scale at minimum aggressiveness.</param>
    /// <param name="maximumScale">Output scale at maximum aggressiveness.</param>
    /// <returns>Interpolated scalar in the requested range.<returns>
    private static float ResolveAggressivenessScale(float aggressiveness, float minimumScale, float maximumScale)
    {
        float normalizedAggressiveness = math.saturate((aggressiveness - MinimumSteeringAggressiveness) /
                                                       math.max(0.0001f, MaximumSteeringAggressiveness - MinimumSteeringAggressiveness));
        return math.lerp(minimumScale, maximumScale, normalizedAggressiveness);
    }

    /// <summary>
    /// Resolves temporary max-speed boost applied while yielding to higher-priority neighbors.
    /// </summary>
    /// <param name="yieldUrgency">Yield urgency in [0..1].</param>
    /// <param name="priorityGapNormalized">Normalized priority-tier gap in [0..1].</param>
    /// <param name="aggressiveness">Resolved steering aggressiveness.</param>
    /// <returns>Additional speed ratio in [0..+].<returns>
    private static float ResolvePriorityYieldSpeedBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.85f, 1.22f);
        float gapScale = math.lerp(PriorityYieldGapSpeedScaleMin,
                                   PriorityYieldGapSpeedScaleMax,
                                   math.saturate(priorityGapNormalized));
        return normalizedUrgency * PriorityYieldMaxSpeedBoost * aggressivenessScale * gapScale;
    }

    /// <summary>
    /// Resolves temporary acceleration boost applied while yielding to higher-priority neighbors.
    /// </summary>
    /// <param name="yieldUrgency">Yield urgency in [0..1].</param>
    /// <param name="priorityGapNormalized">Normalized priority-tier gap in [0..1].</param>
    /// <param name="aggressiveness">Resolved steering aggressiveness.</param>
    /// <returns>Additional acceleration ratio in [0..+].<returns>
    private static float ResolvePriorityYieldAccelerationBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.9f, 1.3f);
        float gapScale = math.lerp(PriorityYieldGapAccelerationScaleMin,
                                   PriorityYieldGapAccelerationScaleMax,
                                   math.saturate(priorityGapNormalized));
        return normalizedUrgency * PriorityYieldMaxAccelerationBoost * aggressivenessScale * gapScale;
    }

    #endregion

    #endregion
}
