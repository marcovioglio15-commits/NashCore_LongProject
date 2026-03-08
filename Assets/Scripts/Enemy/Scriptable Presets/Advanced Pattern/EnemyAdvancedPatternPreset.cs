using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Stores modular advanced enemy behavior definitions and active loadout selection.
/// </summary>
[CreateAssetMenu(fileName = "EnemyAdvancedPatternPreset", menuName = "Enemy/Advanced Pattern Preset", order = 12)]
public sealed class EnemyAdvancedPatternPreset : ScriptableObject
{
    #region Constants
    private const string DefaultPatternId = "Pattern_DefaultGrunt";
    private const string DefaultModuleIdStationary = "Module_Stationary";
    private const string DefaultModuleIdGrunt = "Module_Grunt";
    private const string DefaultModuleIdWanderer = "Module_Wanderer";
    private const string DefaultModuleIdShooter = "Module_Shooter";
    private const string DefaultModuleIdDropItems = "Module_DropItems";
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this advanced pattern preset.")]
    [SerializeField] private string presetId;

    [Tooltip("Human-readable name shown in Enemy Management Tool.")]
    [SerializeField] private string presetName = "New Enemy Advanced Pattern Preset";

    [Tooltip("Short description of this advanced pattern preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this preset.")]
    [SerializeField] private string version = "1.0.0";

    [Tooltip("Legacy shooter projectile prefab fallback kept for backward compatibility with older presets.")]
    [FormerlySerializedAs("shooterProjectilePrefab")]
    [SerializeField] private GameObject legacyShooterProjectilePrefab;

    [Tooltip("Legacy shooter pool initial capacity fallback kept for backward compatibility with older presets.")]
    [FormerlySerializedAs("shooterProjectilePoolInitialCapacity")]
    [SerializeField] private int legacyShooterProjectilePoolInitialCapacity = 24;

    [Tooltip("Legacy shooter pool expand batch fallback kept for backward compatibility with older presets.")]
    [FormerlySerializedAs("shooterProjectilePoolExpandBatch")]
    [SerializeField] private int legacyShooterProjectilePoolExpandBatch = 8;

    [Header("Modules Definition")]
    [Tooltip("Reusable module catalog used by Pattern Assemble entries.")]
    [SerializeField] private List<EnemyPatternModuleDefinition> moduleDefinitions = new List<EnemyPatternModuleDefinition>();

    [Header("Pattern Assemble")]
    [Tooltip("Assembled patterns composed from module bindings.")]
    [SerializeField] private List<EnemyPatternDefinition> patterns = new List<EnemyPatternDefinition>();

    [Header("Pattern Loadout")]
    [Tooltip("List of active pattern IDs compiled at bake time. All selected patterns are merged.")]
    [SerializeField] private List<string> activePatternIds = new List<string>();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public GameObject ShooterProjectilePrefab
    {
        get
        {
            return legacyShooterProjectilePrefab;
        }
    }

    public int ShooterProjectilePoolInitialCapacity
    {
        get
        {
            return legacyShooterProjectilePoolInitialCapacity;
        }
    }

    public int ShooterProjectilePoolExpandBatch
    {
        get
        {
            return legacyShooterProjectilePoolExpandBatch;
        }
    }

    public IReadOnlyList<EnemyPatternModuleDefinition> ModuleDefinitions
    {
        get
        {
            return moduleDefinitions;
        }
    }

    public IReadOnlyList<EnemyPatternDefinition> Patterns
    {
        get
        {
            return patterns;
        }
    }

