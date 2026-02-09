using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves projectile hits against active enemies using a spatial hash.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySteeringSystem))]
public partial struct EnemyProjectileHitSystem : ISystem
{
    #region Fields
    private EntityQuery enemyQuery;
    private EntityQuery projectileQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        projectileQuery = SystemAPI.QueryBuilder()
            .WithAll<Projectile, ProjectileOwner, LocalTransform, ProjectileActive>()
            .Build();

        state.RequireForUpdate(enemyQuery);
        state.RequireForUpdate(projectileQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        int enemyCount = enemyQuery.CalculateEntityCount();

        if (enemyCount <= 0)
            return;

        int projectileCount = projectileQuery.CalculateEntityCount();

        if (projectileCount <= 0)
            return;

        NativeArray<Entity> enemyEntities = enemyQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<EnemyData> enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(Allocator.TempJob);
        NativeArray<EnemyHealth> enemyHealthArray = enemyQuery.ToComponentDataArray<EnemyHealth>(Allocator.TempJob);
        NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        NativeArray<Entity> projectileEntities = projectileQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<Projectile> projectileDataArray = projectileQuery.ToComponentDataArray<Projectile>(Allocator.TempJob);
        NativeArray<ProjectileOwner> projectileOwnerArray = projectileQuery.ToComponentDataArray<ProjectileOwner>(Allocator.TempJob);
        NativeArray<LocalTransform> projectileTransforms = projectileQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        NativeArray<float3> enemyPositions = new NativeArray<float3>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> enemyRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        float maxRadius = 0.05f;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            enemyPositions[enemyIndex] = enemyTransforms[enemyIndex].Position;
            float bodyRadius = math.max(0.05f, enemyDataArray[enemyIndex].BodyRadius);
            enemyRadii[enemyIndex] = bodyRadius;

            if (bodyRadius > maxRadius)
                maxRadius = bodyRadius;
        }

        float cellSize = math.max(0.25f, maxRadius);
        float inverseCellSize = 1f / cellSize;
        NativeParallelMultiHashMap<int, int> enemyCellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.TempJob);

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            float3 enemyPosition = enemyPositions[enemyIndex];
            int cellX = (int)math.floor(enemyPosition.x * inverseCellSize);
            int cellY = (int)math.floor(enemyPosition.z * inverseCellSize);
            enemyCellMap.Add(EncodeCell(cellX, cellY), enemyIndex);
        }

        NativeArray<float3> projectilePositions = new NativeArray<float3>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            projectilePositions[projectileIndex] = projectileTransforms[projectileIndex].Position;

        NativeArray<int> hitEnemyIndices = new NativeArray<int>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        EnemyProjectileHitJob hitJob = new EnemyProjectileHitJob
        {
            ProjectilePositions = projectilePositions,
            EnemyPositions = enemyPositions,
            EnemyRadii = enemyRadii,
            CellMap = enemyCellMap,
            InverseCellSize = inverseCellSize,
            HitEnemyIndices = hitEnemyIndices
        };

        JobHandle handle = hitJob.Schedule(projectileCount, 64, state.Dependency);
        handle.Complete();

        NativeArray<float> damageByEnemy = new NativeArray<float>(enemyCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
        BufferLookup<ProjectilePoolElement> projectilePoolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            int enemyIndex = hitEnemyIndices[projectileIndex];

            if (enemyIndex < 0)
                continue;

            if (enemyIndex >= enemyCount)
                continue;

            damageByEnemy[enemyIndex] += math.max(0f, projectileDataArray[projectileIndex].Damage);
            DespawnProjectile(entityManager, projectileEntities[projectileIndex], projectileOwnerArray[projectileIndex], ref projectilePoolLookup);
        }

        ComponentLookup<EnemyDespawnRequest> despawnLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            float damage = damageByEnemy[enemyIndex];

            if (damage <= 0f)
                continue;

            Entity enemyEntity = enemyEntities[enemyIndex];

            if (entityManager.Exists(enemyEntity) == false)
                continue;

            EnemyHealth enemyHealth = enemyHealthArray[enemyIndex];
            enemyHealth.Current -= damage;

            if (enemyHealth.Current < 0f)
                enemyHealth.Current = 0f;

            entityManager.SetComponentData(enemyEntity, enemyHealth);

            if (enemyHealth.Current <= 0f)
            {
                if (despawnLookup.HasComponent(enemyEntity) == false)
                {
                    commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
                    {
                        Reason = EnemyDespawnReason.Killed
                    });
                }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        enemyEntities.Dispose();
        enemyDataArray.Dispose();
        enemyHealthArray.Dispose();
        enemyTransforms.Dispose();

        projectileEntities.Dispose();
        projectileDataArray.Dispose();
        projectileOwnerArray.Dispose();
        projectileTransforms.Dispose();

        enemyPositions.Dispose();
        enemyRadii.Dispose();
        projectilePositions.Dispose();
        hitEnemyIndices.Dispose();
        enemyCellMap.Dispose();
        damageByEnemy.Dispose();
    }
    #endregion

    #region Helpers
    private static int EncodeCell(int x, int y)
    {
        unchecked
        {
            return (x * 73856093) ^ (y * 19349663);
        }
    }

    private static void DespawnProjectile(EntityManager entityManager, Entity projectileEntity, ProjectileOwner projectileOwner, ref BufferLookup<ProjectilePoolElement> projectilePoolLookup)
    {
        if (entityManager.Exists(projectileEntity) == false)
            return;

        ProjectilePoolUtility.SetProjectileParked(entityManager, projectileEntity);
        entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);

        Entity shooterEntity = projectileOwner.ShooterEntity;

        if (projectilePoolLookup.HasBuffer(shooterEntity) == false)
            return;

        DynamicBuffer<ProjectilePoolElement> shooterPool = projectilePoolLookup[shooterEntity];
        shooterPool.Add(new ProjectilePoolElement
        {
            ProjectileEntity = projectileEntity
        });
    }
    #endregion

    #endregion

    #region Jobs
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    private struct EnemyProjectileHitJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> ProjectilePositions;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<float> EnemyRadii;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> CellMap;
        [ReadOnly] public float InverseCellSize;
        public NativeArray<int> HitEnemyIndices;

        public void Execute(int index)
        {
            float3 projectilePosition = ProjectilePositions[index];
            int centerCellX = (int)math.floor(projectilePosition.x * InverseCellSize);
            int centerCellY = (int)math.floor(projectilePosition.z * InverseCellSize);

            int closestEnemyIndex = -1;
            float closestDistanceSquared = float.MaxValue;

            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int key = EncodeCell(centerCellX + offsetX, centerCellY + offsetY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int enemyIndex;

                    if (CellMap.TryGetFirstValue(key, out enemyIndex, out iterator) == false)
                        continue;

                    do
                    {
                        float3 delta = projectilePosition - EnemyPositions[enemyIndex];
                        delta.y = 0f;
                        float sqrDistance = math.lengthsq(delta);
                        float radius = math.max(0.01f, EnemyRadii[enemyIndex]);
                        float radiusSquared = radius * radius;

                        if (sqrDistance > radiusSquared)
                            continue;

                        if (sqrDistance >= closestDistanceSquared)
                            continue;

                        closestDistanceSquared = sqrDistance;
                        closestEnemyIndex = enemyIndex;
                    }
                    while (CellMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            HitEnemyIndices[index] = closestEnemyIndex;
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
}
