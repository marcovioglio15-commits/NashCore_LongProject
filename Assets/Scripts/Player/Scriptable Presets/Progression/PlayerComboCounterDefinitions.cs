using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Describes how the combo reacts after a damage event configured to break it.
/// none.
/// returns none.
/// </summary>
public enum PlayerComboDamageBreakMode : byte
{
    ResetCombo = 0,
    DowngradeToPreviousRank = 1
}

/// <summary>
/// Stores authored combo-counter rules used by progression presets to grant temporary rank-based bonuses.
/// none.
/// returns none.
/// </summary>
[Serializable]
public sealed class PlayerComboCounterDefinition
{
    #region Fields

    #region Serialized Fields
    [Header("Runtime")]
    [Tooltip("Enables combo accumulation, rank evaluation, and temporary combo bonuses for this progression preset.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Amount added to the combo counter every time one enemy is killed while the combo remains unbroken.")]
    [SerializeField] private int comboGainPerKill = 1;

    [Tooltip("Controls whether valid damage breaks the combo entirely or only drops it to the previous reached rank threshold.")]
    [SerializeField] private PlayerComboDamageBreakMode damageBreakMode = PlayerComboDamageBreakMode.ResetCombo;

    [Tooltip("When enabled, shield-only damage also breaks the current combo. Health damage always breaks it.")]
    [SerializeField] private bool shieldDamageBreaksCombo;

    [Header("Ranks")]
    [Tooltip("Ordered rank milestones used to resolve the active combo rank and its temporary Character Tuning bonuses.")]
    [SerializeField] private List<PlayerComboRankDefinition> rankDefinitions = new List<PlayerComboRankDefinition>();
    #endregion

    #endregion

    #region Properties
    public bool IsEnabled
    {
        get
        {
            return isEnabled;
        }
    }

    public int ComboGainPerKill
    {
        get
        {
            return comboGainPerKill;
        }
    }

    public bool ShieldDamageBreaksCombo
    {
        get
        {
            return shieldDamageBreaksCombo;
        }
    }

    public PlayerComboDamageBreakMode DamageBreakMode
    {
        get
        {
            return damageBreakMode;
        }
    }

