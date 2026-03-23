using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds runtime-scaling baselines and formula metadata used to resynchronize ECS configs after scalable-stat changes.
/// </summary>
internal static class PlayerRuntimeScalingBakeUtility
{
    #region Constants
    private const string ActivePowerUpsRoot = "activePowerUps.Array.data[";
    private const string PassivePowerUpsRoot = "passivePowerUps.Array.data[";
    private const string ModuleDefinitionsRoot = "moduleDefinitions.Array.data[";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the immutable baseline movement config from one authoring preset.
    /// /params preset: Source controller preset.
    /// /returns Sanitized runtime baseline movement config.
    /// </summary>
    public static PlayerBaseMovementConfig BuildBaseMovementConfig(PlayerControllerPreset preset)
    {
        MovementSettings movementSettings = preset != null ? preset.MovementSettings : null;

        if (movementSettings == null)
            movementSettings = new MovementSettings();

        return new PlayerBaseMovementConfig
        {
            DirectionsMode = movementSettings.DirectionsMode,
            DiscreteDirectionCount = math.max(1, movementSettings.DiscreteDirectionCount),
            DirectionOffsetDegrees = movementSettings.DirectionOffsetDegrees,
            MovementReference = movementSettings.MovementReference,
            Values = new MovementValuesBlob
            {
                BaseSpeed = movementSettings.Values.BaseSpeed,
                MaxSpeed = movementSettings.Values.MaxSpeed,
                Acceleration = movementSettings.Values.Acceleration,
                Deceleration = movementSettings.Values.Deceleration,
                OppositeDirectionBrakeMultiplier = movementSettings.Values.OppositeDirectionBrakeMultiplier,
                WallBounceCoefficient = movementSettings.Values.WallBounceCoefficient,
                WallCollisionSkinWidth = movementSettings.Values.WallCollisionSkinWidth,
                InputDeadZone = movementSettings.Values.InputDeadZone,
                DigitalReleaseGraceSeconds = movementSettings.Values.DigitalReleaseGraceSeconds
            }
        };
    }

    /// <summary>
    /// Builds the immutable baseline look config from one authoring preset.
    /// /params preset: Source controller preset.
    /// /returns Sanitized runtime baseline look config.
    /// </summary>
    public static PlayerBaseLookConfig BuildBaseLookConfig(PlayerControllerPreset preset)
    {
        LookSettings lookSettings = preset != null ? preset.LookSettings : null;

        if (lookSettings == null)
            lookSettings = new LookSettings();

        return new PlayerBaseLookConfig
        {
            DirectionsMode = lookSettings.DirectionsMode,
            DiscreteDirectionCount = math.max(1, lookSettings.DiscreteDirectionCount),
            DirectionOffsetDegrees = lookSettings.DirectionOffsetDegrees,
            RotationMode = lookSettings.RotationMode,
            RotationSpeed = lookSettings.RotationSpeed,
            MultiplierSampling = lookSettings.MultiplierSampling,
            FrontCone = new ConeConfig
            {
                Enabled = lookSettings.FrontConeEnabled,
                AngleDegrees = lookSettings.FrontConeAngle,
                MaxSpeedMultiplier = lookSettings.FrontConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.FrontConeAccelerationMultiplier
            },
            BackCone = new ConeConfig
            {
                Enabled = lookSettings.BackConeEnabled,
                AngleDegrees = lookSettings.BackConeAngle,
                MaxSpeedMultiplier = lookSettings.BackConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.BackConeAccelerationMultiplier
            },
            LeftCone = new ConeConfig
            {
                Enabled = lookSettings.LeftConeEnabled,
                AngleDegrees = lookSettings.LeftConeAngle,
                MaxSpeedMultiplier = lookSettings.LeftConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.LeftConeAccelerationMultiplier
            },
            RightCone = new ConeConfig
            {
                Enabled = lookSettings.RightConeEnabled,
                AngleDegrees = lookSettings.RightConeAngle,
                MaxSpeedMultiplier = lookSettings.RightConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.RightConeAccelerationMultiplier
            },
            Values = new LookValuesBlob
            {
                RotationDamping = lookSettings.Values.RotationDamping,
                RotationMaxSpeed = lookSettings.Values.RotationMaxSpeed,
                RotationDeadZone = lookSettings.Values.RotationDeadZone,
                DigitalReleaseGraceSeconds = lookSettings.Values.DigitalReleaseGraceSeconds
            }
        };
    }

