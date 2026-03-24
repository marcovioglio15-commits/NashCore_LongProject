using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Rebuilds all runtime-scaled player configs from immutable baselines whenever scalable stats change.
/// </summary>
internal static class PlayerRuntimeScalingApplyUtility
{
    #region Fields
    private static readonly Dictionary<string, float> variableContext = new Dictionary<string, float>(64, System.StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reapplies controller, progression, and power-up scaling against the current scalable-stat values.
    /// /params scalableStats: Runtime scalable-stat buffer used as formula variable context.
    /// /params controllerScaling: Controller scaling metadata baked from Add Scaling rules.
    /// /params baseMovement: Immutable movement baseline.
    /// /params runtimeMovement: Mutable runtime movement config rebuilt in place.
    /// /params baseLook: Immutable look baseline.
    /// /params runtimeLook: Mutable runtime look config rebuilt in place.
    /// /params baseCamera: Immutable camera baseline.
    /// /params runtimeCamera: Mutable runtime camera config rebuilt in place.
    /// /params baseShooting: Immutable shooting baseline.
    /// /params runtimeShooting: Mutable runtime shooting config rebuilt in place.
    /// /params baseHealth: Immutable health baseline.
    /// /params runtimeHealth: Mutable runtime health config rebuilt in place.
    /// /params progressionScaling: Progression scaling metadata baked from Add Scaling rules.
    /// /params baseGamePhases: Immutable progression-phase baselines.
    /// /params runtimeGamePhases: Mutable runtime progression phases rebuilt in place.
    /// /params basePowerUpConfigs: Immutable modular power-up baselines keyed by PowerUpId.
    /// /params powerUpScaling: Power-up scaling metadata baked from Add Scaling rules.
    /// /params powerUpsConfig: Mutable active-slot config rebuilt in place.
    /// /params unlockCatalog: Mutable unlock catalog rebuilt in place for active/passive runtime configs.
    /// /params equippedPassiveTools: Mutable equipped-passives buffer rebuilt in place.
    /// /params passiveToolsState: Mutable aggregated passive runtime state.
    /// /params playerHealth: Mutable runtime health component clamped to the new max.
    /// /params playerShield: Mutable runtime shield component clamped to the new max.
    /// /params progressionConfig: Runtime progression config used to update required experience and pickup radius.
    /// /params playerExperience: Mutable runtime experience component kept aligned with current scalable stats.
    /// /params playerLevel: Mutable runtime level component updated against current runtime phases.
    /// /params playerExperienceCollection: Mutable pickup-radius runtime component synchronized after formulas.
    /// /params runtimeScalingState: Mutable sync state storing the last applied scalable-stat hash.
    /// /params forceApply: True to rebuild even when the scalable-stat hash did not change.
    /// /returns True when the runtime-scaled state was rebuilt; otherwise false.
    /// </summary>
    public static bool TryApply(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScaling,
                                in PlayerBaseMovementConfig baseMovement,
                                ref PlayerRuntimeMovementConfig runtimeMovement,
                                in PlayerBaseLookConfig baseLook,
                                ref PlayerRuntimeLookConfig runtimeLook,
                                in PlayerBaseCameraConfig baseCamera,
                                ref PlayerRuntimeCameraConfig runtimeCamera,
                                in PlayerBaseShootingConfig baseShooting,
                                ref PlayerRuntimeShootingConfig runtimeShooting,
                                in PlayerBaseHealthStatisticsConfig baseHealth,
                                ref PlayerRuntimeHealthStatisticsConfig runtimeHealth,
                                DynamicBuffer<PlayerRuntimeProgressionScalingElement> progressionScaling,
                                DynamicBuffer<PlayerBaseGamePhaseElement> baseGamePhases,
                                DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                DynamicBuffer<PlayerPowerUpBaseConfigElement> basePowerUpConfigs,
                                DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling,
                                ref PlayerPowerUpsConfig powerUpsConfig,
                                DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                ref PlayerPassiveToolsState passiveToolsState,
                                ref PlayerHealth playerHealth,
                                ref PlayerShield playerShield,
                                PlayerProgressionConfig progressionConfig,
                                ref PlayerExperience playerExperience,
                                ref PlayerLevel playerLevel,
                                ref PlayerExperienceCollection playerExperienceCollection,
                                ref PlayerRuntimeScalingState runtimeScalingState,
                                bool forceApply)
    {
        uint currentHash = PlayerScalableStatHashUtility.ComputeHash(scalableStats);

        if (!forceApply &&
            runtimeScalingState.Initialized != 0 &&
            runtimeScalingState.LastScalableStatsHash == currentHash)
        {
            return false;
        }

        runtimeScalingState.Initialized = 1;
        runtimeScalingState.LastScalableStatsHash = currentHash;
        PlayerScalingRuntimeFormulaUtility.FillVariableContext(scalableStats, variableContext);
        runtimeMovement = CopyMovement(in baseMovement);
        runtimeLook = CopyLook(in baseLook);
        runtimeCamera = CopyCamera(in baseCamera);
        runtimeShooting = CopyShooting(in baseShooting);
        runtimeHealth = CopyHealth(in baseHealth);
        ApplyControllerScaling(controllerScaling,
                               ref runtimeMovement,
                               ref runtimeLook,
                               ref runtimeCamera,
                               ref runtimeShooting,
                               ref runtimeHealth);
        RebuildRuntimeGamePhases(baseGamePhases, runtimeGamePhases, progressionScaling);
        SyncPowerUpConfigs(basePowerUpConfigs, powerUpScaling, ref powerUpsConfig, unlockCatalog, equippedPassiveTools);
        passiveToolsState = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(equippedPassiveTools);
        SyncHealthAndShield(ref playerHealth, ref playerShield, in runtimeHealth);
        SyncProgressionRuntimeState(scalableStats,
                                    progressionConfig,
                                    runtimeGamePhases,
                                    ref playerExperience,
                                    ref playerLevel,
                                    ref playerExperienceCollection);
        return true;
    }

    /// <summary>
    /// Synchronizes runtime level requirement and pickup radius using the already rebuilt runtime phases.
    /// /params scalableStats: Current scalable-stat buffer.
    /// /params progressionConfig: Runtime progression config.
    /// /params runtimeGamePhases: Current rebuilt runtime phases.
    /// /params playerExperience: Mutable runtime experience component.
    /// /params playerLevel: Mutable runtime level component.
    /// /params playerExperienceCollection: Mutable pickup-radius runtime component.
    /// /returns void.
    /// </summary>
    public static void SyncProgressionRuntimeState(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                                   PlayerProgressionConfig progressionConfig,
                                                   DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                   ref PlayerExperience playerExperience,
                                                   ref PlayerLevel playerLevel,
                                                   ref PlayerExperienceCollection playerExperienceCollection)
    {
        float resolvedExperience = math.max(0f, ResolveReservedStatValue(scalableStats, "experience", playerExperience.Current));
        int levelCap = PlayerProgressionPhaseUtility.ResolveLevelCap(progressionConfig);
        int resolvedLevel = math.clamp((int)math.round(ResolveReservedStatValue(scalableStats, "level", playerLevel.Current)), 0, levelCap);
        int activeGamePhaseIndex = PlayerProgressionPhaseUtility.ResolveActiveGamePhaseIndex(progressionConfig, resolvedLevel);
        float requiredExperienceForNextLevel = 0f;

        if (resolvedLevel < levelCap)
        {
            requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig,
                                                                                                             runtimeGamePhases,
                                                                                                             resolvedLevel,
                                                                                                             out activeGamePhaseIndex,
                                                                                                             out bool _,
                                                                                                             out int _);
        }

        playerExperience = new PlayerExperience
        {
            Current = resolvedExperience
        };
        playerLevel = new PlayerLevel
        {
            Current = resolvedLevel,
            ActiveGamePhaseIndex = activeGamePhaseIndex,
            RequiredExperienceForNextLevel = requiredExperienceForNextLevel
        };
        PlayerExperiencePickupRadiusRuntimeUtility.SyncRuntimeComponent(ref playerExperienceCollection,
                                                                        progressionConfig,
                                                                        scalableStats);
    }
    #endregion

