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

        DeclarePresetDependencies(authoring);
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(entity, new EnemyData
        {
            MoveSpeed = math.max(0f, authoring.MoveSpeed),
            MaxSpeed = math.max(0f, authoring.MaxSpeed),
            Acceleration = math.max(0f, authoring.Acceleration),
            Deceleration = math.max(0f, authoring.Deceleration),
            SpawnInactivityTime = math.max(0f, authoring.InactivityTime),
            RotationSpeedDegreesPerSecond = authoring.RotationSpeedDegreesPerSecond,
            SeparationRadius = math.max(0.1f, authoring.SeparationRadius),
            SeparationWeight = math.max(0f, authoring.SeparationWeight),
            BodyRadius = math.max(0.05f, authoring.BodyRadius),
            MinimumWallDistance = math.max(0f, authoring.MinimumWallDistance),
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
            SpawnInactivityTimer = 0f,
            LifetimeSeconds = 0f,
            FirstDamageLifetimeSeconds = 0f,
            LastDamageLifetimeSeconds = 0f,
            SpawnVersion = 0u,
            HasTakenDamage = 0
        });
        AddComponent(entity, new EnemyKnockbackState
        {
            Velocity = float3.zero,
            RemainingTime = 0f
        });
        AddComponent<EnemySpawnInactivityLock>(entity);
        SetComponentEnabled<EnemySpawnInactivityLock>(entity, false);
        AddComponent<EnemySpawnWarningState>(entity);
        SetComponentEnabled<EnemySpawnWarningState>(entity, false);

        EnemyCompiledPatternBakeResult compiledPattern = EnemyAdvancedPatternBakeUtility.Compile(authoring.AdvancedPatternPreset);

        AddComponent(entity, compiledPattern.PatternConfig);
        AddComponent(entity, EnemyPatternDefaultsUtility.CreatePatternRuntimeState());
        AddComponent(entity, new EnemyShooterControlState
        {
            MovementLocked = 0,
            AimDirection = float3.zero,
            HasAimDirection = 0
        });

        if (compiledPattern.HasCustomMovement)
            AddComponent<EnemyCustomPatternMovementTag>(entity);

        DynamicBuffer<EnemyShooterConfigElement> shooterConfigs = AddBuffer<EnemyShooterConfigElement>(entity);
        DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = AddBuffer<EnemyShooterRuntimeElement>(entity);
        DynamicBuffer<EnemyOffensiveEngagementConfigElement> offensiveEngagementConfigs = AddBuffer<EnemyOffensiveEngagementConfigElement>(entity);

        for (int shooterIndex = 0; shooterIndex < compiledPattern.ShooterConfigs.Count; shooterIndex++)
        {
            shooterConfigs.Add(compiledPattern.ShooterConfigs[shooterIndex]);
            shooterRuntime.Add(new EnemyShooterRuntimeElement
            {
                NextBurstTimer = 0f,
                NextShotInBurstTimer = 0f,
                RemainingBurstShots = 0,
                ShotsFiredInCurrentBurst = 0,
                BurstWindupDurationSeconds = 0f,
                IsPlayerInRange = 0,
                LockedAimDirection = float3.zero,
                HasLockedAimDirection = 0
            });
        }

        if (compiledPattern.ShooterConfigs.Count > 0)
        {
            TryBakeShooterRuntime(authoring, entity, compiledPattern);
        }

        TryBakeDropItemsRuntime(authoring, entity, compiledPattern);
        EnemyOffensiveEngagementBakeUtility.AppendConfigs(authoring, offensiveEngagementConfigs);

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
        AddComponent(entity, new OutlineVisualConfig
        {
            Enabled = authoring.EnableOutline ? (byte)1 : (byte)0,
            Thickness = math.max(0f, authoring.OutlineThickness),
            Color = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.OutlineColor)
        });

        Entity hitVfxPrefabEntity = ResolveHitVfxPrefabEntity(authoring);
        AddComponent(entity, new EnemyHitVfxConfig
        {
            PrefabEntity = hitVfxPrefabEntity,
            LifetimeSeconds = math.max(0.05f, authoring.HitVfxLifetimeSeconds),
            ScaleMultiplier = math.max(0.01f, authoring.HitVfxScaleMultiplier)
        });

        float4 damageFlashColor = DamageFlashRuntimeUtility.ToLinearFloat4(authoring.DamageFlashColor);
        AddComponent(entity, new DamageFlashConfig
        {
            FlashColor = damageFlashColor,
            DurationSeconds = math.max(0f, authoring.DamageFlashDurationSeconds),
            MaximumBlend = math.saturate(authoring.DamageFlashMaximumBlend)
        });
        AddComponent(entity, new DamageFlashState
        {
            RemainingSeconds = 0f,
            AppliedBlend = 0f
        });
        AddComponent(entity, new EnemyVisualFlashPresentationState
        {
            AppliedBlend = 0f,
            AppliedColor = damageFlashColor,
            OffensiveEngagementColor = damageFlashColor,
            OffensiveEngagementBlend = 0f,
            OffensiveEngagementFadeOutSeconds = 0f
        });
        BakeDamageFlashRenderTargets(authoring, entity);

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

        EnemyOffensiveEngagementBillboardView resolvedBillboardView = ResolveOffensiveEngagementBillboardView(authoring);

        if (resolvedBillboardView != null)
        {
            resolvedBillboardView.SyncPresetSources(authoring);
            AddComponentObject(entity, resolvedBillboardView);
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
        {
            anchorEntity = GetEntity(authoring.ElementalVfxAnchor, TransformUsageFlags.Dynamic);
        }

        AddComponent(entity, new EnemyElementalVfxAnchor
        {
            AnchorEntity = anchorEntity
        });

        AddComponent<EnemyActive>(entity);
        SetComponentEnabled<EnemyActive>(entity, false);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Declares preset dependencies consumed during enemy bake so edits on master, sub-preset and shared pattern assets trigger a rebake.
    /// /params authoring Source authoring component used to resolve all preset references.
    /// /returns None.
    /// </summary>
    private void DeclarePresetDependencies(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return;

        EnemyMasterPreset masterPreset = authoring.MasterPreset;
        EnemyBrainPreset brainPreset = authoring.BrainPreset;
        EnemyVisualPreset visualPreset = authoring.VisualPreset;
        EnemyAdvancedPatternPreset advancedPatternPreset = authoring.AdvancedPatternPreset;

        if (masterPreset != null)
            DependsOn(masterPreset);

        if (brainPreset != null)
            DependsOn(brainPreset);

        if (visualPreset != null)
            DependsOn(visualPreset);

        if (advancedPatternPreset != null)
        {
            DependsOn(advancedPatternPreset);

            if (advancedPatternPreset.ModulesAndPatternsPreset != null)
                DependsOn(advancedPatternPreset.ModulesAndPatternsPreset);
        }
    }

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

    private static EnemyOffensiveEngagementBillboardView ResolveOffensiveEngagementBillboardView(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return null;

        EnemyOffensiveEngagementBillboardView assignedBillboardView = authoring.OffensiveEngagementBillboardView;

        if (assignedBillboardView != null &&
            assignedBillboardView.gameObject != null)
        {
            return assignedBillboardView;
        }

        EnemyOffensiveEngagementBillboardView fallbackBillboardView = authoring.GetComponentInChildren<EnemyOffensiveEngagementBillboardView>(true);

        if (fallbackBillboardView != null &&
            fallbackBillboardView.gameObject != null)
        {
            return fallbackBillboardView;
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

        if (EnemyAuthoringValidationUtility.IsInvalidShooterProjectilePrefab(authoring, projectilePrefabObject))
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

    private void TryBakeDropItemsRuntime(EnemyAuthoring authoring, Entity entity, EnemyCompiledPatternBakeResult compiledPattern)
    {
        if (authoring == null)
            return;

        if (compiledPattern == null)
            return;

        EnemyDropItemsConfig dropItemsConfig = EnemyDropItemsBakeUtility.CreateDefaultConfig();
        List<EnemyExperienceDropModuleElement> stagedExperienceModules = new List<EnemyExperienceDropModuleElement>(compiledPattern.ExperienceDropModules.Count);
        List<EnemyExperienceDropDefinitionElement> stagedExperienceDefinitions = new List<EnemyExperienceDropDefinitionElement>(compiledPattern.ExperienceDropDefinitions.Count);
        List<EnemyExtraComboPointsModuleElement> stagedExtraComboPointsModules = new List<EnemyExtraComboPointsModuleElement>(compiledPattern.ExtraComboPointsModules.Count);
        List<EnemyExtraComboPointsConditionElement> stagedExtraComboPointsConditions = new List<EnemyExtraComboPointsConditionElement>(compiledPattern.ExtraComboPointsConditions.Count);

        for (int moduleIndex = 0; moduleIndex < compiledPattern.ExperienceDropModules.Count; moduleIndex++)
        {
            EnemyCompiledExperienceDropModule compiledModule = compiledPattern.ExperienceDropModules[moduleIndex];

            if (compiledModule.MaximumTotalExperienceDrop <= 0f)
                continue;

            int stagedDefinitionStartIndex = stagedExperienceDefinitions.Count;
            int definitionStartIndex = math.max(0, compiledModule.DefinitionStartIndex);
            int definitionEndIndex = math.min(compiledPattern.ExperienceDropDefinitions.Count,
                                              definitionStartIndex + math.max(0, compiledModule.DefinitionCount));
            List<float> stagedDefinitionAmounts = new List<float>(definitionEndIndex - definitionStartIndex);

            for (int definitionIndex = definitionStartIndex; definitionIndex < definitionEndIndex; definitionIndex++)
            {
                EnemyCompiledExperienceDropDefinition compiledDefinition = compiledPattern.ExperienceDropDefinitions[definitionIndex];
                GameObject dropPrefab = compiledDefinition.Prefab;

                if (dropPrefab == null)
                    continue;

                if (EnemyAuthoringValidationUtility.IsInvalidExperienceDropPrefab(authoring, dropPrefab))
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
                stagedExperienceDefinitions.Add(new EnemyExperienceDropDefinitionElement
                {
                    PrefabEntity = dropPrefabEntity,
                    ExperienceAmount = experienceAmount
                });
                stagedDefinitionAmounts.Add(experienceAmount);
            }

            int stagedDefinitionCount = stagedExperienceDefinitions.Count - stagedDefinitionStartIndex;

            if (stagedDefinitionCount <= 0)
                continue;

            int estimatedDropsPerDeath = math.max(0, compiledModule.EstimatedDropsPerDeath);

            if (estimatedDropsPerDeath <= 0)
            {
                estimatedDropsPerDeath = math.max(0,
                                                  EnemyExperienceDropDistributionUtility.EstimateDropsForPreview(stagedDefinitionAmounts,
                                                                                                                 compiledModule.MaximumTotalExperienceDrop,
                                                                                                                 compiledModule.Distribution,
                                                                                                                 out float _,
                                                                                                                 out float _));
            }

            stagedExperienceModules.Add(new EnemyExperienceDropModuleElement
            {
                MinimumTotalExperienceDrop = math.max(0f, compiledModule.MinimumTotalExperienceDrop),
                MaximumTotalExperienceDrop = math.max(math.max(0f, compiledModule.MinimumTotalExperienceDrop), compiledModule.MaximumTotalExperienceDrop),
                Distribution = math.clamp(compiledModule.Distribution, 0f, 1f),
                DropRadius = math.max(0f, compiledModule.DropRadius),
                AttractionSpeed = math.max(0f, compiledModule.AttractionSpeed),
                CollectDistance = math.max(0.01f, compiledModule.CollectDistance),
                CollectDistancePerPlayerSpeed = math.max(0f, compiledModule.CollectDistancePerPlayerSpeed),
                SpawnAnimationMinDuration = math.max(0f, compiledModule.SpawnAnimationMinDuration),
                SpawnAnimationMaxDuration = math.max(math.max(0f, compiledModule.SpawnAnimationMinDuration), compiledModule.SpawnAnimationMaxDuration),
                DefinitionStartIndex = stagedDefinitionStartIndex,
                DefinitionCount = stagedDefinitionCount,
                EstimatedDropsPerDeath = estimatedDropsPerDeath
            });
            dropItemsConfig.HasExperienceDrops = 1;
            dropItemsConfig.ExperienceModuleCount = stagedExperienceModules.Count;
            dropItemsConfig.EstimatedDropsPerDeath = EnemyAuthoringValidationUtility.AddEstimatedCount(dropItemsConfig.EstimatedDropsPerDeath,
                                                                                                       estimatedDropsPerDeath);
        }

        for (int moduleIndex = 0; moduleIndex < compiledPattern.ExtraComboPointsModules.Count; moduleIndex++)
        {
            EnemyCompiledExtraComboPointsModule compiledModule = compiledPattern.ExtraComboPointsModules[moduleIndex];
            int stagedConditionStartIndex = stagedExtraComboPointsConditions.Count;
            int conditionStartIndex = math.max(0, compiledModule.ConditionStartIndex);
            int conditionEndIndex = math.min(compiledPattern.ExtraComboPointsConditions.Count,
                                             conditionStartIndex + math.max(0, compiledModule.ConditionCount));

            for (int conditionIndex = conditionStartIndex; conditionIndex < conditionEndIndex; conditionIndex++)
            {
                EnemyCompiledExtraComboPointsCondition compiledCondition = compiledPattern.ExtraComboPointsConditions[conditionIndex];
                stagedExtraComboPointsConditions.Add(new EnemyExtraComboPointsConditionElement
                {
                    Metric = compiledCondition.Metric,
                    MinimumValue = compiledCondition.MinimumValue,
                    UseMaximumValue = compiledCondition.UseMaximumValue,
                    MaximumValue = compiledCondition.MaximumValue,
                    MinimumMultiplier = compiledCondition.MinimumMultiplier,
                    MaximumMultiplier = compiledCondition.MaximumMultiplier,
                    NormalizedMultiplierCurveSamples = compiledCondition.NormalizedMultiplierCurveSamples
                });
            }

            stagedExtraComboPointsModules.Add(new EnemyExtraComboPointsModuleElement
            {
                BaseMultiplier = compiledModule.BaseMultiplier,
                MinimumFinalMultiplier = compiledModule.MinimumFinalMultiplier,
                MaximumFinalMultiplier = compiledModule.MaximumFinalMultiplier,
                ConditionCombineMode = compiledModule.ConditionCombineMode,
                ConditionStartIndex = stagedConditionStartIndex,
                ConditionCount = stagedExtraComboPointsConditions.Count - stagedConditionStartIndex
            });
            dropItemsConfig.HasExtraComboPoints = 1;
            dropItemsConfig.ExtraComboPointsModuleCount = stagedExtraComboPointsModules.Count;
        }

        if (dropItemsConfig.HasExperienceDrops == 0 && dropItemsConfig.HasExtraComboPoints == 0)
            return;

        AddComponent(entity, dropItemsConfig);

        if (stagedExperienceModules.Count > 0)
        {
            DynamicBuffer<EnemyExperienceDropModuleElement> experienceModulesBuffer = AddBuffer<EnemyExperienceDropModuleElement>(entity);
            DynamicBuffer<EnemyExperienceDropDefinitionElement> experienceDefinitionsBuffer = AddBuffer<EnemyExperienceDropDefinitionElement>(entity);

            for (int moduleIndex = 0; moduleIndex < stagedExperienceModules.Count; moduleIndex++)
                experienceModulesBuffer.Add(stagedExperienceModules[moduleIndex]);

            for (int definitionIndex = 0; definitionIndex < stagedExperienceDefinitions.Count; definitionIndex++)
                experienceDefinitionsBuffer.Add(stagedExperienceDefinitions[definitionIndex]);
        }

        if (stagedExtraComboPointsModules.Count > 0)
        {
            DynamicBuffer<EnemyExtraComboPointsModuleElement> extraComboPointsModulesBuffer = AddBuffer<EnemyExtraComboPointsModuleElement>(entity);
            DynamicBuffer<EnemyExtraComboPointsConditionElement> extraComboPointsConditionsBuffer = AddBuffer<EnemyExtraComboPointsConditionElement>(entity);

            for (int moduleIndex = 0; moduleIndex < stagedExtraComboPointsModules.Count; moduleIndex++)
                extraComboPointsModulesBuffer.Add(stagedExtraComboPointsModules[moduleIndex]);

            for (int conditionIndex = 0; conditionIndex < stagedExtraComboPointsConditions.Count; conditionIndex++)
                extraComboPointsConditionsBuffer.Add(stagedExtraComboPointsConditions[conditionIndex]);
        }
    }

    private Entity ResolveHitVfxPrefabEntity(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return Entity.Null;

        GameObject candidatePrefab = authoring.HitVfxPrefab;

        if (candidatePrefab == null)
            return Entity.Null;

        if (EnemyAuthoringValidationUtility.IsInvalidHitVfxPrefab(authoring, candidatePrefab))
        {
#if UNITY_EDITOR
            Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid enemy hit VFX prefab '{0}' on '{1}'. Assign a prefab asset without EnemyAuthoring or PlayerAuthoring components.", candidatePrefab.name, authoring.name), authoring);
#endif
            return Entity.Null;
        }

        return GetEntity(candidatePrefab, TransformUsageFlags.Dynamic);
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

    /// <summary>
    /// Registers all renderer entities that must react to enemy hit flash feedback.
    /// authoring: Source enemy authoring component used to enumerate renderers.
    /// rootEntity: Root enemy entity that owns the flash config and render target buffer.
    /// returns None.
    /// </summary>
    private void BakeDamageFlashRenderTargets(EnemyAuthoring authoring, Entity rootEntity)
    {
        if (authoring == null)
            return;

        DynamicBuffer<DamageFlashRenderTargetElement> renderTargets = AddBuffer<DamageFlashRenderTargetElement>(rootEntity);
        Renderer[] renderers = authoring.GetComponentsInChildren<Renderer>(true);
        HashSet<Entity> bakedRenderEntities = new HashSet<Entity>();

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];

            if (renderer == null)
                continue;

            Entity renderEntity = GetEntity(renderer.gameObject, TransformUsageFlags.Renderable);

            if (!bakedRenderEntities.Add(renderEntity))
                continue;

            renderTargets.Add(new DamageFlashRenderTargetElement
            {
                Value = renderEntity,
                BaseColor = ResolveRendererBaseColor(renderer)
            });
        }

        if (renderTargets.Length > 0)
            return;

        renderTargets.Add(new DamageFlashRenderTargetElement
        {
            Value = rootEntity,
            BaseColor = new float4(1f, 1f, 1f, 1f)
        });
    }

    /// <summary>
    /// Resolves the first valid base color exposed by one authored renderer.
    /// renderer: Renderer inspected for compatible material color properties.
    /// returns Resolved base color or white when the renderer has no supported color property.
    /// </summary>
    private static float4 ResolveRendererBaseColor(Renderer renderer)
    {
        if (renderer == null)
            return new float4(1f, 1f, 1f, 1f);

        Material[] sharedMaterials = renderer.sharedMaterials;

        for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
        {
            Material sharedMaterial = sharedMaterials[materialIndex];

            if (sharedMaterial == null)
                continue;

            if (sharedMaterial.HasProperty("_BaseColor"))
            {
                Color baseColor = sharedMaterial.GetColor("_BaseColor");
                return new float4(baseColor.r, baseColor.g, baseColor.b, baseColor.a);
            }

            if (sharedMaterial.HasProperty("_Color"))
            {
                Color baseColor = sharedMaterial.GetColor("_Color");
                return new float4(baseColor.r, baseColor.g, baseColor.b, baseColor.a);
            }
        }

        return new float4(1f, 1f, 1f, 1f);
    }
    #endregion

    #endregion
}