    /// <summary>
    /// Builds the immutable baseline camera config from one authoring preset.
    /// /params preset: Source controller preset.
    /// /returns Sanitized runtime baseline camera config.
    /// </summary>
    public static PlayerBaseCameraConfig BuildBaseCameraConfig(PlayerControllerPreset preset)
    {
        CameraSettings cameraSettings = preset != null ? preset.CameraSettings : null;

        if (cameraSettings == null)
            cameraSettings = new CameraSettings();

        Vector3 followOffset = cameraSettings.FollowOffset;

        return new PlayerBaseCameraConfig
        {
            Behavior = cameraSettings.Behavior,
            FollowOffset = new float3(followOffset.x, followOffset.y, followOffset.z),
            Values = new CameraValuesBlob
            {
                FollowSpeed = cameraSettings.Values.FollowSpeed,
                CameraLag = cameraSettings.Values.CameraLag,
                Damping = cameraSettings.Values.Damping,
                MaxFollowDistance = cameraSettings.Values.MaxFollowDistance,
                DeadZoneRadius = cameraSettings.Values.DeadZoneRadius
            }
        };
    }

    /// <summary>
    /// Builds the immutable baseline shooting config from one authoring preset.
    /// /params preset: Source controller preset.
    /// /returns Sanitized runtime baseline shooting config.
    /// </summary>
    public static PlayerBaseShootingConfig BuildBaseShootingConfig(PlayerControllerPreset preset)
    {
        ShootingSettings shootingSettings = preset != null ? preset.ShootingSettings : null;

        if (shootingSettings == null)
            shootingSettings = new ShootingSettings();

        Vector3 shootOffset = shootingSettings.ShootOffset;

        return new PlayerBaseShootingConfig
        {
            TriggerMode = shootingSettings.TriggerMode,
            ProjectilesInheritPlayerSpeed = shootingSettings.ProjectilesInheritPlayerSpeed ? (byte)1 : (byte)0,
            ShootOffset = new float3(shootOffset.x, shootOffset.y, shootOffset.z),
            Values = new ShootingValuesBlob
            {
                ShootSpeed = shootingSettings.Values.ShootSpeed,
                RateOfFire = shootingSettings.Values.RateOfFire,
                ProjectileSizeMultiplier = shootingSettings.Values.ProjectileSizeMultiplier,
                ExplosionRadius = shootingSettings.Values.ExplosionRadius,
                Range = shootingSettings.Values.Range,
                Lifetime = shootingSettings.Values.Lifetime,
                Damage = shootingSettings.Values.Damage,
                PenetrationMode = shootingSettings.Values.PenetrationMode,
                MaxPenetrations = math.max(0, shootingSettings.Values.MaxPenetrations)
            }
        };
    }

    /// <summary>
    /// Builds the immutable baseline health config from one authoring preset.
    /// /params preset: Source controller preset.
    /// /returns Sanitized runtime baseline health config.
    /// </summary>
    public static PlayerBaseHealthStatisticsConfig BuildBaseHealthStatisticsConfig(PlayerControllerPreset preset)
    {
        PlayerHealthStatisticsSettings healthStatistics = preset != null ? preset.HealthStatistics : null;

        if (healthStatistics == null)
            healthStatistics = new PlayerHealthStatisticsSettings();

        return new PlayerBaseHealthStatisticsConfig
        {
            MaxHealth = math.max(1f, healthStatistics.MaxHealth),
            MaxShield = math.max(0f, healthStatistics.MaxShield)
        };
    }