    #region Controller Scaling
    private static void ApplyControllerScaling(DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScaling,
                                               ref PlayerRuntimeMovementConfig runtimeMovement,
                                               ref PlayerRuntimeLookConfig runtimeLook,
                                               ref PlayerRuntimeCameraConfig runtimeCamera,
                                               ref PlayerRuntimeShootingConfig runtimeShooting,
                                               ref PlayerRuntimeHealthStatisticsConfig runtimeHealth)
    {
        if (!controllerScaling.IsCreated)
            return;

        for (int scalingIndex = 0; scalingIndex < controllerScaling.Length; scalingIndex++)
        {
            PlayerRuntimeControllerScalingElement scalingElement = controllerScaling[scalingIndex];

            if (!TryEvaluateValue(scalingElement.Formula.ToString(),
                                  scalingElement.BaseValue,
                                  scalingElement.IsInteger != 0,
                                  out float resolvedValue))
            {
                continue;
            }

            ApplyControllerValue(scalingElement.FieldId,
                                 resolvedValue,
                                 ref runtimeMovement,
                                 ref runtimeLook,
                                 ref runtimeCamera,
                                 ref runtimeShooting,
                                 ref runtimeHealth);
        }
    }

    private static void ApplyControllerValue(PlayerRuntimeControllerFieldId fieldId,
                                             float resolvedValue,
                                             ref PlayerRuntimeMovementConfig runtimeMovement,
                                             ref PlayerRuntimeLookConfig runtimeLook,
                                             ref PlayerRuntimeCameraConfig runtimeCamera,
                                             ref PlayerRuntimeShootingConfig runtimeShooting,
                                             ref PlayerRuntimeHealthStatisticsConfig runtimeHealth)
    {
        switch (fieldId)
        {
            case PlayerRuntimeControllerFieldId.MovementDiscreteDirectionCount:
                runtimeMovement.DiscreteDirectionCount = math.max(1, (int)resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.MovementDirectionOffsetDegrees:
                runtimeMovement.DirectionOffsetDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementBaseSpeed:
                runtimeMovement.Values.BaseSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementMaxSpeed:
                runtimeMovement.Values.MaxSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementAcceleration:
                runtimeMovement.Values.Acceleration = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementDeceleration:
                runtimeMovement.Values.Deceleration = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementOppositeDirectionBrakeMultiplier:
                runtimeMovement.Values.OppositeDirectionBrakeMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementWallBounceCoefficient:
                runtimeMovement.Values.WallBounceCoefficient = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementWallCollisionSkinWidth:
                runtimeMovement.Values.WallCollisionSkinWidth = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementInputDeadZone:
                runtimeMovement.Values.InputDeadZone = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.MovementDigitalReleaseGraceSeconds:
                runtimeMovement.Values.DigitalReleaseGraceSeconds = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookDiscreteDirectionCount:
                runtimeLook.DiscreteDirectionCount = math.max(1, (int)resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.LookDirectionOffsetDegrees:
                runtimeLook.DirectionOffsetDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRotationSpeed:
                runtimeLook.RotationSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookFrontConeAngleDegrees:
                runtimeLook.FrontCone.AngleDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookFrontConeMaxSpeedMultiplier:
                runtimeLook.FrontCone.MaxSpeedMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookFrontConeAccelerationMultiplier:
                runtimeLook.FrontCone.AccelerationMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookBackConeAngleDegrees:
                runtimeLook.BackCone.AngleDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookBackConeMaxSpeedMultiplier:
                runtimeLook.BackCone.MaxSpeedMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookBackConeAccelerationMultiplier:
                runtimeLook.BackCone.AccelerationMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookLeftConeAngleDegrees:
                runtimeLook.LeftCone.AngleDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookLeftConeMaxSpeedMultiplier:
                runtimeLook.LeftCone.MaxSpeedMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookLeftConeAccelerationMultiplier:
                runtimeLook.LeftCone.AccelerationMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRightConeAngleDegrees:
                runtimeLook.RightCone.AngleDegrees = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRightConeMaxSpeedMultiplier:
                runtimeLook.RightCone.MaxSpeedMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRightConeAccelerationMultiplier:
                runtimeLook.RightCone.AccelerationMultiplier = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRotationDamping:
                runtimeLook.Values.RotationDamping = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRotationMaxSpeed:
                runtimeLook.Values.RotationMaxSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookRotationDeadZone:
                runtimeLook.Values.RotationDeadZone = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.LookDigitalReleaseGraceSeconds:
                runtimeLook.Values.DigitalReleaseGraceSeconds = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraFollowOffsetX:
                runtimeCamera.FollowOffset.x = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraFollowOffsetY:
                runtimeCamera.FollowOffset.y = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraFollowOffsetZ:
                runtimeCamera.FollowOffset.z = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraFollowSpeed:
                runtimeCamera.Values.FollowSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraLag:
                runtimeCamera.Values.CameraLag = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraDamping:
                runtimeCamera.Values.Damping = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraMaxFollowDistance:
                runtimeCamera.Values.MaxFollowDistance = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.CameraDeadZoneRadius:
                runtimeCamera.Values.DeadZoneRadius = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingShootOffsetX:
                runtimeShooting.ShootOffset.x = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingShootOffsetY:
                runtimeShooting.ShootOffset.y = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingShootOffsetZ:
                runtimeShooting.ShootOffset.z = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingShootSpeed:
                runtimeShooting.Values.ShootSpeed = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingRateOfFire:
                runtimeShooting.Values.RateOfFire = resolvedValue;
                return;
            case PlayerRuntimeControllerFieldId.ShootingProjectileSizeMultiplier:
                runtimeShooting.Values.ProjectileSizeMultiplier = math.max(0.01f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingExplosionRadius:
                runtimeShooting.Values.ExplosionRadius = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingRange:
                runtimeShooting.Values.Range = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingLifetime:
                runtimeShooting.Values.Lifetime = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingDamage:
                runtimeShooting.Values.Damage = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.ShootingMaxPenetrations:
                runtimeShooting.Values.MaxPenetrations = math.max(0, (int)resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.HealthMaxHealth:
                runtimeHealth.MaxHealth = math.max(1f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.HealthMaxShield:
                runtimeHealth.MaxShield = math.max(0f, resolvedValue);
                return;
            case PlayerRuntimeControllerFieldId.HealthGraceTimeSeconds:
                runtimeHealth.GraceTimeSeconds = math.max(0f, resolvedValue);
                return;
        }
    }
    #endregion

    #region Progression Scaling
    private static void RebuildRuntimeGamePhases(DynamicBuffer<PlayerBaseGamePhaseElement> baseGamePhases,
                                                 DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases,
                                                 DynamicBuffer<PlayerRuntimeProgressionScalingElement> progressionScaling)
    {
        if (!baseGamePhases.IsCreated || !runtimeGamePhases.IsCreated)
            return;

        runtimeGamePhases.Clear();

        for (int phaseIndex = 0; phaseIndex < baseGamePhases.Length; phaseIndex++)
        {
            PlayerBaseGamePhaseElement basePhase = baseGamePhases[phaseIndex];
            runtimeGamePhases.Add(new PlayerRuntimeGamePhaseElement
            {
                StartsAtLevel = basePhase.StartsAtLevel,
                StartingRequiredLevelUpExp = basePhase.StartingRequiredLevelUpExp,
                RequiredExperienceGrouth = basePhase.RequiredExperienceGrouth
            });
        }

        if (!progressionScaling.IsCreated)
            return;

        for (int scalingIndex = 0; scalingIndex < progressionScaling.Length; scalingIndex++)
        {
            PlayerRuntimeProgressionScalingElement scalingElement = progressionScaling[scalingIndex];

            if (scalingElement.PhaseIndex < 0 || scalingElement.PhaseIndex >= runtimeGamePhases.Length)
                continue;

            if (!TryEvaluateValue(scalingElement.Formula.ToString(),
                                  scalingElement.BaseValue,
                                  scalingElement.IsInteger != 0,
                                  out float resolvedValue))
            {
                continue;
            }

            PlayerRuntimeGamePhaseElement runtimePhase = runtimeGamePhases[scalingElement.PhaseIndex];

            switch (scalingElement.FieldId)
            {
                case PlayerRuntimeProgressionFieldId.PhaseStartingRequiredLevelUpExp:
                    runtimePhase.StartingRequiredLevelUpExp = math.max(1f, resolvedValue);
                    break;
                case PlayerRuntimeProgressionFieldId.PhaseRequiredExperienceGrouth:
                    runtimePhase.RequiredExperienceGrouth = math.max(0f, resolvedValue);
                    break;
            }

            runtimeGamePhases[scalingElement.PhaseIndex] = runtimePhase;
        }
    }
    #endregion

    #region Power-Up Scaling
    private static void SyncPowerUpConfigs(DynamicBuffer<PlayerPowerUpBaseConfigElement> basePowerUpConfigs,
                                           DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling,
                                           ref PlayerPowerUpsConfig powerUpsConfig,
                                           DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                           DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        if (basePowerUpConfigs.IsCreated)
        {
            RebuildSlotFromBase(basePowerUpConfigs, powerUpScaling, ref powerUpsConfig.PrimarySlot);
            RebuildSlotFromBase(basePowerUpConfigs, powerUpScaling, ref powerUpsConfig.SecondarySlot);

            for (int catalogIndex = 0; catalogIndex < unlockCatalog.Length; catalogIndex++)
            {
                PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

                if (TryFindBaseConfig(basePowerUpConfigs, catalogEntry.PowerUpId, catalogEntry.UnlockKind, out PlayerPowerUpBaseConfigElement baseConfig))
                {
                    catalogEntry.ActiveSlotConfig = baseConfig.ActiveSlotConfig;
                    catalogEntry.PassiveToolConfig = baseConfig.PassiveToolConfig;
                    ApplyPowerUpScaling(powerUpScaling,
                                        catalogEntry.PowerUpId,
                                        catalogEntry.UnlockKind,
                                        ref catalogEntry.ActiveSlotConfig,
                                        ref catalogEntry.PassiveToolConfig);
                    unlockCatalog[catalogIndex] = catalogEntry;
                }
            }

            for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
            {
                EquippedPassiveToolElement equippedPassiveTool = equippedPassiveTools[passiveIndex];

                if (!TryFindBaseConfig(basePowerUpConfigs, equippedPassiveTool.PowerUpId, PlayerPowerUpUnlockKind.Passive, out PlayerPowerUpBaseConfigElement baseConfig))
                    continue;

                equippedPassiveTool.Tool = baseConfig.PassiveToolConfig;
                PlayerPowerUpSlotConfig unusedActiveSlot = default;
                ApplyPowerUpScaling(powerUpScaling,
                                    equippedPassiveTool.PowerUpId,
                                    PlayerPowerUpUnlockKind.Passive,
                                    ref unusedActiveSlot,
                                    ref equippedPassiveTool.Tool);
                equippedPassiveTools[passiveIndex] = equippedPassiveTool;
            }
        }
    }

    private static void RebuildSlotFromBase(DynamicBuffer<PlayerPowerUpBaseConfigElement> basePowerUpConfigs,
                                            DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling,
                                            ref PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0 || slotConfig.PowerUpId.Length <= 0)
            return;

        if (!TryFindBaseConfig(basePowerUpConfigs, slotConfig.PowerUpId, PlayerPowerUpUnlockKind.Active, out PlayerPowerUpBaseConfigElement baseConfig))
            return;

        slotConfig = baseConfig.ActiveSlotConfig;
        PlayerPassiveToolConfig unusedPassiveTool = default;
        ApplyPowerUpScaling(powerUpScaling,
                            slotConfig.PowerUpId,
                            PlayerPowerUpUnlockKind.Active,
                            ref slotConfig,
                            ref unusedPassiveTool);
    }

    private static void ApplyPowerUpScaling(DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling,
                                            FixedString64Bytes powerUpId,
                                            PlayerPowerUpUnlockKind unlockKind,
                                            ref PlayerPowerUpSlotConfig activeSlotConfig,
                                            ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (!powerUpScaling.IsCreated || powerUpId.Length <= 0)
            return;

        for (int scalingIndex = 0; scalingIndex < powerUpScaling.Length; scalingIndex++)
        {
            PlayerRuntimePowerUpScalingElement scalingElement = powerUpScaling[scalingIndex];

            if (scalingElement.UnlockKind != unlockKind)
                continue;

            if (scalingElement.PowerUpId != powerUpId)
                continue;

            if (!TryEvaluateValue(scalingElement.Formula.ToString(),
                                  scalingElement.BaseValue,
                                  scalingElement.IsInteger != 0,
                                  out float resolvedValue))
            {
                continue;
            }

            PlayerRuntimePowerUpScalingPathUtility.ApplyValue(scalingElement.PayloadPath.ToString(),
                                                              unlockKind,
                                                              resolvedValue,
                                                              ref activeSlotConfig,
                                                              ref passiveToolConfig);
        }
    }

    private static bool TryFindBaseConfig(DynamicBuffer<PlayerPowerUpBaseConfigElement> basePowerUpConfigs,
                                          FixedString64Bytes powerUpId,
                                          PlayerPowerUpUnlockKind unlockKind,
                                          out PlayerPowerUpBaseConfigElement baseConfig)
    {
        baseConfig = default;

        if (!basePowerUpConfigs.IsCreated || powerUpId.Length <= 0)
            return false;

        for (int configIndex = 0; configIndex < basePowerUpConfigs.Length; configIndex++)
        {
            PlayerPowerUpBaseConfigElement candidate = basePowerUpConfigs[configIndex];

            if (candidate.UnlockKind != unlockKind)
                continue;

            if (candidate.PowerUpId != powerUpId)
                continue;

            baseConfig = candidate;
            return true;
        }

        return false;
    }
    #endregion

    #region Helpers
    private static bool TryEvaluateValue(string formula, float baseValue, bool isInteger, out float resolvedValue)
    {
        resolvedValue = baseValue;

        if (!PlayerScalingRuntimeFormulaUtility.TryEvaluateFormula(formula,
                                                                   baseValue,
                                                                   variableContext,
                                                                   out float evaluatedValue,
                                                                   out string _))
        {
            return false;
        }

        resolvedValue = isInteger ? math.round(evaluatedValue) : evaluatedValue;
        return true;
    }

    private static void SyncHealthAndShield(ref PlayerHealth playerHealth,
                                            ref PlayerShield playerShield,
                                            in PlayerRuntimeHealthStatisticsConfig runtimeHealth)
    {
        playerHealth.Max = math.max(1f, runtimeHealth.MaxHealth);

        if (playerHealth.Current > playerHealth.Max)
            playerHealth.Current = playerHealth.Max;

        playerShield.Max = math.max(0f, runtimeHealth.MaxShield);

        if (playerShield.Current > playerShield.Max)
            playerShield.Current = playerShield.Max;
    }

    private static float ResolveReservedStatValue(DynamicBuffer<PlayerScalableStatElement> scalableStats, string statName, float fallbackValue)
    {
        if (!scalableStats.IsCreated)
            return fallbackValue;

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (!string.Equals(scalableStat.Name.ToString(), statName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            return scalableStat.Value;
        }

        return fallbackValue;
    }

    private static PlayerRuntimeMovementConfig CopyMovement(in PlayerBaseMovementConfig baseMovement)
    {
        return new PlayerRuntimeMovementConfig
        {
            DirectionsMode = baseMovement.DirectionsMode,
            DiscreteDirectionCount = baseMovement.DiscreteDirectionCount,
            DirectionOffsetDegrees = baseMovement.DirectionOffsetDegrees,
            MovementReference = baseMovement.MovementReference,
            Values = baseMovement.Values
        };
    }

    private static PlayerRuntimeLookConfig CopyLook(in PlayerBaseLookConfig baseLook)
    {
        return new PlayerRuntimeLookConfig
        {
            DirectionsMode = baseLook.DirectionsMode,
            DiscreteDirectionCount = baseLook.DiscreteDirectionCount,
            DirectionOffsetDegrees = baseLook.DirectionOffsetDegrees,
            RotationMode = baseLook.RotationMode,
            RotationSpeed = baseLook.RotationSpeed,
            MultiplierSampling = baseLook.MultiplierSampling,
            FrontCone = baseLook.FrontCone,
            BackCone = baseLook.BackCone,
            LeftCone = baseLook.LeftCone,
            RightCone = baseLook.RightCone,
            Values = baseLook.Values
        };
    }

    private static PlayerRuntimeCameraConfig CopyCamera(in PlayerBaseCameraConfig baseCamera)
    {
        return new PlayerRuntimeCameraConfig
        {
            Behavior = baseCamera.Behavior,
            FollowOffset = baseCamera.FollowOffset,
            Values = baseCamera.Values
        };
    }

    private static PlayerRuntimeShootingConfig CopyShooting(in PlayerBaseShootingConfig baseShooting)
    {
        return new PlayerRuntimeShootingConfig
        {
            TriggerMode = baseShooting.TriggerMode,
            ProjectilesInheritPlayerSpeed = baseShooting.ProjectilesInheritPlayerSpeed,
            ShootOffset = baseShooting.ShootOffset,
            Values = baseShooting.Values
        };
    }

    private static PlayerRuntimeHealthStatisticsConfig CopyHealth(in PlayerBaseHealthStatisticsConfig baseHealth)
    {
        return new PlayerRuntimeHealthStatisticsConfig
        {
            MaxHealth = baseHealth.MaxHealth,
            MaxShield = baseHealth.MaxShield,
            GraceTimeSeconds = baseHealth.GraceTimeSeconds
        };
    }
    #endregion

    #endregion
}
