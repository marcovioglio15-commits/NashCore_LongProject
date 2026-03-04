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
    /// <returns>Compiled bake result.</returns>
    public static EnemyCompiledPatternBakeResult Compile(EnemyAdvancedPatternPreset preset)
    {
        EnemyCompiledPatternBakeResult result = new EnemyCompiledPatternBakeResult();
        result.PatternConfig = BuildDefaultPatternConfig();
        result.HasCustomMovement = false;
        result.ShooterProjectilePrefab = preset != null ? preset.ShooterProjectilePrefab : null;
        result.ShooterProjectilePoolInitialCapacity = preset != null ? math.max(0, preset.ShooterProjectilePoolInitialCapacity) : 0;
        result.ShooterProjectilePoolExpandBatch = preset != null ? math.max(1, preset.ShooterProjectilePoolExpandBatch) : 1;
        result.HasShooterRuntimeSettings = false;

        if (preset == null)
            return result;

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

                switch (moduleDefinition.ModuleKind)
                {
                    case EnemyPatternModuleKind.Stationary:
                    case EnemyPatternModuleKind.Grunt:
                    case EnemyPatternModuleKind.Wanderer:
                        TryApplyMovementModule(moduleDefinition.ModuleKind,
                                               resolvedPayload,
                                               ref result.PatternConfig,
                                               ref selectedMovementPriority,
                                               ref result.HasCustomMovement);
                        break;

                    case EnemyPatternModuleKind.Shooter:
                        TryAddShooterModule(resolvedPayload, result.ShooterConfigs, ref result);
                        break;
                }
            }
        }

        return result;
    }
    #endregion

    #region Helpers
    private static EnemyPatternConfig BuildDefaultPatternConfig()
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

    private static EnemyPatternModulePayloadData ResolveBindingPayload(EnemyPatternModuleDefinition moduleDefinition,
                                                                       EnemyPatternModuleBinding binding)
    {
        if (binding != null && binding.UseOverridePayload && binding.OverridePayload != null)
            return binding.OverridePayload;

        if (moduleDefinition != null && moduleDefinition.Data != null)
            return moduleDefinition.Data;

        return new EnemyPatternModulePayloadData();
    }

    private static void TryApplyMovementModule(EnemyPatternModuleKind moduleKind,
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

            default:
                patternConfig.MovementKind = EnemyCompiledMovementPatternKind.Grunt;
                hasCustomMovement = false;
                return;
        }
    }

    private static int ResolveMovementPriority(EnemyPatternModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                return 3;

            case EnemyPatternModuleKind.Wanderer:
                return 2;

            case EnemyPatternModuleKind.Grunt:
                return 1;

            default:
                return 0;
        }
    }

    private static void ApplyWandererPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        if (payload == null || payload.Wanderer == null)
        {
            patternConfig.MovementKind = EnemyCompiledMovementPatternKind.WandererBasic;
            return;
        }

        EnemyWandererModuleData wanderer = payload.Wanderer;
        EnemyWandererBasicPayload basic = wanderer.Basic;
        EnemyWandererDvdPayload dvd = wanderer.Dvd;

        if (basic != null)
        {
            patternConfig.BasicSearchRadius = math.max(0.5f, basic.SearchRadius);
            patternConfig.BasicMinimumTravelDistance = math.max(0f, basic.MinimumTravelDistance);
            patternConfig.BasicMaximumTravelDistance = math.max(patternConfig.BasicMinimumTravelDistance, basic.MaximumTravelDistance);
            patternConfig.BasicArrivalTolerance = math.max(0.05f, basic.ArrivalTolerance);
            patternConfig.BasicWaitCooldownSeconds = math.max(0f, basic.WaitCooldownSeconds);
            patternConfig.BasicCandidateSampleCount = math.max(1, basic.CandidateSampleCount);
            patternConfig.BasicUseInfiniteDirectionSampling = basic.UseInfiniteDirectionSampling ? (byte)1 : (byte)0;
            patternConfig.BasicInfiniteDirectionStepDegrees = math.clamp(basic.InfiniteDirectionStepDegrees, 0.5f, 90f);
            patternConfig.BasicUnexploredDirectionPreference = math.max(0f, basic.UnexploredDirectionPreference);
            patternConfig.BasicTowardPlayerPreference = math.max(0f, basic.TowardPlayerPreference);
            patternConfig.BasicMinimumWallDistance = math.max(0f, basic.MinimumWallDistance);
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

        switch (wanderer.Mode)
        {
            case EnemyWandererMode.Dvd:
                patternConfig.MovementKind = EnemyCompiledMovementPatternKind.WandererDvd;
                return;

            default:
                patternConfig.MovementKind = EnemyCompiledMovementPatternKind.WandererBasic;
                return;
        }
    }

    private static void ApplyStationaryPayload(EnemyPatternModulePayloadData payload, ref EnemyPatternConfig patternConfig)
    {
        patternConfig.MovementKind = EnemyCompiledMovementPatternKind.Stationary;
        patternConfig.StationaryFreezeRotation = 1;

        if (payload == null || payload.Stationary == null)
            return;

        patternConfig.StationaryFreezeRotation = payload.Stationary.FreezeRotation ? (byte)1 : (byte)0;
    }

    private static void TryAddShooterModule(EnemyPatternModulePayloadData payload,
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

        EnemyShooterConfigElement shooterConfig = new EnemyShooterConfigElement
        {
            AimPolicy = shooterData.AimPolicy,
            MovementPolicy = shooterData.MovementPolicy,
            FireInterval = math.max(0.01f, shooterData.FireInterval),
            BurstCount = math.max(1, shooterData.BurstCount),
            IntraBurstDelay = math.max(0f, shooterData.IntraBurstDelay),
            UseMinimumRange = shooterData.UseMinimumRange ? (byte)1 : (byte)0,
            MinimumRange = math.max(0f, shooterData.MinimumRange),
            UseMaximumRange = shooterData.UseMaximumRange ? (byte)1 : (byte)0,
            MaximumRange = math.max(0f, shooterData.MaximumRange),
            ProjectilesPerShot = math.max(1, projectilePayload.ProjectilesPerShot),
            SpreadAngleDegrees = math.max(0f, projectilePayload.SpreadAngleDegrees),
            ProjectileSpeed = math.max(0f, projectilePayload.ProjectileSpeed),
            ProjectileDamage = math.max(0f, projectilePayload.ProjectileDamage),
            ProjectileRange = math.max(0f, projectilePayload.ProjectileRange),
            ProjectileLifetime = math.max(0f, projectilePayload.ProjectileLifetime),
            ProjectileExplosionRadius = math.max(0f, projectilePayload.ProjectileExplosionRadius),
            ProjectileScaleMultiplier = math.max(0.01f, projectilePayload.ProjectileScaleMultiplier),
            PenetrationMode = projectilePayload.PenetrationMode,
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

    private static void TryAssignShooterRuntimeSettings(EnemyShooterModuleData shooterData, ref EnemyCompiledPatternBakeResult result)
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

    private static ElementalEffectConfig BuildElementalEffectConfig(ElementalEffectDefinitionData definitionData)
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
    public readonly List<EnemyShooterConfigElement> ShooterConfigs = new List<EnemyShooterConfigElement>();
    #endregion
}
