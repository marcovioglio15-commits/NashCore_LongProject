using System;
using System.Globalization;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Resolves player level progression, applies schedule steps, and triggers milestone power-up selection rolls.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerProgressionInitializeSystem))]
public partial struct PlayerLevelUpSystem : ISystem
{
    #region Constants
    private const int MaxLevelUpsPerFrame = 1024;
    #endregion

    #region Nested Types
    private struct ScheduleApplyDebugInfo
    {
        public string ScheduleId;
        public string StatName;
        public int StepIndex;
        public int StepCount;
        public PlayerLevelUpScheduleApplyMode ApplyMode;
        public float StepValue;
        public float PreviousValue;
        public float NewValue;
    }
    #endregion

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
    /// Resolves one or more level-ups, applies schedule growth and opens milestone power-up selection when needed.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerMilestonePowerUpSelectionState> milestoneSelectionStateLookup = SystemAPI.GetComponentLookup<PlayerMilestonePowerUpSelectionState>(false);
        BufferLookup<PlayerMilestonePowerUpSelectionOfferElement> milestoneSelectionOffersLookup = SystemAPI.GetBufferLookup<PlayerMilestonePowerUpSelectionOfferElement>(false);
        BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup = SystemAPI.GetBufferLookup<PlayerPowerUpUnlockCatalogElement>(false);
        BufferLookup<PlayerPowerUpTierDefinitionElement> tierDefinitionsLookup = SystemAPI.GetBufferLookup<PlayerPowerUpTierDefinitionElement>(true);
        BufferLookup<PlayerPowerUpTierEntryElement> tierEntriesLookup = SystemAPI.GetBufferLookup<PlayerPowerUpTierEntryElement>(true);
        ComponentLookup<PlayerMilestoneTimeScaleResumeState> milestoneTimeScaleResumeStateLookup = SystemAPI.GetComponentLookup<PlayerMilestoneTimeScaleResumeState>(false);

        foreach ((RefRW<PlayerExperience> playerExperience,
                  RefRW<PlayerLevel> playerLevel,
                  RefRO<PlayerProgressionConfig> progressionConfig,
                  DynamicBuffer<PlayerScalableStatElement> scalableStats,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerExperience>,
                                    RefRW<PlayerLevel>,
                                    RefRO<PlayerProgressionConfig>,
                                    DynamicBuffer<PlayerScalableStatElement>>().WithEntityAccess())
        {
            bool hasMilestoneSelectionData = PlayerMilestonePowerUpRollUtility.HasMilestoneSelectionData(entity,
                                                                                                           in milestoneSelectionStateLookup,
                                                                                                           in milestoneSelectionOffersLookup,
                                                                                                           in unlockCatalogLookup,
                                                                                                           in tierDefinitionsLookup,
                                                                                                           in tierEntriesLookup);
            PlayerMilestonePowerUpSelectionState milestoneSelectionState = hasMilestoneSelectionData
                ? milestoneSelectionStateLookup[entity]
                : default;

            // Pause progression consumption while a milestone selection is active.
            if (hasMilestoneSelectionData && milestoneSelectionState.IsSelectionActive != 0)
            {
                SyncScalableStats(scalableStats, playerExperience.ValueRO.Current, playerLevel.ValueRO.Current);
                continue;
            }

            float currentExperience = mathMax(0f, playerExperience.ValueRO.Current);
            int currentLevel = mathMax(1, playerLevel.ValueRO.Current);
            int activeGamePhaseIndex = playerLevel.ValueRO.ActiveGamePhaseIndex;
            float requiredExperienceForNextLevel = playerLevel.ValueRO.RequiredExperienceForNextLevel;

            if (requiredExperienceForNextLevel < 1f)
                requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig.ValueRO,
                                                                                                                   currentLevel,
                                                                                                                   out activeGamePhaseIndex,
                                                                                                                   out bool _,
                                                                                                                   out int _);

            int startingGamePhaseIndex = activeGamePhaseIndex;
            bool reachedMilestone = false;
            int lastReachedMilestoneLevel = 0;
            int gainedLevelsCount = 0;
            bool openedMilestoneSelection = false;
            int milestoneOfferCount = 0;

            while (currentExperience >= requiredExperienceForNextLevel)
            {
                // Consume current threshold and advance one level.
                currentExperience -= requiredExperienceForNextLevel;
                currentLevel += 1;
                gainedLevelsCount += 1;

                // Apply repeating level-up schedule for the newly reached level.
                if (TryApplyScheduleStep(progressionConfig.ValueRO,
                                         scalableStats,
                                         currentLevel,
                                         out ScheduleApplyDebugInfo scheduleDebugInfo))
                {
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                            "[PlayerLevelUpSystem] Schedule '{0}' step {1}/{2} applied on '{3}' at level {4}. Mode: {5}, Value: {6:0.###}, Result: {7:0.###} -> {8:0.###}.",
                                            scheduleDebugInfo.ScheduleId,
                                            scheduleDebugInfo.StepIndex + 1,
                                            scheduleDebugInfo.StepCount,
                                            scheduleDebugInfo.StatName,
                                            currentLevel,
                                            scheduleDebugInfo.ApplyMode,
                                            scheduleDebugInfo.StepValue,
                                            scheduleDebugInfo.PreviousValue,
                                            scheduleDebugInfo.NewValue));
                }

