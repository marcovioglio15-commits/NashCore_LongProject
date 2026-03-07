using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerProgressionPreset", menuName = "Player/Progression Preset", order = 12)]
public sealed class PlayerProgressionPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this progression preset, used for stable references.")]
    [FormerlySerializedAs("m_PresetId")]
    [SerializeField] private string presetId;

    [Tooltip("Progression preset name.")]
    [FormerlySerializedAs("m_PresetName")]
    [SerializeField] private string presetName = "New Progression Preset";

    [Tooltip("Short description of the progression preset use case.")]
    [FormerlySerializedAs("m_Description")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this progression preset.")]
    [FormerlySerializedAs("m_Version")]
    [SerializeField] private string version = "1.0.0";

    [Header("Scalable Stats")]
    [Tooltip("Scalable stats used as runtime progression variables and formula inputs.")]
    [SerializeField] private List<PlayerScalableStatDefinition> scalableStats = new List<PlayerScalableStatDefinition>();

    [Header("Scaling")]
    [Tooltip("Optional formula-based scaling rules applied to numeric progression properties during bake.")]
    [SerializeField] private List<PlayerStatScalingRule> scalingRules = new List<PlayerStatScalingRule>();

    [Tooltip("Legacy base stats storage kept for backward-compatible migration from old presets.")]
    [FormerlySerializedAs("baseStats")]
    [HideInInspector]
    [SerializeField] private LegacyPlayerProgressionBaseStats legacyBaseStats = new LegacyPlayerProgressionBaseStats();

    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public IReadOnlyList<PlayerScalableStatDefinition> ScalableStats
    {
        get
        {
            return scalableStats;
        }
    }

    public IReadOnlyList<PlayerStatScalingRule> ScalingRules
    {
        get
        {
            return scalingRules;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns legacy health data when an old preset still contains migrated base-stat health.
    /// </summary>
    /// <param name="legacyHealth">Resolved legacy health value when available.</param>
    /// <returns>True when legacy health data exists, otherwise false.</returns>
    public bool TryGetLegacyHealth(out float legacyHealth)
    {
        legacyHealth = 0f;

        if (legacyBaseStats == null)
            return false;

        legacyHealth = Mathf.Max(1f, legacyBaseStats.Health);
        return true;
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (scalableStats == null)
            scalableStats = new List<PlayerScalableStatDefinition>();

        if (scalingRules == null)
            scalingRules = new List<PlayerStatScalingRule>();

        if (legacyBaseStats == null)
            legacyBaseStats = new LegacyPlayerProgressionBaseStats();

        legacyBaseStats.Validate();
        ValidateScalableStats();
        ValidateScalingRules();
    }
    #endregion

    #region Validation
    private void ValidateScalableStats()
    {
        for (int index = 0; index < scalableStats.Count; index++)
        {
            PlayerScalableStatDefinition statDefinition = scalableStats[index];

            if (statDefinition != null)
                continue;

            statDefinition = new PlayerScalableStatDefinition();
            statDefinition.Configure(string.Format("stat{0}", index + 1), PlayerScalableStatType.Float, 0f);
            scalableStats[index] = statDefinition;
        }

        for (int index = 0; index < scalableStats.Count; index++)
        {
            PlayerScalableStatDefinition statDefinition = scalableStats[index];
            string fallbackName = string.Format("stat{0}", index + 1);
            statDefinition.Validate(fallbackName);
        }

        EnsureUniqueScalableStatNames();
    }

    private void EnsureUniqueScalableStatNames()
    {
        HashSet<string> visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < scalableStats.Count; index++)
        {
            PlayerScalableStatDefinition statDefinition = scalableStats[index];
            string baseName = statDefinition.StatName;

            if (visitedNames.Add(baseName))
                continue;

            string uniqueName = baseName;
            int suffix = 2;

            while (visitedNames.Contains(uniqueName))
            {
                uniqueName = string.Format("{0}_{1}", baseName, suffix);
                suffix += 1;
            }

            statDefinition.Configure(uniqueName, statDefinition.StatType, statDefinition.DefaultValue);
            visitedNames.Add(uniqueName);
        }
    }

    private void ValidateScalingRules()
    {
        for (int index = 0; index < scalingRules.Count; index++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];

            if (scalingRule != null)
                continue;

            scalingRule = new PlayerStatScalingRule();
            scalingRule.Configure(string.Empty, false, string.Empty);
            scalingRules[index] = scalingRule;
        }

        for (int index = scalingRules.Count - 1; index >= 0; index--)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];
            scalingRule.Validate();

            if (string.IsNullOrWhiteSpace(scalingRule.StatKey))
            {
                scalingRules.RemoveAt(index);
                continue;
            }
        }
    }

    #endregion

    #endregion
}

/// <summary>
/// Stores deprecated progression base stats for one-time migration into the scalable stat model.
/// </summary>
[Serializable]
public sealed class LegacyPlayerProgressionBaseStats
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Legacy maximum health value used before health moved to controller presets.")]
    [SerializeField] private float health = 100f;

    #endregion

    #endregion

    #region Properties
    public float Health
    {
        get
        {
            return health;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Sanitizes legacy values to avoid invalid migration data.
    /// </summary>
    /// <returns>Void.</returns>
    public void Validate()
    {
        if (health < 1f)
            health = 1f;
    }
    #endregion

    #endregion
}
