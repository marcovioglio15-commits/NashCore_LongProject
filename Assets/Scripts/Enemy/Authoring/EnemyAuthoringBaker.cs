using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bakes EnemyAuthoring data into ECS enemy components.
/// </summary>
public sealed class EnemyAuthoringBaker : Baker<EnemyAuthoring>
{
    #region Methods

    #region Bake
    public override void Bake(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return;

        Entity entity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(entity, new EnemyData
        {
            MoveSpeed = math.max(0f, authoring.MoveSpeed),
            MaxSpeed = math.max(0f, authoring.MaxSpeed),
            Acceleration = math.max(0f, authoring.Acceleration),
            Deceleration = math.max(0f, authoring.Deceleration),
            RotationSpeedDegreesPerSecond = authoring.RotationSpeedDegreesPerSecond,
            SeparationRadius = math.max(0.1f, authoring.SeparationRadius),
            SeparationWeight = math.max(0f, authoring.SeparationWeight),
            BodyRadius = math.max(0.05f, authoring.BodyRadius),
            PriorityTier = math.clamp(authoring.PriorityTier, -128, 128),
            SteeringAggressiveness = math.clamp(authoring.SteeringAggressiveness, 0f, 2.5f),
            ContactDamageEnabled = authoring.ContactDamageEnabled ? (byte)1 : (byte)0,
            ContactRadius = math.max(0f, authoring.ContactRadius),
            ContactAmountPerTick = math.max(0f, authoring.ContactAmountPerTick),
            ContactTickInterval = math.max(0.01f, authoring.ContactTickInterval),
            AreaDamageEnabled = authoring.AreaDamageEnabled ? (byte)1 : (byte)0,
            AreaRadius = math.max(0f, authoring.AreaRadius),
            AreaAmountPerTickPercent = math.max(0f, authoring.AreaAmountPerTickPercent),
            AreaTickInterval = math.max(0.01f, authoring.AreaTickInterval)
        });

        float bakedHealth = math.max(1f, authoring.MaxHealth);
        float bakedShield = math.max(0f, authoring.MaxShield);

        AddComponent(entity, new EnemyHealth
        {
            Current = bakedHealth,
            Max = bakedHealth,
            CurrentShield = bakedShield,
            MaxShield = bakedShield
        });

        AddComponent(entity, new EnemyRuntimeState
        {
            Velocity = float3.zero,
            ContactDamageCooldown = 0f,
            AreaDamageCooldown = 0f,
            SpawnVersion = 0u
        });

        EnemyCompiledPatternBakeResult compiledPattern = EnemyAdvancedPatternBakeUtility.Compile(authoring.AdvancedPatternPreset);

        AddComponent(entity, compiledPattern.PatternConfig);
        AddComponent(entity, new EnemyPatternRuntimeState
        {
            WanderTargetPosition = float3.zero,
            WanderWaitTimer = 0f,
            WanderRetryTimer = 0f,
            LastWanderDirectionAngle = 0f,
            WanderHasTarget = 0,
            WanderInitialized = 0,
            DvdDirection = float3.zero,
            DvdInitialized = 0
        });
        AddComponent(entity, new EnemyShooterControlState
        {
            MovementLocked = 0
        });

        if (compiledPattern.HasCustomMovement)
            AddComponent<EnemyCustomPatternMovementTag>(entity);

        DynamicBuffer<EnemyShooterConfigElement> shooterConfigs = AddBuffer<EnemyShooterConfigElement>(entity);
        DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = AddBuffer<EnemyShooterRuntimeElement>(entity);

        for (int shooterIndex = 0; shooterIndex < compiledPattern.ShooterConfigs.Count; shooterIndex++)
        {
            shooterConfigs.Add(compiledPattern.ShooterConfigs[shooterIndex]);
            shooterRuntime.Add(new EnemyShooterRuntimeElement
            {
                NextBurstTimer = 0f,
                NextShotInBurstTimer = 0f,
                RemainingBurstShots = 0,
                LockedAimDirection = float3.zero,
                HasLockedAimDirection = 0
            });
        }

        if (compiledPattern.ShooterConfigs.Count > 0)
            TryBakeShooterRuntime(authoring, entity, compiledPattern);

        TryBakeDropItemsRuntime(authoring, entity, compiledPattern);

        EnemyVisualMode bakedVisualMode = ResolveBakedVisualMode(authoring, out Animator resolvedAnimatorComponent);

        AddComponent(entity, new EnemyVisualConfig
        {
            Mode = bakedVisualMode,
            AnimationSpeed = math.max(0f, authoring.VisualAnimationSpeed),
            GpuLoopDuration = math.max(0.05f, authoring.GpuAnimationLoopDuration),
            MaxVisibleDistance = math.max(0f, authoring.MaxVisibleDistance),
            VisibleDistanceHysteresis = math.max(0f, authoring.VisibleDistanceHysteresis),
            UseDistanceCulling = authoring.EnableDistanceCulling ? (byte)1 : (byte)0
        });

        Entity hitVfxPrefabEntity = ResolveHitVfxPrefabEntity(authoring);
        AddComponent(entity, new EnemyHitVfxConfig
        {
            PrefabEntity = hitVfxPrefabEntity,
            LifetimeSeconds = math.max(0.05f, authoring.HitVfxLifetimeSeconds),
            ScaleMultiplier = math.max(0.01f, authoring.HitVfxScaleMultiplier)
        });

        AddComponent(entity, new EnemyVisualRuntimeState
        {
            AnimationTime = 0f,
            LastDistanceToPlayer = 0f,
            IsVisible = 1,
            CompanionInitialized = 0,
            AppliedVisibilityPriorityTier = int.MinValue
        });

        switch (bakedVisualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
                AddComponentObject(entity, resolvedAnimatorComponent);
                AddComponent<EnemyVisualCompanionAnimator>(entity);
                break;

            default:
                AddComponent<EnemyVisualGpuBaked>(entity);
                break;
        }

        EnemyWorldSpaceStatusBarsView resolvedStatusBarsView = ResolveWorldSpaceStatusBarsView(authoring);
        Entity statusBarsViewEntity = RegisterStatusBarsViewEntity(resolvedStatusBarsView);
        AddComponent(entity, new EnemyWorldSpaceStatusBarsLink
        {
            ViewEntity = statusBarsViewEntity
        });
        AddComponent(entity, new EnemyWorldSpaceStatusBarsRuntimeLink
        {
            ViewEntity = Entity.Null
        });

        AddComponent(entity, new EnemyOwnerSpawner
        {
            SpawnerEntity = Entity.Null
        });
        AddComponent(entity, new EnemyOwnerPool
        {
            PoolEntity = Entity.Null
        });
        AddComponent(entity, new EnemyWaveOwner
        {
            WaveIndex = -1
        });

        AddComponent(entity, new EnemyElementalRuntimeState
        {
            SlowPercent = 0f
        });
        AddBuffer<EnemyElementStackElement>(entity);

        Entity anchorEntity = Entity.Null;

        if (authoring.ElementalVfxAnchor != null)
            anchorEntity = GetEntity(authoring.ElementalVfxAnchor, TransformUsageFlags.Dynamic);

        AddComponent(entity, new EnemyElementalVfxAnchor
        {
            AnchorEntity = anchorEntity
        });

        AddComponent<EnemyActive>(entity);
        SetComponentEnabled<EnemyActive>(entity, false);
    }
    #endregion

