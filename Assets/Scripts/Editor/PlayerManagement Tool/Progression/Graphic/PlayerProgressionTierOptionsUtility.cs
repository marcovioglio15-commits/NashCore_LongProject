using System;
using System.Collections.Generic;

/// <summary>
/// Provides editor lookup helpers for progression milestone tier selectors.
/// </summary>
public static class PlayerProgressionTierOptionsUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Collects distinct tier IDs from the power-ups preset currently relevant to the edited progression context.
    /// </summary>
    /// <returns>Sorted list of available tier IDs.</returns>
    public static List<string> BuildTierIdOptionsFromPowerUpsLibrary()
    {
        List<string> tierIds = new List<string>();

        if (TryCollectTierIdsFromScopedPreset(tierIds))
            return tierIds;

        PlayerPowerUpsPresetLibrary library = PlayerPowerUpsPresetLibraryUtility.GetOrCreateLibrary();

        if (library == null || library.Presets == null)
            return tierIds;

        HashSet<string> visitedTierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int presetIndex = 0; presetIndex < library.Presets.Count; presetIndex++)
        {
            CollectTierIds(library.Presets[presetIndex], tierIds, visitedTierIds);
        }

        tierIds.Sort(StringComparer.OrdinalIgnoreCase);
        return tierIds;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Tries to resolve tier IDs from the power-ups preset linked to the current editor selection context.
    /// </summary>
    /// <param name="tierIds">Destination list filled with distinct tier IDs when a scoped preset is available.</param>
    /// <returns>True when a scoped preset was found and processed; otherwise false.</returns>
    private static bool TryCollectTierIdsFromScopedPreset(List<string> tierIds)
    {
        PlayerPowerUpsPreset scopedPreset = null;
        PlayerMasterPreset activeMasterPreset = PlayerManagementSelectionContext.ActiveMasterPreset;

        if (activeMasterPreset != null)
            scopedPreset = activeMasterPreset.PowerUpsPreset;

        if (scopedPreset == null)
            scopedPreset = PlayerManagementSelectionContext.ActivePowerUpsPreset;

        if (scopedPreset == null)
            return false;

        HashSet<string> visitedTierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectTierIds(scopedPreset, tierIds, visitedTierIds);
        tierIds.Sort(StringComparer.OrdinalIgnoreCase);
        return true;
    }

    /// <summary>
    /// Appends distinct tier IDs from one power-ups preset into the provided destination list.
    /// </summary>
    /// <param name="preset">Power-ups preset that owns the tier definitions.</param>
    /// <param name="tierIds">Destination list receiving normalized tier IDs.</param>
    /// <param name="visitedTierIds">Set used to prevent duplicate additions across scanned presets.</param>
    /// <returns>Void.</returns>
    private static void CollectTierIds(PlayerPowerUpsPreset preset, List<string> tierIds, HashSet<string> visitedTierIds)
    {
        if (preset == null || preset.TierLevels == null || tierIds == null || visitedTierIds == null)
            return;

        for (int tierIndex = 0; tierIndex < preset.TierLevels.Count; tierIndex++)
        {
            PowerUpTierLevelDefinition tierLevel = preset.TierLevels[tierIndex];

            if (tierLevel == null || string.IsNullOrWhiteSpace(tierLevel.TierId))
                continue;

            string tierId = tierLevel.TierId.Trim();

            if (!visitedTierIds.Add(tierId))
                continue;

            tierIds.Add(tierId);
        }
    }
    #endregion

    #endregion
}
