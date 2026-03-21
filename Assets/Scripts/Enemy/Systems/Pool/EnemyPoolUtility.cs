using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

#region Utilities
/// <summary>
/// Provides shared helper methods for enemy pool expansion and pooled enemy state resets.
/// </summary>
public static class EnemyPoolUtility
{
    #region Constants
    private static readonly float3 ParkingPosition = new float3(0f, -10000f, 0f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Expands one concrete enemy pool by instantiating the provided prefab entity.
    /// Called by pool initialization and by runtime fallback expansion when a wave needs more instances.
    /// /params entityManager: Entity manager used to create and initialize pooled instances.
    /// /params poolEntity: Pool entity that receives the new pooled enemies.
    /// /params spawnerEntity: Spawner that owns the pool and spawned instances.
    /// /params enemyPrefab: Enemy prefab entity to instantiate.
    /// /params count: Number of pooled enemies to create.
    /// /returns None.
    /// </summary>
    public static void ExpandPool(EntityManager entityManager,
                                  Entity poolEntity,
                                  Entity spawnerEntity,
                                  Entity enemyPrefab,
                                  int count)
    {
        if (count <= 0)
            return;

        if (!entityManager.Exists(poolEntity))
            return;

        if (!entityManager.Exists(enemyPrefab))
            return;

        if (!entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
            return;

        NativeArray<Entity> spawnedEnemies = new NativeArray<Entity>(count, Allocator.Temp);
        entityManager.Instantiate(enemyPrefab, spawnedEnemies);

        try
        {
            DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);

            for (int index = 0; index < spawnedEnemies.Length; index++)
            {
                Entity enemyEntity = spawnedEnemies[index];
                EnsureEnemyComponents(entityManager, enemyEntity);
                PrepareEnemyForPool(entityManager, enemyEntity, spawnerEntity, poolEntity);
                poolBuffer.Add(new EnemyPoolElement
                {
                    EnemyEntity = enemyEntity
                });
            }
        }
        finally
        {
            if (spawnedEnemies.IsCreated)
                spawnedEnemies.Dispose();
        }
    }

    /// <summary>
    /// Ensures core pooled-enemy components exist on the provided entity.
    /// Mostly acts as a defensive safety net for prefab authoring inconsistencies.
    /// /params entityManager: Entity manager used to add missing components.
    /// /params enemyEntity: Enemy instance that must expose the expected pooled runtime components.
    /// /returns None.
    /// </summary>
    public static void EnsureEnemyComponents(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasComponent<LocalTransform>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, LocalTransform.Identity);

        if (!entityManager.HasComponent<EnemyData>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, default(EnemyData));

        if (!entityManager.HasComponent<EnemyRuntimeState>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, default(EnemyRuntimeState));

        if (!entityManager.HasComponent<EnemyPatternConfig>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, CreateDefaultPatternConfig());

        if (!entityManager.HasComponent<EnemyPatternRuntimeState>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, default(EnemyPatternRuntimeState));

        if (!entityManager.HasComponent<EnemyShooterControlState>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, default(EnemyShooterControlState));

        if (!entityManager.HasBuffer<EnemyShooterConfigElement>(enemyEntity))
            entityManager.AddBuffer<EnemyShooterConfigElement>(enemyEntity);

        if (!entityManager.HasBuffer<EnemyShooterRuntimeElement>(enemyEntity))
            entityManager.AddBuffer<EnemyShooterRuntimeElement>(enemyEntity);

        if (!entityManager.HasComponent<EnemyHealth>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyHealth
            {
                Current = 1f,
                Max = 1f,
                CurrentShield = 0f,
                MaxShield = 0f
            });

        if (!entityManager.HasComponent<EnemyOwnerSpawner>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyOwnerSpawner
            {
                SpawnerEntity = Entity.Null
            });

        if (!entityManager.HasComponent<EnemyOwnerPool>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyOwnerPool
            {
                PoolEntity = Entity.Null
            });

        if (!entityManager.HasComponent<EnemyWaveOwner>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyWaveOwner
            {
                WaveIndex = -1
            });

        if (!entityManager.HasComponent<EnemyWorldSpaceStatusBarsRuntimeLink>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyWorldSpaceStatusBarsRuntimeLink
            {
                ViewEntity = Entity.Null
            });

        if (!entityManager.HasComponent<EnemyVisualConfig>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyVisualConfig
            {
                Mode = EnemyVisualMode.GpuBaked,
                AnimationSpeed = 1f,
                GpuLoopDuration = 1f,
                MaxVisibleDistance = 55f,
                VisibleDistanceHysteresis = 6f,
                UseDistanceCulling = 1
            });

        if (!entityManager.HasComponent<EnemyVisualRuntimeState>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyVisualRuntimeState
            {
                AnimationTime = 0f,
                LastDistanceToPlayer = 0f,
                IsVisible = 1,
                CompanionInitialized = 0,
                AppliedVisibilityPriorityTier = int.MinValue
            });

        if (!entityManager.HasComponent<EnemyHitVfxConfig>(enemyEntity))
            entityManager.AddComponentData(enemyEntity, new EnemyHitVfxConfig
            {
                PrefabEntity = Entity.Null,
                LifetimeSeconds = 0.35f,
                ScaleMultiplier = 1f
            });

        if (!entityManager.HasComponent<EnemyActive>(enemyEntity))
            entityManager.AddComponent<EnemyActive>(enemyEntity);

        EnsureCustomMovementTag(entityManager, enemyEntity);
        EnsureVisualModeTags(entityManager, enemyEntity);
    }

