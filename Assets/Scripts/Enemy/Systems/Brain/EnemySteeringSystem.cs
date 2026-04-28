using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Runs enemy pursuit and separation steering using Burst jobs and LOD-driven update frequency.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySpawnSystem))]
public partial struct EnemySteeringSystem : ISystem
{
    #region Fields
    private EntityQuery activeEnemiesQuery;
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        activeEnemiesQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData,
                     EnemyRuntimeState,
                     EnemyKnockbackState,
                     LocalTransform,
                     EnemyActive,
                     EnemyElementalRuntimeState,
                     EnemyShooterControlState>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();

        state.RequireForUpdate(activeEnemiesQuery);
        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        int enemyCount = activeEnemiesQuery.CalculateEntityCount();

        if (enemyCount <= 0)
            return;

        if (!SystemAPI.TryGetSingletonEntity<PlayerControllerConfig>(out Entity playerEntity))
            return;

        float3 playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        if (enemyTimeScale <= 0f)
            return;

        NativeArray<Entity> enemyEntities = activeEnemiesQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<LocalTransform> enemyTransforms = activeEnemiesQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        NativeArray<EnemyData> enemyDataArray = activeEnemiesQuery.ToComponentDataArray<EnemyData>(Allocator.TempJob);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = activeEnemiesQuery.ToComponentDataArray<EnemyRuntimeState>(Allocator.TempJob);
        NativeArray<EnemyKnockbackState> enemyKnockbackArray = activeEnemiesQuery.ToComponentDataArray<EnemyKnockbackState>(Allocator.TempJob);
        NativeArray<EnemyElementalRuntimeState> enemyElementalRuntimeArray = activeEnemiesQuery.ToComponentDataArray<EnemyElementalRuntimeState>(Allocator.TempJob);
        NativeArray<EnemyShooterControlState> enemyShooterControlArray = activeEnemiesQuery.ToComponentDataArray<EnemyShooterControlState>(Allocator.TempJob);

        NativeArray<float3> positions = new NativeArray<float3>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float2> speedData = new NativeArray<float2>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> contactRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> bodyRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> priorityTiers = new NativeArray<int>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> steeringAggressiveness = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float3> planarVelocities = new NativeArray<float3>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<byte> wandererMovementFlags = new NativeArray<byte>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<byte> customPatternMovementFlags = new NativeArray<byte>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<byte> spawnInactivityLockFlags = new NativeArray<byte>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<byte> shooterMovementLockedFlags = new NativeArray<byte>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> separationRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> ownedBossIndices = new NativeArray<int>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int2> cellCoordinates = default;
        NativeArray<int> enemyToEvaluatedIndex = new NativeArray<int>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeParallelHashMap<Entity, int> enemyIndexByEntity = new NativeParallelHashMap<Entity, int>(enemyCount, Allocator.TempJob);

        NativeList<int> evaluatedEnemyIndices = new NativeList<int>(enemyCount, Allocator.TempJob);
        ComponentLookup<EnemyCustomPatternMovementTag> customPatternMovementTagLookup = SystemAPI.GetComponentLookup<EnemyCustomPatternMovementTag>(true);
        ComponentLookup<EnemyPatternConfig> patternConfigLookup = SystemAPI.GetComponentLookup<EnemyPatternConfig>(true);
        ComponentLookup<EnemyPatternRuntimeState> patternRuntimeStateLookup = SystemAPI.GetComponentLookup<EnemyPatternRuntimeState>(true);
        ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup = SystemAPI.GetComponentLookup<EnemySpawnInactivityLock>(true);
        ComponentLookup<EnemyBossMinionOwner> bossMinionOwnerLookup = SystemAPI.GetComponentLookup<EnemyBossMinionOwner>(true);

        float maxSeparationRadius = 0.25f;
        float maxBodyRadius = 0.05f;
        float maxSteeringAggressiveness = EnemySteeringUtility.DefaultSteeringAggressiveness;
        int frameCount = Time.frameCount;

        for (int index = 0; index < enemyCount; index++)
            enemyIndexByEntity.TryAdd(enemyEntities[index], index);

