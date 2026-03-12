using System;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Provides shared helpers to resolve runtime game phases, milestone flags, and level-up experience requirements.
/// </summary>
public static class PlayerProgressionPhaseUtility
{
    #region Constants
    private const float DefaultRequiredExperience = 100f;
    private const string DefaultPhaseID = "Phase0";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves required experience for a specific player level using the baked progression config.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="levelValue">Player level used to resolve current phase and requirement.</param>
    /// <param name="activeGamePhaseIndex">Resolved index of the active game phase.</param>
    /// <param name="isMilestoneRequirement">True when the requirement came from a milestone override.</param>
    /// <param name="milestoneLevel">Milestone level that produced the override, or 0 when not used.</param>
    /// <returns>Required experience for the next level-up attempt.</returns>
    public static float ResolveRequiredExperienceForLevel(PlayerProgressionConfig progressionConfig,
                                                          int levelValue,
                                                          out int activeGamePhaseIndex,
                                                          out bool isMilestoneRequirement,
                                                          out int milestoneLevel)
    {
        if (levelValue < 1)
        {
            levelValue = 1;
        }

        if (!progressionConfig.Config.IsCreated)
        {
            activeGamePhaseIndex = 0;
            isMilestoneRequirement = false;
            milestoneLevel = 0;
            return DefaultRequiredExperience;
        }

        ref BlobArray<PlayerGamePhaseBlob> gamePhases = ref progressionConfig.Config.Value.GamePhases;
        return ResolveRequiredExperienceForLevel(ref gamePhases,
                                                 levelValue,
                                                 out activeGamePhaseIndex,
                                                 out isMilestoneRequirement,
                                                 out milestoneLevel);
    }

    /// <summary>
    /// Resolves the active game phase index for a given level from baked progression config.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="levelValue">Player level used to select the phase.</param>
    /// <returns>Index of the active game phase, or 0 when data is missing.</returns>
    public static int ResolveActiveGamePhaseIndex(PlayerProgressionConfig progressionConfig, int levelValue)
    {
        if (!progressionConfig.Config.IsCreated)
        {
            return 0;
        }

        ref BlobArray<PlayerGamePhaseBlob> gamePhases = ref progressionConfig.Config.Value.GamePhases;
        return ResolveActiveGamePhaseIndex(ref gamePhases, levelValue);
    }

    /// <summary>
    /// Resolves displayable phase ID text from baked progression config and phase index.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="phaseIndex">Phase index to resolve.</param>
    /// <returns>Phase ID string with safe fallback when index/data is invalid.</returns>
    public static string ResolvePhaseID(PlayerProgressionConfig progressionConfig, int phaseIndex)
    {
        if (!progressionConfig.Config.IsCreated)
        {
            return DefaultPhaseID;
        }

        ref BlobArray<PlayerGamePhaseBlob> gamePhases = ref progressionConfig.Config.Value.GamePhases;

        if (phaseIndex < 0 || phaseIndex >= gamePhases.Length)
        {
            return DefaultPhaseID;
        }

        string resolvedPhaseID = gamePhases[phaseIndex].PhaseID.ToString();

        if (string.IsNullOrWhiteSpace(resolvedPhaseID))
        {
            return DefaultPhaseID;
        }

        return resolvedPhaseID;
    }

    /// <summary>
    /// Resolves one milestone index inside the specified game phase.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="gamePhaseIndex">Game phase index expected to contain the milestone.</param>
    /// <param name="milestoneLevel">Milestone level to resolve.</param>
    /// <param name="milestoneIndex">Resolved milestone index when found.</param>
    /// <returns>True when the milestone exists in the specified phase; otherwise false.</returns>
    public static bool TryResolveMilestoneIndex(PlayerProgressionConfig progressionConfig,
                                                int gamePhaseIndex,
                                                int milestoneLevel,
                                                out int milestoneIndex)
    {
        milestoneIndex = -1;

        if (!progressionConfig.Config.IsCreated)
            return false;

        ref BlobArray<PlayerGamePhaseBlob> gamePhases = ref progressionConfig.Config.Value.GamePhases;

        if (gamePhaseIndex < 0 || gamePhaseIndex >= gamePhases.Length)
            return false;

        ref PlayerGamePhaseBlob gamePhase = ref gamePhases[gamePhaseIndex];
        return TryResolveMilestoneIndex(ref gamePhase, milestoneLevel, out milestoneIndex);
    }
    #endregion