    #region Helpers
    private static EnemyVisualMode ResolveBakedVisualMode(EnemyAuthoring authoring, out Animator resolvedAnimatorComponent)
    {
        resolvedAnimatorComponent = null;

        if (authoring == null)
            return EnemyVisualMode.GpuBaked;

        EnemyVisualMode requestedMode = authoring.VisualMode;

        switch (requestedMode)
        {
            case EnemyVisualMode.CompanionAnimator:
                resolvedAnimatorComponent = ResolveAnimatorComponent(authoring);

                if (resolvedAnimatorComponent != null)
                    return EnemyVisualMode.CompanionAnimator;

#if UNITY_EDITOR
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] CompanionAnimator requested on '{0}', but no valid scene Animator was resolved. Falling back to GpuBaked mode.",
                                               authoring.name),
                                 authoring);
#endif
                return EnemyVisualMode.GpuBaked;

            case EnemyVisualMode.GpuBaked:
                return EnemyVisualMode.GpuBaked;

            default:
                return EnemyVisualMode.GpuBaked;
        }
    }

    private static Animator ResolveAnimatorComponent(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return null;

        Animator assignedAnimator = authoring.AnimatorComponent;

        if (assignedAnimator != null &&
            assignedAnimator.gameObject != null &&
            assignedAnimator.gameObject.scene.IsValid())
            return assignedAnimator;

        Animator fallbackAnimator = authoring.GetComponentInChildren<Animator>(true);

        if (fallbackAnimator != null &&
            fallbackAnimator.gameObject != null &&
            fallbackAnimator.gameObject.scene.IsValid())
            return fallbackAnimator;

        return null;
    }

    private static EnemyWorldSpaceStatusBarsView ResolveWorldSpaceStatusBarsView(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return null;

        EnemyWorldSpaceStatusBarsView assignedStatusBarsView = authoring.WorldSpaceStatusBarsView;

        if (assignedStatusBarsView != null &&
            assignedStatusBarsView.gameObject != null)
        {
            return assignedStatusBarsView;
        }

        EnemyWorldSpaceStatusBarsView fallbackStatusBarsView = authoring.GetComponentInChildren<EnemyWorldSpaceStatusBarsView>(true);

        if (fallbackStatusBarsView != null &&
            fallbackStatusBarsView.gameObject != null)
        {
            return fallbackStatusBarsView;
        }

        return null;
    }

    private void TryBakeShooterRuntime(EnemyAuthoring authoring, Entity entity, EnemyCompiledPatternBakeResult compiledPattern)
    {
        if (authoring == null)
            return;

        if (compiledPattern == null)
            return;

        GameObject projectilePrefabObject = compiledPattern.ShooterProjectilePrefab;

        if (IsInvalidShooterProjectilePrefab(authoring, projectilePrefabObject))
        {
#if UNITY_EDITOR
            if (projectilePrefabObject == null)
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Shooter modules are active on '{0}', but Runtime Projectile prefab is not assigned in the resolved Shooter payload.", authoring.name), authoring);
            else
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid Runtime Projectile prefab '{0}' on '{1}'. Assign a dedicated projectile prefab without authoring components.", projectilePrefabObject.name, authoring.name), authoring);
#endif
            return;
        }

        Entity projectilePrefabEntity = GetEntity(projectilePrefabObject, TransformUsageFlags.Dynamic);
        AddComponent(entity, new ShooterProjectilePrefab
        {
            PrefabEntity = projectilePrefabEntity
        });
        AddComponent(entity, new ProjectilePoolState
        {
            InitialCapacity = math.max(0, compiledPattern.ShooterProjectilePoolInitialCapacity),
            ExpandBatch = math.max(1, compiledPattern.ShooterProjectilePoolExpandBatch),
            Initialized = 0
        });
        AddBuffer<ShootRequest>(entity);
        AddBuffer<ProjectilePoolElement>(entity);
    }

    private static bool IsInvalidShooterProjectilePrefab(EnemyAuthoring authoring, GameObject projectilePrefabObject)
    {
        if (projectilePrefabObject == null)
            return true;

        if (authoring != null && projectilePrefabObject == authoring.gameObject)
            return true;

        if (projectilePrefabObject.scene.IsValid())
            return true;

        if (projectilePrefabObject.GetComponent<EnemyAuthoring>() != null)
            return true;

        if (projectilePrefabObject.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    private void TryBakeDropItemsRuntime(EnemyAuthoring authoring, Entity entity, EnemyCompiledPatternBakeResult compiledPattern)
    {
        if (authoring == null)
            return;

        if (compiledPattern == null)
            return;

        EnemyDropItemsConfig dropItemsConfig = compiledPattern.DropItemsConfig;

        if (dropItemsConfig.PayloadKind != EnemyDropItemsPayloadKind.Experience)
            return;

        if (dropItemsConfig.MaximumTotalExperienceDrop <= 0f)
            return;

        if (compiledPattern.ExperienceDropDefinitions == null || compiledPattern.ExperienceDropDefinitions.Count <= 0)
            return;

        List<EnemyExperienceDropDefinitionElement> stagedDefinitions = new List<EnemyExperienceDropDefinitionElement>(compiledPattern.ExperienceDropDefinitions.Count);
        List<float> stagedAmounts = new List<float>(compiledPattern.ExperienceDropDefinitions.Count);

        for (int definitionIndex = 0; definitionIndex < compiledPattern.ExperienceDropDefinitions.Count; definitionIndex++)
        {
            EnemyCompiledExperienceDropDefinition compiledDefinition = compiledPattern.ExperienceDropDefinitions[definitionIndex];
            GameObject dropPrefab = compiledDefinition.Prefab;

            if (dropPrefab == null)
                continue;

            if (IsInvalidExperienceDropPrefab(authoring, dropPrefab))
            {
#if UNITY_EDITOR
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid experience drop prefab '{0}' on '{1}'. Assign a prefab asset without EnemyAuthoring or PlayerAuthoring components.", dropPrefab.name, authoring.name), authoring);
#endif
                continue;
            }

            float experienceAmount = math.max(0f, compiledDefinition.ExperienceAmount);

            if (experienceAmount <= 0f)
                continue;

            Entity dropPrefabEntity = GetEntity(dropPrefab, TransformUsageFlags.Dynamic);
            stagedDefinitions.Add(new EnemyExperienceDropDefinitionElement
            {
                PrefabEntity = dropPrefabEntity,
                ExperienceAmount = experienceAmount
            });
            stagedAmounts.Add(experienceAmount);
        }

        if (stagedDefinitions.Count <= 0)
            return;

        int estimatedDropsPerDeath = EnemyExperienceDropDistributionUtility.EstimateDropsForPreview(stagedAmounts,
                                                                                                     dropItemsConfig.MaximumTotalExperienceDrop,
                                                                                                     dropItemsConfig.Distribution,
                                                                                                     out float _,
                                                                                                     out float _);
        dropItemsConfig.EstimatedDropsPerDeath = math.max(1, estimatedDropsPerDeath);
        dropItemsConfig.MinimumTotalExperienceDrop = math.max(0f, dropItemsConfig.MinimumTotalExperienceDrop);
        dropItemsConfig.MaximumTotalExperienceDrop = math.max(dropItemsConfig.MinimumTotalExperienceDrop, dropItemsConfig.MaximumTotalExperienceDrop);
        dropItemsConfig.DropRadius = math.max(0f, dropItemsConfig.DropRadius);
        dropItemsConfig.AttractionSpeed = math.max(0f, dropItemsConfig.AttractionSpeed);
        dropItemsConfig.CollectDistance = math.max(0.01f, dropItemsConfig.CollectDistance);
        dropItemsConfig.CollectDistancePerPlayerSpeed = math.max(0f, dropItemsConfig.CollectDistancePerPlayerSpeed);
        dropItemsConfig.SpawnAnimationMinDuration = math.max(0f, dropItemsConfig.SpawnAnimationMinDuration);
        dropItemsConfig.SpawnAnimationMaxDuration = math.max(dropItemsConfig.SpawnAnimationMinDuration, dropItemsConfig.SpawnAnimationMaxDuration);

        AddComponent(entity, dropItemsConfig);
        DynamicBuffer<EnemyExperienceDropDefinitionElement> definitionsBuffer = AddBuffer<EnemyExperienceDropDefinitionElement>(entity);

        for (int definitionIndex = 0; definitionIndex < stagedDefinitions.Count; definitionIndex++)
            definitionsBuffer.Add(stagedDefinitions[definitionIndex]);
    }

    private static bool IsInvalidExperienceDropPrefab(EnemyAuthoring authoring, GameObject dropPrefabObject)
    {
        if (dropPrefabObject == null)
            return true;

        if (authoring != null && dropPrefabObject == authoring.gameObject)
            return true;

        if (dropPrefabObject.scene.IsValid())
            return true;

        if (dropPrefabObject.GetComponent<EnemyAuthoring>() != null)
            return true;

        if (dropPrefabObject.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    private Entity ResolveHitVfxPrefabEntity(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return Entity.Null;

        GameObject candidatePrefab = authoring.HitVfxPrefab;

        if (candidatePrefab == null)
            return Entity.Null;

        if (IsInvalidHitVfxPrefab(authoring, candidatePrefab))
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid enemy hit VFX prefab '{0}' on '{1}'. Assign a prefab asset without EnemyAuthoring or PlayerAuthoring components.", candidatePrefab.name, authoring.name), authoring);
#endif
            return Entity.Null;
        }

        return GetEntity(candidatePrefab, TransformUsageFlags.Dynamic);
    }

    private static bool IsInvalidHitVfxPrefab(EnemyAuthoring authoring, GameObject hitVfxPrefabObject)
    {
        if (hitVfxPrefabObject == null)
            return true;

        if (authoring != null && hitVfxPrefabObject == authoring.gameObject)
            return true;

        if (hitVfxPrefabObject.scene.IsValid())
            return true;

        if (hitVfxPrefabObject.GetComponent<EnemyAuthoring>() != null)
            return true;

        if (hitVfxPrefabObject.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    private Entity RegisterStatusBarsViewEntity(EnemyWorldSpaceStatusBarsView statusBarsView)
    {
        if (statusBarsView == null)
            return Entity.Null;

        GameObject viewGameObject = statusBarsView.gameObject;

        if (viewGameObject == null)
            return Entity.Null;

        return GetEntity(viewGameObject, TransformUsageFlags.Dynamic);
    }
    #endregion

    #endregion
}
