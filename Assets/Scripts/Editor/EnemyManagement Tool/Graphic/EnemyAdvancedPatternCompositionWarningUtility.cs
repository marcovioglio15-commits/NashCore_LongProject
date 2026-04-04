using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds warning messages for unsupported or ambiguous advanced-pattern compositions without mutating authoring data.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyAdvancedPatternCompositionWarningUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends composition warnings for the selected preset to the provided container.
    /// /params panel Owning panel that provides the selected preset.
    /// /params container Target UI container that receives generated warning boxes.
    /// /returns None.
    /// </summary>
    public static void AddWarnings(EnemyAdvancedPatternPresetsPanel panel, VisualElement container)
    {
        if (panel == null || container == null)
            return;

        EnemyAdvancedPatternPreset preset = panel.SelectedPreset;

        if (preset == null)
            return;

        HashSet<string> warnings = CollectWarnings(preset);

        foreach (string warning in warnings)
        {
            HelpBox warningBox = new HelpBox(warning, HelpBoxMessageType.Warning);
            warningBox.style.marginTop = 4f;
            container.Add(warningBox);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Collects all warnings implied by the current preset loadout source.
    /// /params preset Selected advanced-pattern preset.
    /// /returns The distinct warning set.
    /// </summary>
    private static HashSet<string> CollectWarnings(EnemyAdvancedPatternPreset preset)
    {
        HashSet<string> warnings = new HashSet<string>();

        if (preset == null)
            return warnings;

        if (preset.ActivePatternIds != null && preset.ActivePatternIds.Count > 1)
        {
            warnings.Add("Pattern Loadout currently contains multiple active pattern IDs. The current workflow expects one active shared pattern at a time.");
        }

        if (preset.ModulesAndPatternsPreset != null)
        {
            IReadOnlyList<EnemyModulesPatternDefinition> sharedPatterns = preset.ModulesAndPatternsPreset.Patterns;

            for (int index = 0; index < sharedPatterns.Count; index++)
                AnalyzeSharedPattern(preset, sharedPatterns[index], warnings);

            return warnings;
        }

        IReadOnlyList<EnemyPatternDefinition> legacyPatterns = preset.LegacyPatterns;

        for (int index = 0; index < legacyPatterns.Count; index++)
            AnalyzeLegacyPattern(preset, legacyPatterns[index], warnings);

        return warnings;
    }

    /// <summary>
    /// Analyzes one shared assembled pattern against the new category model.
    /// /params preset Selected advanced-pattern preset.
    /// /params pattern Shared assembled pattern to inspect.
    /// /params warnings Mutable warning set.
    /// /returns None.
    /// </summary>
    private static void AnalyzeSharedPattern(EnemyAdvancedPatternPreset preset,
                                             EnemyModulesPatternDefinition pattern,
                                             HashSet<string> warnings)
    {
        if (preset == null || pattern == null || warnings == null)
            return;

        string patternName = string.IsNullOrWhiteSpace(pattern.DisplayName) ? pattern.PatternId : pattern.DisplayName;
        EnemyPatternCoreMovementAssembly coreMovement = pattern.CoreMovement;

        if (coreMovement == null || coreMovement.Binding == null || !coreMovement.Binding.IsEnabled)
        {
            warnings.Add(string.Format("Shared pattern '{0}' is missing an enabled Core Movement module.", patternName));
        }
        else
        {
            EnemyPatternModuleDefinition coreDefinition = preset.ResolveModuleDefinitionById(coreMovement.Binding.ModuleId);

            if (coreDefinition == null)
            {
                warnings.Add(string.Format("Shared pattern '{0}' references a Core Movement module ID that cannot be resolved.", patternName));
            }
            else if (!EnemyAdvancedPatternDrawerUtility.IsModuleKindAllowedInCatalogSection(coreDefinition.ModuleKind, EnemyPatternModuleCatalogSection.CoreMovement))
            {
                warnings.Add(string.Format("Shared pattern '{0}' assigns '{1}' to Core Movement, but that module kind does not belong to the Core Movement category.", patternName, coreDefinition.ModuleKind));
            }
        }

        AnalyzeOptionalCategory(patternName,
                                pattern.ShortRangeInteraction != null && pattern.ShortRangeInteraction.IsEnabled,
                                pattern.ShortRangeInteraction != null ? pattern.ShortRangeInteraction.Binding : null,
                                EnemyPatternModuleCatalogSection.ShortRangeInteraction,
                                preset,
                                warnings,
                                "Short-Range Interaction");
        AnalyzeOptionalCategory(patternName,
                                pattern.WeaponInteraction != null && pattern.WeaponInteraction.IsEnabled,
                                pattern.WeaponInteraction != null ? pattern.WeaponInteraction.Binding : null,
                                EnemyPatternModuleCatalogSection.WeaponInteraction,
                                preset,
                                warnings,
                                "Weapon Interaction");
        AnalyzeOptionalCategory(patternName,
                                pattern.DropItems != null && pattern.DropItems.IsEnabled,
                                pattern.DropItems != null ? pattern.DropItems.Binding : null,
                                EnemyPatternModuleCatalogSection.DropItems,
                                preset,
                                warnings,
                                "Drop Items");
    }

    /// <summary>
    /// Adds warnings for one optional shared pattern category.
    /// /params patternName Display name of the analyzed pattern.
    /// /params categoryEnabled Whether the category is enabled.
    /// /params binding Optional category binding.
    /// /params section Expected catalog section for the category.
    /// /params preset Selected advanced-pattern preset.
    /// /params warnings Mutable warning set.
    /// /params categoryLabel Display label of the category.
    /// /returns None.
    /// </summary>
    private static void AnalyzeOptionalCategory(string patternName,
                                                bool categoryEnabled,
                                                EnemyPatternModuleBinding binding,
                                                EnemyPatternModuleCatalogSection section,
                                                EnemyAdvancedPatternPreset preset,
                                                HashSet<string> warnings,
                                                string categoryLabel)
    {
        if (!categoryEnabled)
            return;

        if (binding == null || !binding.IsEnabled)
        {
            warnings.Add(string.Format("Shared pattern '{0}' enables {1} but no enabled module binding is assigned.", patternName, categoryLabel));
            return;
        }

        EnemyPatternModuleDefinition definition = preset.ResolveModuleDefinitionById(binding.ModuleId);

        if (definition == null)
        {
            warnings.Add(string.Format("Shared pattern '{0}' enables {1} but its module ID cannot be resolved.", patternName, categoryLabel));
            return;
        }

        if (!EnemyAdvancedPatternDrawerUtility.IsModuleKindAllowedInCatalogSection(definition.ModuleKind, section))
        {
            warnings.Add(string.Format("Shared pattern '{0}' enables {1} with module kind '{2}', which does not belong to that category.", patternName, categoryLabel, definition.ModuleKind));
        }
    }

    /// <summary>
    /// Analyzes one hidden legacy pattern for backward-compatible warnings.
    /// /params preset Selected advanced-pattern preset.
    /// /params pattern Legacy pattern to inspect.
    /// /params warnings Mutable warning set.
    /// /returns None.
    /// </summary>
    private static void AnalyzeLegacyPattern(EnemyAdvancedPatternPreset preset,
                                             EnemyPatternDefinition pattern,
                                             HashSet<string> warnings)
    {
        if (preset == null || pattern == null || warnings == null)
            return;

        int baseMovementCount = 0;
        int shooterCount = 0;
        int dropItemsCount = 0;
        List<string> movementLabels = new List<string>(4);
        IReadOnlyList<EnemyPatternModuleBinding> bindings = pattern.ModuleBindings;

        if (bindings == null)
            return;

        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            EnemyPatternModuleBinding binding = bindings[bindingIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            EnemyPatternModuleDefinition definition = preset.ResolveLegacyModuleDefinitionById(binding.ModuleId);

            if (definition == null)
                continue;

            EnemyPatternModulePayloadData payload = ResolvePayload(definition, binding);

            switch (definition.ModuleKind)
            {
                case EnemyPatternModuleKind.Stationary:
                    baseMovementCount++;
                    movementLabels.Add("Stationary");
                    break;

                case EnemyPatternModuleKind.Grunt:
                    baseMovementCount++;
                    movementLabels.Add("Grunt");
                    break;

                case EnemyPatternModuleKind.Wanderer:
                    baseMovementCount++;
                    movementLabels.Add(ResolveWandererLabel(payload));
                    break;

                case EnemyPatternModuleKind.Coward:
                    baseMovementCount++;
                    movementLabels.Add("Coward");
                    break;

                case EnemyPatternModuleKind.Shooter:
                    shooterCount++;
                    break;

                case EnemyPatternModuleKind.DropItems:
                    dropItemsCount++;
                    break;
            }
        }

        string patternName = string.IsNullOrWhiteSpace(pattern.DisplayName) ? pattern.PatternId : pattern.DisplayName;

        if (baseMovementCount <= 0)
        {
            warnings.Add(string.Format("Legacy pattern '{0}' does not contain any movement module. Runtime falls back to Grunt.", patternName));
        }

        if (baseMovementCount > 1)
        {
            warnings.Add(string.Format("Legacy pattern '{0}' still combines multiple movement modules ({1}). Rebuild it with one Core Movement plus optional Short-Range / Weapon categories.", patternName, string.Join(", ", movementLabels.ToArray())));
        }

        if (shooterCount > 1)
        {
            warnings.Add(string.Format("Legacy pattern '{0}' contains multiple Shooter modules. Shared Weapon Interaction expects one module slot.", patternName));
        }

        if (dropItemsCount > 1)
        {
            warnings.Add(string.Format("Legacy pattern '{0}' contains multiple Drop Items modules. Shared Drop Items expects one module slot.", patternName));
        }
    }

    /// <summary>
    /// Resolves the payload block used by one legacy binding after considering its optional override payload.
    /// /params definition Source module definition.
    /// /params binding Source binding.
    /// /returns The effective payload block.
    /// </summary>
    private static EnemyPatternModulePayloadData ResolvePayload(EnemyPatternModuleDefinition definition, EnemyPatternModuleBinding binding)
    {
        if (binding != null && binding.UseOverridePayload && binding.OverridePayload != null)
            return binding.OverridePayload;

        if (definition != null)
            return definition.Data;

        return null;
    }

    /// <summary>
    /// Resolves a user-facing Wanderer label from the provided payload.
    /// /params payload Effective payload block of the module.
    /// /returns The resolved Wanderer display label.
    /// </summary>
    private static string ResolveWandererLabel(EnemyPatternModulePayloadData payload)
    {
        if (payload == null || payload.Wanderer == null)
            return "Wanderer";

        if (payload.Wanderer.Mode == EnemyWandererMode.Dvd)
            return "DVD";

        return "Wanderer";
    }
    #endregion

    #endregion
}
