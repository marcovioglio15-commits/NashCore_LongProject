using System;
using System.Globalization;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves player level progression by consuming runtime experience with game-phase and milestone requirements.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerProgressionInitializeSystem))]
public partial struct PlayerLevelUpSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Configures singleton requirements for level progression updates.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerExperience>();
        state.RequireForUpdate<PlayerLevel>();
        state.RequireForUpdate<PlayerProgressionConfig>();
    }

    /// <summary>
    /// Resolves one or more level-ups, then updates cached phase/required experience values.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<PlayerExperience> playerExperience,
                  RefRW<PlayerLevel> playerLevel,
                  RefRO<PlayerProgressionConfig> progressionConfig,
                  DynamicBuffer<PlayerScalableStatElement> scalableStats)
                 in SystemAPI.Query<RefRW<PlayerExperience>,
                                    RefRW<PlayerLevel>,
                                    RefRO<PlayerProgressionConfig>,
                                    DynamicBuffer<PlayerScalableStatElement>>())
        {
            float currentExperience = playerExperience.ValueRO.Current;

            if (currentExperience < 0f)
            {
                currentExperience = 0f;
            }

            int currentLevel = playerLevel.ValueRO.Current;

            if (currentLevel < 1)
            {
                currentLevel = 1;
            }

            int activeGamePhaseIndex = playerLevel.ValueRO.ActiveGamePhaseIndex;
            float requiredExperienceForNextLevel = playerLevel.ValueRO.RequiredExperienceForNextLevel;

            if (requiredExperienceForNextLevel < 1f)
            {
                requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig.ValueRO,
                                                                                                                    currentLevel,
                                                                                                                    out activeGamePhaseIndex,
                                                                                                                    out bool _,
                                                                                                                    out int _);
            }

            int startingGamePhaseIndex = activeGamePhaseIndex;
            bool reachedMilestone = false;
            int lastReachedMilestoneLevel = 0;
            int gainedLevelsCount = 0;

            while (currentExperience >= requiredExperienceForNextLevel)
            {
                currentExperience -= requiredExperienceForNextLevel;
                currentLevel += 1;
                gainedLevelsCount += 1;
                requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig.ValueRO,
                                                                                                                    currentLevel,
                                                                                                                    out activeGamePhaseIndex,
                                                                                                                    out bool isMilestoneRequirement,
                                                                                                                    out int milestoneLevel);

                if (isMilestoneRequirement)
                {
                    reachedMilestone = true;
                    lastReachedMilestoneLevel = milestoneLevel;
                }

                if (gainedLevelsCount >= 1024)
                {
                    break;
                }
            }

            playerExperience.ValueRW = new PlayerExperience
            {
                Current = currentExperience
            };
            playerLevel.ValueRW = new PlayerLevel
            {
                Current = currentLevel,
                ActiveGamePhaseIndex = activeGamePhaseIndex,
                RequiredExperienceForNextLevel = requiredExperienceForNextLevel
            };
            SyncScalableStats(scalableStats, currentExperience, currentLevel);

            if (gainedLevelsCount <= 0)
            {
                continue;
            }

            string previousPhaseID = PlayerProgressionPhaseUtility.ResolvePhaseID(progressionConfig.ValueRO, startingGamePhaseIndex);
            string activePhaseID = PlayerProgressionPhaseUtility.ResolvePhaseID(progressionConfig.ValueRO, activeGamePhaseIndex);
            string phaseTransition = startingGamePhaseIndex != activeGamePhaseIndex
                ? string.Format(CultureInfo.InvariantCulture, "Phase changed: Yes ({0} -> {1}).", previousPhaseID, activePhaseID)
                : string.Format(CultureInfo.InvariantCulture, "Phase changed: No (Current {0}).", activePhaseID);
            string milestoneState = reachedMilestone
                ? string.Format(CultureInfo.InvariantCulture, "Milestone reached: Yes (Level {0}).", lastReachedMilestoneLevel)
                : "Milestone reached: No.";
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                    "[PlayerLevelUpSystem] Player reached level {0}. {1} Required EXP for next level-up: {2:0.###}. {3}",
                                    currentLevel,
                                    phaseTransition,
                                    requiredExperienceForNextLevel,
                                    milestoneState));
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Synchronizes common progression scalable stat values after experience/level changes.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable stat buffer.</param>
    /// <param name="experienceValue">Current experience value to propagate.</param>
    /// <param name="levelValue">Current level value to propagate.</param>
    /// <returns>Void.</returns>
    private static void SyncScalableStats(DynamicBuffer<PlayerScalableStatElement> scalableStats, float experienceValue, int levelValue)
    {
        if (scalableStats.IsCreated == false)
        {
            return;
        }

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement statElement = scalableStats[statIndex];
            string statName = statElement.Name.ToString();

            if (string.Equals(statName, "experience", StringComparison.OrdinalIgnoreCase))
            {
                statElement.Value = experienceValue;
                scalableStats[statIndex] = statElement;
                continue;
            }

            if (string.Equals(statName, "level", StringComparison.OrdinalIgnoreCase) == false)
            {
                continue;
            }

            statElement.Value = levelValue;
            scalableStats[statIndex] = statElement;
        }
    }
    #endregion

    #endregion
}