                requiredExperienceForNextLevel = PlayerProgressionPhaseUtility.ResolveRequiredExperienceForLevel(progressionConfig.ValueRO,
                                                                                                                   currentLevel,
                                                                                                                   out activeGamePhaseIndex,
                                                                                                                   out bool isMilestoneRequirement,
                                                                                                                   out int milestoneLevel);

                if (!isMilestoneRequirement)
                {
                    if (gainedLevelsCount < MaxLevelUpsPerFrame)
                        continue;

                    break;
                }

                reachedMilestone = true;
                lastReachedMilestoneLevel = milestoneLevel;

                if (!hasMilestoneSelectionData)
                {
                    if (gainedLevelsCount < MaxLevelUpsPerFrame)
                        continue;

                    break;
                }

                DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog = unlockCatalogLookup[entity];
                DynamicBuffer<PlayerPowerUpTierDefinitionElement> tierDefinitions = tierDefinitionsLookup[entity];
                DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries = tierEntriesLookup[entity];
                DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers = milestoneSelectionOffersLookup[entity];

                if (!PlayerMilestonePowerUpRollUtility.TryOpenMilestoneSelection(progressionConfig.ValueRO,
                                                                                 activeGamePhaseIndex,
                                                                                 milestoneLevel,
                                                                                 unlockCatalog,
                                                                                 tierDefinitions,
                                                                                 tierEntries,
                                                                                 selectionOffers,
                                                                                 ref milestoneSelectionState,
                                                                                 out milestoneOfferCount))
                {
                    if (gainedLevelsCount < MaxLevelUpsPerFrame)
                        continue;

                    break;
                }

