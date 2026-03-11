using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Selects how a schedule step modifies a scalable stat.
/// </summary>
public enum PlayerLevelUpScheduleApplyMode
{
    Flat = 0,
    Percent = 1
}

/// <summary>
/// Defines one weighted tier roll candidate used by a progression milestone.
/// </summary>
[Serializable]
public sealed class PlayerMilestoneTierRollDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Tier ID selected from the Player Power-Ups preset tier catalog.")]
    [SerializeField] private string tierId;

    [Tooltip("Relative probability weight used when rolling this tier at milestone level-up.")]
    [SerializeField] private float selectionWeight = 1f;
    #endregion

    #endregion

    #region Properties
    public string TierId
    {
        get
        {
            return tierId;
        }
    }

    public float SelectionWeight
    {
        get
        {
            return selectionWeight;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns milestone tier-roll values after external editor selection.
    /// </summary>
    /// <param name="tierIdValue">Tier ID resolved from the power-ups preset.</param>
    /// <param name="selectionWeightValue">Relative roll weight inside milestone tier candidates.</param>
    /// <returns>Void.</returns>
    public void Configure(string tierIdValue, float selectionWeightValue)
    {
        tierId = tierIdValue;
        selectionWeight = selectionWeightValue;
    }

    /// <summary>
    /// Sanitizes serialized values to avoid invalid milestone tier roll entries.
    /// </summary>
    /// <param name="fallbackTierId">Fallback tier ID used when the current value is empty.</param>
    /// <returns>Void.</returns>
    public void Validate(string fallbackTierId)
    {
        if (string.IsNullOrWhiteSpace(tierId))
            tierId = string.IsNullOrWhiteSpace(fallbackTierId) ? string.Empty : fallbackTierId.Trim();
        else
            tierId = tierId.Trim();

        if (selectionWeight < 0f)
            selectionWeight = 0f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines one stat step used by repeating level-up schedules.
/// </summary>
[Serializable]
public sealed class PlayerLevelUpScheduleStepDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Scalable stat name selected from the progression scalable-stats catalog.")]
    [SerializeField] private string statName;

    [Tooltip("Operation mode used to apply this step value.")]
    [SerializeField] private PlayerLevelUpScheduleApplyMode applyMode = PlayerLevelUpScheduleApplyMode.Flat;

    [Tooltip("Step value applied to the selected stat.")]
    [SerializeField] private float value = 1f;
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

    public PlayerLevelUpScheduleApplyMode ApplyMode
    {
        get
        {
            return applyMode;
        }
    }

    public float Value
    {
        get
        {
            return value;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns schedule-step data after external editor normalization.
    /// </summary>
    /// <param name="statNameValue">Scalable stat name targeted by this step.</param>
    /// <param name="applyModeValue">Stat operation mode.</param>
    /// <param name="valueValue">Step value used by the selected operation mode.</param>
    /// <returns>Void.</returns>
    public void Configure(string statNameValue, PlayerLevelUpScheduleApplyMode applyModeValue, float valueValue)
    {
        statName = statNameValue;
        applyMode = applyModeValue;
        value = valueValue;
    }

    /// <summary>
    /// Sanitizes this step for deterministic runtime execution.
    /// </summary>
    /// <param name="fallbackStatName">Fallback stat name used when the current value is empty.</param>
    /// <returns>Void.</returns>
    public void Validate(string fallbackStatName)
    {
        if (string.IsNullOrWhiteSpace(statName))
            statName = string.IsNullOrWhiteSpace(fallbackStatName) ? string.Empty : fallbackStatName.Trim();
        else
            statName = statName.Trim();
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines one repeating level-up schedule made of ordered stat-boost steps.
/// </summary>
[Serializable]
public sealed class PlayerLevelUpScheduleDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable schedule ID used as equipped schedule reference on the progression preset.")]
    [SerializeField] private string scheduleId = "Schedule0";

    [Tooltip("Ordered sequence of stat changes applied cyclically each time the player levels up.")]
    [SerializeField] private List<PlayerLevelUpScheduleStepDefinition> sequence = new List<PlayerLevelUpScheduleStepDefinition>();
    #endregion

    #endregion

    #region Properties
    public string ScheduleId
    {
        get
        {
            return scheduleId;
        }
    }

    public IReadOnlyList<PlayerLevelUpScheduleStepDefinition> Sequence
    {
        get
        {
            return sequence;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns schedule values after editor-side migrations.
    /// </summary>
    /// <param name="scheduleIdValue">Schedule identifier.</param>
    /// <param name="sequenceValue">Ordered stat-step sequence.</param>
    /// <returns>Void.</returns>
    public void Configure(string scheduleIdValue, List<PlayerLevelUpScheduleStepDefinition> sequenceValue)
    {
        scheduleId = scheduleIdValue;
        sequence = sequenceValue;
    }

    /// <summary>
    /// Sanitizes schedule ID and nested step entries.
    /// </summary>
    /// <param name="fallbackScheduleId">Fallback schedule ID used when the current value is empty.</param>
    /// <returns>Void.</returns>
    public void Validate(string fallbackScheduleId)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
            scheduleId = string.IsNullOrWhiteSpace(fallbackScheduleId) ? "Schedule0" : fallbackScheduleId.Trim();
        else
            scheduleId = scheduleId.Trim();

        if (sequence == null)
            sequence = new List<PlayerLevelUpScheduleStepDefinition>();

        for (int stepIndex = 0; stepIndex < sequence.Count; stepIndex++)
        {
            PlayerLevelUpScheduleStepDefinition step = sequence[stepIndex];

            if (step != null)
                continue;

            step = new PlayerLevelUpScheduleStepDefinition();
            sequence[stepIndex] = step;
        }

        for (int stepIndex = 0; stepIndex < sequence.Count; stepIndex++)
            sequence[stepIndex].Validate(string.Empty);
    }

    /// <summary>
    /// Removes schedule steps that reference unknown scalable-stat names.
    /// Keeps placeholder entries with empty stat names so list editing remains possible in Inspector.
    /// </summary>
    /// <param name="validStatNames">Set of scalable-stat names available in the progression preset.</param>
    /// <returns>Void.</returns>
    public void RemoveInvalidStatSteps(ISet<string> validStatNames)
    {
        if (sequence == null)
            return;

        if (validStatNames == null || validStatNames.Count <= 0)
            return;

        for (int stepIndex = sequence.Count - 1; stepIndex >= 0; stepIndex--)
        {
            PlayerLevelUpScheduleStepDefinition step = sequence[stepIndex];

            if (step == null)
            {
                sequence.RemoveAt(stepIndex);
                continue;
            }

            step.Validate(string.Empty);

            if (string.IsNullOrWhiteSpace(step.StatName))
                continue;

            if (validStatNames.Contains(step.StatName))
                continue;

            sequence.RemoveAt(stepIndex);
        }
    }
    #endregion

    #endregion
}