    /// <summary>
    /// Resets one pooled enemy for activation and places it at the provided world position.
    /// Called immediately before the instance is enabled as part of a baked wave event.
    /// /params entityManager: Entity manager used to mutate runtime state.
    /// /params enemyEntity: Enemy instance being activated.
    /// /params spawnerEntity: Spawner that owns the enemy.
    /// /params poolEntity: Pool entity that must receive the enemy back on despawn.
    /// /params waveIndex: Wave index that emitted the enemy.
    /// /params worldPosition: Resolved world position for the spawn event.
    /// /returns None.
    /// </summary>
    public static void ActivateEnemy(EntityManager entityManager,
                                     Entity enemyEntity,
                                     Entity spawnerEntity,
                                     Entity poolEntity,
                                     int waveIndex,
                                     float3 worldPosition)
    {
        EnsureEnemyComponents(entityManager, enemyEntity);
        ResetEnemySimulationState(entityManager, enemyEntity);
        SetEnemyTransformPosition(entityManager, enemyEntity, worldPosition);
        SetEnemyOwnership(entityManager, enemyEntity, spawnerEntity, poolEntity, waveIndex);
        ResetVisualRuntimeState(entityManager, enemyEntity, 1);

        if (entityManager.HasComponent<EnemyDespawnRequest>(enemyEntity))
            entityManager.RemoveComponent<EnemyDespawnRequest>(enemyEntity);

        entityManager.SetComponentEnabled<EnemyActive>(enemyEntity, true);
    }

    /// <summary>
    /// Resets one pooled enemy for inactive parking inside its owning pool.
    /// Called during prewarm and on final despawn.
    /// /params entityManager: Entity manager used to mutate runtime state.
    /// /params enemyEntity: Enemy instance being returned to pool.
    /// /params spawnerEntity: Spawner that owns the enemy pool.
    /// /params poolEntity: Pool that receives the enemy.
    /// /returns None.
    /// </summary>
    public static void PrepareEnemyForPool(EntityManager entityManager,
                                           Entity enemyEntity,
                                           Entity spawnerEntity,
                                           Entity poolEntity)
    {
        EnsureEnemyComponents(entityManager, enemyEntity);
        ResetEnemySimulationState(entityManager, enemyEntity);
        SetEnemyOwnership(entityManager, enemyEntity, spawnerEntity, poolEntity, -1);
        ParkEnemy(entityManager, enemyEntity);
        ResetVisualRuntimeState(entityManager, enemyEntity, 0);
        entityManager.SetComponentEnabled<EnemyActive>(enemyEntity, false);
    }