    #region Private Methods
    private static float ResolveRequiredExperienceForLevel(ref BlobArray<PlayerGamePhaseBlob> gamePhases,
                                                           int levelValue,
                                                           out int activeGamePhaseIndex,
                                                           out bool isMilestoneRequirement,
                                                           out int milestoneLevel)
    {
        if (levelValue < 1)
        {
            levelValue = 1;
        }

        activeGamePhaseIndex = ResolveActiveGamePhaseIndex(ref gamePhases, levelValue);
        isMilestoneRequirement = false;
        milestoneLevel = 0;

        if (gamePhases.Length == 0)
        {
            return DefaultRequiredExperience;
        }

        ref PlayerGamePhaseBlob activeGamePhase = ref gamePhases[activeGamePhaseIndex];
        int relativePhaseLevel = levelValue - activeGamePhase.StartsAtLevel;

        if (relativePhaseLevel < 0)
        {
            relativePhaseLevel = 0;
        }

        float resolvedRequirement = activeGamePhase.StartingRequiredLevelUpExp + (activeGamePhase.RequiredExperienceGrouth * relativePhaseLevel);

        if (resolvedRequirement < 1f)
        {
            resolvedRequirement = 1f;
        }

        ref BlobArray<PlayerLevelUpMilestoneBlob> milestones = ref activeGamePhase.Milestones;

        for (int milestoneIndex = 0; milestoneIndex < milestones.Length; milestoneIndex++)
        {
            ref PlayerLevelUpMilestoneBlob milestone = ref milestones[milestoneIndex];

            if (milestone.MilestoneLevel != levelValue)
            {
                continue;
            }

            float milestoneRequirement = milestone.SpecialExpRequirement;

            if (milestoneRequirement < 1f)
            {
                milestoneRequirement = 1f;
            }

            resolvedRequirement = milestoneRequirement;
            isMilestoneRequirement = true;
            milestoneLevel = milestone.MilestoneLevel;
            break;
        }

        return resolvedRequirement;
    }

    private static bool TryResolveMilestoneIndex(ref PlayerGamePhaseBlob gamePhase,
                                                 int milestoneLevel,
                                                 out int milestoneIndex)
    {
        milestoneIndex = -1;

        for (int milestoneIndexValue = 0; milestoneIndexValue < gamePhase.Milestones.Length; milestoneIndexValue++)
        {
            ref PlayerLevelUpMilestoneBlob milestone = ref gamePhase.Milestones[milestoneIndexValue];

            if (milestone.MilestoneLevel != milestoneLevel)
                continue;

            milestoneIndex = milestoneIndexValue;
            return true;
        }

        return false;
    }

    private static int ResolveActiveGamePhaseIndex(ref BlobArray<PlayerGamePhaseBlob> gamePhases, int levelValue)
    {
        if (levelValue < 1)
        {
            levelValue = 1;
        }

        if (gamePhases.Length == 0)
        {
            return 0;
        }

        for (int phaseIndex = gamePhases.Length - 1; phaseIndex >= 0; phaseIndex--)
        {
            ref PlayerGamePhaseBlob gamePhase = ref gamePhases[phaseIndex];

            if (levelValue < gamePhase.StartsAtLevel)
            {
                continue;
            }

            return phaseIndex;
        }

        return 0;
    }
    #endregion

    #endregion
}
