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
            .WithAll<EnemyData, EnemyRuntimeState, LocalTransform, EnemyActive>()
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
        EntityManager entityManager = state.EntityManager;
        int enemyCount = activeEnemiesQuery.CalculateEntityCount();

        if (enemyCount <= 0)
            return;

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

        if (entityManager.Exists(playerEntity) == false)
            return;
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        if (enemyTimeScale <= 0f)
            return;

        NativeArray<Entity> enemyEntities = activeEnemiesQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<LocalTransform> enemyTransforms = activeEnemiesQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        NativeArray<EnemyData> enemyDataArray = activeEnemiesQuery.ToComponentDataArray<EnemyData>(Allocator.TempJob);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = activeEnemiesQuery.ToComponentDataArray<EnemyRuntimeState>(Allocator.TempJob);

        NativeArray<float3> positions = new NativeArray<float3>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float2> speedData = new NativeArray<float2>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> contactRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> bodyRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> priorityTiers = new NativeArray<int>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float3> planarVelocities = new NativeArray<float3>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> separationRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int2> cellCoordinates = new NativeArray<int2>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> enemyToEvaluatedIndex = new NativeArray<int>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeList<int> evaluatedEnemyIndices = new NativeList<int>(enemyCount, Allocator.TempJob);
        NativeList<byte> evaluatedSeparationEnabled = new NativeList<byte>(enemyCount, Allocator.TempJob);
        ComponentLookup<EnemyElementalRuntimeState> elementalRuntimeLookup = SystemAPI.GetComponentLookup<EnemyElementalRuntimeState>(true);
        ComponentLookup<EnemyShooterControlState> shooterControlLookup = SystemAPI.GetComponentLookup<EnemyShooterControlState>(true);
        ComponentLookup<EnemyCustomPatternMovementTag> customPatternMovementTagLookup = SystemAPI.GetComponentLookup<EnemyCustomPatternMovementTag>(true);

        float maxSeparationRadius = 0.25f;
        float maxBodyRadius = 0.05f;
        int frameCount = Time.frameCount;

        for (int index = 0; index < enemyCount; index++)
        {
            EnemyData enemyData = enemyDataArray[index];
            LocalTransform enemyTransform = enemyTransforms[index];

            positions[index] = enemyTransform.Position;
            Entity enemyEntity = enemyEntities[index];
            float elementalSlowPercent = 0f;

            if (elementalRuntimeLookup.HasComponent(enemyEntity))
                elementalSlowPercent = math.clamp(elementalRuntimeLookup[enemyEntity].SlowPercent, 0f, 100f);

            float slowMultiplier = math.saturate(1f - elementalSlowPercent * 0.01f);
            speedData[index] = new float2(math.max(0f, enemyData.MoveSpeed) * slowMultiplier, math.max(0f, enemyData.MaxSpeed) * slowMultiplier);
            contactRadii[index] = math.max(0f, enemyData.ContactRadius);
            bodyRadii[index] = math.max(0.05f, enemyData.BodyRadius);
            priorityTiers[index] = math.clamp(enemyData.PriorityTier, -128, 128);
            float3 planarVelocity = enemyRuntimeArray[index].Velocity;
            planarVelocity.y = 0f;
            planarVelocities[index] = planarVelocity;

            if (bodyRadii[index] > maxBodyRadius)
                maxBodyRadius = bodyRadii[index];

            float separationRadius = math.max(0.05f, enemyData.SeparationRadius);
            separationRadii[index] = separationRadius;

            if (separationRadius > maxSeparationRadius)
                maxSeparationRadius = separationRadius;

            enemyToEvaluatedIndex[index] = -1;

            SteeringLodLevel lodLevel = EvaluateLod(playerPosition, enemyTransform.Position);
            bool shouldEvaluate = ShouldEvaluateLod(lodLevel, frameCount, enemyEntity.Index);
            bool hasCustomPatternMovement = customPatternMovementTagLookup.HasComponent(enemyEntity);

            if (shouldEvaluate && hasCustomPatternMovement == false)
            {
                int evaluatedIndex = evaluatedEnemyIndices.Length;
                enemyToEvaluatedIndex[index] = evaluatedIndex;
                evaluatedEnemyIndices.Add(index);

                byte separationEnabled = 1;
                evaluatedSeparationEnabled.Add(separationEnabled);
            }
        }

        float maxHardClearanceRadius = (maxBodyRadius * 2f + SeparationClearancePadding) * 2.75f;
        float cellSize = math.max(0.25f, math.max(maxSeparationRadius, maxHardClearanceRadius));
        float inverseCellSize = 1f / cellSize;
        NativeParallelMultiHashMap<int, int> cellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.TempJob);

        for (int index = 0; index < enemyCount; index++)
        {
            float3 position = positions[index];
            int cellX = (int)math.floor(position.x * inverseCellSize);
            int cellY = (int)math.floor(position.z * inverseCellSize);
            int2 cell = new int2(cellX, cellY);
            cellCoordinates[index] = cell;
            cellMap.Add(EncodeCell(cellX, cellY), index);
        }

        int evaluatedCount = evaluatedEnemyIndices.Length;
        NativeArray<float3> approachResults = new NativeArray<float3>(evaluatedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float3> separationResults = new NativeArray<float3>(evaluatedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        if (evaluatedCount > 0)
        {
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
                SeparationEnabled = evaluatedSeparationEnabled.AsArray(),
                Positions = positions,
                BodyRadii = bodyRadii,
                PriorityTiers = priorityTiers,
                Velocities = planarVelocities,
                SeparationRadii = separationRadii,
                CellCoordinates = cellCoordinates,
                CellMap = cellMap,
                Results = separationResults
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

            if (entityManager.Exists(enemyEntity) == false)
                continue;

            if (customPatternMovementTagLookup.HasComponent(enemyEntity))
                continue;

            EnemyData enemyData = enemyDataArray[enemyIndex];
            EnemyRuntimeState runtimeState = enemyRuntimeArray[enemyIndex];
            LocalTransform enemyTransform = enemyTransforms[enemyIndex];

            int evaluatedIndex = enemyToEvaluatedIndex[enemyIndex];

            if (evaluatedIndex >= 0)
            {
                float separationWeight = math.max(0f, enemyData.SeparationWeight);
                float3 desiredVelocity = approachResults[evaluatedIndex];

                if (evaluatedSeparationEnabled[evaluatedIndex] != 0)
                    desiredVelocity += separationResults[evaluatedIndex] * separationWeight;

                float maxSpeed = math.max(0f, speedData[enemyIndex].y);
                float desiredSpeed = math.length(desiredVelocity);

                if (maxSpeed > 0f && desiredSpeed > maxSpeed && desiredSpeed > DirectionEpsilon)
                    desiredVelocity *= maxSpeed / desiredSpeed;

                float acceleration = math.max(0f, enemyData.Acceleration);
                float deceleration = math.max(0f, enemyData.Deceleration);
                float accelerationRate = ResolveVelocityChangeRate(runtimeState.Velocity,
                                                                   desiredVelocity,
                                                                   acceleration,
                                                                   deceleration);
                float maxVelocityDelta = accelerationRate * deltaTime;
                float3 velocityDelta = desiredVelocity - runtimeState.Velocity;
                float velocityDeltaMagnitude = math.length(velocityDelta);

                if (velocityDeltaMagnitude > maxVelocityDelta && velocityDeltaMagnitude > DirectionEpsilon)
                    runtimeState.Velocity += velocityDelta * (maxVelocityDelta / velocityDeltaMagnitude);
                else
                    runtimeState.Velocity = desiredVelocity;
            }

            float velocityMagnitude = math.length(runtimeState.Velocity);
            float velocityMaxSpeed = math.max(0f, speedData[enemyIndex].y);

            if (velocityMaxSpeed > 0f && velocityMagnitude > velocityMaxSpeed && velocityMagnitude > DirectionEpsilon)
                runtimeState.Velocity *= velocityMaxSpeed / velocityMagnitude;

            if (shooterControlLookup.HasComponent(enemyEntity) && shooterControlLookup[enemyEntity].MovementLocked != 0)
                runtimeState.Velocity = float3.zero;

            float3 desiredDisplacement = runtimeState.Velocity * deltaTime;
            float3 position = enemyTransform.Position;

            if (wallsEnabled)
            {
                float collisionRadius = math.max(0.01f, enemyData.BodyRadius);
                bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                       position,
                                                                                       desiredDisplacement,
                                                                                       collisionRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 allowedDisplacement,
                                                                                       out float3 hitNormal);

                position += allowedDisplacement;

                if (hitWall)
                    runtimeState.Velocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(runtimeState.Velocity, hitNormal);
            }
            else
            {
                position += desiredDisplacement;
            }

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
                float3 forward = math.normalizesafe(new float3(runtimeState.Velocity.x, 0f, runtimeState.Velocity.z), ForwardAxis);
                enemyTransform.Rotation = quaternion.LookRotationSafe(forward, UpAxis);
            }

            entityManager.SetComponentData(enemyEntity, runtimeState);
            entityManager.SetComponentData(enemyEntity, enemyTransform);
        }

        enemyEntities.Dispose();
        enemyTransforms.Dispose();
        enemyDataArray.Dispose();
        enemyRuntimeArray.Dispose();
        positions.Dispose();
        speedData.Dispose();
        contactRadii.Dispose();
        bodyRadii.Dispose();
        priorityTiers.Dispose();
        planarVelocities.Dispose();
        separationRadii.Dispose();
        cellCoordinates.Dispose();
        enemyToEvaluatedIndex.Dispose();
        evaluatedEnemyIndices.Dispose();
        evaluatedSeparationEnabled.Dispose();
        approachResults.Dispose();
        separationResults.Dispose();
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

    private static int EncodeCell(int x, int y)
    {
        unchecked
        {
            return (x * 73856093) ^ (y * 19349663);
        }
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
        [ReadOnly] public NativeArray<byte> SeparationEnabled;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float> BodyRadii;
        [ReadOnly] public NativeArray<int> PriorityTiers;
        [ReadOnly] public NativeArray<float3> Velocities;
        [ReadOnly] public NativeArray<float> SeparationRadii;
        [ReadOnly] public NativeArray<int2> CellCoordinates;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> CellMap;
        public NativeArray<float3> Results;

        public void Execute(int index)
        {
            if (SeparationEnabled[index] == 0)
            {
                Results[index] = float3.zero;
                return;
            }

            int enemyIndex = EvaluatedEnemyIndices[index];
            float separationRadius = math.max(0.01f, SeparationRadii[enemyIndex]);
            float bodyRadius = math.max(0.01f, BodyRadii[enemyIndex]);
            int selfPriorityTier = PriorityTiers[enemyIndex];
            float3 position = Positions[enemyIndex];
            float3 selfVelocity = Velocities[enemyIndex];
            float selfSpeed = math.length(selfVelocity);
            int2 cell = CellCoordinates[enemyIndex];
            float3 separation = float3.zero;

            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int key = EncodeCell(cell.x + offsetX, cell.y + offsetY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int neighborIndex;

                    if (CellMap.TryGetFirstValue(key, out neighborIndex, out iterator) == false)
                        continue;

                    do
                    {
                        if (neighborIndex == enemyIndex)
                            continue;

                        float3 delta = position - Positions[neighborIndex];
                        delta.y = 0f;
                        float sqrDistance = math.lengthsq(delta);
                        float3 neighborVelocity = Velocities[neighborIndex];
                        float neighborSpeed = math.length(neighborVelocity);
                        float neighborBodyRadius = math.max(0.01f, BodyRadii[neighborIndex]);
                        int neighborPriorityTier = PriorityTiers[neighborIndex];
                        float priorityClearanceMultiplier = ResolvePriorityClearanceMultiplier(selfPriorityTier, neighborPriorityTier);
                        float hardClearanceDistance = (bodyRadius + neighborBodyRadius + SeparationClearancePadding) * priorityClearanceMultiplier;
                        float influenceRadius = math.max(separationRadius, hardClearanceDistance * 1.35f);

                        if (selfPriorityTier < neighborPriorityTier)
                            influenceRadius = math.max(influenceRadius, hardClearanceDistance * 1.65f);

                        float relativeSpeed = math.length(selfVelocity - neighborVelocity);
                        float predictionSeconds = math.clamp(SeparationPredictionBaseSeconds + relativeSpeed * SeparationPredictionSpeedScale,
                                                             SeparationPredictionBaseSeconds,
                                                             SeparationPredictionMaxSeconds);

                        if (selfPriorityTier < neighborPriorityTier)
                            predictionSeconds = math.min(SeparationPredictionMaxSeconds, predictionSeconds * 1.3f);

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

                            if (closingSpeed > 0f)
                            {
                                float speedNormalization = math.max(0.1f, selfSpeed + neighborSpeed);
                                float closingFactor = math.saturate(closingSpeed / speedNormalization);
                                weight *= 1f + closingFactor * PriorityApproachUrgencyWeight;
                            }
                        }

                        float priorityWeight = ResolvePriorityAvoidanceWeight(selfPriorityTier, neighborPriorityTier);
                        separation += direction * math.max(0f, weight) * priorityWeight;
                    }
                    while (CellMap.TryGetNextValue(out neighborIndex, ref iterator));
                }
            }

            Results[index] = separation;
        }

        private static float3 ResolveDeterministicSeparationDirection(int enemyIndex, int neighborIndex)
        {
            uint hash = math.hash(new int2(enemyIndex * 3 + 17, neighborIndex * 5 + 29));
            float angleRadians = (hash & 0x0000FFFFu) / 65535f * math.PI * 2f;
            return new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
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

            return 1f;
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

            return 1f;
        }

        private static int EncodeCell(int x, int y)
        {
            unchecked
            {
                return (x * 73856093) ^ (y * 19349663);
            }
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
