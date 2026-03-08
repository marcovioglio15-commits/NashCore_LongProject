#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Logs one ordered debug line per scaling rule, including formula, resolved math expression, and final value.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerProgressionInitializeSystem))]
public partial struct PlayerScalingDebugConsoleSystem : ISystem
{
    #region Constants
    private const float ValueChangeEpsilon = 0.0001f;
    private const double MinimumLogIntervalSeconds = 0.25d;
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;
    #endregion

    #region Fields
    private EntityQuery ruleDebugQuery;
    private NativeParallelHashMap<ulong, float> lastLoggedRuleValues;
    private NativeParallelHashMap<ulong, uint> lastEntityVariableHashes;
    private double nextAllowedLogTime;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Creates the query used to detect entities with scaling debug snapshots and scalable stat values.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnCreate(ref SystemState state)
    {
        ruleDebugQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerScalingDebugRuleElement, PlayerScalableStatElement>()
            .Build();
        state.RequireForUpdate(ruleDebugQuery);
        lastLoggedRuleValues = new NativeParallelHashMap<ulong, float>(256, Allocator.Persistent);
        lastEntityVariableHashes = new NativeParallelHashMap<ulong, uint>(32, Allocator.Persistent);
        nextAllowedLogTime = 0d;
    }

    /// <summary>
    /// Resets cached change-tracking maps when the system starts running.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnStartRunning(ref SystemState state)
    {
        if (lastLoggedRuleValues.IsCreated)
            lastLoggedRuleValues.Clear();

        if (lastEntityVariableHashes.IsCreated)
            lastEntityVariableHashes.Clear();

        nextAllowedLogTime = 0d;
    }

    /// <summary>
    /// Releases native allocations owned by this editor-only debug system.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnDestroy(ref SystemState state)
    {
        if (lastLoggedRuleValues.IsCreated)
            lastLoggedRuleValues.Dispose();

        if (lastEntityVariableHashes.IsCreated)
            lastEntityVariableHashes.Dispose();
    }