    /// <summary>
    /// Populates immutable and runtime progression phase buffers from progression presets.
    /// /params scaledPreset: Scaled preset currently used by bake.
    /// /params sourcePreset: Unscaled source preset used as runtime baseline.
    /// /params basePhases: Destination immutable baseline buffer.
    /// /params runtimePhases: Destination runtime buffer initialized from the scaled preset.
    /// /returns void.
    /// </summary>
    public static void PopulateProgressionPhaseBuffers(PlayerProgressionPreset scaledPreset,
                                                       PlayerProgressionPreset sourcePreset,
                                                       DynamicBuffer<PlayerBaseGamePhaseElement> basePhases,
                                                       DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimePhases)
    {
        basePhases.Clear();
        runtimePhases.Clear();
        IReadOnlyList<PlayerGamePhaseDefinition> sourcePhases = sourcePreset != null ? sourcePreset.GamePhasesDefinition : null;
        IReadOnlyList<PlayerGamePhaseDefinition> scaledPhases = scaledPreset != null ? scaledPreset.GamePhasesDefinition : null;
        int phaseCount = math.max(sourcePhases != null ? sourcePhases.Count : 0, scaledPhases != null ? scaledPhases.Count : 0);

        for (int phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
        {
            PlayerGamePhaseDefinition sourcePhase = sourcePhases != null && phaseIndex < sourcePhases.Count ? sourcePhases[phaseIndex] : null;
            PlayerGamePhaseDefinition scaledPhase = scaledPhases != null && phaseIndex < scaledPhases.Count ? scaledPhases[phaseIndex] : null;
            int baseStartsAtLevel = sourcePhase != null ? math.max(0, sourcePhase.StartsAtLevel) : 0;
            float baseStartingExp = sourcePhase != null ? math.max(1f, sourcePhase.StartingRequiredLevelUpExp) : 100f;
            float baseGrowth = sourcePhase != null ? math.max(0f, sourcePhase.RequiredExperienceGrouth) : 0f;
            int runtimeStartsAtLevel = scaledPhase != null ? math.max(0, scaledPhase.StartsAtLevel) : baseStartsAtLevel;
            float runtimeStartingExp = scaledPhase != null ? math.max(1f, scaledPhase.StartingRequiredLevelUpExp) : baseStartingExp;
            float runtimeGrowth = scaledPhase != null ? math.max(0f, scaledPhase.RequiredExperienceGrouth) : baseGrowth;

            basePhases.Add(new PlayerBaseGamePhaseElement
            {
                StartsAtLevel = baseStartsAtLevel,
                StartingRequiredLevelUpExp = baseStartingExp,
                RequiredExperienceGrouth = baseGrowth
            });
            runtimePhases.Add(new PlayerRuntimeGamePhaseElement
            {
                StartsAtLevel = runtimeStartsAtLevel,
                StartingRequiredLevelUpExp = runtimeStartingExp,
                RequiredExperienceGrouth = runtimeGrowth
            });
        }
    }

    /// <summary>
    /// Populates immutable base configs for all modular power-ups so runtime scaling can rebuild active/passive snapshots.
    /// /params authoring: Owning player authoring component.
    /// /params sourcePreset: Unscaled power-ups preset.
    /// /params resolveDynamicPrefabEntity: Prefab-to-entity resolver provided by the baker.
    /// /params baseConfigs: Destination immutable base config buffer.
    /// /returns void.
    /// </summary>
    public static void PopulatePowerUpBaseConfigs(PlayerAuthoring authoring,
                                                  PlayerPowerUpsPreset sourcePreset,
                                                  Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                  DynamicBuffer<PlayerPowerUpBaseConfigElement> baseConfigs)
    {
        baseConfigs.Clear();

        if (sourcePreset == null)
            return;

        IReadOnlyList<ModularPowerUpDefinition> activePowerUps = sourcePreset.ActivePowerUps;
        IReadOnlyList<ModularPowerUpDefinition> passivePowerUps = sourcePreset.PassivePowerUps;
        AppendPowerUpBaseConfigs(authoring,
                                 sourcePreset,
                                 activePowerUps,
                                 PlayerPowerUpUnlockKind.Active,
                                 resolveDynamicPrefabEntity,
                                 baseConfigs);
        AppendPowerUpBaseConfigs(authoring,
                                 sourcePreset,
                                 passivePowerUps,
                                 PlayerPowerUpUnlockKind.Passive,
                                 resolveDynamicPrefabEntity,
                                 baseConfigs);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Populates controller scaling metadata from the unscaled controller preset.
    /// /params sourcePreset: Unscaled source controller preset.
    /// /params scalingBuffer: Destination scaling metadata buffer.
    /// /returns void.
    /// </summary>
    public static void PopulateControllerScalingMetadata(PlayerControllerPreset sourcePreset,
                                                         DynamicBuffer<PlayerRuntimeControllerScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();
        PopulateScalingMetadata(sourcePreset, sourcePreset != null ? sourcePreset.ScalingRules : null, scalingBuffer);
    }

    /// <summary>
    /// Populates progression scaling metadata from the unscaled progression preset.
    /// /params sourcePreset: Unscaled source progression preset.
    /// /params scalingBuffer: Destination scaling metadata buffer.
    /// /returns void.
    /// </summary>
    public static void PopulateProgressionScalingMetadata(PlayerProgressionPreset sourcePreset,
                                                          DynamicBuffer<PlayerRuntimeProgressionScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();

        if (sourcePreset == null || sourcePreset.ScalingRules == null || sourcePreset.ScalingRules.Count <= 0)
            return;

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < sourcePreset.ScalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = sourcePreset.ScalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling)
                continue;

            if (string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            if (!TryMapProgressionFieldId(scalingRule.StatKey, out int phaseIndex, out PlayerRuntimeProgressionFieldId fieldId))
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            float baseValue = property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
            scalingBuffer.Add(new PlayerRuntimeProgressionScalingElement
            {
                PhaseIndex = phaseIndex,
                FieldId = fieldId,
                BaseValue = baseValue,
                IsInteger = property.propertyType == SerializedPropertyType.Integer ? (byte)1 : (byte)0,
                Formula = new FixedString512Bytes(scalingRule.Formula)
            });
        }
    }

    /// <summary>
    /// Populates power-up scaling metadata from the unscaled power-ups preset.
    /// /params sourcePreset: Unscaled source power-ups preset.
    /// /params scalingBuffer: Destination scaling metadata buffer.
    /// /returns void.
    /// </summary>
    public static void PopulatePowerUpScalingMetadata(PlayerPowerUpsPreset sourcePreset,
                                                      DynamicBuffer<PlayerRuntimePowerUpScalingElement> scalingBuffer)
    {
        scalingBuffer.Clear();

        if (sourcePreset == null || sourcePreset.ScalingRules == null || sourcePreset.ScalingRules.Count <= 0)
            return;

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < sourcePreset.ScalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = sourcePreset.ScalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling)
                continue;

            if (string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            if (TryAddDirectPowerUpScalingEntry(sourcePreset, scalingRule, property, scalingBuffer))
                continue;

            TryAddSharedModuleScalingEntries(sourcePreset, scalingRule, property, scalingBuffer);
        }
    }
#endif
    #endregion

