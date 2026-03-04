using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Provides Wanderer Basic destination selection and occupancy-aware free-trajectory scoring.
/// </summary>
public static class EnemyPatternWandererUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Nested Types
    /// <summary>
    /// Immutable occupancy context used while scoring Wanderer candidates.
    /// </summary>
    public readonly struct OccupancyContext
    {
        public readonly NativeArray<Entity> Entities;
        public readonly NativeArray<float3> Positions;
        public readonly NativeArray<float3> Velocities;
        public readonly NativeArray<float> Radii;
        public readonly NativeParallelMultiHashMap<int, int> CellMap;
        public readonly float InverseCellSize;
        public readonly float MaxRadius;

        public OccupancyContext(NativeArray<Entity> entities,
                                NativeArray<float3> positions,
                                NativeArray<float3> velocities,
                                NativeArray<float> radii,
                                NativeParallelMultiHashMap<int, int> cellMap,
                                float inverseCellSize,
                                float maxRadius)
        {
            Entities = entities;
            Positions = positions;
            Velocities = velocities;
            Radii = radii;
            CellMap = cellMap;
            InverseCellSize = inverseCellSize;
            MaxRadius = maxRadius;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves Wanderer Basic desired velocity and updates runtime target/wait/retry state.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="enemyData">Enemy immutable data.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="patternRuntimeState">Pattern runtime state to mutate.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="moveSpeed">Resolved movement speed.</param>
    /// <param name="maxSpeed">Resolved max speed.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="deltaTime">Current simulation delta time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether walls collisions are enabled.</param>
    /// <param name="occupancyContext">Occupancy context used for free-trajectory scoring.</param>
    /// <returns>Desired planar velocity for this frame.</returns>
    public static float3 ResolveWandererBasicVelocity(Entity enemyEntity,
                                                      in EnemyData enemyData,
                                                      in EnemyPatternConfig patternConfig,
                                                      ref EnemyPatternRuntimeState patternRuntimeState,
                                                      float3 enemyPosition,
                                                      float3 playerPosition,
                                                      float moveSpeed,
                                                      float maxSpeed,
                                                      float elapsedTime,
                                                      float deltaTime,
                                                      in PhysicsWorldSingleton physicsWorldSingleton,
                                                      int wallsLayerMask,
                                                      bool wallsEnabled,
                                                      in OccupancyContext occupancyContext)
    {
        if (patternRuntimeState.WanderWaitTimer > 0f)
        {
            patternRuntimeState.WanderWaitTimer = math.max(0f, patternRuntimeState.WanderWaitTimer - deltaTime);
            return float3.zero;
        }

        if (patternRuntimeState.WanderRetryTimer > 0f)
            patternRuntimeState.WanderRetryTimer = math.max(0f, patternRuntimeState.WanderRetryTimer - deltaTime);

        if (patternRuntimeState.WanderHasTarget != 0)
        {
            float3 toTarget = patternRuntimeState.WanderTargetPosition - enemyPosition;
            toTarget.y = 0f;
            float distanceToTarget = math.length(toTarget);

            if (distanceToTarget <= math.max(0.05f, patternConfig.BasicArrivalTolerance))
            {
                patternRuntimeState.WanderHasTarget = 0;
                patternRuntimeState.WanderWaitTimer = math.max(0f, patternConfig.BasicWaitCooldownSeconds);
                return float3.zero;
            }

            float3 targetDirection = toTarget / math.max(distanceToTarget, DirectionEpsilon);
            float desiredSpeed = maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed;
            return targetDirection * math.max(0f, desiredSpeed);
        }

        if (patternRuntimeState.WanderRetryTimer > 0f)
            return float3.zero;

        bool pickedDestination = TryPickWanderDestination(enemyEntity,
                                                          enemyData.BodyRadius,
                                                          in patternConfig,
                                                          enemyPosition,
                                                          playerPosition,
                                                          patternRuntimeState.LastWanderDirectionAngle,
                                                          patternRuntimeState.WanderInitialized != 0,
                                                          elapsedTime,
                                                          in physicsWorldSingleton,
                                                          wallsLayerMask,
                                                          wallsEnabled,
                                                          in occupancyContext,
                                                          out float3 selectedTarget,
                                                          out float selectedDirectionAngle);

        if (pickedDestination)
        {
            patternRuntimeState.WanderTargetPosition = selectedTarget;
            patternRuntimeState.LastWanderDirectionAngle = selectedDirectionAngle;
            patternRuntimeState.WanderInitialized = 1;
            patternRuntimeState.WanderHasTarget = 1;
            return float3.zero;
        }

        patternRuntimeState.WanderRetryTimer = math.max(0f, patternConfig.BasicBlockedPathRetryDelay);
        return float3.zero;
    }

    /// <summary>
    /// Encodes integer grid coordinates into a stable hash-map key.
    /// </summary>
    /// <param name="x">Grid X coordinate.</param>
    /// <param name="y">Grid Y coordinate.</param>
    /// <returns>Encoded integer key.</returns>
    public static int EncodeCell(int x, int y)
    {
        unchecked
        {
            return (x * 73856093) ^ (y * 19349663);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Picks the best Wanderer destination by prioritizing collision-safe trajectories and then designer biases.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="bodyRadius">Current enemy body radius.</param>
    /// <param name="patternConfig">Compiled pattern config.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="playerPosition">Current player position.</param>
    /// <param name="lastDirectionAngle">Previously selected direction angle in degrees.</param>
    /// <param name="hasPreviousDirection">Whether a previous direction exists.</param>
    /// <param name="elapsedTime">Elapsed world time.</param>
    /// <param name="physicsWorldSingleton">Physics world singleton.</param>
    /// <param name="wallsLayerMask">Walls layer mask.</param>
    /// <param name="wallsEnabled">Whether walls collisions are enabled.</param>
    /// <param name="occupancyContext">Occupancy context used for free-trajectory scoring.</param>
    /// <param name="selectedTarget">Selected target output.</param>
    /// <param name="selectedDirectionAngle">Selected direction angle output.</param>
    /// <returns>True when a valid destination is found.</returns>
    private static bool TryPickWanderDestination(Entity enemyEntity,
                                                 float bodyRadius,
                                                 in EnemyPatternConfig patternConfig,
                                                 float3 enemyPosition,
                                                 float3 playerPosition,
                                                 float lastDirectionAngle,
                                                 bool hasPreviousDirection,
                                                 float elapsedTime,
                                                 in PhysicsWorldSingleton physicsWorldSingleton,
                                                 int wallsLayerMask,
                                                 bool wallsEnabled,
                                                 in OccupancyContext occupancyContext,
                                                 out float3 selectedTarget,
                                                 out float selectedDirectionAngle)
    {
        selectedTarget = enemyPosition;
        selectedDirectionAngle = 0f;

        float searchRadius = math.max(0.5f, patternConfig.BasicSearchRadius);
        float minimumDistance = math.max(0f, patternConfig.BasicMinimumTravelDistance);
        float maximumDistance = math.max(minimumDistance, patternConfig.BasicMaximumTravelDistance);
        float unexploredPreference = math.max(0f, patternConfig.BasicUnexploredDirectionPreference);
        float towardPlayerPreference = math.max(0f, patternConfig.BasicTowardPlayerPreference);
        float minimumWallDistance = math.max(0f, patternConfig.BasicMinimumWallDistance);
        float minimumEnemyClearance = math.max(0f, patternConfig.BasicMinimumEnemyClearance);
        float trajectoryPredictionTime = math.max(0f, patternConfig.BasicTrajectoryPredictionTime);
        float freeTrajectoryPreference = math.max(0f, patternConfig.BasicFreeTrajectoryPreference);
        bool useInfiniteDirectionSampling = patternConfig.BasicUseInfiniteDirectionSampling != 0;
        int sampleCount = math.max(1, patternConfig.BasicCandidateSampleCount);
        float directionStepDegrees = math.clamp(patternConfig.BasicInfiniteDirectionStepDegrees, 0.5f, 90f);

        if (useInfiniteDirectionSampling)
            sampleCount = math.max(8, (int)math.ceil(360f / directionStepDegrees));

        float phaseAngleRadians = 0f;

        if (useInfiniteDirectionSampling)
        {
            uint phaseSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, (int)(elapsedTime * 11f), 191));
            phaseAngleRadians = ResolveHash01(phaseSeed) * math.PI * 2f;
        }

        float stepRadians = math.radians(directionStepDegrees);
        float collisionRadius = math.max(0.01f, bodyRadius + minimumWallDistance);
        float bestScore = float.NegativeInfinity;
        bool foundCandidate = false;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float angleRadians;

            if (useInfiniteDirectionSampling)
                angleRadians = phaseAngleRadians + stepRadians * sampleIndex;
            else
            {
                uint directionSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, sampleIndex, (int)(elapsedTime * 17f)));
                angleRadians = ResolveHash01(directionSeed) * math.PI * 2f;
            }

            float3 direction = new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
            uint distanceSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, sampleIndex, (int)(elapsedTime * 37f)));
            float distance01 = ResolveHash01(distanceSeed);
            float targetDistance = math.lerp(minimumDistance, maximumDistance, distance01);
            targetDistance = math.min(targetDistance, searchRadius);

            if (targetDistance <= 0.01f)
                continue;

            float3 candidate = enemyPosition + direction * targetDistance;
            float3 candidateDisplacement = candidate - enemyPosition;

            if (wallsEnabled)
            {
                bool blocked = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                       enemyPosition,
                                                                                       candidateDisplacement,
                                                                                       collisionRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 allowedDisplacement,
                                                                                       out float3 _);

                if (blocked)
                {
                    float requestedDistanceSquared = math.lengthsq(candidateDisplacement);
                    float allowedDistanceSquared = math.lengthsq(allowedDisplacement);

                    if (requestedDistanceSquared <= DirectionEpsilon || allowedDistanceSquared < requestedDistanceSquared * 0.96f)
                        continue;
                }
            }

            bool freeTrajectory = TryEvaluateTrajectoryFreedom(enemyEntity,
                                                               enemyPosition,
                                                               candidate,
                                                               math.max(0.01f, bodyRadius),
                                                               minimumEnemyClearance,
                                                               trajectoryPredictionTime,
                                                               in occupancyContext,
                                                               out float freeTrajectoryScore,
                                                               out float freeSpaceScore);

            if (freeTrajectory == false)
                continue;

            float3 toPlayer = playerPosition - candidate;
            toPlayer.y = 0f;
            float playerDistance = math.length(toPlayer);
            float playerDistanceScore = 1f / (1f + playerDistance);
            float angleDegrees = math.degrees(math.atan2(direction.x, direction.z));
            float unexploredScore = 1f;

            if (hasPreviousDirection)
            {
                float deltaDegrees = math.abs(math.degrees(math.atan2(math.sin(math.radians(angleDegrees - lastDirectionAngle)),
                                                                      math.cos(math.radians(angleDegrees - lastDirectionAngle)))));
                unexploredScore = deltaDegrees / 180f;
            }

            float safetyScore = math.saturate(freeTrajectoryScore * 0.7f + freeSpaceScore * 0.3f);
            float safetyWeight = math.max(1f, freeTrajectoryPreference * 10f);
            float score = safetyWeight * safetyScore +
                          unexploredPreference * unexploredScore +
                          towardPlayerPreference * playerDistanceScore;

            if (score <= bestScore)
                continue;

            bestScore = score;
            selectedTarget = candidate;
            selectedDirectionAngle = angleDegrees;
            foundCandidate = true;
        }

        return foundCandidate;
    }

    /// <summary>
    /// Evaluates whether a candidate and its segment are clear enough from neighboring enemies.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="origin">Current enemy origin.</param>
    /// <param name="candidate">Candidate destination.</param>
    /// <param name="bodyRadius">Current enemy body radius.</param>
    /// <param name="minimumEnemyClearance">Extra clearance from neighbors.</param>
    /// <param name="trajectoryPredictionTime">Prediction time for neighbor movement.</param>
    /// <param name="occupancyContext">Occupancy context used for neighbor lookup.</param>
    /// <param name="freeTrajectoryScore">Output free-trajectory score.</param>
    /// <param name="freeSpaceScore">Output free-space score.</param>
    /// <returns>True when candidate and path are valid.</returns>
    private static bool TryEvaluateTrajectoryFreedom(Entity enemyEntity,
                                                     float3 origin,
                                                     float3 candidate,
                                                     float bodyRadius,
                                                     float minimumEnemyClearance,
                                                     float trajectoryPredictionTime,
                                                     in OccupancyContext occupancyContext,
                                                     out float freeTrajectoryScore,
                                                     out float freeSpaceScore)
    {
        freeTrajectoryScore = 1f;
        freeSpaceScore = 1f;

        int occupancyCount = occupancyContext.Entities.Length;

        if (occupancyCount <= 1)
            return true;

        float searchPadding = math.max(0.1f, bodyRadius + minimumEnemyClearance + occupancyContext.MaxRadius);
        float minX = math.min(origin.x, candidate.x) - searchPadding;
        float maxX = math.max(origin.x, candidate.x) + searchPadding;
        float minY = math.min(origin.z, candidate.z) - searchPadding;
        float maxY = math.max(origin.z, candidate.z) + searchPadding;
        int minCellX = (int)math.floor(minX * occupancyContext.InverseCellSize);
        int maxCellX = (int)math.floor(maxX * occupancyContext.InverseCellSize);
        int minCellY = (int)math.floor(minY * occupancyContext.InverseCellSize);
        int maxCellY = (int)math.floor(maxY * occupancyContext.InverseCellSize);
        float minimumCandidateClearance = float.MaxValue;
        float minimumTrajectoryClearance = float.MaxValue;
        float summedTrajectoryClearance = 0f;
        int neighborCount = 0;

        for (int cellX = minCellX; cellX <= maxCellX; cellX++)
        {
            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                int cellKey = EncodeCell(cellX, cellY);
                NativeParallelMultiHashMapIterator<int> iterator;
                int occupancyIndex;

                if (occupancyContext.CellMap.TryGetFirstValue(cellKey, out occupancyIndex, out iterator) == false)
                    continue;

                do
                {
                    Entity otherEntity = occupancyContext.Entities[occupancyIndex];

                    if (otherEntity == enemyEntity)
                        continue;

                    float3 otherPosition = occupancyContext.Positions[occupancyIndex];
                    float3 otherVelocity = occupancyContext.Velocities[occupancyIndex];
                    float3 predictedOtherPosition = otherPosition + otherVelocity * trajectoryPredictionTime;
                    predictedOtherPosition.y = 0f;

                    float otherRadius = math.max(0.05f, occupancyContext.Radii[occupancyIndex]);
                    float requiredClearance = math.max(0.01f, bodyRadius + otherRadius + minimumEnemyClearance);

                    float3 deltaToCandidate = candidate - predictedOtherPosition;
                    deltaToCandidate.y = 0f;
                    float candidateDistance = math.length(deltaToCandidate);
                    float trajectoryDistance = math.sqrt(DistancePointToSegmentSquaredXZ(predictedOtherPosition, origin, candidate));

                    if (candidateDistance < requiredClearance || trajectoryDistance < requiredClearance)
                        return false;

                    float candidateClearance = candidateDistance - requiredClearance;
                    float trajectoryClearance = trajectoryDistance - requiredClearance;

                    if (candidateClearance < minimumCandidateClearance)
                        minimumCandidateClearance = candidateClearance;

                    if (trajectoryClearance < minimumTrajectoryClearance)
                        minimumTrajectoryClearance = trajectoryClearance;

                    summedTrajectoryClearance += trajectoryClearance;
                    neighborCount++;
                }
                while (occupancyContext.CellMap.TryGetNextValue(out occupancyIndex, ref iterator));
            }
        }

        if (neighborCount <= 0)
            return true;

        float normalization = math.max(0.1f, bodyRadius + minimumEnemyClearance + occupancyContext.MaxRadius);
        float averageTrajectoryClearance = summedTrajectoryClearance / neighborCount;
        float minimumTrajectoryScore = math.saturate(minimumTrajectoryClearance / normalization);
        float averageTrajectoryScore = math.saturate(averageTrajectoryClearance / (normalization * 1.5f));
        freeSpaceScore = math.saturate(minimumCandidateClearance / normalization);
        freeTrajectoryScore = math.saturate(minimumTrajectoryScore * 0.8f + averageTrajectoryScore * 0.2f);
        return true;
    }

    /// <summary>
    /// Computes squared planar distance between a point and a segment on the XZ plane.
    /// </summary>
    /// <param name="point">Point to project.</param>
    /// <param name="segmentStart">Segment start.</param>
    /// <param name="segmentEnd">Segment end.</param>
    /// <returns>Squared planar distance from point to segment.</returns>
    private static float DistancePointToSegmentSquaredXZ(float3 point, float3 segmentStart, float3 segmentEnd)
    {
        float2 point2 = new float2(point.x, point.z);
        float2 segmentStart2 = new float2(segmentStart.x, segmentStart.z);
        float2 segmentEnd2 = new float2(segmentEnd.x, segmentEnd.z);
        float2 segmentVector = segmentEnd2 - segmentStart2;
        float segmentLengthSquared = math.lengthsq(segmentVector);

        if (segmentLengthSquared <= DirectionEpsilon)
            return math.lengthsq(point2 - segmentStart2);

        float projection = math.dot(point2 - segmentStart2, segmentVector) / segmentLengthSquared;
        projection = math.saturate(projection);
        float2 projectedPoint = segmentStart2 + segmentVector * projection;
        return math.lengthsq(point2 - projectedPoint);
    }

    /// <summary>
    /// Converts hash bits to normalized range [0..1].
    /// </summary>
    /// <param name="hashValue">Input hash value.</param>
    /// <returns>Normalized value in [0..1].</returns>
    private static float ResolveHash01(uint hashValue)
    {
        return (hashValue & 0x00FFFFFFu) / 16777215f;
    }
    #endregion

    #endregion
}
