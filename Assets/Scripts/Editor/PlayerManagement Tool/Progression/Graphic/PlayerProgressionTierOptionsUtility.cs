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
    /// Collects distinct tier IDs from all registered power-ups presets.
    /// </summary>
    /// <returns>Sorted list of available tier IDs.</returns>
    public static List<string> BuildTierIdOptionsFromPowerUpsLibrary()
    {
        List<string> tierIds = new List<string>();
        PlayerPowerUpsPresetLibrary library = PlayerPowerUpsPresetLibraryUtility.GetOrCreateLibrary();

        if (library == null || library.Presets == null)
            return tierIds;

        HashSet<string> visitedTierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int presetIndex = 0; presetIndex < library.Presets.Count; presetIndex++)
        {
            PlayerPowerUpsPreset preset = library.Presets[presetIndex];

            if (preset == null || preset.TierLevels == null)
                continue;

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

        tierIds.Sort(StringComparer.OrdinalIgnoreCase);
        return tierIds;
    }
    #endregion

    #endregion
}
