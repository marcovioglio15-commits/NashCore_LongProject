using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Provides shared runtime helpers for milestone tier roll extraction and offer generation.
/// </summary>
public static class PlayerMilestonePowerUpRollUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Checks whether all milestone-selection runtime components are available on the player.
    /// </summary>
    /// <param name="entity">Player entity being validated.</param>
    /// <param name="milestoneSelectionStateLookup">Selection-state component lookup.</param>
    /// <param name="milestoneSelectionOffersLookup">Selection-offers buffer lookup.</param>
    /// <param name="unlockCatalogLookup">Unlock catalog buffer lookup.</param>
    /// <param name="tierDefinitionsLookup">Tier definitions buffer lookup.</param>
    /// <param name="tierEntriesLookup">Tier entries buffer lookup.</param>
    /// <returns>True when all required components/buffers are available; otherwise false.</returns>
    public static bool HasMilestoneSelectionData(Entity entity,
                                                 in ComponentLookup<PlayerMilestonePowerUpSelectionState> milestoneSelectionStateLookup,
                                                 in BufferLookup<PlayerMilestonePowerUpSelectionOfferElement> milestoneSelectionOffersLookup,
                                                 in BufferLookup<PlayerPowerUpUnlockCatalogElement> unlockCatalogLookup,
                                                 in BufferLookup<PlayerPowerUpTierDefinitionElement> tierDefinitionsLookup,
                                                 in BufferLookup<PlayerPowerUpTierEntryElement> tierEntriesLookup)
    {
        if (!milestoneSelectionStateLookup.HasComponent(entity))
            return false;

        if (!milestoneSelectionOffersLookup.HasBuffer(entity))
            return false;

        if (!unlockCatalogLookup.HasBuffer(entity))
            return false;

        if (!tierDefinitionsLookup.HasBuffer(entity))
            return false;

        return tierEntriesLookup.HasBuffer(entity);
    }

    /// <summary>
    /// Rolls milestone offers and activates runtime selection state.
    /// </summary>
    /// <param name="progressionConfig">Runtime progression configuration component.</param>
    /// <param name="activeGamePhaseIndex">Resolved active game-phase index for current level.</param>
    /// <param name="milestoneLevel">Milestone level being processed.</param>
    /// <param name="unlockCatalog">Unlock catalog used to exclude already unlocked entries.</param>
    /// <param name="tierDefinitions">Tier definitions buffer.</param>
    /// <param name="tierEntries">Flattened tier-entry buffer.</param>
    /// <param name="selectionOffers">Selection-offers destination buffer.</param>
    /// <param name="selectionState">Selection-state component updated in place.</param>
    /// <param name="rolledOfferCount">Number of offers rolled for this milestone selection.</param>
    /// <returns>True when at least one offer is rolled and selection is activated; otherwise false.</returns>
    public static bool TryOpenMilestoneSelection(PlayerProgressionConfig progressionConfig,
                                                 int activeGamePhaseIndex,
                                                 int milestoneLevel,
                                                 DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                                 DynamicBuffer<PlayerPowerUpTierDefinitionElement> tierDefinitions,
                                                 DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                                 DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers,
                                                 ref PlayerMilestonePowerUpSelectionState selectionState,
                                                 out int rolledOfferCount)
    {
        rolledOfferCount = 0;

        if (!progressionConfig.Config.IsCreated)
            return false;

        if (selectionState.IsSelectionActive != 0)
            return false;

        if (activeGamePhaseIndex < 0 || activeGamePhaseIndex >= progressionConfig.Config.Value.GamePhases.Length)
            return false;

        ref PlayerGamePhaseBlob gamePhase = ref progressionConfig.Config.Value.GamePhases[activeGamePhaseIndex];

        if (!TryResolveMilestoneIndex(ref gamePhase, milestoneLevel, out int milestoneIndex))
            return false;

        ref PlayerLevelUpMilestoneBlob milestoneBlob = ref gamePhase.Milestones[milestoneIndex];

        if (milestoneBlob.PowerUpUnlockRollCount <= 0 || milestoneBlob.TierRolls.Length <= 0)
            return false;

        selectionOffers.Clear();
        HashSet<int> rolledCatalogIndices = new HashSet<int>();

        for (int rollIndex = 0; rollIndex < milestoneBlob.PowerUpUnlockRollCount; rollIndex++)
        {
            if (!TryRollMilestoneOffer(ref milestoneBlob,
                                       unlockCatalog,
                                       tierDefinitions,
                                       tierEntries,
                                       rolledCatalogIndices,
                                       out int rolledCatalogIndex,
                                       out string selectedTierId,
                                       out float selectedTierWeight,
                                       out float selectedEntryWeight))
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                        "[PlayerLevelUpSystem] Milestone {0} roll {1}/{2} failed: no valid tier/power-up candidate.",
                                        milestoneLevel,
                                        rollIndex + 1,
                                        milestoneBlob.PowerUpUnlockRollCount));
                continue;
            }

            rolledCatalogIndices.Add(rolledCatalogIndex);
            PlayerPowerUpUnlockCatalogElement unlockEntry = unlockCatalog[rolledCatalogIndex];
            selectionOffers.Add(new PlayerMilestonePowerUpSelectionOfferElement
            {
                CatalogIndex = rolledCatalogIndex,
                PowerUpId = unlockEntry.PowerUpId,
                DisplayName = unlockEntry.DisplayName,
                Description = unlockEntry.Description,
                UnlockKind = unlockEntry.UnlockKind
            });
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                                    "[PlayerLevelUpSystem] Milestone {0} roll {1}/{2}: Tier '{3}' (Weight {4:0.###}) -> Power-Up '{5}' ({6}) [Entry Weight {7:0.###}].",
                                    milestoneLevel,
                                    rollIndex + 1,
                                    milestoneBlob.PowerUpUnlockRollCount,
                                    selectedTierId,
                                    selectedTierWeight,
                                    unlockEntry.PowerUpId.ToString(),
                                    unlockEntry.UnlockKind,
                                    selectedEntryWeight));
        }

        rolledOfferCount = selectionOffers.Length;

        if (rolledOfferCount <= 0)
            return false;

        selectionState.IsSelectionActive = 1;
        selectionState.MilestoneLevel = milestoneLevel;
        selectionState.OfferCount = rolledOfferCount;
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves one milestone index by level inside a game phase.
    /// </summary>
    /// <param name="gamePhase">Game-phase blob containing milestone entries.</param>
    /// <param name="milestoneLevel">Target milestone level to resolve.</param>
    /// <param name="milestoneIndex">Resolved milestone index when found.</param>
    /// <returns>True when the milestone exists in the phase; otherwise false.</returns>
    private static bool TryResolveMilestoneIndex(ref PlayerGamePhaseBlob gamePhase,
                                                 int milestoneLevel,
                                                 out int milestoneIndex)
    {
        milestoneIndex = -1;

        for (int index = 0; index < gamePhase.Milestones.Length; index++)
        {
            ref PlayerLevelUpMilestoneBlob candidate = ref gamePhase.Milestones[index];

            if (candidate.MilestoneLevel != milestoneLevel)
                continue;

            milestoneIndex = index;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Rolls one unlock catalog entry from milestone tier candidates.
    /// </summary>
    /// <param name="milestoneBlob">Milestone blob containing tier-roll settings.</param>
    /// <param name="unlockCatalog">Unlock catalog buffer.</param>
    /// <param name="tierDefinitions">Tier definitions buffer.</param>
    /// <param name="tierEntries">Flattened tier-entry buffer.</param>
    /// <param name="rolledCatalogIndices">Catalog indices already rolled in this milestone selection.</param>
    /// <param name="rolledCatalogIndex">Resolved rolled catalog index when successful.</param>
    /// <param name="selectedTierId">Tier ID selected for the current roll.</param>
    /// <param name="selectedTierWeight">Weight of the selected milestone tier candidate.</param>
    /// <param name="selectedEntryWeight">Weight of the selected power-up entry inside the selected tier.</param>
    /// <returns>True when an entry is successfully rolled; otherwise false.</returns>
    private static bool TryRollMilestoneOffer(ref PlayerLevelUpMilestoneBlob milestoneBlob,
                                              DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                              DynamicBuffer<PlayerPowerUpTierDefinitionElement> tierDefinitions,
                                              DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                              HashSet<int> rolledCatalogIndices,
                                              out int rolledCatalogIndex,
                                              out string selectedTierId,
                                              out float selectedTierWeight,
                                              out float selectedEntryWeight)
    {
        rolledCatalogIndex = -1;
        selectedTierId = string.Empty;
        selectedTierWeight = 0f;
        selectedEntryWeight = 0f;
        List<int> rollCandidateIndices = new List<int>();
        List<float> rollCandidateWeights = new List<float>();

        // Collect milestone tier rolls that currently have at least one available unlock candidate.
        for (int tierRollIndex = 0; tierRollIndex < milestoneBlob.TierRolls.Length; tierRollIndex++)
        {
            ref PlayerMilestoneTierRollBlob tierRoll = ref milestoneBlob.TierRolls[tierRollIndex];
            float tierRollWeight = mathMax(0f, tierRoll.SelectionWeight);

            if (tierRollWeight <= 0f)
                continue;

            string tierId = tierRoll.TierId.ToString();

            if (!TryResolveTierDefinition(tierDefinitions, tierId, out PlayerPowerUpTierDefinitionElement tierDefinition))
                continue;

            if (!HasAnyRollableEntry(tierDefinition, tierEntries, unlockCatalog, rolledCatalogIndices))
                continue;

            rollCandidateIndices.Add(tierRollIndex);
            rollCandidateWeights.Add(tierRollWeight);
        }

        int selectedTierRollCandidate = RollWeightedIndex(rollCandidateWeights);

        if (selectedTierRollCandidate < 0)
            return false;

        int selectedTierRollIndex = rollCandidateIndices[selectedTierRollCandidate];
        ref PlayerMilestoneTierRollBlob selectedTierRoll = ref milestoneBlob.TierRolls[selectedTierRollIndex];
        selectedTierId = selectedTierRoll.TierId.ToString();
        selectedTierWeight = mathMax(0f, selectedTierRoll.SelectionWeight);

        if (!TryResolveTierDefinition(tierDefinitions, selectedTierId, out PlayerPowerUpTierDefinitionElement selectedTierDefinition))
            return false;

        return TryRollCatalogFromTier(selectedTierDefinition,
                                      tierEntries,
                                      unlockCatalog,
                                      rolledCatalogIndices,
                                      out rolledCatalogIndex,
                                      out selectedEntryWeight);
    }

    /// <summary>
    /// Resolves one tier definition by ID.
    /// </summary>
    /// <param name="tierDefinitions">Tier definitions buffer.</param>
    /// <param name="tierId">Requested tier ID.</param>
    /// <param name="tierDefinition">Resolved tier definition when found.</param>
    /// <returns>True when tier exists; otherwise false.</returns>
    private static bool TryResolveTierDefinition(DynamicBuffer<PlayerPowerUpTierDefinitionElement> tierDefinitions,
                                                 string tierId,
                                                 out PlayerPowerUpTierDefinitionElement tierDefinition)
    {
        tierDefinition = default;

        if (string.IsNullOrWhiteSpace(tierId))
            return false;

        for (int tierIndex = 0; tierIndex < tierDefinitions.Length; tierIndex++)
        {
            PlayerPowerUpTierDefinitionElement candidate = tierDefinitions[tierIndex];

            if (!string.Equals(candidate.TierId.ToString(), tierId, StringComparison.OrdinalIgnoreCase))
                continue;

            tierDefinition = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a tier still has at least one available unlock candidate.
    /// </summary>
    /// <param name="tierDefinition">Tier metadata entry.</param>
    /// <param name="tierEntries">Flattened tier-entry buffer.</param>
    /// <param name="unlockCatalog">Unlock catalog buffer.</param>
    /// <param name="rolledCatalogIndices">Catalog indices already rolled in current milestone selection.</param>
    /// <returns>True when at least one rollable candidate is available; otherwise false.</returns>
    private static bool HasAnyRollableEntry(in PlayerPowerUpTierDefinitionElement tierDefinition,
                                            DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                            HashSet<int> rolledCatalogIndices)
    {
        int startIndex = mathMax(0, tierDefinition.EntryStartIndex);
        int endIndex = mathMin(tierEntries.Length, startIndex + mathMax(0, tierDefinition.EntryCount));

        for (int tierEntryIndex = startIndex; tierEntryIndex < endIndex; tierEntryIndex++)
        {
            PlayerPowerUpTierEntryElement tierEntry = tierEntries[tierEntryIndex];

            if (tierEntry.SelectionWeight <= 0f)
                continue;

            int catalogIndex = tierEntry.CatalogIndex;

            if (catalogIndex < 0 || catalogIndex >= unlockCatalog.Length)
                continue;

            if (rolledCatalogIndices.Contains(catalogIndex))
                continue;

            if (unlockCatalog[catalogIndex].IsUnlocked != 0)
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Rolls one unlock catalog index from a tier definition.
    /// </summary>
    /// <param name="tierDefinition">Tier metadata entry.</param>
    /// <param name="tierEntries">Flattened tier-entry buffer.</param>
    /// <param name="unlockCatalog">Unlock catalog buffer.</param>
    /// <param name="rolledCatalogIndices">Catalog indices already rolled in current milestone selection.</param>
    /// <param name="catalogIndex">Resolved catalog index when successful.</param>
    /// <param name="entryWeight">Weight of the selected power-up entry.</param>
    /// <returns>True when a candidate is rolled; otherwise false.</returns>
    private static bool TryRollCatalogFromTier(in PlayerPowerUpTierDefinitionElement tierDefinition,
                                               DynamicBuffer<PlayerPowerUpTierEntryElement> tierEntries,
                                               DynamicBuffer<PlayerPowerUpUnlockCatalogElement> unlockCatalog,
                                               HashSet<int> rolledCatalogIndices,
                                               out int catalogIndex,
                                               out float entryWeight)
    {
        catalogIndex = -1;
        entryWeight = 0f;
        List<int> candidateCatalogIndices = new List<int>();
        List<float> candidateWeights = new List<float>();
        int startIndex = mathMax(0, tierDefinition.EntryStartIndex);
        int endIndex = mathMin(tierEntries.Length, startIndex + mathMax(0, tierDefinition.EntryCount));

        // Collect eligible entries from this tier.
        for (int tierEntryIndex = startIndex; tierEntryIndex < endIndex; tierEntryIndex++)
        {
            PlayerPowerUpTierEntryElement tierEntry = tierEntries[tierEntryIndex];
            float tierEntryWeight = mathMax(0f, tierEntry.SelectionWeight);

            if (tierEntryWeight <= 0f)
                continue;

            int candidateCatalogIndex = tierEntry.CatalogIndex;

            if (candidateCatalogIndex < 0 || candidateCatalogIndex >= unlockCatalog.Length)
                continue;

            if (rolledCatalogIndices.Contains(candidateCatalogIndex))
                continue;

            if (unlockCatalog[candidateCatalogIndex].IsUnlocked != 0)
                continue;

            candidateCatalogIndices.Add(candidateCatalogIndex);
            candidateWeights.Add(tierEntryWeight);
        }

        int selectedCandidateIndex = RollWeightedIndex(candidateWeights);

        if (selectedCandidateIndex < 0)
            return false;

        catalogIndex = candidateCatalogIndices[selectedCandidateIndex];
        entryWeight = candidateWeights[selectedCandidateIndex];
        return true;
    }

    /// <summary>
    /// Resolves one weighted random index.
    /// </summary>
    /// <param name="weights">Weight list where each element maps to one candidate index.</param>
    /// <returns>Rolled candidate index, or -1 when no valid weight exists.</returns>
    private static int RollWeightedIndex(List<float> weights)
    {
        if (weights == null || weights.Count <= 0)
            return -1;

        float totalWeight = 0f;

        for (int weightIndex = 0; weightIndex < weights.Count; weightIndex++)
            totalWeight += mathMax(0f, weights[weightIndex]);

        if (totalWeight <= 0f)
            return -1;

        float roll = UnityEngine.Random.value * totalWeight;
        float cumulativeWeight = 0f;

        for (int weightIndex = 0; weightIndex < weights.Count; weightIndex++)
        {
            cumulativeWeight += mathMax(0f, weights[weightIndex]);

            if (roll > cumulativeWeight)
                continue;

            return weightIndex;
        }

        return weights.Count - 1;
    }

    private static int mathMax(int left, int right)
    {
        return left > right ? left : right;
    }

    private static int mathMin(int left, int right)
    {
        return left < right ? left : right;
    }

    private static float mathMax(float left, float right)
    {
        return left > right ? left : right;
    }
    #endregion

    #endregion
}
