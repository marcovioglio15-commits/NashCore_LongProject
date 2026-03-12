using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds the progression blob used by runtime level-up, milestone, and scalable-stat systems.
/// </summary>
internal static class PlayerProgressionBlobBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the baked progression blob from the selected progression preset.
    /// </summary>
    /// <param name="preset">Source progression preset used during baking.</param>
    /// <returns>Persistent blob asset reference ready to assign to PlayerProgressionConfig.</returns>
    public static BlobAssetReference<PlayerProgressionConfigBlob> BuildProgressionConfigBlob(PlayerProgressionPreset preset)
    {
        BlobBuilder builder = new BlobBuilder(Allocator.Temp);
        ref PlayerProgressionConfigBlob root = ref builder.ConstructRoot<PlayerProgressionConfigBlob>();

        root.ExperiencePickupRadius = preset != null ? math.max(0f, preset.ExperiencePickupRadius) : 0f;
        root.MilestoneTimeScaleResumeDurationSeconds = preset != null ? math.max(0f, preset.MilestoneTimeScaleResumeDurationSeconds) : 0f;
        root.EquippedScheduleIndex = -1;

        BakeProgressionGamePhases(builder, ref root, preset);
        BakeProgressionScalableStats(builder, ref root, preset);
        BakeProgressionSchedules(builder, ref root, preset);

        BlobAssetReference<PlayerProgressionConfigBlob> blob = builder.CreateBlobAssetReference<PlayerProgressionConfigBlob>(Allocator.Persistent);
        builder.Dispose();
        return blob;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Bakes game phases, milestone requirements, milestone offer rolls, and skip compensations.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays and strings.</param>
    /// <param name="root">Progression blob root being populated.</param>
    /// <param name="preset">Source progression preset.</param>
    /// <returns>Void.</returns>
    private static void BakeProgressionGamePhases(BlobBuilder builder,
                                                  ref PlayerProgressionConfigBlob root,
                                                  PlayerProgressionPreset preset)
    {
        IReadOnlyList<PlayerGamePhaseDefinition> gamePhases = preset != null ? preset.GamePhasesDefinition : null;
        int gamePhasesCount = gamePhases != null && gamePhases.Count > 0 ? gamePhases.Count : 1;
        BlobBuilderArray<PlayerGamePhaseBlob> gamePhasesArray = builder.Allocate(ref root.GamePhases, gamePhasesCount);

        // Bake each phase with safe defaults when authoring data is missing.
        for (int phaseIndex = 0; phaseIndex < gamePhasesCount; phaseIndex++)
        {
            PlayerGamePhaseDefinition gamePhase = gamePhases != null && phaseIndex < gamePhases.Count ? gamePhases[phaseIndex] : null;
            string phaseID = gamePhase != null ? gamePhase.PhaseID : string.Format("Phase{0}", phaseIndex);
            int startsAtLevel = gamePhase != null ? math.max(0, gamePhase.StartsAtLevel) : 0;
            float startingRequiredLevelUpExp = gamePhase != null ? math.max(1f, gamePhase.StartingRequiredLevelUpExp) : 100f;
            float requiredExperienceGrouth = gamePhase != null ? math.max(0f, gamePhase.RequiredExperienceGrouth) : 0f;

            if (string.IsNullOrWhiteSpace(phaseID))
                phaseID = string.Format("Phase{0}", phaseIndex);

            gamePhasesArray[phaseIndex] = new PlayerGamePhaseBlob
            {
                StartsAtLevel = startsAtLevel,
                StartingRequiredLevelUpExp = startingRequiredLevelUpExp,
                RequiredExperienceGrouth = requiredExperienceGrouth
            };
            builder.AllocateString(ref gamePhasesArray[phaseIndex].PhaseID, phaseID);

            IReadOnlyList<PlayerLevelUpMilestoneDefinition> milestones = gamePhase != null ? gamePhase.Milestones : null;
            int milestonesCount = milestones != null ? milestones.Count : 0;
            BlobBuilderArray<PlayerLevelUpMilestoneBlob> milestoneArray = builder.Allocate(ref gamePhasesArray[phaseIndex].Milestones, milestonesCount);

            // Bake each milestone together with its nested custom unlock rolls.
            for (int milestoneIndex = 0; milestoneIndex < milestonesCount; milestoneIndex++)
            {
                PlayerLevelUpMilestoneDefinition milestone = milestones[milestoneIndex];
                int milestoneLevel = milestone != null ? math.max(startsAtLevel, milestone.MilestoneLevel) : startsAtLevel;
                float specialExpRequirement = milestone != null ? math.max(1f, milestone.SpecialExpRequirement) : startingRequiredLevelUpExp;

                milestoneArray[milestoneIndex] = new PlayerLevelUpMilestoneBlob
                {
                    MilestoneLevel = milestoneLevel,
                    SpecialExpRequirement = specialExpRequirement
                };

                BakeMilestonePowerUpUnlocks(builder, ref milestoneArray[milestoneIndex], milestone);
                BakeMilestoneSkipCompensations(builder, ref milestoneArray[milestoneIndex], milestone);
            }
        }
    }

    /// <summary>
    /// Bakes milestone power-up extractions, each with its own tier-roll list.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays and strings.</param>
    /// <param name="milestoneBlob">Destination milestone blob.</param>
    /// <param name="milestone">Source milestone definition.</param>
    /// <returns>Void.</returns>
    private static void BakeMilestonePowerUpUnlocks(BlobBuilder builder,
                                                    ref PlayerLevelUpMilestoneBlob milestoneBlob,
                                                    PlayerLevelUpMilestoneDefinition milestone)
    {
        IReadOnlyList<PlayerMilestonePowerUpUnlockDefinition> powerUpUnlocks = milestone != null ? milestone.PowerUpUnlocks : null;
        int powerUpUnlockCount = powerUpUnlocks != null ? math.min(PlayerLevelUpMilestoneDefinition.MaxPowerUpUnlockCount, powerUpUnlocks.Count) : 0;
        BlobBuilderArray<PlayerMilestonePowerUpUnlockBlob> powerUpUnlockArray = builder.Allocate(ref milestoneBlob.PowerUpUnlocks, powerUpUnlockCount);

        for (int powerUpUnlockIndex = 0; powerUpUnlockIndex < powerUpUnlockCount; powerUpUnlockIndex++)
        {
            PlayerMilestonePowerUpUnlockDefinition powerUpUnlock = powerUpUnlocks[powerUpUnlockIndex];
            IReadOnlyList<PlayerMilestoneTierRollDefinition> tierRolls = powerUpUnlock != null ? powerUpUnlock.TierRolls : null;
            int tierRollCount = tierRolls != null ? tierRolls.Count : 0;
            BlobBuilderArray<PlayerMilestoneTierRollBlob> tierRollArray = builder.Allocate(ref powerUpUnlockArray[powerUpUnlockIndex].TierRolls, tierRollCount);

            // Copy authoring tier candidates into the contiguous blob array.
            for (int tierRollIndex = 0; tierRollIndex < tierRollCount; tierRollIndex++)
            {
                PlayerMilestoneTierRollDefinition tierRoll = tierRolls[tierRollIndex];
                string tierId = tierRoll != null && !string.IsNullOrWhiteSpace(tierRoll.TierId)
                    ? tierRoll.TierId.Trim()
                    : string.Empty;
                float selectionWeight = tierRoll != null ? math.max(0f, tierRoll.SelectionWeight) : 0f;

                tierRollArray[tierRollIndex] = new PlayerMilestoneTierRollBlob
                {
                    SelectionWeight = selectionWeight
                };
                builder.AllocateString(ref tierRollArray[tierRollIndex].TierId, tierId);
            }
        }
    }

    /// <summary>
    /// Bakes skip-compensation resource entries for one milestone.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays.</param>
    /// <param name="milestoneBlob">Destination milestone blob.</param>
    /// <param name="milestone">Source milestone definition.</param>
    /// <returns>Void.</returns>
    private static void BakeMilestoneSkipCompensations(BlobBuilder builder,
                                                       ref PlayerLevelUpMilestoneBlob milestoneBlob,
                                                       PlayerLevelUpMilestoneDefinition milestone)
    {
        IReadOnlyList<PlayerMilestoneSkipCompensationDefinition> skipCompensationResources = milestone != null ? milestone.SkipCompensationResources : null;
        int compensationCount = skipCompensationResources != null ? skipCompensationResources.Count : 0;
        BlobBuilderArray<PlayerMilestoneSkipCompensationBlob> compensationArray = builder.Allocate(ref milestoneBlob.SkipCompensationResources, compensationCount);

        for (int compensationIndex = 0; compensationIndex < compensationCount; compensationIndex++)
        {
            PlayerMilestoneSkipCompensationDefinition compensation = skipCompensationResources[compensationIndex];
            PlayerMilestoneSkipCompensationResourceType resourceType = compensation != null
                ? compensation.ResourceType
                : PlayerMilestoneSkipCompensationResourceType.Health;
            PlayerMilestoneCompensationApplyMode applyMode = compensation != null
                ? compensation.ApplyMode
                : PlayerMilestoneCompensationApplyMode.Flat;
            float value = compensation != null ? math.max(0f, compensation.Value) : 0f;

            compensationArray[compensationIndex] = new PlayerMilestoneSkipCompensationBlob
            {
                ResourceType = (byte)resourceType,
                ApplyMode = (byte)applyMode,
                Value = value
            };
        }
    }

    /// <summary>
    /// Bakes default scalable-stat values into the progression blob.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays and strings.</param>
    /// <param name="root">Progression blob root being populated.</param>
    /// <param name="preset">Source progression preset.</param>
    /// <returns>Void.</returns>
    private static void BakeProgressionScalableStats(BlobBuilder builder,
                                                     ref PlayerProgressionConfigBlob root,
                                                     PlayerProgressionPreset preset)
    {
        IReadOnlyList<PlayerScalableStatDefinition> scalableStats = preset != null ? preset.ScalableStats : null;
        int scalableStatsCount = scalableStats != null ? scalableStats.Count : 0;
        BlobBuilderArray<PlayerScalableStatBlob> scalableStatsArray = builder.Allocate(ref root.ScalableStats, scalableStatsCount);

        for (int statIndex = 0; statIndex < scalableStatsCount; statIndex++)
        {
            PlayerScalableStatDefinition scalableStat = scalableStats[statIndex];
            string statName = scalableStat != null ? scalableStat.StatName : string.Format("stat{0}", statIndex + 1);
            float defaultValue = scalableStat != null ? scalableStat.DefaultValue : 0f;
            PlayerScalableStatType statType = scalableStat != null ? scalableStat.StatType : PlayerScalableStatType.Float;

            if (statType == PlayerScalableStatType.Integer)
                defaultValue = Mathf.Round(defaultValue);

            if (string.IsNullOrWhiteSpace(statName))
                statName = string.Format("stat{0}", statIndex + 1);

            scalableStatsArray[statIndex] = new PlayerScalableStatBlob
            {
                Type = (byte)statType,
                DefaultValue = defaultValue
            };
            builder.AllocateString(ref scalableStatsArray[statIndex].Name, statName);
        }
    }

    /// <summary>
    /// Bakes repeating level-up schedules and resolves the equipped schedule index.
    /// </summary>
    /// <param name="builder">Blob builder used to allocate nested arrays and strings.</param>
    /// <param name="root">Progression blob root being populated.</param>
    /// <param name="preset">Source progression preset.</param>
    /// <returns>Void.</returns>
    private static void BakeProgressionSchedules(BlobBuilder builder,
                                                 ref PlayerProgressionConfigBlob root,
                                                 PlayerProgressionPreset preset)
    {
        IReadOnlyList<PlayerLevelUpScheduleDefinition> schedules = preset != null ? preset.Schedules : null;
        int schedulesCount = schedules != null ? schedules.Count : 0;
        BlobBuilderArray<PlayerLevelUpScheduleBlob> schedulesArray = builder.Allocate(ref root.Schedules, schedulesCount);
        root.EquippedScheduleIndex = -1;
        string equippedScheduleId = preset != null ? preset.EquippedScheduleId : string.Empty;

        for (int scheduleIndex = 0; scheduleIndex < schedulesCount; scheduleIndex++)
        {
            PlayerLevelUpScheduleDefinition schedule = schedules[scheduleIndex];
            string scheduleId = schedule != null && !string.IsNullOrWhiteSpace(schedule.ScheduleId)
                ? schedule.ScheduleId.Trim()
                : string.Format("Schedule{0}", scheduleIndex + 1);

            schedulesArray[scheduleIndex] = new PlayerLevelUpScheduleBlob();
            builder.AllocateString(ref schedulesArray[scheduleIndex].ScheduleId, scheduleId);

            IReadOnlyList<PlayerLevelUpScheduleStepDefinition> sequence = schedule != null ? schedule.Sequence : null;
            int stepCount = sequence != null ? sequence.Count : 0;
            BlobBuilderArray<PlayerLevelUpScheduleStepBlob> stepArray = builder.Allocate(ref schedulesArray[scheduleIndex].Steps, stepCount);

            // Serialize schedule steps in authoring order for deterministic runtime cycling.
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                PlayerLevelUpScheduleStepDefinition step = sequence[stepIndex];
                string statName = step != null && !string.IsNullOrWhiteSpace(step.StatName)
                    ? step.StatName.Trim()
                    : string.Empty;
                PlayerLevelUpScheduleApplyMode applyMode = step != null ? step.ApplyMode : PlayerLevelUpScheduleApplyMode.Flat;
                float value = step != null ? step.Value : 0f;

                stepArray[stepIndex] = new PlayerLevelUpScheduleStepBlob
                {
                    ApplyMode = (byte)applyMode,
                    Value = value
                };
                builder.AllocateString(ref stepArray[stepIndex].StatName, statName);
            }

            if (root.EquippedScheduleIndex >= 0 || string.IsNullOrWhiteSpace(equippedScheduleId))
                continue;

            if (!string.Equals(scheduleId, equippedScheduleId, System.StringComparison.OrdinalIgnoreCase))
                continue;

            root.EquippedScheduleIndex = scheduleIndex;
        }

        if (root.EquippedScheduleIndex >= 0 || schedulesCount <= 0)
            return;

        root.EquippedScheduleIndex = 0;
    }
    #endregion

    #endregion
}
