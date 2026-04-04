using System;
using UnityEditor;

/// <summary>
/// Resolves shared pattern-definition state keys, identity helpers and serialized field accessors.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetPatternDefinitionUtility
{
    #region Constants
    private const string CardStateSuffix = "EnemySharedPatternDefinitionCard";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the foldout title shown by one shared pattern card.
    /// /params patternIndex Current pattern index.
    /// /params patternId Resolved pattern ID.
    /// /params displayName Resolved display name.
    /// /params unreplaceable Resolved unreplaceable flag.
    /// /returns Card title text.
    /// </summary>
    public static string BuildPatternCardTitle(int patternIndex,
                                               string patternId,
                                               string displayName,
                                               bool unreplaceable)
    {
        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "<Unnamed>" : displayName.Trim();
        string resolvedPatternId = string.IsNullOrWhiteSpace(patternId) ? "<No ID>" : patternId.Trim();
        string policyLabel = unreplaceable ? "Locked" : "Replaceable";
        return string.Format("#{0:D2}  {1}  ({2})  [{3}]",
                             patternIndex + 1,
                             resolvedDisplayName,
                             resolvedPatternId,
                             policyLabel);
    }

    /// <summary>
    /// Builds the persistent foldout-state key for one shared pattern card.
    /// /params patternProperty Serialized pattern property that owns the card.
    /// /returns Foldout-state key.
    /// </summary>
    public static string BuildPatternCardStateKey(SerializedProperty patternProperty)
    {
        return ManagementToolFoldoutStateUtility.BuildPropertyStateKey(patternProperty, CardStateSuffix);
    }

    /// <summary>
    /// Generates a unique pattern ID inside the shared pattern list.
    /// /params patternsProperty Serialized patterns array.
    /// /params basePatternId Preferred pattern ID prefix.
    /// /params excludedPropertyPath Property path excluded from duplicate checks.
    /// /returns Unique pattern ID.
    /// </summary>
    public static string GenerateUniquePatternId(SerializedProperty patternsProperty,
                                                 string basePatternId,
                                                 string excludedPropertyPath)
    {
        string sanitizedBasePatternId = string.IsNullOrWhiteSpace(basePatternId) ? "Pattern_Custom" : basePatternId.Trim();

        if (!ContainsPatternId(patternsProperty, sanitizedBasePatternId, excludedPropertyPath))
            return sanitizedBasePatternId;

        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidatePatternId = sanitizedBasePatternId + suffix.ToString();

            if (ContainsPatternId(patternsProperty, candidatePatternId, excludedPropertyPath))
                continue;

            return candidatePatternId;
        }

        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Initializes a newly inserted shared pattern with one default identity and one best-effort core movement binding.
    /// /params sharedPresetSerializedObject Serialized shared preset used to resolve the first core movement module.
    /// /params patternProperty Serialized pattern property being initialized.
    /// /params patternId Unique pattern ID assigned to the new pattern.
    /// /returns None.
    /// </summary>
    public static void InitializeNewPatternDefinition(SerializedObject sharedPresetSerializedObject,
                                                      SerializedProperty patternProperty,
                                                      string patternId)
    {
        if (patternProperty == null)
            return;

        SetPatternId(patternProperty, patternId);
        SetPatternDisplayName(patternProperty, "New Pattern");
        SetPatternDescription(patternProperty, string.Empty);
        SetPatternUnreplaceable(patternProperty, false);

        SerializedProperty coreMovementProperty = patternProperty.FindPropertyRelative("coreMovement");

        if (coreMovementProperty == null)
            return;

        SerializedProperty bindingProperty = coreMovementProperty.FindPropertyRelative("binding");

        if (bindingProperty == null)
            return;

        SerializedProperty moduleIdProperty = bindingProperty.FindPropertyRelative("moduleId");
        SerializedProperty enabledProperty = bindingProperty.FindPropertyRelative("isEnabled");
        SerializedProperty useOverridePayloadProperty = bindingProperty.FindPropertyRelative("useOverridePayload");

        if (moduleIdProperty != null)
            moduleIdProperty.stringValue = ResolveFirstModuleId(sharedPresetSerializedObject, "coreMovementDefinitions");

        if (enabledProperty != null)
            enabledProperty.boolValue = true;

        if (useOverridePayloadProperty != null)
            useOverridePayloadProperty.boolValue = false;
    }

    /// <summary>
    /// Resolves the pattern ID of one serialized shared pattern.
    /// /params patternProperty Serialized pattern property.
    /// /returns Pattern ID string, or an empty string when unavailable.
    /// </summary>
    public static string ResolvePatternId(SerializedProperty patternProperty)
    {
        if (patternProperty == null)
            return string.Empty;

        SerializedProperty patternIdProperty = patternProperty.FindPropertyRelative("patternId");

        if (patternIdProperty == null)
            return string.Empty;

        return patternIdProperty.stringValue;
    }

    /// <summary>
    /// Resolves the display name of one serialized shared pattern.
    /// /params patternProperty Serialized pattern property.
    /// /returns Display name string, or an empty string when unavailable.
    /// </summary>
    public static string ResolvePatternDisplayName(SerializedProperty patternProperty)
    {
        if (patternProperty == null)
            return string.Empty;

        SerializedProperty displayNameProperty = patternProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return string.Empty;

        return displayNameProperty.stringValue;
    }

    /// <summary>
    /// Resolves the unreplaceable flag of one serialized shared pattern.
    /// /params patternProperty Serialized pattern property.
    /// /returns Unreplaceable flag value.
    /// </summary>
    public static bool ResolvePatternUnreplaceable(SerializedProperty patternProperty)
    {
        if (patternProperty == null)
            return false;

        SerializedProperty unreplaceableProperty = patternProperty.FindPropertyRelative("unreplaceable");

        if (unreplaceableProperty == null)
            return false;

        return unreplaceableProperty.boolValue;
    }

    /// <summary>
    /// Sets the pattern ID of one serialized shared pattern.
    /// /params patternProperty Serialized pattern property.
    /// /params patternId New pattern ID.
    /// /returns None.
    /// </summary>
    public static void SetPatternId(SerializedProperty patternProperty, string patternId)
    {
        if (patternProperty == null)
            return;

        SerializedProperty patternIdProperty = patternProperty.FindPropertyRelative("patternId");

        if (patternIdProperty == null)
            return;

        patternIdProperty.stringValue = patternId;
    }

    /// <summary>
    /// Sets the display name of one serialized shared pattern.
    /// /params patternProperty Serialized pattern property.
    /// /params displayName New display name.
    /// /returns None.
    /// </summary>
    public static void SetPatternDisplayName(SerializedProperty patternProperty, string displayName)
    {
        if (patternProperty == null)
            return;

        SerializedProperty displayNameProperty = patternProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return;

        displayNameProperty.stringValue = displayName;
    }

    /// <summary>
    /// Sets the description of one serialized shared pattern.
    /// /params patternProperty Serialized pattern property.
    /// /params description New description text.
    /// /returns None.
    /// </summary>
    public static void SetPatternDescription(SerializedProperty patternProperty, string description)
    {
        if (patternProperty == null)
            return;

        SerializedProperty descriptionProperty = patternProperty.FindPropertyRelative("description");

        if (descriptionProperty == null)
            return;

        descriptionProperty.stringValue = description;
    }

    /// <summary>
    /// Sets the unreplaceable flag of one serialized shared pattern.
    /// /params patternProperty Serialized pattern property.
    /// /params unreplaceable New unreplaceable flag.
    /// /returns None.
    /// </summary>
    public static void SetPatternUnreplaceable(SerializedProperty patternProperty, bool unreplaceable)
    {
        if (patternProperty == null)
            return;

        SerializedProperty unreplaceableProperty = patternProperty.FindPropertyRelative("unreplaceable");

        if (unreplaceableProperty == null)
            return;

        unreplaceableProperty.boolValue = unreplaceable;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Returns whether the shared pattern list already contains one pattern ID.
    /// /params patternsProperty Serialized patterns array.
    /// /params patternId Candidate pattern ID.
    /// /params excludedPropertyPath Property path excluded from duplicate checks.
    /// /returns True when the candidate ID already exists.
    /// </summary>
    private static bool ContainsPatternId(SerializedProperty patternsProperty,
                                          string patternId,
                                          string excludedPropertyPath)
    {
        if (patternsProperty == null || string.IsNullOrWhiteSpace(patternId))
            return false;

        for (int patternIndex = 0; patternIndex < patternsProperty.arraySize; patternIndex++)
        {
            SerializedProperty patternProperty = patternsProperty.GetArrayElementAtIndex(patternIndex);

            if (patternProperty == null)
                continue;

            if (!string.IsNullOrWhiteSpace(excludedPropertyPath) &&
                string.Equals(patternProperty.propertyPath, excludedPropertyPath, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(ResolvePatternId(patternProperty), patternId, StringComparison.OrdinalIgnoreCase))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the first authored module ID from one shared catalog section.
    /// /params sharedPresetSerializedObject Serialized shared preset that owns the catalog.
    /// /params definitionsPropertyName Serialized property name of the catalog.
    /// /returns First authored module ID, or an empty string when unavailable.
    /// </summary>
    private static string ResolveFirstModuleId(SerializedObject sharedPresetSerializedObject, string definitionsPropertyName)
    {
        if (sharedPresetSerializedObject == null)
            return string.Empty;

        SerializedProperty definitionsProperty = sharedPresetSerializedObject.FindProperty(definitionsPropertyName);

        if (definitionsProperty == null)
            return string.Empty;

        for (int moduleIndex = 0; moduleIndex < definitionsProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = definitionsProperty.GetArrayElementAtIndex(moduleIndex);
            string moduleId = ResolveModuleIdFromProperty(moduleProperty);

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            return moduleId;
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves the module ID of one serialized module property.
    /// /params moduleProperty Serialized module property.
    /// /returns Module ID string, or an empty string when unavailable.
    /// </summary>
    private static string ResolveModuleIdFromProperty(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
            return string.Empty;

        SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
            return string.Empty;

        return moduleIdProperty.stringValue;
    }
    #endregion

    #endregion
}
