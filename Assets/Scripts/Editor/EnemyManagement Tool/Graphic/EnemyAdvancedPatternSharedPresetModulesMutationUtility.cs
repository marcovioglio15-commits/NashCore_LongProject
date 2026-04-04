using System;
using UnityEditor;

/// <summary>
/// Applies structural mutations to the shared module catalogs used by enemy Modules and Patterns presets.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyAdvancedPatternSharedPresetModulesMutationUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one new module definition to the requested catalog section.
    /// /params panel Owning panel used for rebuild callbacks.
    /// /params sharedPresetSerializedObject Serialized shared preset.
    /// /params sharedPreset Shared preset asset being edited.
    /// /params section Catalog section receiving the new definition.
    /// /returns None.
    /// </summary>
    public static void AddModuleDefinition(EnemyAdvancedPatternPresetsPanel panel,
                                           SerializedObject sharedPresetSerializedObject,
                                           EnemyModulesAndPatternsPreset sharedPreset,
                                           EnemyPatternModuleCatalogSection section)
    {
        ApplyModuleCatalogMutation(panel,
                                   sharedPresetSerializedObject,
                                   sharedPreset,
                                   section,
                                   "Add Enemy Shared Module Definition",
                                   definitionsProperty =>
                                   {
                                       int insertIndex = definitionsProperty.arraySize;
                                       definitionsProperty.arraySize = insertIndex + 1;
                                       SerializedProperty insertedProperty = definitionsProperty.GetArrayElementAtIndex(insertIndex);

                                       if (insertedProperty == null)
                                           return;

                                       string uniqueModuleId = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.GenerateUniqueModuleId(sharedPresetSerializedObject,
                                                                                                                                           EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveDefaultModuleIdPrefix(section),
                                                                                                                                           insertedProperty.propertyPath);
                                       EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.InitializeNewModuleDefinition(insertedProperty,
                                                                                                                             section,
                                                                                                                             uniqueModuleId,
                                                                                                                             EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveDefaultModuleDisplayName(section));
                                       string stateKey = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.BuildModuleCardStateKey(insertedProperty);
                                       ManagementToolFoldoutStateUtility.SetFoldoutState(stateKey, true);
                                   });
    }

    /// <summary>
    /// Duplicates one module definition inside the current catalog section.
    /// /params panel Owning panel used for rebuild callbacks.
    /// /params sharedPresetSerializedObject Serialized shared preset.
    /// /params sharedPreset Shared preset asset being edited.
    /// /params section Catalog section that owns the duplicated definition.
    /// /params moduleIndex Index of the source definition.
    /// /returns None.
    /// </summary>
    public static void DuplicateModuleDefinition(EnemyAdvancedPatternPresetsPanel panel,
                                                 SerializedObject sharedPresetSerializedObject,
                                                 EnemyModulesAndPatternsPreset sharedPreset,
                                                 EnemyPatternModuleCatalogSection section,
                                                 int moduleIndex)
    {
        ApplyModuleCatalogMutation(panel,
                                   sharedPresetSerializedObject,
                                   sharedPreset,
                                   section,
                                   "Duplicate Enemy Shared Module Definition",
                                   definitionsProperty =>
                                   {
                                       if (moduleIndex < 0 || moduleIndex >= definitionsProperty.arraySize)
                                           return;

                                       definitionsProperty.InsertArrayElementAtIndex(moduleIndex);
                                       definitionsProperty.MoveArrayElement(moduleIndex, moduleIndex + 1);
                                       int duplicatedIndex = moduleIndex + 1;
                                       SerializedProperty duplicatedProperty = definitionsProperty.GetArrayElementAtIndex(duplicatedIndex);

                                       if (duplicatedProperty == null)
                                           return;

                                       string baseModuleId = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionId(duplicatedProperty);
                                       string displayName = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionDisplayName(duplicatedProperty);
                                       string uniqueModuleId = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.GenerateUniqueModuleId(sharedPresetSerializedObject,
                                                                                                                                           baseModuleId,
                                                                                                                                           duplicatedProperty.propertyPath);

                                       if (string.IsNullOrWhiteSpace(displayName))
                                           displayName = EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveDefaultModuleDisplayName(section);

                                       EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.SetModuleDefinitionId(duplicatedProperty, uniqueModuleId);
                                       EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.SetModuleDefinitionDisplayName(duplicatedProperty, displayName + " Copy");
                                       string stateKey = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.BuildModuleCardStateKey(duplicatedProperty);
                                       ManagementToolFoldoutStateUtility.SetFoldoutState(stateKey, true);
                                   });
    }

    /// <summary>
    /// Deletes one module definition from the current catalog section.
    /// /params panel Owning panel used for rebuild callbacks.
    /// /params sharedPresetSerializedObject Serialized shared preset.
    /// /params sharedPreset Shared preset asset being edited.
    /// /params section Catalog section that owns the deleted definition.
    /// /params moduleIndex Index of the definition to remove.
    /// /returns None.
    /// </summary>
    public static void DeleteModuleDefinition(EnemyAdvancedPatternPresetsPanel panel,
                                              SerializedObject sharedPresetSerializedObject,
                                              EnemyModulesAndPatternsPreset sharedPreset,
                                              EnemyPatternModuleCatalogSection section,
                                              int moduleIndex)
    {
        ApplyModuleCatalogMutation(panel,
                                   sharedPresetSerializedObject,
                                   sharedPreset,
                                   section,
                                   "Delete Enemy Shared Module Definition",
                                   definitionsProperty =>
                                   {
                                       if (moduleIndex < 0 || moduleIndex >= definitionsProperty.arraySize)
                                           return;

                                       definitionsProperty.DeleteArrayElementAtIndex(moduleIndex);
                                   });
    }

    /// <summary>
    /// Moves one module definition inside its current catalog section.
    /// /params panel Owning panel used for rebuild callbacks.
    /// /params sharedPresetSerializedObject Serialized shared preset.
    /// /params sharedPreset Shared preset asset being edited.
    /// /params section Catalog section that owns the moved definition.
    /// /params fromIndex Source index.
    /// /params toIndex Destination index.
    /// /returns None.
    /// </summary>
    public static void MoveModuleDefinition(EnemyAdvancedPatternPresetsPanel panel,
                                            SerializedObject sharedPresetSerializedObject,
                                            EnemyModulesAndPatternsPreset sharedPreset,
                                            EnemyPatternModuleCatalogSection section,
                                            int fromIndex,
                                            int toIndex)
    {
        ApplyModuleCatalogMutation(panel,
                                   sharedPresetSerializedObject,
                                   sharedPreset,
                                   section,
                                   "Move Enemy Shared Module Definition",
                                   definitionsProperty =>
                                   {
                                       if (fromIndex < 0 || fromIndex >= definitionsProperty.arraySize)
                                           return;

                                       if (toIndex < 0 || toIndex >= definitionsProperty.arraySize)
                                           return;

                                       if (fromIndex == toIndex)
                                           return;

                                       definitionsProperty.MoveArrayElement(fromIndex, toIndex);
                                   });
    }

    /// <summary>
    /// Sets the foldout state of every module card currently visible under one catalog section filter.
    /// /params panel Owning panel that stores the active filters.
    /// /params sharedPresetSerializedObject Serialized shared preset.
    /// /params section Catalog section whose visible cards are being updated.
    /// /params expanded True to expand every visible card, otherwise false.
    /// /returns None.
    /// </summary>
    public static void SetAllModuleFoldoutStates(EnemyAdvancedPatternPresetsPanel panel,
                                                 SerializedObject sharedPresetSerializedObject,
                                                 EnemyPatternModuleCatalogSection section,
                                                 bool expanded)
    {
        if (panel == null || sharedPresetSerializedObject == null)
            return;

        SerializedProperty definitionsProperty = sharedPresetSerializedObject.FindProperty(EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveDefinitionsPropertyName(section));

        if (definitionsProperty == null)
            return;

        for (int moduleIndex = 0; moduleIndex < definitionsProperty.arraySize; moduleIndex++)
        {
            SerializedProperty moduleProperty = definitionsProperty.GetArrayElementAtIndex(moduleIndex);

            if (moduleProperty == null)
                continue;

            string moduleId = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionId(moduleProperty);
            string displayName = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.ResolveModuleDefinitionDisplayName(moduleProperty);

            if (!EnemyAdvancedPatternSharedPresetModulesListUtility.IsMatchingModuleFilters(panel, section, moduleId, displayName))
                continue;

            string stateKey = EnemyAdvancedPatternSharedPresetModuleDefinitionUtility.BuildModuleCardStateKey(moduleProperty);
            ManagementToolFoldoutStateUtility.SetFoldoutState(stateKey, expanded);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one explicit mutation to the requested catalog section through the shared preset serialized object.
    /// /params panel Owning panel used for rebuild callbacks.
    /// /params sharedPresetSerializedObject Serialized shared preset.
    /// /params sharedPreset Shared preset asset being edited.
    /// /params section Catalog section being mutated.
    /// /params undoLabel Undo label used for the mutation.
    /// /params mutation Mutation callback that receives the serialized array property.
    /// /returns None.
    /// </summary>
    private static void ApplyModuleCatalogMutation(EnemyAdvancedPatternPresetsPanel panel,
                                                   SerializedObject sharedPresetSerializedObject,
                                                   EnemyModulesAndPatternsPreset sharedPreset,
                                                   EnemyPatternModuleCatalogSection section,
                                                   string undoLabel,
                                                   Action<SerializedProperty> mutation)
    {
        if (panel == null || sharedPresetSerializedObject == null || sharedPreset == null || mutation == null)
            return;

        SerializedProperty definitionsProperty = sharedPresetSerializedObject.FindProperty(EnemyAdvancedPatternSharedPresetModuleCatalogUtility.ResolveDefinitionsPropertyName(section));

        if (definitionsProperty == null)
            return;

        Undo.RecordObject(sharedPreset, undoLabel);
        sharedPresetSerializedObject.Update();
        mutation(definitionsProperty);
        sharedPresetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(sharedPreset);
        EnemyManagementDraftSession.MarkDirty();
        panel.BuildActiveDetailsSection();
    }
    #endregion

    #endregion
}
