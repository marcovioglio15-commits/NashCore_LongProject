using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds warning messages for unsupported or ambiguous advanced-pattern compositions without mutating authoring data.
/// </summary>
internal static class EnemyAdvancedPatternCompositionWarningUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Appends composition warnings for the selected preset to the provided container.
    /// panel: Owning panel that provides the selected preset.
    /// container: Target UI container that receives generated warning boxes.
    /// returns None.
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

    #region Collect
    private static HashSet<string> CollectWarnings(EnemyAdvancedPatternPreset preset)
    {
        HashSet<string> warnings = new HashSet<string>();

        if (preset == null)
            return warnings;

        if (preset.ActivePatternIds != null && preset.ActivePatternIds.Count > 1)
        {
            warnings.Add("Pattern Loadout currently merges multiple patterns at bake time. Prefer one active pattern that already contains one base movement module plus optional Shooter and Drop Items modules.");
        }

        IReadOnlyList<EnemyPatternDefinition> patterns = preset.Patterns;

        if (patterns == null)
            return warnings;

        for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
        {
            EnemyPatternDefinition pattern = patterns[patternIndex];

            if (pattern == null)
                continue;

            AnalyzePattern(preset, pattern, warnings);
        }

        return warnings;
    }

    private static void AnalyzePattern(EnemyAdvancedPatternPreset preset,
                                       EnemyPatternDefinition pattern,
                                       HashSet<string> warnings)
    {
        if (preset == null || pattern == null || warnings == null)
            return;

        int baseMovementCount = 0;
        int shooterCount = 0;
        int dropItemsCount = 0;
        bool usesStationary = false;
        List<string> movementLabels = new List<string>(4);
        IReadOnlyList<EnemyPatternModuleBinding> bindings = pattern.ModuleBindings;

        if (bindings == null)
            return;

        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            EnemyPatternModuleBinding binding = bindings[bindingIndex];

            if (binding == null || !binding.IsEnabled)
                continue;

            EnemyPatternModuleDefinition definition = preset.ResolveModuleDefinitionById(binding.ModuleId);

            if (definition == null)
                continue;

            EnemyPatternModulePayloadData payload = ResolvePayload(definition, binding);

            switch (definition.ModuleKind)
            {
                case EnemyPatternModuleKind.Stationary:
                    baseMovementCount++;
                    usesStationary = true;
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
            warnings.Add(string.Format("Pattern '{0}' does not contain a supported base movement module. Runtime falls back to Grunt, so prefer one explicit base movement: Grunt, Wanderer, DVD or Coward.", patternName));
        }

        if (baseMovementCount > 1)
        {
            warnings.Add(string.Format("Pattern '{0}' enables multiple base movement modules ({1}). Keep only one base movement at a time and layer Shooter / Drop Items on top.", patternName, string.Join(", ", movementLabels.ToArray())));
        }

        if (usesStationary)
        {
            warnings.Add(string.Format("Pattern '{0}' still uses Stationary. The current supported base movement flow is Grunt, Wanderer, DVD or Coward.", patternName));
        }

        if (shooterCount > 0 && baseMovementCount <= 0)
        {
            warnings.Add(string.Format("Pattern '{0}' contains Shooter but no explicit base movement module. Shooter inherits the movement of the assembled pattern, so add exactly one base movement module.", patternName));
        }

        if (dropItemsCount > 1)
        {
            warnings.Add(string.Format("Pattern '{0}' contains multiple Drop Items modules. Only one drop payload should define the assembled pattern to avoid ambiguous runtime results.", patternName));
        }
    }

    private static EnemyPatternModulePayloadData ResolvePayload(EnemyPatternModuleDefinition definition, EnemyPatternModuleBinding binding)
    {
        if (binding != null && binding.UseOverridePayload && binding.OverridePayload != null)
            return binding.OverridePayload;

        if (definition != null)
            return definition.Data;

        return null;
    }

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
