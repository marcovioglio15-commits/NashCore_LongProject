using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Supports entry cards, coverage validation and mutations for active and passive power-up definitions.
/// </summary>
public static class PlayerPowerUpsPresetsPanelEntriesSupportUtility
{
    #region Methods

    #region Mutations
    public static void AddPowerUpDefinition(PlayerPowerUpsPresetsPanel panel, bool isActiveSection)
    {
        string undoLabel = isActiveSection ? "Add Active Power Up" : "Add Passive Power Up";

        ApplyPowerUpDefinitionsMutation(panel, undoLabel, isActiveSection, powerUpsProperty =>
        {
            int insertIndex = powerUpsProperty.arraySize;
            powerUpsProperty.arraySize = insertIndex + 1;
            SerializedProperty insertedProperty = powerUpsProperty.GetArrayElementAtIndex(insertIndex);
            string basePowerUpId = isActiveSection ? "ActivePowerUpCustom" : "PassivePowerUpCustom";
            string uniquePowerUpId = PlayerPowerUpsPresetsPanelEntriesInitializationUtility.GenerateUniquePowerUpId(powerUpsProperty, basePowerUpId, insertIndex);
            string defaultDisplayName = isActiveSection ? "New Active Power Up" : "New Passive Power Up";
            PlayerPowerUpsPresetsPanelEntriesInitializationUtility.InitializeNewPowerUpDefinition(panel, insertedProperty, uniquePowerUpId, defaultDisplayName);
            Dictionary<string, bool> foldoutStateByKey = ResolvePowerUpFoldoutStateMap(panel, isActiveSection);
            string foldoutKey = BuildPowerUpFoldoutStateKey(isActiveSection, uniquePowerUpId, insertIndex);
            foldoutStateByKey[foldoutKey] = true;
        });
    }

    public static void DuplicatePowerUpDefinition(PlayerPowerUpsPresetsPanel panel, bool isActiveSection, int sourceIndex)
    {
        string undoLabel = isActiveSection ? "Duplicate Active Power Up" : "Duplicate Passive Power Up";

        ApplyPowerUpDefinitionsMutation(panel, undoLabel, isActiveSection, powerUpsProperty =>
        {
            if (sourceIndex < 0 || sourceIndex >= powerUpsProperty.arraySize)
                return;

            powerUpsProperty.InsertArrayElementAtIndex(sourceIndex);
            powerUpsProperty.MoveArrayElement(sourceIndex, sourceIndex + 1);
            int duplicatedIndex = sourceIndex + 1;
            SerializedProperty duplicatedProperty = powerUpsProperty.GetArrayElementAtIndex(duplicatedIndex);
            string basePowerUpId = ResolvePowerUpDefinitionId(duplicatedProperty);
            string copiedDisplayName = ResolvePowerUpDefinitionDisplayName(duplicatedProperty);
            string uniquePowerUpId = PlayerPowerUpsPresetsPanelEntriesInitializationUtility.GenerateUniquePowerUpId(powerUpsProperty, basePowerUpId, duplicatedIndex);
            PlayerPowerUpsPresetsPanelEntriesInitializationUtility.SetPowerUpDefinitionId(duplicatedProperty, uniquePowerUpId);

            if (string.IsNullOrWhiteSpace(copiedDisplayName))
                copiedDisplayName = isActiveSection ? "New Active Power Up" : "New Passive Power Up";

            PlayerPowerUpsPresetsPanelEntriesInitializationUtility.SetPowerUpDefinitionDisplayName(duplicatedProperty, copiedDisplayName + " Copy");
            Dictionary<string, bool> foldoutStateByKey = ResolvePowerUpFoldoutStateMap(panel, isActiveSection);
            string foldoutKey = BuildPowerUpFoldoutStateKey(isActiveSection, uniquePowerUpId, duplicatedIndex);
            foldoutStateByKey[foldoutKey] = true;
        });
    }

    public static void DeletePowerUpDefinition(PlayerPowerUpsPresetsPanel panel, bool isActiveSection, int index)
    {
        string undoLabel = isActiveSection ? "Delete Active Power Up" : "Delete Passive Power Up";

        ApplyPowerUpDefinitionsMutation(panel, undoLabel, isActiveSection, powerUpsProperty =>
        {
            if (index < 0 || index >= powerUpsProperty.arraySize)
                return;

            powerUpsProperty.DeleteArrayElementAtIndex(index);
        });
    }

