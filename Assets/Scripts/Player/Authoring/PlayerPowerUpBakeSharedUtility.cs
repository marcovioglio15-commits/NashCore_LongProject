using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Shared lookup, prefab validation and VFX bake helpers used by player power-up bake utilities.
/// </summary>
public static class PlayerPowerUpBakeSharedUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds VFX cap settings from player authoring values.
    ///  authoring: Source authoring component.
    /// returns Sanitized VFX cap config.
    /// </summary>
    public static PlayerPowerUpVfxCapConfig BuildPowerUpVfxCapConfig(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return default;

        return new PlayerPowerUpVfxCapConfig
        {
            MaxSamePrefabPerCell = math.max(0, authoring.MaxIdenticalOneShotVfxPerCell),
            CellSize = math.max(0.1f, authoring.OneShotVfxCellSize),
            MaxAttachedSamePrefabPerTarget = math.max(0, authoring.MaxAttachedElementalVfxPerTarget),
            MaxActiveOneShotVfx = math.max(0, authoring.MaxActiveOneShotPowerUpVfx),
            RefreshAttachedLifetimeOnCapHit = authoring.RefreshAttachedElementalVfxLifetimeOnCapHit ? (byte)1 : (byte)0
        };
    }

    /// <summary>
    /// Builds runtime elemental VFX assignments from the preset.
    ///  authoring: Owning player authoring component.
    ///  preset: Source power-ups preset.
    ///  resolveDynamicPrefabEntity: Prefab-to-entity resolver provided by the baker.
    /// returns Elemental VFX config for all supported elements.
    /// </summary>
    public static PlayerElementalVfxConfig BuildElementalVfxConfig(PlayerAuthoring authoring,
                                                                   PlayerPowerUpsPreset preset,
                                                                   Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        IReadOnlyList<ElementalVfxByElementData> assignments = preset != null ? preset.ElementalVfxByElement : null;

        return new PlayerElementalVfxConfig
        {
            Fire = BuildElementalVfxDefinitionConfig(authoring,
                                                     ResolveElementalVfxAssignment(assignments, ElementType.Fire),
                                                     "Elemental Fire",
                                                     resolveDynamicPrefabEntity),
            Ice = BuildElementalVfxDefinitionConfig(authoring,
                                                    ResolveElementalVfxAssignment(assignments, ElementType.Ice),
                                                    "Elemental Ice",
                                                    resolveDynamicPrefabEntity),
            Poison = BuildElementalVfxDefinitionConfig(authoring,
                                                       ResolveElementalVfxAssignment(assignments, ElementType.Poison),
                                                       "Elemental Poison",
                                                       resolveDynamicPrefabEntity),
            Custom = BuildElementalVfxDefinitionConfig(authoring,
                                                       ResolveElementalVfxAssignment(assignments, ElementType.Custom),
                                                       "Elemental Custom",
                                                       resolveDynamicPrefabEntity)
        };
    }

    /// <summary>
    /// Resolves a modular active power-up by ID with positional fallback.
    ///  preset: Source preset.
    ///  powerUpId: Requested power-up ID.
    ///  fallbackIndex: Fallback index inside the active list.
    /// returns Resolved modular active power-up or null.
    /// </summary>
    public static ModularPowerUpDefinition ResolveLoadoutActivePowerUp(PlayerPowerUpsPreset preset, string powerUpId, int fallbackIndex)
    {
        if (preset == null)
            return null;

        IReadOnlyList<ModularPowerUpDefinition> activePowerUps = preset.ActivePowerUps;

        if (activePowerUps == null)
            return null;

        if (!string.IsNullOrWhiteSpace(powerUpId))
        {
            for (int index = 0; index < activePowerUps.Count; index++)
            {
                ModularPowerUpDefinition activePowerUp = activePowerUps[index];

                if (activePowerUp == null)
                    continue;

                PowerUpCommonData commonData = activePowerUp.CommonData;

                if (commonData == null)
                    continue;

                if (!string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
                    continue;

                return activePowerUp;
            }
        }

        if (fallbackIndex >= 0 && fallbackIndex < activePowerUps.Count)
            return activePowerUps[fallbackIndex];

        return null;
    }

    /// <summary>
    /// Resolves a modular passive power-up by ID.
    ///  preset: Source preset.
    ///  powerUpId: Requested power-up ID.
    /// returns Resolved modular passive power-up or null.
    /// </summary>
    public static ModularPowerUpDefinition ResolveLoadoutPassivePowerUp(PlayerPowerUpsPreset preset, string powerUpId)
    {
        if (preset == null)
            return null;

        IReadOnlyList<ModularPowerUpDefinition> passivePowerUps = preset.PassivePowerUps;

        if (passivePowerUps == null)
            return null;

        if (string.IsNullOrWhiteSpace(powerUpId))
            return null;

        for (int index = 0; index < passivePowerUps.Count; index++)
        {
            ModularPowerUpDefinition passivePowerUp = passivePowerUps[index];

            if (passivePowerUp == null)
                continue;

            PowerUpCommonData commonData = passivePowerUp.CommonData;

            if (commonData == null)
                continue;

            if (!string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
                continue;

            return passivePowerUp;
        }

        return null;
    }

    /// <summary>
    /// Resolves a legacy active tool by ID with positional fallback.
    ///  preset: Source preset.
    ///  toolId: Requested tool ID.
    ///  fallbackIndex: Fallback index inside the active tool list.
    /// returns Resolved active tool or null.
    /// </summary>
    public static ActiveToolDefinition ResolveLoadoutTool(PlayerPowerUpsPreset preset, string toolId, int fallbackIndex)
    {
        if (preset == null)
            return null;

        IReadOnlyList<ActiveToolDefinition> activeTools = preset.ActiveTools;

        if (activeTools == null)
            return null;

        if (!string.IsNullOrWhiteSpace(toolId))
        {
            for (int index = 0; index < activeTools.Count; index++)
            {
                ActiveToolDefinition activeTool = activeTools[index];

                if (activeTool == null)
                    continue;

                PowerUpCommonData commonData = activeTool.CommonData;

                if (commonData == null)
                    continue;

                if (!string.Equals(commonData.PowerUpId, toolId, StringComparison.OrdinalIgnoreCase))
                    continue;

                return activeTool;
            }
        }

        if (fallbackIndex >= 0 && fallbackIndex < activeTools.Count)
            return activeTools[fallbackIndex];

        return null;
    }

    /// <summary>
    /// Resolves a legacy passive tool by ID.
    ///  preset: Source preset.
    ///  toolId: Requested tool ID.
    /// returns Resolved passive tool or null.
    /// </summary>
    public static PassiveToolDefinition ResolveLoadoutPassiveTool(PlayerPowerUpsPreset preset, string toolId)
    {
        if (preset == null)
            return null;

        IReadOnlyList<PassiveToolDefinition> passiveTools = preset.PassiveTools;

        if (passiveTools == null || string.IsNullOrWhiteSpace(toolId))
            return null;

        for (int index = 0; index < passiveTools.Count; index++)
        {
            PassiveToolDefinition passiveTool = passiveTools[index];

            if (passiveTool == null)
                continue;

            PowerUpCommonData commonData = passiveTool.CommonData;

            if (commonData == null)
                continue;

            if (!string.Equals(commonData.PowerUpId, toolId, StringComparison.OrdinalIgnoreCase))
                continue;

            return passiveTool;
        }

        return null;
    }

    /// <summary>
    /// Resolves one module definition by ID from the module catalog.
    ///  preset: Source preset.
    ///  moduleId: Requested module ID.
    /// returns Resolved module definition or null.
    /// </summary>
    public static PowerUpModuleDefinition ResolveModuleDefinitionById(PlayerPowerUpsPreset preset, string moduleId)
    {
        if (preset == null || string.IsNullOrWhiteSpace(moduleId))
            return null;

        IReadOnlyList<PowerUpModuleDefinition> modules = preset.ModuleDefinitions;

        if (modules == null)
            return null;

        for (int index = 0; index < modules.Count; index++)
        {
            PowerUpModuleDefinition module = modules[index];

            if (module == null)
                continue;

            if (string.Equals(module.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                return module;
        }

        return null;
    }

    /// <summary>
    /// Resolves and validates one optional power-up prefab to an ECS prefab entity.
    ///  authoring: Owning player authoring component.
    ///  optionalPrefab: Prefab candidate.
    ///  contextLabel: Label used in validation logs.
    ///  resolveDynamicPrefabEntity: Prefab-to-entity resolver provided by the baker.
    /// returns Resolved entity or Entity.Null when validation fails.
    /// </summary>
    public static Entity ResolveOptionalPowerUpPrefabEntity(PlayerAuthoring authoring,
                                                            GameObject optionalPrefab,
                                                            string contextLabel,
                                                            Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (optionalPrefab == null)
            return Entity.Null;

        if (IsInvalidPowerUpPrefab(authoring, optionalPrefab))
        {
#if UNITY_EDITOR
            if (authoring != null)
                Debug.LogError(string.Format("[PlayerAuthoringBaker] Invalid prefab '{0}' in '{1}' on '{2}'. Assign a dedicated prefab without PlayerAuthoring.", optionalPrefab.name, contextLabel, authoring.name), authoring);
#endif
            return Entity.Null;
        }

        return ResolvePrefabEntity(resolveDynamicPrefabEntity, optionalPrefab);
    }

    /// <summary>
    /// Resolves a validated prefab through the baker callback.
    ///  resolveDynamicPrefabEntity: Prefab-to-entity resolver provided by the baker.
    ///  prefab: Prefab to resolve.
    /// returns Resolved entity or Entity.Null when no resolver is available.
    /// </summary>
    public static Entity ResolvePrefabEntity(Func<GameObject, Entity> resolveDynamicPrefabEntity, GameObject prefab)
    {
        if (resolveDynamicPrefabEntity == null || prefab == null)
            return Entity.Null;

        return resolveDynamicPrefabEntity(prefab);
    }

    /// <summary>
    /// Validates bomb prefabs used by active power-ups.
    ///  authoring: Owning player authoring component.
    ///  bombPrefab: Bomb prefab candidate.
    /// returns True when the prefab is not valid for baking.
    /// </summary>
    public static bool IsInvalidBombPrefab(PlayerAuthoring authoring, GameObject bombPrefab)
    {
        if (bombPrefab == null)
            return true;

        if (authoring != null && bombPrefab == authoring.gameObject)
            return true;

        if (bombPrefab.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    /// <summary>
    /// Validates generic VFX prefabs used by power-up bake paths.
    ///  authoring: Owning player authoring component.
    ///  optionalPrefab: Prefab candidate.
    /// returns True when the prefab is not valid for baking.
    /// </summary>
    public static bool IsInvalidPowerUpPrefab(PlayerAuthoring authoring, GameObject optionalPrefab)
    {
        if (optionalPrefab == null)
            return true;

        if (authoring != null && optionalPrefab == authoring.gameObject)
            return true;

        if (optionalPrefab.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    /// <summary>
    /// Converts elemental effect authoring data into runtime-safe values.
    ///  effectData: Source elemental effect definition.
    /// returns Runtime elemental effect config.
    /// </summary>
    public static ElementalEffectConfig BuildElementalEffectConfig(ElementalEffectDefinitionData effectData)
    {
        if (effectData == null)
            return default;

        float procThresholdStacks = math.max(0.1f, effectData.ProcThresholdStacks);
        float maximumStacks = math.max(0.1f, effectData.MaximumStacks);

        if (maximumStacks < procThresholdStacks)
            maximumStacks = procThresholdStacks;

        return new ElementalEffectConfig
        {
            ElementType = effectData.ElementType,
            EffectKind = effectData.EffectKind,
            ProcMode = effectData.ProcMode,
            ReapplyMode = effectData.ReapplyMode,
            ProcThresholdStacks = procThresholdStacks,
            MaximumStacks = maximumStacks,
            StackDecayPerSecond = math.max(0f, effectData.StackDecayPerSecond),
            ConsumeStacksOnProc = effectData.ConsumeStacksOnProc ? (byte)1 : (byte)0,
            DotDamagePerTick = math.max(0f, effectData.DotDamagePerTick),
            DotTickInterval = math.max(0.01f, effectData.DotTickInterval),
            DotDurationSeconds = math.max(0.05f, effectData.DotDurationSeconds),
            ImpedimentSlowPercentPerStack = math.clamp(effectData.ImpedimentSlowPercentPerStack, 0f, 100f),
            ImpedimentProcSlowPercent = math.clamp(effectData.ImpedimentProcSlowPercent, 0f, 100f),
            ImpedimentMaxSlowPercent = math.clamp(effectData.ImpedimentMaxSlowPercent, 0f, 100f),
            ImpedimentDurationSeconds = math.max(0.05f, effectData.ImpedimentDurationSeconds)
        };
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves one elemental VFX assignment from the preset list.
    ///  assignments: Source assignment list.
    ///  elementType: Requested element type.
    /// returns Matching assignment or null.
    /// </summary>
    private static ElementalVfxByElementData ResolveElementalVfxAssignment(IReadOnlyList<ElementalVfxByElementData> assignments, ElementType elementType)
    {
        if (assignments == null)
            return null;

        for (int index = 0; index < assignments.Count; index++)
        {
            ElementalVfxByElementData assignment = assignments[index];

            if (assignment == null || assignment.ElementType != elementType)
                continue;

            return assignment;
        }

        return null;
    }

    /// <summary>
    /// Compiles one elemental VFX assignment into runtime-safe data.
    ///  authoring: Owning player authoring component.
    ///  assignment: Source elemental assignment.
    ///  labelPrefix: Label used in validation logs.
    ///  resolveDynamicPrefabEntity: Prefab-to-entity resolver provided by the baker.
    /// returns Runtime elemental VFX definition.
    /// </summary>
    private static ElementalVfxDefinitionConfig BuildElementalVfxDefinitionConfig(PlayerAuthoring authoring,
                                                                                  ElementalVfxByElementData assignment,
                                                                                  string labelPrefix,
                                                                                  Func<GameObject, Entity> resolveDynamicPrefabEntity)
    {
        if (assignment == null)
            return default;

        Entity stackVfxPrefabEntity = Entity.Null;
        Entity procVfxPrefabEntity = Entity.Null;

        if (authoring != null && authoring.BakePowerUpVfxEntityPrefabs)
        {
            stackVfxPrefabEntity = ResolveOptionalPowerUpPrefabEntity(authoring,
                                                                      assignment.StackVfxPrefab,
                                                                      labelPrefix + " Stack VFX",
                                                                      resolveDynamicPrefabEntity);
            procVfxPrefabEntity = ResolveOptionalPowerUpPrefabEntity(authoring,
                                                                     assignment.ProcVfxPrefab,
                                                                     labelPrefix + " Proc VFX",
                                                                     resolveDynamicPrefabEntity);
        }

        return new ElementalVfxDefinitionConfig
        {
            SpawnStackVfx = assignment.SpawnStackVfx ? (byte)1 : (byte)0,
            StackVfxPrefabEntity = stackVfxPrefabEntity,
            StackVfxScaleMultiplier = math.max(0.01f, assignment.StackVfxScaleMultiplier),
            SpawnProcVfx = assignment.SpawnProcVfx ? (byte)1 : (byte)0,
            ProcVfxPrefabEntity = procVfxPrefabEntity,
            ProcVfxScaleMultiplier = math.max(0.01f, assignment.ProcVfxScaleMultiplier)
        };
    }
    #endregion

    #endregion
}
