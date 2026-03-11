using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns pooled experience drops for enemies killed in the current frame.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyKilledEventsSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyExperienceDropSpawnSystem : ISystem
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    private const float TwoPi = 6.283185307179586f;
    private const int MaxSpawnStepsPerEnemy = 4096;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures required components for drop spawning.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyKilledEventElement>();
        state.RequireForUpdate<EnemyDropItemsConfig>();
        state.RequireForUpdate<EnemyExperienceDropPoolRegistry>();
    }

    /// <summary>
    /// Processes killed events and spawns experience drops from configured pools.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.TryGetSingletonEntity<EnemyExperienceDropPoolRegistry>(out Entity registryEntity) == false)
            return;

        EntityManager entityManager = state.EntityManager;

        if (entityManager.HasBuffer<EnemyExperienceDropPoolMapElement>(registryEntity) == false)
            return;

        DynamicBuffer<EnemyExperienceDropPoolMapElement> poolMap = entityManager.GetBuffer<EnemyExperienceDropPoolMapElement>(registryEntity);

        if (poolMap.Length <= 0)
            return;

        if (SystemAPI.TryGetSingletonBuffer<EnemyKilledEventElement>(out DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer) == false)
            return;

        if (killedEventsBuffer.Length <= 0)
            return;

        // Snapshot buffers to keep iteration stable even if later systems mutate the live buffers.
        NativeArray<EnemyExperienceDropPoolMapElement> poolMapSnapshot = poolMap.ToNativeArray(Allocator.Temp);
        NativeArray<EnemyKilledEventElement> killedEventsSnapshot = killedEventsBuffer.ToNativeArray(Allocator.Temp);

        try
        {
            for (int killedIndex = 0; killedIndex < killedEventsSnapshot.Length; killedIndex++)
                SpawnDropsForKilledEnemy(entityManager, poolMapSnapshot, killedEventsSnapshot[killedIndex], killedIndex);
        }
        finally
        {
            if (poolMapSnapshot.IsCreated)
                poolMapSnapshot.Dispose();

            if (killedEventsSnapshot.IsCreated)
                killedEventsSnapshot.Dispose();
        }
    }
    #endregion

    #region Spawn
    private static void SpawnDropsForKilledEnemy(EntityManager entityManager,
                                                 NativeArray<EnemyExperienceDropPoolMapElement> poolMap,
                                                 EnemyKilledEventElement killedEvent,
                                                 int killEventIndex)
    {
        Entity enemyEntity = killedEvent.EnemyEntity;

        if (enemyEntity == Entity.Null || entityManager.Exists(enemyEntity) == false)
            return;

        if (entityManager.HasComponent<EnemyDropItemsConfig>(enemyEntity) == false)
            return;

        if (entityManager.HasBuffer<EnemyExperienceDropDefinitionElement>(enemyEntity) == false)
            return;

        EnemyDropItemsConfig dropItemsConfig = entityManager.GetComponentData<EnemyDropItemsConfig>(enemyEntity);

        if (dropItemsConfig.PayloadKind != EnemyDropItemsPayloadKind.Experience)
            return;

        DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions = entityManager.GetBuffer<EnemyExperienceDropDefinitionElement>(enemyEntity);

        if (definitions.Length <= 0)
            return;

        float minimumTotalExperienceDrop = math.max(0f, dropItemsConfig.MinimumTotalExperienceDrop);
        float maximumTotalExperienceDrop = math.max(minimumTotalExperienceDrop, dropItemsConfig.MaximumTotalExperienceDrop);

        if (maximumTotalExperienceDrop <= 0f)
            return;

        uint randomSeed = ResolveDropTotalRandomSeed(enemyEntity, killEventIndex);
        float resolvedTotalExperienceDrop;

        if (EnemyExperienceDropDistributionUtility.TryResolveRandomCompatibleTotal(definitions,
                                                                                   minimumTotalExperienceDrop,
                                                                                   maximumTotalExperienceDrop,
                                                                                   dropItemsConfig.Distribution,
                                                                                   randomSeed,
                                                                                   out resolvedTotalExperienceDrop) == false)
            return;

        float remainingExperience = resolvedTotalExperienceDrop;
        float dropRadius = math.max(0f, dropItemsConfig.DropRadius);
        float attractionSpeed = math.max(0f, dropItemsConfig.AttractionSpeed);
        float collectDistance = math.max(0.01f, dropItemsConfig.CollectDistance);
        float collectDistancePerPlayerSpeed = math.max(0f, dropItemsConfig.CollectDistancePerPlayerSpeed);
        float spawnAnimationMinDuration = math.max(0f, dropItemsConfig.SpawnAnimationMinDuration);
        float spawnAnimationMaxDuration = math.max(spawnAnimationMinDuration, dropItemsConfig.SpawnAnimationMaxDuration);

        // Greedy decomposition: emit drops until the resolved total is exhausted or no valid definition fits.
        for (int stepIndex = 0; stepIndex < MaxSpawnStepsPerEnemy; stepIndex++)
        {
            if (remainingExperience <= PrecisionEpsilon)
                break;

            int definitionIndex;
            float definitionAmount;
            bool fitsRemaining;

            if (EnemyExperienceDropDistributionUtility.TryResolveNextDefinition(definitions,
                                                                                remainingExperience,
                                                                                dropItemsConfig.Distribution,
                                                                                out definitionIndex,
                                                                                out definitionAmount,
                                                                                out fitsRemaining) == false)
                break;

            if (definitionIndex < 0 || definitionAmount <= 0f)
                break;

            EnemyExperienceDropDefinitionElement definition = definitions[definitionIndex];
            Entity poolEntity;

            if (EnemyExperienceDropPoolUtility.TryResolvePoolEntity(poolMap, definition.PrefabEntity, out poolEntity) == false)
                break;

            Entity dropEntity;

            if (EnemyExperienceDropPoolUtility.TryAcquireDrop(entityManager, poolEntity, out dropEntity) == false)
                break;

            float3 spawnCenterPosition = killedEvent.Position;
            float3 spawnTargetPosition = ResolveDropSpawnPosition(spawnCenterPosition, stepIndex, dropRadius);
            float spawnAnimationDuration = ResolveSpawnAnimationDuration(stepIndex, spawnAnimationMinDuration, spawnAnimationMaxDuration);
            LocalTransform dropTransform = entityManager.GetComponentData<LocalTransform>(dropEntity);
            dropTransform.Position = spawnCenterPosition;
            entityManager.SetComponentData(dropEntity, dropTransform);

            EnemyExperienceDrop dropData = entityManager.GetComponentData<EnemyExperienceDrop>(dropEntity);
            dropData.ExperienceAmount = definitionAmount;
            dropData.AttractionSpeed = attractionSpeed;
            dropData.CollectDistance = collectDistance;
            dropData.CollectDistancePerPlayerSpeed = collectDistancePerPlayerSpeed;
            dropData.SpawnStartPosition = spawnCenterPosition;
            dropData.SpawnTargetPosition = spawnTargetPosition;
            dropData.SpawnAnimationDuration = spawnAnimationDuration;
            dropData.SpawnAnimationElapsed = 0f;
            dropData.PoolEntity = poolEntity;
            dropData.IsAttracting = 0;
            entityManager.SetComponentData(dropEntity, dropData);
            entityManager.SetComponentEnabled<EnemyExperienceDropActive>(dropEntity, true);

            if (fitsRemaining == false)
                break;

            remainingExperience -= definitionAmount;
        }
    }
    #endregion

    #region Helpers
    private static uint ResolveDropTotalRandomSeed(Entity enemyEntity, int killEventIndex)
    {
        int sanitizedKillEventIndex = math.max(0, killEventIndex);
        uint seed = math.hash(new int4(enemyEntity.Index,
                                       enemyEntity.Version,
                                       sanitizedKillEventIndex + 1,
                                       19349663));

        if (seed == 0u)
            return 1u;

        return seed;
    }

    private static float3 ResolveDropSpawnPosition(float3 centerPosition, int dropIndex, float dropRadius)
    {
        if (dropRadius <= PrecisionEpsilon)
            return centerPosition;

        // Deterministic low-discrepancy samples spread drops without clustering and without per-frame RNG state.
        int sampleIndex = dropIndex + 1;
        float radialSample = ResolveHaltonSequence(sampleIndex, 2);
        float angularSample = ResolveHaltonSequence(sampleIndex, 3);
        float radius = dropRadius * math.sqrt(math.saturate(radialSample));
        float angleRadians = TwoPi * angularSample;
        float2 offset = new float2(math.cos(angleRadians), math.sin(angleRadians)) * radius;
        float3 position = centerPosition;
        position.x += offset.x;
        position.z += offset.y;
        return position;
    }

    private static float ResolveHaltonSequence(int index, int radix)
    {
        float result = 0f;
        float fraction = 1f / radix;
        int remaining = index;

        while (remaining > 0)
        {
            int digit = remaining % radix;
            result += fraction * digit;
            remaining /= radix;
            fraction /= radix;
        }

        return result;
    }

    private static float ResolveSpawnAnimationDuration(int dropIndex, float minimumDuration, float maximumDuration)
    {
        int sampleIndex = dropIndex + 1;
        float durationSample = ResolveHaltonSequence(sampleIndex, 5);
        return math.lerp(minimumDuration, maximumDuration, durationSample);
    }
    #endregion

    #endregion
}
