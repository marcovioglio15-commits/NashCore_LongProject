using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Authoring component for configuring player settings and visualizing player-related gizmos in 
/// the Unity editor.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Preset")]
    [Tooltip("Master preset used to configure this player instance.")]
    [FormerlySerializedAs("m_MasterPreset")]
    [SerializeField] private PlayerMasterPreset masterPreset;

    [Header("Cheats")]
    [Tooltip("Optional power-up preset library used by runtime cheat shortcuts. Ctrl+Number applies the preset at the matching index.")]
    [SerializeField] private PlayerPowerUpsPresetLibrary powerUpsCheatPresetLibrary;

    [Header("Gizmos")]
    [Tooltip("Optional radius for gizmo previews in the editor.")]
    [FormerlySerializedAs("m_GizmoRadius")]
    [SerializeField] private float gizmoRadius = 2f;

    [Header("Shooting")]
    [Tooltip("Optional muzzle transform used as shooting reference for spawn orientation and offset.")]
    [SerializeField] private Transform weaponReference;

    [Header("Animation")]
    [Tooltip("Optional Animator used for ECS-driven visual animation sync.")]
    [SerializeField] private Animator animatorComponent;

    [Tooltip("When enabled, draws local animation debug axes used by ECS->Animator parameter projection.")]
    [SerializeField] private bool drawAnimationDebugGizmos = true;

    [Tooltip("Length in meters for animation debug axes in Scene view.")]
    [SerializeField] private float animationDebugAxisLength = 0.75f;

    [Header("Runtime Visual Bridge")]
    [Tooltip("Optional prefab asset instantiated at runtime when no valid Animator companion exists. Use a visual-only prefab with Animator and full rig hierarchy.")]
    [SerializeField] private GameObject runtimeVisualBridgePrefab;

    [Tooltip("When enabled, spawns the runtime visual bridge only if Animator companion is missing or null at runtime.")]
    [SerializeField] private bool spawnRuntimeVisualBridgeWhenAnimatorMissing = true;

    [Tooltip("When enabled, runtime visual bridge follows ECS player rotation.")]
    [SerializeField] private bool runtimeVisualBridgeSyncRotation = true;

    [Tooltip("Local-space position offset applied to runtime visual bridge relative to ECS player transform.")]
    [SerializeField] private Vector3 runtimeVisualBridgeOffset = Vector3.zero;

    [Tooltip("Draws runtime visual bridge offset gizmo from player origin to target visual anchor.")]
    [SerializeField] private bool drawRuntimeVisualBridgeGizmo = true;

    [Header("Hybrid Bake Safety")]
    [Tooltip("When enabled, bakes the attached Elemental Trail prefab reference into ECS. Disable to isolate SubScene object-reference streaming issues.")]
    [SerializeField] private bool bakeElementalTrailAttachedVfxReference = false;

    [Tooltip("When enabled, converts power-up VFX prefabs into ECS prefab entities (explosion/proc/stack). Disable to isolate SubScene object-reference streaming issues.")]
    [SerializeField] private bool bakePowerUpVfxEntityPrefabs = false;

    [Header("Power-Ups VFX")]
    [Tooltip("Optional attached VFX prefab activated while Elemental Trail passive is enabled.")]
    [SerializeField] private GameObject elementalTrailAttachedVfxPrefab;

    [Tooltip("Scale multiplier applied to the attached Elemental Trail VFX instance.")]
    [SerializeField] private float elementalTrailAttachedVfxScaleMultiplier = 1f;

    [Tooltip("Maximum number of identical one-shot VFX allowed in the same spatial cell. Set 0 to disable this cap.")]
    [SerializeField] private int maxIdenticalOneShotVfxPerCell = 6;

    [Tooltip("Cell size in meters used by the one-shot VFX per-cell cap.")]
    [SerializeField] private float oneShotVfxCellSize = 2.5f;

    [Tooltip("Maximum number of identical attached elemental VFX allowed on the same target. Set 0 to disable this cap.")]
    [SerializeField] private int maxAttachedElementalVfxPerTarget = 1;

    [Tooltip("Maximum number of active one-shot power-up VFX managed by one player. Set 0 to disable this cap.")]
    [SerializeField] private int maxActiveOneShotPowerUpVfx = 400;

    [Tooltip("When enabled, hitting the attached-target cap refreshes lifetime of the existing VFX.")]
    [SerializeField] private bool refreshAttachedElementalVfxLifetimeOnCapHit = true;
    #endregion

    #endregion

    #region Properties
    public PlayerMasterPreset MasterPreset
    {
        get
        {
            return masterPreset;
        }
    }

    public PlayerPowerUpsPresetLibrary PowerUpsCheatPresetLibrary
    {
        get
        {
            return powerUpsCheatPresetLibrary;
        }
    }

    public float GizmoRadius
    {
        get
        {
            return gizmoRadius;
        }
    }

    public Transform WeaponReference
    {
        get
        {
            return weaponReference;
        }
    }

    public Animator AnimatorComponent
    {
        get
        {
            return animatorComponent;
        }
    }

    public bool DrawAnimationDebugGizmos
    {
        get
        {
            return drawAnimationDebugGizmos;
        }
    }

    public float AnimationDebugAxisLength
    {
        get
        {
            return animationDebugAxisLength;
        }
    }

    public GameObject RuntimeVisualBridgePrefab
    {
        get
        {
            return runtimeVisualBridgePrefab;
        }
    }

    public bool SpawnRuntimeVisualBridgeWhenAnimatorMissing
    {
        get
        {
            return spawnRuntimeVisualBridgeWhenAnimatorMissing;
        }
    }

    public bool RuntimeVisualBridgeSyncRotation
    {
        get
        {
            return runtimeVisualBridgeSyncRotation;
        }
    }

    public Vector3 RuntimeVisualBridgeOffset
    {
        get
        {
            return runtimeVisualBridgeOffset;
        }
    }

    public bool DrawRuntimeVisualBridgeGizmo
    {
        get
        {
            return drawRuntimeVisualBridgeGizmo;
        }
    }

    public bool BakeElementalTrailAttachedVfxReference
    {
        get
        {
            return bakeElementalTrailAttachedVfxReference;
        }
    }

    public bool BakePowerUpVfxEntityPrefabs
    {
        get
        {
            return bakePowerUpVfxEntityPrefabs;
        }
    }

    public GameObject ElementalTrailAttachedVfxPrefab
    {
        get
        {
            return elementalTrailAttachedVfxPrefab;
        }
    }

    public float ElementalTrailAttachedVfxScaleMultiplier
    {
        get
        {
            return elementalTrailAttachedVfxScaleMultiplier;
        }
    }

    public int MaxIdenticalOneShotVfxPerCell
    {
        get
        {
            return maxIdenticalOneShotVfxPerCell;
        }
    }

    public float OneShotVfxCellSize
    {
        get
        {
            return oneShotVfxCellSize;
        }
    }

    public int MaxAttachedElementalVfxPerTarget
    {
        get
        {
            return maxAttachedElementalVfxPerTarget;
        }
    }

    public int MaxActiveOneShotPowerUpVfx
    {
        get
        {
            return maxActiveOneShotPowerUpVfx;
        }
    }

    public bool RefreshAttachedElementalVfxLifetimeOnCapHit
    {
        get
        {
            return refreshAttachedElementalVfxLifetimeOnCapHit;
        }
    }
    #endregion

    #region Methods

    #region Preset
    /// <summary>
    /// Retrieves the controller preset from the master preset.
    /// </summary>
    /// <returns>The PlayerControllerPreset from the master preset, or null if the master preset is not set.</returns>
    public PlayerControllerPreset GetControllerPreset()
    {
        if (masterPreset == null)
            return null;

        return masterPreset.ControllerPreset;
    }

    /// <summary>
    /// Retrieves the progression preset from the master preset.
    /// </summary>
    /// <returns>The PlayerProgressionPreset from the master preset, or null if the master preset is not set.</returns>
    public PlayerProgressionPreset GetProgressionPreset()
    {
        if (masterPreset == null)
            return null;

        return masterPreset.ProgressionPreset;
    }

    /// <summary>
    /// Retrieves the power-ups preset from the master preset.
    /// </summary>
    /// <returns>The PlayerPowerUpsPreset from the master preset, or null if the master preset is not set.</returns>
    public PlayerPowerUpsPreset GetPowerUpsPreset()
    {
        if (masterPreset == null)
            return null;

        return masterPreset.PowerUpsPreset;
    }
    #endregion

    #region Gizmos
    /// <summary>
    /// Draws player gizmos in the editor through the shared utility.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        PlayerAuthoringGizmoUtility.DrawSelectedGizmos(this);
    }
    #endregion

    #endregion
}