    /// <summary>
    /// Emits colorized scaling-debug logs at a throttled cadence and only for rules whose evaluated value changed.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    /// <returns>Void.</returns>
    public void OnUpdate(ref SystemState state)
    {
        if (Application.isPlaying == false)
            return;

        if (ruleDebugQuery.IsEmptyIgnoreFilter)
            return;

        double elapsedTime = SystemAPI.Time.ElapsedTime;

        if (elapsedTime < nextAllowedLogTime)
            return;

        nextAllowedLogTime = elapsedTime + MinimumLogIntervalSeconds;

        Dictionary<string, float> variableContext = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        StringBuilder batchedLogBuilder = new StringBuilder(1024);
        int batchedRuleCount = 0;

        foreach ((DynamicBuffer<PlayerScalingDebugRuleElement> debugRules,
                  DynamicBuffer<PlayerScalableStatElement> scalableStats,
                  Entity entity) in SystemAPI.Query<DynamicBuffer<PlayerScalingDebugRuleElement>,
                                                    DynamicBuffer<PlayerScalableStatElement>>()
                                              .WithEntityAccess())
        {
            if (debugRules.Length == 0)
                continue;

            ulong entityKey = ComposeEntityKey(entity);
            uint variableContextHash = ComputeVariableContextHash(scalableStats);

            if (HasEntityVariableHashChanged(entityKey, variableContextHash) == false)
                continue;

            FillVariableContext(scalableStats, variableContext);

            for (int ruleIndex = 0; ruleIndex < debugRules.Length; ruleIndex++)
            {
                PlayerScalingDebugRuleElement debugRule = debugRules[ruleIndex];
                string targetDisplayName = ResolveTargetDisplayName(debugRule);
                string formulaText = debugRule.Formula.ToString();
                string translatedFormula = TranslateFormula(formulaText, variableContext, debugRule.ThisValue);
                float evaluatedValue = debugRule.FinalValue;
                Color debugColor = ResolveDebugColor(debugRule);
                string debugColorHex = ColorUtility.ToHtmlStringRGBA(debugColor);

                if (PlayerStatFormulaEngine.TryEvaluate(formulaText,
                                                        debugRule.ThisValue,
                                                        variableContext,
                                                        out float runtimeEvaluatedValue,
                                                        out string _))
                {
                    evaluatedValue = runtimeEvaluatedValue;
                }

                ulong ruleKey = ComposeRuleKey(entity, ruleIndex);

                if (HasRuleValueChanged(ruleKey, evaluatedValue) == false)
                    continue;

                AppendRuleLine(batchedLogBuilder,
                               targetDisplayName,
                               formulaText,
                               translatedFormula,
                               evaluatedValue,
                               debugColorHex);
                batchedRuleCount++;
            }
        }

        if (batchedRuleCount == 0)
            return;

        string batchedLog = string.Format(CultureInfo.InvariantCulture,
                                          "[PlayerScalingDebugConsoleSystem] Updated rules: {0}\n{1}",
                                          batchedRuleCount,
                                          batchedLogBuilder.ToString());
        Debug.Log(batchedLog);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Creates a stable 64-bit key for one entity using index/version tuple.
    /// </summary>
    /// <param name="entity">Entity used as key source.</param>
    /// <returns>Packed key used by native hash maps.</returns>
    private static ulong ComposeEntityKey(Entity entity)
    {
        ulong versionPart = (uint)entity.Version;
        ulong indexPart = (uint)entity.Index;
        return (versionPart << 32) | indexPart;
    }

    /// <summary>
    /// Creates a stable 64-bit key for one debug rule entry bound to an entity.
    /// </summary>
    /// <param name="entity">Entity owning the debug-rule buffer.</param>
    /// <param name="ruleIndex">Rule index inside the debug-rule buffer.</param>
    /// <returns>Combined key for rule-value change tracking.</returns>
    private static ulong ComposeRuleKey(Entity entity, int ruleIndex)
    {
        ulong entityKey = ComposeEntityKey(entity);
        ulong rulePart = (uint)ruleIndex;
        return (entityKey * 1099511628211UL) ^ rulePart;
    }

    /// <summary>
    /// Computes an FNV-1a hash from all scalable stat names and values to detect entity-level variable changes.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable stat buffer.</param>
    /// <returns>Hash representing current variable-context snapshot.</returns>
    private static uint ComputeVariableContextHash(DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        uint rollingHash = FnvOffsetBasis;

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (scalableStat.Name.Length == 0)
                continue;

            uint nameHash = (uint)scalableStat.Name.GetHashCode();
            uint valueHash = math.asuint(scalableStat.Value);
            rollingHash = (rollingHash ^ nameHash) * FnvPrime;
            rollingHash = (rollingHash ^ valueHash) * FnvPrime;
        }

        rollingHash = (rollingHash ^ (uint)scalableStats.Length) * FnvPrime;
        return rollingHash;
    }

    /// <summary>
    /// Updates the entity-variable hash cache and reports whether the context changed since the previous logged sample.
    /// </summary>
    /// <param name="entityKey">Packed entity key.</param>
    /// <param name="currentHash">Current variable-context hash.</param>
    /// <returns>True when values changed and rule evaluation should run, otherwise false.</returns>
    private bool HasEntityVariableHashChanged(ulong entityKey, uint currentHash)
    {
        if (lastEntityVariableHashes.TryGetValue(entityKey, out uint previousHash))
        {
            if (previousHash == currentHash)
                return false;

            lastEntityVariableHashes.Remove(entityKey);
            lastEntityVariableHashes.TryAdd(entityKey, currentHash);
            return true;
        }

        EnsureEntityHashCapacity(lastEntityVariableHashes.Count() + 1);
        lastEntityVariableHashes.TryAdd(entityKey, currentHash);
        return true;
    }

    /// <summary>
    /// Updates the rule-value cache and reports whether a meaningful value delta occurred.
    /// </summary>
    /// <param name="ruleKey">Packed key for one rule entry.</param>
    /// <param name="currentValue">Current evaluated value.</param>
    /// <returns>True when value changed beyond epsilon and should be logged, otherwise false.</returns>
    private bool HasRuleValueChanged(ulong ruleKey, float currentValue)
    {
        if (lastLoggedRuleValues.TryGetValue(ruleKey, out float previousValue))
        {
            float delta = Mathf.Abs(currentValue - previousValue);

            if (delta <= ValueChangeEpsilon)
                return false;

            lastLoggedRuleValues.Remove(ruleKey);
            lastLoggedRuleValues.TryAdd(ruleKey, currentValue);
            return true;
        }

        EnsureRuleValueCapacity(lastLoggedRuleValues.Count() + 1);
        lastLoggedRuleValues.TryAdd(ruleKey, currentValue);
        return true;
    }

