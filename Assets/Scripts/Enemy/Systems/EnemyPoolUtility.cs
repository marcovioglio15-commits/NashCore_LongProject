using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Utilities
/// <summary>
/// Provides helper methods for enemy pooling.
/// </summary>
public static class EnemyPoolUtility
{
    #region Constants
    private static readonly float3 ParkingPosition = new float3(0f, -10000f, 0f);
    #endregion

    #region Methods

    #region Public Methods
    public static void ExpandPool(EntityManager entityManager, Entity spawnerEntity, Entity enemyPrefab, int count)
    {
        if (count <= 0)
            return;

        if (entityManager.HasBuffer<EnemyPoolElement>(spawnerEntity) == false)
            return;

        NativeArray<Entity> spawnedEnemies = new NativeArray<Entity>(count, Allocator.Temp);
        entityManager.Instantiate(enemyPrefab, spawnedEnemies);

        for (int index = 0; index < spawnedEnemies.Length; index++)
        {
            Entity enemyEntity = spawnedEnemies[index];
            EnsureEnemyComponents(entityManager, enemyEntity);

            EnemyOwnerSpawner ownerSpawner = entityManager.GetComponentData<EnemyOwnerSpawner>(enemyEntity);
            ownerSpawner.SpawnerEntity = spawnerEntity;
            entityManager.SetComponentData(enemyEntity, ownerSpawner);

            ParkEnemy(entityManager, enemyEntity);
            entityManager.SetComponentEnabled<EnemyActive>(enemyEntity, false);
        }

        DynamicBuffer<EnemyPoolElement> pool = entityManager.GetBuffer<EnemyPoolElement>(spawnerEntity);

        for (int index = 0; index < spawnedEnemies.Length; index++)
        {
            pool.Add(new EnemyPoolElement
            {
                EnemyEntity = spawnedEnemies[index]
            });
        }

        spawnedEnemies.Dispose();
    }

    public static void EnsureEnemyComponents(EntityManager entityManager, Entity enemyEntity)
    {
        if (entityManager.HasComponent<LocalTransform>(enemyEntity) == false)
            entityManager.AddComponentData(enemyEntity, LocalTransform.Identity);

        if (entityManager.HasComponent<EnemyData>(enemyEntity) == false)
            entityManager.AddComponentData(enemyEntity, default(EnemyData));

        if (entityManager.HasComponent<EnemyRuntimeState>(enemyEntity) == false)
            entityManager.AddComponentData(enemyEntity, default(EnemyRuntimeState));

        if (entityManager.HasComponent<EnemyHealth>(enemyEntity) == false)
            entityManager.AddComponentData(enemyEntity, new EnemyHealth
            {
                Current = 1f,
                Max = 1f
            });

        if (entityManager.HasComponent<EnemyOwnerSpawner>(enemyEntity) == false)
            entityManager.AddComponentData(enemyEntity, new EnemyOwnerSpawner
            {
                SpawnerEntity = Entity.Null
            });

        if (entityManager.HasComponent<EnemyActive>(enemyEntity) == false)
            entityManager.AddComponent<EnemyActive>(enemyEntity);
    }

    public static void ParkEnemy(EntityManager entityManager, Entity enemyEntity)
    {
        if (entityManager.HasComponent<LocalTransform>(enemyEntity) == false)
            return;

        LocalTransform parkedTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
        parkedTransform.Position = ParkingPosition;
        entityManager.SetComponentData(enemyEntity, parkedTransform);
    }
    #endregion

    #endregion
}
#endregion