        for (int index = 0; index < enemyCount; index++)
        {
            EnemyData enemyData = enemyDataArray[index];
            LocalTransform enemyTransform = enemyTransforms[index];

            positions[index] = enemyTransform.Position;
            Entity enemyEntity = enemyEntities[index];
            float elementalSlowPercent = math.clamp(enemyElementalRuntimeArray[index].SlowPercent, 0f, 100f);
            EnemyKnockbackState currentKnockbackState = enemyKnockbackArray[index];

            float slowMultiplier = math.saturate(1f - elementalSlowPercent * 0.01f);
            speedData[index] = new float2(math.max(0f, enemyData.MoveSpeed) * slowMultiplier, math.max(0f, enemyData.MaxSpeed) * slowMultiplier);
            contactRadii[index] = math.max(0f, enemyData.ContactRadius);
            bodyRadii[index] = math.max(0.05f, enemyData.BodyRadius);
            priorityTiers[index] = math.clamp(enemyData.PriorityTier, -128, 128);
            float enemySteeringAggressiveness = EnemySteeringUtility.ResolveSteeringAggressiveness(enemyData.SteeringAggressiveness);
            steeringAggressiveness[index] = enemySteeringAggressiveness;
            float3 planarVelocity = EnemyKnockbackRuntimeUtility.ResolveCombinedVelocity(enemyRuntimeArray[index].Velocity,
                                                                                        in currentKnockbackState);
            planarVelocity.y = 0f;
            planarVelocities[index] = planarVelocity;
            ownedBossIndices[index] = ResolveOwnedBossIndex(enemyEntity, in bossMinionOwnerLookup, enemyIndexByEntity);
            wandererMovementFlags[index] = 0;
            spawnInactivityLockFlags[index] = spawnInactivityLockLookup.HasComponent(enemyEntity) && spawnInactivityLockLookup.IsComponentEnabled(enemyEntity)
                ? (byte)1
                : (byte)0;
            bool hasCustomPatternMovementTag = customPatternMovementTagLookup.HasComponent(enemyEntity);
            bool usesCustomPatternMovement = hasCustomPatternMovementTag;

            if (patternConfigLookup.HasComponent(enemyEntity))
            {
                EnemyPatternConfig patternConfig = patternConfigLookup[enemyEntity];
                EnemyPatternRuntimeState patternRuntimeState = patternRuntimeStateLookup.HasComponent(enemyEntity)
                    ? patternRuntimeStateLookup[enemyEntity]
                    : default;
                float3 toPlayer = playerPosition - enemyTransform.Position;
                toPlayer.y = 0f;
                float playerDistance = math.length(toPlayer);
                EnemyCompiledMovementPatternKind activeMovementKind = EnemyPatternMovementRuntimeUtility.ResolveActiveMovementKind(in patternConfig,
                                                                                                                                    in patternRuntimeState,
                                                                                                                                    playerDistance);

                if (hasCustomPatternMovementTag && activeMovementKind == EnemyCompiledMovementPatternKind.Grunt)
                    usesCustomPatternMovement = false;

                if (activeMovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                    activeMovementKind == EnemyCompiledMovementPatternKind.WandererDvd ||
                    activeMovementKind == EnemyCompiledMovementPatternKind.Coward)
                {
                    wandererMovementFlags[index] = 1;
                }
            }

            customPatternMovementFlags[index] = usesCustomPatternMovement ? (byte)1 : (byte)0;
            shooterMovementLockedFlags[index] = 0;

            if (enemyShooterControlArray[index].MovementLocked != 0)
                shooterMovementLockedFlags[index] = 1;

            if (bodyRadii[index] > maxBodyRadius)
                maxBodyRadius = bodyRadii[index];

            float separationRadius = math.max(0.05f, enemyData.SeparationRadius);
            separationRadii[index] = separationRadius;

            if (separationRadius > maxSeparationRadius)
                maxSeparationRadius = separationRadius;

            if (enemySteeringAggressiveness > maxSteeringAggressiveness)
                maxSteeringAggressiveness = enemySteeringAggressiveness;

            enemyToEvaluatedIndex[index] = -1;

            // LOD gating reduces per-frame steering cost for far enemies while keeping a stable cadence.
            EnemySteeringUtility.SteeringLodLevel lodLevel = EnemySteeringUtility.EvaluateLod(playerPosition, enemyTransform.Position);
            bool shouldEvaluate = EnemySteeringUtility.ShouldEvaluateLod(lodLevel, frameCount, enemyEntity.Index);

            if (!shouldEvaluate && ownedBossIndices[index] >= 0)
            {
                int ownedBossIndex = ownedBossIndices[index];
                shouldEvaluate = EnemyBossMinionSteeringUtility.ShouldForceBossAvoidanceEvaluation(enemyTransform.Position,
                                                                                                   enemyTransforms[ownedBossIndex].Position,
                                                                                                   enemyData.BodyRadius,
                                                                                                   enemyData.SeparationRadius,
                                                                                                   enemySteeringAggressiveness);
            }

            if (shouldEvaluate && customPatternMovementFlags[index] == 0 && spawnInactivityLockFlags[index] == 0)
            {
                int evaluatedIndex = evaluatedEnemyIndices.Length;
                enemyToEvaluatedIndex[index] = evaluatedIndex;
                evaluatedEnemyIndices.Add(index);
            }
        }