    /// <summary>
    /// Parks the enemy far below the playable area so inactive pooled instances stay hidden.
    /// /params entityManager: Entity manager used to mutate LocalTransform.
    /// /params enemyEntity: Enemy instance to park.
    /// /returns None.
    /// </summary>
    public static void ParkEnemy(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.HasComponent<LocalTransform>(enemyEntity))
            return;

        LocalTransform parkedTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
        parkedTransform.Position = ParkingPosition;
        entityManager.SetComponentData(enemyEntity, parkedTransform);
    }

    /// <summary>
    /// Resets presentation runtime state for one enemy instance.
    /// Called when entering or leaving the active simulation set.
    /// /params entityManager: Entity manager used to mutate visual runtime state.
    /// /params enemyEntity: Enemy instance to reset.
    /// /params isVisible: Target visibility flag after reset.
    /// /returns None.
    /// </summary>
    public static void ResetVisualRuntimeState(EntityManager entityManager, Entity enemyEntity, byte isVisible)
    {
        if (!entityManager.HasComponent<EnemyVisualRuntimeState>(enemyEntity))
            return;

        EnemyVisualRuntimeState visualRuntimeState = entityManager.GetComponentData<EnemyVisualRuntimeState>(enemyEntity);
        visualRuntimeState.AnimationTime = 0f;
        visualRuntimeState.LastDistanceToPlayer = 0f;
        visualRuntimeState.IsVisible = isVisible;
        visualRuntimeState.CompanionInitialized = 0;
        visualRuntimeState.AppliedVisibilityPriorityTier = int.MinValue;
        entityManager.SetComponentData(enemyEntity, visualRuntimeState);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates the default fallback pattern config used when instantiated prefabs miss it.
    /// /returns Default EnemyPatternConfig value.
    /// </summary>
    private static EnemyPatternConfig CreateDefaultPatternConfig()
    {
        return new EnemyPatternConfig
        {
            MovementKind = EnemyCompiledMovementPatternKind.Grunt,
            StationaryFreezeRotation = 1,
            BasicSearchRadius = 9f,
            BasicMinimumTravelDistance = 2f,
            BasicMaximumTravelDistance = 8f,
            BasicArrivalTolerance = 0.35f,
            BasicWaitCooldownSeconds = 0.7f,
            BasicCandidateSampleCount = 9,
            BasicUseInfiniteDirectionSampling = 1,
            BasicInfiniteDirectionStepDegrees = 8f,
            BasicUnexploredDirectionPreference = 0.65f,
            BasicTowardPlayerPreference = 0.35f,
            BasicMinimumWallDistance = 0.25f,
            BasicMinimumEnemyClearance = 0.2f,
            BasicTrajectoryPredictionTime = 0.35f,
            BasicFreeTrajectoryPreference = 4f,
            BasicBlockedPathRetryDelay = 0.25f,
            DvdSpeedMultiplier = 1.05f,
            DvdBounceDamping = 1f,
            DvdRandomizeInitialDirection = 1,
            DvdFixedInitialDirectionDegrees = 45f,
            DvdCornerNudgeDistance = 0.08f,
            DvdIgnoreSteeringAndPriority = 0
        };
    }

    /// <summary>
    /// Ensures the custom movement tag matches the baked movement pattern kind.
    /// /params entityManager: Entity manager used to add or remove the tag.
    /// /params enemyEntity: Enemy instance to inspect.
    /// /returns None.
    /// </summary>
    private static void EnsureCustomMovementTag(EntityManager entityManager, Entity enemyEntity)
    {
        EnemyCompiledMovementPatternKind movementKind = EnemyCompiledMovementPatternKind.Grunt;

        if (entityManager.HasComponent<EnemyPatternConfig>(enemyEntity))
            movementKind = entityManager.GetComponentData<EnemyPatternConfig>(enemyEntity).MovementKind;

        bool shouldUseCustomMovement = movementKind != EnemyCompiledMovementPatternKind.Grunt;
        bool hasCustomPatternMovementTag = entityManager.HasComponent<EnemyCustomPatternMovementTag>(enemyEntity);

        if (shouldUseCustomMovement && !hasCustomPatternMovementTag)
            entityManager.AddComponent<EnemyCustomPatternMovementTag>(enemyEntity);

        if (!shouldUseCustomMovement && hasCustomPatternMovementTag)
            entityManager.RemoveComponent<EnemyCustomPatternMovementTag>(enemyEntity);
    }

    /// <summary>
    /// Ensures the visual-mode tag set matches the resolved visual configuration.
    /// /params entityManager: Entity manager used to add or remove visual tags.
    /// /params enemyEntity: Enemy instance to inspect.
    /// /returns None.
    /// </summary>
    private static void EnsureVisualModeTags(EntityManager entityManager, Entity enemyEntity)
    {
        bool hasCompanionVisualTag = entityManager.HasComponent<EnemyVisualCompanionAnimator>(enemyEntity);
        bool hasGpuVisualTag = entityManager.HasComponent<EnemyVisualGpuBaked>(enemyEntity);
        bool hasAnimatorComponent = entityManager.HasComponent<Animator>(enemyEntity);
        EnemyVisualMode visualMode = EnemyVisualMode.GpuBaked;

        if (entityManager.HasComponent<EnemyVisualConfig>(enemyEntity))
            visualMode = entityManager.GetComponentData<EnemyVisualConfig>(enemyEntity).Mode;

        switch (visualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
                if (!hasAnimatorComponent)
                    goto default;

                if (hasGpuVisualTag)
                    entityManager.RemoveComponent<EnemyVisualGpuBaked>(enemyEntity);

                if (!hasCompanionVisualTag)
                    entityManager.AddComponent<EnemyVisualCompanionAnimator>(enemyEntity);
                break;

            default:
                if (hasCompanionVisualTag)
                    entityManager.RemoveComponent<EnemyVisualCompanionAnimator>(enemyEntity);

                if (!hasGpuVisualTag)
                    entityManager.AddComponent<EnemyVisualGpuBaked>(enemyEntity);
                break;
        }
    }

    /// <summary>
    /// Resets gameplay runtime state that must start clean every time an enemy is reused.
    /// /params entityManager: Entity manager used to mutate components and buffers.
    /// /params enemyEntity: Enemy instance to reset.
    /// /returns None.
    /// </summary>
    private static void ResetEnemySimulationState(EntityManager entityManager, Entity enemyEntity)
    {
        if (entityManager.HasComponent<EnemyRuntimeState>(enemyEntity))
        {
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
        }

        if (entityManager.HasComponent<EnemyPatternRuntimeState>(enemyEntity))
            entityManager.SetComponentData(enemyEntity, CreateDefaultPatternRuntimeState());

        if (entityManager.HasComponent<EnemyShooterControlState>(enemyEntity))
        {
            EnemyShooterControlState shooterControlState = entityManager.GetComponentData<EnemyShooterControlState>(enemyEntity);
            shooterControlState.MovementLocked = 0;
            entityManager.SetComponentData(enemyEntity, shooterControlState);
        }

        if (entityManager.HasBuffer<EnemyShooterRuntimeElement>(enemyEntity))
            ResetShooterRuntime(entityManager, enemyEntity);

        if (entityManager.HasComponent<EnemyHealth>(enemyEntity))
            ResetHealth(entityManager, enemyEntity);

        if (entityManager.HasBuffer<EnemyElementStackElement>(enemyEntity))
            entityManager.GetBuffer<EnemyElementStackElement>(enemyEntity).Clear();

        if (entityManager.HasComponent<EnemyElementalRuntimeState>(enemyEntity))
        {
            EnemyElementalRuntimeState elementalRuntimeState = entityManager.GetComponentData<EnemyElementalRuntimeState>(enemyEntity);
            elementalRuntimeState.SlowPercent = 0f;
            entityManager.SetComponentData(enemyEntity, elementalRuntimeState);
        }
    }

    /// <summary>
    /// Returns the default runtime pattern state for a freshly activated pooled enemy.
    /// /returns Default EnemyPatternRuntimeState value.
    /// </summary>
    private static EnemyPatternRuntimeState CreateDefaultPatternRuntimeState()
    {
        return new EnemyPatternRuntimeState
        {
            WanderTargetPosition = float3.zero,
            WanderWaitTimer = 0f,
            WanderRetryTimer = 0f,
            LastWanderDirectionAngle = 0f,
            WanderHasTarget = 0,
            WanderInitialized = 0,
            DvdDirection = float3.zero,
            DvdInitialized = 0
        };
    }

    /// <summary>
    /// Resets shooter runtime elements from the baked shooter config count.
    /// /params entityManager: Entity manager used to access shooter buffers.
    /// /params enemyEntity: Enemy instance whose shooter runtime must be rebuilt.
    /// /returns None.
    /// </summary>
    private static void ResetShooterRuntime(EntityManager entityManager, Entity enemyEntity)
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

    /// <summary>
    /// Restores health and shield values to their baked maxima.
    /// /params entityManager: Entity manager used to mutate EnemyHealth.
    /// /params enemyEntity: Enemy instance whose health must be reset.
    /// /returns None.
    /// </summary>
    private static void ResetHealth(EntityManager entityManager, Entity enemyEntity)
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

    /// <summary>
    /// Writes current ownership metadata onto the pooled enemy instance.
    /// /params entityManager: Entity manager used to mutate owner components.
    /// /params enemyEntity: Enemy instance whose ownership must be updated.
    /// /params spawnerEntity: Owning spawner entity.
    /// /params poolEntity: Owning concrete pool entity.
    /// /params waveIndex: Wave index that currently owns the enemy, or -1 when pooled.
    /// /returns None.
    /// </summary>
    private static void SetEnemyOwnership(EntityManager entityManager,
                                          Entity enemyEntity,
                                          Entity spawnerEntity,
                                          Entity poolEntity,
                                          int waveIndex)
    {
        if (entityManager.HasComponent<EnemyOwnerSpawner>(enemyEntity))
        {
            EnemyOwnerSpawner ownerSpawner = entityManager.GetComponentData<EnemyOwnerSpawner>(enemyEntity);
            ownerSpawner.SpawnerEntity = spawnerEntity;
            entityManager.SetComponentData(enemyEntity, ownerSpawner);
        }

        if (entityManager.HasComponent<EnemyOwnerPool>(enemyEntity))
        {
            EnemyOwnerPool ownerPool = entityManager.GetComponentData<EnemyOwnerPool>(enemyEntity);
            ownerPool.PoolEntity = poolEntity;
            entityManager.SetComponentData(enemyEntity, ownerPool);
        }

        if (entityManager.HasComponent<EnemyWaveOwner>(enemyEntity))
        {
            EnemyWaveOwner waveOwner = entityManager.GetComponentData<EnemyWaveOwner>(enemyEntity);
            waveOwner.WaveIndex = waveIndex;
            entityManager.SetComponentData(enemyEntity, waveOwner);
        }
    }

    /// <summary>
    /// Updates LocalTransform position without altering existing rotation or scale.
    /// /params entityManager: Entity manager used to mutate LocalTransform.
    /// /params enemyEntity: Enemy instance to move.
    /// /params worldPosition: Target world position.
    /// /returns None.
    /// </summary>
    private static void SetEnemyTransformPosition(EntityManager entityManager, Entity enemyEntity, float3 worldPosition)
    {
        if (!entityManager.HasComponent<LocalTransform>(enemyEntity))
            return;

        LocalTransform enemyTransform = entityManager.GetComponentData<LocalTransform>(enemyEntity);
        enemyTransform.Position = worldPosition;
        entityManager.SetComponentData(enemyEntity, enemyTransform);
    }
    #endregion

    #endregion
}
#endregion
