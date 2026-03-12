using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Builds editor warnings for milestone definitions that can overlap within the same game phase.
/// </summary>
public static class PlayerMilestoneOverlapWarningUtility
{
    #region Constants
    private const int MaxVisibleWarnings = 4;
    #endregion

    #region Nested Types
    private readonly struct MilestonePattern
    {
        public readonly int ListIndex;
        public readonly int MilestoneLevel;
        public readonly bool Recurring;
        public readonly int RecurrenceIntervalLevels;

        public MilestonePattern(int listIndexValue,
                                int milestoneLevelValue,
                                bool recurringValue,
                                int recurrenceIntervalLevelsValue)
        {
            ListIndex = listIndexValue;
            MilestoneLevel = milestoneLevelValue;
            Recurring = recurringValue;
            RecurrenceIntervalLevels = recurrenceIntervalLevelsValue > 0 ? recurrenceIntervalLevelsValue : 1;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds overlap warnings for one serialized game-phase property.
    /// </summary>
    /// <param name="warningsRoot">UI container that receives the warning help boxes.</param>
    /// <param name="phaseProperty">Serialized game-phase property currently being drawn.</param>
    public static void RefreshWarnings(VisualElement warningsRoot, SerializedProperty phaseProperty)
    {
        if (warningsRoot == null)
            return;

        warningsRoot.Clear();

        if (phaseProperty == null)
            return;

        List<string> overlapMessages = BuildOverlapMessages(phaseProperty);

        if (overlapMessages.Count <= 0)
            return;

        for (int messageIndex = 0; messageIndex < overlapMessages.Count && messageIndex < MaxVisibleWarnings; messageIndex++)
        {
            HelpBox warningBox = new HelpBox(overlapMessages[messageIndex], HelpBoxMessageType.Warning);
            warningsRoot.Add(warningBox);
        }

        if (overlapMessages.Count <= MaxVisibleWarnings)
            return;

        int hiddenWarningCount = overlapMessages.Count - MaxVisibleWarnings;
        HelpBox summaryBox = new HelpBox(string.Format(CultureInfo.InvariantCulture,
                                                       "Additional overlap warnings hidden: {0}. Runtime always uses the first valid milestone in list order.",
                                                       hiddenWarningCount),
                                         HelpBoxMessageType.Warning);
        warningsRoot.Add(summaryBox);
    }
    #endregion

    #region Private Methods
    private static List<string> BuildOverlapMessages(SerializedProperty phaseProperty)
    {
        List<string> overlapMessages = new List<string>();
        SerializedProperty milestonesProperty = phaseProperty.FindPropertyRelative("milestones");

        if (milestonesProperty == null || !milestonesProperty.isArray || milestonesProperty.arraySize <= 1)
            return overlapMessages;

        List<MilestonePattern> milestonePatterns = new List<MilestonePattern>(milestonesProperty.arraySize);

        for (int milestoneIndex = 0; milestoneIndex < milestonesProperty.arraySize; milestoneIndex++)
        {
            SerializedProperty milestoneProperty = milestonesProperty.GetArrayElementAtIndex(milestoneIndex);

            if (milestoneProperty == null)
                continue;

            milestonePatterns.Add(ReadPattern(milestoneProperty, milestoneIndex));
        }

        if (milestonePatterns.Count <= 1)
            return overlapMessages;

        int phaseStartLevel = ReadIntProperty(phaseProperty.FindPropertyRelative("startsAtLevel"), 0);
        int phaseEndExclusive = ResolvePhaseEndExclusive(phaseProperty);

        for (int leftIndex = 0; leftIndex < milestonePatterns.Count; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < milestonePatterns.Count; rightIndex++)
            {
                MilestonePattern leftPattern = milestonePatterns[leftIndex];
                MilestonePattern rightPattern = milestonePatterns[rightIndex];

                if (!TryFindFirstOverlapLevel(in leftPattern,
                                              in rightPattern,
                                              phaseStartLevel,
                                              phaseEndExclusive,
                                              out int overlapLevel))
                {
                    continue;
                }

                overlapMessages.Add(string.Format(CultureInfo.InvariantCulture,
                                                  "Milestones #{0} and #{1} both trigger at level {2}. Runtime will keep the first valid list entry.",
                                                  leftPattern.ListIndex + 1,
                                                  rightPattern.ListIndex + 1,
                                                  overlapLevel));
            }
        }

        return overlapMessages;
    }

    private static MilestonePattern ReadPattern(SerializedProperty milestoneProperty, int listIndex)
    {
        int milestoneLevel = ReadIntProperty(milestoneProperty.FindPropertyRelative("milestoneLevel"), 0);
        bool recurring = ReadBoolProperty(milestoneProperty.FindPropertyRelative("recurring"));
        int recurrenceIntervalLevels = ReadIntProperty(milestoneProperty.FindPropertyRelative("recurrenceIntervalLevels"), 1);
        return new MilestonePattern(listIndex, milestoneLevel, recurring, recurrenceIntervalLevels);
    }

    private static int ResolvePhaseEndExclusive(SerializedProperty phaseProperty)
    {
        if (phaseProperty == null || phaseProperty.serializedObject == null)
            return int.MaxValue;

        SerializedProperty allPhasesProperty = phaseProperty.serializedObject.FindProperty("gamePhasesDefinition");

        if (allPhasesProperty == null || !allPhasesProperty.isArray)
            return int.MaxValue;

        int phaseIndex = ExtractArrayIndex(phaseProperty.propertyPath);

        if (phaseIndex < 0)
            return int.MaxValue;

        int nextPhaseIndex = phaseIndex + 1;

        if (nextPhaseIndex >= allPhasesProperty.arraySize)
            return int.MaxValue;

        SerializedProperty nextPhaseProperty = allPhasesProperty.GetArrayElementAtIndex(nextPhaseIndex);

        if (nextPhaseProperty == null)
            return int.MaxValue;

        SerializedProperty nextPhaseStartProperty = nextPhaseProperty.FindPropertyRelative("startsAtLevel");
        return ReadIntProperty(nextPhaseStartProperty, int.MaxValue);
    }

    private static int ExtractArrayIndex(string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return -1;

        int markerIndex = propertyPath.LastIndexOf("Array.data[", StringComparison.Ordinal);

        if (markerIndex < 0)
            return -1;

        int indexStart = markerIndex + "Array.data[".Length;
        int indexEnd = propertyPath.IndexOf(']', indexStart);

        if (indexEnd <= indexStart)
            return -1;

        string indexText = propertyPath.Substring(indexStart, indexEnd - indexStart);
        return int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int arrayIndex) ? arrayIndex : -1;
    }