        int evaluatedCount = evaluatedEnemyIndices.Length;
        NativeParallelMultiHashMap<int, int> cellMap = default;
        NativeArray<float3> approachResults = default;
        NativeArray<float3> separationResults = default;
        NativeArray<float> separationUrgencyResults = default;
        NativeArray<float> priorityYieldUrgencyResults = default;
        NativeArray<float> priorityYieldGapResults = default;

        if (evaluatedCount > 0)
        {
            float clearanceAggressivenessScale = EnemySteeringUtility.ResolveAggressivenessScale(maxSteeringAggressiveness, 0.82f, 1.35f);
            float maxHardClearanceRadius = (maxBodyRadius * 2f + EnemySteeringUtility.SeparationClearancePadding) * 2.75f * clearanceAggressivenessScale;
            float maxScaledSeparationRadius = maxSeparationRadius * EnemySteeringUtility.ResolveAggressivenessScale(maxSteeringAggressiveness, 0.9f, 1.45f);
            float cellSize = math.max(0.25f, math.max(maxScaledSeparationRadius, maxHardClearanceRadius));
            float inverseCellSize = 1f / cellSize;
            cellCoordinates = new NativeArray<int2>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            cellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.TempJob);

            for (int index = 0; index < enemyCount; index++)
            {
                float3 position = positions[index];
                int cellX = (int)math.floor(position.x * inverseCellSize);
                int cellY = (int)math.floor(position.z * inverseCellSize);
                int2 cell = new int2(cellX, cellY);
                cellCoordinates[index] = cell;
                cellMap.Add(EnemySpatialHashUtility.EncodeCell(cellX, cellY), index);
            }

