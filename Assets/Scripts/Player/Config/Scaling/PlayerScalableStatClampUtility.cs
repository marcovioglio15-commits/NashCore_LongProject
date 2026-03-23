using System;
using UnityEngine;

/// <summary>
/// Centralizes scalable-stat clamp and integer normalization rules shared by bake and runtime systems.
/// </summary>
public static class PlayerScalableStatClampUtility
{
    #region Constants
    public const float DefaultMinimumValue = -1000000f;
    public const float DefaultMaximumValue = 1000000f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one normalized scalable-stat value using the provided raw clamp range and stat type.
    /// /params statType: Runtime numeric type of the scalable stat.
    /// /params minimumValue: Raw minimum clamp value configured for the stat.
    /// /params maximumValue: Raw maximum clamp value configured for the stat.
    /// /params rawValue: Unnormalized value to sanitize for runtime usage.
    /// /returns Clamped value normalized for the scalable-stat type.
    /// </summary>
    public static float ResolveNormalizedValue(PlayerScalableStatType statType,
                                               float minimumValue,
                                               float maximumValue,
                                               float rawValue)
    {
        ResolveOrderedRange(minimumValue, maximumValue, out float resolvedMinimumValue, out float resolvedMaximumValue);
        float clampedValue = Mathf.Clamp(rawValue, resolvedMinimumValue, resolvedMaximumValue);

        if (statType == PlayerScalableStatType.Integer)
            clampedValue = (float)Math.Round(clampedValue, MidpointRounding.AwayFromZero);

        return clampedValue;
    }

    /// <summary>
    /// Resolves one normalized scalable-stat value using a baked blob entry as clamp metadata source.
    /// /params scalableStat: Baked scalable-stat metadata.
    /// /params rawValue: Unnormalized value to sanitize for runtime usage.
    /// /returns Clamped value normalized for the scalable-stat type.
    /// </summary>
    public static float ResolveNormalizedValue(ref PlayerScalableStatBlob scalableStat, float rawValue)
    {
        return ResolveNormalizedValue((PlayerScalableStatType)scalableStat.Type,
                                      scalableStat.MinimumValue,
                                      scalableStat.MaximumValue,
                                      rawValue);
    }

    /// <summary>
    /// Resolves one normalized scalable-stat value using a runtime buffer element as clamp metadata source.
    /// /params scalableStat: Runtime scalable-stat entry.
    /// /params rawValue: Unnormalized value to sanitize for runtime usage.
    /// /returns Clamped value normalized for the scalable-stat type.
    /// </summary>
    public static float ResolveNormalizedValue(in PlayerScalableStatElement scalableStat, float rawValue)
    {
        return ResolveNormalizedValue((PlayerScalableStatType)scalableStat.Type,
                                      scalableStat.MinimumValue,
                                      scalableStat.MaximumValue,
                                      rawValue);
    }

    /// <summary>
    /// Resolves one ordered clamp range from raw serialized minimum and maximum values.
    /// /params minimumValue: Raw serialized minimum value.
    /// /params maximumValue: Raw serialized maximum value.
    /// /params resolvedMinimumValue: Ordered lower bound used by runtime normalization.
    /// /params resolvedMaximumValue: Ordered upper bound used by runtime normalization.
    /// /returns void.
    /// </summary>
    public static void ResolveOrderedRange(float minimumValue,
                                           float maximumValue,
                                           out float resolvedMinimumValue,
                                           out float resolvedMaximumValue)
    {
        resolvedMinimumValue = Mathf.Min(minimumValue, maximumValue);
        resolvedMaximumValue = Mathf.Max(minimumValue, maximumValue);
    }
    #endregion

    #endregion
}
