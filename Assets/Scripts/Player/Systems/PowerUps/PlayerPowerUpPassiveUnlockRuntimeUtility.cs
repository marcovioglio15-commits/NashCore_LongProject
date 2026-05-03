using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Shares runtime acquisition helpers for passive power-ups granted by milestone selections and combo rank rewards.
/// none.
/// returns none.
/// </summary>
internal static class PlayerPowerUpPassiveUnlockRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Finds one passive unlock catalog entry by PowerUpId.
    /// /params passivePowerUpId Passive PowerUpId requested by the caller.
    /// /params unlockCatalog Runtime unlock catalog scanned for a passive entry.
    /// /params catalogIndex Resolved catalog index when a matching passive entry is found.
    /// /returns True when the catalog contains the requested passive PowerUpId.
    /// </summary>
    public static bool TryFindPassiveCatalogIndex(FixedString64Bytes passivePowerUpId,
                                                  DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                  out int catalogIndex)
    {
        catalogIndex = -1;

        if (passivePowerUpId.Length <= 0 || !unlockCatalog.IsCreated)
        {
            return false;
        }

        for (int candidateIndex = 0; candidateIndex < unlockCatalog.Length; candidateIndex++)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[candidateIndex];

            if (catalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Passive)
            {
                continue;
            }

            if (catalogEntry.PowerUpId != passivePowerUpId)
            {
                continue;
            }

            catalogIndex = candidateIndex;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Acquires one passive catalog entry and equips its passive tool on first acquisition when possible.
    /// /params catalogIndex Runtime unlock catalog index to acquire.
    /// /params unlockCatalog Mutable runtime unlock catalog updated with unlock ownership.
    /// /params equippedPassiveTools Mutable equipped-passive tool buffer.
    /// /params passiveToolsState Mutable aggregated passive state rebuilt when a tool is equipped.
    /// /params applyTarget Debug label describing the passive apply result.
    /// /returns True when the catalog entry ownership changed; otherwise false.
    /// </summary>
    public static bool TryAcquirePassiveCatalogEntry(int catalogIndex,
                                                     DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                     ref PlayerPassiveToolsState passiveToolsState,
                                                     out string applyTarget)
    {
        return TryAcquirePassiveCatalogEntry(catalogIndex,
                                             unlockCatalog,
                                             equippedPassiveTools,
                                             ref passiveToolsState,
                                             out applyTarget,
                                             out bool _);
    }

    /// <summary>
    /// Acquires one passive catalog entry and reports whether this acquisition equipped the passive tool.
    /// /params catalogIndex Runtime unlock catalog index to acquire.
    /// /params unlockCatalog Mutable runtime unlock catalog updated with unlock ownership.
    /// /params equippedPassiveTools Mutable equipped-passive tool buffer.
    /// /params passiveToolsState Mutable aggregated passive state rebuilt when a tool is equipped.
    /// /params applyTarget Debug label describing the passive apply result.
    /// /params equippedOnGrant True when this acquisition added the passive tool to the equipped buffer.
    /// /returns True when the catalog entry ownership changed; otherwise false.
    /// </summary>
    public static bool TryAcquirePassiveCatalogEntry(int catalogIndex,
                                                     DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                     ref PlayerPassiveToolsState passiveToolsState,
                                                     out string applyTarget,
                                                     out bool equippedOnGrant)
    {
        applyTarget = "InvalidCatalogIndex";
        equippedOnGrant = false;

        if (!unlockCatalog.IsCreated || catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
        {
            return false;
        }

        PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

        if (catalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Passive)
        {
            applyTarget = "NotPassive";
            return false;
        }

        int maximumUnlockCount = math.max(1, catalogEntry.MaximumUnlockCount);

        if (catalogEntry.CurrentUnlockCount >= maximumUnlockCount)
        {
            applyTarget = "AcquisitionCapReached";
            return false;
        }

        if (catalogEntry.CurrentUnlockCount <= 0)
        {
            equippedOnGrant = TryEquipPassiveTool(in catalogEntry,
                                                  equippedPassiveTools,
                                                  ref passiveToolsState,
                                                  out applyTarget);
        }
        else
        {
            applyTarget = "PassiveStacked";
        }

        catalogEntry.CurrentUnlockCount = math.min(maximumUnlockCount, catalogEntry.CurrentUnlockCount + 1);
        catalogEntry.IsUnlocked = 1;
        catalogEntry.PendingInitialCharacterTuningApply = 0;
        unlockCatalog[catalogIndex] = catalogEntry;
        return true;
    }

    /// <summary>
    /// Releases one passive catalog stack previously granted by combo rank-up and removes the equipped tool only when that grant equipped it.
    /// /params grant Combo passive grant entry being revoked.
    /// /params unlockCatalog Mutable runtime unlock catalog updated with reduced ownership.
    /// /params equippedPassiveTools Mutable equipped-passive tool buffer.
    /// /params passiveToolsState Mutable aggregated passive state rebuilt when a tool is removed.
    /// /returns True when catalog ownership or equipped passive state changed.
    /// </summary>
    public static bool TryReleaseComboPassiveGrant(in PlayerComboPassivePowerUpGrantElement grant,
                                                   DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                   DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                   ref PlayerPassiveToolsState passiveToolsState)
    {
        if (!TryResolveGrantCatalogIndex(in grant, unlockCatalog, out int catalogIndex))
        {
            return false;
        }

        PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[catalogIndex];

        if (catalogEntry.UnlockKind != PlayerPowerUpUnlockKind.Passive || catalogEntry.CurrentUnlockCount <= 0)
        {
            return false;
        }

        catalogEntry.CurrentUnlockCount = math.max(0, catalogEntry.CurrentUnlockCount - 1);

        if (catalogEntry.CurrentUnlockCount <= 0)
        {
            catalogEntry.IsUnlocked = 0;
            catalogEntry.PendingInitialCharacterTuningApply = 0;
        }

        unlockCatalog[catalogIndex] = catalogEntry;

        if (catalogEntry.CurrentUnlockCount > 0 || grant.EquippedOnGrant == 0)
        {
            return true;
        }

        if (!TryRemoveEquippedPassiveTool(grant.PowerUpId, equippedPassiveTools))
        {
            return true;
        }

        passiveToolsState = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(equippedPassiveTools);
        return true;
    }

    /// <summary>
    /// Equips one passive tool into the passive buffer and rebuilds aggregated passive runtime state.
    /// /params selectedCatalogEntry Passive unlock catalog entry containing the passive tool payload.
    /// /params equippedPassiveTools Runtime equipped-passive tool buffer.
    /// /params passiveToolsState Aggregated passive runtime state updated when a tool is added.
    /// /params applyTarget Debug label describing the passive-apply result.
    /// /returns True when a passive tool was added; otherwise false.
    /// </summary>
    public static bool TryEquipPassiveTool(in PlayerPowerUpUnlockCatalogElement selectedCatalogEntry,
                                           DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                           ref PlayerPassiveToolsState passiveToolsState,
                                           out string applyTarget)
    {
        PlayerPassiveToolConfig passiveToolConfig = selectedCatalogEntry.PassiveToolConfig;
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
            PowerUpId = selectedCatalogEntry.PowerUpId,
            Tool = passiveToolConfig
        });
        passiveToolsState = PlayerPassiveToolsAggregationUtility.BuildPassiveToolsState(equippedPassiveTools);
        applyTarget = "PassiveAdded";
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Checks whether one passive tool kind is already present in the equipped buffer.
    /// /params equippedPassiveTools Runtime equipped-passive tool buffer.
    /// /params toolKind Passive tool kind to test.
    /// /returns True when at least one matching passive tool kind exists.
    /// </summary>
    private static bool ContainsPassiveToolKind(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                                PassiveToolKind toolKind)
    {
        if (!equippedPassiveTools.IsCreated)
        {
            return false;
        }

        for (int passiveIndex = 0; passiveIndex < equippedPassiveTools.Length; passiveIndex++)
        {
            PlayerPassiveToolConfig candidate = equippedPassiveTools[passiveIndex].Tool;

            if (candidate.IsDefined == 0)
            {
                continue;
            }

            if (candidate.ToolKind != toolKind)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the catalog entry targeted by one combo passive grant using its cached index first, then PowerUpId fallback.
    /// /params grant Combo passive grant entry being revoked.
    /// /params unlockCatalog Runtime unlock catalog scanned for the grant target.
    /// /params catalogIndex Resolved catalog index.
    /// /returns True when a matching passive catalog entry exists.
    /// </summary>
    private static bool TryResolveGrantCatalogIndex(in PlayerComboPassivePowerUpGrantElement grant,
                                                    DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                    out int catalogIndex)
    {
        catalogIndex = -1;

        if (!unlockCatalog.IsCreated)
        {
            return false;
        }

        if (grant.CatalogIndex >= 0 && grant.CatalogIndex < unlockCatalog.Length)
        {
            PlayerPowerUpUnlockCatalogElement catalogEntry = unlockCatalog[grant.CatalogIndex];

            if (catalogEntry.UnlockKind == PlayerPowerUpUnlockKind.Passive && catalogEntry.PowerUpId == grant.PowerUpId)
            {
                catalogIndex = grant.CatalogIndex;
                return true;
            }
        }

        return TryFindPassiveCatalogIndex(grant.PowerUpId, unlockCatalog, out catalogIndex);
    }

    /// <summary>
    /// Removes one equipped passive tool by PowerUpId.
    /// /params passivePowerUpId Passive PowerUpId to remove.
    /// /params equippedPassiveTools Mutable equipped-passive tool buffer.
    /// /returns True when one equipped passive entry was removed.
    /// </summary>
    private static bool TryRemoveEquippedPassiveTool(FixedString64Bytes passivePowerUpId,
                                                     DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        if (passivePowerUpId.Length <= 0 || !equippedPassiveTools.IsCreated)
        {
            return false;
        }

        for (int passiveIndex = equippedPassiveTools.Length - 1; passiveIndex >= 0; passiveIndex--)
        {
            if (equippedPassiveTools[passiveIndex].PowerUpId != passivePowerUpId)
            {
                continue;
            }

            equippedPassiveTools.RemoveAt(passiveIndex);
            return true;
        }

        return false;
    }
    #endregion

    #endregion
}
