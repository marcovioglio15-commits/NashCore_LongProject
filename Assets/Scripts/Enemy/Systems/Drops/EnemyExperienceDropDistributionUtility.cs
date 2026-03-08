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
    private const float CompatibilityTolerance = 0.01f;
    private const int CompatibilityQuantizationScale = 100;
    private const int MaxCompatibilitySearchSteps = 262144;
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

    /// <summary>
    /// Resolves the nearest compatible preview range endpoints contained inside a requested range.
    /// </summary>
    /// <param name="definitionExperienceValues">Preview definition values collected from serialized properties.</param>
    /// <param name="requestedMinimumTotal">Requested minimum total experience.</param>
    /// <param name="requestedMaximumTotal">Requested maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="resolvedMinimumTotal">Resolved compatible minimum total inside the requested range.</param>
    /// <param name="resolvedMaximumTotal">Resolved compatible maximum total inside the requested range.</param>
    /// <returns>True when at least one compatible value exists in the requested range.</returns>
    public static bool TryResolveCompatiblePreviewRange(IReadOnlyList<float> definitionExperienceValues,
                                                        float requestedMinimumTotal,
                                                        float requestedMaximumTotal,
                                                        float distribution,
                                                        out float resolvedMinimumTotal,
                                                        out float resolvedMaximumTotal)
    {
        resolvedMinimumTotal = 0f;
        resolvedMaximumTotal = 0f;

        if (definitionExperienceValues == null || definitionExperienceValues.Count <= 0)
            return false;

        float sanitizedMinimumTotal = math.max(0f, requestedMinimumTotal);
        float sanitizedMaximumTotal = math.max(sanitizedMinimumTotal, requestedMaximumTotal);

        if (sanitizedMaximumTotal <= PrecisionEpsilon)
            return false;

        int quantizationStepUnits = ResolvePreviewQuantizationStepUnits(definitionExperienceValues);

        if (quantizationStepUnits <= 0)
            return false;

        int minimumUnits = ConvertMinimumTotalToUnits(sanitizedMinimumTotal, quantizationStepUnits);
        int maximumUnits = ConvertMaximumTotalToUnits(sanitizedMaximumTotal, quantizationStepUnits);

        if (maximumUnits < minimumUnits)
            return false;

        int compatibleMinimumUnits;

        if (TryFindFirstCompatiblePreviewTotalUnits(definitionExperienceValues,
                                                    minimumUnits,
                                                    maximumUnits,
                                                    quantizationStepUnits,
                                                    distribution,
                                                    out compatibleMinimumUnits) == false)
            return false;

        int compatibleMaximumUnits;

        if (TryFindLastCompatiblePreviewTotalUnits(definitionExperienceValues,
                                                   compatibleMinimumUnits,
                                                   maximumUnits,
                                                   quantizationStepUnits,
                                                   distribution,
                                                   out compatibleMaximumUnits) == false)
            return false;

        resolvedMinimumTotal = ConvertUnitsToTotal(compatibleMinimumUnits, quantizationStepUnits);
        resolvedMaximumTotal = ConvertUnitsToTotal(compatibleMaximumUnits, quantizationStepUnits);
        return true;
    }

    /// <summary>
    /// Picks one random compatible total experience value inside the requested runtime range.
    /// </summary>
    /// <param name="definitions">Available runtime drop definitions.</param>
    /// <param name="minimumTotal">Requested minimum total experience.</param>
    /// <param name="maximumTotal">Requested maximum total experience.</param>
    /// <param name="distribution">Distribution bias where 0 favors low values and 1 favors high values.</param>
    /// <param name="randomSeed">Deterministic random seed used for total-value selection.</param>
    /// <param name="resolvedTotal">Resolved compatible random total when successful.</param>
    /// <returns>True when a compatible random total is resolved, otherwise false.</returns>
    public static bool TryResolveRandomCompatibleTotal(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                       float minimumTotal,
                                                       float maximumTotal,
                                                       float distribution,
                                                       uint randomSeed,
                                                       out float resolvedTotal)
    {
        resolvedTotal = 0f;

        if (definitions.Length <= 0)
            return false;

        float sanitizedMinimumTotal = math.max(0f, minimumTotal);
        float sanitizedMaximumTotal = math.max(sanitizedMinimumTotal, maximumTotal);

        if (sanitizedMaximumTotal <= PrecisionEpsilon)
            return false;

        int quantizationStepUnits = ResolveRuntimeQuantizationStepUnits(definitions);

        if (quantizationStepUnits <= 0)
            return false;

        int minimumUnits = ConvertMinimumTotalToUnits(sanitizedMinimumTotal, quantizationStepUnits);
        int maximumUnits = ConvertMaximumTotalToUnits(sanitizedMaximumTotal, quantizationStepUnits);

        if (maximumUnits < minimumUnits)
            return false;

        uint sanitizedSeed = randomSeed;

        if (sanitizedSeed == 0u)
            sanitizedSeed = 1u;

        Unity.Mathematics.Random random = new Unity.Mathematics.Random(sanitizedSeed);
        int targetUnits = random.NextInt(minimumUnits, maximumUnits + 1);
        int compatibleUnits;

        if (TryFindNearestCompatibleRuntimeTotalUnits(definitions,
                                                      minimumUnits,
                                                      maximumUnits,
                                                      targetUnits,
                                                      quantizationStepUnits,
                                                      distribution,
                                                      out compatibleUnits) == false)
            return false;

        resolvedTotal = ConvertUnitsToTotal(compatibleUnits, quantizationStepUnits);
        return true;
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

    private static bool TryFindNearestCompatibleRuntimeTotalUnits(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                                  int minimumUnits,
                                                                  int maximumUnits,
                                                                  int targetUnits,
                                                                  int quantizationStepUnits,
                                                                  float distribution,
                                                                  out int compatibleUnits)
    {
        compatibleUnits = -1;

        if (minimumUnits > maximumUnits)
            return false;

        int clampedTargetUnits = math.clamp(targetUnits, minimumUnits, maximumUnits);
        int totalSpan = maximumUnits - minimumUnits;
        int maximumSearchSteps = math.min(totalSpan, MaxCompatibilitySearchSteps);
        float clampedDistribution = math.clamp(distribution, 0f, 1f);
        bool preferHigherTotals = clampedDistribution >= 0.5f;

        for (int offset = 0; offset <= maximumSearchSteps; offset++)
        {
            int lowerCandidateUnits = clampedTargetUnits - offset;
            int upperCandidateUnits = clampedTargetUnits + offset;

            if (preferHigherTotals)
            {
                if (upperCandidateUnits <= maximumUnits)
                {
                    float upperCandidateTotal = ConvertUnitsToTotal(upperCandidateUnits, quantizationStepUnits);

                    if (IsRuntimeTotalCompatible(definitions, upperCandidateTotal, distribution))
                    {
                        compatibleUnits = upperCandidateUnits;
                        return true;
                    }
                }

                if (offset > 0 && lowerCandidateUnits >= minimumUnits)
                {
                    float lowerCandidateTotal = ConvertUnitsToTotal(lowerCandidateUnits, quantizationStepUnits);

                    if (IsRuntimeTotalCompatible(definitions, lowerCandidateTotal, distribution))
                    {
                        compatibleUnits = lowerCandidateUnits;
                        return true;
                    }
                }

                continue;
            }

            if (lowerCandidateUnits >= minimumUnits)
            {
                float lowerCandidateTotal = ConvertUnitsToTotal(lowerCandidateUnits, quantizationStepUnits);

                if (IsRuntimeTotalCompatible(definitions, lowerCandidateTotal, distribution))
                {
                    compatibleUnits = lowerCandidateUnits;
                    return true;
                }
            }

            if (offset > 0 && upperCandidateUnits <= maximumUnits)
            {
                float upperCandidateTotal = ConvertUnitsToTotal(upperCandidateUnits, quantizationStepUnits);

                if (IsRuntimeTotalCompatible(definitions, upperCandidateTotal, distribution))
                {
                    compatibleUnits = upperCandidateUnits;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindFirstCompatiblePreviewTotalUnits(IReadOnlyList<float> definitionExperienceValues,
                                                                int minimumUnits,
                                                                int maximumUnits,
                                                                int quantizationStepUnits,
                                                                float distribution,
                                                                out int compatibleUnits)
    {
        compatibleUnits = -1;

        if (minimumUnits > maximumUnits)
            return false;

        int totalSpan = maximumUnits - minimumUnits;
        int maximumSearchSteps = math.min(totalSpan, MaxCompatibilitySearchSteps);

        for (int offset = 0; offset <= maximumSearchSteps; offset++)
        {
            int candidateUnits = minimumUnits + offset;
            float candidateTotal = ConvertUnitsToTotal(candidateUnits, quantizationStepUnits);

            if (IsPreviewTotalCompatible(definitionExperienceValues, candidateTotal, distribution) == false)
                continue;

            compatibleUnits = candidateUnits;
            return true;
        }

        return false;
    }

    private static bool TryFindLastCompatiblePreviewTotalUnits(IReadOnlyList<float> definitionExperienceValues,
                                                               int minimumUnits,
                                                               int maximumUnits,
                                                               int quantizationStepUnits,
                                                               float distribution,
                                                               out int compatibleUnits)
    {
        compatibleUnits = -1;

        if (minimumUnits > maximumUnits)
            return false;

        int totalSpan = maximumUnits - minimumUnits;
        int maximumSearchSteps = math.min(totalSpan, MaxCompatibilitySearchSteps);

        for (int offset = 0; offset <= maximumSearchSteps; offset++)
        {
            int candidateUnits = maximumUnits - offset;
            float candidateTotal = ConvertUnitsToTotal(candidateUnits, quantizationStepUnits);

            if (IsPreviewTotalCompatible(definitionExperienceValues, candidateTotal, distribution) == false)
                continue;

            compatibleUnits = candidateUnits;
            return true;
        }

        return false;
    }

    private static bool IsRuntimeTotalCompatible(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions,
                                                 float totalExperienceDrop,
                                                 float distribution)
    {
        if (totalExperienceDrop <= PrecisionEpsilon)
            return false;

        float deliveredExperience;
        float absoluteError;
        int estimatedDropCount = EstimateDropsPerDeath(definitions,
                                                       totalExperienceDrop,
                                                       distribution,
                                                       out deliveredExperience,
                                                       out absoluteError);

        if (estimatedDropCount <= 0)
            return false;

        return absoluteError <= CompatibilityTolerance;
    }

    private static bool IsPreviewTotalCompatible(IReadOnlyList<float> definitionExperienceValues,
                                                 float totalExperienceDrop,
                                                 float distribution)
    {
        if (totalExperienceDrop <= PrecisionEpsilon)
            return false;

        float deliveredExperience;
        float absoluteError;
        int estimatedDropCount = EstimateDropsForPreview(definitionExperienceValues,
                                                         totalExperienceDrop,
                                                         distribution,
                                                         out deliveredExperience,
                                                         out absoluteError);

        if (estimatedDropCount <= 0)
            return false;

        return absoluteError <= CompatibilityTolerance;
    }

    private static int ResolveRuntimeQuantizationStepUnits(DynamicBuffer<EnemyExperienceDropDefinitionElement> definitions)
    {
        int quantizationStepUnits = 0;

        for (int index = 0; index < definitions.Length; index++)
        {
            EnemyExperienceDropDefinitionElement definition = definitions[index];

            if (definition.PrefabEntity == Entity.Null)
                continue;

            float amount = definition.ExperienceAmount;

            if (amount <= PrecisionEpsilon)
                continue;

            int amountUnits = ConvertExperienceAmountToUnits(amount);

            if (amountUnits <= 0)
                continue;

            if (quantizationStepUnits <= 0)
            {
                quantizationStepUnits = amountUnits;
                continue;
            }

            quantizationStepUnits = ComputeGreatestCommonDivisor(quantizationStepUnits, amountUnits);

            if (quantizationStepUnits <= 1)
                return 1;
        }

        return quantizationStepUnits;
    }

    private static int ResolvePreviewQuantizationStepUnits(IReadOnlyList<float> definitionExperienceValues)
    {
        int quantizationStepUnits = 0;

        if (definitionExperienceValues == null)
            return quantizationStepUnits;

        for (int index = 0; index < definitionExperienceValues.Count; index++)
        {
            float amount = definitionExperienceValues[index];

            if (amount <= PrecisionEpsilon)
                continue;

            int amountUnits = ConvertExperienceAmountToUnits(amount);

            if (amountUnits <= 0)
                continue;

            if (quantizationStepUnits <= 0)
            {
                quantizationStepUnits = amountUnits;
                continue;
            }

            quantizationStepUnits = ComputeGreatestCommonDivisor(quantizationStepUnits, amountUnits);

            if (quantizationStepUnits <= 1)
                return 1;
        }

        return quantizationStepUnits;
    }

    private static int ConvertExperienceAmountToUnits(float amount)
    {
        float clampedAmount = math.max(0f, amount);
        int amountUnits = (int)math.round(clampedAmount * CompatibilityQuantizationScale);
        return math.max(1, amountUnits);
    }

    private static int ConvertMinimumTotalToUnits(float totalExperienceDrop, int quantizationStepUnits)
    {
        float clampedTotalExperienceDrop = math.max(0f, totalExperienceDrop);
        int scaledTotalUnits = (int)math.ceil(clampedTotalExperienceDrop * CompatibilityQuantizationScale - PrecisionEpsilon);

        if (scaledTotalUnits <= 0)
            return 0;

        return (scaledTotalUnits + quantizationStepUnits - 1) / quantizationStepUnits;
    }

    private static int ConvertMaximumTotalToUnits(float totalExperienceDrop, int quantizationStepUnits)
    {
        float clampedTotalExperienceDrop = math.max(0f, totalExperienceDrop);
        int scaledTotalUnits = (int)math.floor(clampedTotalExperienceDrop * CompatibilityQuantizationScale + PrecisionEpsilon);

        if (scaledTotalUnits < 0)
            return -1;

        return scaledTotalUnits / quantizationStepUnits;
    }

    private static float ConvertUnitsToTotal(int unitCount, int quantizationStepUnits)
    {
        if (unitCount <= 0 || quantizationStepUnits <= 0)
            return 0f;

        int scaledTotalUnits = unitCount * quantizationStepUnits;
        return scaledTotalUnits / (float)CompatibilityQuantizationScale;
    }

    private static int ComputeGreatestCommonDivisor(int leftValue, int rightValue)
    {
        int left = math.abs(leftValue);
        int right = math.abs(rightValue);

        if (left == 0)
            return right;

        if (right == 0)
            return left;

        while (right != 0)
        {
            int remainder = left % right;
            left = right;
            right = remainder;
        }

        return math.max(1, left);
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