    public IReadOnlyList<PlayerComboRankDefinition> RankDefinitions
    {
        get
        {
            return rankDefinitions;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the authored combo runtime rules and rank list.
    /// /params isEnabledValue Enables or disables the combo system for the owning preset.
    /// /params comboGainPerKillValue Amount added for every valid enemy kill.
    /// /params damageBreakModeValue Controls whether damage resets the combo entirely or downgrades it to the previous rank.
    /// /params shieldDamageBreaksComboValue True when shield-only damage should also interrupt the combo.
    /// /params rankDefinitionsValue Ordered rank list stored by this combo definition.
    /// /returns void.
    /// </summary>
    public void Configure(bool isEnabledValue,
                          int comboGainPerKillValue,
                          PlayerComboDamageBreakMode damageBreakModeValue,
                          bool shieldDamageBreaksComboValue,
                          List<PlayerComboRankDefinition> rankDefinitionsValue)
    {
        isEnabled = isEnabledValue;
        comboGainPerKill = comboGainPerKillValue;
        damageBreakMode = damageBreakModeValue;
        shieldDamageBreaksCombo = shieldDamageBreaksComboValue;
        rankDefinitions = rankDefinitionsValue;
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures nested collections exist and normalizes nested combo ranks without snapping authored numeric values.
    /// /params None.
    /// /returns void.
    /// </summary>
    public void Validate()
    {
        if (rankDefinitions == null)
        {
            rankDefinitions = new List<PlayerComboRankDefinition>();
        }

        for (int rankIndex = 0; rankIndex < rankDefinitions.Count; rankIndex++)
        {
            PlayerComboRankDefinition rankDefinition = rankDefinitions[rankIndex];

            if (rankDefinition == null)
            {
                rankDefinition = new PlayerComboRankDefinition();
                rankDefinitions[rankIndex] = rankDefinition;
            }

            rankDefinition.Validate();
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one combo rank milestone with its display identifier and temporary Character Tuning bonus formulas.
/// none.
/// returns none.
/// </summary>
[Serializable]
public sealed class PlayerComboRankVisualDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Optional sprite shown inside the combo HUD badge while this rank is active.")]
    [SerializeField] private Sprite badgeSprite;

    [Tooltip("Tint applied to the combo HUD badge image while this rank is active.")]
    [SerializeField] private Color badgeTint = Color.white;

    [Tooltip("Text color applied to the combo HUD rank label while this rank is active.")]
    [SerializeField] private Color rankTextColor = Color.white;

    [Tooltip("Text color applied to the combo HUD numeric combo value while this rank is active.")]
    [SerializeField] private Color comboValueTextColor = Color.white;

    [Tooltip("Tint applied to the combo HUD progress fill while this rank is active.")]
    [SerializeField] private Color progressFillColor = Color.white;

    [Tooltip("Tint applied to the combo HUD progress background while this rank is active.")]
    [SerializeField] private Color progressBackgroundColor = Color.white;
    #endregion

    #endregion

    #region Properties
    public Sprite BadgeSprite
    {
        get
        {
            return badgeSprite;
        }
    }

    public Color BadgeTint
    {
        get
        {
            return badgeTint;
        }
    }

    public Color RankTextColor
    {
        get
        {
            return rankTextColor;
        }
    }

    public Color ComboValueTextColor
    {
        get
        {
            return comboValueTextColor;
        }
    }

    public Color ProgressFillColor
    {
        get
        {
            return progressFillColor;
        }
    }

    public Color ProgressBackgroundColor
    {
        get
        {
            return progressBackgroundColor;
        }
    }
    #endregion
}

/// <summary>
/// Stores one combo rank milestone with its display identifier, HUD presentation overrides, and temporary Character Tuning bonus formulas.
/// none.
/// returns none.
/// </summary>
[Serializable]
public sealed class PlayerComboRankDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable combo-rank identifier used for Add Scaling stat keys and for the runtime rank label exposed by the combo state.")]
    [SerializeField] private string rankId = "Rank01";

    [Tooltip("Minimum combo value required to activate this rank and its temporary bonuses.")]
    [SerializeField] private int requiredComboValue = 10;

    [Tooltip("Optional HUD presentation overrides applied automatically while this rank is active.")]
    [SerializeField] private PlayerComboRankVisualDefinition rankVisuals = new PlayerComboRankVisualDefinition();

    [Tooltip("Ordered Character Tuning formulas applied while this rank remains active. Active combo ranks stack cumulatively in ascending milestone order.")]
    [SerializeField] private PowerUpCharacterTuningModuleData rankBonuses = new PowerUpCharacterTuningModuleData();
    #endregion

    #endregion

    #region Properties
    public string RankId
    {
        get
        {
            return rankId;
        }
    }

    public int RequiredComboValue
    {
        get
        {
            return requiredComboValue;
        }
    }

    public PowerUpCharacterTuningModuleData RankBonuses
    {
        get
        {
            return rankBonuses;
        }
    }

    public PlayerComboRankVisualDefinition RankVisuals
    {
        get
        {
            return rankVisuals;
        }
    }
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the authored combo-rank identity, milestone threshold, optional HUD visuals, and temporary Character Tuning bonuses.
    /// /params rankIdValue Stable rank identifier shown by the runtime combo label.
    /// /params requiredComboValueValue Minimum combo value required by this rank.
    /// /params rankVisualsValue Optional HUD visuals resolved automatically while this rank is active.
    /// /params rankBonusesValue Character Tuning formulas applied while the rank is active.
    /// /returns void.
    /// </summary>
    public void Configure(string rankIdValue,
                          int requiredComboValueValue,
                          PlayerComboRankVisualDefinition rankVisualsValue,
                          PowerUpCharacterTuningModuleData rankBonusesValue)
    {
        rankId = rankIdValue;
        requiredComboValue = requiredComboValueValue;
        rankVisuals = rankVisualsValue ?? new PlayerComboRankVisualDefinition();
        rankBonuses = rankBonusesValue;
    }

    /// <summary>
    /// Assigns the authored combo-rank identity, milestone threshold, and temporary Character Tuning bonuses while preserving current HUD visuals.
    /// /params rankIdValue Stable rank identifier shown by the runtime combo label.
    /// /params requiredComboValueValue Minimum combo value required by this rank.
    /// /params rankBonusesValue Character Tuning formulas applied while the rank is active.
    /// /returns void.
    /// </summary>
    public void Configure(string rankIdValue, int requiredComboValueValue, PowerUpCharacterTuningModuleData rankBonusesValue)
    {
        Configure(rankIdValue,
                  requiredComboValueValue,
                  rankVisuals,
                  rankBonusesValue);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Ensures nested Character Tuning payloads exist and trims identifier serialization noise without snapping numeric values.
    /// /params None.
    /// /returns void.
    /// </summary>
    public void Validate()
    {
        if (rankId == null)
        {
            rankId = string.Empty;
        }

        rankId = rankId.Trim();

        if (rankBonuses == null)
        {
            rankBonuses = new PowerUpCharacterTuningModuleData();
        }

        if (rankVisuals == null)
        {
            rankVisuals = new PlayerComboRankVisualDefinition();
        }

        rankBonuses.Validate();
    }
    #endregion

    #endregion
}