                openedMilestoneSelection = true;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                        "[PlayerLevelUpSystem] Milestone level {0} opened power-up selection with {1} rolled offer(s).",
                                        milestoneLevel,
                                        milestoneOfferCount));
                break;
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

            if (hasMilestoneSelectionData)
                milestoneSelectionStateLookup[entity] = milestoneSelectionState;

            if (openedMilestoneSelection)
            {
                if (milestoneTimeScaleResumeStateLookup.HasComponent(entity))
                    milestoneTimeScaleResumeStateLookup[entity] = PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState();

                Time.timeScale = 0f;
            }

            if (gainedLevelsCount <= 0)
                continue;

            string previousPhaseID = PlayerProgressionPhaseUtility.ResolvePhaseID(progressionConfig.ValueRO, startingGamePhaseIndex);
            string activePhaseID = PlayerProgressionPhaseUtility.ResolvePhaseID(progressionConfig.ValueRO, activeGamePhaseIndex);
            string phaseTransition = startingGamePhaseIndex != activeGamePhaseIndex
                ? string.Format(CultureInfo.InvariantCulture, "Phase changed: Yes ({0} -> {1}).", previousPhaseID, activePhaseID)
                : string.Format(CultureInfo.InvariantCulture, "Phase changed: No (Current {0}).", activePhaseID);
            string milestoneState = reachedMilestone
                ? string.Format(CultureInfo.InvariantCulture, "Milestone reached: Yes (Level {0}).", lastReachedMilestoneLevel)
                : "Milestone reached: No.";
            string selectionState = openedMilestoneSelection
                ? string.Format(CultureInfo.InvariantCulture, "Milestone selection opened: Yes ({0} offer(s)).", milestoneOfferCount)
                : "Milestone selection opened: No.";
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                    "[PlayerLevelUpSystem] Player reached level {0}. {1} Required EXP for next level-up: {2:0.###}. {3} {4}",
                                    currentLevel,
                                    phaseTransition,
                                    requiredExperienceForNextLevel,
                                    milestoneState,
                                    selectionState));
        }
    }
    #endregion

    #region Schedules
    /// <summary>
    /// Applies one schedule step for the newly reached level.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="scalableStats">Runtime scalable-stat buffer.</param>
    /// <param name="targetLevel">Newly reached player level.</param>
    /// <param name="scheduleDebugInfo">Debug payload describing the applied step.</param>
    /// <returns>True when a step is applied; otherwise false.</returns>
    private static bool TryApplyScheduleStep(PlayerProgressionConfig progressionConfig,
                                             DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                             int targetLevel,
                                             out ScheduleApplyDebugInfo scheduleDebugInfo)
    {
        scheduleDebugInfo = default;

        if (targetLevel <= 1)
            return false;

        if (!progressionConfig.Config.IsCreated)
            return false;

        if (!scalableStats.IsCreated || scalableStats.Length <= 0)
            return false;

        ref PlayerProgressionConfigBlob root = ref progressionConfig.Config.Value;
        int equippedScheduleIndex = root.EquippedScheduleIndex;

        if (equippedScheduleIndex < 0 || equippedScheduleIndex >= root.Schedules.Length)
            return false;

        ref PlayerLevelUpScheduleBlob schedule = ref root.Schedules[equippedScheduleIndex];

        if (schedule.Steps.Length <= 0)
            return false;

        int stepIndex = (targetLevel - 2) % schedule.Steps.Length;
        ref PlayerLevelUpScheduleStepBlob step = ref schedule.Steps[stepIndex];
        string statName = step.StatName.ToString();

        if (string.IsNullOrWhiteSpace(statName))
            return false;

        int statBufferIndex = FindScalableStatIndex(scalableStats, statName);

        if (statBufferIndex < 0)
            return false;

        PlayerScalableStatElement scalableStat = scalableStats[statBufferIndex];
        float previousValue = scalableStat.Value;
        PlayerLevelUpScheduleApplyMode applyMode = step.ApplyMode == (byte)PlayerLevelUpScheduleApplyMode.Percent
            ? PlayerLevelUpScheduleApplyMode.Percent
            : PlayerLevelUpScheduleApplyMode.Flat;
        float deltaValue = step.Value;
        float newValue = applyMode == PlayerLevelUpScheduleApplyMode.Percent
            ? previousValue + (previousValue * (deltaValue * 0.01f))
            : previousValue + deltaValue;

        if ((PlayerScalableStatType)scalableStat.Type == PlayerScalableStatType.Integer)
            newValue = (float)Math.Round(newValue, MidpointRounding.AwayFromZero);

        scalableStat.Value = newValue;
        scalableStats[statBufferIndex] = scalableStat;
        scheduleDebugInfo = new ScheduleApplyDebugInfo
        {
            ScheduleId = schedule.ScheduleId.ToString(),
            StatName = statName,
            StepIndex = stepIndex,
            StepCount = schedule.Steps.Length,
            ApplyMode = applyMode,
            StepValue = deltaValue,
            PreviousValue = previousValue,
            NewValue = newValue
        };
        return true;
    }

    /// <summary>
    /// Resolves one scalable-stat buffer index by stat name.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable-stat buffer.</param>
    /// <param name="statName">Requested scalable-stat name.</param>
    /// <returns>Buffer index when found; otherwise -1.</returns>
    private static int FindScalableStatIndex(DynamicBuffer<PlayerScalableStatElement> scalableStats, string statName)
    {
        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement candidate = scalableStats[statIndex];

            if (!string.Equals(candidate.Name.ToString(), statName, StringComparison.OrdinalIgnoreCase))
                continue;

            return statIndex;
        }

        return -1;
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
        if (!scalableStats.IsCreated)
            return;

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

            if (!string.Equals(statName, "level", StringComparison.OrdinalIgnoreCase))
                continue;

            statElement.Value = levelValue;
            scalableStats[statIndex] = statElement;
        }
    }

    private static int mathMax(int left, int right)
    {
        return left > right ? left : right;
    }

    private static float mathMax(float left, float right)
    {
        return left > right ? left : right;
    }
    #endregion

    #endregion
}
