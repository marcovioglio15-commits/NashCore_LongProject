using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores the final combo HUD theme resolved from preset-authored rank visuals, hidden legacy fallbacks, and HUD defaults.
/// none.
/// returns none.
/// </summary>
internal readonly struct HUDComboCounterResolvedVisualTheme
{
    #region Fields
    public readonly Sprite BadgeSprite;
    public readonly Color BadgeTint;
    public readonly Color RankTextColor;
    public readonly Color ComboValueTextColor;
    public readonly Color ProgressFillColor;
    public readonly Color ProgressBackgroundColor;
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Stores one fully resolved combo HUD visual theme.
    /// /params badgeSprite Final badge sprite to show for the active rank.
    /// /params badgeTint Final badge tint to apply for the active rank.
    /// /params rankTextColor Final rank-label color to apply for the active rank.
    /// /params comboValueTextColor Final combo-value color to apply for the active rank.
    /// /params progressFillColor Final progress-fill color to apply for the active rank.
    /// /params progressBackgroundColor Final progress-background color to apply for the active rank.
    /// /returns void.
    /// </summary>
    public HUDComboCounterResolvedVisualTheme(Sprite badgeSprite,
                                              Color badgeTint,
                                              Color rankTextColor,
                                              Color comboValueTextColor,
                                              Color progressFillColor,
                                              Color progressBackgroundColor)
    {
        BadgeSprite = badgeSprite;
        BadgeTint = badgeTint;
        RankTextColor = rankTextColor;
        ComboValueTextColor = comboValueTextColor;
        ProgressFillColor = progressFillColor;
        ProgressBackgroundColor = progressBackgroundColor;
    }
    #endregion

    #endregion
}

/// <summary>
/// Identifies which combo HUD color channel should be resolved from rank-owned visuals.
/// none.
/// returns none.
/// </summary>
internal enum HUDComboCounterVisualColorChannel : byte
{
    BadgeTint = 0,
    RankText = 1,
    ComboValueText = 2,
    ProgressFill = 3,
    ProgressBackground = 4
}

