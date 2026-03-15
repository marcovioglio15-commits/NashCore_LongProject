using System.Globalization;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Consumes milestone power-up selection commands and applies either the chosen unlock or the configured skip compensations.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLevelUpSystem))]
[UpdateBefore(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerMilestonePowerUpSelectionResolveSystem : ISystem
{
    #region Nested Types
    private struct ResolvedMilestoneSelectionCommand
    {
        public PlayerMilestoneSelectionCommandType CommandType;
        public int OfferIndex;
    }
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers all runtime data required to resolve milestone selection commands.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMilestonePowerUpSelectionCommand>();
        state.RequireForUpdate<PlayerMilestonePowerUpSelectionOfferElement>();
        state.RequireForUpdate<PlayerMilestonePowerUpSelectionState>();
        state.RequireForUpdate<PlayerMilestoneTimeScaleResumeState>();
        state.RequireForUpdate<PlayerPowerUpUnlockCatalogElement>();
        state.RequireForUpdate<PlayerProgressionConfig>();
        state.RequireForUpdate<PlayerLevel>();
        state.RequireForUpdate<PlayerExperience>();
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerShield>();
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Processes queued HUD selection commands and resolves the active milestone outcome immediately.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>

    public void OnUpdate(ref SystemState state)
    {
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        ComponentLookup<PlayerPassiveToolsState> passiveToolsStateLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(false);
        ComponentLookup<PlayerMilestoneTimeScaleResumeState> milestoneTimeScaleResumeStateLookup = SystemAPI.GetComponentLookup<PlayerMilestoneTimeScaleResumeState>(false);
        ComponentLookup<PlayerProgressionConfig> progressionConfigLookup = SystemAPI.GetComponentLookup<PlayerProgressionConfig>(true);
        ComponentLookup<PlayerLevel> playerLevelLookup = SystemAPI.GetComponentLookup<PlayerLevel>(true);
        ComponentLookup<PlayerExperience> playerExperienceLookup = SystemAPI.GetComponentLookup<PlayerExperience>(false);
        ComponentLookup<PlayerHealth> playerHealthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(false);
        ComponentLookup<PlayerShield> playerShieldLookup = SystemAPI.GetComponentLookup<PlayerShield>(false);
        ComponentLookup<PlayerPowerUpsConfig> powerUpsConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsConfig>(false);
        ComponentLookup<PlayerPowerUpsState> powerUpsStateLookup = SystemAPI.GetComponentLookup<PlayerPowerUpsState>(false);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PlayerPowerUpContainerInteractionConfig> powerUpContainerConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpContainerInteractionConfig>(true);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(false);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((DynamicBuffer<PlayerMilestonePowerUpSelectionCommand> selectionCommands,
                  DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers,
                  DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                  RefRW<PlayerMilestonePowerUpSelectionState> selectionState,
                  Entity entity)
                 in SystemAPI.Query<DynamicBuffer<PlayerMilestonePowerUpSelectionCommand>,
                                    DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement>,
                                    DynamicBuffer<PlayerPowerUpUnlockCatalogElement>,
                                    RefRW<PlayerMilestonePowerUpSelectionState>>().WithEntityAccess())
        {
            if (!passiveToolsStateLookup.HasComponent(entity) ||
                !milestoneTimeScaleResumeStateLookup.HasComponent(entity) ||
                !progressionConfigLookup.HasComponent(entity) ||
                !playerLevelLookup.HasComponent(entity) ||
                !playerExperienceLookup.HasComponent(entity) ||
                !playerHealthLookup.HasComponent(entity) ||
                !playerShieldLookup.HasComponent(entity) ||
                !powerUpsConfigLookup.HasComponent(entity) ||
                !powerUpsStateLookup.HasComponent(entity) ||
                !equippedPassiveToolsLookup.HasBuffer(entity))
                continue;

            DynamicBuffer<PlayerMilestonePowerUpSelectionCommand> selectionCommandsBuffer = selectionCommands;
            DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffersBuffer = selectionOffers;
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalogBuffer = unlockCatalog;
            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = equippedPassiveToolsLookup[entity];

            if (selectionCommandsBuffer.Length <= 0)
                continue;

            if (!TryResolveRequestedCommand(selectionCommandsBuffer, out ResolvedMilestoneSelectionCommand resolvedCommand))
            {
                selectionCommandsBuffer.Clear();
                continue;
            }

            selectionCommandsBuffer.Clear();

            if (selectionState.ValueRO.IsSelectionActive == 0)
            {
                Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                                               "[PlayerMilestonePowerUpSelectionResolveSystem] Ignored stale selection command on entity {0}: no active milestone selection.",
                                               entity.Index));
                continue;
            }

            PlayerMilestonePowerUpSelectionState selectionStateValue = selectionState.ValueRO;
            PlayerMilestoneTimeScaleResumeState milestoneTimeScaleResumeStateValue = milestoneTimeScaleResumeStateLookup[entity];
            PlayerProgressionConfig progressionConfigValue = progressionConfigLookup[entity];
            PlayerLevel playerLevelValue = playerLevelLookup[entity];
            PlayerPowerUpsConfig powerUpsConfigValue = powerUpsConfigLookup[entity];
            PlayerPowerUpsState powerUpsStateValue = powerUpsStateLookup[entity];
            PlayerExperience playerExperienceValue = playerExperienceLookup[entity];
            PlayerHealth playerHealthValue = playerHealthLookup[entity];
            PlayerShield playerShieldValue = playerShieldLookup[entity];

            if (resolvedCommand.CommandType == PlayerMilestoneSelectionCommandType.Skip)
            {
                int skippedMilestoneLevel = selectionStateValue.MilestoneLevel;
                int appliedCompensationCount = PlayerMilestoneSelectionOutcomeUtility.ApplySkipCompensations(progressionConfigValue,
                                                                                                              in selectionStateValue,
                                                                                                              ref playerHealthValue,
                                                                                                              ref playerShieldValue,
                                                                                                              ref playerExperienceValue,
                                                                                                              in playerLevelValue,
                                                                                                              in powerUpsConfigValue,
                                                                                                              ref powerUpsStateValue);

                playerHealthLookup[entity] = playerHealthValue;
                playerShieldLookup[entity] = playerShieldValue;
                playerExperienceLookup[entity] = playerExperienceValue;
                powerUpsStateLookup[entity] = powerUpsStateValue;
                FinalizeSelection(progressionConfigValue,
                                  selectionOffersBuffer,
                                  ref selectionStateValue,
                                  ref milestoneTimeScaleResumeStateValue);
                selectionState.ValueRW = selectionStateValue;
                milestoneTimeScaleResumeStateLookup[entity] = milestoneTimeScaleResumeStateValue;

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                        "[PlayerMilestonePowerUpSelectionResolveSystem] Skip executed for milestone {0}. Applied compensation entries: {1}.",
                                        skippedMilestoneLevel,
                                        appliedCompensationCount));
                continue;
            }

            int selectedOfferIndex = resolvedCommand.OfferIndex;

            if (selectedOfferIndex < 0 || selectedOfferIndex >= selectionOffersBuffer.Length)
            {
                Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                                               "[PlayerMilestonePowerUpSelectionResolveSystem] Invalid offer index {0}. Active offers: {1}.",
                                               selectedOfferIndex,
                                               selectionOffersBuffer.Length));
                continue;
            }

            PlayerMilestonePowerUpSelectionOfferElement selectedOffer = selectionOffersBuffer[selectedOfferIndex];
            int selectedCatalogIndex = selectedOffer.CatalogIndex;

            if (selectedCatalogIndex < 0 || selectedCatalogIndex >= unlockCatalogBuffer.Length)
            {
                Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                                               "[PlayerMilestonePowerUpSelectionResolveSystem] Invalid catalog index {0} for selected offer.",
                                               selectedCatalogIndex));
                continue;
            }

            PlayerPassiveToolsState passiveToolsState = passiveToolsStateLookup[entity];
            PlayerPowerUpUnlockCatalogElement selectedCatalogEntry = unlockCatalogBuffer[selectedCatalogIndex];
            bool wasAlreadyUnlocked = selectedCatalogEntry.IsUnlocked != 0;
            bool runtimeApplied = ApplySelectedUnlock(selectedOffer.UnlockKind,
                                                      in selectedCatalogEntry,
                                                      in physicsWorldSingleton,
                                                      ref powerUpsConfigValue,
                                                      ref powerUpsStateValue,
                                                      equippedPassiveToolsBuffer,
                                                      ref passiveToolsState,
                                                      entity,
                                                      in localTransformLookup,
                                                      in powerUpContainerConfigLookup,
                                                      ref commandBuffer,
                                                      out string applyTarget);

            if (runtimeApplied)
            {
                selectedCatalogEntry.IsUnlocked = 1;
                unlockCatalogBuffer[selectedCatalogIndex] = selectedCatalogEntry;
            }

            powerUpsConfigLookup[entity] = powerUpsConfigValue;
            powerUpsStateLookup[entity] = powerUpsStateValue;
            passiveToolsStateLookup[entity] = passiveToolsState;
            FinalizeSelection(progressionConfigValue,
                              selectionOffersBuffer,
                              ref selectionStateValue,
                              ref milestoneTimeScaleResumeStateValue);
            selectionState.ValueRW = selectionStateValue;
            milestoneTimeScaleResumeStateLookup[entity] = milestoneTimeScaleResumeStateValue;

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                    "[PlayerMilestonePowerUpSelectionResolveSystem] Selected offer {0}: Power-Up '{1}' ({2}). Already Unlocked: {3}. Runtime Applied: {4} ({5}).",
                                    selectedOfferIndex,
                                    selectedOffer.PowerUpId.ToString(),
                                    selectedOffer.UnlockKind,
                                    wasAlreadyUnlocked ? "Yes" : "No",
                                    runtimeApplied ? "Yes" : "No",
                                    applyTarget));
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Commands
    /// <summary>
    /// Resolves the first actionable milestone-selection command from the queue.
    /// </summary>
    /// <param name="selectionCommands">Queued HUD selection commands.</param>
    /// <param name="resolvedCommand">Resolved command payload when found.</param>
    /// <returns>True when one actionable command exists; otherwise false.</returns>
    private static bool TryResolveRequestedCommand(DynamicBuffer<PlayerMilestonePowerUpSelectionCommand> selectionCommands,
                                                   out ResolvedMilestoneSelectionCommand resolvedCommand)
    {
        resolvedCommand = default;

        for (int commandIndex = 0; commandIndex < selectionCommands.Length; commandIndex++)
        {
            PlayerMilestonePowerUpSelectionCommand selectionCommand = selectionCommands[commandIndex];

            if (selectionCommand.CommandType == PlayerMilestoneSelectionCommandType.Skip)
            {
                resolvedCommand = new ResolvedMilestoneSelectionCommand
                {
                    CommandType = PlayerMilestoneSelectionCommandType.Skip,
                    OfferIndex = -1
                };
                return true;
            }

            if (selectionCommand.OfferIndex < 0)
                continue;

            resolvedCommand = new ResolvedMilestoneSelectionCommand
            {
                CommandType = PlayerMilestoneSelectionCommandType.SelectOffer,
                OfferIndex = selectionCommand.OfferIndex
            };
            return true;
        }

        return false;
    }

    /// <summary>
    /// Closes the active milestone selection and starts the smooth Time.timeScale resume.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression config containing the resume duration.</param>
    /// <param name="selectionOffers">Offer buffer cleared when the selection closes.</param>
    /// <param name="selectionState">Selection state reset to inactive defaults.</param>
    /// <param name="resumeState">Time.timeScale resume state configured in place.</param>

    private static void FinalizeSelection(PlayerProgressionConfig progressionConfig,
                                          DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers,
                                          ref PlayerMilestonePowerUpSelectionState selectionState,
                                          ref PlayerMilestoneTimeScaleResumeState resumeState)
    {
        PlayerMilestoneSelectionOutcomeUtility.ResetSelection(selectionOffers, ref selectionState);
        PlayerMilestoneSelectionOutcomeUtility.BeginTimeScaleResume(progressionConfig, ref resumeState);
    }

    /// <summary>
    /// Applies the selected unlock payload to runtime active/passive player data.
    /// </summary>
    /// <param name="unlockKind">Selected unlock payload type.</param>
    /// <param name="selectedCatalogEntry">Selected catalog entry data.</param>
    /// <param name="powerUpsConfig">Runtime active-slot config to mutate.</param>
    /// <param name="powerUpsState">Runtime active-slot state to mutate.</param>
    /// <param name="equippedPassiveTools">Runtime passive-tools buffer.</param>
    /// <param name="passiveToolsState">Aggregated runtime passive state.</param>
    /// <param name="applyTarget">Debug-friendly destination where the unlock was applied.</param>
    /// <returns>True when runtime state changed; otherwise false.</returns>
    private static bool ApplySelectedUnlock(PlayerPowerUpUnlockKind unlockKind,
                                            in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                            in PhysicsWorldSingleton physicsWorldSingleton,
                                            ref PlayerPowerUpsConfig powerUpsConfig,
                                            ref PlayerPowerUpsState powerUpsState,
                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                            ref PlayerPassiveToolsState passiveToolsState,
                                            Entity playerEntity,
                                            in ComponentLookup<LocalTransform> localTransformLookup,
                                            in ComponentLookup<PlayerPowerUpContainerInteractionConfig> powerUpContainerConfigLookup,
                                            ref EntityCommandBuffer commandBuffer,
                                            out string applyTarget)
    {
        switch (unlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                return TryEquipActiveUnlock(in selectedCatalogEntry.ActiveSlotConfig,
                                            in physicsWorldSingleton,
                                            ref powerUpsConfig,
                                            ref powerUpsState,
                                            playerEntity,
                                            in localTransformLookup,
                                            in powerUpContainerConfigLookup,
                                            ref commandBuffer,
                                            out applyTarget);
            case PlayerPowerUpUnlockKind.Passive:
                return TryEquipPassiveUnlock(in selectedCatalogEntry.PassiveToolConfig,
                                             equippedPassiveTools,
                                             ref passiveToolsState,
                                             out applyTarget);
            default:
                applyTarget = "UnknownUnlockKind";
                return false;
        }
    }
    #endregion

    #region Active Unlock
    /// <summary>
    /// Equips one active unlock into the first available or replaceable runtime slot.
    /// </summary>
    /// <param name="activeSlotConfig">Unlocked active-slot config payload.</param>
    /// <param name="powerUpsConfig">Runtime active-slot config to mutate.</param>
    /// <param name="powerUpsState">Runtime active-slot state to reset.</param>
    /// <param name="playerEntity">Player entity receiving the new active unlock.</param>
    /// <param name="localTransformLookup">Read-only player transform lookup used to position dropped containers.</param>
    /// <param name="powerUpContainerConfigLookup">Read-only lookup of player-side dropped-container settings.</param>
    /// <param name="commandBuffer">ECB used to spawn dropped containers when one active is replaced.</param>
    /// <param name="applyTarget">Debug label of the slot where the unlock was applied.</param>
    /// <returns>True when the active slot was applied; otherwise false.</returns>
    private static bool TryEquipActiveUnlock(in PlayerPowerUpSlotConfig activeSlotConfig,
                                             in PhysicsWorldSingleton physicsWorldSingleton,
                                             ref PlayerPowerUpsConfig powerUpsConfig,
                                             ref PlayerPowerUpsState powerUpsState,
                                             Entity playerEntity,
                                             in ComponentLookup<LocalTransform> localTransformLookup,
                                             in ComponentLookup<PlayerPowerUpContainerInteractionConfig> powerUpContainerConfigLookup,
                                             ref EntityCommandBuffer commandBuffer,
                                             out string applyTarget)
    {
        applyTarget = "ActiveSlot";

        if (activeSlotConfig.IsDefined == 0)
        {
            applyTarget = "InvalidActiveConfig";
            return false;
        }

        PlayerPowerUpContainerStoredStateMode storedStateMode = PlayerPowerUpContainerStoredStateMode.PreserveEnergyAndCooldown;

        if (powerUpContainerConfigLookup.HasComponent(playerEntity))
            storedStateMode = powerUpContainerConfigLookup[playerEntity].StoredStateMode;

        if (!PlayerPowerUpLoadoutRuntimeUtility.TryEquipIntoOldestSlot(in activeSlotConfig,
                                                                       storedStateMode,
                                                                       ref powerUpsConfig,
                                                                       ref powerUpsState,
                                                                       out int targetSlotIndex,
                                                                       out PlayerStoredActivePowerUpData replacedPowerUp))
        {
            applyTarget = "NoReplaceableSlot";
            return false;
        }

        bool spawnedDroppedContainer = false;

        if (replacedPowerUp.SlotConfig.IsDefined != 0 &&
            powerUpContainerConfigLookup.HasComponent(playerEntity) &&
            localTransformLookup.HasComponent(playerEntity))
        {
            PlayerPowerUpContainerInteractionConfig interactionConfig = powerUpContainerConfigLookup[playerEntity];
            LocalTransform playerTransform = localTransformLookup[playerEntity];
            spawnedDroppedContainer = PlayerPowerUpContainerSpawnUtility.TrySpawnDroppedContainer(in physicsWorldSingleton,
                                                                                                  in playerTransform,
                                                                                                  in interactionConfig,
                                                                                                  in replacedPowerUp,
                                                                                                  ref commandBuffer);
        }

        switch (targetSlotIndex)
        {
            case 0:
                applyTarget = spawnedDroppedContainer ? "PrimaryOldestReplacedAndDropped" : "PrimaryOldestReplaced";
                return true;
            case 1:
                applyTarget = spawnedDroppedContainer ? "SecondaryOldestReplacedAndDropped" : "SecondaryOldestReplaced";
                return true;
            default:
                applyTarget = "UnknownTargetSlot";
                return false;
        }
    }
    #endregion

    #region Passive Unlock
    /// <summary>
    /// Equips one passive unlock and rebuilds aggregated passive runtime state.
    /// </summary>
    /// <param name="passiveToolConfig">Unlocked passive-tool payload.</param>
    /// <param name="equippedPassiveTools">Runtime equipped passives buffer.</param>
    /// <param name="passiveToolsState">Aggregated passive runtime state to update.</param>
    /// <param name="applyTarget">Debug label describing the passive-apply result.</param>
    /// <returns>True when a passive tool was added; otherwise false.</returns>
    private static bool TryEquipPassiveUnlock(in PlayerPassiveToolConfig passiveToolConfig,
                                              DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                              ref PlayerPassiveToolsState passiveToolsState,
                                              out string applyTarget)
    {
        applyTarget = "PassiveBuffer";

        if (passiveToolConfig.IsDefined == 0)
        {
            applyTarget = "InvalidPassiveConfig";
            return false;
        }

        if (ContainsPassiveToolKind(equippedPassiveTools, passiveToolConfig.ToolKind))
        {
            applyTarget = "AlreadyEquipped";
            return false;
        }

        equippedPassiveTools.Add(new EquippedPassiveToolElement
        {
            Tool = passiveToolConfig
        });
        passiveToolsState = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(equippedPassiveTools);
        applyTarget = "PassiveAdded";
        return true;
    }

    /// <summary>
    /// Checks whether one passive tool kind is already present in the equipped buffer.
    /// </summary>
    /// <param name="equippedPassiveTools">Runtime equipped passives buffer.</param>
    /// <param name="toolKind">Passive tool kind to test.</param>
    /// <returns>True when at least one matching passive tool kind exists; otherwise false.</returns>
    private static bool ContainsPassiveToolKind(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools, PassiveToolKind toolKind)
    {
        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            PlayerPassiveToolConfig candidate = equippedPassiveTools[passiveIndex].Tool;

            if (candidate.IsDefined == 0)
                continue;

            if (candidate.ToolKind != toolKind)
                continue;

            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
