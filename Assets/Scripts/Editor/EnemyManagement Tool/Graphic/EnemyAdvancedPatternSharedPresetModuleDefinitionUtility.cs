using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Resolves shared module-definition state keys, identity helpers and serialized field accessors.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetModuleDefinitionUtility
{
    #region Constants
    private const string CardStateSuffix = "EnemySharedModuleDefinitionCard";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the foldout title shown by one shared module card.
    /// /params moduleIndex Current module index.
    /// /params moduleId Resolved module ID.
    /// /params displayName Resolved display name.
    /// /params moduleKind Resolved module kind.
    /// /returns Card title text.
    /// </summary>
    public static string BuildModuleCardTitle(int moduleIndex,
                                              string moduleId,
                                              string displayName,
                                              EnemyPatternModuleKind moduleKind)
    {
        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "<Unnamed>" : displayName.Trim();
        string resolvedModuleId = string.IsNullOrWhiteSpace(moduleId) ? "<No ID>" : moduleId.Trim();
        return string.Format("#{0:D2}  {1}  ({2})  [{3}]",
                             moduleIndex + 1,
                             resolvedDisplayName,
                             resolvedModuleId,
                             moduleKind);
    }

    /// <summary>
    /// Builds the persistent foldout-state key for one shared module card.
    /// /params moduleProperty Serialized module property that owns the card.
    /// /returns Foldout-state key.
    /// </summary>
    public static string BuildModuleCardStateKey(SerializedProperty moduleProperty)
    {
        return ManagementToolFoldoutStateUtility.BuildPropertyStateKey(moduleProperty, CardStateSuffix);
    }

    /// <summary>
    /// Generates a globally unique module ID across all shared catalog sections.
    /// /params sharedPresetSerializedObject Serialized shared preset that owns every catalog section.
    /// /params baseModuleId Preferred module ID prefix.
    /// /params excludedPropertyPath Property path excluded from duplicate checks.
    /// /returns Unique module ID.
    /// </summary>
    public static string GenerateUniqueModuleId(SerializedObject sharedPresetSerializedObject,
                                                string baseModuleId,
                                                string excludedPropertyPath)
    {
        string sanitizedBaseModuleId = string.IsNullOrWhiteSpace(baseModuleId) ? "Module_Custom" : baseModuleId.Trim();
        HashSet<string> existingModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectModuleIds(sharedPresetSerializedObject,
                         "coreMovementDefinitions",
                         excludedPropertyPath,
                         existingModuleIds);
        CollectModuleIds(sharedPresetSerializedObject,
                         "shortRangeInteractionDefinitions",
                         excludedPropertyPath,
                         existingModuleIds);
        CollectModuleIds(sharedPresetSerializedObject,
                         "weaponInteractionDefinitions",
                         excludedPropertyPath,
                         existingModuleIds);
        CollectModuleIds(sharedPresetSerializedObject,
                         "dropItemsDefinitions",
                         excludedPropertyPath,
                         existingModuleIds);

        if (!existingModuleIds.Contains(sanitizedBaseModuleId))
            return sanitizedBaseModuleId;

        for (int suffix = 2; suffix < int.MaxValue; suffix++)
        {
            string candidateModuleId = sanitizedBaseModuleId + suffix.ToString();

            if (existingModuleIds.Contains(candidateModuleId))
                continue;

            return candidateModuleId;
        }

        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Initializes a newly inserted module definition with one section-appropriate default payload setup.
    /// /params moduleProperty Serialized module property being initialized.
    /// /params section Catalog section that owns the new definition.
    /// /params moduleId Unique module ID assigned to the definition.
    /// /params displayName Display name assigned to the definition.
    /// /returns None.
    /// </summary>
    public static void InitializeNewModuleDefinition(SerializedProperty moduleProperty,
                                                     EnemyPatternModuleCatalogSection section,
                                                     string moduleId,
                                                     string displayName)
    {
        if (moduleProperty == null)
            return;

        SetModuleDefinitionId(moduleProperty, moduleId);
        SetModuleDefinitionDisplayName(moduleProperty, displayName);
        SetModuleDefinitionKind(moduleProperty, EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveDefaultModuleKind(section));
        SetModuleDefinitionNotes(moduleProperty, string.Empty);
    }

    /// <summary>
    /// Resolves the module ID of one serialized definition.
    /// /params moduleProperty Serialized module definition property.
    /// /returns Module ID string, or an empty string when unavailable.
    /// </summary>
    public static string ResolveModuleDefinitionId(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
            return string.Empty;

        SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
            return string.Empty;

        return moduleIdProperty.stringValue;
    }

    /// <summary>
    /// Resolves the display name of one serialized definition.
    /// /params moduleProperty Serialized module definition property.
    /// /returns Display name string, or an empty string when unavailable.
    /// </summary>
    public static string ResolveModuleDefinitionDisplayName(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
            return string.Empty;

        SerializedProperty displayNameProperty = moduleProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return string.Empty;

        return displayNameProperty.stringValue;
    }

    /// <summary>
    /// Resolves the module kind of one serialized definition.
    /// /params moduleProperty Serialized module definition property.
    /// /returns Module kind, or Grunt when unavailable.
    /// </summary>
    public static EnemyPatternModuleKind ResolveModuleDefinitionKind(SerializedProperty moduleProperty)
    {
        if (moduleProperty == null)
            return EnemyPatternModuleKind.Grunt;

        SerializedProperty moduleKindProperty = moduleProperty.FindPropertyRelative("moduleKind");

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return EnemyPatternModuleKind.Grunt;

        return (EnemyPatternModuleKind)moduleKindProperty.enumValueIndex;
    }

    /// <summary>
    /// Sets the module ID of one serialized definition.
    /// /params moduleProperty Serialized module definition property.
    /// /params moduleId New module ID.
    /// /returns None.
    /// </summary>
    public static void SetModuleDefinitionId(SerializedProperty moduleProperty, string moduleId)
    {
        if (moduleProperty == null)
            return;

        SerializedProperty moduleIdProperty = moduleProperty.FindPropertyRelative("moduleId");

        if (moduleIdProperty == null)
            return;

        moduleIdProperty.stringValue = moduleId;
    }

    /// <summary>
    /// Sets the display name of one serialized definition.
    /// /params moduleProperty Serialized module definition property.
    /// /params displayName New display name.
    /// /returns None.
    /// </summary>
    public static void SetModuleDefinitionDisplayName(SerializedProperty moduleProperty, string displayName)
    {
        if (moduleProperty == null)
            return;

        SerializedProperty displayNameProperty = moduleProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return;

        displayNameProperty.stringValue = displayName;
    }

    /// <summary>
    /// Sets the module kind of one serialized definition.
    /// /params moduleProperty Serialized module definition property.
    /// /params moduleKind New module kind.
    /// /returns None.
    /// </summary>
    public static void SetModuleDefinitionKind(SerializedProperty moduleProperty, EnemyPatternModuleKind moduleKind)
    {
        if (moduleProperty == null)
            return;

        SerializedProperty moduleKindProperty = moduleProperty.FindPropertyRelative("moduleKind");

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return;

        moduleKindProperty.enumValueIndex = (int)moduleKind;
    }

    /// <summary>
    /// Sets the notes text of one serialized definition.
    /// /params moduleProperty Serialized module definition property.
    /// /params notes New notes text.
    /// /returns None.
    /// </summary>
    public static void SetModuleDefinitionNotes(SerializedProperty moduleProperty, string notes)
    {
        if (moduleProperty == null)
            return;

        SerializedProperty notesProperty = moduleProperty.FindPropertyRelative("notes");

        if (notesProperty == null)
            return;

        notesProperty.stringValue = notes;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Collects every module ID from one serialized catalog section except one optional excluded property path.
    /// /params sharedPresetSerializedObject Serialized shared preset that owns the catalog.
    /// /params definitionsPropertyName Serialized property name of the catalog.
    /// /params excludedPropertyPath Property path excluded from duplicate checks.
    /// /params moduleIds Destination hash set.
    /// /returns None.
    /// </summary>
    private static void CollectModuleIds(SerializedObject sharedPresetSerializedObject,
                                         string definitionsPropertyName,
                                         string excludedPropertyPath,
                                         HashSet<string> moduleIds)
    {
        if (sharedPresetSerializedObject == null || moduleIds == null)
            return;

        SerializedProperty definitionsProperty = sharedPresetSerializedObject.FindProperty(definitionsPropertyName);

        if (definitionsProperty == null)
            return;

        for (int moduleIndex = 0; moduleIndex < definitionsProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = definitionsProperty.GetArrayElementAtIndex(moduleIndex);

            if (moduleProperty == null)
                continue;

            if (!string.IsNullOrWhiteSpace(excludedPropertyPath) &&
                string.Equals(moduleProperty.propertyPath, excludedPropertyPath, StringComparison.Ordinal))
            {
                continue;
            }

            string moduleId = ResolveModuleDefinitionId(moduleProperty);

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            moduleIds.Add(moduleId.Trim());
        }
    }
    #endregion

    #endregion
}
