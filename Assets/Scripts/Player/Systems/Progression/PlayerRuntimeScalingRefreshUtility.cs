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
    /// entity: Player entity being refreshed.
    /// scalableStatsLookup: Runtime scalable-stat buffer lookup.
    /// controllerScalingLookup: Controller scaling metadata lookup.
    /// baseMovementLookup: Immutable movement baseline lookup.
    /// runtimeMovementLookup: Mutable runtime movement config lookup.
    /// baseLookLookup: Immutable look baseline lookup.
    /// runtimeLookLookup: Mutable runtime look config lookup.
    /// baseCameraLookup: Immutable camera baseline lookup.
    /// runtimeCameraLookup: Mutable runtime camera config lookup.
    /// baseShootingLookup: Immutable shooting baseline lookup.
    /// runtimeShootingLookup: Mutable runtime shooting config lookup.
    /// baseAppliedElementSlotsLookup: Immutable shooting applied-element slot baseline lookup.
    /// runtimeAppliedElementSlotsLookup: Mutable runtime shooting applied-element slot lookup.
    /// baseHealthLookup: Immutable health baseline lookup.
    /// runtimeHealthLookup: Mutable runtime health config lookup.
    /// progressionScalingLookup: Progression scaling metadata lookup.
    /// baseGamePhasesLookup: Immutable progression-phase baseline lookup.
    /// runtimeGamePhasesLookup: Mutable runtime progression-phase lookup.
    /// baseComboPassiveUnlocksLookup: Immutable combo passive-unlock baseline lookup.
    /// runtimeComboPassiveUnlocksLookup: Mutable runtime combo passive-unlock lookup.
    /// basePowerUpConfigsLookup: Immutable modular power-up baseline lookup.
    /// powerUpScalingLookup: Runtime power-up scaling metadata lookup.
    /// powerUpsConfigLookup: Mutable active-slot config lookup.
    /// unlockCatalogLookup: Mutable unlock catalog lookup.
    /// equippedPassiveToolsLookup: Mutable equipped-passive buffer lookup.
    /// passiveToolsStateLookup: Mutable aggregated passive-state lookup.
    /// healthLookup: Mutable health component lookup.
    /// shieldLookup: Mutable shield component lookup.
    /// progressionConfigLookup: Runtime progression config lookup.
    /// experienceLookup: Mutable player experience lookup.
    /// levelLookup: Mutable player level lookup.
    /// experienceCollectionLookup: Mutable pickup-radius runtime lookup.
    /// runtimeScalingStateLookup: Mutable runtime-scaling sync state lookup.
    /// forceApply: True to bypass the scalable-stat hash short-circuit.
    /// returns True when runtime-scaled data was rebuilt; otherwise false.
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
                                         BufferLookup<PlayerBaseShootingAppliedElementSlot> baseAppliedElementSlotsLookup,
                                         BufferLookup<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlotsLookup,
                                         ComponentLookup<PlayerBaseHealthStatisticsConfig> baseHealthLookup,
                                         ComponentLookup<PlayerRuntimeHealthStatisticsConfig> runtimeHealthLookup,
                                         BufferLookup<PlayerRuntimeProgressionScalingElement> progressionScalingLookup,
                                         BufferLookup<PlayerBaseGamePhaseElement> baseGamePhasesLookup,
                                         BufferLookup<PlayerRuntimeGamePhaseElement> runtimeGamePhasesLookup,
                                         ComponentLookup<PlayerBaseComboCounterConfig> baseComboConfigLookup,
                                         ComponentLookup<PlayerRuntimeComboCounterConfig> runtimeComboConfigLookup,
                                         BufferLookup<PlayerBaseComboRankElement> baseComboRanksLookup,
                                         BufferLookup<PlayerRuntimeComboRankElement> runtimeComboRanksLookup,
                                         BufferLookup<PlayerBaseComboPassiveUnlockElement> baseComboPassiveUnlocksLookup,
                                         BufferLookup<PlayerRuntimeComboPassiveUnlockElement> runtimeComboPassiveUnlocksLookup,
                                         BufferLookup<PlayerRuntimeComboCounterScalingElement> comboScalingLookup,
                                         ComponentLookup<PlayerComboCounterState> comboCounterStateLookup,
                                         BufferLookup<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulaLookup,
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
            !baseAppliedElementSlotsLookup.HasBuffer(entity) ||
            !runtimeAppliedElementSlotsLookup.HasBuffer(entity) ||
            !baseHealthLookup.HasComponent(entity) ||
            !runtimeHealthLookup.HasComponent(entity) ||
            !progressionScalingLookup.HasBuffer(entity) ||
            !baseGamePhasesLookup.HasBuffer(entity) ||
            !runtimeGamePhasesLookup.HasBuffer(entity) ||
            !baseComboConfigLookup.HasComponent(entity) ||
            !runtimeComboConfigLookup.HasComponent(entity) ||
            !baseComboRanksLookup.HasBuffer(entity) ||
            !runtimeComboRanksLookup.HasBuffer(entity) ||
            !baseComboPassiveUnlocksLookup.HasBuffer(entity) ||
            !runtimeComboPassiveUnlocksLookup.HasBuffer(entity) ||
            !comboScalingLookup.HasBuffer(entity) ||
            !comboCounterStateLookup.HasComponent(entity) ||
            !characterTuningFormulaLookup.HasBuffer(entity) ||
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
        DynamicBuffer<PlayerBaseShootingAppliedElementSlot> baseAppliedElementSlots = baseAppliedElementSlotsLookup[entity];
        DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> runtimeAppliedElementSlots = runtimeAppliedElementSlotsLookup[entity];
        PlayerBaseHealthStatisticsConfig baseHealth = baseHealthLookup[entity];
        PlayerRuntimeHealthStatisticsConfig runtimeHealth = runtimeHealthLookup[entity];
        DynamicBuffer<PlayerRuntimeProgressionScalingElement> progressionScaling = progressionScalingLookup[entity];
        DynamicBuffer<PlayerBaseGamePhaseElement> baseGamePhases = baseGamePhasesLookup[entity];
        DynamicBuffer<PlayerRuntimeGamePhaseElement> runtimeGamePhases = runtimeGamePhasesLookup[entity];
        PlayerBaseComboCounterConfig baseComboConfig = baseComboConfigLookup[entity];
        PlayerRuntimeComboCounterConfig runtimeComboConfig = runtimeComboConfigLookup[entity];
        DynamicBuffer<PlayerBaseComboRankElement> baseComboRanks = baseComboRanksLookup[entity];
        DynamicBuffer<PlayerRuntimeComboRankElement> runtimeComboRanks = runtimeComboRanksLookup[entity];
        DynamicBuffer<PlayerBaseComboPassiveUnlockElement> baseComboPassiveUnlocks = baseComboPassiveUnlocksLookup[entity];
        DynamicBuffer<PlayerRuntimeComboPassiveUnlockElement> runtimeComboPassiveUnlocks = runtimeComboPassiveUnlocksLookup[entity];
        DynamicBuffer<PlayerRuntimeComboCounterScalingElement> comboScaling = comboScalingLookup[entity];
        PlayerComboCounterState comboCounterState = comboCounterStateLookup[entity];
        DynamicBuffer<PlayerPowerUpCharacterTuningFormulaElement> characterTuningFormulas = characterTuningFormulaLookup[entity];
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
                                                                 baseAppliedElementSlots,
                                                                 runtimeAppliedElementSlots,
                                                                 in baseHealth,
                                                                 ref runtimeHealth,
                                                                 progressionScaling,
                                                                 baseGamePhases,
                                                                 runtimeGamePhases,
                                                                 in baseComboConfig,
                                                                 ref runtimeComboConfig,
                                                                 baseComboRanks,
                                                                 runtimeComboRanks,
                                                                 baseComboPassiveUnlocks,
                                                                 runtimeComboPassiveUnlocks,
                                                                 comboScaling,
                                                                 characterTuningFormulas,
                                                                 ref comboCounterState,
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
        runtimeComboConfigLookup[entity] = runtimeComboConfig;
        comboCounterStateLookup[entity] = comboCounterState;
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