/// <summary>
/// Resolves combo HUD themes from preset-authored rank visuals and optional hidden legacy scene fallbacks.
/// none.
/// returns none.
/// </summary>
internal static class HUDComboCounterVisualThemeRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the preset-owned rank visuals matching the current runtime rank index.
    /// /params progressionPreset Progression preset currently used by the player scene authoring.
    /// /params currentRankIndex Active combo-rank index reported by ECS state.
    /// /returns Matching preset-owned rank visuals, or null when none are available.
    /// </summary>
    public static PlayerComboRankVisualDefinition ResolvePresetRankVisual(PlayerProgressionPreset progressionPreset, int currentRankIndex)
    {
        if (progressionPreset == null || currentRankIndex < 0)
        {
            return null;
        }

        PlayerComboCounterDefinition comboCounterDefinition = progressionPreset.ComboCounter;

        if (comboCounterDefinition == null)
        {
            return null;
        }

        IReadOnlyList<PlayerComboRankDefinition> rankDefinitions = comboCounterDefinition.RankDefinitions;

        if (rankDefinitions == null || currentRankIndex >= rankDefinitions.Count)
        {
            return null;
        }

        PlayerComboRankDefinition rankDefinition = rankDefinitions[currentRankIndex];

        if (rankDefinition == null)
        {
            return null;
        }

        return rankDefinition.RankVisuals;
    }

    /// <summary>
    /// Resolves the active combo-rank visual theme directly from the baked player ECS buffers.
    /// /params entityManager Runtime entity manager used to inspect combo-rank visual buffers.
    /// /params playerEntity Current player entity that owns the visual buffer.
    /// /params currentRankIndex Active combo-rank index reported by ECS presentation state.
    /// /params defaultBadgeSprite Default HUD badge sprite used when the baked rank has no sprite.
    /// /params defaultBadgeTint Default HUD badge tint used when the baked rank buffer is unavailable.
    /// /params defaultRankTextColor Default HUD rank-label color used when the baked rank buffer is unavailable.
    /// /params defaultComboValueTextColor Default HUD combo-value color used when the baked rank buffer is unavailable.
    /// /params defaultProgressFillColor Default HUD progress-fill color used when the baked rank buffer is unavailable.
    /// /params defaultProgressBackgroundColor Default HUD progress-background color used when the baked rank buffer is unavailable.
    /// /params resolvedTheme Resolved theme returned when the baked visual buffer contains the requested rank.
    /// /returns True when the baked ECS visual buffer supplied the requested theme; otherwise false.
    /// </summary>
    public static bool TryResolveRuntimeTheme(EntityManager entityManager,
                                              Entity playerEntity,
                                              int currentRankIndex,
                                              Sprite defaultBadgeSprite,
                                              Color defaultBadgeTint,
                                              Color defaultRankTextColor,
                                              Color defaultComboValueTextColor,
                                              Color defaultProgressFillColor,
                                              Color defaultProgressBackgroundColor,
                                              out HUDComboCounterResolvedVisualTheme resolvedTheme)
    {
        resolvedTheme = new HUDComboCounterResolvedVisualTheme(defaultBadgeSprite,
                                                               defaultBadgeTint,
                                                               defaultRankTextColor,
                                                               defaultComboValueTextColor,
                                                               defaultProgressFillColor,
                                                               defaultProgressBackgroundColor);

        if (currentRankIndex < 0)
        {
            return false;
        }

        if (!entityManager.Exists(playerEntity) || !entityManager.HasBuffer<PlayerComboRankVisualElement>(playerEntity))
        {
            return false;
        }

        DynamicBuffer<PlayerComboRankVisualElement> rankVisuals = entityManager.GetBuffer<PlayerComboRankVisualElement>(playerEntity);

        if (currentRankIndex >= rankVisuals.Length)
        {
            return false;
        }

        PlayerComboRankVisualElement rankVisual = rankVisuals[currentRankIndex];
        Sprite badgeSprite = rankVisual.BadgeSprite.Value != null ? rankVisual.BadgeSprite.Value : defaultBadgeSprite;
        resolvedTheme = new HUDComboCounterResolvedVisualTheme(badgeSprite,
                                                               ToColor(rankVisual.BadgeTint),
                                                               ToColor(rankVisual.RankTextColor),
                                                               ToColor(rankVisual.ComboValueTextColor),
                                                               ToColor(rankVisual.ProgressFillColor),
                                                               ToColor(rankVisual.ProgressBackgroundColor));
        return true;
    }

    /// <summary>
    /// Resolves one hidden legacy scene visual entry matching the current runtime rank identifier.
    /// /params rankVisualDefinitions Hidden legacy visual list stored on HUDManager scenes.
    /// /params currentRankId Active combo-rank identifier reported by ECS state.
    /// /returns Matching hidden legacy visuals, or null when no fallback entry matches.
    /// </summary>
    public static HUDComboCounterRankVisualDefinition ResolveLegacyRankVisual(IReadOnlyList<HUDComboCounterRankVisualDefinition> rankVisualDefinitions,
                                                                              FixedString64Bytes currentRankId)
    {
        if (currentRankId.Length <= 0 || rankVisualDefinitions == null || rankVisualDefinitions.Count <= 0)
        {
            return null;
        }

        string currentRankIdText = currentRankId.ToString();

        for (int visualIndex = 0; visualIndex < rankVisualDefinitions.Count; visualIndex++)
        {
            HUDComboCounterRankVisualDefinition rankVisual = rankVisualDefinitions[visualIndex];

            if (rankVisual == null || string.IsNullOrWhiteSpace(rankVisual.RankId))
            {
                continue;
            }

            if (!string.Equals(rankVisual.RankId, currentRankIdText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return rankVisual;
        }

        return null;
    }

    /// <summary>
    /// Resolves the full combo HUD visual theme using preset-owned visuals first, hidden legacy scene data second, and HUD defaults last.
    /// /params rankVisual Preset-owned visuals resolved from the active combo-rank index.
    /// /params legacyRankVisual Hidden legacy scene visuals resolved from the active combo-rank identifier.
    /// /params defaultBadgeSprite Default HUD badge sprite used when no override exists.
    /// /params defaultBadgeTint Default HUD badge tint used when no override exists.
    /// /params defaultRankTextColor Default HUD rank-label color used when no override exists.
    /// /params defaultComboValueTextColor Default HUD combo-value color used when no override exists.
    /// /params defaultProgressFillColor Default HUD progress-fill color used when no override exists.
    /// /params defaultProgressBackgroundColor Default HUD progress-background color used when no override exists.
    /// /returns Fully resolved visual theme for the active combo state.
    /// </summary>
    public static HUDComboCounterResolvedVisualTheme ResolveTheme(PlayerComboRankVisualDefinition rankVisual,
                                                                 HUDComboCounterRankVisualDefinition legacyRankVisual,
                                                                 Sprite defaultBadgeSprite,
                                                                 Color defaultBadgeTint,
                                                                 Color defaultRankTextColor,
                                                                 Color defaultComboValueTextColor,
                                                                 Color defaultProgressFillColor,
                                                                 Color defaultProgressBackgroundColor)
    {
        Sprite resolvedBadgeSprite = ResolveBadgeSprite(rankVisual, legacyRankVisual, defaultBadgeSprite);
        Color resolvedBadgeTint = ResolveColor(rankVisual,
                                               legacyRankVisual,
                                               defaultBadgeTint,
                                               HUDComboCounterVisualColorChannel.BadgeTint);
        Color resolvedRankTextColor = ResolveColor(rankVisual,
                                                   legacyRankVisual,
                                                   defaultRankTextColor,
                                                   HUDComboCounterVisualColorChannel.RankText);
        Color resolvedComboValueTextColor = ResolveColor(rankVisual,
                                                         legacyRankVisual,
                                                         defaultComboValueTextColor,
                                                         HUDComboCounterVisualColorChannel.ComboValueText);
        Color resolvedProgressFillColor = ResolveColor(rankVisual,
                                                       legacyRankVisual,
                                                       defaultProgressFillColor,
                                                       HUDComboCounterVisualColorChannel.ProgressFill);
        Color resolvedProgressBackgroundColor = ResolveColor(rankVisual,
                                                             legacyRankVisual,
                                                             defaultProgressBackgroundColor,
                                                             HUDComboCounterVisualColorChannel.ProgressBackground);

        return new HUDComboCounterResolvedVisualTheme(resolvedBadgeSprite,
                                                      resolvedBadgeTint,
                                                      resolvedRankTextColor,
                                                      resolvedComboValueTextColor,
                                                      resolvedProgressFillColor,
                                                      resolvedProgressBackgroundColor);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the badge sprite from preset-owned visuals, hidden legacy data, and HUD defaults.
    /// /params rankVisual Preset-owned visuals resolved from the active combo-rank index.
    /// /params legacyRankVisual Hidden legacy scene visuals resolved from the active combo-rank identifier.
    /// /params defaultBadgeSprite Default HUD badge sprite used when no override exists.
    /// /returns Final badge sprite to show for the active combo state.
    /// </summary>
    private static Sprite ResolveBadgeSprite(PlayerComboRankVisualDefinition rankVisual,
                                             HUDComboCounterRankVisualDefinition legacyRankVisual,
                                             Sprite defaultBadgeSprite)
    {
        if (rankVisual != null && rankVisual.BadgeSprite != null)
        {
            return rankVisual.BadgeSprite;
        }

        if (legacyRankVisual != null && legacyRankVisual.BadgeSprite != null)
        {
            return legacyRankVisual.BadgeSprite;
        }

        return defaultBadgeSprite;
    }

    /// <summary>
    /// Resolves one combo HUD color channel from preset-owned visuals, hidden legacy data, and HUD defaults.
    /// /params rankVisual Preset-owned visuals resolved from the active combo-rank index.
    /// /params legacyRankVisual Hidden legacy scene visuals resolved from the active combo-rank identifier.
    /// /params defaultColor Default HUD color used when no override exists.
    /// /params colorChannel Requested combo HUD color channel.
    /// /returns Final color to apply for the requested channel.
    /// </summary>
    private static Color ResolveColor(PlayerComboRankVisualDefinition rankVisual,
                                      HUDComboCounterRankVisualDefinition legacyRankVisual,
                                      Color defaultColor,
                                      HUDComboCounterVisualColorChannel colorChannel)
    {
        if (rankVisual != null)
        {
            return ResolvePresetColor(rankVisual, colorChannel);
        }

        if (legacyRankVisual != null)
        {
            return ResolveLegacyColor(legacyRankVisual, colorChannel);
        }

        return defaultColor;
    }

    /// <summary>
    /// Resolves one combo HUD color channel directly from preset-owned rank visuals.
    /// /params rankVisual Preset-owned visuals resolved from the active combo-rank index.
    /// /params colorChannel Requested combo HUD color channel.
    /// /returns Color stored on the preset-owned visuals.
    /// </summary>
    private static Color ResolvePresetColor(PlayerComboRankVisualDefinition rankVisual, HUDComboCounterVisualColorChannel colorChannel)
    {
        switch (colorChannel)
        {
            case HUDComboCounterVisualColorChannel.BadgeTint:
                return rankVisual.BadgeTint;

            case HUDComboCounterVisualColorChannel.RankText:
                return rankVisual.RankTextColor;

            case HUDComboCounterVisualColorChannel.ComboValueText:
                return rankVisual.ComboValueTextColor;

            case HUDComboCounterVisualColorChannel.ProgressFill:
                return rankVisual.ProgressFillColor;

            case HUDComboCounterVisualColorChannel.ProgressBackground:
                return rankVisual.ProgressBackgroundColor;

            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Resolves one combo HUD color channel directly from hidden legacy scene visuals.
    /// /params legacyRankVisual Hidden legacy scene visuals resolved from the active combo-rank identifier.
    /// /params colorChannel Requested combo HUD color channel.
    /// /returns Color stored on the hidden legacy visuals.
    /// </summary>
    private static Color ResolveLegacyColor(HUDComboCounterRankVisualDefinition legacyRankVisual,
                                            HUDComboCounterVisualColorChannel colorChannel)
    {
        switch (colorChannel)
        {
            case HUDComboCounterVisualColorChannel.BadgeTint:
                return legacyRankVisual.BadgeTint;

            case HUDComboCounterVisualColorChannel.RankText:
                return legacyRankVisual.RankTextColor;

            case HUDComboCounterVisualColorChannel.ComboValueText:
                return legacyRankVisual.ComboValueTextColor;

            case HUDComboCounterVisualColorChannel.ProgressFill:
                return legacyRankVisual.ProgressFillColor;

            case HUDComboCounterVisualColorChannel.ProgressBackground:
                return legacyRankVisual.ProgressBackgroundColor;

            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Converts one float4 color stored inside ECS buffers back to UnityEngine.Color.
    /// /params colorValue Float4 color stored in the runtime buffer.
    /// /returns Unity color rebuilt from the float4 channels.
    /// </summary>
    private static Color ToColor(float4 colorValue)
    {
        return new Color(colorValue.x, colorValue.y, colorValue.z, colorValue.w);
    }
    #endregion

    #endregion
}