    public IReadOnlyList<string> ActivePatternIds
    {
        get
        {
            return activePatternIds;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one module definition by module ID.
    /// </summary>
    /// <param name="moduleId">Target module ID.</param>
    /// <returns>Resolved module definition instance or null when not found.</returns>
    public EnemyPatternModuleDefinition ResolveModuleDefinitionById(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return null;

        string normalizedModuleId = moduleId.Trim();

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            EnemyPatternModuleDefinition definition = moduleDefinitions[index];

            if (definition == null)
                continue;

            if (string.Equals(definition.ModuleId, normalizedModuleId, StringComparison.OrdinalIgnoreCase) == false)
                continue;

            return definition;
        }

        return null;
    }

    /// <summary>
    /// Resolves one assembled pattern by pattern ID.
    /// </summary>
    /// <param name="patternId">Target pattern ID.</param>
    /// <returns>Resolved pattern definition instance or null when not found.</returns>
    public EnemyPatternDefinition ResolvePatternById(string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
            return null;

        string normalizedPatternId = patternId.Trim();

        for (int index = 0; index < patterns.Count; index++)
        {
            EnemyPatternDefinition pattern = patterns[index];

            if (pattern == null)
                continue;

            if (string.Equals(pattern.PatternId, normalizedPatternId, StringComparison.OrdinalIgnoreCase) == false)
                continue;

            return pattern;
        }

        return null;
    }

    /// <summary>
    /// Validates metadata, module catalog, pattern assembly and active loadout.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "New Enemy Advanced Pattern Preset";

        if (moduleDefinitions == null)
            moduleDefinitions = new List<EnemyPatternModuleDefinition>();

        if (patterns == null)
            patterns = new List<EnemyPatternDefinition>();

        if (activePatternIds == null)
            activePatternIds = new List<string>();

        if (moduleDefinitions.Count <= 0)
            moduleDefinitions = BuildDefaultModuleDefinitions();

        if (patterns.Count <= 0)
            patterns = BuildDefaultPatternDefinitions();

        ValidateModuleDefinitions();
        ValidatePatternDefinitions();
        MigrateLegacyRuntimeProjectileToShooterPayload();
        NormalizeLoadoutIds();

        if (activePatternIds.Count <= 0 && patterns.Count > 0)
            activePatternIds.Add(patterns[0].PatternId);

        if (legacyShooterProjectilePoolInitialCapacity < 0)
            legacyShooterProjectilePoolInitialCapacity = 0;

        if (legacyShooterProjectilePoolExpandBatch < 1)
            legacyShooterProjectilePoolExpandBatch = 1;
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #region Defaults
    private static List<EnemyPatternModuleDefinition> BuildDefaultModuleDefinitions()
    {
        List<EnemyPatternModuleDefinition> definitions = new List<EnemyPatternModuleDefinition>();
        definitions.Add(CreateDefaultModule(DefaultModuleIdStationary, "Stationary", EnemyPatternModuleKind.Stationary, "Forces movement speed to zero."));
        definitions.Add(CreateDefaultModule(DefaultModuleIdGrunt, "Grunt", EnemyPatternModuleKind.Grunt, "Uses standard chase and separation from Brain settings."));
        definitions.Add(CreateDefaultModule(DefaultModuleIdWanderer, "Wanderer", EnemyPatternModuleKind.Wanderer, "Uses autonomous wandering behavior."));
        definitions.Add(CreateDefaultModule(DefaultModuleIdShooter, "Shooter", EnemyPatternModuleKind.Shooter, "Shoots periodic projectiles toward the player."));
        definitions.Add(CreateDefaultModule(DefaultModuleIdDropItems, "Drop Items", EnemyPatternModuleKind.DropItems, "Spawns collectible drops (for now experience drops) at enemy death."));
        return definitions;
    }

    private static EnemyPatternModuleDefinition CreateDefaultModule(string moduleId,
                                                                    string displayName,
                                                                    EnemyPatternModuleKind moduleKind,
                                                                    string notes)
    {
        EnemyPatternModuleDefinition definition = new EnemyPatternModuleDefinition();
        EnemyPatternModulePayloadData payloadData = new EnemyPatternModulePayloadData();
        definition.Configure(moduleId, displayName, moduleKind, notes, payloadData);
        definition.Validate();
        return definition;
    }

    private static List<EnemyPatternDefinition> BuildDefaultPatternDefinitions()
    {
        List<EnemyPatternDefinition> definitions = new List<EnemyPatternDefinition>();
        EnemyPatternDefinition defaultPattern = new EnemyPatternDefinition();
        EnemyPatternModuleBinding gruntBinding = new EnemyPatternModuleBinding();
        gruntBinding.Configure(DefaultModuleIdGrunt, true);
        gruntBinding.ConfigureOverride(false, new EnemyPatternModulePayloadData());
        defaultPattern.Configure(DefaultPatternId,
                                 "Default Grunt",
                                 "Baseline pattern with Grunt movement.",
                                 false);
        defaultPattern.ClearBindings();
        defaultPattern.AddBinding(gruntBinding);
        defaultPattern.Validate();
        definitions.Add(defaultPattern);
        return definitions;
    }
    #endregion

    #region Validation
    private void ValidateModuleDefinitions()
    {
        HashSet<string> moduleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            EnemyPatternModuleDefinition definition = moduleDefinitions[index];

            if (definition == null)
            {
                definition = new EnemyPatternModuleDefinition();
                moduleDefinitions[index] = definition;
            }

            definition.Validate();
            string normalizedId = NormalizeId(definition.ModuleId, "Module_Custom");
            string uniqueId = ResolveUniqueId(normalizedId, moduleIds, "Module_Custom");

            if (string.Equals(definition.ModuleId, uniqueId, StringComparison.Ordinal) == false)
            {
                definition.Configure(uniqueId,
                                     definition.DisplayName,
                                     definition.ModuleKind,
                                     definition.Notes,
                                     definition.Data);
                definition.Validate();
            }

            moduleIds.Add(uniqueId);
        }
    }