    #region Private Methods
    private static void AppendPowerUpBaseConfigs(PlayerAuthoring authoring,
                                                 PlayerPowerUpsPreset sourcePreset,
                                                 IReadOnlyList<ModularPowerUpDefinition> powerUps,
                                                 PlayerPowerUpUnlockKind unlockKind,
                                                 Func<GameObject, Entity> resolveDynamicPrefabEntity,
                                                 DynamicBuffer<PlayerPowerUpBaseConfigElement> baseConfigs)
    {
        if (powerUps == null)
            return;

        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp == null || powerUp.CommonData == null || string.IsNullOrWhiteSpace(powerUp.CommonData.PowerUpId))
                continue;

            PlayerPowerUpBaseConfigElement element = new PlayerPowerUpBaseConfigElement
            {
                PowerUpId = new FixedString64Bytes(powerUp.CommonData.PowerUpId.Trim()),
                UnlockKind = unlockKind,
                ActiveSlotConfig = default,
                PassiveToolConfig = default
            };

            if (unlockKind == PlayerPowerUpUnlockKind.Active)
                element.ActiveSlotConfig = PlayerPowerUpActiveBakeUtility.BuildSlotConfigFromModularPowerUp(authoring,
                                                                                                            sourcePreset,
                                                                                                            powerUp,
                                                                                                            resolveDynamicPrefabEntity);
            else
                element.PassiveToolConfig = PlayerPowerUpPassiveBakeUtility.BuildPassiveToolConfigFromModularPowerUp(authoring,
                                                                                                                    sourcePreset,
                                                                                                                    powerUp,
                                                                                                                    resolveDynamicPrefabEntity);

            baseConfigs.Add(element);
        }
    }