/// <summary>
/// Bakes PlayerAuthoring data into ECS components and configuration blobs for player controller and camera setup.
/// </summary>
public sealed class PlayerAuthoringBaker : Baker<PlayerAuthoring>
{
    #region Bake
    /// <summary>
    /// Configures and adds player controller and camera anchor components to the entity based on the provided authoring
    /// data.
    /// </summary>
    /// <param name="authoring">The PlayerAuthoring instance containing configuration data.</param>
    public override void Bake(PlayerAuthoring authoring)
    {
        // Validate authoring data
        if (authoring == null)
            return;

        PlayerControllerPreset controllerPreset = authoring.GetControllerPreset();

        if (controllerPreset == null)
            return;

        PlayerProgressionPreset progressionPreset = authoring.GetProgressionPreset();
        PlayerPowerUpsPreset powerUpsPreset = authoring.GetPowerUpsPreset();
        PlayerProgressionPreset sourceProgressionPreset = progressionPreset;
        PlayerPowerUpsPreset sourcePowerUpsPreset = powerUpsPreset;
        PlayerAnimationBindingsPreset animationBindingsPreset = authoring.MasterPreset != null ? authoring.MasterPreset.AnimationBindingsPreset : null;

#if UNITY_EDITOR
        PlayerScaledPresetScope scaledPresetScope = PlayerPresetScalingBakeUtility.CreateScope(controllerPreset,
                                                                                               progressionPreset,
                                                                                               powerUpsPreset,
                                                                                               animationBindingsPreset);
        controllerPreset = scaledPresetScope.ControllerPreset;
        progressionPreset = scaledPresetScope.ProgressionPreset;
        powerUpsPreset = scaledPresetScope.PowerUpsPreset;
        animationBindingsPreset = scaledPresetScope.AnimationBindingsPreset;

        try
        {
#endif

        // Create entity and build configuration blob
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
#if UNITY_EDITOR
        TryAddScalingDebugBuffers(entity,
                                  scaledPresetScope);
#endif
        BlobAssetReference<PlayerControllerConfigBlob> blob = PlayerControllerConfigBakeUtility.BuildConfigBlob(controllerPreset);
        AddBlobAsset(ref blob, out Unity.Entities.Hash128 _);

        PlayerControllerConfig config = new PlayerControllerConfig
        {
            Config = blob
        };

        //  Add player controller config component to entity
        AddComponent(entity, config);

        PlayerWorldLayersConfig worldLayersConfig = PlayerControllerConfigBakeUtility.BuildWorldLayersConfig(authoring.MasterPreset);
        AddComponent(entity, worldLayersConfig);
        Animator resolvedAnimatorComponent = PlayerAuthoringBakerValidationUtility.ResolveAnimatorComponent(authoring);
        GameObject resolvedRuntimeVisualBridgePrefab = PlayerAuthoringBakerValidationUtility.ResolveRuntimeVisualBridgePrefab(authoring);

        if (animationBindingsPreset != null)
        {
            AddComponent(entity, PlayerControllerConfigBakeUtility.BuildAnimatorParameterConfig(animationBindingsPreset));
            AddComponent(entity, new PlayerAnimatorRuntimeState
            {
                PreviousShooting = 0,
                Initialized = 0,
                ParametersValidated = 0,
                BoundAnimatorInstanceId = 0,
                RecoilValue = 0f,
                AimWeightValue = 0f,
                LeanValue = 0f,
                LastMoveX = 0f,
                LastMoveY = 1f
            });
            TryAddAnimatorAssetFallbackComponents(entity, resolvedAnimatorComponent, animationBindingsPreset);
        }

        AddComponent(entity, new PlayerVisualRuntimeBridgeConfig
        {
            VisualPrefab = resolvedRuntimeVisualBridgePrefab,
            PositionOffset = new float3(authoring.RuntimeVisualBridgeOffset.x,
                                        authoring.RuntimeVisualBridgeOffset.y,
                                        authoring.RuntimeVisualBridgeOffset.z),
            SyncRotation = authoring.RuntimeVisualBridgeSyncRotation ? (byte)1 : (byte)0,
            SpawnWhenAnimatorMissing = authoring.SpawnRuntimeVisualBridgeWhenAnimatorMissing ? (byte)1 : (byte)0
        });

        if (authoring.SpawnRuntimeVisualBridgeWhenAnimatorMissing &&
            resolvedRuntimeVisualBridgePrefab == null)
        {
            Debug.LogWarning(string.Format("[PlayerAuthoringBaker] Runtime visual bridge spawn is enabled on '{0}', but RuntimeVisualBridgePrefab is missing or invalid.",
                                           authoring.name),
                             authoring);
        }
        if (progressionPreset != null)
        {
            BlobAssetReference<PlayerProgressionConfigBlob> progressionBlob = PlayerProgressionBlobBakeUtility.BuildProgressionConfigBlob(progressionPreset,
                                                                                                                                        powerUpsPreset,
                                                                                                                                        sourceProgressionPreset,
                                                                                                                                        sourcePowerUpsPreset);
            AddBlobAsset(ref progressionBlob, out Unity.Entities.Hash128 _);

            PlayerProgressionConfig progressionConfig = new PlayerProgressionConfig
            {
                Config = progressionBlob
            };

            AddComponent(entity, progressionConfig);
            AddComponent(entity, PlayerPowerUpContainerBakeUtility.BuildInteractionConfig(progressionPreset,
                                                                                         ResolveDynamicPrefabEntity));
            AddComponent(entity, new PlayerPowerUpContainerProximityState
            {
                NearestContainerEntity = Entity.Null,
                NearestDistanceSquared = 0f,
                HasContainerInRange = 0
            });
            AddBuffer<PlayerPowerUpContainerSwapCommand>(entity);
        }

        if (powerUpsPreset != null)
        {
            PlayerPowerUpsConfig powerUpsConfig = PlayerPowerUpActiveBakeUtility.BuildPowerUpsConfig(authoring,
                                                                                                     powerUpsPreset,
                                                                                                     ResolveDynamicPrefabEntity);
            AddComponent(entity, powerUpsConfig);
            PlayerPowerUpVfxCapConfig powerUpVfxCapConfig = PlayerPowerUpBakeSharedUtility.BuildPowerUpVfxCapConfig(authoring);
            AddComponent(entity, powerUpVfxCapConfig);
            PlayerElementalVfxConfig elementalVfxConfig = PlayerPowerUpBakeSharedUtility.BuildElementalVfxConfig(authoring,
                                                                                                                 powerUpsPreset,
                                                                                                                 ResolveDynamicPrefabEntity);
            AddComponent(entity, elementalVfxConfig);

            if (authoring.BakeElementalTrailAttachedVfxReference && authoring.ElementalTrailAttachedVfxPrefab != null)
            {
                AddComponent(entity, new PlayerElementalTrailAttachedVfxPrefabReference
                {
                    Prefab = authoring.ElementalTrailAttachedVfxPrefab
                });
            }
#if UNITY_EDITOR
            else if (authoring.BakeElementalTrailAttachedVfxReference && authoring.ElementalTrailAttachedVfxPrefab == null)
            {
                Debug.LogWarning(string.Format("[PlayerAuthoringBaker] Attached Elemental Trail prefab reference bake enabled on '{0}', but no prefab is assigned.",
                                               authoring.name),
                                 authoring);
            }
#endif

            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = AddBuffer<EquippedPassiveToolElement>(entity);
            PlayerPowerUpCatalogBakeUtility.PopulateEquippedPassiveToolsBuffer(authoring,
                                                                               powerUpsPreset,
                                                                               ResolveDynamicPrefabEntity,
                                                                               equippedPassiveToolsBuffer);
            DynamicBuffer<PlayerPowerUpUnlockCatalogElement> powerUpUnlockCatalogBuffer = AddBuffer<PlayerPowerUpUnlockCatalogElement>(entity);
            DynamicBuffer<PlayerPowerUpTierDefinitionElement> powerUpTierDefinitionsBuffer = AddBuffer<PlayerPowerUpTierDefinitionElement>(entity);
            DynamicBuffer<PlayerPowerUpTierEntryElement> powerUpTierEntriesBuffer = AddBuffer<PlayerPowerUpTierEntryElement>(entity);
            DynamicBuffer<PlayerPowerUpTierEntryScalingElement> powerUpTierEntryScalingBuffer = AddBuffer<PlayerPowerUpTierEntryScalingElement>(entity);
            PlayerPowerUpCatalogBakeUtility.PopulatePowerUpUnlockTierBuffers(authoring,
                                                                             powerUpsPreset,
                                                                             sourcePowerUpsPreset,
                                                                             ResolveDynamicPrefabEntity,
                                                                             powerUpUnlockCatalogBuffer,
                                                                             powerUpTierDefinitionsBuffer,
                                                                             powerUpTierEntriesBuffer,
                                                                             powerUpTierEntryScalingBuffer);
            DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntriesBuffer = AddBuffer<PlayerPowerUpCheatPresetEntry>(entity);
            DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassivesBuffer = AddBuffer<PlayerPowerUpCheatPresetPassiveElement>(entity);
            PlayerPowerUpCatalogBakeUtility.PopulatePowerUpCheatPresetBuffers(authoring,
                                                                              ResolveDynamicPrefabEntity,
                                                                              cheatPresetEntriesBuffer,
                                                                              cheatPresetPassivesBuffer);
        }

        ShootingSettings shootingSettings = controllerPreset.ShootingSettings;

        if (shootingSettings != null && shootingSettings.ProjectilePrefab != null)
        {
            GameObject projectilePrefabObject = shootingSettings.ProjectilePrefab;

            if (PlayerAuthoringBakerValidationUtility.IsInvalidProjectilePrefab(authoring, projectilePrefabObject))
            {
#if UNITY_EDITOR
                Debug.LogError(string.Format("[PlayerAuthoringBaker] Invalid projectile prefab '{0}' on '{1}'. Assign a dedicated projectile prefab without PlayerAuthoring.", projectilePrefabObject.name, authoring.name), authoring);
#endif
            }
            else
            {
                Entity projectilePrefabEntity = GetEntity(projectilePrefabObject, TransformUsageFlags.Dynamic);
                ShooterProjectilePrefab projectilePrefab = new ShooterProjectilePrefab
                {
                    PrefabEntity = projectilePrefabEntity
                };

                AddComponent(entity, projectilePrefab);
                AddComponent(entity, new ProjectilePoolState
                {
                    InitialCapacity = math.max(0, shootingSettings.InitialPoolCapacity),
                    ExpandBatch = math.max(1, shootingSettings.PoolExpandBatch),
                    Initialized = 0
                });

                AddBuffer<ShootRequest>(entity);
                AddBuffer<ProjectilePoolElement>(entity);
            }
        }

        if (authoring.WeaponReference != null)
        {
            Entity muzzleAnchorEntity = GetEntity(authoring.WeaponReference, TransformUsageFlags.Dynamic);
            ShooterMuzzleAnchor muzzleAnchor = new ShooterMuzzleAnchor
            {
                AnchorEntity = muzzleAnchorEntity
            };

            AddComponent(entity, muzzleAnchor);
        }

        Transform roomAnchor = controllerPreset.CameraSettings.RoomAnchor;

        if (roomAnchor == null)
            return;

        Entity anchorEntity = GetEntity(roomAnchor, TransformUsageFlags.Dynamic);
        PlayerCameraAnchor cameraAnchor = new PlayerCameraAnchor
        {
            AnchorEntity = anchorEntity
        };

        AddComponent(entity, cameraAnchor);
#if UNITY_EDITOR
        }
        finally
        {
            if (scaledPresetScope != null)
                scaledPresetScope.Dispose();
        }
#endif
    }
    #endregion

