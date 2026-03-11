using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines one progression game phase with linear growth and optional milestone overrides.
/// </summary>
[Serializable]
public sealed class PlayerGamePhaseDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Custom identifier used to reference this game phase in debug and runtime logs.")]
    [SerializeField] private string phaseID = "Phase0";

    [Tooltip("First player level where this game phase becomes active.")]
    [SerializeField] private int startsAtLevel = 1;

    [Tooltip("Required level-up experience at the first level included in this game phase.")]
    [SerializeField] private float startingRequiredLevelUpExp = 100f;

    [Tooltip("Linear increase added to required level-up experience for each additional level in this game phase.")]
    [SerializeField] private float requiredExperienceGrouth = 0f;

    [Tooltip("Optional special requirements applied when the current player level matches a milestone.")]
    [SerializeField] private List<PlayerLevelUpMilestoneDefinition> milestones = new List<PlayerLevelUpMilestoneDefinition>();
    #endregion

    #endregion

    #region Properties
    public string PhaseID
    {
        get
        {
            return phaseID;
        }
    }

    public int StartsAtLevel
    {
        get
        {
            return startsAtLevel;
        }
    }

    public float StartingRequiredLevelUpExp
    {
        get
        {
            return startingRequiredLevelUpExp;
        }
    }

    public float RequiredExperienceGrouth
    {
        get
        {
            return requiredExperienceGrouth;
        }
    }

    public IReadOnlyList<PlayerLevelUpMilestoneDefinition> Milestones
    {
        get
        {
            return milestones;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Assigns game phase values after external normalization logic.
    /// </summary>
    /// <param name="phaseIDValue">Phase ID to assign.</param>
    /// <param name="startsAtLevelValue">Start level to assign.</param>
    /// <param name="startingRequiredLevelUpExpValue">Initial required level-up experience for this phase.</param>
    /// <param name="requiredExperienceGrouthValue">Linear required experience increase per level.</param>
    /// <returns>Void.</returns>
    public void Configure(string phaseIDValue,
                          int startsAtLevelValue,
                          float startingRequiredLevelUpExpValue,
                          float requiredExperienceGrouthValue)
    {
        phaseID = string.IsNullOrWhiteSpace(phaseIDValue) ? "Phase0" : phaseIDValue.Trim();
        startsAtLevel = Mathf.Max(1, startsAtLevelValue);
        startingRequiredLevelUpExp = Mathf.Max(1f, startingRequiredLevelUpExpValue);
        requiredExperienceGrouth = Mathf.Max(0f, requiredExperienceGrouthValue);
    }

    /// <summary>
    /// Validates and normalizes game phase fields and all nested milestone entries.
    /// </summary>
    /// <param name="fallbackPhaseID">Fallback phase ID used when the current ID is empty.</param>
    /// <param name="minimumStartLevel">Minimum allowed start level for this phase.</param>
    /// <param name="fallbackStartingRequiredLevelUpExp">Fallback required experience used when values are invalid.</param>
    /// <returns>Void.</returns>
    public void Validate(string fallbackPhaseID, int minimumStartLevel, float fallbackStartingRequiredLevelUpExp)
    {
        if (string.IsNullOrWhiteSpace(phaseID))
        {
            phaseID = string.IsNullOrWhiteSpace(fallbackPhaseID) ? "Phase0" : fallbackPhaseID.Trim();
        }
        else
        {
            phaseID = phaseID.Trim();
        }

        if (startsAtLevel < minimumStartLevel)
        {
            startsAtLevel = minimumStartLevel;
        }

        if (startsAtLevel < 1)
        {
            startsAtLevel = 1;
        }

        if (startingRequiredLevelUpExp < 1f)
        {
            startingRequiredLevelUpExp = Mathf.Max(1f, fallbackStartingRequiredLevelUpExp);
        }

        if (requiredExperienceGrouth < 0f)
        {
            requiredExperienceGrouth = 0f;
        }

        if (milestones == null)
        {
            milestones = new List<PlayerLevelUpMilestoneDefinition>();
        }

        ValidateMilestonesAgainstStartLevel();
    }

    /// <summary>
    /// Validates milestone entries using this phase start level as minimum threshold.
    /// </summary>
    /// <returns>Void.</returns>
    public void ValidateMilestonesAgainstStartLevel()
    {
        if (milestones == null)
        {
            milestones = new List<PlayerLevelUpMilestoneDefinition>();
        }

        for (int milestoneIndex = 0; milestoneIndex < milestones.Count; milestoneIndex++)
        {
            PlayerLevelUpMilestoneDefinition milestone = milestones[milestoneIndex];

            if (milestone != null)
            {
                continue;
            }

            milestone = new PlayerLevelUpMilestoneDefinition();
            milestones[milestoneIndex] = milestone;
        }

        for (int milestoneIndex = 0; milestoneIndex < milestones.Count; milestoneIndex++)
        {
            PlayerLevelUpMilestoneDefinition milestone = milestones[milestoneIndex];
            milestone.Validate(startsAtLevel, startingRequiredLevelUpExp);
        }

        milestones.Sort(CompareMilestonesByLevel);
        EnsureUniqueMilestoneLevels();
    }
    #endregion

    #region Private Methods
    private static int CompareMilestonesByLevel(PlayerLevelUpMilestoneDefinition leftMilestone, PlayerLevelUpMilestoneDefinition rightMilestone)
    {
        int leftLevel = leftMilestone != null ? leftMilestone.MilestoneLevel : 1;
        int rightLevel = rightMilestone != null ? rightMilestone.MilestoneLevel : 1;
        return leftLevel.CompareTo(rightLevel);
    }

    private void EnsureUniqueMilestoneLevels()
    {
        HashSet<int> visitedLevels = new HashSet<int>();

        for (int milestoneIndex = milestones.Count - 1; milestoneIndex >= 0; milestoneIndex--)
        {
            PlayerLevelUpMilestoneDefinition milestone = milestones[milestoneIndex];

            if (visitedLevels.Add(milestone.MilestoneLevel))
            {
                continue;
            }

            milestones.RemoveAt(milestoneIndex);
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Defines one level milestone override used to create one-time experience spikes and optional power-up unlock rolls.
/// </summary>
[Serializable]
public sealed class PlayerLevelUpMilestoneDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Player level that activates this special requirement for the next level-up.")]
    [SerializeField] private int milestoneLevel = 5;

    [Tooltip("Special required experience applied when this milestone level is active.")]
    [SerializeField] private float specialExpRequirement = 300f;

    [Tooltip("Number of power-up extractions rolled when this milestone level is reached.")]
    [SerializeField] private int powerUpUnlockRollCount = 1;

    [Tooltip("Weighted tier candidates used to resolve milestone power-up extractions.")]
    [SerializeField] private List<PlayerMilestoneTierRollDefinition> milestoneTierRolls = new List<PlayerMilestoneTierRollDefinition>();
    #endregion

    #endregion

    #region Properties
    public int MilestoneLevel
    {
        get
        {
            return milestoneLevel;
        }
    }

    public float SpecialExpRequirement
    {
        get
        {
            return specialExpRequirement;
        }
    }

    public int PowerUpUnlockRollCount
    {
        get
        {
            return powerUpUnlockRollCount;
        }
    }

    public IReadOnlyList<PlayerMilestoneTierRollDefinition> MilestoneTierRolls
    {
        get
        {
            return milestoneTierRolls;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates this milestone against minimum level and required experience constraints.
    /// </summary>
    /// <param name="minimumMilestoneLevel">Minimum allowed level for the milestone.</param>
    /// <param name="fallbackSpecialRequirement">Fallback requirement used when current value is invalid.</param>
    /// <returns>Void.</returns>
    public void Validate(int minimumMilestoneLevel, float fallbackSpecialRequirement)
    {
        if (milestoneLevel < minimumMilestoneLevel)
        {
            milestoneLevel = minimumMilestoneLevel;
        }

        if (milestoneLevel < 1)
        {
            milestoneLevel = 1;
        }

        if (specialExpRequirement < 1f)
        {
            specialExpRequirement = Mathf.Max(1f, fallbackSpecialRequirement);
        }

        if (powerUpUnlockRollCount < 0)
        {
            powerUpUnlockRollCount = 0;
        }

        if (milestoneTierRolls == null)
        {
            milestoneTierRolls = new List<PlayerMilestoneTierRollDefinition>();
        }

        for (int tierRollIndex = 0; tierRollIndex < milestoneTierRolls.Count; tierRollIndex++)
        {
            PlayerMilestoneTierRollDefinition tierRoll = milestoneTierRolls[tierRollIndex];

            if (tierRoll != null)
            {
                continue;
            }

            tierRoll = new PlayerMilestoneTierRollDefinition();
            milestoneTierRolls[tierRollIndex] = tierRoll;
        }

        HashSet<string> visitedTierIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        for (int tierRollIndex = milestoneTierRolls.Count - 1; tierRollIndex >= 0; tierRollIndex--)
        {
            PlayerMilestoneTierRollDefinition tierRoll = milestoneTierRolls[tierRollIndex];
            tierRoll.Validate(string.Empty);

            if (string.IsNullOrWhiteSpace(tierRoll.TierId))
                continue;

            if (visitedTierIds.Add(tierRoll.TierId))
                continue;

            milestoneTierRolls.RemoveAt(tierRollIndex);
        }
    }
    #endregion

    #endregion
}
