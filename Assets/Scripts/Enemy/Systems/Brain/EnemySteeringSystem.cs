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
    #region Constants
    private static readonly float3 UpAxis = new float3(0f, 1f, 0f);
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float DirectionEpsilon = 1e-6f;
    private const float RotationSpeedEpsilon = 1e-4f;
    private const float SeparationClearancePadding = 0.05f;
    private const float SeparationPredictionBaseSeconds = 0.14f;
    private const float SeparationPredictionSpeedScale = 0.04f;
    private const float SeparationPredictionMaxSeconds = 0.55f;
    private const float PriorityApproachUrgencyWeight = 2.2f;
    private const float SeparationUrgencyMaxBoost = 3.6f;
    private const float PriorityYieldMaxSpeedBoost = 0.75f;
    private const float PriorityYieldMaxAccelerationBoost = 2.25f;
    private const float PriorityYieldGapNormalization = 6f;
    private const float PriorityYieldGapSpeedScaleMin = 0.62f;
    private const float PriorityYieldGapSpeedScaleMax = 1.45f;
    private const float PriorityYieldGapAccelerationScaleMin = 0.7f;
    private const float PriorityYieldGapAccelerationScaleMax = 1.7f;
    private const float DefaultSteeringAggressiveness = 1f;
    private const float MinimumSteeringAggressiveness = 0f;
    private const float MaximumSteeringAggressiveness = 2.5f;
    private const float LookRotationSpeedGateRatio = 0.12f;
    private const float LookRotationFallbackSpeed = 0.2f;
    private const float LookRotationMinDegreesPerSecond = 360f;
    private const float LookRotationMaxDegreesPerSecond = 820f;

    private const float HighLodRadius = 16f;
    private const float MediumLodRadius = 34f;
    private const int MediumLodUpdateInterval = 2;
    private const int LowLodUpdateInterval = 4;
    #endregion

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

        if (SystemAPI.TryGetSingletonEntity<PlayerControllerConfig>(out Entity playerEntity) == false)
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
        NativeArray<byte> shooterMovementLockedFlags = new NativeArray<byte>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> separationRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int2> cellCoordinates = default;
        NativeArray<int> enemyToEvaluatedIndex = new NativeArray<int>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeList<int> evaluatedEnemyIndices = new NativeList<int>(enemyCount, Allocator.TempJob);
        ComponentLookup<EnemyCustomPatternMovementTag> customPatternMovementTagLookup = SystemAPI.GetComponentLookup<EnemyCustomPatternMovementTag>(true);
        ComponentLookup<EnemyPatternConfig> patternConfigLookup = SystemAPI.GetComponentLookup<EnemyPatternConfig>(true);

        float maxSeparationRadius = 0.25f;
        float maxBodyRadius = 0.05f;
        float maxSteeringAggressiveness = DefaultSteeringAggressiveness;
        int frameCount = Time.frameCount;

        for (int index = 0; index < enemyCount; index++)
        {
            EnemyData enemyData = enemyDataArray[index];
            LocalTransform enemyTransform = enemyTransforms[index];

            positions[index] = enemyTransform.Position;
            Entity enemyEntity = enemyEntities[index];
            float elementalSlowPercent = math.clamp(enemyElementalRuntimeArray[index].SlowPercent, 0f, 100f);

            float slowMultiplier = math.saturate(1f - elementalSlowPercent * 0.01f);
            speedData[index] = new float2(math.max(0f, enemyData.MoveSpeed) * slowMultiplier, math.max(0f, enemyData.MaxSpeed) * slowMultiplier);
            contactRadii[index] = math.max(0f, enemyData.ContactRadius);
            bodyRadii[index] = math.max(0.05f, enemyData.BodyRadius);
            priorityTiers[index] = math.clamp(enemyData.PriorityTier, -128, 128);
            float enemySteeringAggressiveness = ResolveSteeringAggressiveness(enemyData.SteeringAggressiveness);
            steeringAggressiveness[index] = enemySteeringAggressiveness;
            float3 planarVelocity = enemyRuntimeArray[index].Velocity;
            planarVelocity.y = 0f;
            planarVelocities[index] = planarVelocity;
            wandererMovementFlags[index] = 0;
            bool hasCustomPatternMovementTag = customPatternMovementTagLookup.HasComponent(enemyEntity);
            customPatternMovementFlags[index] = hasCustomPatternMovementTag ? (byte)1 : (byte)0;

            if (hasCustomPatternMovementTag && patternConfigLookup.HasComponent(enemyEntity))
            {
                EnemyPatternConfig patternConfig = patternConfigLookup[enemyEntity];

                if (patternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererBasic ||
                    patternConfig.MovementKind == EnemyCompiledMovementPatternKind.WandererDvd)
                    wandererMovementFlags[index] = 1;
            }

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

            SteeringLodLevel lodLevel = EvaluateLod(playerPosition, enemyTransform.Position);
            bool shouldEvaluate = ShouldEvaluateLod(lodLevel, frameCount, enemyEntity.Index);
            if (shouldEvaluate && hasCustomPatternMovementTag == false)
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
            float clearanceAggressivenessScale = ResolveAggressivenessScale(maxSteeringAggressiveness, 0.82f, 1.35f);
            float maxHardClearanceRadius = (maxBodyRadius * 2f + SeparationClearancePadding) * 2.75f * clearanceAggressivenessScale;
            float maxScaledSeparationRadius = maxSeparationRadius * ResolveAggressivenessScale(maxSteeringAggressiveness, 0.9f, 1.45f);
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

            EnemyApproachJob approachJob = new EnemyApproachJob
            {
                EvaluatedEnemyIndices = evaluatedEnemyIndices.AsArray(),
                Positions = positions,
                SpeedData = speedData,
                ContactRadii = contactRadii,
                PlayerPosition = playerPosition,
                Results = approachResults
            };

            EnemySeparationJob separationJob = new EnemySeparationJob
            {
                EvaluatedEnemyIndices = evaluatedEnemyIndices.AsArray(),
                Positions = positions,
                BodyRadii = bodyRadii,
                PriorityTiers = priorityTiers,
                SteeringAggressiveness = steeringAggressiveness,
                Velocities = planarVelocities,
                WandererMovementFlags = wandererMovementFlags,
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

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        bool wallsEnabled = wallsLayerMask != 0;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            Entity enemyEntity = enemyEntities[enemyIndex];

            if (customPatternMovementFlags[enemyIndex] != 0)
                continue;

            EnemyData enemyData = enemyDataArray[enemyIndex];
            EnemyRuntimeState runtimeState = enemyRuntimeArray[enemyIndex];
            LocalTransform enemyTransform = enemyTransforms[enemyIndex];
            float velocityMaxSpeed = math.max(0f, speedData[enemyIndex].y);
            float enemySteeringAggressiveness = steeringAggressiveness[enemyIndex];

            int evaluatedIndex = enemyToEvaluatedIndex[enemyIndex];

            if (evaluatedIndex >= 0)
            {
                float separationWeight = math.max(0f, enemyData.SeparationWeight);
                float3 desiredVelocity = approachResults[evaluatedIndex];
                float urgency = math.saturate(separationUrgencyResults[evaluatedIndex]);
                float urgencyBoost = math.lerp(1f, SeparationUrgencyMaxBoost, urgency);
                float separationResponseScale = ResolveAggressivenessScale(enemySteeringAggressiveness, 0.72f, 1.95f);
                desiredVelocity += separationResults[evaluatedIndex] * separationWeight * urgencyBoost * separationResponseScale * enemySteeringAggressiveness;
                float priorityYieldUrgency = math.saturate(priorityYieldUrgencyResults[evaluatedIndex]);
                float priorityYieldGap = math.saturate(priorityYieldGapResults[evaluatedIndex]);

                if (velocityMaxSpeed > 0f && priorityYieldUrgency > 0f)
                {
                    float speedBoost = ResolvePriorityYieldSpeedBoost(priorityYieldUrgency, priorityYieldGap, enemySteeringAggressiveness);
                    velocityMaxSpeed *= 1f + speedBoost;
                }

                float desiredSpeed = math.length(desiredVelocity);

                if (velocityMaxSpeed > 0f && desiredSpeed > velocityMaxSpeed && desiredSpeed > DirectionEpsilon)
                    desiredVelocity *= velocityMaxSpeed / desiredSpeed;

                float acceleration = math.max(0f, enemyData.Acceleration);
                float deceleration = math.max(0f, enemyData.Deceleration);
                float accelerationRate = ResolveVelocityChangeRate(runtimeState.Velocity,
                                                                   desiredVelocity,
                                                                   acceleration,
                                                                   deceleration);

                if (priorityYieldUrgency > 0f)
                {
                    float accelerationBoost = ResolvePriorityYieldAccelerationBoost(priorityYieldUrgency, priorityYieldGap, enemySteeringAggressiveness);
                    accelerationRate *= 1f + accelerationBoost;
                }

                float maxVelocityDelta = accelerationRate * deltaTime;
                float3 velocityDelta = desiredVelocity - runtimeState.Velocity;
                float velocityDeltaMagnitude = math.length(velocityDelta);

                if (velocityDeltaMagnitude > maxVelocityDelta && velocityDeltaMagnitude > DirectionEpsilon)
                    runtimeState.Velocity += velocityDelta * (maxVelocityDelta / velocityDeltaMagnitude);
                else
                    runtimeState.Velocity = desiredVelocity;
            }

            float velocityMagnitude = math.length(runtimeState.Velocity);

            if (velocityMaxSpeed > 0f && velocityMagnitude > velocityMaxSpeed && velocityMagnitude > DirectionEpsilon)
                runtimeState.Velocity *= velocityMaxSpeed / velocityMagnitude;

            if (shooterMovementLockedFlags[enemyIndex] != 0)
                runtimeState.Velocity = float3.zero;

            float3 desiredDisplacement = runtimeState.Velocity * deltaTime;
            float desiredDisplacementSquared = math.lengthsq(desiredDisplacement);
            float3 position = enemyTransform.Position;
            float3 resolvedDisplacement = desiredDisplacement;
            float3 resolvedVelocity = runtimeState.Velocity;

            if (wallsEnabled && desiredDisplacementSquared > DirectionEpsilon)
            {
                float collisionRadius = math.max(0.01f, enemyData.BodyRadius);
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
            position.y = enemyTransform.Position.y;
            enemyTransform.Position = position;

            float planarVelocitySquared = runtimeState.Velocity.x * runtimeState.Velocity.x + runtimeState.Velocity.z * runtimeState.Velocity.z;
            float rotationSpeedDegreesPerSecond = enemyData.RotationSpeedDegreesPerSecond;
            bool hasSelfRotation = math.abs(rotationSpeedDegreesPerSecond) > RotationSpeedEpsilon;

            if (hasSelfRotation)
            {
                float deltaYawRadians = math.radians(rotationSpeedDegreesPerSecond) * deltaTime;
                quaternion deltaRotation = quaternion.RotateY(deltaYawRadians);
                enemyTransform.Rotation = math.normalize(math.mul(enemyTransform.Rotation, deltaRotation));
            }
            else if (planarVelocitySquared > DirectionEpsilon)
            {
                float planarSpeed = math.sqrt(planarVelocitySquared);
                float lookSpeedThreshold = ResolveLookSpeedThreshold(velocityMaxSpeed);

                if (planarSpeed > lookSpeedThreshold)
                {
                    float3 forward = math.normalizesafe(new float3(runtimeState.Velocity.x, 0f, runtimeState.Velocity.z), ForwardAxis);
                    float lookTurnRateDegrees = ResolveAggressivenessScale(enemySteeringAggressiveness,
                                                                           LookRotationMinDegreesPerSecond,
                                                                           LookRotationMaxDegreesPerSecond);
                    float maxRadiansDelta = math.radians(lookTurnRateDegrees) * deltaTime;
                    enemyTransform.Rotation = RotateTowardsPlanar(enemyTransform.Rotation, forward, maxRadiansDelta);
                }
            }

            enemyRuntimeArray[enemyIndex] = runtimeState;
            enemyTransforms[enemyIndex] = enemyTransform;
        }

        activeEnemiesQuery.CopyFromComponentDataArray(enemyRuntimeArray);
        activeEnemiesQuery.CopyFromComponentDataArray(enemyTransforms);

        enemyEntities.Dispose();
        enemyTransforms.Dispose();
        enemyDataArray.Dispose();
        enemyRuntimeArray.Dispose();
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
        customPatternMovementFlags.Dispose();
        shooterMovementLockedFlags.Dispose();
        separationRadii.Dispose();
        enemyToEvaluatedIndex.Dispose();
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
    private static SteeringLodLevel EvaluateLod(float3 playerPosition, float3 enemyPosition)
    {
        float3 delta = enemyPosition - playerPosition;
        delta.y = 0f;
        float sqrDistance = math.lengthsq(delta);

        float highSqr = HighLodRadius * HighLodRadius;

        if (sqrDistance <= highSqr)
            return SteeringLodLevel.High;

        float mediumSqr = MediumLodRadius * MediumLodRadius;

        if (sqrDistance <= mediumSqr)
            return SteeringLodLevel.Medium;

        return SteeringLodLevel.Low;
    }

    private static bool ShouldEvaluateLod(SteeringLodLevel lodLevel, int frameCount, int stableIndex)
    {
        if (lodLevel == SteeringLodLevel.High)
            return true;

        int interval = lodLevel == SteeringLodLevel.Medium ? MediumLodUpdateInterval : LowLodUpdateInterval;
        int token = frameCount + math.abs(stableIndex);
        return token % interval == 0;
    }

    /// <summary>
    /// Resolves one steering aggressiveness value with safe defaults and clamps.
    /// </summary>
    /// <param name="rawAggressiveness">Serialized aggressiveness value.</param>
    /// <returns>Resolved aggressiveness value ready for runtime use.</returns>
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
    /// <returns>Interpolated scalar in the requested range.</returns>
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
    /// <returns>Additional speed ratio in [0..+].</returns>
    private static float ResolvePriorityYieldSpeedBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.85f, 1.25f);
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
    /// <returns>Additional acceleration ratio in [0..+].</returns>
    private static float ResolvePriorityYieldAccelerationBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.9f, 1.35f);
        float gapScale = math.lerp(PriorityYieldGapAccelerationScaleMin,
                                   PriorityYieldGapAccelerationScaleMax,
                                   math.saturate(priorityGapNormalized));
        return normalizedUrgency * PriorityYieldMaxAccelerationBoost * aggressivenessScale * gapScale;
    }

    /// <summary>
    /// Resolves planar speed threshold used to skip noisy look updates when movement is near zero.
    /// </summary>
    /// <param name="maxSpeed">Current movement max speed after modifiers.</param>
    /// <returns>Planar speed threshold used by look smoothing.</returns>
    private static float ResolveLookSpeedThreshold(float maxSpeed)
    {
        float normalizedMaxSpeed = math.max(0f, maxSpeed);

        if (normalizedMaxSpeed <= DirectionEpsilon)
            return LookRotationFallbackSpeed;

        return math.max(LookRotationFallbackSpeed, normalizedMaxSpeed * LookRotationSpeedGateRatio);
    }

    /// <summary>
    /// Rotates current orientation toward target planar forward with a bounded angular delta.
    /// </summary>
    /// <param name="currentRotation">Current world rotation.</param>
    /// <param name="targetForward">Target planar forward direction.</param>
    /// <param name="maxRadiansDelta">Maximum rotation in radians for this frame.</param>
    /// <returns>Smoothed rotation result.</returns>
    private static quaternion RotateTowardsPlanar(quaternion currentRotation, float3 targetForward, float maxRadiansDelta)
    {
        float normalizedDelta = math.max(0f, maxRadiansDelta);

        if (normalizedDelta <= DirectionEpsilon)
            return currentRotation;

        quaternion targetRotation = quaternion.LookRotationSafe(targetForward, UpAxis);
        float4 currentValue = currentRotation.value;
        float4 targetValue = targetRotation.value;
        float dot = math.clamp(math.dot(currentValue, targetValue), -1f, 1f);
        float absoluteDot = math.abs(dot);
        float angle = math.acos(math.min(1f, absoluteDot)) * 2f;

        if (angle <= normalizedDelta || angle <= DirectionEpsilon)
            return targetRotation;

        float interpolation = math.saturate(normalizedDelta / math.max(angle, DirectionEpsilon));
        quaternion rotated = math.slerp(currentRotation, targetRotation, interpolation);
        return math.normalize(rotated);
    }

    /// <summary>
    /// Resolves per-frame velocity change rate using acceleration for speed-up and deceleration for slow-down.
    /// </summary>
    /// <param name="currentVelocity">Current planar velocity.</param>
    /// <param name="desiredVelocity">Target planar velocity.</param>
    /// <param name="acceleration">Configured acceleration.</param>
    /// <param name="deceleration">Configured deceleration.</param>
    /// <returns>Velocity delta rate in units per second.</returns>
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
    #endregion

    #endregion

    #region Jobs
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    private struct EnemyApproachJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> EvaluatedEnemyIndices;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float2> SpeedData;
        [ReadOnly] public NativeArray<float> ContactRadii;
        [ReadOnly] public float3 PlayerPosition;
        public NativeArray<float3> Results;

        public void Execute(int index)
        {
            int enemyIndex = EvaluatedEnemyIndices[index];
            float3 position = Positions[enemyIndex];
            float3 toPlayer = PlayerPosition - position;
            toPlayer.y = 0f;

            float sqrDistance = math.lengthsq(toPlayer);

            if (sqrDistance <= 1e-6f)
            {
                Results[index] = float3.zero;
                return;
            }

            float distance = math.sqrt(sqrDistance);
            float contactRadius = math.max(0f, ContactRadii[enemyIndex]);

            if (distance <= contactRadius)
            {
                Results[index] = float3.zero;
                return;
            }

            float3 direction = toPlayer / math.max(distance, 1e-6f);
            float moveSpeed = math.max(0f, SpeedData[enemyIndex].x);
            float maxSpeed = math.max(0f, SpeedData[enemyIndex].y);
            float speed = maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed;
            Results[index] = direction * speed;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    private struct EnemySeparationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> EvaluatedEnemyIndices;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> BodyRadii;
        [ReadOnly] public NativeArray<int> PriorityTiers;
        [ReadOnly] public NativeArray<float> SteeringAggressiveness;
        [ReadOnly] public NativeArray<float3> Velocities;
        [ReadOnly] public NativeArray<byte> WandererMovementFlags;
        [ReadOnly] public NativeArray<float> SeparationRadii;
        [ReadOnly] public NativeArray<int2> CellCoordinates;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> CellMap;
        public NativeArray<float3> Results;
        public NativeArray<float> UrgencyResults;
        public NativeArray<float> YieldUrgencyResults;
        public NativeArray<float> YieldPriorityGapResults;

        public void Execute(int index)
        {
            // Early outs and data retrieval
            int enemyIndex = EvaluatedEnemyIndices[index];
            float separationRadius = math.max(0.01f, SeparationRadii[enemyIndex]);
            float bodyRadius = math.max(0.01f, BodyRadii[enemyIndex]);
            int selfPriorityTier = PriorityTiers[enemyIndex];
            float selfSteeringAggressiveness = ResolveSteeringAggressiveness(SteeringAggressiveness[enemyIndex]);
            float3 position = Positions[enemyIndex];
            float3 selfVelocity = Velocities[enemyIndex];
            float selfSpeed = math.length(selfVelocity);
            int2 cell = CellCoordinates[enemyIndex];
            float3 separation = float3.zero;
            float highestUrgency = 0f;
            float highestYieldUrgency = 0f;
            float highestYieldPriorityGap = 0f;

            // Iterate neighbors in surrounding cells
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int key = EnemySpatialHashUtility.EncodeCell(cell.x + offsetX, cell.y + offsetY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int neighborIndex;

                    if (CellMap.TryGetFirstValue(key, out neighborIndex, out iterator) == false)
                        continue;

                    do
                    {
                        if (neighborIndex == enemyIndex)
                            continue;

                        // The separation behavior is based on a combination of hard clearance and a softer influence radius,
                        // both of which are scaled by the aggressiveness settings to allow for more dynamic and varied interactions.
                        // The prediction of future positions based on current velocities adds an anticipatory element to the separation,
                        // making it more effective at avoiding collisions in fast-paced scenarios.
                        float3 delta = position - Positions[neighborIndex];
                        delta.y = 0f;
                        float sqrDistance = math.lengthsq(delta);
                        float3 neighborVelocity = Velocities[neighborIndex];
                        float neighborSpeed = math.length(neighborVelocity);
                        float neighborBodyRadius = math.max(0.01f, BodyRadii[neighborIndex]);
                        int neighborPriorityTier = PriorityTiers[neighborIndex];
                        float neighborSteeringAggressiveness = ResolveSteeringAggressiveness(SteeringAggressiveness[neighborIndex]);
                        float pairSteeringAggressiveness = math.max(selfSteeringAggressiveness, neighborSteeringAggressiveness);
                        float pairClearanceScale = ResolveAggressivenessScale(pairSteeringAggressiveness, 0.82f, 1.35f);
                        bool neighborIsWanderer = WandererMovementFlags[neighborIndex] != 0;
                        float priorityClearanceMultiplier = ResolvePriorityClearanceMultiplier(selfPriorityTier, neighborPriorityTier);
                        float hardClearanceDistance = (bodyRadius + neighborBodyRadius + SeparationClearancePadding) * priorityClearanceMultiplier * pairClearanceScale;

                        if (neighborIsWanderer)
                            hardClearanceDistance *= 1.18f;

                        float selfRadiusScale = ResolveAggressivenessScale(selfSteeringAggressiveness, 0.9f, 1.45f);
                        float influenceRadius = math.max(separationRadius * selfRadiusScale, hardClearanceDistance * 1.35f);

                        if (selfPriorityTier < neighborPriorityTier)
                            influenceRadius = math.max(influenceRadius, hardClearanceDistance * 1.65f);

                        if (neighborIsWanderer)
                            influenceRadius = math.max(influenceRadius, hardClearanceDistance * 1.95f);

                        float relativeSpeed = math.length(selfVelocity - neighborVelocity);
                        float predictionSeconds = math.clamp(SeparationPredictionBaseSeconds + relativeSpeed * SeparationPredictionSpeedScale,
                                                             SeparationPredictionBaseSeconds,
                                                             SeparationPredictionMaxSeconds);

                        if (selfPriorityTier < neighborPriorityTier)
                            predictionSeconds = math.min(SeparationPredictionMaxSeconds, predictionSeconds * 1.3f);

                        // Predict future positions based on current velocity to create a more dynamic and anticipatory separation behavior.
                        float3 predictedSelfPosition = position + selfVelocity * predictionSeconds;
                        float3 predictedNeighborPosition = Positions[neighborIndex] + neighborVelocity * predictionSeconds;
                        float3 predictedDelta = predictedSelfPosition - predictedNeighborPosition;
                        predictedDelta.y = 0f;
                        float predictedDistanceSquared = math.lengthsq(predictedDelta);
                        bool usePredictedDelta = predictedDistanceSquared < sqrDistance;
                        float3 effectiveDelta = usePredictedDelta ? predictedDelta : delta;
                        float effectiveDistanceSquared = usePredictedDelta ? predictedDistanceSquared : sqrDistance;
                        float influenceRadiusSquared = influenceRadius * influenceRadius;

                        if (effectiveDistanceSquared > influenceRadiusSquared)
                            continue;

                        float distance = math.sqrt(math.max(effectiveDistanceSquared, 0f));
                        float3 direction;

                        if (distance > DirectionEpsilon)
                            direction = effectiveDelta / distance;
                        else
                            direction = ResolveDeterministicSeparationDirection(enemyIndex, neighborIndex);

                        float weight = 0f;

                        if (distance < hardClearanceDistance)
                        {
                            float penetration = hardClearanceDistance - distance;
                            weight = 1f + penetration / math.max(0.01f, hardClearanceDistance);
                        }
                        else
                        {
                            float softDenominator = math.max(0.01f, influenceRadius - hardClearanceDistance);
                            weight = (influenceRadius - distance) / softDenominator;
                        }

                        if (selfPriorityTier < neighborPriorityTier)
                        {
                            float3 toNeighborDirection = math.normalizesafe(Positions[neighborIndex] - position, float3.zero);
                            float closingSpeed = math.dot(selfVelocity - neighborVelocity, toNeighborDirection);
                            float closingFactor = 0f;

                            if (closingSpeed > 0f)
                            {
                                float speedNormalization = math.max(0.1f, selfSpeed + neighborSpeed);
                                closingFactor = math.saturate(closingSpeed / speedNormalization);
                                weight *= 1f + closingFactor * PriorityApproachUrgencyWeight;
                            }

                            float priorityGap = math.min(PriorityYieldGapNormalization, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
                            float priorityGapNormalized = math.saturate(priorityGap / PriorityYieldGapNormalization);
                            float yieldDistanceGate = math.max(hardClearanceDistance * 1.1f, influenceRadius * 0.92f);
                            float distanceUrgency = math.saturate((yieldDistanceGate - distance) / math.max(0.01f, yieldDistanceGate));
                            float yieldUrgency = math.saturate(distanceUrgency * 0.72f + closingFactor * 0.28f);
                            yieldUrgency *= 1f + priorityGapNormalized * 0.45f;

                            if (neighborIsWanderer)
                                yieldUrgency = math.max(yieldUrgency, math.saturate(distanceUrgency + 0.12f));

                            if (yieldUrgency > highestYieldUrgency)
                                highestYieldUrgency = yieldUrgency;

                            if (priorityGapNormalized > highestYieldPriorityGap)
                                highestYieldPriorityGap = priorityGapNormalized;
                        }

                        float priorityWeight = ResolvePriorityAvoidanceWeight(selfPriorityTier, neighborPriorityTier);

                        if (neighborIsWanderer)
                            priorityWeight *= 1.55f;

                        float sideStepWeight = math.saturate((influenceRadius - distance) / math.max(0.01f, influenceRadius));
                        sideStepWeight *= ResolveAggressivenessScale(selfSteeringAggressiveness, 0.35f, 1.1f);

                        if (selfPriorityTier < neighborPriorityTier)
                            sideStepWeight *= 1.25f;

                        // The lateral direction is determined by the relative movement of the two enemies,
                        // which encourages them to sidestep rather than just slow down when they are on a collision course.
                        // The deterministic fallback ensures consistent behavior when movement is minimal or perfectly aligned.
                        float3 lateralDirection = ResolveLateralAvoidanceDirection(direction,
                                                                                   selfVelocity,
                                                                                   neighborVelocity,
                                                                                   enemyIndex,
                                                                                   neighborIndex);

                        float3 avoidanceDirection = math.normalizesafe(direction + lateralDirection * sideStepWeight, direction);
                        separation += avoidanceDirection * math.max(0f, weight) * priorityWeight;

                        float urgencyDistanceGate = math.max(hardClearanceDistance, influenceRadius * 0.8f);
                        float urgency = math.saturate((urgencyDistanceGate - distance) / math.max(0.01f, urgencyDistanceGate));

                        if (neighborIsWanderer)
                            urgency = math.max(urgency, math.saturate((influenceRadius - distance) / math.max(0.01f, influenceRadius)));

                        if (selfPriorityTier < neighborPriorityTier)
                        {
                            float priorityGap = math.min(4f, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
                            urgency *= 1f + priorityGap * 0.35f;
                        }

                        float3 toNeighborDirectionForUrgency = math.normalizesafe(Positions[neighborIndex] - position, float3.zero);
                        float closingSpeedForUrgency = math.dot(selfVelocity - neighborVelocity, toNeighborDirectionForUrgency);

                        if (closingSpeedForUrgency > 0f)
                        {
                            float speedNormalization = math.max(0.1f, selfSpeed + neighborSpeed);
                            float closingFactor = math.saturate(closingSpeedForUrgency / speedNormalization);
                            urgency = math.max(urgency, math.saturate(urgency + closingFactor * (neighborIsWanderer ? 0.85f : 0.55f)));
                        }

                        // The aggressiveness settings of the enemies influence not only the strength of the separation response but also the urgency,
                        // allowing for more dynamic interactions where more aggressive enemies will react more strongly and urgently to potential collisions.
                        urgency *= ResolveAggressivenessScale(selfSteeringAggressiveness, 0.85f, 1.2f);

                        if (urgency > highestUrgency)
                            highestUrgency = urgency;
                    }
                    while (CellMap.TryGetNextValue(out neighborIndex, ref iterator));
                }
            }

            Results[index] = separation;
            UrgencyResults[index] = math.saturate(highestUrgency);
            YieldUrgencyResults[index] = math.saturate(highestYieldUrgency);
            YieldPriorityGapResults[index] = math.saturate(highestYieldPriorityGap);
        }

        private static float3 ResolveDeterministicSeparationDirection(int enemyIndex, int neighborIndex)
        {
            uint hash = math.hash(new int2(enemyIndex * 3 + 17, neighborIndex * 5 + 29));
            float angleRadians = (hash & 0x0000FFFFu) / 65535f * math.PI * 2f;
            return new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
        }

        private static float3 ResolveLateralAvoidanceDirection(float3 awayDirection,
                                                               float3 selfVelocity,
                                                               float3 neighborVelocity,
                                                               int enemyIndex,
                                                               int neighborIndex)
        {
            float3 lateral = new float3(-awayDirection.z, 0f, awayDirection.x);
            float lateralLengthSquared = lateral.x * lateral.x + lateral.z * lateral.z;

            if (lateralLengthSquared <= DirectionEpsilon)
                return ResolveDeterministicSeparationDirection(enemyIndex, neighborIndex);

            float inverseLateralLength = math.rsqrt(lateralLengthSquared);
            float3 normalizedLateral = lateral * inverseLateralLength;
            float3 relativeVelocity = selfVelocity - neighborVelocity;
            float3 normalizedRelativeVelocity = math.normalizesafe(new float3(relativeVelocity.x, 0f, relativeVelocity.z), float3.zero);
            float alignment = math.dot(normalizedRelativeVelocity, normalizedLateral);

            if (math.abs(alignment) > 0.12f)
                return alignment >= 0f ? normalizedLateral : -normalizedLateral;

            uint hash = math.hash(new int2(enemyIndex * 19 + 3, neighborIndex * 23 + 5));

            if ((hash & 1u) == 0u)
                return normalizedLateral;

            return -normalizedLateral;
        }

        private static float ResolveSteeringAggressiveness(float rawAggressiveness)
        {
            if (rawAggressiveness < 0f)
                return MinimumSteeringAggressiveness;

            return math.clamp(rawAggressiveness, MinimumSteeringAggressiveness, MaximumSteeringAggressiveness);
        }

        private static float ResolveAggressivenessScale(float aggressiveness, float minimumScale, float maximumScale)
        {
            float normalizedAggressiveness = math.saturate((aggressiveness - MinimumSteeringAggressiveness) /
                                                           math.max(0.0001f, MaximumSteeringAggressiveness - MinimumSteeringAggressiveness));
            return math.lerp(minimumScale, maximumScale, normalizedAggressiveness);
        }

        private static float ResolvePriorityClearanceMultiplier(int selfPriorityTier, int neighborPriorityTier)
        {
            if (selfPriorityTier < neighborPriorityTier)
            {
                float priorityGap = math.min(6f, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
                return 1.75f + priorityGap * 0.22f;
            }

            if (selfPriorityTier > neighborPriorityTier)
            {
                float priorityGap = math.min(6f, (float)math.max(1, selfPriorityTier - neighborPriorityTier));
                return math.max(0.5f, 0.94f - priorityGap * 0.07f);
            }

            return 1.08f;
        }

        private static float ResolvePriorityAvoidanceWeight(int selfPriorityTier, int neighborPriorityTier)
        {
            if (selfPriorityTier < neighborPriorityTier)
            {
                float priorityGap = math.min(6f, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
                return 3.4f + priorityGap * 1.1f;
            }

            if (selfPriorityTier > neighborPriorityTier)
            {
                float priorityGap = math.min(6f, (float)math.max(1, selfPriorityTier - neighborPriorityTier));
                return math.max(0.15f, 0.6f - priorityGap * 0.08f);
            }

            return 1.2f;
        }

    }
    #endregion

    #region Nested Types
    private enum SteeringLodLevel : byte
    {
        High = 0,
        Medium = 1,
        Low = 2
    }
    #endregion
}
