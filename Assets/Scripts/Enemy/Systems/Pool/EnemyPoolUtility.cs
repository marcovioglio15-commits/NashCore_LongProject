using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
            ResetVisualRuntimeState(entityManager, enemyEntity, 0);
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
                Max = 1f,
                CurrentShield = 0f,
                MaxShield = 0f
            });

        if (entityManager.HasComponent<EnemyOwnerSpawner>(enemyEntity) == false)
            entityManager.AddComponentData(enemyEntity, new EnemyOwnerSpawner
            {
                SpawnerEntity = Entity.Null
            });

        if (entityManager.HasComponent<EnemyVisualConfig>(enemyEntity) == false)
            entityManager.AddComponentData(enemyEntity, new EnemyVisualConfig
            {
                Mode = EnemyVisualMode.GpuBaked,
                AnimationSpeed = 1f,
                GpuLoopDuration = 1f,
                MaxVisibleDistance = 55f,
                VisibleDistanceHysteresis = 6f,
                UseDistanceCulling = 1
            });

        if (entityManager.HasComponent<EnemyVisualRuntimeState>(enemyEntity) == false)
            entityManager.AddComponentData(enemyEntity, new EnemyVisualRuntimeState
            {
                AnimationTime = 0f,
                LastDistanceToPlayer = 0f,
                IsVisible = 1,
                CompanionInitialized = 0
            });

        if (entityManager.HasComponent<EnemyActive>(enemyEntity) == false)
            entityManager.AddComponent<EnemyActive>(enemyEntity);

        bool hasCompanionVisualTag = entityManager.HasComponent<EnemyVisualCompanionAnimator>(enemyEntity);
        bool hasGpuVisualTag = entityManager.HasComponent<EnemyVisualGpuBaked>(enemyEntity);

        if (hasCompanionVisualTag || hasGpuVisualTag)
            return;

        EnemyVisualMode visualMode = EnemyVisualMode.GpuBaked;

        if (entityManager.HasComponent<EnemyVisualConfig>(enemyEntity))
            visualMode = entityManager.GetComponentData<EnemyVisualConfig>(enemyEntity).Mode;

        switch (visualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
                if (entityManager.HasComponent<Animator>(enemyEntity))
                    entityManager.AddComponent<EnemyVisualCompanionAnimator>(enemyEntity);
                else
                    entityManager.AddComponent<EnemyVisualGpuBaked>(enemyEntity);
                break;

            default:
                entityManager.AddComponent<EnemyVisualGpuBaked>(enemyEntity);
                break;
        }
    }

    public static void ParkEnemy(EntityManager entityManager, Entity enemyEntity)
    {
        if (entityManager.HasComponent<LocalTransform>(enemyEntity) == false)
            return;

        LocalTransform parkedTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
        parkedTransform.Position = ParkingPosition;
        entityManager.SetComponentData(enemyEntity, parkedTransform);
    }

    public static void ResetVisualRuntimeState(EntityManager entityManager, Entity enemyEntity, byte isVisible)
    {
        if (entityManager.HasComponent<EnemyVisualRuntimeState>(enemyEntity) == false)
            return;

        EnemyVisualRuntimeState visualRuntimeState = entityManager.GetComponentData<EnemyVisualRuntimeState>(enemyEntity);
        visualRuntimeState.AnimationTime = 0f;
        visualRuntimeState.LastDistanceToPlayer = 0f;
        visualRuntimeState.IsVisible = isVisible;
        visualRuntimeState.CompanionInitialized = 0;
        entityManager.SetComponentData(enemyEntity, visualRuntimeState);
    }
    #endregion

    #endregion
}
#endregion
