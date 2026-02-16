using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves passive explosion requests by damaging enemies in radius and scheduling optional VFX.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyProjectileHitSystem))]
[UpdateBefore(typeof(EnemyContactDamageSystem))]
public partial struct PlayerPassiveExplosionResolveSystem : ISystem
{
    #region Fields
    private EntityQuery enemyQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerExplosionRequest>();

        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        int enemyCount = enemyQuery.CalculateEntityCount();

        if (enemyCount <= 0)
        {
            ClearRequestsWithoutEnemies(ref state);
            return;
        }

        EntityManager entityManager = state.EntityManager;
        NativeArray<Entity> enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
        NativeArray<EnemyData> enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(Allocator.Temp);
        NativeArray<EnemyHealth> enemyHealthArray = enemyQuery.ToComponentDataArray<EnemyHealth>(Allocator.Temp);
        NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        NativeArray<byte> enemyDirtyFlags = new NativeArray<byte>(enemyCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
        ComponentLookup<EnemyDespawnRequest> despawnRequestLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((DynamicBuffer<PlayerExplosionRequest> explosionRequests,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
                 in SystemAPI.Query<DynamicBuffer<PlayerExplosionRequest>, DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>())
        {
            for (int requestIndex = 0; requestIndex < explosionRequests.Length; requestIndex++)
            {
                PlayerExplosionRequest explosionRequest = explosionRequests[requestIndex];
                ApplyExplosionRequest(in explosionRequest,
                                      enemyCount,
                                      in enemyEntities,
                                      in enemyDataArray,
                                      ref enemyHealthArray,
                                      in enemyTransforms,
                                      ref enemyDirtyFlags,
                                      in despawnRequestLookup,
                                      ref commandBuffer);
                EnqueueExplosionVfxRequest(in explosionRequest, vfxRequests);
            }

            explosionRequests.Clear();
        }

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            if (enemyDirtyFlags[enemyIndex] == 0)
                continue;

            Entity enemyEntity = enemyEntities[enemyIndex];

            if (entityManager.Exists(enemyEntity) == false)
                continue;

            entityManager.SetComponentData(enemyEntity, enemyHealthArray[enemyIndex]);
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
        enemyEntities.Dispose();
        enemyDataArray.Dispose();
        enemyHealthArray.Dispose();
        enemyTransforms.Dispose();
        enemyDirtyFlags.Dispose();
    }
    #endregion

    #region Helpers
    private void ClearRequestsWithoutEnemies(ref SystemState state)
    {
        foreach ((DynamicBuffer<PlayerExplosionRequest> explosionRequests,
                  DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
                 in SystemAPI.Query<DynamicBuffer<PlayerExplosionRequest>, DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>())
        {
            for (int requestIndex = 0; requestIndex < explosionRequests.Length; requestIndex++)
            {
                PlayerExplosionRequest explosionRequest = explosionRequests[requestIndex];
                EnqueueExplosionVfxRequest(in explosionRequest, vfxRequests);
            }

            explosionRequests.Clear();
        }
    }

    private static void ApplyExplosionRequest(in PlayerExplosionRequest explosionRequest,
                                              int enemyCount,
                                              in NativeArray<Entity> enemyEntities,
                                              in NativeArray<EnemyData> enemyDataArray,
                                              ref NativeArray<EnemyHealth> enemyHealthArray,
                                              in NativeArray<LocalTransform> enemyTransforms,
                                              ref NativeArray<byte> enemyDirtyFlags,
                                              in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                              ref EntityCommandBuffer commandBuffer)
    {
        float radius = math.max(0f, explosionRequest.Radius);
        float damage = math.max(0f, explosionRequest.Damage);

        if (radius <= 0f || damage <= 0f)
            return;

        float radiusSquared = radius * radius;

        if (explosionRequest.AffectAllEnemiesInRadius != 0)
        {
            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                TryDamageEnemy(enemyIndex,
                               radiusSquared,
                               damage,
                               explosionRequest.Position,
                               in enemyEntities,
                               in enemyDataArray,
                               ref enemyHealthArray,
                               in enemyTransforms,
                               ref enemyDirtyFlags,
                               in despawnRequestLookup,
                               ref commandBuffer);
            }

            return;
        }

        int closestEnemyIndex = -1;
        float closestDistanceSquared = float.MaxValue;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            float3 enemyPosition = enemyTransforms[enemyIndex].Position;
            float3 delta = enemyPosition - explosionRequest.Position;
            delta.y = 0f;
            float distanceSquared = math.lengthsq(delta);

            if (distanceSquared > radiusSquared)
                continue;

            if (distanceSquared >= closestDistanceSquared)
                continue;

            closestDistanceSquared = distanceSquared;
            closestEnemyIndex = enemyIndex;
        }

        if (closestEnemyIndex < 0)
            return;

        TryDamageEnemy(closestEnemyIndex,
                       radiusSquared,
                       damage,
                       explosionRequest.Position,
                       in enemyEntities,
                       in enemyDataArray,
                       ref enemyHealthArray,
                       in enemyTransforms,
                       ref enemyDirtyFlags,
                       in despawnRequestLookup,
                       ref commandBuffer);
    }

    private static void TryDamageEnemy(int enemyIndex,
                                       float radiusSquared,
                                       float damage,
                                       float3 explosionPosition,
                                       in NativeArray<Entity> enemyEntities,
                                       in NativeArray<EnemyData> enemyDataArray,
                                       ref NativeArray<EnemyHealth> enemyHealthArray,
                                       in NativeArray<LocalTransform> enemyTransforms,
                                       ref NativeArray<byte> enemyDirtyFlags,
                                       in ComponentLookup<EnemyDespawnRequest> despawnRequestLookup,
                                       ref EntityCommandBuffer commandBuffer)
    {
        Entity enemyEntity = enemyEntities[enemyIndex];
        float3 enemyPosition = enemyTransforms[enemyIndex].Position;
        float3 delta = enemyPosition - explosionPosition;
        delta.y = 0f;
        float distanceSquared = math.lengthsq(delta);
        float bodyRadius = math.max(0f, enemyDataArray[enemyIndex].BodyRadius);
        float bodyRadiusSquared = bodyRadius * bodyRadius;

        if (distanceSquared > radiusSquared + bodyRadiusSquared)
            return;

        EnemyHealth enemyHealth = enemyHealthArray[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return;

        enemyHealth.Current -= damage;

        if (enemyHealth.Current < 0f)
            enemyHealth.Current = 0f;

        enemyHealthArray[enemyIndex] = enemyHealth;
        enemyDirtyFlags[enemyIndex] = 1;

        if (enemyHealth.Current > 0f)
            return;

        if (despawnRequestLookup.HasComponent(enemyEntity))
            return;

        commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
        {
            Reason = EnemyDespawnReason.Killed
        });
    }

    private static void EnqueueExplosionVfxRequest(in PlayerExplosionRequest explosionRequest, DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
    {
        if (explosionRequest.ExplosionVfxPrefabEntity == Entity.Null)
            return;

        float scaleMultiplier = math.max(0.01f, explosionRequest.VfxScaleMultiplier);

        if (explosionRequest.ScaleVfxToRadius != 0)
            scaleMultiplier *= math.max(0.1f, explosionRequest.Radius);

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = explosionRequest.ExplosionVfxPrefabEntity,
            Position = explosionRequest.Position,
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
    #endregion

    #endregion
}
