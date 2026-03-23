using Unity.Entities;

/// <summary>
/// Reapplies runtime-scaled player configs for one entity from lookup-based callers outside the dedicated sync system.
/// </summary>
internal static class PlayerRuntimeScalingRefreshUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds runtime-scaled controller, progression, and power-up configs for one player entity when all required data is available.
    /// /params entity: Player entity being refreshed.
    /// /params scalableStatsLookup: Runtime scalable-stat buffer lookup.
    /// /params controllerScalingLookup: Controller scaling metadata lookup.
    /// /params baseMovementLookup: Immutable movement baseline lookup.
    /// /params runtimeMovementLookup: Mutable runtime movement config lookup.
    /// /params baseLookLookup: Immutable look baseline lookup.
    /// /params runtimeLookLookup: Mutable runtime look config lookup.
    /// /params baseCameraLookup: Immutable camera baseline lookup.
    /// /params runtimeCameraLookup: Mutable runtime camera config lookup.
    /// /params baseShootingLookup: Immutable shooting baseline lookup.
    /// /params runtimeShootingLookup: Mutable runtime shooting config lookup.
    /// /params baseHealthLookup: Immutable health baseline lookup.
    /// /params runtimeHealthLookup: Mutable runtime health config lookup.
    /// /params progressionScalingLookup: Progression scaling metadata lookup.
    /// /params baseGamePhasesLookup: Immutable progression-phase baseline lookup.
    /// /params runtimeGamePhasesLookup: Mutable runtime progression-phase lookup.
    /// /params basePowerUpConfigsLookup: Immutable modular power-up baseline lookup.
    /// /params powerUpScalingLookup: Runtime power-up scaling metadata lookup.
    /// /params powerUpsConfigLookup: Mutable active-slot config lookup.
    /// /params unlockCatalogLookup: Mutable unlock catalog lookup.
    /// /params equippedPassiveToolsLookup: Mutable equipped-passive buffer lookup.
    /// /params passiveToolsStateLookup: Mutable aggregated passive-state lookup.
    /// /params healthLookup: Mutable health component lookup.
    /// /params shieldLookup: Mutable shield component lookup.
    /// /params progressionConfigLookup: Runtime progression config lookup.
    /// /params experienceLookup: Mutable player experience lookup.
    /// /params levelLookup: Mutable player level lookup.
    /// /params experienceCollectionLookup: Mutable pickup-radius runtime lookup.
    /// /params runtimeScalingStateLookup: Mutable runtime-scaling sync state lookup.
    /// /params forceApply: True to bypass the scalable-stat hash short-circuit.
    /// /returns True when runtime-scaled data was rebuilt; otherwise false.
    /// </summary>
    public static bool TryApplyForEntity(Entity entity,
                                         BufferLookup<PlayerScalableStatElement> scalableStatsLookup,
                                         BufferLookup<PlayerRuntimeControllerScalingElement> controllerScalingLookup,
                                         ComponentLookup<PlayerBaseMovementConfig> baseMovementLookup,
                                         ComponentLookup<PlayerRuntimeMovementConfig> runtimeMovementLookup,
                                         ComponentLookup<PlayerBaseLookConfig> baseLookLookup,
                                         ComponentLookup<PlayerRuntimeLookConfig> runtimeLookLookup,
                                         ComponentLookup<PlayerBaseCameraConfig> baseCameraLookup,
                                         ComponentLookup<PlayerRuntimeCameraConfig> runtimeCameraLookup,
                                         ComponentLookup<PlayerBaseShootingConfig> baseShootingLookup,
                                         ComponentLookup<PlayerRuntimeShootingConfig> runtimeShootingLookup,
                                         ComponentLookup<PlayerBaseHealthStatisticsConfig> baseHealthLookup,
                                         ComponentLookup<PlayerRuntimeHealthStatisticsConfig> runtimeHealthLookup,
                                         BufferLookup<PlayerRuntimeProgressionScalingElement> progressionScalingLookup,
                                         BufferLookup<PlayerBaseGamePhaseElement> baseGamePhasesLookup,
                                         BufferLookup<PlayerRuntimeGamePhaseElement> runtimeGamePhasesLookup,
                                         BufferLookup<PlayerPowerUpBaseConfigElement> basePowerUpConfigsLookup,
                                         BufferLookup<PlayerRuntimePowerUpScalingElement> powerUpScalingLookup,
                                         ComponentLookup<PlayerPowerUpsConfig> powerUpsConfigLookup,
                                         BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup,
                                         BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup,
                                         ComponentLookup<PlayerPassiveToolsState> passiveToolsStateLookup,
                                         ComponentLookup<PlayerHealth> healthLookup,
                                         ComponentLookup<PlayerShield> shieldLookup,
                                         ComponentLookup<PlayerProgressionConfig> progressionConfigLookup,
                                         ComponentLookup<PlayerExperience> experienceLookup,
                                         ComponentLookup<PlayerLevel> levelLookup,
                                         ComponentLookup<PlayerExperienceCollection> experienceCollectionLookup,
                                         ComponentLookup<PlayerRuntimeScalingState> runtimeScalingStateLookup,
                                         bool forceApply)
    {
        if (!scalableStatsLookup.HasBuffer(entity) ||
            !controllerScalingLookup.HasBuffer(entity) ||
            !baseMovementLookup.HasComponent(entity) ||
            !runtimeMovementLookup.HasComponent(entity) ||
            !baseLookLookup.HasComponent(entity) ||
            !runtimeLookLookup.HasComponent(entity) ||
            !baseCameraLookup.HasComponent(entity) ||
            !runtimeCameraLookup.HasComponent(entity) ||
            !baseShootingLookup.HasComponent(entity) ||
            !runtimeShootingLookup.HasComponent(entity) ||
            !baseHealthLookup.HasComponent(entity) ||
            !runtimeHealthLookup.HasComponent(entity) ||
            !progressionScalingLookup.HasBuffer(entity) ||
            !baseGamePhasesLookup.HasBuffer(entity) ||
            !runtimeGamePhasesLookup.HasBuffer(entity) ||
            !basePowerUpConfigsLookup.HasBuffer(entity) ||
            !powerUpScalingLookup.HasBuffer(entity) ||
            !powerUpsConfigLookup.HasComponent(entity) ||
            !unlockCatalogLookup.HasBuffer(entity) ||
            !equippedPassiveToolsLookup.HasBuffer(entity) ||
            !passiveToolsStateLookup.HasComponent(entity) ||
            !healthLookup.HasComponent(entity) ||
            !shieldLookup.HasComponent(entity) ||
            !progressionConfigLookup.HasComponent(entity) ||
            !experienceLookup.HasComponent(entity) ||
            !levelLookup.HasComponent(entity) ||
            !experienceCollectionLookup.HasComponent(entity) ||
            !runtimeScalingStateLookup.HasComponent(entity))
        {
            return false;
        }

        DynamicBuffer<PlayerScalableStatElement> scalableStats = scalableStatsLookup[entity];
        DynamicBuffer<PlayerRuntimeControllerScalingElement> controllerScaling = controllerScalingLookup[entity];
        PlayerBaseMovementConfig baseMovement = baseMovementLookup[entity];
        PlayerRuntimeMovementConfig runtimeMovement = runtimeMovementLookup[entity];
        PlayerBaseLookConfig baseLook = baseLookLookup[entity];
        PlayerRuntimeLookConfig runtimeLook = runtimeLookLookup[entity];
        PlayerBaseCameraConfig baseCamera = baseCameraLookup[entity];
        PlayerRuntimeCameraConfig runtimeCamera = runtimeCameraLookup[entity];
        PlayerBaseShootingConfig baseShooting = baseShootingLookup[entity];
        PlayerRuntimeShootingConfig runtimeShooting = runtimeShootingLookup[entity];
        PlayerBaseHealthStatisticsConfig baseHealth = baseHealthLookup[entity];
        PlayerRuntimeHealthStatisticsConfig runtimeHealth = runtimeHealthLookup[entity];
        DynamicBuffer<PlayerRuntimeProgressionScalingElement> progressionScaling = progressionScalingLookup[entity];
        DynamicBuffer<PlayerBaseGamePhaseElement> baseGamePhases = baseGamePhasesLookup[entity];
        DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases = runtimeGamePhasesLookup[entity];
        DynamicBuffer<PlayerPowerUpBaseConfigElement> basePowerUpConfigs = basePowerUpConfigsLookup[entity];
        DynamicBuffer<PlayerRuntimePowerUpScalingElement> powerUpScaling = powerUpScalingLookup[entity];
        PlayerPowerUpsConfig powerUpsConfig = powerUpsConfigLookup[entity];
        DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = unlockCatalogLookup[entity];
        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools = equippedPassiveToolsLookup[entity];
        PlayerPassiveToolsState passiveToolsState = passiveToolsStateLookup[entity];
        PlayerHealth playerHealth = healthLookup[entity];
        PlayerShield playerShield = shieldLookup[entity];
        PlayerProgressionConfig progressionConfig = progressionConfigLookup[entity];
        PlayerExperience playerExperience = experienceLookup[entity];
        PlayerLevel playerLevel = levelLookup[entity];
        PlayerExperienceCollection playerExperienceCollection = experienceCollectionLookup[entity];
        PlayerRuntimeScalingState runtimeScalingState = runtimeScalingStateLookup[entity];
        bool rebuilt = PlayerRuntimeScalingApplyUtility.TryApply(scalableStats,
                                                                 controllerScaling,
                                                                 in baseMovement,
                                                                 ref runtimeMovement,
                                                                 in baseLook,
                                                                 ref runtimeLook,
                                                                 in baseCamera,
                                                                 ref runtimeCamera,
                                                                 in baseShooting,
                                                                 ref runtimeShooting,
                                                                 in baseHealth,
                                                                 ref runtimeHealth,
                                                                 progressionScaling,
                                                                 baseGamePhases,
                                                                 runtimeGamePhases,
                                                                 basePowerUpConfigs,
                                                                 powerUpScaling,
                                                                 ref powerUpsConfig,
                                                                 unlockCatalog,
                                                                 equippedPassiveTools,
                                                                 ref passiveToolsState,
                                                                 ref playerHealth,
                                                                 ref playerShield,
                                                                 progressionConfig,
                                                                 ref playerExperience,
                                                                 ref playerLevel,
                                                                 ref playerExperienceCollection,
                                                                 ref runtimeScalingState,
                                                                 forceApply);

        if (!rebuilt)
            return false;

        runtimeMovementLookup[entity] = runtimeMovement;
        runtimeLookLookup[entity] = runtimeLook;
        runtimeCameraLookup[entity] = runtimeCamera;
        runtimeShootingLookup[entity] = runtimeShooting;
        runtimeHealthLookup[entity] = runtimeHealth;
        powerUpsConfigLookup[entity] = powerUpsConfig;
        passiveToolsStateLookup[entity] = passiveToolsState;
        healthLookup[entity] = playerHealth;
        shieldLookup[entity] = playerShield;
        experienceLookup[entity] = playerExperience;
        levelLookup[entity] = playerLevel;
        experienceCollectionLookup[entity] = playerExperienceCollection;
        runtimeScalingStateLookup[entity] = runtimeScalingState;
        return true;
    }
    #endregion

    #endregion
}
