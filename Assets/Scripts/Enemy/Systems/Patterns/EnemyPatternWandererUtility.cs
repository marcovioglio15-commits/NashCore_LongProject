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
    private const float ClearancePredictionMinimumSeconds = 0.1f;
    private const float RightOfWayTieSeconds = 0.02f;
    private const float SoftClearanceMultiplier = 1.35f;
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
        public readonly NativeArray<int> PriorityTiers;
        public readonly NativeParallelMultiHashMap<int, int> CellMap;
        public readonly float InverseCellSize;
        public readonly float MaxRadius;

        public OccupancyContext(NativeArray<Entity> entities,
                                NativeArray<float3> positions,
                                NativeArray<float3> velocities,
                                NativeArray<float> radii,
                                NativeArray<int> priorityTiers,
                                NativeParallelMultiHashMap<int, int> cellMap,
                                float inverseCellSize,
                                float maxRadius)
        {
            Entities = entities;
            Positions = positions;
            Velocities = velocities;
            Radii = radii;
            PriorityTiers = priorityTiers;
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
            float3 desiredVelocity = targetDirection * math.max(0f, desiredSpeed);
            float bodyRadius = math.max(0.05f, enemyData.BodyRadius);
            float minimumEnemyClearance = math.max(0f, patternConfig.BasicMinimumEnemyClearance);
            float predictionTime = math.max(ClearancePredictionMinimumSeconds, patternConfig.BasicTrajectoryPredictionTime);

            if (ShouldYieldToNeighbor(enemyEntity,
                                      enemyData.PriorityTier,
                                      enemyPosition,
                                      desiredVelocity,
                                      distanceToTarget,
                                      bodyRadius,
                                      minimumEnemyClearance,
                                      predictionTime,
                                      in occupancyContext))
            {
                patternRuntimeState.WanderHasTarget = 0;
                patternRuntimeState.WanderWaitTimer = 0f;
                patternRuntimeState.WanderRetryTimer = 0f;
                return float3.zero;
            }

            return desiredVelocity;
        }

        if (patternRuntimeState.WanderRetryTimer > 0f)
            return float3.zero;

        bool pickedDestination = TryPickWanderDestination(enemyEntity,
                                                          enemyData.PriorityTier,
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

    /// <summary>
    /// Computes a planar clearance velocity used to reduce overlap and deadlocks with nearby enemies.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="selfPriorityTier">Current enemy general priority tier.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="bodyRadius">Current enemy body radius.</param>
    /// <param name="minimumEnemyClearance">Extra minimum clearance from neighbors.</param>
    /// <param name="maxSpeed">Current movement speed cap.</param>
    /// <param name="occupancyContext">Occupancy context used for neighbor lookup.</param>
    /// <returns>Planar clearance velocity contribution.</returns>
    public static float3 ResolveLocalClearanceVelocity(Entity enemyEntity,
                                                       int selfPriorityTier,
                                                       float3 enemyPosition,
                                                       float bodyRadius,
                                                       float minimumEnemyClearance,
                                                       float maxSpeed,
                                                       in OccupancyContext occupancyContext)
    {
        float clearanceSpeedCap = math.max(0f, maxSpeed);

        if (clearanceSpeedCap <= 0f)
            return float3.zero;

        float requiredPadding = math.max(0f, minimumEnemyClearance);
        float normalizedBodyRadius = math.max(0.05f, bodyRadius);
        float searchRadius = (normalizedBodyRadius + requiredPadding + math.max(0.05f, occupancyContext.MaxRadius)) * SoftClearanceMultiplier;
        int minCellX = (int)math.floor((enemyPosition.x - searchRadius) * occupancyContext.InverseCellSize);
        int maxCellX = (int)math.floor((enemyPosition.x + searchRadius) * occupancyContext.InverseCellSize);
        int minCellY = (int)math.floor((enemyPosition.z - searchRadius) * occupancyContext.InverseCellSize);
        int maxCellY = (int)math.floor((enemyPosition.z + searchRadius) * occupancyContext.InverseCellSize);
        float3 accumulatedDirection = float3.zero;
        float maximumPenetration = 0f;
        int contributorCount = 0;

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
                    Entity neighborEntity = occupancyContext.Entities[occupancyIndex];

                    if (neighborEntity == enemyEntity)
                        continue;

                    float3 neighborPosition = occupancyContext.Positions[occupancyIndex];
                    float neighborRadius = math.max(0.05f, occupancyContext.Radii[occupancyIndex]);
                    int neighborPriorityTier = occupancyContext.PriorityTiers[occupancyIndex];

                    if (neighborPriorityTier < selfPriorityTier)
                        continue;

                    float priorityClearanceMultiplier = ResolvePriorityClearanceMultiplier(selfPriorityTier, neighborPriorityTier);
                    float requiredClearance = math.max(0.01f, (normalizedBodyRadius + neighborRadius + requiredPadding) * priorityClearanceMultiplier);
                    float softClearance = requiredClearance * SoftClearanceMultiplier;
                    float3 delta = enemyPosition - neighborPosition;
                    delta.y = 0f;
                    float distanceSquared = math.lengthsq(delta);
                    float distance = math.sqrt(math.max(0f, distanceSquared));

                    if (distance >= softClearance)
                        continue;

                    float3 direction = distance > DirectionEpsilon
                        ? delta / distance
                        : ResolveDeterministicDirection(enemyEntity, neighborEntity);

                    float penetration = math.max(0f, requiredClearance - distance);
                    float softWeight = math.saturate((softClearance - distance) / math.max(0.01f, softClearance));
                    float penetrationWeight = penetration / math.max(0.01f, requiredClearance);
                    float weight = softWeight + penetrationWeight * 1.5f;
                    float priorityWeight = ResolvePriorityAvoidanceWeight(selfPriorityTier, neighborPriorityTier);
                    accumulatedDirection += direction * weight * priorityWeight;

                    if (penetration > maximumPenetration)
                        maximumPenetration = penetration;

                    contributorCount++;
                }
                while (occupancyContext.CellMap.TryGetNextValue(out occupancyIndex, ref iterator));
            }
        }

        if (contributorCount <= 0)
            return float3.zero;

        float3 normalizedDirection = math.normalizesafe(new float3(accumulatedDirection.x, 0f, accumulatedDirection.z), float3.zero);

        if (math.lengthsq(normalizedDirection) <= DirectionEpsilon)
            return float3.zero;

        float referenceClearance = math.max(0.05f, normalizedBodyRadius + requiredPadding);
        float penetrationFactor = math.saturate(maximumPenetration / referenceClearance);
        float clearanceSpeed = math.lerp(clearanceSpeedCap * 0.35f, clearanceSpeedCap, penetrationFactor);
        return normalizedDirection * clearanceSpeed;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Picks the best Wanderer destination by prioritizing collision-safe trajectories and then designer biases.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="selfPriorityTier">Current enemy general priority tier.</param>
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
                                                 int selfPriorityTier,
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
                                                               selfPriorityTier,
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
    /// <param name="selfPriorityTier">Current enemy general priority tier.</param>
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
                                                     int selfPriorityTier,
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
                    int otherPriorityTier = occupancyContext.PriorityTiers[occupancyIndex];

                    if (otherPriorityTier < selfPriorityTier)
                        continue;

                    float priorityClearanceMultiplier = ResolvePriorityClearanceMultiplier(selfPriorityTier, otherPriorityTier);
                    float requiredClearance = math.max(0.01f, (bodyRadius + otherRadius + minimumEnemyClearance) * priorityClearanceMultiplier);

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
    /// Evaluates imminent movement conflicts and resolves deterministic right-of-way for Wanderer agents.
    /// </summary>
    /// <param name="enemyEntity">Current enemy entity.</param>
    /// <param name="selfPriorityTier">Current enemy general priority tier.</param>
    /// <param name="enemyPosition">Current enemy position.</param>
    /// <param name="desiredVelocity">Current desired velocity toward target.</param>
    /// <param name="distanceToTarget">Distance to the current Wanderer target.</param>
    /// <param name="bodyRadius">Current enemy body radius.</param>
    /// <param name="minimumEnemyClearance">Extra clearance from neighbors.</param>
    /// <param name="predictionTime">Prediction horizon in seconds.</param>
    /// <param name="occupancyContext">Occupancy context used for neighbor lookup.</param>
    /// <returns>True when current enemy should yield and repath.</returns>
    private static bool ShouldYieldToNeighbor(Entity enemyEntity,
                                              int selfPriorityTier,
                                              float3 enemyPosition,
                                              float3 desiredVelocity,
                                              float distanceToTarget,
                                              float bodyRadius,
                                              float minimumEnemyClearance,
                                              float predictionTime,
                                              in OccupancyContext occupancyContext)
    {
        float selfSpeed = math.length(new float3(desiredVelocity.x, 0f, desiredVelocity.z));

        if (selfSpeed <= DirectionEpsilon)
            return false;

        float searchRadius = bodyRadius + minimumEnemyClearance + math.max(0.05f, occupancyContext.MaxRadius) + selfSpeed * predictionTime;
        int minCellX = (int)math.floor((enemyPosition.x - searchRadius) * occupancyContext.InverseCellSize);
        int maxCellX = (int)math.floor((enemyPosition.x + searchRadius) * occupancyContext.InverseCellSize);
        int minCellY = (int)math.floor((enemyPosition.z - searchRadius) * occupancyContext.InverseCellSize);
        int maxCellY = (int)math.floor((enemyPosition.z + searchRadius) * occupancyContext.InverseCellSize);

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

                    float otherRadius = math.max(0.05f, occupancyContext.Radii[occupancyIndex]);
                    int otherPriorityTier = occupancyContext.PriorityTiers[occupancyIndex];

                    if (otherPriorityTier < selfPriorityTier)
                        continue;

                    float priorityClearanceMultiplier = ResolvePriorityClearanceMultiplier(selfPriorityTier, otherPriorityTier);
                    float requiredClearance = math.max(0.01f, (bodyRadius + otherRadius + minimumEnemyClearance) * priorityClearanceMultiplier);
                    float3 otherPosition = occupancyContext.Positions[occupancyIndex];
                    float3 otherVelocity = occupancyContext.Velocities[occupancyIndex];
                    otherVelocity.y = 0f;

                    float3 delta = otherPosition - enemyPosition;
                    delta.y = 0f;
                    float distance = math.length(delta);

                    if (selfPriorityTier < otherPriorityTier)
                    {
                        float priorityGap = math.min(4f, (float)math.max(1, otherPriorityTier - selfPriorityTier));
                        float forcedYieldDistance = requiredClearance * (1.6f + priorityGap * 0.35f);

                        if (distance <= forcedYieldDistance)
                            return true;
                    }
                    else if (selfPriorityTier > otherPriorityTier)
                    {
                        if (distance > requiredClearance * 1.05f)
                            continue;
                    }

                    if (distance <= DirectionEpsilon)
                    {
                        if (ShouldYieldByStablePriority(enemyEntity, otherEntity))
                            return true;

                        continue;
                    }

                    float softGateDistance = requiredClearance * SoftClearanceMultiplier;

                    if (selfPriorityTier < otherPriorityTier)
                    {
                        float priorityGap = math.min(3f, (float)math.max(1, otherPriorityTier - selfPriorityTier));
                        softGateDistance *= 1f + priorityGap * 0.2f;
                    }

                    if (distance > softGateDistance)
                        continue;

                    float otherSpeed = math.length(otherVelocity);
                    float3 relativeVelocity = desiredVelocity - otherVelocity;
                    float relativeSpeedSquared = math.lengthsq(relativeVelocity);

                    if (relativeSpeedSquared <= DirectionEpsilon)
                    {
                        if (ShouldYieldByStablePriority(enemyEntity, otherEntity))
                            return true;

                        continue;
                    }

                    float timeToClosest = -math.dot(delta, relativeVelocity) / relativeSpeedSquared;

                    if (timeToClosest < 0f || timeToClosest > predictionTime)
                        continue;

                    float3 closestSelfPosition = enemyPosition + desiredVelocity * timeToClosest;
                    float3 closestOtherPosition = otherPosition + otherVelocity * timeToClosest;
                    float3 closestDelta = closestOtherPosition - closestSelfPosition;
                    closestDelta.y = 0f;
                    float closestDistance = math.length(closestDelta);

                    if (closestDistance > requiredClearance)
                        continue;

                    if (selfPriorityTier < otherPriorityTier)
                        return true;

                    float approachDistance = math.max(0f, distance - requiredClearance);
                    float selfArrivalSeconds = approachDistance / math.max(0.01f, selfSpeed);
                    float otherArrivalSeconds = approachDistance / math.max(0.01f, otherSpeed);

                    if (selfArrivalSeconds > otherArrivalSeconds + RightOfWayTieSeconds)
                        return true;

                    if (selfArrivalSeconds + RightOfWayTieSeconds < otherArrivalSeconds)
                        continue;

                    float distanceBiasThreshold = math.max(0.05f, requiredClearance * 1.25f);

                    if (distanceToTarget > distanceBiasThreshold && ShouldYieldByStablePriority(enemyEntity, otherEntity))
                        return true;
                }
                while (occupancyContext.CellMap.TryGetNextValue(out occupancyIndex, ref iterator));
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves stable deterministic tie-break ordering between two entities.
    /// </summary>
    /// <param name="currentEntity">Current entity.</param>
    /// <param name="otherEntity">Neighbor entity.</param>
    /// <returns>True when current entity should yield.</returns>
    private static bool ShouldYieldByStablePriority(Entity currentEntity, Entity otherEntity)
    {
        uint currentHash = math.hash(new int2(currentEntity.Index * 3 + 7, currentEntity.Version * 5 + 11));
        uint otherHash = math.hash(new int2(otherEntity.Index * 3 + 7, otherEntity.Version * 5 + 11));

        if (currentHash == otherHash)
            return currentEntity.Index > otherEntity.Index;

        return currentHash < otherHash;
    }

    private static float ResolvePriorityClearanceMultiplier(int selfPriorityTier, int neighborPriorityTier)
    {
        if (selfPriorityTier < neighborPriorityTier)
        {
            float priorityGap = math.min(6f, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
            return 1.65f + priorityGap * 0.2f;
        }

        if (selfPriorityTier > neighborPriorityTier)
        {
            float priorityGap = math.min(6f, (float)math.max(1, selfPriorityTier - neighborPriorityTier));
            return math.max(0.55f, 0.96f - priorityGap * 0.06f);
        }

        return 1f;
    }

    private static float ResolvePriorityAvoidanceWeight(int selfPriorityTier, int neighborPriorityTier)
    {
        if (selfPriorityTier < neighborPriorityTier)
        {
            float priorityGap = math.min(6f, (float)math.max(1, neighborPriorityTier - selfPriorityTier));
            return 3.1f + priorityGap * 0.9f;
        }

        if (selfPriorityTier > neighborPriorityTier)
        {
            float priorityGap = math.min(6f, (float)math.max(1, selfPriorityTier - neighborPriorityTier));
            return math.max(0.2f, 0.7f - priorityGap * 0.08f);
        }

        return 1f;
    }

    /// <summary>
    /// Resolves a deterministic planar fallback direction for zero-distance overlap cases.
    /// </summary>
    /// <param name="currentEntity">Current entity.</param>
    /// <param name="otherEntity">Neighbor entity.</param>
    /// <returns>Normalized planar direction.</returns>
    private static float3 ResolveDeterministicDirection(Entity currentEntity, Entity otherEntity)
    {
        uint hash = math.hash(new int4(currentEntity.Index, currentEntity.Version, otherEntity.Index, otherEntity.Version));
        float angleRadians = (hash & 0x0000FFFFu) / 65535f * math.PI * 2f;
        return new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
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
