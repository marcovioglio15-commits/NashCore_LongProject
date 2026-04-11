using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies periodic Laser Beam damage ticks against enemies currently intersecting resolved beam segments.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyProjectileHitSystem))]
[UpdateBefore(typeof(EnemyElementalEffectsSystem))]
public partial struct PlayerLaserBeamDamageSystem : ISystem
{
    #region Nested Types
    private struct LaserBeamHitCandidate
    {
        public int EnemyIndex;
        public float DistanceAlongLane;
        public float3 HitPoint;
        public float3 HitDirection;
    }
    #endregion

    #region Fields
    private EntityQuery enemyQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the enemy query used by the Laser Beam hit resolution path.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, EnemyRuntimeState, EnemyKnockbackState, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        state.RequireForUpdate<PlayerLaserBeamState>();
        state.RequireForUpdate<PlayerLaserBeamLaneElement>();
    }

    /// <summary>
    /// Resolves periodic Laser Beam ticks for every player that has one active and tick-ready beam state.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        int enemyCount = enemyQuery.CalculateEntityCount();

        if (enemyCount <= 0)
        {
            ConsumeBeamTicksWithoutTargets(ref state);
            return;
        }

        NativeArray<Entity> enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
        NativeArray<EnemyData> enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(Allocator.Temp);
        NativeArray<EnemyHealth> projectedEnemyHealth = enemyQuery.ToComponentDataArray<EnemyHealth>(Allocator.Temp);
        NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = enemyQuery.ToComponentDataArray<EnemyRuntimeState>(Allocator.Temp);
        NativeArray<EnemyKnockbackState> projectedEnemyKnockback = enemyQuery.ToComponentDataArray<EnemyKnockbackState>(Allocator.Temp);
        NativeArray<byte> enemyDirtyFlags = new NativeArray<byte>(enemyCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
        NativeArray<byte> enemyKnockbackDirtyFlags = new NativeArray<byte>(enemyCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
        NativeArray<float3> enemyPositions = new NativeArray<float3>(enemyCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> enemyBodyRadii = new NativeArray<float>(enemyCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        float maximumEnemyRadius = 0.05f;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            enemyPositions[enemyIndex] = enemyTransforms[enemyIndex].Position;
            float bodyRadius = math.max(0.05f, enemyDataArray[enemyIndex].BodyRadius);
            enemyBodyRadii[enemyIndex] = bodyRadius;

            if (bodyRadius > maximumEnemyRadius)
                maximumEnemyRadius = bodyRadius;
        }

        float cellSize = EnemySpatialHashUtility.ResolveCellSize(maximumEnemyRadius);
        float inverseCellSize = 1f / cellSize;
        NativeParallelMultiHashMap<int, int> enemyCellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.Temp);
        EnemySpatialHashUtility.BuildCellMap(in enemyPositions, inverseCellSize, ref enemyCellMap);

        BufferLookup<EnemyElementStackElement> elementalStackLookup = SystemAPI.GetBufferLookup<EnemyElementStackElement>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerElementalVfxConfig>(true);
        ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup = SystemAPI.GetComponentLookup<EnemyElementalVfxAnchor>(true);
        ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup = SystemAPI.GetComponentLookup<EnemyHitVfxConfig>(true);
        ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup = SystemAPI.GetComponentLookup<EnemySpawnInactivityLock>(true);
        ComponentLookup<EnemyDespawnRequest> despawnRequestLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        NativeList<LaserBeamHitCandidate> hitCandidates = new NativeList<LaserBeamHitCandidate>(32, Allocator.Temp);

        foreach ((RefRO<PlayerRuntimeShootingConfig> runtimeShootingConfig,
                  DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                  RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRW<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerRuntimeShootingConfig>,
                                    DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot>,
                                    RefRO<PlayerPassiveToolsState>,
                                    RefRW<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamLaneElement>>()
                             .WithEntityAccess())
        {
            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;

            if (currentLaserBeamState.IsActive == 0 || currentLaserBeamState.IsTickReady == 0)
                continue;

            LaserBeamPassiveConfig laserBeamConfig = passiveToolsState.ValueRO.LaserBeam;
            float damageTickIntervalSeconds = math.max(0.0001f, laserBeamConfig.DamageTickIntervalSeconds);
            int pendingTickCount = ResolvePendingTickCount(ref currentLaserBeamState, damageTickIntervalSeconds);
            currentLaserBeamState.IsTickReady = 0;

            if (pendingTickCount <= 0)
            {
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            NotifyTickPulse(ref currentLaserBeamState, pendingTickCount, damageTickIntervalSeconds);

            if (laserBeamLanes.Length <= 0 || passiveToolsState.ValueRO.HasLaserBeam == 0)
            {
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            ElementalEffectConfig unusedElementalEffect = default;
            PlayerProjectileRequestTemplate projectileTemplate = PlayerProjectileRequestUtility.BuildProjectileTemplate(in runtimeShootingConfig.ValueRO,
                                                                                                                         appliedElementSlots,
                                                                                                                         in passiveToolsState.ValueRO,
                                                                                                                         1f,
                                                                                                                         1f,
                                                                                                                         1f,
                                                                                                                         1f,
                                                                                                                         1f,
                                                                                                                         false,
                                                                                                                         in unusedElementalEffect,
                                                                                                                         0f);
            float chargeImpulseDamageMultiplier = currentLaserBeamState.ChargeImpulseRemainingSeconds > 0f
                ? math.max(1f, currentLaserBeamState.ChargeImpulseDamageMultiplier)
                : 1f;
            float baseDamagePerTick = math.max(0f,
                                               projectileTemplate.Damage *
                                               math.max(0f, runtimeShootingConfig.ValueRO.Values.RateOfFire) *
                                               math.max(0f, laserBeamConfig.DamageMultiplier) *
                                               damageTickIntervalSeconds *
                                               chargeImpulseDamageMultiplier *
                                               pendingTickCount);

            if (baseDamagePerTick <= 0f)
            {
                laserBeamState.ValueRW = currentLaserBeamState;
                continue;
            }

            PlayerProjectileRequestUtility.ResolvePenetrationSettings(in runtimeShootingConfig.ValueRO.Values,
                                                                      ProjectilePenetrationMode.None,
                                                                      0,
                                                                      out ProjectilePenetrationMode penetrationMode,
                                                                      out int maximumPenetrations);
            bool canEnqueueVfxRequests = vfxRequestLookup.HasBuffer(playerEntity);
            DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests = default;

            if (canEnqueueVfxRequests)
                shooterVfxRequests = vfxRequestLookup[playerEntity];

            int segmentStartIndex = 0;

            while (segmentStartIndex < laserBeamLanes.Length)
            {
                PlayerLaserBeamLaneElement firstSegment = laserBeamLanes[segmentStartIndex];
                int laneIndex = firstSegment.LaneIndex;
                int segmentEndIndex = segmentStartIndex + 1;

                while (segmentEndIndex < laserBeamLanes.Length &&
                       laserBeamLanes[segmentEndIndex].LaneIndex == laneIndex)
                {
                    segmentEndIndex++;
                }

                CollectHitCandidates(laserBeamLanes,
                                     segmentStartIndex,
                                     segmentEndIndex,
                                     enemyCount,
                                     in enemyPositions,
                                     in enemyBodyRadii,
                                     in enemyCellMap,
                                     inverseCellSize,
                                     maximumEnemyRadius,
                                     ref hitCandidates);

                if (hitCandidates.Length > 0)
                {
                    ResolveLaneHits(playerEntity,
                                    baseDamagePerTick,
                                    penetrationMode,
                                    maximumPenetrations,
                                    projectileTemplate,
                                    in laserBeamLanes,
                                    segmentStartIndex,
                                    in hitCandidates,
                                    enemyEntities,
                                    ref projectedEnemyHealth,
                                    in enemyPositions,
                                    in enemyRuntimeArray,
                                    ref projectedEnemyKnockback,
                                    ref enemyDirtyFlags,
                                    ref enemyKnockbackDirtyFlags,
                                    in elementalVfxConfigLookup,
                                    in elementalVfxAnchorLookup,
                                    in enemyHitVfxConfigLookup,
                                    in spawnInactivityLockLookup,
                                    canEnqueueVfxRequests,
                                    ref shooterVfxRequests,
                                    ref elementalStackLookup,
                                    in despawnRequestLookup,
                                    ref commandBuffer);
                }

                segmentStartIndex = segmentEndIndex;
            }

            laserBeamState.ValueRW = currentLaserBeamState;
        }

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            if (enemyDirtyFlags[enemyIndex] == 0 && enemyKnockbackDirtyFlags[enemyIndex] == 0)
                continue;

            Entity enemyEntity = enemyEntities[enemyIndex];

            if (!entityManager.Exists(enemyEntity))
                continue;

            if (enemyKnockbackDirtyFlags[enemyIndex] != 0)
                entityManager.SetComponentData(enemyEntity, projectedEnemyKnockback[enemyIndex]);

            if (enemyDirtyFlags[enemyIndex] == 0)
                continue;

            entityManager.SetComponentData(enemyEntity, projectedEnemyHealth[enemyIndex]);
            DamageFlashRuntimeUtility.Trigger(entityManager, enemyEntity);
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
        hitCandidates.Dispose();
        enemyEntities.Dispose();
        enemyDataArray.Dispose();
        projectedEnemyHealth.Dispose();
        enemyTransforms.Dispose();
        enemyRuntimeArray.Dispose();
        projectedEnemyKnockback.Dispose();
        enemyDirtyFlags.Dispose();
        enemyKnockbackDirtyFlags.Dispose();
        enemyPositions.Dispose();
        enemyBodyRadii.Dispose();
        enemyCellMap.Dispose();
    }
    #endregion

    #region Helpers
    private void ConsumeBeamTicksWithoutTargets(ref SystemState state)
    {
        foreach ((RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRW<PlayerLaserBeamState> laserBeamState)
                 in SystemAPI.Query<RefRO<PlayerPassiveToolsState>, RefRW<PlayerLaserBeamState>>())
        {
            PlayerLaserBeamState currentLaserBeamState = laserBeamState.ValueRO;

            if (currentLaserBeamState.IsActive == 0 || currentLaserBeamState.IsTickReady == 0)
                continue;

            float tickIntervalSeconds = math.max(0.0001f, passiveToolsState.ValueRO.LaserBeam.DamageTickIntervalSeconds);
            int pendingTickCount = ResolvePendingTickCount(ref currentLaserBeamState, tickIntervalSeconds);

            if (pendingTickCount > 0)
                NotifyTickPulse(ref currentLaserBeamState, pendingTickCount, tickIntervalSeconds);

            currentLaserBeamState.IsTickReady = 0;
            laserBeamState.ValueRW = currentLaserBeamState;
        }
    }

    private static int ResolvePendingTickCount(ref PlayerLaserBeamState laserBeamState,
                                               float tickIntervalSeconds)
    {
        float safeTickIntervalSeconds = math.max(0.0001f, tickIntervalSeconds);

        if (laserBeamState.DamageTickTimer > 0f)
            return 0;

        float overdueSeconds = -laserBeamState.DamageTickTimer;
        int additionalTickCount = (int)math.floor(overdueSeconds / safeTickIntervalSeconds);
        int pendingTickCount = 1 + math.max(0, additionalTickCount);
        laserBeamState.DamageTickTimer += pendingTickCount * safeTickIntervalSeconds;

        if (laserBeamState.DamageTickTimer <= 0f)
            laserBeamState.DamageTickTimer = safeTickIntervalSeconds;

        return pendingTickCount;
    }

    /// <summary>
    /// Records one or two travelling pulse ages so the presentation layer can show recent damage ticks moving along the beam body.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params pendingTickCount Number of ticks consumed during the current frame.
    /// /params tickIntervalSeconds Authored tick cadence used to age the older stored pulse when several ticks were consumed at once.
    /// /returns None.
    /// </summary>
    private static void NotifyTickPulse(ref PlayerLaserBeamState laserBeamState,
                                        int pendingTickCount,
                                        float tickIntervalSeconds)
    {
        if (pendingTickCount <= 0)
            return;

        float olderPulseElapsedSeconds = laserBeamState.PrimaryTickPulseElapsedSeconds;
        bool hasOlderPulse = laserBeamState.HasPrimaryTickPulse != 0;

        if (pendingTickCount > 1)
        {
            olderPulseElapsedSeconds = math.max(0f, tickIntervalSeconds) * math.max(1, pendingTickCount - 1);
            hasOlderPulse = true;
        }

        if (hasOlderPulse)
        {
            laserBeamState.HasSecondaryTickPulse = 1;
            laserBeamState.SecondaryTickPulseElapsedSeconds = olderPulseElapsedSeconds;
        }
        else
        {
            laserBeamState.HasSecondaryTickPulse = 0;
            laserBeamState.SecondaryTickPulseElapsedSeconds = 0f;
        }

        laserBeamState.HasPrimaryTickPulse = 1;
        laserBeamState.PrimaryTickPulseElapsedSeconds = 0f;
    }

    private static void CollectHitCandidates(DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                             int segmentStartIndex,
                                             int segmentEndIndex,
                                             int enemyCount,
                                             in NativeArray<float3> enemyPositions,
                                             in NativeArray<float> enemyBodyRadii,
                                             in NativeParallelMultiHashMap<int, int> enemyCellMap,
                                             float inverseCellSize,
                                             float maximumEnemyRadius,
                                             ref NativeList<LaserBeamHitCandidate> hitCandidates)
    {
        hitCandidates.Clear();
        float accumulatedDistanceBeforeSegment = 0f;

        for (int segmentIndex = segmentStartIndex; segmentIndex < segmentEndIndex; segmentIndex++)
        {
            PlayerLaserBeamLaneElement segment = laserBeamLanes[segmentIndex];
            float3 midpoint = (segment.StartPoint + segment.EndPoint) * 0.5f;
            float queryRadius = segment.Length * 0.5f + math.max(0.01f, segment.CollisionRadius + maximumEnemyRadius);
            EnemySpatialHashUtility.ResolveCellBounds(midpoint,
                                                      queryRadius,
                                                      inverseCellSize,
                                                      out int minCellX,
                                                      out int maxCellX,
                                                      out int minCellY,
                                                      out int maxCellY);

            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    int cellKey = EnemySpatialHashUtility.EncodeCell(cellX, cellY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int enemyIndex;

                    if (!enemyCellMap.TryGetFirstValue(cellKey, out enemyIndex, out iterator))
                        continue;

                    do
                    {
                        if (enemyIndex < 0 || enemyIndex >= enemyCount)
                            continue;

                        float3 closestPoint = ClosestPointOnSegment(enemyPositions[enemyIndex], segment.StartPoint, segment.EndPoint);
                        float3 delta = enemyPositions[enemyIndex] - closestPoint;
                        delta.y = 0f;
                        float combinedRadius = math.max(0.01f, segment.CollisionRadius + math.max(0.05f, enemyBodyRadii[enemyIndex]));

                        if (math.lengthsq(delta) > combinedRadius * combinedRadius)
                            continue;

                        float distanceAlongSegment = math.distance(segment.StartPoint, closestPoint);
                        AddHitCandidate(ref hitCandidates,
                                        enemyIndex,
                                        accumulatedDistanceBeforeSegment + distanceAlongSegment,
                                        closestPoint,
                                        segment.Direction);
                    }
                    while (enemyCellMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            accumulatedDistanceBeforeSegment += math.max(0f, segment.Length);
        }
    }

    private static void AddHitCandidate(ref NativeList<LaserBeamHitCandidate> hitCandidates,
                                        int enemyIndex,
                                        float distanceAlongLane,
                                        float3 hitPoint,
                                        float3 hitDirection)
    {
        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            if (hitCandidates[candidateIndex].EnemyIndex != enemyIndex)
                continue;

            if (distanceAlongLane >= hitCandidates[candidateIndex].DistanceAlongLane)
                return;

            hitCandidates[candidateIndex] = new LaserBeamHitCandidate
            {
                EnemyIndex = enemyIndex,
                DistanceAlongLane = distanceAlongLane,
                HitPoint = hitPoint,
                HitDirection = hitDirection
            };
            SortCandidates(ref hitCandidates);
            return;
        }

        hitCandidates.Add(new LaserBeamHitCandidate
        {
            EnemyIndex = enemyIndex,
            DistanceAlongLane = distanceAlongLane,
            HitPoint = hitPoint,
            HitDirection = hitDirection
        });
        SortCandidates(ref hitCandidates);
    }

    private static void SortCandidates(ref NativeList<LaserBeamHitCandidate> hitCandidates)
    {
        for (int candidateIndex = hitCandidates.Length - 1; candidateIndex > 0; candidateIndex--)
        {
            LaserBeamHitCandidate currentCandidate = hitCandidates[candidateIndex];
            LaserBeamHitCandidate previousCandidate = hitCandidates[candidateIndex - 1];

            if (previousCandidate.DistanceAlongLane <= currentCandidate.DistanceAlongLane)
                break;

            hitCandidates[candidateIndex - 1] = currentCandidate;
            hitCandidates[candidateIndex] = previousCandidate;
        }
    }

    private static void ResolveLaneHits(Entity shooterEntity,
                                        float laneDamagePerTick,
                                        ProjectilePenetrationMode penetrationMode,
                                        int maximumPenetrations,
                                        PlayerProjectileRequestTemplate projectileTemplate,
                                        in DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                        int segmentStartIndex,
                                        in NativeList<LaserBeamHitCandidate> hitCandidates,
                                        NativeArray<Entity> enemyEntities,
                                        ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                        in NativeArray<float3> enemyPositions,
                                        in NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                        ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                        ref NativeArray<byte> enemyDirtyFlags,
                                        ref NativeArray<byte> enemyKnockbackDirtyFlags,
                                        in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                        in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                        in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                        in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                        bool canEnqueueVfxRequests,
                                        ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                        ref BufferLookup<EnemyElementStackElement> elementalStackLookup,
                                        in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                        ref EntityCommandBuffer commandBuffer)
    {
        PlayerLaserBeamLaneElement referenceSegment = laserBeamLanes[segmentStartIndex];
        float effectiveLaneDamagePerTick = math.max(0f, laneDamagePerTick * math.max(0f, referenceSegment.DamageMultiplier));

        switch (penetrationMode)
        {
            case ProjectilePenetrationMode.FixedHits:
                ResolveFixedHitMode(shooterEntity,
                                    effectiveLaneDamagePerTick,
                                    maximumPenetrations,
                                    projectileTemplate,
                                    in referenceSegment,
                                    in hitCandidates,
                                    enemyEntities,
                                    ref projectedEnemyHealth,
                                    in enemyPositions,
                                    in enemyRuntimeArray,
                                    ref projectedEnemyKnockback,
                                    ref enemyDirtyFlags,
                                    ref enemyKnockbackDirtyFlags,
                                    in elementalVfxConfigLookup,
                                    in elementalVfxAnchorLookup,
                                    in enemyHitVfxConfigLookup,
                                    in spawnInactivityLockLookup,
                                    canEnqueueVfxRequests,
                                    ref shooterVfxRequests,
                                    ref elementalStackLookup,
                                    in despawnRequestLookup,
                                    ref commandBuffer);
                return;
            case ProjectilePenetrationMode.Infinite:
                ResolveInfiniteHitMode(shooterEntity,
                                       effectiveLaneDamagePerTick,
                                       projectileTemplate,
                                       in referenceSegment,
                                       in hitCandidates,
                                       enemyEntities,
                                       ref projectedEnemyHealth,
                                       in enemyPositions,
                                       in enemyRuntimeArray,
                                       ref projectedEnemyKnockback,
                                       ref enemyDirtyFlags,
                                       ref enemyKnockbackDirtyFlags,
                                       in elementalVfxConfigLookup,
                                       in elementalVfxAnchorLookup,
                                       in enemyHitVfxConfigLookup,
                                       in spawnInactivityLockLookup,
                                       canEnqueueVfxRequests,
                                       ref shooterVfxRequests,
                                       ref elementalStackLookup,
                                       in despawnRequestLookup,
                                       ref commandBuffer);
                return;
            case ProjectilePenetrationMode.DamageBased:
                ResolveDamageBasedMode(shooterEntity,
                                       effectiveLaneDamagePerTick,
                                       maximumPenetrations,
                                       projectileTemplate,
                                       in referenceSegment,
                                       in hitCandidates,
                                       enemyEntities,
                                       ref projectedEnemyHealth,
                                       in enemyPositions,
                                       in enemyRuntimeArray,
                                       ref projectedEnemyKnockback,
                                       ref enemyDirtyFlags,
                                       ref enemyKnockbackDirtyFlags,
                                       in elementalVfxConfigLookup,
                                       in elementalVfxAnchorLookup,
                                       in enemyHitVfxConfigLookup,
                                       in spawnInactivityLockLookup,
                                       canEnqueueVfxRequests,
                                       ref shooterVfxRequests,
                                       ref elementalStackLookup,
                                       in despawnRequestLookup,
                                       ref commandBuffer);
                return;
            default:
                ResolveSingleHitMode(shooterEntity,
                                     effectiveLaneDamagePerTick,
                                     projectileTemplate,
                                     in referenceSegment,
                                     in hitCandidates,
                                     enemyEntities,
                                     ref projectedEnemyHealth,
                                     in enemyPositions,
                                     in enemyRuntimeArray,
                                     ref projectedEnemyKnockback,
                                     ref enemyDirtyFlags,
                                     ref enemyKnockbackDirtyFlags,
                                     in elementalVfxConfigLookup,
                                     in elementalVfxAnchorLookup,
                                     in enemyHitVfxConfigLookup,
                                     in spawnInactivityLockLookup,
                                     canEnqueueVfxRequests,
                                     ref shooterVfxRequests,
                                     ref elementalStackLookup,
                                     in despawnRequestLookup,
                                     ref commandBuffer);
                return;
        }
    }

    private static void ResolveSingleHitMode(Entity shooterEntity,
                                             float laneDamagePerTick,
                                             PlayerProjectileRequestTemplate projectileTemplate,
                                             in PlayerLaserBeamLaneElement referenceSegment,
                                             in NativeList<LaserBeamHitCandidate> hitCandidates,
                                             NativeArray<Entity> enemyEntities,
                                             ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                             in NativeArray<float3> enemyPositions,
                                             in NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                             ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                             ref NativeArray<byte> enemyDirtyFlags,
                                             ref NativeArray<byte> enemyKnockbackDirtyFlags,
                                             in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                             in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                             in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                             in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                             bool canEnqueueVfxRequests,
                                             ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                             ref BufferLookup<EnemyElementStackElement> elementalStackLookup,
                                             in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                             ref EntityCommandBuffer commandBuffer)
    {
        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];

            if (!TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                       hitCandidate.EnemyIndex,
                                       laneDamagePerTick,
                                       out bool enemyKilled))
            {
                continue;
            }

            enemyDirtyFlags[hitCandidate.EnemyIndex] = 1;
            ApplyHitPayloads(shooterEntity,
                             hitCandidate.EnemyIndex,
                             hitCandidate.HitPoint,
                             hitCandidate.HitDirection,
                             laneDamagePerTick,
                             projectileTemplate,
                             in referenceSegment,
                             enemyEntities,
                             in enemyPositions,
                             in enemyRuntimeArray,
                             ref projectedEnemyKnockback,
                             ref enemyKnockbackDirtyFlags,
                             in elementalVfxConfigLookup,
                             in elementalVfxAnchorLookup,
                             in enemyHitVfxConfigLookup,
                             in spawnInactivityLockLookup,
                             canEnqueueVfxRequests,
                             ref shooterVfxRequests,
                             ref elementalStackLookup);
            TryScheduleDespawn(enemyEntities[hitCandidate.EnemyIndex],
                               projectedEnemyHealth[hitCandidate.EnemyIndex],
                               in despawnRequestLookup,
                               ref commandBuffer);
            return;
        }
    }

    private static void ResolveFixedHitMode(Entity shooterEntity,
                                            float laneDamagePerTick,
                                            int maximumPenetrations,
                                            PlayerProjectileRequestTemplate projectileTemplate,
                                            in PlayerLaserBeamLaneElement referenceSegment,
                                            in NativeList<LaserBeamHitCandidate> hitCandidates,
                                            NativeArray<Entity> enemyEntities,
                                            ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                            in NativeArray<float3> enemyPositions,
                                            in NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                            ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                            ref NativeArray<byte> enemyDirtyFlags,
                                            ref NativeArray<byte> enemyKnockbackDirtyFlags,
                                            in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                            in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                            in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                            in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                            bool canEnqueueVfxRequests,
                                            ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                            ref BufferLookup<EnemyElementStackElement> elementalStackLookup,
                                            in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                            ref EntityCommandBuffer commandBuffer)
    {
        int maximumHitCount = 1 + math.max(0, maximumPenetrations);
        int appliedHitCount = 0;

        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            if (appliedHitCount >= maximumHitCount)
                return;

            LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];

            if (!TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                       hitCandidate.EnemyIndex,
                                       laneDamagePerTick,
                                       out bool _))
            {
                continue;
            }

            appliedHitCount++;
            enemyDirtyFlags[hitCandidate.EnemyIndex] = 1;
            ApplyHitPayloads(shooterEntity,
                             hitCandidate.EnemyIndex,
                             hitCandidate.HitPoint,
                             hitCandidate.HitDirection,
                             laneDamagePerTick,
                             projectileTemplate,
                             in referenceSegment,
                             enemyEntities,
                             in enemyPositions,
                             in enemyRuntimeArray,
                             ref projectedEnemyKnockback,
                             ref enemyKnockbackDirtyFlags,
                             in elementalVfxConfigLookup,
                             in elementalVfxAnchorLookup,
                             in enemyHitVfxConfigLookup,
                             in spawnInactivityLockLookup,
                             canEnqueueVfxRequests,
                             ref shooterVfxRequests,
                             ref elementalStackLookup);
            TryScheduleDespawn(enemyEntities[hitCandidate.EnemyIndex],
                               projectedEnemyHealth[hitCandidate.EnemyIndex],
                               in despawnRequestLookup,
                               ref commandBuffer);
        }
    }

    private static void ResolveInfiniteHitMode(Entity shooterEntity,
                                               float laneDamagePerTick,
                                               PlayerProjectileRequestTemplate projectileTemplate,
                                               in PlayerLaserBeamLaneElement referenceSegment,
                                               in NativeList<LaserBeamHitCandidate> hitCandidates,
                                               NativeArray<Entity> enemyEntities,
                                               ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                               in NativeArray<float3> enemyPositions,
                                               in NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                               ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                               ref NativeArray<byte> enemyDirtyFlags,
                                               ref NativeArray<byte> enemyKnockbackDirtyFlags,
                                               in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                               in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                               in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                               in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                               bool canEnqueueVfxRequests,
                                               ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                               ref BufferLookup<EnemyElementStackElement> elementalStackLookup,
                                               in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                               ref EntityCommandBuffer commandBuffer)
    {
        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];

            if (!TryApplyFlatDamageHit(ref projectedEnemyHealth,
                                       hitCandidate.EnemyIndex,
                                       laneDamagePerTick,
                                       out bool _))
            {
                continue;
            }

            enemyDirtyFlags[hitCandidate.EnemyIndex] = 1;
            ApplyHitPayloads(shooterEntity,
                             hitCandidate.EnemyIndex,
                             hitCandidate.HitPoint,
                             hitCandidate.HitDirection,
                             laneDamagePerTick,
                             projectileTemplate,
                             in referenceSegment,
                             enemyEntities,
                             in enemyPositions,
                             in enemyRuntimeArray,
                             ref projectedEnemyKnockback,
                             ref enemyKnockbackDirtyFlags,
                             in elementalVfxConfigLookup,
                             in elementalVfxAnchorLookup,
                             in enemyHitVfxConfigLookup,
                             in spawnInactivityLockLookup,
                             canEnqueueVfxRequests,
                             ref shooterVfxRequests,
                             ref elementalStackLookup);
            TryScheduleDespawn(enemyEntities[hitCandidate.EnemyIndex],
                               projectedEnemyHealth[hitCandidate.EnemyIndex],
                               in despawnRequestLookup,
                               ref commandBuffer);
        }
    }

    private static void ResolveDamageBasedMode(Entity shooterEntity,
                                               float laneDamagePerTick,
                                               int maximumPenetrations,
                                               PlayerProjectileRequestTemplate projectileTemplate,
                                               in PlayerLaserBeamLaneElement referenceSegment,
                                               in NativeList<LaserBeamHitCandidate> hitCandidates,
                                               NativeArray<Entity> enemyEntities,
                                               ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                               in NativeArray<float3> enemyPositions,
                                               in NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                               ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                               ref NativeArray<byte> enemyDirtyFlags,
                                               ref NativeArray<byte> enemyKnockbackDirtyFlags,
                                               in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                               in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                               in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                               in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                               bool canEnqueueVfxRequests,
                                               ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                               ref BufferLookup<EnemyElementStackElement> elementalStackLookup,
                                               in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                               ref EntityCommandBuffer commandBuffer)
    {
        float remainingDamage = math.max(0f, laneDamagePerTick);
        int consumedPenetrations = 0;

        for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
        {
            if (remainingDamage <= 0f)
                return;

            LaserBeamHitCandidate hitCandidate = hitCandidates[candidateIndex];
            bool enemyKilled;
            float leftoverDamage = ApplyDamageBasedHit(ref projectedEnemyHealth,
                                                       hitCandidate.EnemyIndex,
                                                       remainingDamage,
                                                       out enemyKilled);

            if (leftoverDamage == remainingDamage)
                continue;

            enemyDirtyFlags[hitCandidate.EnemyIndex] = 1;
            ApplyHitPayloads(shooterEntity,
                             hitCandidate.EnemyIndex,
                             hitCandidate.HitPoint,
                             hitCandidate.HitDirection,
                             remainingDamage - leftoverDamage,
                             projectileTemplate,
                             in referenceSegment,
                             enemyEntities,
                             in enemyPositions,
                             in enemyRuntimeArray,
                             ref projectedEnemyKnockback,
                             ref enemyKnockbackDirtyFlags,
                             in elementalVfxConfigLookup,
                             in elementalVfxAnchorLookup,
                             in enemyHitVfxConfigLookup,
                             in spawnInactivityLockLookup,
                             canEnqueueVfxRequests,
                             ref shooterVfxRequests,
                             ref elementalStackLookup);
            TryScheduleDespawn(enemyEntities[hitCandidate.EnemyIndex],
                               projectedEnemyHealth[hitCandidate.EnemyIndex],
                               in despawnRequestLookup,
                               ref commandBuffer);

            if (!enemyKilled)
                return;

            consumedPenetrations++;
            remainingDamage = leftoverDamage;

            if (consumedPenetrations > maximumPenetrations)
                return;
        }
    }

    private static void ApplyHitPayloads(Entity shooterEntity,
                                         int enemyIndex,
                                         float3 hitPoint,
                                         float3 hitDirection,
                                         float appliedDamage,
                                         PlayerProjectileRequestTemplate projectileTemplate,
                                         in PlayerLaserBeamLaneElement referenceSegment,
                                         NativeArray<Entity> enemyEntities,
                                         in NativeArray<float3> enemyPositions,
                                         in NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                         ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                         ref NativeArray<byte> enemyKnockbackDirtyFlags,
                                         in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                         in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                         in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                         in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                         bool canEnqueueVfxRequests,
                                         ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                         ref BufferLookup<EnemyElementStackElement> elementalStackLookup)
    {
        Projectile projectileForPayloads = new Projectile
        {
            Velocity = math.normalizesafe(hitDirection, referenceSegment.Direction) * math.max(0f, projectileTemplate.Speed),
            Damage = math.max(0f, appliedDamage),
            ExplosionRadius = math.max(0f, projectileTemplate.ExplosionRadius),
            MaxRange = 0f,
            MaxLifetime = 0f,
            PenetrationMode = ProjectilePenetrationMode.None,
            RemainingPenetrations = 0,
            KnockbackEnabled = projectileTemplate.Knockback.Enabled,
            KnockbackStrength = math.max(0f, projectileTemplate.Knockback.Strength),
            KnockbackDurationSeconds = math.max(0f, projectileTemplate.Knockback.DurationSeconds),
            KnockbackDirectionMode = projectileTemplate.Knockback.DirectionMode,
            KnockbackStackingMode = projectileTemplate.Knockback.StackingMode,
            InheritPlayerSpeed = projectileTemplate.InheritPlayerSpeed
        };
        LocalTransform projectileTransform = LocalTransform.FromPositionRotationScale(hitPoint,
                                                                                     quaternion.identity,
                                                                                     math.max(0.01f, referenceSegment.CollisionRadius / PlayerLaserBeamUtility.BaseProjectileRadius));

        if (EnemyHitPayloadRuntimeUtility.ApplyEnemyHitPayloads(enemyIndex,
                                                                shooterEntity,
                                                                hitPoint,
                                                                in projectileForPayloads,
                                                                in projectileTransform,
                                                                in projectileTemplate.ElementalPayloadOverride,
                                                                enemyEntities,
                                                                enemyPositions,
                                                                enemyRuntimeArray,
                                                                ref projectedEnemyKnockback,
                                                                in elementalVfxConfigLookup,
                                                                in elementalVfxAnchorLookup,
                                                                in enemyHitVfxConfigLookup,
                                                                in spawnInactivityLockLookup,
                                                                canEnqueueVfxRequests,
                                                                ref shooterVfxRequests,
                                                                ref elementalStackLookup))
        {
            enemyKnockbackDirtyFlags[enemyIndex] = 1;
        }
    }

    private static bool TryApplyFlatDamageHit(ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                              int enemyIndex,
                                              float damage,
                                              out bool enemyKilled)
    {
        enemyKilled = false;

        if (enemyIndex < 0 || enemyIndex >= projectedEnemyHealth.Length)
            return false;

        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return false;

        EnemyDamageUtility.ApplyFlatShieldDamage(ref enemyHealth, damage);
        enemyKilled = enemyHealth.Current <= 0f;
        projectedEnemyHealth[enemyIndex] = enemyHealth;
        return true;
    }

    private static float ApplyDamageBasedHit(ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                             int enemyIndex,
                                             float damage,
                                             out bool enemyKilled)
    {
        enemyKilled = false;

        if (enemyIndex < 0 || enemyIndex >= projectedEnemyHealth.Length)
            return damage;

        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return damage;

        float leftoverDamage = EnemyDamageUtility.ConsumeFlatShieldDamage(ref enemyHealth, damage);
        enemyKilled = enemyHealth.Current <= 0f;
        projectedEnemyHealth[enemyIndex] = enemyHealth;
        return leftoverDamage;
    }

    private static void TryScheduleDespawn(Entity enemyEntity,
                                           EnemyHealth enemyHealth,
                                           in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                           ref EntityCommandBuffer commandBuffer)
    {
        if (enemyHealth.Current > 0f)
            return;

        if (despawnRequestLookup.HasComponent(enemyEntity))
            return;

        commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
        {
            Reason = EnemyDespawnReason.Killed
        });
    }

    private static float3 ClosestPointOnSegment(float3 point,
                                                float3 segmentStart,
                                                float3 segmentEnd)
    {
        float3 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = math.lengthsq(segment);

        if (segmentLengthSquared <= 1e-6f)
            return segmentStart;

        float projection = math.dot(point - segmentStart, segment) / segmentLengthSquared;
        float clampedProjection = math.clamp(projection, 0f, 1f);
        return segmentStart + segment * clampedProjection;
    }
    #endregion

    #endregion
}
