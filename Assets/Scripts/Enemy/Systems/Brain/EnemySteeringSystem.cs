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

        NativeArray<Entity> playerEntities = playerQuery.ToEntityArray(Allocator.Temp);

        if (playerEntities.Length == 0)
        {
            playerEntities.Dispose();
            return;
        }

        Entity playerEntity = playerEntities[0];
        playerEntities.Dispose();

        if (entityManager.Exists(playerEntity) == false)
            return;

        float3 playerPosition = entityManager.GetComponentData<LocalTransform>(playerEntity).Position;

        NativeArray<Entity> enemyEntities = activeEnemiesQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<LocalTransform> enemyTransforms = activeEnemiesQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        NativeArray<EnemyData> enemyDataArray = activeEnemiesQuery.ToComponentDataArray<EnemyData>(Allocator.TempJob);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = activeEnemiesQuery.ToComponentDataArray<EnemyRuntimeState>(Allocator.TempJob);

        NativeArray<float3> positions = new NativeArray<float3>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float2> speedData = new NativeArray<float2>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> contactRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> separationRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int2> cellCoordinates = new NativeArray<int2>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int> enemyToEvaluatedIndex = new NativeArray<int>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeList<int> evaluatedEnemyIndices = new NativeList<int>(enemyCount, Allocator.TempJob);
        NativeList<byte> evaluatedSeparationEnabled = new NativeList<byte>(enemyCount, Allocator.TempJob);

        float maxSeparationRadius = 0.25f;
        int frameCount = Time.frameCount;

        for (int index = 0; index < enemyCount; index++)
        {
            EnemyData enemyData = enemyDataArray[index];
            LocalTransform enemyTransform = enemyTransforms[index];

            positions[index] = enemyTransform.Position;
            speedData[index] = new float2(math.max(0f, enemyData.MoveSpeed), math.max(0f, enemyData.MaxSpeed));
            contactRadii[index] = math.max(0f, enemyData.ContactRadius);

            float separationRadius = math.max(0.05f, enemyData.SeparationRadius);
            separationRadii[index] = separationRadius;

            if (separationRadius > maxSeparationRadius)
                maxSeparationRadius = separationRadius;

            enemyToEvaluatedIndex[index] = -1;

            Entity enemyEntity = enemyEntities[index];
            SteeringLodLevel lodLevel = EvaluateLod(playerPosition, enemyTransform.Position);
            bool shouldEvaluate = ShouldEvaluateLod(lodLevel, frameCount, enemyEntity.Index);

            if (shouldEvaluate)
            {
                int evaluatedIndex = evaluatedEnemyIndices.Length;
                enemyToEvaluatedIndex[index] = evaluatedIndex;
                evaluatedEnemyIndices.Add(index);

                byte separationEnabled = lodLevel == SteeringLodLevel.Low ? (byte)0 : (byte)1;
                evaluatedSeparationEnabled.Add(separationEnabled);
            }
        }

        float cellSize = math.max(0.25f, maxSeparationRadius);
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

        float deltaTime = SystemAPI.Time.DeltaTime;
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

                float maxSpeed = math.max(0f, enemyData.MaxSpeed);
                float desiredSpeed = math.length(desiredVelocity);

                if (maxSpeed > 0f && desiredSpeed > maxSpeed && desiredSpeed > 1e-6f)
                    desiredVelocity *= maxSpeed / desiredSpeed;

                float acceleration = math.max(0f, enemyData.Acceleration);
                float maxVelocityDelta = acceleration * deltaTime;
                float3 velocityDelta = desiredVelocity - runtimeState.Velocity;
                float velocityDeltaMagnitude = math.length(velocityDelta);

                if (velocityDeltaMagnitude > maxVelocityDelta && velocityDeltaMagnitude > 1e-6f)
                    runtimeState.Velocity += velocityDelta * (maxVelocityDelta / velocityDeltaMagnitude);
                else
                    runtimeState.Velocity = desiredVelocity;
            }

            float velocityMagnitude = math.length(runtimeState.Velocity);
            float velocityMaxSpeed = math.max(0f, enemyData.MaxSpeed);

            if (velocityMaxSpeed > 0f && velocityMagnitude > velocityMaxSpeed && velocityMagnitude > 1e-6f)
                runtimeState.Velocity *= velocityMaxSpeed / velocityMagnitude;

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

            if (planarVelocitySquared > 1e-6f)
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
            float radius = math.max(0.01f, SeparationRadii[enemyIndex]);
            float radiusSquared = radius * radius;
            float3 position = Positions[enemyIndex];
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

                        if (sqrDistance <= 1e-6f)
                            continue;

                        if (sqrDistance > radiusSquared)
                            continue;

                        float inverseDistance = math.rsqrt(sqrDistance);
                        separation += delta * inverseDistance;
                    }
                    while (CellMap.TryGetNextValue(out neighborIndex, ref iterator));
                }
            }

            Results[index] = separation;
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
