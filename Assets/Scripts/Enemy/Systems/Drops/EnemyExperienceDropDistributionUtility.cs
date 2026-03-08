using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Provides reusable distribution helpers for experience drop planning and estimation.
/// </summary>
public static class EnemyExperienceDropDistributionUtility
{
    #region Constants
    private const float PrecisionEpsilon = 0.0001f;
    private const int MaxPlanSteps = 4096;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the next runtime drop-definition choice for a remaining experience budget.
    /// </summary>
    /// <param name="definitions">Available runtime drop definitions.</param>
    /// <param name="remainingExperience">Remaining experience budget to distribute.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="definitionIndex">Resolved definition index when successful.</param>
    /// <param name="definitionExperienceAmount">Resolved definition experience amount when successful.</param>
    /// <param name="fitsRemaining">True when selected amount is lower or equal to remaining budget.</param>
    /// <returns>True when a valid definition is selected, otherwise false.</returns>
    public static bool TryResolveNextDefinition(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                float remainingExperience,
                                                float distribution,
                                                out int definitionIndex,
                                                out float definitionExperienceAmount,
                                                out bool fitsRemaining)
    {
        definitionIndex = -1;
        definitionExperienceAmount = 0f;
        fitsRemaining = false;

        if (definitions.Length <= 0)
            return false;

        float minimumValue;
        float maximumValue;

        if (TryResolveRuntimeBounds(definitions, out minimumValue, out maximumValue) == false)
            return false;

        float clampedDistribution = math.clamp(distribution, 0f, 1f);
        float targetValue = math.lerp(minimumValue, maximumValue, clampedDistribution);
        int bestIndex = -1;
        float bestAmount = 0f;
        float bestScore = float.MinValue;

        for (int index = 0; index < definitions.Length; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];
            float amount = definition.ExperienceAmount;

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (amount <= 0f)
                continue;

            if (amount > remainingExperience + PrecisionEpsilon)
                continue;

            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution) == false)
                continue;

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
        }

        if (bestIndex >= 0)
        {
            definitionIndex = bestIndex;
            definitionExperienceAmount = bestAmount;
            fitsRemaining = true;
            return true;
        }

        float bestAbsoluteDifference = float.MaxValue;

        for (int index = 0; index < definitions.Length; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];
            float amount = definition.ExperienceAmount;

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (amount <= 0f)
                continue;

            float absoluteDifference = math.abs(amount - remainingExperience);
            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (absoluteDifference > bestAbsoluteDifference + PrecisionEpsilon)
                continue;

            if (math.abs(absoluteDifference - bestAbsoluteDifference) <= PrecisionEpsilon &&
                IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution) == false)
                continue;

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
            bestAbsoluteDifference = absoluteDifference;
        }

        if (bestIndex < 0)
            return false;

        definitionIndex = bestIndex;
        definitionExperienceAmount = bestAmount;
        fitsRemaining = bestAmount <= remainingExperience + PrecisionEpsilon;
        return true;
    }

    /// <summary>
    /// Estimates drop count and delivery error for runtime definitions using the same distribution logic used at spawn.
    /// </summary>
    /// <param name="definitions">Available runtime drop definitions.</param>
    /// <param name="totalExperienceDrop">Total experience budget to distribute.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="deliveredExperience">Estimated delivered experience by planned drops.</param>
    /// <param name="absoluteError">Absolute difference between requested and delivered experience.</param>
    /// <returns>Estimated planned drop count.</returns>
    public static int EstimateDropsPerDeath(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                            float totalExperienceDrop,
                                            float distribution,
                                            out float deliveredExperience,
                                            out float absoluteError)
    {
        deliveredExperience = 0f;
        absoluteError = math.max(0f, totalExperienceDrop);

        if (definitions.Length <= 0)
            return 0;

        float remainingExperience = math.max(0f, totalExperienceDrop);

        if (remainingExperience <= PrecisionEpsilon)
        {
            absoluteError = 0f;
            return 0;
        }

        int dropCount = 0;

        for (int stepIndex = 0; stepIndex < MaxPlanSteps; stepIndex++)
        {
            if (remainingExperience <= PrecisionEpsilon)
                break;

            int definitionIndex;
            float definitionAmount;
            bool fitsRemaining;

            if (TryResolveNextDefinition(definitions,
                                         remainingExperience,
                                         distribution,
                                         out definitionIndex,
                                         out definitionAmount,
                                         out fitsRemaining) == false)
                break;

            if (definitionIndex < 0 || definitionAmount <= 0f)
                break;

            deliveredExperience += definitionAmount;
            dropCount += 1;

            if (fitsRemaining == false)
                break;

            remainingExperience -= definitionAmount;
        }

        absoluteError = math.abs(math.max(0f, totalExperienceDrop) - deliveredExperience);
        return dropCount;
    }

    /// <summary>
    /// Estimates drop count and delivery error for editor preview values.
    /// </summary>
    /// <param name="definitionExperienceValues">Preview definition values collected from serialized properties.</param>
    /// <param name="totalExperienceDrop">Total experience budget to distribute.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="deliveredExperience">Estimated delivered experience by planned drops.</param>
    /// <param name="absoluteError">Absolute difference between requested and delivered experience.</param>
    /// <returns>Estimated planned drop count.</returns>
    public static int EstimateDropsForPreview(IReadOnlyList<float> definitionExperienceValues,
                                              float totalExperienceDrop,
                                              float distribution,
                                              out float deliveredExperience,
                                              out float absoluteError)
    {
        deliveredExperience = 0f;
        absoluteError = math.max(0f, totalExperienceDrop);

        if (definitionExperienceValues == null || definitionExperienceValues.Count <= 0)
            return 0;

        float remainingExperience = math.max(0f, totalExperienceDrop);

        if (remainingExperience <= PrecisionEpsilon)
        {
            absoluteError = 0f;
            return 0;
        }

        int dropCount = 0;

        for (int stepIndex = 0; stepIndex < MaxPlanSteps; stepIndex++)
        {
            if (remainingExperience <= PrecisionEpsilon)
                break;

            int definitionIndex;
            float definitionAmount;
            bool fitsRemaining;

            if (TryResolveNextPreviewDefinition(definitionExperienceValues,
                                                remainingExperience,
                                                distribution,
                                                out definitionIndex,
                                                out definitionAmount,
                                                out fitsRemaining) == false)
                break;

            if (definitionIndex < 0 || definitionAmount <= 0f)
                break;

            deliveredExperience += definitionAmount;
            dropCount += 1;

            if (fitsRemaining == false)
                break;

            remainingExperience -= definitionAmount;
        }

        absoluteError = math.abs(math.max(0f, totalExperienceDrop) - deliveredExperience);
        return dropCount;
    }
    #endregion

    #region Selection Helpers
    private static bool TryResolveRuntimeBounds(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                out float minimumValue,
                                                out float maximumValue)
    {
        minimumValue = float.MaxValue;
        maximumValue = 0f;
        bool hasValidDefinition = false;

        for (int index = 0; index < definitions.Length; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];
            float amount = definition.ExperienceAmount;

            if (definition.PrefabEntity == Entity.Null)
                continue;

            if (amount <= 0f)
                continue;

            hasValidDefinition = true;

            if (amount < minimumValue)
                minimumValue = amount;

            if (amount > maximumValue)
                maximumValue = amount;
        }

        if (hasValidDefinition == false)
        {
            minimumValue = 0f;
            maximumValue = 0f;
            return false;
        }

        return true;
    }

    private static bool TryResolvePreviewBounds(IReadOnlyList<float> definitionExperienceValues,
                                                out float minimumValue,
                                                out float maximumValue)
    {
        minimumValue = float.MaxValue;
        maximumValue = 0f;
        bool hasValidDefinition = false;

        for (int index = 0; index < definitionExperienceValues.Count; index++)
        {
            float amount = definitionExperienceValues[index];

            if (amount <= 0f)
                continue;

            hasValidDefinition = true;

            if (amount < minimumValue)
                minimumValue = amount;

            if (amount > maximumValue)
                maximumValue = amount;
        }

        if (hasValidDefinition == false)
        {
            minimumValue = 0f;
            maximumValue = 0f;
            return false;
        }

        return true;
    }

    private static bool TryResolveNextPreviewDefinition(IReadOnlyList<float> definitionExperienceValues,
                                                        float remainingExperience,
                                                        float distribution,
                                                        out int definitionIndex,
                                                        out float definitionExperienceAmount,
                                                        out bool fitsRemaining)
    {
        definitionIndex = -1;
        definitionExperienceAmount = 0f;
        fitsRemaining = false;

        if (definitionExperienceValues == null || definitionExperienceValues.Count <= 0)
            return false;

        float minimumValue;
        float maximumValue;

        if (TryResolvePreviewBounds(definitionExperienceValues, out minimumValue, out maximumValue) == false)
            return false;

        float clampedDistribution = math.clamp(distribution, 0f, 1f);
        float targetValue = math.lerp(minimumValue, maximumValue, clampedDistribution);
        int bestIndex = -1;
        float bestAmount = 0f;
        float bestScore = float.MinValue;

        for (int index = 0; index < definitionExperienceValues.Count; index++)
        {
            float amount = definitionExperienceValues[index];

            if (amount <= 0f)
                continue;

            if (amount > remainingExperience + PrecisionEpsilon)
                continue;

            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution) == false)
                continue;

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
        }

        if (bestIndex >= 0)
        {
            definitionIndex = bestIndex;
            definitionExperienceAmount = bestAmount;
            fitsRemaining = true;
            return true;
        }

        float bestAbsoluteDifference = float.MaxValue;

        for (int index = 0; index < definitionExperienceValues.Count; index++)
        {
            float amount = definitionExperienceValues[index];

            if (amount <= 0f)
                continue;

            float absoluteDifference = math.abs(amount - remainingExperience);
            float score = ComputeSelectionScore(amount, minimumValue, maximumValue, targetValue, clampedDistribution);

            if (absoluteDifference > bestAbsoluteDifference + PrecisionEpsilon)
                continue;

            if (math.abs(absoluteDifference - bestAbsoluteDifference) <= PrecisionEpsilon &&
                IsBetterSelection(score, amount, bestScore, bestAmount, clampedDistribution) == false)
                continue;

            bestIndex = index;
            bestAmount = amount;
            bestScore = score;
            bestAbsoluteDifference = absoluteDifference;
        }

        if (bestIndex < 0)
            return false;

        definitionIndex = bestIndex;
        definitionExperienceAmount = bestAmount;
        fitsRemaining = bestAmount <= remainingExperience + PrecisionEpsilon;
        return true;
    }

    private static float ComputeSelectionScore(float value,
                                               float minimumValue,
                                               float maximumValue,
                                               float targetValue,
                                               float clampedDistribution)
    {
        float valueRange = math.max(PrecisionEpsilon, maximumValue - minimumValue);
        float normalizedValue = math.clamp((value - minimumValue) / valueRange, 0f, 1f);
        float directionalScore = math.lerp(1f - normalizedValue, normalizedValue, clampedDistribution);
        float proximityScore = 1f - math.clamp(math.abs(value - targetValue) / valueRange, 0f, 1f);
        return directionalScore * 0.75f + proximityScore * 0.25f;
    }

    private static bool IsBetterSelection(float candidateScore,
                                          float candidateAmount,
                                          float currentScore,
                                          float currentAmount,
                                          float clampedDistribution)
    {
        if (candidateScore > currentScore + PrecisionEpsilon)
            return true;

        if (math.abs(candidateScore - currentScore) > PrecisionEpsilon)
            return false;

        if (clampedDistribution >= 0.5f)
            return candidateAmount > currentAmount + PrecisionEpsilon;

        return candidateAmount < currentAmount - PrecisionEpsilon || currentAmount <= 0f;
    }
    #endregion

    #endregion
}
