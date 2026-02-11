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

        NativeArray<Entity> bombEntities = bombQuery.ToEntityArray(Allocator.Temp);
        NativeArray<BombFuseState> bombFuseStates = bombQuery.ToComponentDataArray<BombFuseState>(Allocator.Temp);

        int enemyCount = enemyQuery.CalculateEntityCount();
        NativeArray<Entity> enemyEntities = default;
        NativeArray<EnemyData> enemyDataArray = default;
        NativeArray<EnemyHealth> enemyHealthArray = default;
        NativeArray<LocalTransform> enemyTransforms = default;
        NativeArray<byte> enemyDirtyFlags = default;

        if (enemyCount > 0)
        {
            enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
            enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(Allocator.Temp);
            enemyHealthArray = enemyQuery.ToComponentDataArray<EnemyHealth>(Allocator.Temp);
            enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            enemyDirtyFlags = new NativeArray<byte>(enemyCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
        }

        for (int bombIndex = 0; bombIndex < bombEntities.Length; bombIndex++)
        {
            BombFuseState fuseState = bombFuseStates[bombIndex];

            if (enemyCount > 0)
                ApplyExplosionToEnemies(ref state,
                                        in fuseState,
                                        enemyCount,
                                        in enemyEntities,
                                        in enemyDataArray,
                                        ref enemyHealthArray,
                                        in enemyTransforms,
                                        ref enemyDirtyFlags);

            Entity bombEntity = bombEntities[bombIndex];

            if (entityManager.Exists(bombEntity))
                entityManager.DestroyEntity(bombEntity);
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
            enemyDirtyFlags.Dispose();
        }

        bombEntities.Dispose();
        bombFuseStates.Dispose();
    }
    #endregion

    #region Helpers
    private static void ApplyExplosionToEnemies(ref SystemState state,
                                                in BombFuseState fuseState,
                                                int enemyCount,
                                                in NativeArray<Entity> enemyEntities,
                                                in NativeArray<EnemyData> enemyDataArray,
                                                ref NativeArray<EnemyHealth> enemyHealthArray,
                                                in NativeArray<LocalTransform> enemyTransforms,
                                                ref NativeArray<byte> enemyDirtyFlags)
    {
        float explosionRadius = math.max(0.1f, fuseState.Radius);
        float explosionRadiusSquared = explosionRadius * explosionRadius;
        float explosionDamage = math.max(0f, fuseState.Damage);

        if (explosionDamage <= 0f)
            return;

        if (fuseState.AffectAllEnemiesInRadius != 0)
        {
            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
                ApplyExplosionDamageToEnemy(ref state,
                                            in fuseState,
                                            enemyIndex,
                                            explosionRadiusSquared,
                                            explosionDamage,
                                            in enemyEntities,
                                            in enemyDataArray,
                                            ref enemyHealthArray,
                                            in enemyTransforms,
                                            ref enemyDirtyFlags);

            return;
        }

        int closestEnemyIndex = -1;
        float closestDistanceSquared = float.MaxValue;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            float3 enemyPosition = enemyTransforms[enemyIndex].Position;
            float3 delta = enemyPosition - fuseState.Position;
            delta.y = 0f;
            float sqrDistance = math.lengthsq(delta);

            if (sqrDistance > explosionRadiusSquared)
                continue;

            if (sqrDistance >= closestDistanceSquared)
                continue;

            closestDistanceSquared = sqrDistance;
            closestEnemyIndex = enemyIndex;
        }

        if (closestEnemyIndex < 0)
            return;

        ApplyExplosionDamageToEnemy(ref state,
                                    in fuseState,
                                    closestEnemyIndex,
                                    explosionRadiusSquared,
                                    explosionDamage,
                                    in enemyEntities,
                                    in enemyDataArray,
                                    ref enemyHealthArray,
                                    in enemyTransforms,
                                    ref enemyDirtyFlags);
    }

    private static void ApplyExplosionDamageToEnemy(ref SystemState state,
                                                    in BombFuseState fuseState,
                                                    int enemyIndex,
                                                    float explosionRadiusSquared,
                                                    float explosionDamage,
                                                    in NativeArray<Entity> enemyEntities,
                                                    in NativeArray<EnemyData> enemyDataArray,
                                                    ref NativeArray<EnemyHealth> enemyHealthArray,
                                                    in NativeArray<LocalTransform> enemyTransforms,
                                                    ref NativeArray<byte> enemyDirtyFlags)
    {
        Entity enemyEntity = enemyEntities[enemyIndex];
        EntityManager entityManager = state.EntityManager;

        if (entityManager.Exists(enemyEntity) == false)
            return;

        float3 enemyPosition = enemyTransforms[enemyIndex].Position;
        float3 delta = enemyPosition - fuseState.Position;
        delta.y = 0f;
        float sqrDistance = math.lengthsq(delta);
        float bodyRadius = math.max(0f, enemyDataArray[enemyIndex].BodyRadius);
        float bodyRadiusSquared = bodyRadius * bodyRadius;

        if (sqrDistance > explosionRadiusSquared + bodyRadiusSquared)
            return;

        EnemyHealth enemyHealth = enemyHealthArray[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return;

        enemyHealth.Current -= explosionDamage;

        if (enemyHealth.Current < 0f)
            enemyHealth.Current = 0f;

        enemyHealthArray[enemyIndex] = enemyHealth;
        enemyDirtyFlags[enemyIndex] = 1;

        if (enemyHealth.Current > 0f)
            return;

        if (entityManager.HasComponent<EnemyDespawnRequest>(enemyEntity))
            return;

        entityManager.AddComponentData(enemyEntity, new EnemyDespawnRequest
        {
            Reason = EnemyDespawnReason.Killed
        });
    }
    #endregion

    #endregion
}