    private void ValidatePatternDefinitions()
    {
        HashSet<string> patternIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < patterns.Count; index++)
        {
            EnemyPatternDefinition pattern = patterns[index];

            if (pattern == null)
            {
                patterns.RemoveAt(index);
                index--;
                continue;
            }

            pattern.Validate();
            string normalizedId = NormalizeId(pattern.PatternId, "Pattern_Custom");
            string uniqueId = ResolveUniqueId(normalizedId, patternIds, "Pattern_Custom");

            if (string.Equals(pattern.PatternId, uniqueId, StringComparison.Ordinal) == false)
            {
                pattern.Configure(uniqueId,
                                  pattern.DisplayName,
                                  pattern.Description,
                                  pattern.Unreplaceable);
                pattern.Validate();
            }

            patternIds.Add(uniqueId);
        }
    }

    private void NormalizeLoadoutIds()
    {
        HashSet<string> validPatternIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < patterns.Count; index++)
        {
            EnemyPatternDefinition pattern = patterns[index];

            if (pattern == null)
                continue;

            validPatternIds.Add(pattern.PatternId);
        }

        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < activePatternIds.Count; index++)
        {
            string loadoutId = NormalizeId(activePatternIds[index], string.Empty);

            if (string.IsNullOrWhiteSpace(loadoutId))
            {
                activePatternIds.RemoveAt(index);
                index--;
                continue;
            }

            if (validPatternIds.Contains(loadoutId) == false)
            {
                activePatternIds.RemoveAt(index);
                index--;
                continue;
            }

            if (visited.Add(loadoutId) == false)
            {
                activePatternIds.RemoveAt(index);
                index--;
                continue;
            }

            activePatternIds[index] = loadoutId;
        }
    }

    private static string NormalizeId(string rawId, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rawId))
            return fallback;

        string normalized = rawId.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            return fallback;

        return normalized;
    }

    private static string ResolveUniqueId(string baseId, HashSet<string> usedIds, string fallbackBaseId)
    {
        string normalizedBaseId = NormalizeId(baseId, fallbackBaseId);

        if (usedIds == null)
            return normalizedBaseId;

        if (usedIds.Contains(normalizedBaseId) == false)
            return normalizedBaseId;

        int suffix = 2;

        while (true)
        {
            string indexedId = string.Format("{0}_{1}", normalizedBaseId, suffix);

            if (usedIds.Contains(indexedId) == false)
                return indexedId;

            suffix++;
        }
    }

    private void MigrateLegacyRuntimeProjectileToShooterPayload()
    {
        if (legacyShooterProjectilePrefab == null)
            return;

        for (int index = 0; index < moduleDefinitions.Count; index++)
        {
            EnemyPatternModuleDefinition moduleDefinition = moduleDefinitions[index];

            if (moduleDefinition == null)
                continue;

            if (moduleDefinition.ModuleKind != EnemyPatternModuleKind.Shooter)
                continue;

            EnemyPatternModulePayloadData payloadData = moduleDefinition.Data;

            if (payloadData == null || payloadData.Shooter == null || payloadData.Shooter.RuntimeProjectile == null)
                continue;

            EnemyShooterRuntimeProjectilePayload runtimePayload = payloadData.Shooter.RuntimeProjectile;

            if (runtimePayload.ProjectilePrefab != null)
                continue;

            runtimePayload.Configure(legacyShooterProjectilePrefab,
                                     legacyShooterProjectilePoolInitialCapacity,
                                     legacyShooterProjectilePoolExpandBatch);
            runtimePayload.Validate();
        }
    }
    #endregion

    #endregion
}
