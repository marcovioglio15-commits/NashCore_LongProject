using System;
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

    [Tooltip("Human-readable progression preset name for designers.")]
    [FormerlySerializedAs("m_PresetName")]
    [SerializeField] private string presetName = "New Progression Preset";

    [Tooltip("Short description of the progression preset use case.")]
    [FormerlySerializedAs("m_Description")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this progression preset.")]
    [FormerlySerializedAs("m_Version")]
    [SerializeField] private string version = "1.0.0";

    [Header("Base Stats")]
    [Tooltip("Baseline player stats applied at runtime before level-up modifiers.")]
    [SerializeField] private PlayerProgressionBaseStats baseStats = new PlayerProgressionBaseStats();
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

    public PlayerProgressionBaseStats BaseStats
    {
        get
        {
            return baseStats;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (baseStats == null)
            baseStats = new PlayerProgressionBaseStats();

        baseStats.Validate();
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PlayerProgressionBaseStats
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Maximum health assigned to the player when this progression preset is initialized.")]
    [SerializeField] private float health = 100f;

    [Tooltip("Starting experience value assigned to the player when this progression preset is initialized.")]
    [SerializeField] private float experience;
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

    public float Experience
    {
        get
        {
            return experience;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (health < 1f)
            health = 1f;

        if (experience < 0f)
            experience = 0f;
    }
    #endregion

    #endregion
}