    private static int ReadIntProperty(SerializedProperty property, int fallbackValue)
    {
        return property != null ? property.intValue : fallbackValue;
    }

    private static bool ReadBoolProperty(SerializedProperty property)
    {
        return property != null && property.boolValue;
    }

    private static bool TryFindFirstOverlapLevel(in MilestonePattern leftPattern,
                                                 in MilestonePattern rightPattern,
                                                 int phaseStartLevel,
                                                 int phaseEndExclusive,
                                                 out int overlapLevel)
    {
        overlapLevel = 0;

        if (!leftPattern.Recurring && !rightPattern.Recurring)
        {
            if (leftPattern.MilestoneLevel != rightPattern.MilestoneLevel)
                return false;

            if (!IsWithinPhase(leftPattern.MilestoneLevel, phaseStartLevel, phaseEndExclusive))
                return false;

            overlapLevel = leftPattern.MilestoneLevel;
            return true;
        }

        if (!leftPattern.Recurring)
            return TryFindSingleLevelOverlap(leftPattern.MilestoneLevel,
                                             in rightPattern,
                                             phaseStartLevel,
                                             phaseEndExclusive,
                                             out overlapLevel);

        if (!rightPattern.Recurring)
            return TryFindSingleLevelOverlap(rightPattern.MilestoneLevel,
                                             in leftPattern,
                                             phaseStartLevel,
                                             phaseEndExclusive,
                                             out overlapLevel);

        return TryFindRecurringOverlap(in leftPattern,
                                       in rightPattern,
                                       phaseStartLevel,
                                       phaseEndExclusive,
                                       out overlapLevel);
    }

    private static bool TryFindSingleLevelOverlap(int singleLevel,
                                                  in MilestonePattern recurringPattern,
                                                  int phaseStartLevel,
                                                  int phaseEndExclusive,
                                                  out int overlapLevel)
    {
        overlapLevel = 0;

        if (!IsWithinPhase(singleLevel, phaseStartLevel, phaseEndExclusive))
            return false;

        if (singleLevel < recurringPattern.MilestoneLevel)
            return false;

        int recurrenceInterval = recurringPattern.RecurrenceIntervalLevels > 0 ? recurringPattern.RecurrenceIntervalLevels : 1;

        if ((singleLevel - recurringPattern.MilestoneLevel) % recurrenceInterval != 0)
            return false;

        overlapLevel = singleLevel;
        return true;
    }

