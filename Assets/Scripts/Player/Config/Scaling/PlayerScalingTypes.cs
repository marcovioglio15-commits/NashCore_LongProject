using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the available scalar data type options for designer-authored scalable stats.
/// </summary>
public enum PlayerScalableStatType : byte
{
    Float = 0,
    Integer = 1
}

/// <summary>
/// Stores one scalable stat entry editable by designers in progression presets.
/// </summary>
[Serializable]
public sealed class PlayerScalableStatDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Scalable stat identifier used in formulas with [StatName] syntax.")]
    [SerializeField] private string statName = "newStat";

    [Tooltip("Data type used by this stat at runtime. Integer values are rounded after scaling.")]
    [SerializeField] private PlayerScalableStatType statType = PlayerScalableStatType.Float;

    [Tooltip("Default raw value of this scalable stat before formula-based scaling is evaluated.")]
    [SerializeField] private float defaultValue;

    [Tooltip("Minimum runtime value allowed for this scalable stat. Runtime sorts Min/Max if they are inverted.")]
    [SerializeField] private float minimumValue = PlayerScalableStatClampUtility.DefaultMinimumValue;

    [Tooltip("Maximum runtime value allowed for this scalable stat. Runtime sorts Min/Max if they are inverted.")]
    [SerializeField] private float maximumValue = PlayerScalableStatClampUtility.DefaultMaximumValue;
    #endregion

    #endregion

    #region Properties
    public string StatName
    {
        get
        {
            return statName;
        }
    }

    public PlayerScalableStatType StatType
    {
        get
        {
            return statType;
        }
    }

    public float DefaultValue
    {
        get
        {
            return defaultValue;
        }
    }

    public float MinimumValue
    {
        get
        {
            return minimumValue;
        }
    }

    public float MaximumValue
    {
        get
        {
            return maximumValue;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Sanitizes this stat entry to keep name data valid for formulas while preserving designer-authored numeric values.
    /// </summary>
    /// <param name="fallbackName">Name to apply when the current name is empty or invalid.</param>
    /// <returns>True when at least one field was modified during validation, otherwise false.<returns>
    public bool Validate(string fallbackName)
    {
        bool changed = false;
        string sanitizedName = PlayerScalableStatNameUtility.Sanitize(statName, fallbackName);

        if (!string.Equals(statName, sanitizedName, StringComparison.Ordinal))
        {
            statName = sanitizedName;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Resolves the normalized runtime default value after clamp and integer rules are applied.
    /// </summary>
    /// <returns>Runtime-ready scalable-stat default value.<returns>
    public float ResolveRuntimeDefaultValue()
    {
        return PlayerScalableStatClampUtility.ResolveNormalizedValue(statType,
                                                                    minimumValue,
                                                                    maximumValue,
                                                                    defaultValue);
    }
    #endregion

    #region Setup
    /// <summary>
    /// Assigns all serialized fields for this scalable stat in one call.
    /// </summary>
    /// <param name="statNameValue">Formula variable name for this stat.</param>
    /// <param name="statTypeValue">Runtime data type of this stat.</param>
    /// <param name="defaultValueValue">Default raw stat value before scaling.</param>

    public void Configure(string statNameValue, PlayerScalableStatType statTypeValue, float defaultValueValue)
    {
        Configure(statNameValue,
                  statTypeValue,
                  defaultValueValue,
                  PlayerScalableStatClampUtility.DefaultMinimumValue,
                  PlayerScalableStatClampUtility.DefaultMaximumValue);
    }

    /// <summary>
    /// Assigns all serialized fields for this scalable stat in one call, including runtime clamp bounds.
    /// </summary>
    /// <param name="statNameValue">Formula variable name for this stat.</param>
    /// <param name="statTypeValue">Runtime data type of this stat.</param>
    /// <param name="defaultValueValue">Default raw stat value before scaling.</param>
    /// <param name="minimumValueValue">Minimum runtime clamp value.</param>
    /// <param name="maximumValueValue">Maximum runtime clamp value.</param>
    public void Configure(string statNameValue,
                          PlayerScalableStatType statTypeValue,
                          float defaultValueValue,
                          float minimumValueValue,
                          float maximumValueValue)
    {
        statName = statNameValue;
        statType = statTypeValue;
        defaultValue = defaultValueValue;
        minimumValue = minimumValueValue;
        maximumValue = maximumValueValue;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one optional scaling rule bound to a specific numeric stat key.
/// </summary>
[Serializable]
public sealed class PlayerStatScalingRule
{
    #region Constants
    private const float DebugColorDefaultRed = 1f;
    private const float DebugColorDefaultGreen = 0.92156863f;
    private const float DebugColorDefaultBlue = 0.015686275f;
    private const float DebugColorDefaultAlpha = 1f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Stable stat key that identifies which numeric serialized property this scaling rule targets.")]
    [SerializeField] private string statKey;

    [Tooltip("When enabled, the Formula field is evaluated and applied to the target numeric stat.")]
    [SerializeField] private bool addScaling;

    [Tooltip("Mathematical expression using [this] and/or [ScalableStatName] variables.")]
    [SerializeField] private string formula;

    [Tooltip("When enabled, runtime editor-only debug logs print scaling formulas only when tracked values change.")]
    [SerializeField] private bool debugInConsole;

    [Tooltip("Editor-only debug color used for this specific rule when Debug in Console is enabled.")]
    [SerializeField] private Color debugColor = new Color(DebugColorDefaultRed,
                                                          DebugColorDefaultGreen,
                                                          DebugColorDefaultBlue,
                                                          DebugColorDefaultAlpha);
    #endregion

    #endregion

    #region Properties
    public string StatKey
    {
        get
        {
            return statKey;
        }
    }

    public bool AddScaling
    {
        get
        {
            return addScaling;
        }
    }

    public string Formula
    {
        get
        {
            return formula;
        }
    }

    public bool DebugInConsole
    {
        get
        {
            return debugInConsole;
        }
    }

    public Color DebugColor
    {
        get
        {
            return debugColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns the default color used for scaling debug logs.
    /// </summary>
    /// <returns>Default debug color (yellow).<returns>
    public static Color GetDefaultDebugColor()
    {
        return new Color(DebugColorDefaultRed,
                         DebugColorDefaultGreen,
                         DebugColorDefaultBlue,
                         DebugColorDefaultAlpha);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Normalizes rule storage fields to avoid null strings and invalid disabled debug state.
    /// </summary>
    /// <returns>True when at least one field was modified during validation, otherwise false.<returns>
    public bool Validate()
    {
        bool changed = false;

        if (statKey == null)
        {
            statKey = string.Empty;
            changed = true;
        }

        if (formula == null)
        {
            formula = string.Empty;
            changed = true;
        }

        if (!addScaling && debugInConsole)
        {
            debugInConsole = false;
            changed = true;
        }

        Color sanitizedDebugColor = SanitizeDebugColor(debugColor);

        if (!AreColorsApproximatelyEqual(debugColor, sanitizedDebugColor))
        {
            debugColor = sanitizedDebugColor;
            changed = true;
        }

        return changed;
    }
    #endregion

    #region Setup
    /// <summary>
    /// Assigns all serialized fields for this scaling rule in one call.
    /// </summary>
    /// <param name="statKeyValue">Stable key of the target numeric stat.</param>
    /// <param name="addScalingValue">Whether formula scaling is enabled.</param>
    /// <param name="formulaValue">Formula text to evaluate when scaling is enabled.</param>

    public void Configure(string statKeyValue, bool addScalingValue, string formulaValue)
    {
        Configure(statKeyValue, addScalingValue, formulaValue, false, GetDefaultDebugColor());
    }

    /// <summary>
    /// Assigns all serialized fields for this scaling rule in one call.
    /// </summary>
    /// <param name="statKeyValue">Stable key of the target numeric stat.</param>
    /// <param name="addScalingValue">Whether formula scaling is enabled.</param>
    /// <param name="formulaValue">Formula text to evaluate when scaling is enabled.</param>
    /// <param name="debugInConsoleValue">Whether runtime editor-only logging is enabled for this rule.</param>

    public void Configure(string statKeyValue, bool addScalingValue, string formulaValue, bool debugInConsoleValue)
    {
        Configure(statKeyValue, addScalingValue, formulaValue, debugInConsoleValue, GetDefaultDebugColor());
    }

    /// <summary>
    /// Assigns all serialized fields for this scaling rule in one call.
    /// </summary>
    /// <param name="statKeyValue">Stable key of the target numeric stat.</param>
    /// <param name="addScalingValue">Whether formula scaling is enabled.</param>
    /// <param name="formulaValue">Formula text to evaluate when scaling is enabled.</param>
    /// <param name="debugInConsoleValue">Whether runtime editor-only logging is enabled for this rule.</param>
    /// <param name="debugColorValue">Editor-only color applied to this rule debug log.</param>

    public void Configure(string statKeyValue,
                          bool addScalingValue,
                          string formulaValue,
                          bool debugInConsoleValue,
                          Color debugColorValue)
    {
        statKey = statKeyValue;
        addScaling = addScalingValue;
        formula = formulaValue;
        debugInConsole = debugInConsoleValue;
        debugColor = SanitizeDebugColor(debugColorValue);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Sanitizes debug color channels and keeps legacy assets visible by forcing default yellow when alpha is zero.
    /// </summary>
    /// <param name="sourceColor">Raw serialized color.</param>
    /// <returns>Sanitized opaque debug color.<returns>
    private static Color SanitizeDebugColor(Color sourceColor)
    {
        if (sourceColor.a <= 0.0001f)
            return GetDefaultDebugColor();

        return new Color(Mathf.Clamp01(sourceColor.r),
                         Mathf.Clamp01(sourceColor.g),
                         Mathf.Clamp01(sourceColor.b),
                         DebugColorDefaultAlpha);
    }

    /// <summary>
    /// Compares colors using a tiny tolerance to avoid noisy validate writes.
    /// </summary>
    /// <param name="leftColor">First color.</param>
    /// <param name="rightColor">Second color.</param>
    /// <returns>True when channel values are approximately equal.<returns>
    private static bool AreColorsApproximatelyEqual(Color leftColor, Color rightColor)
    {
        if (Mathf.Abs(leftColor.r - rightColor.r) > 0.0001f)
            return false;

        if (Mathf.Abs(leftColor.g - rightColor.g) > 0.0001f)
            return false;

        if (Mathf.Abs(leftColor.b - rightColor.b) > 0.0001f)
            return false;

        return Mathf.Abs(leftColor.a - rightColor.a) <= 0.0001f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Provides common validation helpers for scalable stat names used in formulas and preset data.
/// </summary>
public static class PlayerScalableStatNameUtility
{
    #region Constants
    public const string ReservedThisName = "this";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Normalizes and validates one scalable stat name using project naming rules.
    /// </summary>
    /// <param name="inputName">Raw user-entered stat name.</param>
    /// <param name="fallbackName">Name used when the input is invalid or empty.</param>
    /// <returns>Sanitized stat name that is never null or whitespace.<returns>
    public static string Sanitize(string inputName, string fallbackName)
    {
        string trimmedName = string.IsNullOrWhiteSpace(inputName) ? string.Empty : inputName.Trim();
        string resolvedFallback = string.IsNullOrWhiteSpace(fallbackName) ? "newStat" : fallbackName.Trim();

        if (IsValid(trimmedName))
            return trimmedName;

        if (IsValid(resolvedFallback))
            return resolvedFallback;

        return "newStat";
    }

    /// <summary>
    /// Checks whether a scalable stat name satisfies allowed syntax and reserved-word restrictions.
    /// </summary>
    /// <param name="name">Candidate stat name to validate.</param>
    /// <returns>True when the name is valid, otherwise false.<returns>
    public static bool IsValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (string.Equals(name, ReservedThisName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        for (int index = 0; index < name.Length; index++)
        {
            char currentCharacter = name[index];

            if (char.IsLetterOrDigit(currentCharacter))
                continue;

            if (currentCharacter == '_')
                continue;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensures scalable stat names are unique using case-insensitive comparison.
    /// </summary>
    /// <param name="definitions">Collection of scalable stats to inspect.</param>
    /// <returns>True when all names are unique, otherwise false.<returns>
    public static bool AreUnique(IReadOnlyList<PlayerScalableStatDefinition> definitions)
    {
        if (definitions == null)
            return true;

        HashSet<string> visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < definitions.Count; index++)
        {
            PlayerScalableStatDefinition definition = definitions[index];

            if (definition == null)
                continue;

            if (string.IsNullOrWhiteSpace(definition.StatName))
                continue;

            if (!visitedNames.Add(definition.StatName))
                return false;
        }

        return true;
    }
    #endregion

    #endregion
}
