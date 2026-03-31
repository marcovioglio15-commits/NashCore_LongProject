using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caches power-up presentation metadata resolved from the active power-ups preset for managed HUD and prompt rendering.
/// </summary>
public static class PlayerPowerUpPresentationRuntime
{
    #region Fields
    private static readonly Dictionary<string, PowerUpPresentationEntry> entriesByPowerUpId = new Dictionary<string, PowerUpPresentationEntry>(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Rebuilds the runtime presentation cache from the resolved power-ups preset used by the current player.
    /// preset: Power-ups preset whose icons and display names will drive HUD and world-space prompts.
    /// returns void.
    /// </summary>
    public static void Initialize(PlayerPowerUpsPreset preset)
    {
        entriesByPowerUpId.Clear();

        if (preset == null)
            return;

        RegisterModularEntries(preset.ActivePowerUps);
        RegisterModularEntries(preset.PassivePowerUps);
        RegisterLegacyEntries(preset.ActiveTools);
        RegisterLegacyEntries(preset.PassiveTools);
    }

    /// <summary>
    /// Clears the currently cached runtime presentation data.
    /// none.
    /// returns void.
    /// </summary>
    public static void Shutdown()
    {
        entriesByPowerUpId.Clear();
    }
    #endregion

    #region Lookup
    /// <summary>
    /// Resolves one cached power-up display name with a caller-provided fallback when the cache has no matching entry.
    /// powerUpId: Stable power-up identifier requested by HUD or world-space prompts.
    /// fallbackDisplayName: Fallback label used when no cached entry exists.
    /// returns Resolved display name.
    /// </summary>
    public static string ResolveDisplayName(string powerUpId, string fallbackDisplayName)
    {
        if (TryResolveEntry(powerUpId, out PowerUpPresentationEntry entry))
        {
            if (!string.IsNullOrWhiteSpace(entry.DisplayName))
                return entry.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(fallbackDisplayName))
            return fallbackDisplayName;

        return string.IsNullOrWhiteSpace(powerUpId) ? string.Empty : powerUpId.Trim();
    }

    /// <summary>
    /// Resolves one cached sprite icon by power-up identifier.
    /// powerUpId: Stable power-up identifier requested by HUD or world-space prompts.
    /// icon: Resolved sprite icon when present.
    /// returns True when a non-null icon is available; otherwise false.
    /// </summary>
    public static bool TryResolveIcon(string powerUpId, out Sprite icon)
    {
        icon = null;

        if (!TryResolveEntry(powerUpId, out PowerUpPresentationEntry entry))
            return false;

        if (entry.Icon == null)
            return false;

        icon = entry.Icon;
        return true;
    }

    /// <summary>
    /// Resolves one cached presentation entry by power-up identifier.
    /// powerUpId: Stable power-up identifier requested by HUD or world-space prompts.
    /// entry: Resolved cached presentation entry when present.
    /// returns True when the entry exists; otherwise false.
    /// </summary>
    public static bool TryResolveEntry(string powerUpId, out PowerUpPresentationEntry entry)
    {
        entry = default;

        if (string.IsNullOrWhiteSpace(powerUpId))
            return false;

        return entriesByPowerUpId.TryGetValue(powerUpId.Trim(), out entry);
    }
    #endregion

    #region Registration
    /// <summary>
    /// Registers modular active or passive power-up entries in the runtime presentation cache.
    /// powerUps: Modular power-up collection taken from the resolved preset.
    /// returns void.
    /// </summary>
    private static void RegisterModularEntries(IReadOnlyList<ModularPowerUpDefinition> powerUps)
    {
        if (powerUps == null)
            return;

        for (int powerUpIndex = 0; powerUpIndex < powerUps.Count; powerUpIndex++)
        {
            ModularPowerUpDefinition powerUp = powerUps[powerUpIndex];

            if (powerUp == null)
                continue;

            RegisterCommonData(powerUp.CommonData);
        }
    }

    /// <summary>
    /// Registers legacy active-tool entries in the runtime presentation cache.
    /// activeTools: Legacy active-tool collection taken from the resolved preset.
    /// returns void.
    /// </summary>
    private static void RegisterLegacyEntries(IReadOnlyList<ActiveToolDefinition> activeTools)
    {
        if (activeTools == null)
            return;

        for (int toolIndex = 0; toolIndex < activeTools.Count; toolIndex++)
        {
            ActiveToolDefinition activeTool = activeTools[toolIndex];

            if (activeTool == null)
                continue;

            RegisterCommonData(activeTool.CommonData);
        }
    }

    /// <summary>
    /// Registers legacy passive-tool entries in the runtime presentation cache.
    /// passiveTools: Legacy passive-tool collection taken from the resolved preset.
    /// returns void.
    /// </summary>
    private static void RegisterLegacyEntries(IReadOnlyList<PassiveToolDefinition> passiveTools)
    {
        if (passiveTools == null)
            return;

        for (int toolIndex = 0; toolIndex < passiveTools.Count; toolIndex++)
        {
            PassiveToolDefinition passiveTool = passiveTools[toolIndex];

            if (passiveTool == null)
                continue;

            RegisterCommonData(passiveTool.CommonData);
        }
    }

    /// <summary>
    /// Registers one shared power-up metadata entry in the runtime presentation cache.
    /// commonData: Shared power-up metadata resolved from the preset.
    /// returns void.
    /// </summary>
    private static void RegisterCommonData(PowerUpCommonData commonData)
    {
        if (commonData == null || string.IsNullOrWhiteSpace(commonData.PowerUpId))
            return;

        string powerUpId = commonData.PowerUpId.Trim();

        if (entriesByPowerUpId.ContainsKey(powerUpId))
            return;

        entriesByPowerUpId.Add(powerUpId, new PowerUpPresentationEntry(powerUpId,
                                                                       commonData.DisplayName,
                                                                       commonData.Description,
                                                                       commonData.Icon));
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores one cached power-up presentation record used by HUD and world-space prompts.
    /// </summary>
    public readonly struct PowerUpPresentationEntry
    {
        #region Fields
        public readonly string PowerUpId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly Sprite Icon;
        #endregion

        #region Methods
        /// <summary>
        /// Creates one cached presentation record.
        /// powerUpIdValue: Stable power-up identifier.
        /// displayNameValue: Cached display name.
        /// descriptionValue: Cached description.
        /// iconValue: Cached sprite icon.
        /// returns A fully initialized presentation record.
        /// </summary>
        public PowerUpPresentationEntry(string powerUpIdValue,
                                        string displayNameValue,
                                        string descriptionValue,
                                        Sprite iconValue)
        {
            PowerUpId = powerUpIdValue;
            DisplayName = string.IsNullOrWhiteSpace(displayNameValue) ? powerUpIdValue : displayNameValue.Trim();
            Description = string.IsNullOrWhiteSpace(descriptionValue) ? string.Empty : descriptionValue.Trim();
            Icon = iconValue;
        }
        #endregion
    }
    #endregion
}