    /// <summary>
    /// Grows the rule-value cache capacity when the next insertion would overflow.
    /// </summary>
    /// <param name="requiredCapacity">Minimum capacity required by the next insertion.</param>
    /// <returns>Void.</returns>
    private void EnsureRuleValueCapacity(int requiredCapacity)
    {
        if (lastLoggedRuleValues.Capacity >= requiredCapacity)
            return;

        int targetCapacity = requiredCapacity * 2;
        lastLoggedRuleValues.Capacity = targetCapacity;
    }

    /// <summary>
    /// Grows the entity-hash cache capacity when the next insertion would overflow.
    /// </summary>
    /// <param name="requiredCapacity">Minimum capacity required by the next insertion.</param>
    /// <returns>Void.</returns>
    private void EnsureEntityHashCapacity(int requiredCapacity)
    {
        if (lastEntityVariableHashes.Capacity >= requiredCapacity)
            return;

        int targetCapacity = requiredCapacity * 2;
        lastEntityVariableHashes.Capacity = targetCapacity;
    }

    /// <summary>
    /// Appends one formatted colorized rule line to the shared batched output buffer.
    /// </summary>
    /// <param name="batchedLogBuilder">Mutable frame-level string builder.</param>
    /// <param name="targetDisplayName">Display label for the scaled stat.</param>
    /// <param name="formulaText">Original formula text.</param>
    /// <param name="translatedFormula">Formula with resolved variable values.</param>
    /// <param name="evaluatedValue">Final evaluated numeric result.</param>
    /// <param name="debugColorHex">Color used by this debug line as HTML hex RGBA.</param>
    /// <returns>Void.</returns>
    private static void AppendRuleLine(StringBuilder batchedLogBuilder,
                                       string targetDisplayName,
                                       string formulaText,
                                       string translatedFormula,
                                       float evaluatedValue,
                                       string debugColorHex)
    {
        batchedLogBuilder.Append("<color=#");
        batchedLogBuilder.Append(debugColorHex);
        batchedLogBuilder.Append('>');
        batchedLogBuilder.Append(targetDisplayName);
        batchedLogBuilder.Append(" = ");
        batchedLogBuilder.Append(formulaText);
        batchedLogBuilder.Append(" = ");
        batchedLogBuilder.Append(translatedFormula);
        batchedLogBuilder.Append(" = ");
        batchedLogBuilder.Append(FormatNumber(evaluatedValue));
        batchedLogBuilder.AppendLine("</color>");
    }

