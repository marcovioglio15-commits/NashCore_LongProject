using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies bomb explosion damage and despawn requests to enemies, then destroys bomb entities.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerBombFuseSystem))]
public partial struct PlayerBombExplosionSystem : ISystem
{
    #region Fields
    private EntityQuery bombQuery;
    private EntityQuery enemyQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        bombQuery = SystemAPI.QueryBuilder()
            .WithAll<BombFuseState, BombExplodeRequest>()
            .Build();

        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        state.RequireForUpdate(bombQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        NativeArray<Entity> bombEntities = bombQuery.ToEntityArray(Allocator.Temp);
        NativeArray<BombFuseState> bombFuseStates = bombQuery.ToComponentDataArray<BombFuseState>(Allocator.Temp);

        int enemyCount = enemyQuery.CalculateEntityCount();
        NativeArray<Entity> enemyEntities = default;
        NativeArray<EnemyData> enemyDataArray = default;
        NativeArray<EnemyHealth> enemyHealthArray = default;
        NativeArray<LocalTransform> enemyTransforms = default;
        NativeArray<float3> enemyPositions = default;
        NativeArray<float> enemyBodyRadii = default;
        NativeArray<byte> enemyDirtyFlags = default;
        NativeParallelMultiHashMap<int, int> enemyCellMap = default;
        float inverseCellSize = 0f;
        float maximumEnemyRadius = 0.05f;

        if (enemyCount > 0)
        {
            enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
            enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(Allocator.Temp);
            enemyHealthArray = enemyQuery.ToComponentDataArray<EnemyHealth>(Allocator.Temp);
            enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            enemyPositions = new NativeArray<float3>(enemyCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            enemyBodyRadii = new NativeArray<float>(enemyCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            enemyDirtyFlags = new NativeArray<byte>(enemyCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
            enemyCellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.Temp);

            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                enemyPositions[enemyIndex] = enemyTransforms[enemyIndex].Position;
                float bodyRadius = math.max(0f, enemyDataArray[enemyIndex].BodyRadius);
                enemyBodyRadii[enemyIndex] = bodyRadius;

                if (bodyRadius > maximumEnemyRadius)
                    maximumEnemyRadius = bodyRadius;
            }

            float cellSize = EnemySpatialHashUtility.ResolveCellSize(maximumEnemyRadius);
            inverseCellSize = 1f / cellSize;
            EnemySpatialHashUtility.BuildCellMap(in enemyPositions, inverseCellSize, ref enemyCellMap);
        }

        for (int bombIndex = 0; bombIndex < bombEntities.Length; bombIndex++)
        {
            BombFuseState fuseState = bombFuseStates[bombIndex];

            if (enemyCount > 0)
                ApplyExplosionToEnemies(entityManager,
                                        in fuseState,
                                        enemyCount,
                                        in enemyEntities,
                                        ref enemyHealthArray,
                                        in enemyPositions,
                                        in enemyBodyRadii,
                                        ref enemyDirtyFlags,
                                        in enemyCellMap,
                                        inverseCellSize,
                                        maximumEnemyRadius,
                                        ref commandBuffer);

            EnqueueExplosionVfxRequest(in fuseState, in localTransformLookup, ref vfxRequestLookup);

            Entity bombEntity = bombEntities[bombIndex];

            if (entityManager.Exists(bombEntity))
                commandBuffer.DestroyEntity(bombEntity);
        }

        if (enemyCount > 0)
        {
            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                if (enemyDirtyFlags[enemyIndex] == 0)
                    continue;

                Entity enemyEntity = enemyEntities[enemyIndex];

                if (entityManager.Exists(enemyEntity) == false)
                    continue;

                entityManager.SetComponentData(enemyEntity, enemyHealthArray[enemyIndex]);
            }

            enemyEntities.Dispose();
            enemyDataArray.Dispose();
            enemyHealthArray.Dispose();
            enemyTransforms.Dispose();
            enemyPositions.Dispose();
            enemyBodyRadii.Dispose();
            enemyDirtyFlags.Dispose();
            enemyCellMap.Dispose();
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
        bombEntities.Dispose();
        bombFuseStates.Dispose();
    }
    #endregion

    #region Helpers
    private static void EnqueueExplosionVfxRequest(in BombFuseState fuseState,
                                                   in ComponentLookup<LocalTransform> localTransformLookup,
                                                   ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (fuseState.OwnerEntity == Entity.Null)
            return;

        if (fuseState.ExplosionVfxPrefabEntity == Entity.Null)
            return;

        if (vfxRequestLookup.HasBuffer(fuseState.OwnerEntity) == false)
            return;

        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = vfxRequestLookup[fuseState.OwnerEntity];
        float scaleMultiplier = math.max(0.01f, fuseState.VfxScaleMultiplier);

        if (fuseState.ScaleVfxToRadius != 0)
            scaleMultiplier *= math.max(0.1f, fuseState.Radius);

        float3 explosionVfxPosition = fuseState.Position;

        if (localTransformLookup.HasComponent(fuseState.OwnerEntity))
        {
            float ownerFloorReferenceY = localTransformLookup[fuseState.OwnerEntity].Position.y;

            if (explosionVfxPosition.y < ownerFloorReferenceY)
                explosionVfxPosition.y = ownerFloorReferenceY;
        }

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = fuseState.ExplosionVfxPrefabEntity,
            Position = explosionVfxPosition,
            Rotation = quaternion.identity,
            UniformScale = scaleMultiplier,
            LifetimeSeconds = 2f,
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = Entity.Null,
            FollowValidationSpawnVersion = 0u,
            Velocity = float3.zero
        });
    }

    private static void ApplyExplosionToEnemies(EntityManager entityManager,
                                                in BombFuseState fuseState,
                                                int enemyCount,
                                                in NativeArray<Entity> enemyEntities,
                                                ref NativeArray<EnemyHealth> enemyHealthArray,
                                                in NativeArray<float3> enemyPositions,
                                                in NativeArray<float> enemyBodyRadii,
                                                ref NativeArray<byte> enemyDirtyFlags,
                                                in NativeParallelMultiHashMap<int, int> enemyCellMap,
                                                float inverseCellSize,
                                                float maximumEnemyRadius,
                                                ref EntityCommandBuffer commandBuffer)
    {
        float explosionRadius = math.max(0.1f, fuseState.Radius);
        float explosionRadiusSquared = explosionRadius * explosionRadius;
        float explosionDamage = math.max(0f, fuseState.Damage);

        if (explosionDamage <= 0f)
            return;

        if (fuseState.AffectAllEnemiesInRadius != 0)
        {
            float queryRadius = explosionRadius + math.max(0f, maximumEnemyRadius);
            EnemySpatialHashUtility.ResolveCellBounds(fuseState.Position,
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

                    if (enemyCellMap.TryGetFirstValue(cellKey, out enemyIndex, out iterator) == false)
                        continue;

                    do
                    {
                        if (enemyIndex < 0 || enemyIndex >= enemyCount)
                            continue;

                        ApplyExplosionDamageToEnemy(entityManager,
                                                    in fuseState,
                                                    enemyIndex,
                                                    explosionRadiusSquared,
                                                    explosionDamage,
                                                    in enemyEntities,
                                                    ref enemyHealthArray,
                                                    in enemyPositions,
                                                    in enemyBodyRadii,
                                                    ref enemyDirtyFlags,
                                                    ref commandBuffer);
                    }
                    while (enemyCellMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            return;
        }

        int closestEnemyIndex = -1;
        float closestDistanceSquared = float.MaxValue;
        float closestQueryRadius = explosionRadius + math.max(0f, maximumEnemyRadius);
        EnemySpatialHashUtility.ResolveCellBounds(fuseState.Position,
                                                  closestQueryRadius,
                                                  inverseCellSize,
                                                  out int closestMinCellX,
                                                  out int closestMaxCellX,
                                                  out int closestMinCellY,
                                                  out int closestMaxCellY);

        for (int cellX = closestMinCellX; cellX <= closestMaxCellX; cellX++)
        {
            for (int cellY = closestMinCellY; cellY <= closestMaxCellY; cellY++)
            {
                int cellKey = EnemySpatialHashUtility.EncodeCell(cellX, cellY);
                NativeParallelMultiHashMapIterator<int> iterator;
                int enemyIndex;

                if (enemyCellMap.TryGetFirstValue(cellKey, out enemyIndex, out iterator) == false)
                    continue;

                do
                {
                    if (enemyIndex < 0 || enemyIndex >= enemyCount)
                        continue;

                    float3 enemyPosition = enemyPositions[enemyIndex];
                    float3 delta = enemyPosition - fuseState.Position;
                    delta.y = 0f;
                    float sqrDistance = math.lengthsq(delta);
                    float bodyRadius = enemyBodyRadii[enemyIndex];
                    float bodyRadiusSquared = bodyRadius * bodyRadius;

                    if (sqrDistance > explosionRadiusSquared + bodyRadiusSquared)
                        continue;

                    if (sqrDistance >= closestDistanceSquared)
                        continue;

                    closestDistanceSquared = sqrDistance;
                    closestEnemyIndex = enemyIndex;
                }
                while (enemyCellMap.TryGetNextValue(out enemyIndex, ref iterator));
            }
        }

        if (closestEnemyIndex < 0)
            return;

        ApplyExplosionDamageToEnemy(entityManager,
                                    in fuseState,
                                    closestEnemyIndex,
                                    explosionRadiusSquared,
                                    explosionDamage,
                                    in enemyEntities,
                                    ref enemyHealthArray,
                                    in enemyPositions,
                                    in enemyBodyRadii,
                                    ref enemyDirtyFlags,
                                    ref commandBuffer);
    }

    private static void ApplyExplosionDamageToEnemy(EntityManager entityManager,
                                                    in BombFuseState fuseState,
                                                    int enemyIndex,
                                                    float explosionRadiusSquared,
                                                    float explosionDamage,
                                                    in NativeArray<Entity> enemyEntities,
                                                    ref NativeArray<EnemyHealth> enemyHealthArray,
                                                    in NativeArray<float3> enemyPositions,
                                                    in NativeArray<float> enemyBodyRadii,
                                                    ref NativeArray<byte> enemyDirtyFlags,
                                                    ref EntityCommandBuffer commandBuffer)
    {
        Entity enemyEntity = enemyEntities[enemyIndex];

        if (entityManager.Exists(enemyEntity) == false)
            return;

        float3 enemyPosition = enemyPositions[enemyIndex];
        float3 delta = enemyPosition - fuseState.Position;
        delta.y = 0f;
        float sqrDistance = math.lengthsq(delta);
        float bodyRadius = math.max(0f, enemyBodyRadii[enemyIndex]);
        float bodyRadiusSquared = bodyRadius * bodyRadius;

        if (sqrDistance > explosionRadiusSquared + bodyRadiusSquared)
            return;

        EnemyHealth enemyHealth = enemyHealthArray[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return;

        EnemyDamageUtility.ApplyFlatShieldDamage(ref enemyHealth, explosionDamage);
        enemyHealthArray[enemyIndex] = enemyHealth;
        enemyDirtyFlags[enemyIndex] = 1;

        if (enemyHealth.Current > 0f)
            return;

        commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
        {
            Reason = EnemyDespawnReason.Killed
        });
    }
    #endregion

    #endregion
}
