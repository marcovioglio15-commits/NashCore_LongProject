using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies which runtime resource is granted when a milestone power-up selection is skipped.
/// </summary>
public enum PlayerMilestoneSkipCompensationResourceType
{
    Health = 0,
    Shield = 1,
    PrimaryActiveEnergy = 2,
    SecondaryActiveEnergy = 3,
    Experience = 4
}

/// <summary>
/// Defines how a milestone skip-compensation value is interpreted.
/// </summary>
public enum PlayerMilestoneCompensationApplyMode
{
    Flat = 0,
    Percent = 1
}

/// <summary>
/// Defines one milestone power-up roll with its own tier-roll candidates.
/// </summary>
[Serializable]
public sealed class PlayerMilestonePowerUpUnlockDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Weighted tier candidates used to resolve this specific milestone power-up extraction.")]
    [SerializeField] private List<PlayerMilestoneTierRollDefinition> tierRolls = new List<PlayerMilestoneTierRollDefinition>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<PlayerMilestoneTierRollDefinition> TierRolls
    {
        get
        {
            return tierRolls;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns the tier-roll collection after editor migration or cloning logic.
    /// </summary>
    /// <param name="tierRollsValue">Tier-roll list used by this extraction definition.</param>
    /// <returns>Void.</returns>
    public void Configure(List<PlayerMilestoneTierRollDefinition> tierRollsValue)
    {
        tierRolls = tierRollsValue;
    }

    /// <summary>
    /// Sanitizes nested tier-roll entries while keeping placeholder rows available in the Inspector.
    /// </summary>
    /// <returns>Void.</returns>
    public void Validate()
    {
        if (tierRolls == null)
            tierRolls = new List<PlayerMilestoneTierRollDefinition>();

        for (int tierRollIndex = 0; tierRollIndex < tierRolls.Count; tierRollIndex++)
        {
            PlayerMilestoneTierRollDefinition tierRoll = tierRolls[tierRollIndex];

            if (tierRoll != null)
                continue;

            tierRoll = new PlayerMilestoneTierRollDefinition();
            tierRolls[tierRollIndex] = tierRoll;
        }

        HashSet<string> visitedTierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int tierRollIndex = tierRolls.Count - 1; tierRollIndex >= 0; tierRollIndex--)
        {
            PlayerMilestoneTierRollDefinition tierRoll = tierRolls[tierRollIndex];
            tierRoll.Validate(string.Empty);

            if (string.IsNullOrWhiteSpace(tierRoll.TierId))
                continue;

            if (visitedTierIds.Add(tierRoll.TierId))
                continue;

            tierRolls.RemoveAt(tierRollIndex);
        }
    }

    /// <summary>
    /// Builds a deep-cloned list of tier-roll definitions for migration from legacy milestone data.
    /// </summary>
    /// <param name="sourceTierRolls">Source tier-roll list to clone.</param>
    /// <returns>Cloned tier-roll definitions, never null.</returns>
    public static List<PlayerMilestoneTierRollDefinition> CloneTierRolls(IReadOnlyList<PlayerMilestoneTierRollDefinition> sourceTierRolls)
    {
        List<PlayerMilestoneTierRollDefinition> clonedTierRolls = new List<PlayerMilestoneTierRollDefinition>();

        if (sourceTierRolls == null)
            return clonedTierRolls;

        for (int tierRollIndex = 0; tierRollIndex < sourceTierRolls.Count; tierRollIndex++)
        {
            PlayerMilestoneTierRollDefinition sourceTierRoll = sourceTierRolls[tierRollIndex];
            PlayerMilestoneTierRollDefinition clonedTierRoll = new PlayerMilestoneTierRollDefinition();

            if (sourceTierRoll != null)
                clonedTierRoll.Configure(sourceTierRoll.TierId, sourceTierRoll.SelectionWeight);

            clonedTierRolls.Add(clonedTierRoll);
        }

        return clonedTierRolls;
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines one resource compensation granted when the player skips a milestone power-up selection.
/// </summary>
[Serializable]
public sealed class PlayerMilestoneSkipCompensationDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Runtime resource granted when the player presses Skip on this milestone.")]
    [SerializeField] private PlayerMilestoneSkipCompensationResourceType resourceType = PlayerMilestoneSkipCompensationResourceType.Health;

    [Tooltip("How Value is interpreted for the selected resource.")]
    [SerializeField] private PlayerMilestoneCompensationApplyMode applyMode = PlayerMilestoneCompensationApplyMode.Flat;

    [Tooltip("Flat amount or percentage granted to the selected resource when this milestone is skipped.")]
    [SerializeField] private float value;
    #endregion

    #endregion

    #region Properties
    public PlayerMilestoneSkipCompensationResourceType ResourceType
    {
        get
        {
            return resourceType;
        }
    }

    public PlayerMilestoneCompensationApplyMode ApplyMode
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
    /// Assigns skip-compensation values after editor duplication or migration logic.
    /// </summary>
    /// <param name="resourceTypeValue">Resource modified by this compensation entry.</param>
    /// <param name="applyModeValue">Value interpretation mode.</param>
    /// <param name="valueValue">Flat amount or percentage value.</param>
    /// <returns>Void.</returns>
    public void Configure(PlayerMilestoneSkipCompensationResourceType resourceTypeValue,
                          PlayerMilestoneCompensationApplyMode applyModeValue,
                          float valueValue)
    {
        resourceType = resourceTypeValue;
        applyMode = applyModeValue;
        value = valueValue;
    }

    /// <summary>
    /// Sanitizes the serialized compensation value to keep runtime application predictable.
    /// </summary>
    /// <returns>Void.</returns>
    public void Validate()
    {
        if (value < 0f)
            value = 0f;
    }
    #endregion

    #endregion
}
