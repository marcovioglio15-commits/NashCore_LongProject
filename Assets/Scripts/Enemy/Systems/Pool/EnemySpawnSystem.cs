using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns pooled enemies from active spawners.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyPoolInitializeSystem))]
public partial struct EnemySpawnSystem : ISystem
{
    #region Constants
    private const int MaxCatchUpTicksPerFrame = 8;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemySpawner>();
        state.RequireForUpdate<EnemySpawnerState>();
        state.RequireForUpdate<EnemyPoolElement>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRO<EnemySpawner> spawner,
                  RefRW<EnemySpawnerState> spawnerState,
                  RefRO<LocalTransform> spawnerTransform,
                  Entity spawnerEntity) in SystemAPI.Query<RefRO<EnemySpawner>, RefRW<EnemySpawnerState>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            EnemySpawner currentSpawner = spawner.ValueRO;
            EnemySpawnerState currentState = spawnerState.ValueRO;

            if (currentState.Initialized == 0)
                continue;

            if (currentSpawner.SpawnPerTick <= 0)
                continue;

            if (currentSpawner.MaxAliveCount <= 0)
                continue;

            if (elapsedTime < currentState.NextSpawnTime)
                continue;

            float spawnInterval = math.max(0.01f, currentSpawner.SpawnInterval);
            int dueTicks = CalculateDueTicks(elapsedTime, currentState.NextSpawnTime, spawnInterval);
            currentState.NextSpawnTime += spawnInterval * dueTicks;

            int availableSlots = currentSpawner.MaxAliveCount - currentState.AliveCount;

            if (availableSlots <= 0)
            {
                spawnerState.ValueRW = currentState;
                continue;
            }

            int requestedSpawnCount = dueTicks * currentSpawner.SpawnPerTick;
            int spawnCount = math.min(availableSlots, requestedSpawnCount);

            if (spawnCount <= 0)
            {
                spawnerState.ValueRW = currentState;
                continue;
            }

            if (entityManager.HasBuffer<EnemyPoolElement>(spawnerEntity) == false)
            {
                spawnerState.ValueRW = currentState;
                continue;
            }

            Entity enemyPrefab = currentSpawner.EnemyPrefab;

            if (enemyPrefab == Entity.Null)
            {
                spawnerState.ValueRW = currentState;
                continue;
            }

            if (entityManager.Exists(enemyPrefab) == false)
            {
                spawnerState.ValueRW = currentState;
                continue;
            }

            DynamicBuffer<EnemyPoolElement> pool = entityManager.GetBuffer<EnemyPoolElement>(spawnerEntity);
            int missingCount = spawnCount - pool.Length;

            if (missingCount > 0)
            {
                int expandBatch = math.max(1, currentSpawner.ExpandBatch);
                int expandCount = math.max(expandBatch, missingCount);
                EnemyPoolUtility.ExpandPool(entityManager, spawnerEntity, enemyPrefab, expandCount);
                pool = entityManager.GetBuffer<EnemyPoolElement>(spawnerEntity);
            }

            uint randomState = currentState.RandomState;

            if (randomState == 0u)
                randomState = 1u;

            Unity.Mathematics.Random random = new Unity.Mathematics.Random(randomState);
            float3 spawnerPosition = spawnerTransform.ValueRO.Position;
            int spawnedCount = 0;

            for (int index = 0; index < spawnCount; index++)
            {
                if (pool.Length == 0)
                    break;

                int lastPoolIndex = pool.Length - 1;
                Entity enemyEntity = pool[lastPoolIndex].EnemyEntity;
                pool.RemoveAt(lastPoolIndex);

                if (entityManager.Exists(enemyEntity) == false)
                    continue;

                EnemyPoolUtility.EnsureEnemyComponents(entityManager, enemyEntity);

                if (entityManager.HasComponent<LocalTransform>(enemyEntity) == false)
                    continue;

                if (entityManager.HasComponent<EnemyRuntimeState>(enemyEntity) == false)
                    continue;

                if (entityManager.HasComponent<EnemyOwnerSpawner>(enemyEntity) == false)
                    continue;

                LocalTransform enemyTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
                enemyTransform.Position = ResolveSpawnPosition(ref random, spawnerPosition, currentSpawner.SpawnRadius, currentSpawner.SpawnHeightOffset);
                entityManager.SetComponentData(enemyEntity, enemyTransform);

                EnemyRuntimeState runtimeState = entityManager.GetComponentData<EnemyRuntimeState>(enemyEntity);
                runtimeState.Velocity = float3.zero;
                runtimeState.ContactDamageCooldown = 0f;
                runtimeState.AreaDamageCooldown = 0f;

                unchecked
                {
                    runtimeState.SpawnVersion++;
                }

                if (runtimeState.SpawnVersion == 0u)
                    runtimeState.SpawnVersion = 1u;

                entityManager.SetComponentData(enemyEntity, runtimeState);

                if (entityManager.HasComponent<EnemyPatternRuntimeState>(enemyEntity))
                {
                    EnemyPatternRuntimeState patternRuntimeState = entityManager.GetComponentData<EnemyPatternRuntimeState>(enemyEntity);
                    patternRuntimeState.WanderTargetPosition = float3.zero;
                    patternRuntimeState.WanderWaitTimer = 0f;
                    patternRuntimeState.WanderRetryTimer = 0f;
                    patternRuntimeState.LastWanderDirectionAngle = 0f;
                    patternRuntimeState.WanderHasTarget = 0;
                    patternRuntimeState.WanderInitialized = 0;
                    patternRuntimeState.DvdDirection = float3.zero;
                    patternRuntimeState.DvdInitialized = 0;
                    entityManager.SetComponentData(enemyEntity, patternRuntimeState);
                }

                if (entityManager.HasComponent<EnemyShooterControlState>(enemyEntity))
                {
                    EnemyShooterControlState shooterControlState = entityManager.GetComponentData<EnemyShooterControlState>(enemyEntity);
                    shooterControlState.MovementLocked = 0;
                    entityManager.SetComponentData(enemyEntity, shooterControlState);
                }

                if (entityManager.HasBuffer<EnemyShooterRuntimeElement>(enemyEntity))
                {
                    DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = entityManager.GetBuffer<EnemyShooterRuntimeElement>(enemyEntity);
                    int shooterCount = 0;

                    if (entityManager.HasBuffer<EnemyShooterConfigElement>(enemyEntity))
                        shooterCount = entityManager.GetBuffer<EnemyShooterConfigElement>(enemyEntity).Length;

                    shooterRuntime.Clear();

                    for (int shooterIndex = 0; shooterIndex < shooterCount; shooterIndex++)
                    {
                        shooterRuntime.Add(new EnemyShooterRuntimeElement
                        {
                            NextBurstTimer = 0f,
                            NextShotInBurstTimer = 0f,
                            RemainingBurstShots = 0,
                            LockedAimDirection = float3.zero,
                            HasLockedAimDirection = 0
                        });
                    }
                }

                EnemyOwnerSpawner ownerSpawner = entityManager.GetComponentData<EnemyOwnerSpawner>(enemyEntity);
                ownerSpawner.SpawnerEntity = spawnerEntity;
                entityManager.SetComponentData(enemyEntity, ownerSpawner);

                if (entityManager.HasComponent<EnemyHealth>(enemyEntity))
                {
                    EnemyHealth enemyHealth = entityManager.GetComponentData<EnemyHealth>(enemyEntity);

                    if (enemyHealth.Max < 1f)
                        enemyHealth.Max = 1f;

                    if (enemyHealth.MaxShield < 0f)
                        enemyHealth.MaxShield = 0f;

                    enemyHealth.Current = enemyHealth.Max;
                    enemyHealth.CurrentShield = enemyHealth.MaxShield;
                    entityManager.SetComponentData(enemyEntity, enemyHealth);
                }

                if (entityManager.HasBuffer<EnemyElementStackElement>(enemyEntity))
                {
                    DynamicBuffer<EnemyElementStackElement> elementalStacks = entityManager.GetBuffer<EnemyElementStackElement>(enemyEntity);
                    elementalStacks.Clear();
                }

                if (entityManager.HasComponent<EnemyElementalRuntimeState>(enemyEntity))
                {
                    EnemyElementalRuntimeState elementalRuntimeState = entityManager.GetComponentData<EnemyElementalRuntimeState>(enemyEntity);
                    elementalRuntimeState.SlowPercent = 0f;
                    entityManager.SetComponentData(enemyEntity, elementalRuntimeState);
                }

                EnemyPoolUtility.ResetVisualRuntimeState(entityManager, enemyEntity, 1);

                if (entityManager.HasComponent<EnemyDespawnRequest>(enemyEntity))
                    entityManager.RemoveComponent<EnemyDespawnRequest>(enemyEntity);

                entityManager.SetComponentEnabled<EnemyActive>(enemyEntity, true);
                spawnedCount++;
            }

            currentState.AliveCount += spawnedCount;
            currentState.RandomState = random.state;

            if (currentState.RandomState == 0u)
                currentState.RandomState = 1u;

            spawnerState.ValueRW = currentState;
        }
    }
    #endregion

    #region Spawn Helpers
    private static int CalculateDueTicks(float elapsedTime, float nextSpawnTime, float spawnInterval)
    {
        float timePastNextSpawn = elapsedTime - nextSpawnTime;

        if (timePastNextSpawn <= 0f)
            return 1;

        int dueTicks = (int)math.floor(timePastNextSpawn / spawnInterval) + 1;
        return math.clamp(dueTicks, 1, MaxCatchUpTicksPerFrame);
    }

    private static float3 ResolveSpawnPosition(ref Unity.Mathematics.Random random, float3 spawnerPosition, float spawnRadius, float spawnHeightOffset)
    {
        float clampedRadius = math.max(0f, spawnRadius);

        if (clampedRadius <= 0f)
        {
            float3 exactPosition = spawnerPosition;
            exactPosition.y += spawnHeightOffset;
            return exactPosition;
        }

        float angle = random.NextFloat(0f, math.PI * 2f);
        float radialDistance = math.sqrt(random.NextFloat()) * clampedRadius;
        float2 planarOffset = new float2(math.cos(angle), math.sin(angle)) * radialDistance;

        float3 spawnPosition = spawnerPosition;
        spawnPosition.x += planarOffset.x;
        spawnPosition.z += planarOffset.y;
        spawnPosition.y += spawnHeightOffset;
        return spawnPosition;
    }
    #endregion

    #endregion
}