            approachResults = new NativeArray<float3>(evaluatedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            separationResults = new NativeArray<float3>(evaluatedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            separationUrgencyResults = new NativeArray<float>(evaluatedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            priorityYieldUrgencyResults = new NativeArray<float>(evaluatedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            priorityYieldGapResults = new NativeArray<float>(evaluatedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            // Approach and separation are independent, so they run in parallel and sync once before integration.
            EnemySteeringUtility.EnemyApproachJob approachJob = new EnemySteeringUtility.EnemyApproachJob
            {
                EvaluatedEnemyIndices = evaluatedEnemyIndices.AsArray(),
                Positions = positions,
                SpeedData = speedData,
                ContactRadii = contactRadii,
                PlayerPosition = playerPosition,
                Results = approachResults
            };

            EnemySteeringUtility.EnemySeparationJob separationJob = new EnemySteeringUtility.EnemySeparationJob
            {
                EvaluatedEnemyIndices = evaluatedEnemyIndices.AsArray(),
                Positions = positions,
                BodyRadii = bodyRadii,
                PriorityTiers = priorityTiers,
                SteeringAggressiveness = steeringAggressiveness,
                Velocities = planarVelocities,
                WandererMovementFlags = wandererMovementFlags,
                OwnedBossIndices = ownedBossIndices,
                SeparationRadii = separationRadii,
                CellCoordinates = cellCoordinates,
                CellMap = cellMap,
                Results = separationResults,
                UrgencyResults = separationUrgencyResults,
                YieldUrgencyResults = priorityYieldUrgencyResults,
                YieldPriorityGapResults = priorityYieldGapResults
            };

            JobHandle approachHandle = approachJob.Schedule(evaluatedCount, 64, state.Dependency);
            JobHandle separationHandle = separationJob.Schedule(evaluatedCount, 64, state.Dependency);
            JobHandle combinedHandle = JobHandle.CombineDependencies(approachHandle, separationHandle);
            combinedHandle.Complete();
        }

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();
        EnemyNavigationGridState navigationGridState = default;
        DynamicBuffer<EnemyNavigationCellElement> navigationCells = default;
        bool navigationReady = false;

        if (SystemAPI.TryGetSingleton<EnemyNavigationGridState>(out navigationGridState) &&
            SystemAPI.TryGetSingletonBuffer<EnemyNavigationCellElement>(out navigationCells))
        {
            navigationReady = true;
        }

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        bool wallsEnabled = wallsLayerMask != 0;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            Entity enemyEntity = enemyEntities[enemyIndex];

            if (customPatternMovementFlags[enemyIndex] != 0)
                continue;

            if (spawnInactivityLockFlags[enemyIndex] != 0)
            {
                EnemyRuntimeState lockedRuntimeState = enemyRuntimeArray[enemyIndex];
                lockedRuntimeState.Velocity = float3.zero;
                enemyRuntimeArray[enemyIndex] = lockedRuntimeState;
                continue;
            }

            EnemyData enemyData = enemyDataArray[enemyIndex];
            EnemyRuntimeState runtimeState = enemyRuntimeArray[enemyIndex];
            EnemyKnockbackState knockbackState = enemyKnockbackArray[enemyIndex];
            LocalTransform enemyTransform = enemyTransforms[enemyIndex];
            float velocityMaxSpeed = math.max(0f, speedData[enemyIndex].y);
            float enemySteeringAggressiveness = steeringAggressiveness[enemyIndex];

            int evaluatedIndex = enemyToEvaluatedIndex[enemyIndex];

            if (evaluatedIndex >= 0)
            {
                float separationWeight = math.max(0f, enemyData.SeparationWeight);
                float3 desiredVelocity = approachResults[evaluatedIndex];

                if (navigationReady && wallsEnabled)
                {
                    float navigationDesiredSpeed = velocityMaxSpeed > 0f ? velocityMaxSpeed : math.max(0f, enemyData.MoveSpeed);
                    float navigationCollisionRadius = math.max(0.01f, enemyData.BodyRadius + math.max(0f, enemyData.MinimumWallDistance));

                    if (EnemyNavigationFlowFieldUtility.TryResolveNavigationVelocity(enemyTransform.Position,
                                                                                    playerPosition,
                                                                                    navigationCollisionRadius,
                                                                                    navigationDesiredSpeed,
                                                                                    in physicsWorldSingleton,
                                                                                    wallsLayerMask,
                                                                                    in navigationGridState,
                                                                                    navigationCells,
                                                                                    out float3 navigationVelocity))
                    {
                        desiredVelocity = navigationVelocity;
                    }
                }

                float urgency = math.saturate(separationUrgencyResults[evaluatedIndex]);
                float urgencyBoost = math.lerp(1f, EnemySteeringUtility.SeparationUrgencyMaxBoost, urgency);
                float separationResponseScale = EnemySteeringUtility.ResolveAggressivenessScale(enemySteeringAggressiveness, 0.72f, 1.95f);
                desiredVelocity += separationResults[evaluatedIndex] * separationWeight * urgencyBoost * separationResponseScale * enemySteeringAggressiveness;
                float priorityYieldUrgency = math.saturate(priorityYieldUrgencyResults[evaluatedIndex]);
                float priorityYieldGap = math.saturate(priorityYieldGapResults[evaluatedIndex]);

                if (velocityMaxSpeed > 0f && priorityYieldUrgency > 0f)
                {
                    float speedBoost = EnemySteeringUtility.ResolvePriorityYieldSpeedBoost(priorityYieldUrgency, priorityYieldGap, enemySteeringAggressiveness);
                    velocityMaxSpeed *= 1f + speedBoost;
                }

                float desiredSpeed = math.length(desiredVelocity);

                if (velocityMaxSpeed > 0f && desiredSpeed > velocityMaxSpeed && desiredSpeed > EnemySteeringUtility.DirectionEpsilon)
                    desiredVelocity *= velocityMaxSpeed / desiredSpeed;

                float acceleration = math.max(0f, enemyData.Acceleration);
                float deceleration = math.max(0f, enemyData.Deceleration);
                float accelerationRate = EnemySteeringUtility.ResolveVelocityChangeRate(runtimeState.Velocity,
                                                                                       desiredVelocity,
                                                                                       acceleration,
                                                                                       deceleration);

                if (priorityYieldUrgency > 0f)
                {
                    float accelerationBoost = EnemySteeringUtility.ResolvePriorityYieldAccelerationBoost(priorityYieldUrgency, priorityYieldGap, enemySteeringAggressiveness);
                    accelerationRate *= 1f + accelerationBoost;
                }

                float maxVelocityDelta = accelerationRate * deltaTime;
                float3 velocityDelta = desiredVelocity - runtimeState.Velocity;
                float velocityDeltaMagnitude = math.length(velocityDelta);

                if (velocityDeltaMagnitude > maxVelocityDelta && velocityDeltaMagnitude > EnemySteeringUtility.DirectionEpsilon)
                    runtimeState.Velocity += velocityDelta * (maxVelocityDelta / velocityDeltaMagnitude);
                else
                    runtimeState.Velocity = desiredVelocity;
            }

            float velocityMagnitude = math.length(runtimeState.Velocity);

            if (velocityMaxSpeed > 0f && velocityMagnitude > velocityMaxSpeed && velocityMagnitude > EnemySteeringUtility.DirectionEpsilon)
                runtimeState.Velocity *= velocityMaxSpeed / velocityMagnitude;

            if (shooterMovementLockedFlags[enemyIndex] != 0)
                runtimeState.Velocity = float3.zero;

            float3 desiredDisplacement = runtimeState.Velocity * deltaTime;
            float desiredDisplacementSquared = math.lengthsq(desiredDisplacement);
            float3 position = enemyTransform.Position;
            float3 resolvedDisplacement = desiredDisplacement;
            float3 resolvedVelocity = runtimeState.Velocity;

            if (wallsEnabled && desiredDisplacementSquared > EnemySteeringUtility.DirectionEpsilon)
            {
                float collisionRadius = math.max(0.01f, enemyData.BodyRadius + math.max(0f, enemyData.MinimumWallDistance));
                bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                       position,
                                                                                       desiredDisplacement,
                                                                                       collisionRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 allowedDisplacement,
                                                                                       out float3 hitNormal);

                resolvedDisplacement = allowedDisplacement;

                if (hitWall)
                {
                    resolvedVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(runtimeState.Velocity, hitNormal);

                    // If direct motion is blocked, try a short wall-circumnavigation displacement before stopping.
                    if (evaluatedIndex >= 0 &&
                        EnemyWallSteeringUtility.TryResolveCircumnavigationDisplacement(physicsWorldSingleton,
                                                                                        enemyEntity.Index,
                                                                                        enemyTransform.Position,
                                                                                        playerPosition,
                                                                                        runtimeState.Velocity,
                                                                                        desiredDisplacement,
                                                                                        allowedDisplacement,
                                                                                        hitNormal,
                                                                                        collisionRadius,
                                                                                        wallsLayerMask,
                                                                                        deltaTime,
                                                                                        out float3 bypassDisplacement,
                                                                                        out float3 bypassVelocity,
                                                                                        out float3 bypassHitNormal))
                    {
                        resolvedDisplacement = bypassDisplacement;
                        resolvedVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(bypassVelocity, bypassHitNormal);
                    }
                }
            }

            position += resolvedDisplacement;
            runtimeState.Velocity = resolvedVelocity;
            float knockbackCollisionRadius = math.max(0.01f, enemyData.BodyRadius + math.max(0f, enemyData.MinimumWallDistance));
            EnemyKnockbackRuntimeUtility.ApplyDisplacement(ref knockbackState,
                                                           ref position,
                                                           knockbackCollisionRadius,
                                                           in physicsWorldSingleton,
                                                           wallsLayerMask,
                                                           wallsEnabled,
                                                           deltaTime);
            position.y = enemyTransform.Position.y;
            enemyTransform.Position = position;

            float rotationSpeedDegreesPerSecond = enemyData.RotationSpeedDegreesPerSecond;
            bool hasSelfRotation = math.abs(rotationSpeedDegreesPerSecond) > EnemySteeringUtility.RotationSpeedEpsilon;

            if (hasSelfRotation)
            {
                float deltaYawRadians = math.radians(rotationSpeedDegreesPerSecond) * deltaTime;
                quaternion deltaRotation = quaternion.RotateY(deltaYawRadians);
                enemyTransform.Rotation = math.normalize(math.mul(enemyTransform.Rotation, deltaRotation));
            }
            else
            {
                EnemyShooterControlState shooterControlState = enemyShooterControlArray[enemyIndex];
                float3 facingVelocity = EnemyKnockbackRuntimeUtility.ResolveCombinedVelocity(runtimeState.Velocity, in knockbackState);
                enemyTransform.Rotation = EnemySteeringUtility.ResolveDynamicLookRotation(enemyTransform.Rotation,
                                                                                          facingVelocity,
                                                                                          math.max(velocityMaxSpeed, math.length(facingVelocity)),
                                                                                          in shooterControlState,
                                                                                          enemySteeringAggressiveness,
                                                                                          deltaTime);
            }

            enemyRuntimeArray[enemyIndex] = runtimeState;
            enemyKnockbackArray[enemyIndex] = knockbackState;
            enemyTransforms[enemyIndex] = enemyTransform;
        }

        activeEnemiesQuery.CopyFromComponentDataArray(enemyRuntimeArray);
        activeEnemiesQuery.CopyFromComponentDataArray(enemyKnockbackArray);
        activeEnemiesQuery.CopyFromComponentDataArray(enemyTransforms);

        enemyEntities.Dispose();
        enemyTransforms.Dispose();
        enemyDataArray.Dispose();
        enemyRuntimeArray.Dispose();
        enemyKnockbackArray.Dispose();
        enemyElementalRuntimeArray.Dispose();
        enemyShooterControlArray.Dispose();
        positions.Dispose();
        speedData.Dispose();
        contactRadii.Dispose();
        bodyRadii.Dispose();
        priorityTiers.Dispose();
        steeringAggressiveness.Dispose();
        planarVelocities.Dispose();
        wandererMovementFlags.Dispose();
        ownedBossIndices.Dispose();
        customPatternMovementFlags.Dispose();
        spawnInactivityLockFlags.Dispose();
        shooterMovementLockedFlags.Dispose();
        separationRadii.Dispose();
        enemyToEvaluatedIndex.Dispose();
        enemyIndexByEntity.Dispose();
        evaluatedEnemyIndices.Dispose();

        if (cellCoordinates.IsCreated)
            cellCoordinates.Dispose();

        if (approachResults.IsCreated)
            approachResults.Dispose();

        if (separationResults.IsCreated)
            separationResults.Dispose();

        if (separationUrgencyResults.IsCreated)
            separationUrgencyResults.Dispose();

        if (priorityYieldUrgencyResults.IsCreated)
            priorityYieldUrgencyResults.Dispose();

        if (priorityYieldGapResults.IsCreated)
            priorityYieldGapResults.Dispose();

        if (cellMap.IsCreated)
            cellMap.Dispose();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the active-array index of the boss that owns one minion.
    /// /params enemyEntity Enemy entity to inspect.
    /// /params bossMinionOwnerLookup Lookup used to read boss-minion ownership.
    /// /params enemyIndexByEntity Active enemy entity-to-array-index map.
    /// /returns Owning boss index, or -1 when the enemy is not an active boss-owned minion.
    /// </summary>
    private static int ResolveOwnedBossIndex(Entity enemyEntity,
                                             in ComponentLookup<EnemyBossMinionOwner> bossMinionOwnerLookup,
                                             NativeParallelHashMap<Entity, int> enemyIndexByEntity)
    {
        if (!bossMinionOwnerLookup.HasComponent(enemyEntity))
            return -1;

        EnemyBossMinionOwner owner = bossMinionOwnerLookup[enemyEntity];

        if (owner.BossEntity == Entity.Null)
            return -1;

        if (!enemyIndexByEntity.TryGetValue(owner.BossEntity, out int bossIndex))
            return -1;

        return bossIndex;
    }
    #endregion

    #endregion
}
