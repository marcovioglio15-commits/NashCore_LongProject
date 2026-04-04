using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bake-time compiler that resolves EnemyAdvancedPatternPreset into ECS-friendly runtime data.
/// </summary>
public static class EnemyAdvancedPatternBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Compiles one advanced pattern preset into movement config and shooter module list.
    /// </summary>
    /// <param name="preset">Preset to compile. Null returns default grunt config.</param>
    /// <returns>Compiled bake result.<returns>
    public static EnemyCompiledPatternBakeResult Compile(EnemyAdvancedPatternPreset preset)
    {
        EnemyCompiledPatternBakeResult result = CreateDefaultResult(preset);

        if (preset == null)
            return result;

        if (preset.ModulesAndPatternsPreset != null)
            return EnemyModulesAndPatternsBakeUtility.Compile(preset);

        int selectedMovementPriority = 0;

        for (int loadoutIndex = 0; loadoutIndex < preset.ActivePatternIds.Count; loadoutIndex++)
        {
            string patternId = preset.ActivePatternIds[loadoutIndex];
            EnemyPatternDefinition pattern = preset.ResolvePatternById(patternId);

            if (pattern == null)
                continue;

            IReadOnlyList<EnemyPatternModuleBinding> bindings = pattern.ModuleBindings;

            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                EnemyPatternModuleBinding binding = bindings[bindingIndex];

                if (binding == null)
                    continue;

                if (binding.IsEnabled == false)
                    continue;

                EnemyPatternModuleDefinition moduleDefinition = preset.ResolveModuleDefinitionById(binding.ModuleId);

                if (moduleDefinition == null)
                    continue;

                EnemyPatternModulePayloadData resolvedPayload = ResolveBindingPayload(moduleDefinition, binding);

                EnemyPatternModuleKind resolvedModuleKind = ResolveModuleKind(moduleDefinition.ModuleKind);

                switch (resolvedModuleKind)
                {
                    case EnemyPatternModuleKind.Stationary:
                    case EnemyPatternModuleKind.Grunt:
                    case EnemyPatternModuleKind.Wanderer:
                    case EnemyPatternModuleKind.Coward:
                        TryApplyMovementModule(resolvedModuleKind,
                                               resolvedPayload,
                                               ref result.PatternConfig,
                                               ref selectedMovementPriority,
                                               ref result.HasCustomMovement);
                        break;

                    case EnemyPatternModuleKind.Shooter:
                        TryAddShooterModule(resolvedPayload, result.ShooterConfigs, ref result);
                        break;

                    case EnemyPatternModuleKind.DropItems:
                        TryApplyDropItemsModule(resolvedPayload, ref result);
                        break;
                }
            }
        }

        return result;
    }
    #endregion

    #region Helpers
    internal static EnemyCompiledPatternBakeResult CreateDefaultResult(EnemyAdvancedPatternPreset preset)
    {
        EnemyCompiledPatternBakeResult result = new EnemyCompiledPatternBakeResult();
        result.PatternConfig = BuildDefaultPatternConfig();
        result.HasCustomMovement = false;
        result.ShooterProjectilePrefab = preset != null ? preset.LegacyShooterProjectilePrefab : null;
        result.ShooterProjectilePoolInitialCapacity = preset != null ? math.max(0, preset.LegacyShooterProjectilePoolInitialCapacity) : 0;
        result.ShooterProjectilePoolExpandBatch = preset != null ? math.max(1, preset.LegacyShooterProjectilePoolExpandBatch) : 1;
        result.HasShooterRuntimeSettings = false;
        result.DropItemsConfig = BuildDefaultDropItemsConfig();
        return result;
    }

    internal static EnemyPatternConfig BuildDefaultPatternConfig()
    {
        return new EnemyPatternConfig
        {
            MovementKind = EnemyCompiledMovementPatternKind.Grunt,
            HasShortRangeInteraction = 0,
            ShortRangeMovementKind = EnemyCompiledMovementPatternKind.Grunt,
            ShortRangeActivationRange = 6f,
            ShortRangeReleaseDistanceBuffer = 1f,
            ShortRangeSearchRadius = 8f,
            ShortRangeMinimumTravelDistance = 2f,
            ShortRangeMaximumTravelDistance = 8f,
            ShortRangeArrivalTolerance = 0.35f,
            ShortRangeCandidateSampleCount = 12,
            ShortRangeUseInfiniteDirectionSampling = 1,
            ShortRangeInfiniteDirectionStepDegrees = 8f,
            ShortRangeMinimumEnemyClearance = 0.25f,
            ShortRangeTrajectoryPredictionTime = 0.35f,
            ShortRangeFreeTrajectoryPreference = 0.85f,
            ShortRangeBlockedPathRetryDelay = 0.2f,
            ShortRangeRetreatDirectionPreference = 0.65f,
            ShortRangeOpenSpacePreference = 0.55f,
            ShortRangeNavigationPreference = 0.6f,
            ShortRangeRetreatSpeedMultiplierFar = 1f,
            ShortRangeRetreatSpeedMultiplierNear = 1.4f,
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
            BasicMinimumEnemyClearance = 0.2f,
            BasicTrajectoryPredictionTime = 0.35f,
            BasicFreeTrajectoryPreference = 4.4f,
            BasicBlockedPathRetryDelay = 0.25f,
            CowardDetectionRadius = 8f,
            CowardReleaseDistanceBuffer = 1.5f,
            CowardRetreatDirectionPreference = 0.65f,
            CowardOpenSpacePreference = 0.55f,
            CowardNavigationPreference = 0.6f,
            CowardPatrolRadius = 3.5f,
            CowardPatrolWaitSeconds = 0.55f,
            CowardPatrolSpeedMultiplier = 0.82f,
            CowardRetreatSpeedMultiplierFar = 1f,
            CowardRetreatSpeedMultiplierNear = 1.4f,
            DvdSpeedMultiplier = 1.05f,
            DvdBounceDamping = 1f,
            DvdRandomizeInitialDirection = 1,
            DvdFixedInitialDirectionDegrees = 45f,
            DvdCornerNudgeDistance = 0.08f,
            DvdIgnoreSteeringAndPriority = 0
        };
    }

    internal static EnemyDropItemsConfig BuildDefaultDropItemsConfig()
    {
        return new EnemyDropItemsConfig
        {
            PayloadKind = EnemyDropItemsPayloadKind.Experience,
            MinimumTotalExperienceDrop = 0f,
            MaximumTotalExperienceDrop = 0f,
            Distribution = 0.5f,
            DropRadius = 0.6f,
            AttractionSpeed = 0f,
            CollectDistance = 0.3f,
            CollectDistancePerPlayerSpeed = 0.05f,
            SpawnAnimationMinDuration = 0.08f,
            SpawnAnimationMaxDuration = 0.16f,
            EstimatedDropsPerDeath = 0
        };
    }

    internal static EnemyPatternModulePayloadData ResolveBindingPayload(EnemyPatternModuleDefinition moduleDefinition,
                                                                        EnemyPatternModuleBinding binding)
    {
        if (binding != null && binding.UseOverridePayload && binding.OverridePayload != null)
            return binding.OverridePayload;

        if (moduleDefinition != null && moduleDefinition.Data != null)
            return moduleDefinition.Data;

        return new EnemyPatternModulePayloadData();
    }

    internal static void TryApplyMovementModule(EnemyPatternModuleKind moduleKind,
                                                EnemyPatternModulePayloadData payload,
                                                ref EnemyPatternConfig patternConfig,
                                                ref int selectedPriority,
                                                ref bool hasCustomMovement)
    {
        int candidatePriority = ResolveMovementPriority(moduleKind);

        if (candidatePriority < selectedPriority)
            return;

        selectedPriority = candidatePriority;

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                ApplyStationaryPayload(payload, ref patternConfig);
                hasCustomMovement = true;
                return;

            case EnemyPatternModuleKind.Wanderer:
                ApplyWandererPayload(payload, ref patternConfig);
                hasCustomMovement = true;
                return;

            case EnemyPatternModuleKind.Coward:
                ApplyCowardPayload(payload, ref patternConfig);
                hasCustomMovement = true;
                return;

            default:
                patternConfig.MovementKind = EnemyCompiledMovementPatternKind.Grunt;
                hasCustomMovement = false;
                return;
        }
    }

    internal static int ResolveMovementPriority(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                return 3;

            case EnemyPatternModuleKind.Wanderer:
            case EnemyPatternModuleKind.Coward:
                return 2;

            case EnemyPatternModuleKind.Grunt:
                return 1;

            default:
                return 0;
        }
    }

    internal static void ApplyWandererPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        if (payload == null || payload.Wanderer == null)
        {
            patternConfig.MovementKind = EnemyCompiledMovementPatternKind.WandererBasic;
            return;
        }

        EnemyWandererModuleData wanderer = payload.Wanderer;
        EnemyWandererBasicPayload basic = wanderer.Basic;
        EnemyWandererDvdPayload dvd = wanderer.Dvd;
        EnemyWandererMode resolvedMode = ResolveWandererMode(wanderer.Mode);

        if (basic != null)
        {
            patternConfig.BasicSearchRadius = math.max(0.5f, basic.SearchRadius);
            patternConfig.BasicMinimumTravelDistance = math.max(0f, basic.MinimumTravelDistance);
            patternConfig.BasicMaximumTravelDistance = math.max(patternConfig.BasicMinimumTravelDistance, basic.MaximumTravelDistance);
            patternConfig.BasicArrivalTolerance = math.max(0.05f, basic.ArrivalTolerance);
            patternConfig.BasicWaitCooldownSeconds = math.max(0f, basic.WaitCooldownSeconds);
            patternConfig.BasicCandidateSampleCount = math.clamp(math.max(1, basic.CandidateSampleCount), 1, 32);
            patternConfig.BasicUseInfiniteDirectionSampling = basic.UseInfiniteDirectionSampling ? (byte)1 : (byte)0;
            patternConfig.BasicInfiniteDirectionStepDegrees = math.clamp(basic.InfiniteDirectionStepDegrees, 0.5f, 90f);
            patternConfig.BasicUnexploredDirectionPreference = math.max(0f, basic.UnexploredDirectionPreference);
            patternConfig.BasicTowardPlayerPreference = math.max(0f, basic.TowardPlayerPreference);
            patternConfig.BasicMinimumEnemyClearance = math.max(0f, basic.MinimumEnemyClearance);
            patternConfig.BasicTrajectoryPredictionTime = math.max(0f, basic.TrajectoryPredictionTime);
            patternConfig.BasicFreeTrajectoryPreference = math.max(0f, basic.FreeTrajectoryPreference);
            patternConfig.BasicBlockedPathRetryDelay = math.max(0f, basic.BlockedPathRetryDelay);
        }

        if (dvd != null)
        {
            patternConfig.DvdSpeedMultiplier = math.max(0f, dvd.SpeedMultiplier);
            patternConfig.DvdBounceDamping = math.clamp(dvd.BounceDamping, 0f, 1f);
            patternConfig.DvdRandomizeInitialDirection = dvd.RandomizeInitialDirection ? (byte)1 : (byte)0;
            patternConfig.DvdFixedInitialDirectionDegrees = dvd.FixedInitialDirectionDegrees;
            patternConfig.DvdCornerNudgeDistance = math.max(0f, dvd.CornerNudgeDistance);
            patternConfig.DvdIgnoreSteeringAndPriority = dvd.IgnoreSteeringAndPriority ? (byte)1 : (byte)0;
        }

        switch (resolvedMode)
        {
            case EnemyWandererMode.Dvd:
                patternConfig.MovementKind = EnemyCompiledMovementPatternKind.WandererDvd;
                return;

            default:
                patternConfig.MovementKind = EnemyCompiledMovementPatternKind.WandererBasic;
                return;
        }
    }

    internal static void ApplyStationaryPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        patternConfig.MovementKind = EnemyCompiledMovementPatternKind.Stationary;
        patternConfig.StationaryFreezeRotation = 1;

        if (payload == null || payload.Stationary == null)
            return;

        patternConfig.StationaryFreezeRotation = payload.Stationary.FreezeRotation ? (byte)1 : (byte)0;
    }

    internal static void ApplyCowardPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        patternConfig.MovementKind = EnemyCompiledMovementPatternKind.Coward;

        if (payload == null || payload.Coward == null)
            return;

        EnemyCowardModuleData coward = payload.Coward;
        patternConfig.BasicSearchRadius = math.max(0.5f, coward.SearchRadius);
        patternConfig.BasicMinimumTravelDistance = math.max(0f, coward.MinimumRetreatDistance);
        patternConfig.BasicMaximumTravelDistance = math.max(patternConfig.BasicMinimumTravelDistance, coward.MaximumRetreatDistance);
        patternConfig.BasicArrivalTolerance = math.max(0.05f, coward.ArrivalTolerance);
        patternConfig.BasicWaitCooldownSeconds = 0f;
        patternConfig.BasicCandidateSampleCount = math.clamp(math.max(1, coward.CandidateSampleCount), 1, 64);
        patternConfig.BasicUseInfiniteDirectionSampling = coward.UseInfiniteDirectionSampling ? (byte)1 : (byte)0;
        patternConfig.BasicInfiniteDirectionStepDegrees = math.clamp(coward.InfiniteDirectionStepDegrees, 0.5f, 90f);
        patternConfig.BasicUnexploredDirectionPreference = 0f;
        patternConfig.BasicTowardPlayerPreference = 0f;
        patternConfig.BasicMinimumEnemyClearance = math.max(0f, coward.MinimumEnemyClearance);
        patternConfig.BasicTrajectoryPredictionTime = math.max(0f, coward.TrajectoryPredictionTime);
        patternConfig.BasicFreeTrajectoryPreference = math.lerp(1f, 5f, math.saturate(coward.FreeTrajectoryPreference));
        patternConfig.BasicWaitCooldownSeconds = 0f;
        patternConfig.BasicBlockedPathRetryDelay = math.max(0f, coward.BlockedPathRetryDelay);
        patternConfig.CowardDetectionRadius = math.max(0f, coward.DetectionRadius);
        patternConfig.CowardReleaseDistanceBuffer = math.max(0f, coward.ReleaseDistanceBuffer);
        patternConfig.CowardRetreatDirectionPreference = math.saturate(coward.RetreatDirectionPreference);
        patternConfig.CowardOpenSpacePreference = math.saturate(coward.OpenSpacePreference);
        patternConfig.CowardNavigationPreference = math.saturate(coward.NavigationRetreatPreference);
        patternConfig.CowardPatrolRadius = math.max(0f, coward.PatrolRadius);
        patternConfig.CowardPatrolWaitSeconds = math.max(0f, coward.PatrolWaitSeconds);
        patternConfig.CowardPatrolSpeedMultiplier = math.max(0f, coward.PatrolSpeedMultiplier);
        patternConfig.CowardRetreatSpeedMultiplierFar = math.max(0f, coward.RetreatSpeedMultiplierFar);
        patternConfig.CowardRetreatSpeedMultiplierNear = math.max(patternConfig.CowardRetreatSpeedMultiplierFar,
                                                                  coward.RetreatSpeedMultiplierNear);
    }

    internal static void TryAddShooterModule(EnemyPatternModulePayloadData payload,
                                             List<EnemyShooterConfigElement> shooterConfigs,
                                             ref EnemyCompiledPatternBakeResult result)
    {
        if (shooterConfigs == null)
            return;

        if (payload == null)
            return;

        if (payload.Shooter == null)
            return;

        EnemyShooterModuleData shooterData = payload.Shooter;
        TryAssignShooterRuntimeSettings(shooterData, ref result);
        EnemyShooterProjectilePayload projectilePayload = shooterData.Projectile;

        if (projectilePayload == null)
            return;

        float minimumRange = math.max(0f, shooterData.MinimumRange);
        float maximumRange = math.max(minimumRange, shooterData.MaximumRange);

        EnemyShooterConfigElement shooterConfig = new EnemyShooterConfigElement
        {
            AimPolicy = ResolveShooterAimPolicy(shooterData.AimPolicy),
            MovementPolicy = ResolveShooterMovementPolicy(shooterData.MovementPolicy),
            FireInterval = math.max(0.01f, shooterData.FireInterval),
            BurstCount = math.clamp(math.max(1, shooterData.BurstCount), 1, 64),
            AimWindupSeconds = math.max(0f, shooterData.AimWindupSeconds),
            IntraBurstDelay = math.max(0f, shooterData.IntraBurstDelay),
            UseMinimumRange = shooterData.UseMinimumRange ? (byte)1 : (byte)0,
            MinimumRange = minimumRange,
            UseMaximumRange = shooterData.UseMaximumRange ? (byte)1 : (byte)0,
            MaximumRange = maximumRange,
            ProjectilesPerShot = math.clamp(math.max(1, projectilePayload.ProjectilesPerShot), 1, 64),
            SpreadAngleDegrees = math.max(0f, projectilePayload.SpreadAngleDegrees),
            ProjectileSpeed = math.max(0f, projectilePayload.ProjectileSpeed),
            ProjectileDamage = math.max(0f, projectilePayload.ProjectileDamage),
            ProjectileRange = math.max(0f, projectilePayload.ProjectileRange),
            ProjectileLifetime = math.max(0f, projectilePayload.ProjectileLifetime),
            ProjectileExplosionRadius = math.max(0f, projectilePayload.ProjectileExplosionRadius),
            ProjectileScaleMultiplier = math.max(0.01f, projectilePayload.ProjectileScaleMultiplier),
            PenetrationMode = ResolveProjectilePenetrationMode(projectilePayload.PenetrationMode),
            MaxPenetrations = math.max(0, projectilePayload.MaxPenetrations),
            InheritShooterSpeed = projectilePayload.InheritShooterSpeed ? (byte)1 : (byte)0,
            HasElementalPayload = 0,
            ElementalEffect = default,
            ElementalStacksPerHit = 0f
        };

        EnemyShooterElementalPayload elementalPayload = shooterData.Elemental;

        if (elementalPayload != null && elementalPayload.EnableElementalDamage && elementalPayload.StacksPerHit > 0f)
        {
            shooterConfig.HasElementalPayload = 1;
            shooterConfig.ElementalEffect = BuildElementalEffectConfig(elementalPayload.EffectData);
            shooterConfig.ElementalStacksPerHit = math.max(0f, elementalPayload.StacksPerHit);
        }

        shooterConfigs.Add(shooterConfig);
    }

    internal static void TryApplyDropItemsModule(EnemyPatternModulePayloadData payload,
                                                 ref EnemyCompiledPatternBakeResult result)
    {
        if (payload == null || payload.DropItems == null)
            return;

        if (ResolveDropItemsPayloadKind(payload.DropItems.DropPayloadKind) != EnemyDropItemsPayloadKind.Experience)
            return;

        EnemyExperienceDropPayload experiencePayload = payload.DropItems.Experience;

        if (experiencePayload == null)
            return;

        float minimumTotalExperienceDrop = math.max(0f, experiencePayload.ComplessiveExperienceDropMinimum);
        float maximumTotalExperienceDrop = math.max(minimumTotalExperienceDrop, experiencePayload.ComplessiveExperienceDropMaximum);
        float distribution = math.clamp(experiencePayload.DropsDistribution, 0f, 1f);
        float dropRadius = math.max(0f, experiencePayload.DropRadius);
        EnemyExperienceDropCollectionSettings collectionMovement = experiencePayload.CollectionMovement;
        float attractionSpeed = 0f;
        float collectDistance = 0.3f;
        float collectDistancePerPlayerSpeed = 0.05f;
        float spawnAnimationMinDuration = 0.08f;
        float spawnAnimationMaxDuration = 0.16f;

        if (collectionMovement != null)
        {
            attractionSpeed = math.max(0f, collectionMovement.MoveSpeed);
            collectDistance = math.max(0.01f, collectionMovement.CollectDistance);
            collectDistancePerPlayerSpeed = math.max(0f, collectionMovement.CollectDistancePerPlayerSpeed);
            spawnAnimationMinDuration = math.max(0f, collectionMovement.SpawnAnimationMinDuration);
            spawnAnimationMaxDuration = math.max(spawnAnimationMinDuration, collectionMovement.SpawnAnimationMaxDuration);
        }

        result.ExperienceDropDefinitions.Clear();

        IReadOnlyList<EnemyExperienceDropDefinitionData> definitions = experiencePayload.DropDefinitions;

        if (definitions != null)
        {
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                EnemyExperienceDropDefinitionData definition = definitions[definitionIndex];

                if (definition == null)
                    continue;

                float amount = math.max(0f, definition.ExperienceAmount);

                if (amount <= 0f)
                    continue;

                result.ExperienceDropDefinitions.Add(new EnemyCompiledExperienceDropDefinition
                {
                    Prefab = definition.DropPrefab,
                    ExperienceAmount = amount
                });
            }
        }

        List<float> previewValues = new List<float>(result.ExperienceDropDefinitions.Count);

        for (int definitionIndex = 0; definitionIndex < result.ExperienceDropDefinitions.Count; definitionIndex++)
            previewValues.Add(result.ExperienceDropDefinitions[definitionIndex].ExperienceAmount);

        int estimatedDropsPerDeath = EnemyExperienceDropDistributionUtility.EstimateDropsForPreview(previewValues,
                                                                                                     maximumTotalExperienceDrop,
                                                                                                     distribution,
                                                                                                     out float _,
                                                                                                     out float _);

        result.DropItemsConfig = new EnemyDropItemsConfig
        {
            PayloadKind = EnemyDropItemsPayloadKind.Experience,
            MinimumTotalExperienceDrop = minimumTotalExperienceDrop,
            MaximumTotalExperienceDrop = maximumTotalExperienceDrop,
            Distribution = distribution,
            DropRadius = dropRadius,
            AttractionSpeed = attractionSpeed,
            CollectDistance = collectDistance,
            CollectDistancePerPlayerSpeed = collectDistancePerPlayerSpeed,
            SpawnAnimationMinDuration = spawnAnimationMinDuration,
            SpawnAnimationMaxDuration = spawnAnimationMaxDuration,
            EstimatedDropsPerDeath = math.max(0, estimatedDropsPerDeath)
        };
    }

    internal static void TryAssignShooterRuntimeSettings(EnemyShooterModuleData shooterData, ref EnemyCompiledPatternBakeResult result)
    {
        if (result.HasShooterRuntimeSettings)
            return;

        if (shooterData == null)
            return;

        EnemyShooterRuntimeProjectilePayload runtimePayload = shooterData.RuntimeProjectile;

        if (runtimePayload == null)
            return;

        GameObject projectilePrefab = runtimePayload.ProjectilePrefab;

        if (projectilePrefab == null)
            return;

        result.ShooterProjectilePrefab = projectilePrefab;
        result.ShooterProjectilePoolInitialCapacity = math.max(0, runtimePayload.PoolInitialCapacity);
        result.ShooterProjectilePoolExpandBatch = math.max(1, runtimePayload.PoolExpandBatch);
        result.HasShooterRuntimeSettings = true;
    }

    internal static EnemyPatternModuleKind ResolveModuleKind(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
            case EnemyPatternModuleKind.Grunt:
            case EnemyPatternModuleKind.Wanderer:
            case EnemyPatternModuleKind.Coward:
            case EnemyPatternModuleKind.Shooter:
            case EnemyPatternModuleKind.DropItems:
                return moduleKind;

            default:
                return EnemyPatternModuleKind.Grunt;
        }
    }

    internal static EnemyWandererMode ResolveWandererMode(EnemyWandererMode mode)
    {
        switch (mode)
        {
            case EnemyWandererMode.Basic:
            case EnemyWandererMode.Dvd:
                return mode;

            default:
                return EnemyWandererMode.Basic;
        }
    }

    internal static EnemyShooterAimPolicy ResolveShooterAimPolicy(EnemyShooterAimPolicy aimPolicy)
    {
        switch (aimPolicy)
        {
            case EnemyShooterAimPolicy.LockOnFireStart:
            case EnemyShooterAimPolicy.ContinuousTracking:
                return aimPolicy;

            default:
                return EnemyShooterAimPolicy.LockOnFireStart;
        }
    }

    internal static EnemyShooterMovementPolicy ResolveShooterMovementPolicy(EnemyShooterMovementPolicy movementPolicy)
    {
        switch (movementPolicy)
        {
            case EnemyShooterMovementPolicy.KeepMoving:
            case EnemyShooterMovementPolicy.StopWhileAiming:
                return movementPolicy;

            default:
                return EnemyShooterMovementPolicy.KeepMoving;
        }
    }

    internal static ProjectilePenetrationMode ResolveProjectilePenetrationMode(ProjectilePenetrationMode penetrationMode)
    {
        switch (penetrationMode)
        {
            case ProjectilePenetrationMode.None:
            case ProjectilePenetrationMode.FixedHits:
            case ProjectilePenetrationMode.Infinite:
            case ProjectilePenetrationMode.DamageBased:
                return penetrationMode;

            default:
                return ProjectilePenetrationMode.None;
        }
    }

    internal static EnemyDropItemsPayloadKind ResolveDropItemsPayloadKind(EnemyDropItemsPayloadKind payloadKind)
    {
        switch (payloadKind)
        {
            case EnemyDropItemsPayloadKind.Experience:
                return payloadKind;

            default:
                return EnemyDropItemsPayloadKind.Experience;
        }
    }

    internal static ElementalEffectConfig BuildElementalEffectConfig(ElementalEffectDefinitionData definitionData)
    {
        if (definitionData == null)
            return default;

        return new ElementalEffectConfig
        {
            ElementType = definitionData.ElementType,
            EffectKind = definitionData.EffectKind,
            ProcMode = definitionData.ProcMode,
            ReapplyMode = definitionData.ReapplyMode,
            ProcThresholdStacks = math.max(0.1f, definitionData.ProcThresholdStacks),
            MaximumStacks = math.max(0.1f, definitionData.MaximumStacks),
            StackDecayPerSecond = math.max(0f, definitionData.StackDecayPerSecond),
            ConsumeStacksOnProc = definitionData.ConsumeStacksOnProc ? (byte)1 : (byte)0,
            DotDamagePerTick = math.max(0f, definitionData.DotDamagePerTick),
            DotTickInterval = math.max(0.01f, definitionData.DotTickInterval),
            DotDurationSeconds = math.max(0.05f, definitionData.DotDurationSeconds),
            ImpedimentSlowPercentPerStack = math.clamp(definitionData.ImpedimentSlowPercentPerStack, 0f, 100f),
            ImpedimentProcSlowPercent = math.clamp(definitionData.ImpedimentProcSlowPercent, 0f, 100f),
            ImpedimentMaxSlowPercent = math.clamp(definitionData.ImpedimentMaxSlowPercent, 0f, 100f),
            ImpedimentDurationSeconds = math.max(0.05f, definitionData.ImpedimentDurationSeconds)
        };
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores bake-time compiled pattern outputs before converting them to ECS components.
/// </summary>
public sealed class EnemyCompiledPatternBakeResult
{
    #region Fields
    public EnemyPatternConfig PatternConfig;
    public bool HasCustomMovement;
    public GameObject ShooterProjectilePrefab;
    public int ShooterProjectilePoolInitialCapacity;
    public int ShooterProjectilePoolExpandBatch;
    public bool HasShooterRuntimeSettings;
    public EnemyDropItemsConfig DropItemsConfig;
    public readonly List<EnemyShooterConfigElement> ShooterConfigs = new List<EnemyShooterConfigElement>();
    public readonly List<EnemyCompiledExperienceDropDefinition> ExperienceDropDefinitions = new List<EnemyCompiledExperienceDropDefinition>();
    #endregion
}

/// <summary>
/// Stores one compiled experience drop-definition entry before entity conversion in baker.
/// </summary>
public struct EnemyCompiledExperienceDropDefinition
{
    #region Fields
    public GameObject Prefab;
    public float ExperienceAmount;
    #endregion
}
