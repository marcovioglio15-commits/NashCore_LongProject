using System.Globalization;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Consumes milestone power-up selection commands and applies the chosen unlock to runtime player state.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLevelUpSystem))]
[UpdateBefore(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerMilestonePowerUpSelectionResolveSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers all runtime data required to resolve milestone selection commands.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMilestonePowerUpSelectionCommand>();
        state.RequireForUpdate<PlayerMilestonePowerUpSelectionOfferElement>();
        state.RequireForUpdate<PlayerMilestonePowerUpSelectionState>();
        state.RequireForUpdate<PlayerPowerUpUnlockCatalogElement>();
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
    }

    /// <summary>
    /// Processes queued HUD selection commands and applies the selected unlock immediately.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerPassiveToolsState> passiveToolsStateLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(false);

        foreach ((DynamicBuffer<PlayerMilestonePowerUpSelectionCommand> selectionCommands,
                  DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers,
                  DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                  RefRW<PlayerMilestonePowerUpSelectionState> selectionState,
                  RefRW<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                  Entity entity)
                 in SystemAPI.Query<DynamicBuffer<PlayerMilestonePowerUpSelectionCommand>,
                                    DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement>,
                                    DynamicBuffer<PlayerPowerUpUnlockCatalogElement>,
                                    RefRW<PlayerMilestonePowerUpSelectionState>,
                                    RefRW<PlayerPowerUpsConfig>,
                                    RefRW<PlayerPowerUpsState>,
                                    DynamicBuffer<EquippedPassiveToolElement>>().WithEntityAccess())
        {
            if (!passiveToolsStateLookup.HasComponent(entity))
                continue;

            DynamicBuffer<PlayerMilestonePowerUpSelectionCommand> selectionCommandsBuffer = selectionCommands;
            DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffersBuffer = selectionOffers;
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalogBuffer = unlockCatalog;
            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = equippedPassiveTools;

            if (selectionCommandsBuffer.Length <= 0)
                continue;

            int selectedOfferIndex = ResolveRequestedOfferIndex(selectionCommandsBuffer);
            selectionCommandsBuffer.Clear();

            if (selectionState.ValueRO.IsSelectionActive == 0)
            {
                Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                                               "[PlayerMilestonePowerUpSelectionResolveSystem] Ignored stale selection command on entity {0}: no active milestone selection.",
                                               entity.Index));
                continue;
            }

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
            selectedCatalogEntry.IsUnlocked = 1;
            unlockCatalogBuffer[selectedCatalogIndex] = selectedCatalogEntry;

            bool runtimeApplied = ApplySelectedUnlock(selectedOffer.UnlockKind,
                                                      in selectedCatalogEntry,
                                                      ref powerUpsConfig.ValueRW,
                                                      ref powerUpsState.ValueRW,
                                                      equippedPassiveToolsBuffer,
                                                      ref passiveToolsState,
                                                      out string applyTarget);
            passiveToolsStateLookup[entity] = passiveToolsState;

            selectionOffersBuffer.Clear();
            selectionState.ValueRW = new PlayerMilestonePowerUpSelectionState
            {
                IsSelectionActive = 0,
                MilestoneLevel = 0,
                OfferCount = 0
            };

            if (Time.timeScale <= 0f)
                Time.timeScale = 1f;

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                    "[PlayerMilestonePowerUpSelectionResolveSystem] Selected offer {0}: Power-Up '{1}' ({2}). Already Unlocked: {3}. Runtime Applied: {4} ({5}).",
                                    selectedOfferIndex,
                                    selectedOffer.PowerUpId.ToString(),
                                    selectedOffer.UnlockKind,
                                    wasAlreadyUnlocked ? "Yes" : "No",
                                    runtimeApplied ? "Yes" : "No",
                                    applyTarget));
        }
    }
    #endregion

    #region Commands
    /// <summary>
    /// Resolves the first valid requested offer index from the command buffer.
    /// </summary>
    /// <param name="selectionCommands">Queued HUD selection commands.</param>
    /// <returns>Requested offer index, or -1 when none is valid.</returns>
    private static int ResolveRequestedOfferIndex(DynamicBuffer<PlayerMilestonePowerUpSelectionCommand> selectionCommands)
    {
        for (int commandIndex = 0; commandIndex < selectionCommands.Length; commandIndex++)
        {
            int offerIndex = selectionCommands[commandIndex].OfferIndex;

            if (offerIndex < 0)
                continue;

            return offerIndex;
        }

        return -1;
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
                                            ref PlayerPowerUpsConfig powerUpsConfig,
                                            ref PlayerPowerUpsState powerUpsState,
                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                            ref PlayerPassiveToolsState passiveToolsState,
                                            out string applyTarget)
    {
        switch (unlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                return TryEquipActiveUnlock(in selectedCatalogEntry.ActiveSlotConfig,
                                            ref powerUpsConfig,
                                            ref powerUpsState,
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
    /// <param name="applyTarget">Debug label of the slot where the unlock was applied.</param>
    /// <returns>True when the active slot was applied; otherwise false.</returns>
    private static bool TryEquipActiveUnlock(in PlayerPowerUpSlotConfig activeSlotConfig,
                                             ref PlayerPowerUpsConfig powerUpsConfig,
                                             ref PlayerPowerUpsState powerUpsState,
                                             out string applyTarget)
    {
        applyTarget = "ActiveSlot";

        if (activeSlotConfig.IsDefined == 0)
        {
            applyTarget = "InvalidActiveConfig";
            return false;
        }

        int targetSlotIndex = ResolveActiveTargetSlot(in powerUpsConfig);

        switch (targetSlotIndex)
        {
            case 0:
                powerUpsConfig.PrimarySlot = activeSlotConfig;
                ResetPrimaryActiveRuntimeState(ref powerUpsState, in activeSlotConfig);
                applyTarget = "Primary";
                return true;
            case 1:
                powerUpsConfig.SecondarySlot = activeSlotConfig;
                ResetSecondaryActiveRuntimeState(ref powerUpsState, in activeSlotConfig);
                applyTarget = "Secondary";
                return true;
            default:
                applyTarget = "NoReplaceableSlot";
                return false;
        }
    }

    /// <summary>
    /// Resolves which active slot should receive a newly unlocked active power-up.
    /// </summary>
    /// <param name="powerUpsConfig">Current runtime active-slot config.</param>
    /// <returns>0 for primary, 1 for secondary, or -1 when no slot can be replaced.</returns>
    private static int ResolveActiveTargetSlot(in PlayerPowerUpsConfig powerUpsConfig)
    {
        if (powerUpsConfig.PrimarySlot.IsDefined == 0)
            return 0;

        if (powerUpsConfig.SecondarySlot.IsDefined == 0)
            return 1;

        if (powerUpsConfig.SecondarySlot.Unreplaceable == 0)
            return 1;

        if (powerUpsConfig.PrimarySlot.Unreplaceable == 0)
            return 0;

        return -1;
    }

    /// <summary>
    /// Resets primary-slot runtime resource state after active-slot replacement.
    /// </summary>
    /// <param name="powerUpsState">Runtime active-slot state.</param>
    /// <param name="slotConfig">Newly applied active-slot config.</param>
    /// <returns>Void.</returns>
    private static void ResetPrimaryActiveRuntimeState(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpSlotConfig slotConfig)
    {
        powerUpsState.PrimaryEnergy = math.max(0f, slotConfig.MaximumEnergy);
        powerUpsState.PrimaryCooldownRemaining = 0f;
        powerUpsState.PrimaryCharge = 0f;
        powerUpsState.PrimaryIsCharging = 0;
    }

    /// <summary>
    /// Resets secondary-slot runtime resource state after active-slot replacement.
    /// </summary>
    /// <param name="powerUpsState">Runtime active-slot state.</param>
    /// <param name="slotConfig">Newly applied active-slot config.</param>
    /// <returns>Void.</returns>
    private static void ResetSecondaryActiveRuntimeState(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpSlotConfig slotConfig)
    {
        powerUpsState.SecondaryEnergy = math.max(0f, slotConfig.MaximumEnergy);
        powerUpsState.SecondaryCooldownRemaining = 0f;
        powerUpsState.SecondaryCharge = 0f;
        powerUpsState.SecondaryIsCharging = 0;
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