    public static void MovePowerUpDefinition(PlayerPowerUpsPresetsPanel panel, bool isActiveSection, int fromIndex, int toIndex)
    {
        string undoLabel = isActiveSection ? "Move Active Power Up" : "Move Passive Power Up";

        ApplyPowerUpDefinitionsMutation(panel, undoLabel, isActiveSection, powerUpsProperty =>
        {
            if (fromIndex < 0 || fromIndex >= powerUpsProperty.arraySize)
                return;

            if (toIndex < 0 || toIndex >= powerUpsProperty.arraySize)
                return;

            if (fromIndex == toIndex)
                return;

            powerUpsProperty.MoveArrayElement(fromIndex, toIndex);
        });
    }

    public static void SetAllPowerUpFoldoutStates(PlayerPowerUpsPresetsPanel panel, bool isActiveSection, bool expanded)
    {
        if (panel.presetSerializedObject == null)
            return;

        string propertyName = ResolvePowerUpsPropertyName(isActiveSection);
        SerializedProperty powerUpsProperty = panel.presetSerializedObject.FindProperty(propertyName);

        if (powerUpsProperty == null)
            return;

        Dictionary<string, bool> foldoutStateByKey = ResolvePowerUpFoldoutStateMap(panel, isActiveSection);
        string powerUpIdFilterValue = isActiveSection ? panel.activePowerUpIdFilterText : panel.passivePowerUpIdFilterText;
        string displayNameFilterValue = isActiveSection ? panel.activePowerUpDisplayNameFilterText : panel.passivePowerUpDisplayNameFilterText;

        for (int powerUpIndex = 0; powerUpIndex < powerUpsProperty.arraySize; powerUpIndex++)
        {
            SerializedProperty powerUpProperty = powerUpsProperty.GetArrayElementAtIndex(powerUpIndex);

            if (powerUpProperty == null)
                continue;

            string powerUpId = ResolvePowerUpDefinitionId(powerUpProperty);
            string displayName = ResolvePowerUpDefinitionDisplayName(powerUpProperty);

            if (!IsMatchingPowerUpFilters(powerUpId, displayName, powerUpIdFilterValue, displayNameFilterValue))
                continue;

            string foldoutKey = BuildPowerUpFoldoutStateKey(isActiveSection, powerUpId, powerUpIndex);

            if (expanded)
            {
                foldoutStateByKey[foldoutKey] = true;
                continue;
            }

            foldoutStateByKey.Remove(foldoutKey);
        }
    }

    private static void ApplyPowerUpDefinitionsMutation(PlayerPowerUpsPresetsPanel panel, string undoLabel, bool isActiveSection, Action<SerializedProperty> mutation)
    {
        if (panel.presetSerializedObject == null || mutation == null)
            return;

        string propertyName = ResolvePowerUpsPropertyName(isActiveSection);
        SerializedProperty powerUpsProperty = panel.presetSerializedObject.FindProperty(propertyName);

        if (powerUpsProperty == null)
            return;

        if (panel.selectedPreset != null)
            Undo.RecordObject(panel.selectedPreset, undoLabel);

        panel.presetSerializedObject.Update();
        mutation(powerUpsProperty);
        panel.presetSerializedObject.ApplyModifiedProperties();
        PlayerManagementDraftSession.MarkDirty();
        panel.BuildActiveSection();
    }
    #endregion

    #region Lookup
    public static string ResolvePowerUpsPropertyName(bool isActiveSection)
    {
        return isActiveSection ? "activePowerUps" : "passivePowerUps";
    }

    public static Dictionary<string, bool> ResolvePowerUpFoldoutStateMap(PlayerPowerUpsPresetsPanel panel, bool isActiveSection)
    {
        return isActiveSection ? panel.activePowerUpFoldoutStateByKey : panel.passivePowerUpFoldoutStateByKey;
    }

    public static void PruneFoldoutStateMap(Dictionary<string, bool> foldoutStateByKey, HashSet<string> validStateKeys)
    {
        if (foldoutStateByKey == null || validStateKeys == null)
            return;

        List<string> keysToRemove = new List<string>();

        foreach (KeyValuePair<string, bool> entry in foldoutStateByKey)
        {
            if (validStateKeys.Contains(entry.Key))
                continue;

            keysToRemove.Add(entry.Key);
        }

        for (int index = 0; index < keysToRemove.Count; index++)
            foldoutStateByKey.Remove(keysToRemove[index]);
    }

    public static bool ResolvePowerUpFoldoutState(Dictionary<string, bool> foldoutStateByKey, string foldoutStateKey)
    {
        if (foldoutStateByKey == null || string.IsNullOrWhiteSpace(foldoutStateKey))
            return false;

        bool isExpanded;

        if (foldoutStateByKey.TryGetValue(foldoutStateKey, out isExpanded))
            return isExpanded;

        return false;
    }