    /// <summary>
    /// Fills a case-insensitive variable dictionary from runtime scalable stat buffer values.
    /// </summary>
    /// <param name="scalableStats">Runtime scalable stat buffer.</param>
    /// <param name="variableContext">Mutable dictionary receiving variable values.</param>
    /// <returns>Void.</returns>
    private static void FillVariableContext(DynamicBuffer<PlayerScalableStatElement> scalableStats,
                                            Dictionary<string, float> variableContext)
    {
        variableContext.Clear();

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (scalableStat.Name.Length == 0)
                continue;

            string variableName = scalableStat.Name.ToString();

            if (string.IsNullOrWhiteSpace(variableName))
                continue;

            variableContext[variableName] = scalableStat.Value;
        }
    }

    /// <summary>
    /// Resolves target display name with fallback to stat key.
    /// </summary>
    /// <param name="debugRule">Debug-rule snapshot entry.</param>
    /// <returns>Resolved display name used in the output message.</returns>
    private static string ResolveTargetDisplayName(in PlayerScalingDebugRuleElement debugRule)
    {
        string targetDisplayName = debugRule.TargetDisplayName.ToString();

        if (string.IsNullOrWhiteSpace(targetDisplayName) == false)
            return targetDisplayName;

        string statKey = debugRule.StatKey.ToString();

        if (string.IsNullOrWhiteSpace(statKey) == false)
            return statKey;

        return "Scaled Stat";
    }

    /// <summary>
    /// Builds a readable mathematical translation by replacing [variables] with resolved numeric values.
    /// </summary>
    /// <param name="formula">Raw scaling formula text.</param>
    /// <param name="variableContext">Runtime variable dictionary for scalable stats.</param>
    /// <param name="thisValue">Input value mapped to [this].</param>
    /// <returns>Resolved formula expression ready for console output.</returns>
    private static string TranslateFormula(string formula,
                                           IReadOnlyDictionary<string, float> variableContext,
                                           float thisValue)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return string.Empty;

        StringBuilder translatedFormulaBuilder = new StringBuilder(formula.Length + 32);
        int parseIndex = 0;

        while (parseIndex < formula.Length)
        {
            int openBracketIndex = formula.IndexOf('[', parseIndex);

            if (openBracketIndex < 0)
            {
                translatedFormulaBuilder.Append(formula.Substring(parseIndex));
                break;
            }

            if (openBracketIndex > parseIndex)
                translatedFormulaBuilder.Append(formula.Substring(parseIndex, openBracketIndex - parseIndex));

            int closeBracketIndex = formula.IndexOf(']', openBracketIndex + 1);

            if (closeBracketIndex < 0)
            {
                translatedFormulaBuilder.Append(formula.Substring(openBracketIndex));
                break;
            }

            string variableToken = formula.Substring(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1).Trim();

            if (string.Equals(variableToken, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
            {
                translatedFormulaBuilder.Append(FormatNumber(thisValue));
                parseIndex = closeBracketIndex + 1;
                continue;
            }

            if (TryResolveVariableValue(variableContext, variableToken, out float resolvedValue))
            {
                translatedFormulaBuilder.Append(FormatNumber(resolvedValue));
                parseIndex = closeBracketIndex + 1;
                continue;
            }

            translatedFormulaBuilder.Append('[');
            translatedFormulaBuilder.Append(variableToken);
            translatedFormulaBuilder.Append(']');
            parseIndex = closeBracketIndex + 1;
        }

        return translatedFormulaBuilder.ToString();
    }

    /// <summary>
    /// Resolves one variable value from runtime scalable stat dictionary using case-insensitive keys.
    /// </summary>
    /// <param name="variableContext">Runtime variable dictionary.</param>
    /// <param name="variableName">Variable name to resolve.</param>
    /// <param name="resolvedValue">Resolved value when found.</param>
    /// <returns>True when the variable exists in the buffer, otherwise false.</returns>
    private static bool TryResolveVariableValue(IReadOnlyDictionary<string, float> variableContext,
                                                string variableName,
                                                out float resolvedValue)
    {
        resolvedValue = 0f;

        if (variableContext == null || string.IsNullOrWhiteSpace(variableName))
            return false;

        return variableContext.TryGetValue(variableName, out resolvedValue);
    }

    /// <summary>
    /// Resolves the per-rule debug color with safe clamping and fallback to default yellow for invalid/legacy data.
    /// </summary>
    /// <param name="debugRule">Current debug-rule snapshot.</param>
    /// <returns>Resolved color used for this log line.</returns>
    private static Color ResolveDebugColor(in PlayerScalingDebugRuleElement debugRule)
    {
        float red = Mathf.Clamp01(debugRule.DebugColor.x);
        float green = Mathf.Clamp01(debugRule.DebugColor.y);
        float blue = Mathf.Clamp01(debugRule.DebugColor.z);
        float alpha = Mathf.Clamp01(debugRule.DebugColor.w);

        if (alpha <= 0.0001f)
            return PlayerStatScalingRule.GetDefaultDebugColor();

        return new Color(red, green, blue, alpha);
    }

    /// <summary>
    /// Formats numeric values for compact formula-output readability.
    /// </summary>
    /// <param name="value">Numeric value to format.</param>
    /// <returns>Formatted numeric text using invariant culture.</returns>
    private static string FormatNumber(float value)
    {
        float roundedInteger = Mathf.Round(value);

        if (Mathf.Abs(value - roundedInteger) <= 0.0001f)
            return ((int)roundedInteger).ToString(CultureInfo.InvariantCulture);

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
    #endregion

    #endregion
}
#endif
