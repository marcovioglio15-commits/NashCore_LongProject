using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Provides editor-side validation helpers for formula strings used by Add Scaling fields.
/// </summary>
public static class PlayerScalingFormulaValidationUtility
{
    #region Constants
    private const double GlobalVariableCacheDurationSeconds = 0.5d;
    #endregion

    #region Fields
    private static readonly HashSet<string> globalVariableCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HashSet<string>> scopedVariableCacheByKey = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> scopedVariableCacheTimeByKey = new Dictionary<string, double>(StringComparer.Ordinal);
    private static double lastGlobalVariableCacheRefreshTime = -1000d;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates one formula using parser rules and optional variable whitelist.
    /// </summary>
    /// <param name="formula">Formula text entered by designers.</param>
    /// <param name="allowedVariables">Optional variable whitelist (case-insensitive). Null skips unknown-variable checks.</param>
    /// <param name="warningMessage">Validation warning text when formula is invalid.</param>
    /// <returns>True when formula is valid, otherwise false.</returns>
    public static bool TryValidateFormula(string formula,
                                          ISet<string> allowedVariables,
                                          out string warningMessage)
    {
        warningMessage = string.Empty;

        ISet<string> resolvedAllowedVariables = ResolveAllowedVariables(allowedVariables);

        if (TryValidateBracketVariables(formula, resolvedAllowedVariables, out warningMessage) == false)
            return false;

        PlayerStatFormulaCompileResult compileResult = PlayerStatFormulaEngine.Compile(formula, true);

        if (compileResult.IsValid == false || compileResult.CompiledFormula == null)
        {
            warningMessage = string.IsNullOrWhiteSpace(compileResult.ErrorMessage)
                ? "Formula is invalid."
                : compileResult.ErrorMessage;
            return false;
        }

        IReadOnlyList<string> variableNames = compileResult.CompiledFormula.VariableNames;

        for (int index = 0; index < variableNames.Count; index++)
        {
            string variableName = variableNames[index];

            if (string.Equals(variableName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (resolvedAllowedVariables.Contains(variableName))
                continue;

            warningMessage = string.Format("Unknown scalable stat variable [{0}].", variableName);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Collects scalable stat names from progression serialized list into a case-insensitive set.
    /// </summary>
    /// <param name="scalableStatsProperty">Serialized List&lt;PlayerScalableStatDefinition&gt; property.</param>
    /// <returns>Case-insensitive set of scalable stat names.</returns>
    public static HashSet<string> BuildVariableSet(SerializedProperty scalableStatsProperty)
    {
        HashSet<string> variableSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (scalableStatsProperty == null)
            return variableSet;

        if (scalableStatsProperty.isArray == false)
            return variableSet;

        for (int index = 0; index < scalableStatsProperty.arraySize; index++)
        {
            SerializedProperty scalableStatElement = scalableStatsProperty.GetArrayElementAtIndex(index);

            if (scalableStatElement == null)
                continue;

            SerializedProperty statNameProperty = scalableStatElement.FindPropertyRelative("statName");

            if (statNameProperty == null)
                continue;

            if (statNameProperty.propertyType != SerializedPropertyType.String)
                continue;

            if (string.IsNullOrWhiteSpace(statNameProperty.stringValue))
                continue;

            string statName = statNameProperty.stringValue.Trim();

            if (PlayerScalableStatNameUtility.IsValid(statName) == false)
                continue;

            variableSet.Add(statName);
        }

        return variableSet;
    }

    /// <summary>
    /// Builds a scalable variable scope for the current edited preset, constrained by the active master preset context.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the currently edited scaling rules.</param>
    /// <returns>Case-insensitive set of scoped scalable stat names valid for this preset context.</returns>
    public static HashSet<string> BuildScopedVariableSet(SerializedObject serializedObject)
    {
        HashSet<string> variableSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (serializedObject == null)
            return variableSet;

        UnityEngine.Object targetObject = serializedObject.targetObject;

        if (targetObject == null)
            return variableSet;

        string cacheKey = BuildScopedCacheKey(targetObject, PlayerManagementSelectionContext.ActiveMasterPreset);
        double currentTime = EditorApplication.timeSinceStartup;

        if (scopedVariableCacheByKey.TryGetValue(cacheKey, out HashSet<string> cachedVariables) &&
            scopedVariableCacheTimeByKey.TryGetValue(cacheKey, out double cachedTime) &&
            currentTime - cachedTime < GlobalVariableCacheDurationSeconds)
            return new HashSet<string>(cachedVariables, StringComparer.OrdinalIgnoreCase);

        HashSet<string> scopedVariables = BuildScopedVariableSetInternal(targetObject);
        scopedVariableCacheByKey[cacheKey] = new HashSet<string>(scopedVariables, StringComparer.OrdinalIgnoreCase);
        scopedVariableCacheTimeByKey[cacheKey] = currentTime;

        TrimScopedCache();
        return scopedVariables;
    }
    #endregion

    #region Helpers
    private static bool TryValidateBracketVariables(string formula,
                                                    ISet<string> allowedVariables,
                                                    out string warningMessage)
    {
        warningMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(formula))
            return true;

        int parseIndex = 0;

        while (parseIndex < formula.Length)
        {
            int openBracketIndex = formula.IndexOf('[', parseIndex);

            if (openBracketIndex < 0)
                return true;

            int closeBracketIndex = formula.IndexOf(']', openBracketIndex + 1);

            if (closeBracketIndex < 0)
            {
                warningMessage = "Variable token is missing closing ']'.";
                return false;
            }

            string variableName = formula.Substring(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1).Trim();

            if (string.IsNullOrWhiteSpace(variableName))
            {
                warningMessage = "Variable token cannot be empty.";
                return false;
            }

            if (string.Equals(variableName, PlayerScalableStatNameUtility.ReservedThisName, StringComparison.OrdinalIgnoreCase))
            {
                parseIndex = closeBracketIndex + 1;
                continue;
            }

            if (PlayerScalableStatNameUtility.IsValid(variableName) == false)
            {
                warningMessage = string.Format("Token [{0}] is not a valid variable name.", variableName);
                return false;
            }

            if (allowedVariables != null && allowedVariables.Contains(variableName) == false)
            {
                warningMessage = string.Format("Unknown scalable stat variable [{0}].", variableName);
                return false;
            }

            parseIndex = closeBracketIndex + 1;
        }

        return true;
    }

    private static ISet<string> ResolveAllowedVariables(ISet<string> inputAllowedVariables)
    {
        if (inputAllowedVariables != null)
            return inputAllowedVariables;

        return BuildGlobalVariableSet();
    }

    private static string BuildScopedCacheKey(UnityEngine.Object targetObject, PlayerMasterPreset activeMasterPreset)
    {
        int targetInstanceId = targetObject != null ? targetObject.GetInstanceID() : 0;
        int masterInstanceId = activeMasterPreset != null ? activeMasterPreset.GetInstanceID() : 0;
        return string.Format("{0}:{1}", targetInstanceId, masterInstanceId);
    }

    private static HashSet<string> BuildScopedVariableSetInternal(UnityEngine.Object targetObject)
    {
        HashSet<string> scopedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (targetObject == null)
            return scopedVariables;

        if (TryCollectFromActiveMasterScope(targetObject, scopedVariables))
            return scopedVariables;

        PlayerProgressionPreset progressionPreset = targetObject as PlayerProgressionPreset;

        if (progressionPreset != null)
        {
            CollectVariablesFromPreset(progressionPreset, scopedVariables);
            return scopedVariables;
        }

        return scopedVariables;
    }

    private static bool TryCollectFromActiveMasterScope(UnityEngine.Object targetObject, HashSet<string> variables)
    {
        if (variables == null)
            return false;

        PlayerMasterPreset activeMasterPreset = PlayerManagementSelectionContext.ActiveMasterPreset;

        if (activeMasterPreset == null)
            return false;

        if (DoesMasterReferenceTarget(activeMasterPreset, targetObject) == false)
            return false;

        CollectVariablesFromPreset(activeMasterPreset.ProgressionPreset, variables);
        return true;
    }

    private static bool DoesMasterReferenceTarget(PlayerMasterPreset masterPreset, UnityEngine.Object targetObject)
    {
        if (masterPreset == null || targetObject == null)
            return false;

        PlayerControllerPreset controllerPreset = targetObject as PlayerControllerPreset;

        if (controllerPreset != null)
            return masterPreset.ControllerPreset == controllerPreset;

        PlayerProgressionPreset progressionPreset = targetObject as PlayerProgressionPreset;

        if (progressionPreset != null)
            return masterPreset.ProgressionPreset == progressionPreset;

        PlayerPowerUpsPreset powerUpsPreset = targetObject as PlayerPowerUpsPreset;

        if (powerUpsPreset != null)
            return masterPreset.PowerUpsPreset == powerUpsPreset;

        PlayerAnimationBindingsPreset animationPreset = targetObject as PlayerAnimationBindingsPreset;

        if (animationPreset != null)
            return masterPreset.AnimationBindingsPreset == animationPreset;

        return false;
    }

    private static ISet<string> BuildGlobalVariableSet()
    {
        double currentTime = EditorApplication.timeSinceStartup;

        if (currentTime - lastGlobalVariableCacheRefreshTime < GlobalVariableCacheDurationSeconds)
            return globalVariableCache;

        globalVariableCache.Clear();
        CollectGlobalVariablesFromLibrary(globalVariableCache);
        CollectGlobalVariablesFromAssets(globalVariableCache);
        lastGlobalVariableCacheRefreshTime = currentTime;
        return globalVariableCache;
    }

    private static void CollectGlobalVariablesFromLibrary(HashSet<string> variables)
    {
        PlayerProgressionPresetLibrary library = AssetDatabase.LoadAssetAtPath<PlayerProgressionPresetLibrary>(PlayerProgressionPresetLibraryUtility.DefaultLibraryPath);

        if (library == null)
            return;

        IReadOnlyList<PlayerProgressionPreset> presets = library.Presets;

        if (presets == null)
            return;

        for (int index = 0; index < presets.Count; index++)
            CollectVariablesFromPreset(presets[index], variables);
    }

    private static void CollectGlobalVariablesFromAssets(HashSet<string> variables)
    {
        string[] searchFolders = new string[] { "Assets" };
        string[] presetGuids = AssetDatabase.FindAssets("t:PlayerProgressionPreset", searchFolders);

        for (int guidIndex = 0; guidIndex < presetGuids.Length; guidIndex++)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuids[guidIndex]);

            if (string.IsNullOrWhiteSpace(presetPath))
                continue;

            PlayerProgressionPreset preset = AssetDatabase.LoadAssetAtPath<PlayerProgressionPreset>(presetPath);
            CollectVariablesFromPreset(preset, variables);
        }
    }

    private static void CollectVariablesFromPreset(PlayerProgressionPreset preset, HashSet<string> variables)
    {
        if (preset == null || variables == null)
            return;

        IReadOnlyList<PlayerScalableStatDefinition> scalableStats = preset.ScalableStats;

        if (scalableStats == null)
            return;

        for (int statIndex = 0; statIndex < scalableStats.Count; statIndex++)
        {
            PlayerScalableStatDefinition statDefinition = scalableStats[statIndex];

            if (statDefinition == null)
                continue;

            string statName = statDefinition.StatName;

            if (string.IsNullOrWhiteSpace(statName))
                continue;

            statName = statName.Trim();

            if (PlayerScalableStatNameUtility.IsValid(statName) == false)
                continue;

            variables.Add(statName);
        }
    }

    private static void TrimScopedCache()
    {
        const int MaxScopedCacheEntries = 256;

        if (scopedVariableCacheByKey.Count <= MaxScopedCacheEntries)
            return;

        scopedVariableCacheByKey.Clear();
        scopedVariableCacheTimeByKey.Clear();
    }
    #endregion

    #endregion
}