    private static bool TryFindRecurringOverlap(in MilestonePattern leftPattern,
                                                in MilestonePattern rightPattern,
                                                int phaseStartLevel,
                                                int phaseEndExclusive,
                                                out int overlapLevel)
    {
        overlapLevel = 0;
        long leftLevel = leftPattern.MilestoneLevel;
        long rightLevel = rightPattern.MilestoneLevel;
        long leftInterval = leftPattern.RecurrenceIntervalLevels > 0 ? leftPattern.RecurrenceIntervalLevels : 1;
        long rightInterval = rightPattern.RecurrenceIntervalLevels > 0 ? rightPattern.RecurrenceIntervalLevels : 1;
        long greatestCommonDivisor = GreatestCommonDivisor(leftInterval, rightInterval);
        long levelDifference = rightLevel - leftLevel;

        if (levelDifference % greatestCommonDivisor != 0)
            return false;

        long reducedLeftInterval = leftInterval / greatestCommonDivisor;
        long reducedRightInterval = rightInterval / greatestCommonDivisor;
        long reducedDifference = levelDifference / greatestCommonDivisor;
        long modularInverse = ResolveModularInverse(NormalizeModulo(reducedLeftInterval, reducedRightInterval), reducedRightInterval);
        long solutionMultiplier = NormalizeModulo(reducedDifference * modularInverse, reducedRightInterval);
        long firstOverlapLevel = leftLevel + (leftInterval * solutionMultiplier);
        long leastCommonMultiple = leftInterval * reducedRightInterval;
        long minimumRequiredLevel = phaseStartLevel;

        if (minimumRequiredLevel < leftLevel)
            minimumRequiredLevel = leftLevel;

        if (minimumRequiredLevel < rightLevel)
            minimumRequiredLevel = rightLevel;

        if (firstOverlapLevel < minimumRequiredLevel)
        {
            long stepsToAdvance = CeilingDivide(minimumRequiredLevel - firstOverlapLevel, leastCommonMultiple);
            firstOverlapLevel += stepsToAdvance * leastCommonMultiple;
        }

        if (firstOverlapLevel > int.MaxValue)
            return false;

        int resolvedLevel = (int)firstOverlapLevel;

        if (!IsWithinPhase(resolvedLevel, phaseStartLevel, phaseEndExclusive))
            return false;

        overlapLevel = resolvedLevel;
        return true;
    }

    private static bool IsWithinPhase(int levelValue, int phaseStartLevel, int phaseEndExclusive)
    {
        if (levelValue < phaseStartLevel)
            return false;

        if (phaseEndExclusive == int.MaxValue)
            return true;

        return levelValue < phaseEndExclusive;
    }

    private static long GreatestCommonDivisor(long leftValue, long rightValue)
    {
        long absoluteLeft = Math.Abs(leftValue);
        long absoluteRight = Math.Abs(rightValue);

        while (absoluteRight != 0)
        {
            long temporaryValue = absoluteLeft % absoluteRight;
            absoluteLeft = absoluteRight;
            absoluteRight = temporaryValue;
        }

        return absoluteLeft > 0 ? absoluteLeft : 1;
    }

    private static long ResolveModularInverse(long value, long modulus)
    {
        ExtendedGreatestCommonDivisor(value, modulus, out long inverse, out long _);
        return NormalizeModulo(inverse, modulus);
    }

    private static void ExtendedGreatestCommonDivisor(long leftValue,
                                                      long rightValue,
                                                      out long leftCoefficient,
                                                      out long rightCoefficient)
    {
        if (rightValue == 0)
        {
            leftCoefficient = 1;
            rightCoefficient = 0;
            return;
        }

        ExtendedGreatestCommonDivisor(rightValue,
                                      leftValue % rightValue,
                                      out long nextLeftCoefficient,
                                      out long nextRightCoefficient);
        leftCoefficient = nextRightCoefficient;
        rightCoefficient = nextLeftCoefficient - ((leftValue / rightValue) * nextRightCoefficient);
    }

    private static long NormalizeModulo(long value, long modulus)
    {
        if (modulus == 0)
            return 0;

        long normalizedValue = value % modulus;
        return normalizedValue < 0 ? normalizedValue + modulus : normalizedValue;
    }

    private static long CeilingDivide(long numerator, long denominator)
    {
        if (denominator <= 0)
            return 0;

        return numerator <= 0 ? 0 : ((numerator + denominator) - 1) / denominator;
    }
    #endregion

    #endregion
}