    #region Bake Helpers
    /// <summary>
    /// Resolves one prefab asset as a dynamic ECS prefab entity for power-up bake helpers.
    /// /params prefab: Prefab asset to resolve.
    /// /returns ECS prefab entity or Entity.Null when the prefab is missing.
    /// </summary>
    private Entity ResolveDynamicPrefabEntity(GameObject prefab)
    {
        if (prefab == null)
            return Entity.Null;

        return GetEntity(prefab, TransformUsageFlags.Dynamic);
    }
    #endregion

    #region Validation
    private void TryAddAnimatorAssetFallbackComponents(Entity entity,
                                                       Animator resolvedAnimatorComponent,
                                                       PlayerAnimationBindingsPreset animationBindingsPreset)
    {
        RuntimeAnimatorController resolvedController = PlayerAuthoringBakerValidationUtility.ResolveAnimatorController(resolvedAnimatorComponent,
                                                                                                                       animationBindingsPreset);

        if (resolvedController != null)
        {
            AddComponent(entity, new PlayerAnimatorControllerReference
            {
                Controller = resolvedController
            });
        }

        Avatar resolvedAvatar = PlayerAuthoringBakerValidationUtility.ResolveAnimatorAvatar(resolvedAnimatorComponent);

        if (resolvedAvatar != null)
        {
            AddComponent(entity, new PlayerAnimatorAvatarReference
            {
                Avatar = resolvedAvatar
            });
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Adds editor-only runtime debug buffers derived from scaling rules that enable Debug in Console.
    /// </summary>
    /// <param name="entity">Target baked player entity receiving debug buffers.</param>
    /// <param name="scaledPresetScope">Scaled preset scope containing evaluated debug snapshots for [this] and final values.</param>

    private void TryAddScalingDebugBuffers(Entity entity,
                                           PlayerScaledPresetScope scaledPresetScope)
    {
        if (UnityEditor.BuildPipeline.isBuildingPlayer)
            return;

        IReadOnlyList<PlayerScalingDebugRuleSnapshot> debugRuleSnapshots = scaledPresetScope != null
            ? scaledPresetScope.DebugRuleSnapshots
            : null;
        bool hasDebugRuleSnapshots = debugRuleSnapshots != null && debugRuleSnapshots.Count > 0;

        if (!hasDebugRuleSnapshots)
            return;

        DynamicBuffer<PlayerScalingDebugRuleElement> debugRuleBuffer = AddBuffer<PlayerScalingDebugRuleElement>(entity);

        for (int index = 0; index < debugRuleSnapshots.Count; index++)
        {
            PlayerScalingDebugRuleSnapshot snapshot = debugRuleSnapshots[index];
            string presetTypeLabel = string.IsNullOrWhiteSpace(snapshot.PresetTypeLabel) ? "Preset" : snapshot.PresetTypeLabel;
            string targetDisplayName = string.IsNullOrWhiteSpace(snapshot.TargetDisplayName) ? "Scaled Stat" : snapshot.TargetDisplayName;
            string statKey = string.IsNullOrWhiteSpace(snapshot.StatKey) ? "<unknown>" : snapshot.StatKey;
            string formula = string.IsNullOrWhiteSpace(snapshot.Formula) ? "[this]" : snapshot.Formula;
            Color debugColor = snapshot.DebugColor;
            debugRuleBuffer.Add(new PlayerScalingDebugRuleElement
            {
                PresetTypeLabel = new FixedString64Bytes(presetTypeLabel),
                TargetDisplayName = new FixedString64Bytes(targetDisplayName),
                StatKey = new FixedString128Bytes(statKey),
                Formula = new FixedString512Bytes(formula),
                ThisValue = snapshot.ThisValue,
                FinalValue = snapshot.FinalValue,
                DebugColor = new float4(debugColor.r, debugColor.g, debugColor.b, debugColor.a)
            });
        }
    }
#endif
    #endregion

}