#if UNITY_EDITOR
    private static void PopulateScalingMetadata(PlayerControllerPreset sourcePreset,
                                                IReadOnlyList<PlayerStatScalingRule> scalingRules,
                                                DynamicBuffer<PlayerRuntimeControllerScalingElement> scalingBuffer)
    {
        if (sourcePreset == null || scalingRules == null || scalingRules.Count <= 0)
            return;

        SerializedObject serializedPreset = new SerializedObject(sourcePreset);

        for (int ruleIndex = 0; ruleIndex < scalingRules.Count; ruleIndex++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[ruleIndex];

            if (scalingRule == null || !scalingRule.AddScaling)
                continue;

            if (string.IsNullOrWhiteSpace(scalingRule.Formula))
                continue;

            if (!TryMapControllerFieldId(scalingRule.StatKey, out PlayerRuntimeControllerFieldId fieldId))
                continue;

            if (!PlayerScalingStatKeyUtility.TryFindPropertyByStatKey(serializedPreset, scalingRule.StatKey, out SerializedProperty property))
                continue;

            float baseValue = property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
            scalingBuffer.Add(new PlayerRuntimeControllerScalingElement
            {
                FieldId = fieldId,
                BaseValue = baseValue,
                IsInteger = property.propertyType == SerializedPropertyType.Integer ? (byte)1 : (byte)0,
                Formula = new FixedString512Bytes(scalingRule.Formula)
            });
        }
    }

    private static bool TryAddDirectPowerUpScalingEntry(PlayerPowerUpsPreset sourcePreset,
                                                        PlayerStatScalingRule scalingRule,
                                                        SerializedProperty property,
                                                        DynamicBuffer<PlayerRuntimePowerUpScalingElement> scalingBuffer)
    {
        if (!TryResolveDirectPowerUpScalingKey(sourcePreset,
                                               scalingRule.StatKey,
                                               property,
                                               out PlayerPowerUpUnlockKind unlockKind,
                                               out string powerUpId,
                                               out string payloadPath))
        {
            return false;
        }

        float baseValue = property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
        scalingBuffer.Add(new PlayerRuntimePowerUpScalingElement
        {
            PowerUpId = new FixedString64Bytes(powerUpId),
            UnlockKind = unlockKind,
            PayloadPath = new FixedString128Bytes(payloadPath),
            BaseValue = baseValue,
            IsInteger = property.propertyType == SerializedPropertyType.Integer ? (byte)1 : (byte)0,
            Formula = new FixedString512Bytes(scalingRule.Formula)
        });
        return true;
    }

    private static bool TryResolveDirectPowerUpScalingKey(PlayerPowerUpsPreset sourcePreset,
                                                          string statKey,
                                                          SerializedProperty property,
                                                          out PlayerPowerUpUnlockKind unlockKind,
                                                          out string powerUpId,
                                                          out string payloadPath)
    {
        if (TryExtractPowerUpKey(statKey, out unlockKind, out powerUpId, out payloadPath))
            return true;

        return TryResolvePowerUpKeyFromPropertyPath(sourcePreset,
                                                    property != null ? property.propertyPath : string.Empty,
                                                    out unlockKind,
                                                    out powerUpId,
                                                    out payloadPath);
    }

    private static void TryAddSharedModuleScalingEntries(PlayerPowerUpsPreset sourcePreset,
                                                         PlayerStatScalingRule scalingRule,
                                                         SerializedProperty property,
                                                         DynamicBuffer<PlayerRuntimePowerUpScalingElement> scalingBuffer)
    {
        if (!TryExtractSharedModuleKey(scalingRule.StatKey, out string moduleId, out string payloadPath))
            return;

        float baseValue = property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
        AddSharedModuleEntriesForPowerUps(sourcePreset.ActivePowerUps,
                                          PlayerPowerUpUnlockKind.Active,
                                          moduleId,
                                          payloadPath,
                                          baseValue,
                                          property.propertyType == SerializedPropertyType.Integer,
                                          scalingRule.Formula,
                                          scalingBuffer);
        AddSharedModuleEntriesForPowerUps(sourcePreset.PassivePowerUps,
                                          PlayerPowerUpUnlockKind.Passive,
                                          moduleId,
                                          payloadPath,
                                          baseValue,
                                          property.propertyType == SerializedPropertyType.Integer,
                                          scalingRule.Formula,
                                          scalingBuffer);
    }

    private static void AddSharedModuleEntriesForPowerUps(IReadOnlyList<ModularPowerUpDefinition> powerUps,
                                                          PlayerPowerUpUnlockKind unlockKind,
                                                          string moduleId,
                                                          string payloadPath,
                                                          float baseValue,
                                                          bool isInteger,
                                                          string formula,
                                                          DynamicBuffer<PlayerRuntimePowerUpScalingElement> scalingBuffer)
    {
        if (powerUps == null ||
            string.IsNullOrWhiteSpace(moduleId) ||
            string.IsNullOrWhiteSpace(payloadPath))
        {
            return;
        }

        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp == null || powerUp.CommonData == null || string.IsNullOrWhiteSpace(powerUp.CommonData.PowerUpId))
                continue;

            IReadOnlyList<PowerUpModuleBinding> moduleBindings = powerUp.ModuleBindings;

            if (moduleBindings == null)
                continue;

            for (int bindingIndex = 0; bindingIndex < moduleBindings.Count; bindingIndex++)
            {
                PowerUpModuleBinding binding = moduleBindings[bindingIndex];

                if (binding == null || !binding.IsEnabled)
                    continue;

                if (!string.Equals(binding.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (binding.UseOverridePayload)
                    continue;

                scalingBuffer.Add(new PlayerRuntimePowerUpScalingElement
                {
                    PowerUpId = new FixedString64Bytes(powerUp.CommonData.PowerUpId.Trim()),
                    UnlockKind = unlockKind,
                    PayloadPath = new FixedString128Bytes(payloadPath),
                    BaseValue = baseValue,
                    IsInteger = isInteger ? (byte)1 : (byte)0,
                    Formula = new FixedString512Bytes(formula)
                });
                break;
            }
        }
    }

    private static bool TryResolvePowerUpKeyFromPropertyPath(PlayerPowerUpsPreset sourcePreset,
                                                             string propertyPath,
                                                             out PlayerPowerUpUnlockKind unlockKind,
                                                             out string powerUpId,
                                                             out string payloadPath)
    {
        unlockKind = default;
        powerUpId = string.Empty;
        payloadPath = string.Empty;

        if (sourcePreset == null || string.IsNullOrWhiteSpace(propertyPath))
            return false;

        IReadOnlyList<ModularPowerUpDefinition> powerUps;
        string root;

        if (propertyPath.StartsWith(ActivePowerUpsRoot, StringComparison.Ordinal))
        {
            unlockKind = PlayerPowerUpUnlockKind.Active;
            powerUps = sourcePreset.ActivePowerUps;
            root = ActivePowerUpsRoot;
        }
        else if (propertyPath.StartsWith(PassivePowerUpsRoot, StringComparison.Ordinal))
        {
            unlockKind = PlayerPowerUpUnlockKind.Passive;
            powerUps = sourcePreset.PassivePowerUps;
            root = PassivePowerUpsRoot;
        }
        else
        {
            return false;
        }

        if (!TryParsePowerUpArrayIndex(propertyPath, root, out int powerUpIndex))
            return false;

        if (powerUps == null || powerUpIndex < 0 || powerUpIndex >= powerUps.Count)
            return false;

        ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

        if (powerUp == null || powerUp.CommonData == null || string.IsNullOrWhiteSpace(powerUp.CommonData.PowerUpId))
            return false;

        string overridePrefix = ".overridePayload.";
        int payloadIndex = propertyPath.IndexOf(overridePrefix, StringComparison.Ordinal);

        if (payloadIndex < 0 || payloadIndex + overridePrefix.Length >= propertyPath.Length)
            return false;

        powerUpId = powerUp.CommonData.PowerUpId.Trim();
        payloadPath = propertyPath.Substring(payloadIndex + overridePrefix.Length);
        return !string.IsNullOrWhiteSpace(payloadPath);
    }

    private static bool TryParsePowerUpArrayIndex(string propertyPath, string root, out int powerUpIndex)
    {
        powerUpIndex = -1;

        if (string.IsNullOrWhiteSpace(propertyPath) || string.IsNullOrWhiteSpace(root))
            return false;

        if (!propertyPath.StartsWith(root, StringComparison.Ordinal))
            return false;

        int indexStart = root.Length;
        int indexEnd = propertyPath.IndexOf(']', indexStart);

        if (indexEnd <= indexStart)
            return false;

        string indexText = propertyPath.Substring(indexStart, indexEnd - indexStart);
        return int.TryParse(indexText, out powerUpIndex);
    }

    private static bool TryMapControllerFieldId(string statKey, out PlayerRuntimeControllerFieldId fieldId)
    {
        fieldId = default;

        switch (statKey)
        {
            case "movementSettings.discreteDirectionCount":
                fieldId = PlayerRuntimeControllerFieldId.MovementDiscreteDirectionCount;
                return true;
            case "movementSettings.directionOffsetDegrees":
                fieldId = PlayerRuntimeControllerFieldId.MovementDirectionOffsetDegrees;
                return true;
            case "movementSettings.values.baseSpeed":
                fieldId = PlayerRuntimeControllerFieldId.MovementBaseSpeed;
                return true;
            case "movementSettings.values.maxSpeed":
                fieldId = PlayerRuntimeControllerFieldId.MovementMaxSpeed;
                return true;
            case "movementSettings.values.acceleration":
                fieldId = PlayerRuntimeControllerFieldId.MovementAcceleration;
                return true;
            case "movementSettings.values.deceleration":
                fieldId = PlayerRuntimeControllerFieldId.MovementDeceleration;
                return true;
            case "movementSettings.values.oppositeDirectionBrakeMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.MovementOppositeDirectionBrakeMultiplier;
                return true;
            case "movementSettings.values.wallBounceCoefficient":
                fieldId = PlayerRuntimeControllerFieldId.MovementWallBounceCoefficient;
                return true;
            case "movementSettings.values.wallCollisionSkinWidth":
                fieldId = PlayerRuntimeControllerFieldId.MovementWallCollisionSkinWidth;
                return true;
            case "movementSettings.values.inputDeadZone":
                fieldId = PlayerRuntimeControllerFieldId.MovementInputDeadZone;
                return true;
            case "movementSettings.values.digitalReleaseGraceSeconds":
                fieldId = PlayerRuntimeControllerFieldId.MovementDigitalReleaseGraceSeconds;
                return true;
            case "lookSettings.discreteDirectionCount":
                fieldId = PlayerRuntimeControllerFieldId.LookDiscreteDirectionCount;
                return true;
            case "lookSettings.directionOffsetDegrees":
                fieldId = PlayerRuntimeControllerFieldId.LookDirectionOffsetDegrees;
                return true;
            case "lookSettings.rotationSpeed":
                fieldId = PlayerRuntimeControllerFieldId.LookRotationSpeed;
                return true;
            case "lookSettings.frontConeAngle":
                fieldId = PlayerRuntimeControllerFieldId.LookFrontConeAngleDegrees;
                return true;
            case "lookSettings.frontConeMaxSpeedMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookFrontConeMaxSpeedMultiplier;
                return true;
            case "lookSettings.frontConeAccelerationMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookFrontConeAccelerationMultiplier;
                return true;
            case "lookSettings.backConeAngle":
                fieldId = PlayerRuntimeControllerFieldId.LookBackConeAngleDegrees;
                return true;
            case "lookSettings.backConeMaxSpeedMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookBackConeMaxSpeedMultiplier;
                return true;
            case "lookSettings.backConeAccelerationMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookBackConeAccelerationMultiplier;
                return true;
            case "lookSettings.leftConeAngle":
                fieldId = PlayerRuntimeControllerFieldId.LookLeftConeAngleDegrees;
                return true;
            case "lookSettings.leftConeMaxSpeedMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookLeftConeMaxSpeedMultiplier;
                return true;
            case "lookSettings.leftConeAccelerationMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookLeftConeAccelerationMultiplier;
                return true;
            case "lookSettings.rightConeAngle":
                fieldId = PlayerRuntimeControllerFieldId.LookRightConeAngleDegrees;
                return true;
            case "lookSettings.rightConeMaxSpeedMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookRightConeMaxSpeedMultiplier;
                return true;
            case "lookSettings.rightConeAccelerationMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.LookRightConeAccelerationMultiplier;
                return true;
            case "lookSettings.values.rotationDamping":
                fieldId = PlayerRuntimeControllerFieldId.LookRotationDamping;
                return true;
            case "lookSettings.values.rotationMaxSpeed":
                fieldId = PlayerRuntimeControllerFieldId.LookRotationMaxSpeed;
                return true;
            case "lookSettings.values.rotationDeadZone":
                fieldId = PlayerRuntimeControllerFieldId.LookRotationDeadZone;
                return true;
            case "lookSettings.values.digitalReleaseGraceSeconds":
                fieldId = PlayerRuntimeControllerFieldId.LookDigitalReleaseGraceSeconds;
                return true;
            case "cameraSettings.followOffset.x":
                fieldId = PlayerRuntimeControllerFieldId.CameraFollowOffsetX;
                return true;
            case "cameraSettings.followOffset.y":
                fieldId = PlayerRuntimeControllerFieldId.CameraFollowOffsetY;
                return true;
            case "cameraSettings.followOffset.z":
                fieldId = PlayerRuntimeControllerFieldId.CameraFollowOffsetZ;
                return true;
            case "cameraSettings.values.followSpeed":
                fieldId = PlayerRuntimeControllerFieldId.CameraFollowSpeed;
                return true;
            case "cameraSettings.values.cameraLag":
                fieldId = PlayerRuntimeControllerFieldId.CameraLag;
                return true;
            case "cameraSettings.values.damping":
                fieldId = PlayerRuntimeControllerFieldId.CameraDamping;
                return true;
            case "cameraSettings.values.maxFollowDistance":
                fieldId = PlayerRuntimeControllerFieldId.CameraMaxFollowDistance;
                return true;
            case "cameraSettings.values.deadZoneRadius":
                fieldId = PlayerRuntimeControllerFieldId.CameraDeadZoneRadius;
                return true;
            case "shootingSettings.shootOffset.x":
                fieldId = PlayerRuntimeControllerFieldId.ShootingShootOffsetX;
                return true;
            case "shootingSettings.shootOffset.y":
                fieldId = PlayerRuntimeControllerFieldId.ShootingShootOffsetY;
                return true;
            case "shootingSettings.shootOffset.z":
                fieldId = PlayerRuntimeControllerFieldId.ShootingShootOffsetZ;
                return true;
            case "shootingSettings.values.shootSpeed":
                fieldId = PlayerRuntimeControllerFieldId.ShootingShootSpeed;
                return true;
            case "shootingSettings.values.rateOfFire":
                fieldId = PlayerRuntimeControllerFieldId.ShootingRateOfFire;
                return true;
            case "shootingSettings.values.projectileSizeMultiplier":
                fieldId = PlayerRuntimeControllerFieldId.ShootingProjectileSizeMultiplier;
                return true;
            case "shootingSettings.values.explosionRadius":
                fieldId = PlayerRuntimeControllerFieldId.ShootingExplosionRadius;
                return true;
            case "shootingSettings.values.range":
                fieldId = PlayerRuntimeControllerFieldId.ShootingRange;
                return true;
            case "shootingSettings.values.lifetime":
                fieldId = PlayerRuntimeControllerFieldId.ShootingLifetime;
                return true;
            case "shootingSettings.values.damage":
                fieldId = PlayerRuntimeControllerFieldId.ShootingDamage;
                return true;
            case "shootingSettings.values.maxPenetrations":
                fieldId = PlayerRuntimeControllerFieldId.ShootingMaxPenetrations;
                return true;
            case "healthStatistics.maxHealth":
                fieldId = PlayerRuntimeControllerFieldId.HealthMaxHealth;
                return true;
            case "healthStatistics.maxShield":
                fieldId = PlayerRuntimeControllerFieldId.HealthMaxShield;
                return true;
            default:
                return false;
        }
    }

    private static bool TryMapProgressionFieldId(string statKey,
                                                 out int phaseIndex,
                                                 out PlayerRuntimeProgressionFieldId fieldId)
    {
        phaseIndex = -1;
        fieldId = default;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        if (!statKey.StartsWith("gamePhasesDefinition.Array.data[", StringComparison.Ordinal))
            return false;

        int dataStartIndex = statKey.IndexOf("data[", StringComparison.Ordinal);
        int dataEndIndex = statKey.IndexOf(']', dataStartIndex);

        if (dataStartIndex < 0 || dataEndIndex <= dataStartIndex)
            return false;

        string token = statKey.Substring(dataStartIndex + 5, dataEndIndex - dataStartIndex - 5);
        int separatorIndex = token.IndexOf('|');
        string indexText = separatorIndex >= 0 ? token.Substring(0, separatorIndex) : token;

        if (!int.TryParse(indexText, out phaseIndex))
            return false;

        if (statKey.EndsWith(".startingRequiredLevelUpExp", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeProgressionFieldId.PhaseStartingRequiredLevelUpExp;
            return true;
        }

        if (statKey.EndsWith(".requiredExperienceGrouth", StringComparison.Ordinal))
        {
            fieldId = PlayerRuntimeProgressionFieldId.PhaseRequiredExperienceGrouth;
            return true;
        }

        phaseIndex = -1;
        return false;
    }

    private static bool TryExtractSharedModuleKey(string statKey,
                                                  out string moduleId,
                                                  out string payloadPath)
    {
        moduleId = string.Empty;
        payloadPath = string.Empty;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        if (!statKey.StartsWith(ModuleDefinitionsRoot, StringComparison.Ordinal))
            return false;

        int moduleIdTokenIndex = statKey.IndexOf("moduleId:", StringComparison.Ordinal);

        if (moduleIdTokenIndex < 0)
            return false;

        int moduleIdEndIndex = statKey.IndexOf(']', moduleIdTokenIndex);

        if (moduleIdEndIndex <= moduleIdTokenIndex)
            return false;

        moduleId = statKey.Substring(moduleIdTokenIndex + 9, moduleIdEndIndex - moduleIdTokenIndex - 9).Trim();

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        int dataPathIndex = statKey.IndexOf(".data.", StringComparison.Ordinal);

        if (dataPathIndex < 0 || dataPathIndex + 6 >= statKey.Length)
            return false;

        payloadPath = statKey.Substring(dataPathIndex + 6);
        return !string.IsNullOrWhiteSpace(payloadPath);
    }

    private static bool TryExtractPowerUpKey(string statKey,
                                             out PlayerPowerUpUnlockKind unlockKind,
                                             out string powerUpId,
                                             out string payloadPath)
    {
        unlockKind = default;
        powerUpId = string.Empty;
        payloadPath = string.Empty;

        if (statKey.StartsWith(ActivePowerUpsRoot, StringComparison.Ordinal))
        {
            unlockKind = PlayerPowerUpUnlockKind.Active;
        }
        else if (statKey.StartsWith(PassivePowerUpsRoot, StringComparison.Ordinal))
        {
            unlockKind = PlayerPowerUpUnlockKind.Passive;
        }
        else
        {
            return false;
        }

        int powerUpIdIndex = statKey.IndexOf("powerUpId:", StringComparison.Ordinal);

        if (powerUpIdIndex < 0)
            return false;

        int powerUpIdEndIndex = statKey.IndexOf(']', powerUpIdIndex);

        if (powerUpIdEndIndex <= powerUpIdIndex)
            return false;

        powerUpId = statKey.Substring(powerUpIdIndex + 10, powerUpIdEndIndex - powerUpIdIndex - 10).Trim();

        if (string.IsNullOrWhiteSpace(powerUpId))
            return false;

        string overridePrefix = ".overridePayload.";
        int payloadIndex = statKey.IndexOf(overridePrefix, StringComparison.Ordinal);

        if (payloadIndex < 0 || payloadIndex + overridePrefix.Length >= statKey.Length)
            return false;

        payloadPath = statKey.Substring(payloadIndex + overridePrefix.Length);
        return true;
    }
#endif
    #endregion

    #endregion
}