    public static bool IsMatchingPowerUpFilters(string powerUpId, string displayName, string powerUpIdFilterValue, string displayNameFilterValue)
    {
        if (!string.IsNullOrWhiteSpace(powerUpIdFilterValue))
        {
            string resolvedPowerUpId = string.IsNullOrWhiteSpace(powerUpId) ? string.Empty : powerUpId;

            if (resolvedPowerUpId.IndexOf(powerUpIdFilterValue, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(displayNameFilterValue))
        {
            string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;

            if (resolvedDisplayName.IndexOf(displayNameFilterValue, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    public static string BuildPowerUpCardTitle(int powerUpIndex, string powerUpId, string displayName, int bindingCount, bool unreplaceable)
    {
        string resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? "<Unnamed>" : displayName.Trim();
        string resolvedPowerUpId = string.IsNullOrWhiteSpace(powerUpId) ? "<No ID>" : powerUpId.Trim();
        string lockTag = unreplaceable ? " | Unreplaceable" : string.Empty;
        return string.Format("#{0:D2}  {1}  ({2})  [{3} Modules{4}]",
                             powerUpIndex + 1,
                             resolvedDisplayName,
                             resolvedPowerUpId,
                             bindingCount,
                             lockTag);
    }

    public static string BuildPowerUpFoldoutStateKey(bool isActiveSection, string powerUpId, int powerUpIndex)
    {
        string prefix = isActiveSection ? "Active:" : "Passive:";
        string normalizedPowerUpId = string.IsNullOrWhiteSpace(powerUpId) ? "<NoId>" : powerUpId.Trim();
        return string.Format("{0}Index_{1}:Id_{2}", prefix, powerUpIndex, normalizedPowerUpId);
    }

    public static string ResolvePowerUpDefinitionId(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return string.Empty;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");

        if (commonDataProperty == null)
            return string.Empty;

        SerializedProperty powerUpIdProperty = commonDataProperty.FindPropertyRelative("powerUpId");

        if (powerUpIdProperty == null)
            return string.Empty;

        return powerUpIdProperty.stringValue;
    }

    public static string ResolvePowerUpDefinitionDisplayName(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return string.Empty;

        SerializedProperty commonDataProperty = powerUpProperty.FindPropertyRelative("commonData");

        if (commonDataProperty == null)
            return string.Empty;

        SerializedProperty displayNameProperty = commonDataProperty.FindPropertyRelative("displayName");

        if (displayNameProperty == null)
            return string.Empty;

        return displayNameProperty.stringValue;
    }

    public static int ResolvePowerUpDefinitionBindingCount(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return 0;

        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null)
            return 0;

        return moduleBindingsProperty.arraySize;
    }

    public static bool ResolvePowerUpDefinitionUnreplaceable(SerializedProperty powerUpProperty)
    {
        if (powerUpProperty == null)
            return false;

        SerializedProperty unreplaceableProperty = powerUpProperty.FindPropertyRelative("unreplaceable");

        if (unreplaceableProperty == null)
            return false;

        return unreplaceableProperty.boolValue;
    }
    #endregion

    #region Presentation
    public static void UpdatePowerUpCardPresentation(SerializedProperty powerUpProperty,
                                                     int powerUpIndex,
                                                     bool isActiveSection,
                                                     Foldout foldout,
                                                     HelpBox coverageWarningBox,
                                                     Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById)
    {
        if (powerUpProperty == null)
            return;

        if (foldout == null)
            return;

        string powerUpId = ResolvePowerUpDefinitionId(powerUpProperty);
        string displayName = ResolvePowerUpDefinitionDisplayName(powerUpProperty);
        int bindingCount = ResolvePowerUpDefinitionBindingCount(powerUpProperty);
        bool unreplaceable = ResolvePowerUpDefinitionUnreplaceable(powerUpProperty);
        foldout.text = BuildPowerUpCardTitle(powerUpIndex, powerUpId, displayName, bindingCount, unreplaceable);

        if (coverageWarningBox == null)
            return;

        string coverageWarning = BuildPowerUpCoverageWarning(powerUpProperty, isActiveSection, moduleCatalogById);

        if (string.IsNullOrWhiteSpace(coverageWarning))
        {
            coverageWarningBox.style.display = DisplayStyle.None;
            coverageWarningBox.text = string.Empty;
            return;
        }

        coverageWarningBox.style.display = DisplayStyle.Flex;
        coverageWarningBox.text = coverageWarning;
    }

    private static string BuildPowerUpCoverageWarning(SerializedProperty powerUpProperty,
                                                      bool isActiveSection,
                                                      Dictionary<string, PowerUpModuleCatalogEntry> moduleCatalogById)
    {
        if (powerUpProperty == null)
            return string.Empty;

        SerializedProperty moduleBindingsProperty = powerUpProperty.FindPropertyRelative("moduleBindings");

        if (moduleBindingsProperty == null || moduleBindingsProperty.arraySize <= 0)
            return string.Empty;

        HashSet<PowerUpModuleKind> moduleKinds = new HashSet<PowerUpModuleKind>();
        List<string> unresolvedModuleIds = new List<string>();
        List<string> unsupportedModuleKinds = new List<string>();

        for (int bindingIndex = 0; bindingIndex < moduleBindingsProperty.arraySize; bindingIndex++)
        {
            SerializedProperty bindingProperty = moduleBindingsProperty.GetArrayElementAtIndex(bindingIndex);

            if (bindingProperty == null)
                continue;

            SerializedProperty isEnabledProperty = bindingProperty.FindPropertyRelative("isEnabled");

            if (isEnabledProperty != null && !isEnabledProperty.boolValue)
                continue;

            string moduleId = ModularPowerUpBindingDrawerUtility.ResolveBindingModuleId(bindingProperty);

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            string normalizedModuleId = moduleId.Trim();
            PowerUpModuleCatalogEntry moduleEntry;

            if (!PowerUpModuleCatalogUtility.TryResolveModuleInfo(moduleCatalogById, normalizedModuleId, out moduleEntry))
            {
                PlayerPowerUpsPresetsPanelEntriesInitializationUtility.AddUniqueString(unresolvedModuleIds, normalizedModuleId);
                continue;
            }

            moduleKinds.Add(moduleEntry.ModuleKind);

            if (IsModuleKindSupportedInSection(moduleEntry.ModuleKind, isActiveSection))
                continue;

            PlayerPowerUpsPresetsPanelEntriesInitializationUtility.AddUniqueString(unsupportedModuleKinds, PowerUpModuleEnumDescriptions.FormatModuleKindOption(moduleEntry.ModuleKind));
        }

        List<string> warningLines = new List<string>();

        if (unresolvedModuleIds.Count > 0)
            warningLines.Add("Missing module IDs in catalog: " + string.Join(", ", unresolvedModuleIds));

        if (unsupportedModuleKinds.Count > 0)
        {
            string unsupportedPrefix = isActiveSection ? "Ignored in Active runtime: " : "Ignored in Passive runtime: ";
            warningLines.Add(unsupportedPrefix + string.Join(", ", unsupportedModuleKinds));
        }

        string contextWarning = isActiveSection ? BuildActiveCoverageWarning(moduleKinds) : BuildPassiveCoverageWarning(moduleKinds);

        if (!string.IsNullOrWhiteSpace(contextWarning))
            warningLines.Add(contextWarning);

        if (warningLines.Count <= 0)
            return string.Empty;

        return string.Join("\n", warningLines);
    }

    private static bool IsModuleKindSupportedInSection(PowerUpModuleKind moduleKind, bool isActiveSection)
    {
        if (isActiveSection)
        {
            switch (moduleKind)
            {
                case PowerUpModuleKind.TriggerPress:
                case PowerUpModuleKind.TriggerRelease:
                case PowerUpModuleKind.TriggerHoldCharge:
                case PowerUpModuleKind.GateResource:
                case PowerUpModuleKind.StateSuppressShooting:
                case PowerUpModuleKind.ProjectilesPatternCone:
                case PowerUpModuleKind.ProjectilesTuning:
                case PowerUpModuleKind.SpawnObject:
                case PowerUpModuleKind.DeathExplosion:
                case PowerUpModuleKind.Dash:
                case PowerUpModuleKind.TimeDilationEnemies:
                case PowerUpModuleKind.Heal:
                    return true;
                default:
                    return false;
            }
        }

        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerEvent:
            case PowerUpModuleKind.GateResource:
            case PowerUpModuleKind.Heal:
            case PowerUpModuleKind.ProjectilesPatternCone:
            case PowerUpModuleKind.ProjectilesTuning:
            case PowerUpModuleKind.SpawnTrailSegment:
            case PowerUpModuleKind.AreaTickApplyElement:
            case PowerUpModuleKind.DeathExplosion:
            case PowerUpModuleKind.OrbitalProjectiles:
            case PowerUpModuleKind.BouncingProjectiles:
            case PowerUpModuleKind.ProjectileSplit:
                return true;
            default:
                return false;
        }
    }

    private static string BuildActiveCoverageWarning(HashSet<PowerUpModuleKind> moduleKinds)
    {
        if (moduleKinds == null || moduleKinds.Count <= 0)
            return string.Empty;

        List<string> warningLines = new List<string>();
        bool hasHoldCharge = moduleKinds.Contains(PowerUpModuleKind.TriggerHoldCharge);
        bool hasShotgunPattern = moduleKinds.Contains(PowerUpModuleKind.ProjectilesPatternCone);
        bool hasSpawnObject = moduleKinds.Contains(PowerUpModuleKind.SpawnObject);
        bool hasDash = moduleKinds.Contains(PowerUpModuleKind.Dash);
        bool hasBulletTime = moduleKinds.Contains(PowerUpModuleKind.TimeDilationEnemies);
        bool hasHeal = moduleKinds.Contains(PowerUpModuleKind.Heal);
        bool hasDeathExplosion = moduleKinds.Contains(PowerUpModuleKind.DeathExplosion);
        bool hasTriggerEvent = moduleKinds.Contains(PowerUpModuleKind.TriggerEvent);
        bool hasTriggerPress = moduleKinds.Contains(PowerUpModuleKind.TriggerPress);
        bool hasTriggerRelease = moduleKinds.Contains(PowerUpModuleKind.TriggerRelease);
        int executeKindCount = 0;

        if (hasHoldCharge)
            executeKindCount += 1;

        if (hasShotgunPattern)
            executeKindCount += 1;

        if (hasSpawnObject)
            executeKindCount += 1;

        if (hasDash)
            executeKindCount += 1;

        if (hasBulletTime)
            executeKindCount += 1;

        if (hasHeal)
            executeKindCount += 1;

        if (executeKindCount == 0)
            warningLines.Add("No execute module selected. This active power up compiles as undefined.");

        if (executeKindCount > 1)
            warningLines.Add("Multiple execute modules found. Runtime priority is: TriggerHoldCharge > ProjectilesPatternCone > SpawnObject > Dash > TimeDilationEnemies > Heal.");

        if (hasDeathExplosion && !hasSpawnObject)
            warningLines.Add("DeathExplosion is ignored unless SpawnObject is also bound.");

        if (hasTriggerEvent)
            warningLines.Add("TriggerEvent currently has no active runtime consumer.");

        if (hasTriggerPress && hasTriggerRelease)
            warningLines.Add("TriggerPress and TriggerRelease are both bound. Runtime uses OnPress activation.");

        if (warningLines.Count <= 0)
            return string.Empty;

        return string.Join("\n", warningLines);
    }

    private static string BuildPassiveCoverageWarning(HashSet<PowerUpModuleKind> moduleKinds)
    {
        if (moduleKinds == null || moduleKinds.Count <= 0)
            return string.Empty;

        List<string> warningLines = new List<string>();
        bool hasTrail = moduleKinds.Contains(PowerUpModuleKind.SpawnTrailSegment) || moduleKinds.Contains(PowerUpModuleKind.AreaTickApplyElement);
        bool hasExplosion = moduleKinds.Contains(PowerUpModuleKind.DeathExplosion);
        bool hasOrbit = moduleKinds.Contains(PowerUpModuleKind.OrbitalProjectiles);
        bool hasBounce = moduleKinds.Contains(PowerUpModuleKind.BouncingProjectiles);
        bool hasSplit = moduleKinds.Contains(PowerUpModuleKind.ProjectileSplit);
        bool hasShotgun = moduleKinds.Contains(PowerUpModuleKind.ProjectilesPatternCone) || moduleKinds.Contains(PowerUpModuleKind.ProjectilesTuning);
        bool hasHeal = moduleKinds.Contains(PowerUpModuleKind.Heal);
        bool hasGateResource = moduleKinds.Contains(PowerUpModuleKind.GateResource);
        bool hasTriggerEvent = moduleKinds.Contains(PowerUpModuleKind.TriggerEvent);
        bool hasAnyPassiveRuntimeConsumer = hasTrail || hasExplosion || hasOrbit || hasBounce || hasSplit || hasShotgun || hasHeal;

        if (!hasAnyPassiveRuntimeConsumer)
            warningLines.Add("No passive runtime module found. This passive power up compiles as undefined.");

        if (hasTriggerEvent && !hasExplosion && !hasSplit && !hasHeal)
            warningLines.Add("TriggerEvent has no passive consumer without DeathExplosion, ProjectileSplit, or Heal.");

        if (hasGateResource && !hasHeal)
            warningLines.Add("GateResource in Passive currently only contributes cooldown data to Heal modules.");

        if (warningLines.Count <= 0)
            return string.Empty;

        return string.Join("\n", warningLines);
    }
    #endregion

    #endregion
}
