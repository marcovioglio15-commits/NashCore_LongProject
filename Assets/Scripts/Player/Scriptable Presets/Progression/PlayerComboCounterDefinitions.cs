using System;
using System.Collections.Generic;
using UnityEngine;

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
    /// /params shieldDamageBreaksComboValue True when shield-only damage should also interrupt the combo.
    /// /params rankDefinitionsValue Ordered rank list stored by this combo definition.
    /// /returns void.
    /// </summary>
    public void Configure(bool isEnabledValue,
                          int comboGainPerKillValue,
                          bool shieldDamageBreaksComboValue,
                          List<PlayerComboRankDefinition> rankDefinitionsValue)
    {
        isEnabled = isEnabledValue;
        comboGainPerKill = comboGainPerKillValue;
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
public sealed class PlayerComboRankDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable combo-rank identifier used for Add Scaling stat keys and runtime HUD presentation.")]
    [SerializeField] private string rankId = "Rank01";

    [Tooltip("Minimum combo value required to activate this rank and its temporary bonuses.")]
    [SerializeField] private int requiredComboValue = 10;

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
    #endregion

    #region Methods

    #region Setup
    /// <summary>
    /// Assigns the authored combo-rank identity, milestone threshold, and temporary Character Tuning bonuses.
    /// /params rankIdValue Stable rank identifier shown by the HUD.
    /// /params requiredComboValueValue Minimum combo value required by this rank.
    /// /params rankBonusesValue Character Tuning formulas applied while the rank is active.
    /// /returns void.
    /// </summary>
    public void Configure(string rankIdValue, int requiredComboValueValue, PowerUpCharacterTuningModuleData rankBonusesValue)
    {
        rankId = rankIdValue;
        requiredComboValue = requiredComboValueValue;
        rankBonuses = rankBonusesValue;
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

        rankBonuses.Validate();
    }
    #endregion

    #endregion
}
