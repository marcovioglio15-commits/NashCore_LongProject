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
    #region Constants
    private static readonly Color MovementGizmoColor = new Color(0.2f, 0.8f, 0.4f, 0.9f);
    private static readonly Color LookGizmoColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    private static readonly Color CameraFollowGizmoColor = new Color(1f, 0.8f, 0.2f, 0.9f);
    private static readonly Color CameraRoomGizmoColor = new Color(1f, 0.5f, 0.2f, 0.9f);
    private static readonly Color ShootingGizmoColor = new Color(1f, 0.25f, 0.25f, 0.9f);
    private static readonly Color PowerUpBombGizmoColor = new Color(1f, 0.35f, 0.15f, 0.75f);
    private static readonly Color PowerUpDashGizmoColor = new Color(0.25f, 0.85f, 1f, 0.85f);
    private const float LookRadiusScale = 0.9f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Preset")]
    [Tooltip("Master preset used to configure this player instance.")]
    [FormerlySerializedAs("m_MasterPreset")]
    [SerializeField] private PlayerMasterPreset masterPreset;

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
    /// Draws movement, look, and camera gizmos in the editor when the object is selected.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        PlayerControllerPreset controllerPreset = GetControllerPreset();

        if (controllerPreset == null)
            return;

        DrawMovementGizmo(controllerPreset);
        DrawLookGizmo(controllerPreset);
        DrawShootingGizmo(controllerPreset);
        DrawCameraGizmo(controllerPreset);
        DrawPowerUpsGizmos();
        DrawAnimationDebugGizmo();
    }

    /// <summary>
    /// Draws a movement gizmo in the scene view based on the specified player movement settings.
    /// </summary>
    /// <param name="preset">The player controller preset containing movement settings to visualize.</param>
    private void DrawMovementGizmo(PlayerControllerPreset preset)
    {
        MovementSettings movementSettings = preset.MovementSettings;

        if (movementSettings == null)
            return;

        Gizmos.color = MovementGizmoColor;
        Vector3 center = transform.position;

        if (movementSettings.DirectionsMode == MovementDirectionsMode.AllDirections)
        {
            Gizmos.DrawWireSphere(center, gizmoRadius);
            return;
        }

        int count = Mathf.Max(1, movementSettings.DiscreteDirectionCount);
        float step = 360f / count;
        float offset = movementSettings.DirectionOffsetDegrees;

        for (int i = 0; i < count; i++)
        {
            float angle = (i * step) + offset;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Gizmos.DrawLine(center, center + dir * gizmoRadius);
        }

        Gizmos.DrawWireSphere(center, gizmoRadius);
    }

    /// <summary>
    /// Draws a gizmo in the scene view to visualize the look direction settings of the specified player controller
    /// preset.
    /// </summary>
    /// <param name="preset">The player controller preset whose look settings are to be visualized.</param>
    private void DrawLookGizmo(PlayerControllerPreset preset)
    {
        LookSettings lookSettings = preset.LookSettings;

        if (lookSettings == null)
            return;

        Vector3 center = transform.position + Vector3.up * 0.05f;
        Gizmos.color = LookGizmoColor;

        if (lookSettings.DirectionsMode == LookDirectionsMode.AllDirections)
        {
            Gizmos.DrawWireSphere(center, gizmoRadius * LookRadiusScale);
            return;
        }

        if (lookSettings.DirectionsMode == LookDirectionsMode.DiscreteCount)
        {
                int count = Mathf.Max(1, lookSettings.DiscreteDirectionCount);
                float step = 360f / count;
                float offset = lookSettings.DirectionOffsetDegrees;
                float lookRadius = gizmoRadius * LookRadiusScale;

                for (int i = 0; i < count; i++)
                {
                    float angle = (i * step) + offset;
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    Gizmos.DrawLine(center, center + dir * lookRadius);
                }

                Gizmos.DrawWireSphere(center, lookRadius);
                return;
        }

        DrawConeGizmo(center, lookSettings.FrontConeEnabled, 0f, lookSettings.FrontConeAngle);
        DrawConeGizmo(center, lookSettings.RightConeEnabled, 90f, lookSettings.RightConeAngle);
        DrawConeGizmo(center, lookSettings.BackConeEnabled, 180f, lookSettings.BackConeAngle);
        DrawConeGizmo(center, lookSettings.LeftConeEnabled, 270f, lookSettings.LeftConeAngle);
    }

    /// <summary>
    /// Draws a cone-shaped gizmo using two lines representing the cone's boundaries.
    /// </summary>
    /// <param name="center">The world position at the apex of the cone.</param>
    /// <param name="enabled">Indicates whether the cone gizmo should be drawn.</param>
    /// <param name="centerAngle">The central angle, in degrees, defining the direction of the cone.</param>
    /// <param name="coneAngle">The total angle, in degrees, of the cone's spread.</param>
    private void DrawConeGizmo(Vector3 center, bool enabled, float centerAngle, float coneAngle)
    {
        if (enabled == false)
            return;

        float halfAngle = coneAngle * 0.5f;
        Vector3 left = Quaternion.Euler(0f, centerAngle - halfAngle, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, centerAngle + halfAngle, 0f) * Vector3.forward;

        Gizmos.DrawLine(center, center + left * gizmoRadius);
        Gizmos.DrawLine(center, center + right * gizmoRadius);
    }

    /// <summary>
    /// Draws camera-related gizmos in the scene view based on the camera behavior defined in the given player
    /// controller preset.
    /// </summary>
    /// <param name="preset">The player controller preset containing camera settings to visualize.</param>
    private void DrawCameraGizmo(PlayerControllerPreset preset)
    {
        CameraSettings cameraSettings = preset.CameraSettings;

        if (cameraSettings == null)
            return;

        switch (cameraSettings.Behavior)
        {
            case CameraBehavior.FollowWithOffset:
                Gizmos.color = CameraFollowGizmoColor;
                Vector3 offsetPosition = transform.position + cameraSettings.FollowOffset;
                Gizmos.DrawLine(transform.position, offsetPosition);
                Gizmos.DrawWireSphere(offsetPosition, 0.2f);
                return;
            case CameraBehavior.RoomFixed:
                if (cameraSettings.RoomAnchor == null)
                    return;

                Gizmos.color = CameraRoomGizmoColor;
                Vector3 anchorPosition = cameraSettings.RoomAnchor.position;
                Gizmos.DrawLine(transform.position, anchorPosition);
                Gizmos.DrawWireSphere(anchorPosition, 0.25f);
                return;
        }
    }

    /// <summary>
    /// Draws shooting-related gizmos in the scene view, including muzzle reference and spawn offset.
    /// </summary>
    /// <param name="preset">The player controller preset containing shooting settings to visualize.</param>
    private void DrawShootingGizmo(PlayerControllerPreset preset)
    {
        ShootingSettings shootingSettings = preset.ShootingSettings;

        if (shootingSettings == null)
            return;

        Transform referenceTransform = weaponReference != null ? weaponReference : transform;
        Vector3 referencePosition = referenceTransform.position;
        Vector3 spawnPosition = referencePosition + referenceTransform.rotation * shootingSettings.ShootOffset;
        Vector3 forward = referenceTransform.forward;

        Gizmos.color = ShootingGizmoColor;
        Gizmos.DrawLine(referencePosition, spawnPosition);
        Gizmos.DrawWireSphere(spawnPosition, 0.12f);
        Gizmos.DrawLine(spawnPosition, spawnPosition + forward * 0.5f);
    }

    /// <summary>
    /// Draws local animation axes used by runtime parameter projection (MoveX/MoveY and AimX/AimY).
    /// </summary>
    private void DrawAnimationDebugGizmo()
    {
        if (drawAnimationDebugGizmos == false)
            return;

        float axisLength = Mathf.Max(0.1f, animationDebugAxisLength);
        Vector3 origin = transform.position + Vector3.up * 0.06f;
        Vector3 forward = transform.forward * axisLength;
        Vector3 right = transform.right * axisLength;

        Gizmos.color = new Color(0.15f, 0.95f, 0.85f, 0.95f);
        Gizmos.DrawLine(origin, origin + forward);

        Gizmos.color = new Color(0.95f, 0.7f, 0.2f, 0.95f);
        Gizmos.DrawLine(origin, origin + right);
    }

    /// <summary>
    /// Draws preview gizmos for currently loaded Bomb and Dash active tools.
    /// </summary>
    private void DrawPowerUpsGizmos()
    {
        PlayerPowerUpsPreset powerUpsPreset = GetPowerUpsPreset();

        if (powerUpsPreset == null)
            return;

        ActiveToolDefinition primaryTool = ResolveActiveToolById(powerUpsPreset, powerUpsPreset.PrimaryActiveToolId);
        ActiveToolDefinition secondaryTool = ResolveActiveToolById(powerUpsPreset, powerUpsPreset.SecondaryActiveToolId);
        ActiveToolDefinition bombTool = ResolveToolByKind(primaryTool, secondaryTool, ActiveToolKind.Bomb);
        ActiveToolDefinition dashTool = ResolveToolByKind(primaryTool, secondaryTool, ActiveToolKind.Dash);

        if (bombTool != null)
            DrawBombGizmo(bombTool);

        if (dashTool != null)
            DrawDashGizmo(dashTool);
    }

    /// <summary>
    /// Draws the bomb spawn and explosion preview.
    /// </summary>
    /// <param name="bombTool">Tool definition used for visualization.</param>
    private void DrawBombGizmo(ActiveToolDefinition bombTool)
    {
        BombToolData bombData = bombTool.BombData;

        if (bombData == null)
            return;

        Vector3 spawnOffset = bombData.SpawnOffset;
        float radius = Mathf.Max(0.1f, bombData.Radius);
        Vector3 origin = transform.position;
        Vector3 spawnPoint = transform.TransformPoint(spawnOffset);
        float deploySpeed = Mathf.Max(0f, bombData.DeploySpeed);
        Vector3 throwDirection = transform.forward;
        Vector3 throwEnd = spawnPoint + throwDirection * Mathf.Max(0.25f, deploySpeed * 0.2f);

        Gizmos.color = PowerUpBombGizmoColor;
        Gizmos.DrawLine(origin, spawnPoint);
        Gizmos.DrawWireSphere(spawnPoint, 0.15f);
        Gizmos.DrawWireSphere(spawnPoint, radius);
        Gizmos.DrawLine(spawnPoint, throwEnd);
    }

    /// <summary>
    /// Draws the dash direction and distance preview.
    /// </summary>
    /// <param name="dashTool">Tool definition used for visualization.</param>
    private void DrawDashGizmo(ActiveToolDefinition dashTool)
    {
        DashToolData dashData = dashTool.DashData;

        if (dashData == null)
            return;

        float distance = Mathf.Max(0f, dashData.Distance);

        if (distance <= 0f)
            return;

        Vector3 origin = transform.position + Vector3.up * 0.05f;
        Vector3 endPoint = origin + transform.forward * distance;

        Gizmos.color = PowerUpDashGizmoColor;
        Gizmos.DrawLine(origin, endPoint);
        Gizmos.DrawWireSphere(endPoint, 0.12f);
    }

    /// <summary>
    /// Resolves an active tool by PowerUpId from the specified preset.
    /// </summary>
    /// <param name="preset">Power-ups preset used for lookup.</param>
    /// <param name="powerUpId">Requested tool ID.</param>
    /// <returns>Matching tool definition or null if no match exists.</returns>
    private static ActiveToolDefinition ResolveActiveToolById(PlayerPowerUpsPreset preset, string powerUpId)
    {
        if (preset == null)
            return null;

        IReadOnlyList<ActiveToolDefinition> activeTools = preset.ActiveTools;

        if (activeTools == null || activeTools.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(powerUpId))
            return null;

        for (int index = 0; index < activeTools.Count; index++)
        {
            ActiveToolDefinition activeTool = activeTools[index];

            if (activeTool == null)
                continue;

            PowerUpCommonData commonData = activeTool.CommonData;

            if (commonData == null)
                continue;

            if (string.Equals(commonData.PowerUpId, powerUpId, System.StringComparison.OrdinalIgnoreCase) == false)
                continue;

            return activeTool;
        }

        return null;
    }

    /// <summary>
    /// Picks the first tool that matches the requested kind from the provided candidates.
    /// </summary>
    /// <param name="firstCandidate">First candidate tool.</param>
    /// <param name="secondCandidate">Second candidate tool.</param>
    /// <param name="requestedKind">Requested active tool kind.</param>
    /// <returns>Matching tool definition or null.</returns>
    private static ActiveToolDefinition ResolveToolByKind(ActiveToolDefinition firstCandidate, ActiveToolDefinition secondCandidate, ActiveToolKind requestedKind)
    {
        if (firstCandidate != null && firstCandidate.ToolKind == requestedKind)
            return firstCandidate;

        if (secondCandidate != null && secondCandidate.ToolKind == requestedKind)
            return secondCandidate;

        return null;
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

        // Create entity and build configuration blob
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        BlobAssetReference<PlayerControllerConfigBlob> blob = BuildConfigBlob(controllerPreset);
        AddBlobAsset(ref blob, out Unity.Entities.Hash128 _);

        PlayerControllerConfig config = new PlayerControllerConfig
        {
            Config = blob
        };

        //  Add player controller config component to entity
        AddComponent(entity, config);

        PlayerWorldLayersConfig worldLayersConfig = BuildWorldLayersConfig(authoring.MasterPreset);
        AddComponent(entity, worldLayersConfig);
        PlayerAnimationBindingsPreset animationBindingsPreset = authoring.MasterPreset != null ? authoring.MasterPreset.AnimationBindingsPreset : null;

        if (authoring.AnimatorComponent != null)
        {
            AddComponentObject(entity, new PlayerAnimatorReference
            {
                Animator = authoring.AnimatorComponent,
                AnimatorController = animationBindingsPreset != null ? animationBindingsPreset.AnimatorController : null,
                ControllerAssigned = 0
            });
            AddComponent(entity, BuildAnimatorParameterConfig(animationBindingsPreset));
            AddComponent(entity, new PlayerAnimatorRuntimeState
            {
                PreviousShooting = 0,
                RecoilValue = 0f,
                AimWeightValue = 0f,
                LeanValue = 0f
            });
        }

        PlayerProgressionPreset progressionPreset = authoring.GetProgressionPreset();

        if (progressionPreset != null)
        {
            BlobAssetReference<PlayerProgressionConfigBlob> progressionBlob = BuildProgressionConfigBlob(progressionPreset);
            AddBlobAsset(ref progressionBlob, out Unity.Entities.Hash128 _);

            PlayerProgressionConfig progressionConfig = new PlayerProgressionConfig
            {
                Config = progressionBlob
            };

            AddComponent(entity, progressionConfig);
        }

        PlayerPowerUpsPreset powerUpsPreset = authoring.GetPowerUpsPreset();

        if (powerUpsPreset != null)
        {
            PlayerPowerUpsConfig powerUpsConfig = BuildPowerUpsConfig(authoring, powerUpsPreset);
            AddComponent(entity, powerUpsConfig);
            PlayerPowerUpVfxCapConfig powerUpVfxCapConfig = BuildPowerUpVfxCapConfig(authoring);
            AddComponent(entity, powerUpVfxCapConfig);
            PlayerElementalVfxConfig elementalVfxConfig = BuildElementalVfxConfig(authoring, powerUpsPreset);
            AddComponent(entity, elementalVfxConfig);

            if (authoring.ElementalTrailAttachedVfxPrefab != null)
            {
                AddComponentObject(entity, new PlayerElementalTrailAttachedVfxPrefabReference
                {
                    Prefab = authoring.ElementalTrailAttachedVfxPrefab
                });
            }

            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = AddBuffer<EquippedPassiveToolElement>(entity);
            PopulateEquippedPassiveToolsBuffer(authoring, equippedPassiveToolsBuffer, powerUpsPreset);
        }

        ShootingSettings shootingSettings = controllerPreset.ShootingSettings;

        if (shootingSettings != null && shootingSettings.ProjectilePrefab != null)
        {
            GameObject projectilePrefabObject = shootingSettings.ProjectilePrefab;

            if (IsInvalidProjectilePrefab(authoring, projectilePrefabObject))
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
    }
    #endregion

    #region Validation
    /// <summary>
    /// This method checks if the assigned projectile prefab is invalid for use in the shooting system. 
    /// It ensures that the projectile prefab is not null, is not the same GameObject as the player, 
    /// and does not contain a PlayerAuthoring component to prevent recursive nesting of player prefabs.
    /// </summary>
    /// <param name="authoring"></param>
    /// <param name="projectilePrefabObject"></param>
    /// <returns></returns>
    private static bool IsInvalidProjectilePrefab(PlayerAuthoring authoring, GameObject projectilePrefabObject)
    {
        if (projectilePrefabObject == null)
            return true;

        if (projectilePrefabObject == authoring.gameObject)
            return true;

        if (projectilePrefabObject.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }
    #endregion

    #region Blob Building
    /// <summary>
    /// Creates a blob asset reference containing player controller configuration data based on the specified preset.
    /// </summary>
    /// <param name="preset">The player controller preset used to populate the configuration blob.</param>
    /// <returns>A persistent blob asset reference to the constructed player controller configuration blob.</returns>
    private BlobAssetReference<PlayerControllerConfigBlob> BuildConfigBlob(PlayerControllerPreset preset)
    {
        BlobBuilder builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
        ref PlayerControllerConfigBlob root = ref builder.ConstructRoot<PlayerControllerConfigBlob>();

        FillMovementConfig(ref root, preset.MovementSettings);
        FillLookConfig(ref root, preset.LookSettings, ref builder);
        FillCameraConfig(ref root, preset.CameraSettings);
        FillShootingConfig(ref root, preset.ShootingSettings);

        BlobAssetReference<PlayerControllerConfigBlob> blob = builder.CreateBlobAssetReference<PlayerControllerConfigBlob>(Unity.Collections.Allocator.Persistent);
        builder.Dispose();

        return blob;
    }

    /// <summary>
    /// Builds runtime world-layer configuration from the selected master preset.
    /// </summary>
    /// <param name="masterPreset">Master preset containing layer names.</param>
    /// <returns>Runtime world-layer config with resolved masks.</returns>
    private static PlayerWorldLayersConfig BuildWorldLayersConfig(PlayerMasterPreset masterPreset)
    {
        string wallsLayerName = ResolveWallsLayerName(masterPreset);
        int wallsLayerMask = ResolveLayerMaskByName(wallsLayerName);

        if (wallsLayerMask == 0)
            wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        return new PlayerWorldLayersConfig
        {
            WallsLayerMask = wallsLayerMask
        };
    }

    /// <summary>
    /// Resolves the walls layer name configured in the master preset.
    /// </summary>
    /// <param name="masterPreset">Preset source.</param>
    /// <returns>Configured layer name, or the project default walls layer name.</returns>
    private static string ResolveWallsLayerName(PlayerMasterPreset masterPreset)
    {
        if (masterPreset == null)
            return WorldWallCollisionUtility.DefaultWallsLayerName;

        string wallsLayerName = masterPreset.WallsLayerName;

        if (string.IsNullOrWhiteSpace(wallsLayerName))
            return WorldWallCollisionUtility.DefaultWallsLayerName;

        return wallsLayerName.Trim();
    }

    /// <summary>
    /// Resolves a single layer name to its bitmask value.
    /// </summary>
    /// <param name="layerName">Layer name to resolve.</param>
    /// <returns>Layer mask for the resolved layer, or zero when not found.</returns>
    private static int ResolveLayerMaskByName(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return 0;

        int layerIndex = LayerMask.NameToLayer(layerName);

        if (layerIndex < 0)
            return 0;

        return 1 << layerIndex;
    }

    /// <summary>
    /// Builds runtime animator parameter hash configuration from animation bindings preset.
    /// Falls back to default parameter names when the preset is missing.
    /// </summary>
    /// <param name="preset">Animation bindings preset selected in the master preset.</param>
    /// <returns>Baked animator parameter config.</returns>
    private static PlayerAnimatorParameterConfig BuildAnimatorParameterConfig(PlayerAnimationBindingsPreset preset)
    {
        string moveXParameter = preset != null ? preset.MoveXParameter : "MoveX";
        string moveYParameter = preset != null ? preset.MoveYParameter : "MoveY";
        string moveSpeedParameter = preset != null ? preset.MoveSpeedParameter : "MoveSpeed";
        string aimXParameter = preset != null ? preset.AimXParameter : "AimX";
        string aimYParameter = preset != null ? preset.AimYParameter : "AimY";
        string isMovingParameter = preset != null ? preset.IsMovingParameter : "IsMoving";
        string isShootingParameter = preset != null ? preset.IsShootingParameter : "IsShooting";
        string isDashingParameter = preset != null ? preset.IsDashingParameter : "IsDashing";
        string shotPulseParameter = preset != null ? preset.ShotPulseParameter : "ShotPulse";
        string proceduralRecoilParameter = preset != null ? preset.ProceduralRecoilParameter : "ProcRecoil";
        string proceduralAimWeightParameter = preset != null ? preset.ProceduralAimWeightParameter : "ProcAimWeight";
        string proceduralLeanParameter = preset != null ? preset.ProceduralLeanParameter : "ProcLean";

        PlayerAnimatorParameterConfig config = default;
        byte hasMoveX;
        byte hasMoveY;
        byte hasMoveSpeed;
        byte hasAimX;
        byte hasAimY;
        byte hasIsMoving;
        byte hasIsShooting;
        byte hasIsDashing;
        byte hasShotPulse;
        byte hasProceduralRecoil;
        byte hasProceduralAimWeight;
        byte hasProceduralLean;

        config.MoveXHash = ResolveParameterHash(moveXParameter, out hasMoveX);
        config.MoveYHash = ResolveParameterHash(moveYParameter, out hasMoveY);
        config.MoveSpeedHash = ResolveParameterHash(moveSpeedParameter, out hasMoveSpeed);
        config.AimXHash = ResolveParameterHash(aimXParameter, out hasAimX);
        config.AimYHash = ResolveParameterHash(aimYParameter, out hasAimY);
        config.IsMovingHash = ResolveParameterHash(isMovingParameter, out hasIsMoving);
        config.IsShootingHash = ResolveParameterHash(isShootingParameter, out hasIsShooting);
        config.IsDashingHash = ResolveParameterHash(isDashingParameter, out hasIsDashing);
        config.ShotPulseHash = ResolveParameterHash(shotPulseParameter, out hasShotPulse);
        config.ProceduralRecoilHash = ResolveParameterHash(proceduralRecoilParameter, out hasProceduralRecoil);
        config.ProceduralAimWeightHash = ResolveParameterHash(proceduralAimWeightParameter, out hasProceduralAimWeight);
        config.ProceduralLeanHash = ResolveParameterHash(proceduralLeanParameter, out hasProceduralLean);
        config.HasMoveX = hasMoveX;
        config.HasMoveY = hasMoveY;
        config.HasMoveSpeed = hasMoveSpeed;
        config.HasAimX = hasAimX;
        config.HasAimY = hasAimY;
        config.HasIsMoving = hasIsMoving;
        config.HasIsShooting = hasIsShooting;
        config.HasIsDashing = hasIsDashing;
        config.HasShotPulse = hasShotPulse;
        config.HasProceduralRecoil = hasProceduralRecoil;
        config.HasProceduralAimWeight = hasProceduralAimWeight;
        config.HasProceduralLean = hasProceduralLean;
        config.FloatDampTime = preset != null ? math.max(0f, preset.FloatDampTime) : 0.08f;
        config.MovingSpeedThreshold = preset != null ? math.max(0f, preset.MovingSpeedThreshold) : 0.02f;
        config.UseFloatDamping = preset == null || preset.UseFloatDamping ? (byte)1 : (byte)0;
        config.DisableRootMotion = preset == null || preset.DisableRootMotion ? (byte)1 : (byte)0;
        config.ProceduralRecoilEnabled = preset != null && preset.ProceduralRecoilEnabled ? (byte)1 : (byte)0;
        config.ProceduralAimWeightEnabled = preset != null && preset.ProceduralAimWeightEnabled ? (byte)1 : (byte)0;
        config.ProceduralLeanEnabled = preset != null && preset.ProceduralLeanEnabled ? (byte)1 : (byte)0;
        config.ProceduralRecoilKick = preset != null ? math.max(0f, preset.ProceduralRecoilKick) : 0f;
        config.ProceduralRecoilRecoveryPerSecond = preset != null ? math.max(0f, preset.ProceduralRecoilRecoveryPerSecond) : 0f;
        config.ProceduralAimWeightSmoothing = preset != null ? math.max(0f, preset.ProceduralAimWeightSmoothing) : 0f;
        config.ProceduralLeanSmoothing = preset != null ? math.max(0f, preset.ProceduralLeanSmoothing) : 0f;
        return config;
    }

    private static int ResolveParameterHash(string parameterName, out byte hasParameter)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            hasParameter = 0;
            return 0;
        }

        hasParameter = 1;
        return Animator.StringToHash(parameterName.Trim());
    }

    /// <summary>
    /// Creates a blob asset reference containing player progression configuration data based on the specified preset.
    /// </summary>
    /// <param name="preset">The player progression preset used to populate the configuration blob.</param>
    /// <returns>A persistent blob asset reference to the constructed player progression configuration blob.</returns>
    private BlobAssetReference<PlayerProgressionConfigBlob> BuildProgressionConfigBlob(PlayerProgressionPreset preset)
    {
        BlobBuilder builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
        ref PlayerProgressionConfigBlob root = ref builder.ConstructRoot<PlayerProgressionConfigBlob>();

        PlayerProgressionBaseStats progressionBaseStats = preset.BaseStats;
        float health = progressionBaseStats != null ? progressionBaseStats.Health : 100f;
        float experience = progressionBaseStats != null ? progressionBaseStats.Experience : 0f;

        if (health < 1f)
            health = 1f;

        if (experience < 0f)
            experience = 0f;

        root.BaseStats = new PlayerProgressionBaseStatsBlob
        {
            Health = health,
            Experience = experience
        };

        BlobAssetReference<PlayerProgressionConfigBlob> blob = builder.CreateBlobAssetReference<PlayerProgressionConfigBlob>(Unity.Collections.Allocator.Persistent);
        builder.Dispose();

        return blob;
    }

    /// <summary>
    /// Builds runtime power-up config from the selected power-ups preset.
    /// </summary>
    /// <param name="preset">Source power-ups preset.</param>
    /// <returns>Runtime power-up config with resolved primary/secondary loadout slots.</returns>
    private PlayerPowerUpsConfig BuildPowerUpsConfig(PlayerAuthoring authoring, PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return default;

        ActiveToolDefinition primaryTool = ResolveLoadoutTool(preset, preset.PrimaryActiveToolId, 0);
        ActiveToolDefinition secondaryTool = ResolveLoadoutTool(preset, preset.SecondaryActiveToolId, 1);
        PlayerPowerUpSlotConfig primarySlotConfig = BuildSlotConfig(authoring, primaryTool);
        PlayerPowerUpSlotConfig secondarySlotConfig = BuildSlotConfig(authoring, secondaryTool);

        return new PlayerPowerUpsConfig
        {
            PrimarySlot = primarySlotConfig,
            SecondarySlot = secondarySlotConfig
        };
    }

    /// <summary>
    /// Builds runtime VFX cap settings used by the pooled power-up VFX pipeline.
    /// </summary>
    /// <param name="authoring">Source player authoring values.</param>
    /// <returns>Baked VFX cap config.</returns>
    private static PlayerPowerUpVfxCapConfig BuildPowerUpVfxCapConfig(PlayerAuthoring authoring)
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
    /// Builds runtime elemental VFX assignments shared across all elemental passives.
    /// </summary>
    /// <param name="authoring">Source authoring component.</param>
    /// <param name="preset">Source power-ups preset.</param>
    /// <returns>Baked elemental VFX config.</returns>
    private PlayerElementalVfxConfig BuildElementalVfxConfig(PlayerAuthoring authoring, PlayerPowerUpsPreset preset)
    {
        IReadOnlyList<ElementalVfxByElementData> assignments = preset != null ? preset.ElementalVfxByElement : null;

        return new PlayerElementalVfxConfig
        {
            Fire = BuildElementalVfxDefinitionConfig(authoring,
                                                     ResolveElementalVfxAssignment(assignments, ElementType.Fire),
                                                     "Elemental Fire"),
            Ice = BuildElementalVfxDefinitionConfig(authoring,
                                                    ResolveElementalVfxAssignment(assignments, ElementType.Ice),
                                                    "Elemental Ice"),
            Poison = BuildElementalVfxDefinitionConfig(authoring,
                                                       ResolveElementalVfxAssignment(assignments, ElementType.Poison),
                                                       "Elemental Poison"),
            Custom = BuildElementalVfxDefinitionConfig(authoring,
                                                       ResolveElementalVfxAssignment(assignments, ElementType.Custom),
                                                       "Elemental Custom")
        };
    }

    private static ElementalVfxByElementData ResolveElementalVfxAssignment(IReadOnlyList<ElementalVfxByElementData> assignments, ElementType elementType)
    {
        if (assignments == null)
            return null;

        for (int index = 0; index < assignments.Count; index++)
        {
            ElementalVfxByElementData assignment = assignments[index];

            if (assignment == null)
                continue;

            if (assignment.ElementType != elementType)
                continue;

            return assignment;
        }

        return null;
    }

    private ElementalVfxDefinitionConfig BuildElementalVfxDefinitionConfig(PlayerAuthoring authoring, ElementalVfxByElementData assignment, string labelPrefix)
    {
        if (assignment == null)
            return default;

        Entity stackVfxPrefabEntity = ResolveOptionalPowerUpPrefabEntity(authoring, assignment.StackVfxPrefab, labelPrefix + " Stack VFX");
        Entity procVfxPrefabEntity = ResolveOptionalPowerUpPrefabEntity(authoring, assignment.ProcVfxPrefab, labelPrefix + " Proc VFX");

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

    /// <summary>
    /// Populates the equipped passive-tools runtime buffer from loadout IDs.
    /// </summary>
    /// <param name="equippedPassiveToolsBuffer">Target runtime buffer.</param>
    /// <param name="preset">Source power-ups preset.</param>
    private void PopulateEquippedPassiveToolsBuffer(PlayerAuthoring authoring, DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer, PlayerPowerUpsPreset preset)
    {
        if (preset == null)
            return;

        IReadOnlyList<string> equippedPassiveToolIds = preset.EquippedPassiveToolIds;

        if (equippedPassiveToolIds == null || equippedPassiveToolIds.Count == 0)
            return;

        HashSet<string> visitedPassiveToolIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < equippedPassiveToolIds.Count; index++)
        {
            string passiveToolId = equippedPassiveToolIds[index];

            if (string.IsNullOrWhiteSpace(passiveToolId))
                continue;

            if (visitedPassiveToolIds.Add(passiveToolId) == false)
                continue;

            PassiveToolDefinition passiveTool = ResolveLoadoutPassiveTool(preset, passiveToolId);

            if (passiveTool == null)
                continue;

            PlayerPassiveToolConfig passiveToolConfig = BuildPassiveToolConfig(authoring, passiveTool);

            if (passiveToolConfig.IsDefined == 0)
                continue;

            equippedPassiveToolsBuffer.Add(new EquippedPassiveToolElement
            {
                Tool = passiveToolConfig
            });
        }
    }

    /// <summary>
    /// Resolves a loadout tool by ID with fallback to a positional index.
    /// </summary>
    /// <param name="preset">Source preset.</param>
    /// <param name="toolId">Requested PowerUpId.</param>
    /// <param name="fallbackIndex">Fallback index inside active tools list.</param>
    /// <returns>Resolved tool definition or null if no tool can be resolved.</returns>
    private static ActiveToolDefinition ResolveLoadoutTool(PlayerPowerUpsPreset preset, string toolId, int fallbackIndex)
    {
        if (preset == null)
            return null;

        IReadOnlyList<ActiveToolDefinition> activeTools = preset.ActiveTools;

        if (activeTools == null)
            return null;

        if (string.IsNullOrWhiteSpace(toolId) == false)
        {
            for (int index = 0; index < activeTools.Count; index++)
            {
                ActiveToolDefinition activeTool = activeTools[index];

                if (activeTool == null)
                    continue;

                PowerUpCommonData commonData = activeTool.CommonData;

                if (commonData == null)
                    continue;

                if (string.Equals(commonData.PowerUpId, toolId, System.StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                return activeTool;
            }
        }

        if (fallbackIndex >= 0 && fallbackIndex < activeTools.Count)
            return activeTools[fallbackIndex];

        return null;
    }

    /// <summary>
    /// Resolves a passive loadout tool by ID.
    /// </summary>
    /// <param name="preset">Source preset.</param>
    /// <param name="toolId">Requested PowerUpId.</param>
    /// <returns>Resolved passive tool definition or null if no tool can be resolved.</returns>
    private static PassiveToolDefinition ResolveLoadoutPassiveTool(PlayerPowerUpsPreset preset, string toolId)
    {
        if (preset == null)
            return null;

        IReadOnlyList<PassiveToolDefinition> passiveTools = preset.PassiveTools;

        if (passiveTools == null)
            return null;

        if (string.IsNullOrWhiteSpace(toolId) == false)
        {
            for (int index = 0; index < passiveTools.Count; index++)
            {
                PassiveToolDefinition passiveTool = passiveTools[index];

                if (passiveTool == null)
                    continue;

                PowerUpCommonData commonData = passiveTool.CommonData;

                if (commonData == null)
                    continue;

                if (string.Equals(commonData.PowerUpId, toolId, System.StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                return passiveTool;
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a passive tool definition into a blittable runtime slot config.
    /// </summary>
    /// <param name="passiveTool">Passive tool definition to convert.</param>
    /// <returns>Baked passive slot config.</returns>
    private PlayerPassiveToolConfig BuildPassiveToolConfig(PlayerAuthoring authoring, PassiveToolDefinition passiveTool)
    {
        if (passiveTool == null)
            return default;

        PassiveToolKind toolKind = passiveTool.ToolKind;

        if (toolKind == PassiveToolKind.Custom)
            toolKind = PassiveToolKind.ProjectileSize;

        ProjectileSizePassiveConfig projectileSizeConfig = BuildProjectileSizePassiveConfig(passiveTool.ProjectileSizeData);
        ElementalProjectilesPassiveConfig elementalProjectilesConfig = BuildElementalProjectilesPassiveConfig(passiveTool.ElementalProjectilesData);
        PerfectCirclePassiveConfig perfectCircleConfig = BuildPerfectCirclePassiveConfig(passiveTool.PerfectCircleData);
        BouncingProjectilesPassiveConfig bouncingProjectilesConfig = BuildBouncingProjectilesPassiveConfig(passiveTool.BouncingProjectilesData);
        SplittingProjectilesPassiveConfig splittingProjectilesConfig = BuildSplittingProjectilesPassiveConfig(passiveTool.SplittingProjectilesData);
        ExplosionPassiveConfig explosionConfig = BuildExplosionPassiveConfig(authoring, passiveTool.ExplosionData);
        ElementalTrailPassiveConfig elementalTrailConfig = BuildElementalTrailPassiveConfig(authoring, passiveTool.ElementalTrailData);

        return new PlayerPassiveToolConfig
        {
            IsDefined = 1,
            ToolKind = toolKind,
            ProjectileSize = projectileSizeConfig,
            ElementalProjectiles = elementalProjectilesConfig,
            PerfectCircle = perfectCircleConfig,
            BouncingProjectiles = bouncingProjectilesConfig,
            SplittingProjectiles = splittingProjectilesConfig,
            Explosion = explosionConfig,
            ElementalTrail = elementalTrailConfig
        };
    }

    /// <summary>
    /// Converts projectile-size passive authoring data into runtime-safe values.
    /// </summary>
    /// <param name="projectileSizeData">Source projectile passive data.</param>
    /// <returns>Baked projectile passive config.</returns>
    private static ProjectileSizePassiveConfig BuildProjectileSizePassiveConfig(ProjectileSizePassiveToolData projectileSizeData)
    {
        float sizeMultiplier = 1f;
        float damageMultiplier = 1f;
        float speedMultiplier = 1f;
        float lifetimeSecondsMultiplier = 1f;
        float lifetimeRangeMultiplier = 1f;

        if (projectileSizeData != null)
        {
            sizeMultiplier = math.max(0.01f, projectileSizeData.ProjectileSizeMultiplier);
            damageMultiplier = math.max(0f, projectileSizeData.DamageMultiplier);
            speedMultiplier = math.max(0f, projectileSizeData.SpeedMultiplier);
            lifetimeSecondsMultiplier = math.max(0f, projectileSizeData.LifetimeSecondsMultiplier);
            lifetimeRangeMultiplier = math.max(0f, projectileSizeData.LifetimeRangeMultiplier);
        }

        return new ProjectileSizePassiveConfig
        {
            SizeMultiplier = sizeMultiplier,
            DamageMultiplier = damageMultiplier,
            SpeedMultiplier = speedMultiplier,
            LifetimeSecondsMultiplier = lifetimeSecondsMultiplier,
            LifetimeRangeMultiplier = lifetimeRangeMultiplier
        };
    }

    private ElementalProjectilesPassiveConfig BuildElementalProjectilesPassiveConfig(ElementalProjectilesPassiveToolData elementalProjectilesData)
    {
        ElementalEffectConfig effectConfig = BuildElementalEffectConfig(elementalProjectilesData != null ? elementalProjectilesData.EffectData : null);
        float stacksPerHit = elementalProjectilesData != null ? math.max(0f, elementalProjectilesData.StacksPerHit) : 0f;

        return new ElementalProjectilesPassiveConfig
        {
            Effect = effectConfig,
            StacksPerHit = stacksPerHit
        };
    }

    private static PerfectCirclePassiveConfig BuildPerfectCirclePassiveConfig(PerfectCirclePassiveToolData perfectCircleData)
    {
        return new PerfectCirclePassiveConfig
        {
            RadialEntrySpeed = perfectCircleData != null ? math.max(0f, perfectCircleData.RadialEntrySpeed) : 0f,
            OrbitalSpeed = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitalSpeed) : 0f,
            OrbitRadiusMin = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitRadiusMin) : 0f,
            OrbitRadiusMax = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitRadiusMax) : 0f,
            OrbitPulseFrequency = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitPulseFrequency) : 0f,
            OrbitEntryRatio = perfectCircleData != null ? math.clamp(perfectCircleData.OrbitEntryRatio, 0f, 1f) : 0f,
            OrbitBlendDuration = perfectCircleData != null ? math.max(0f, perfectCircleData.OrbitBlendDuration) : 0f,
            HeightOffset = perfectCircleData != null ? perfectCircleData.HeightOffset : 0f,
            GoldenAngleDegrees = perfectCircleData != null ? math.max(0f, perfectCircleData.GoldenAngleDegrees) : 137.5f
        };
    }

    private static BouncingProjectilesPassiveConfig BuildBouncingProjectilesPassiveConfig(BouncingProjectilesPassiveToolData bouncingProjectilesData)
    {
        return new BouncingProjectilesPassiveConfig
        {
            MaxBounces = bouncingProjectilesData != null ? math.max(0, bouncingProjectilesData.MaxBounces) : 0,
            SpeedPercentChangePerBounce = bouncingProjectilesData != null ? bouncingProjectilesData.SpeedPercentChangePerBounce : 0f,
            MinimumSpeedMultiplierAfterBounce = bouncingProjectilesData != null ? math.max(0f, bouncingProjectilesData.MinimumSpeedMultiplierAfterBounce) : 0f,
            MaximumSpeedMultiplierAfterBounce = bouncingProjectilesData != null ? math.max(0f, bouncingProjectilesData.MaximumSpeedMultiplierAfterBounce) : 0f
        };
    }

    private static SplittingProjectilesPassiveConfig BuildSplittingProjectilesPassiveConfig(SplittingProjectilesPassiveToolData splittingProjectilesData)
    {
        SplittingProjectilesPassiveConfig config = default;

        if (splittingProjectilesData == null)
            return config;

        FixedList128Bytes<float> customAngles = default;
        IReadOnlyList<float> customAnglesSource = splittingProjectilesData.CustomAnglesDegrees;

        if (customAnglesSource != null)
        {
            for (int index = 0; index < customAnglesSource.Count; index++)
            {
                if (customAngles.Length >= customAngles.Capacity)
                    break;

                customAngles.Add(customAnglesSource[index]);
            }
        }

        config.DirectionMode = splittingProjectilesData.DirectionMode;
        config.SplitProjectileCount = math.max(1, splittingProjectilesData.SplitProjectileCount);
        config.SplitOffsetDegrees = splittingProjectilesData.SplitOffsetDegrees;
        config.CustomAnglesDegrees = customAngles;
        config.SplitDamageMultiplier = math.max(0f, splittingProjectilesData.SplitDamagePercentFromOriginal * 0.01f);
        config.SplitSizeMultiplier = math.max(0f, splittingProjectilesData.SplitSizePercentFromOriginal * 0.01f);
        config.SplitSpeedMultiplier = math.max(0f, splittingProjectilesData.SplitSpeedPercentFromOriginal * 0.01f);
        config.SplitLifetimeMultiplier = math.max(0f, splittingProjectilesData.SplitLifetimePercentFromOriginal * 0.01f);

        return config;
    }

    private ExplosionPassiveConfig BuildExplosionPassiveConfig(PlayerAuthoring authoring, ExplosionPassiveToolData explosionData)
    {
        if (explosionData == null)
            return default;

        Entity explosionVfxPrefabEntity = ResolveOptionalPowerUpPrefabEntity(authoring, explosionData.ExplosionVfxPrefab, "Passive Explosion VFX");

        return new ExplosionPassiveConfig
        {
            TriggerMode = explosionData.TriggerMode,
            CooldownSeconds = math.max(0f, explosionData.CooldownSeconds),
            Radius = math.max(0f, explosionData.Radius),
            Damage = math.max(0f, explosionData.Damage),
            AffectAllEnemiesInRadius = explosionData.AffectAllEnemiesInRadius ? (byte)1 : (byte)0,
            TriggerOffset = new float3(explosionData.TriggerOffset.x, explosionData.TriggerOffset.y, explosionData.TriggerOffset.z),
            ExplosionVfxPrefabEntity = explosionVfxPrefabEntity,
            ScaleVfxToRadius = explosionData.ScaleVfxToRadius ? (byte)1 : (byte)0,
            VfxScaleMultiplier = math.max(0.01f, explosionData.VfxScaleMultiplier)
        };
    }

    private ElementalTrailPassiveConfig BuildElementalTrailPassiveConfig(PlayerAuthoring authoring, ElementalTrailPassiveToolData elementalTrailData)
    {
        if (elementalTrailData == null)
            return default;

        ElementalEffectConfig effectConfig = BuildElementalEffectConfig(elementalTrailData.EffectData);
        Entity trailAttachedVfxPrefabEntity = ResolveOptionalPowerUpPrefabEntity(authoring, authoring.ElementalTrailAttachedVfxPrefab, "Elemental Trail Attached VFX");

        return new ElementalTrailPassiveConfig
        {
            Effect = effectConfig,
            TrailSegmentLifetimeSeconds = math.max(0.05f, elementalTrailData.TrailSegmentLifetimeSeconds),
            TrailSpawnDistance = math.max(0f, elementalTrailData.TrailSpawnDistance),
            TrailSpawnIntervalSeconds = math.max(0.01f, elementalTrailData.TrailSpawnIntervalSeconds),
            TrailRadius = math.max(0f, elementalTrailData.TrailRadius),
            MaxActiveSegmentsPerPlayer = math.max(1, elementalTrailData.MaxActiveSegmentsPerPlayer),
            StacksPerTick = math.max(0f, elementalTrailData.StacksPerTick),
            ApplyIntervalSeconds = math.max(0.01f, elementalTrailData.ApplyIntervalSeconds),
            TrailAttachedVfxPrefabEntity = trailAttachedVfxPrefabEntity,
            TrailAttachedVfxScaleMultiplier = math.max(0.01f, authoring.ElementalTrailAttachedVfxScaleMultiplier),
            TrailAttachedVfxOffset = new float3(elementalTrailData.TrailAttachedVfxOffset.x,
                                                elementalTrailData.TrailAttachedVfxOffset.y,
                                                elementalTrailData.TrailAttachedVfxOffset.z)
        };
    }

    private static ElementalEffectConfig BuildElementalEffectConfig(ElementalEffectDefinitionData effectData)
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

    private Entity ResolveOptionalPowerUpPrefabEntity(PlayerAuthoring authoring, GameObject optionalPrefab, string contextLabel)
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

        return GetEntity(optionalPrefab, TransformUsageFlags.Dynamic);
    }

    /// <summary>
    /// Converts an active tool definition into a blittable runtime slot config.
    /// </summary>
    /// <param name="authoring">Owning player authoring instance.</param>
    /// <param name="activeTool">Tool definition to convert.</param>
    /// <returns>Baked slot config.</returns>
    private PlayerPowerUpSlotConfig BuildSlotConfig(PlayerAuthoring authoring, ActiveToolDefinition activeTool)
    {
        if (activeTool == null)
            return default;

        BombToolData bombData = activeTool.BombData;
        DashToolData dashData = activeTool.DashData;
        BulletTimeToolData bulletTimeData = activeTool.BulletTimeData;
        Entity bombPrefabEntity = Entity.Null;

        if (activeTool.ToolKind == ActiveToolKind.Bomb && bombData != null && bombData.BombPrefab != null)
        {
            GameObject bombPrefab = bombData.BombPrefab;

            if (IsInvalidBombPrefab(authoring, bombPrefab) == false)
                bombPrefabEntity = GetEntity(bombPrefab, TransformUsageFlags.Dynamic);
            else
            {
#if UNITY_EDITOR
                if (authoring != null)
                    Debug.LogError(string.Format("[PlayerAuthoringBaker] Invalid bomb prefab '{0}' on '{1}'. Assign a dedicated bomb prefab without PlayerAuthoring.", bombPrefab.name, authoring.name), authoring);
#endif
            }
        }

        // Clamp and sanitize values to ensure stability.
        // Invalid values in the authoring data (e.g. negative costs or durations)
        //  will be clamped to reasonable defaults instead.
        float maximumEnergy = math.max(0f, activeTool.MaximumEnergy);
        float activationCost = math.max(0f, activeTool.ActivationCost);
        float maintenanceCostPerSecond = math.max(0f, activeTool.MaintenanceCostPerSecond);
        float chargePerTrigger = math.max(0f, activeTool.ChargePerTrigger);
        float3 bombSpawnOffset = bombData != null ? new float3(bombData.SpawnOffset.x, bombData.SpawnOffset.y, bombData.SpawnOffset.z) : float3.zero;
        float bombDeploySpeed = bombData != null ? math.max(0f, bombData.DeploySpeed) : 0f;
        float bombCollisionRadius = bombData != null ? math.max(0.01f, bombData.CollisionRadius) : 0.1f;
        byte bombBounceOnWalls = bombData != null && bombData.BounceOnWalls ? (byte)1 : (byte)0;
        float bombBounceDamping = bombData != null ? math.clamp(bombData.BounceDamping, 0f, 1f) : 0f;
        float bombLinearDampingPerSecond = bombData != null ? math.max(0f, bombData.LinearDampingPerSecond) : 0f;
        float bombFuseSeconds = bombData != null ? math.max(0.05f, bombData.FuseSeconds) : 0.05f;
        float bombRadius = bombData != null ? math.max(0.1f, bombData.Radius) : 0.1f;
        float bombDamage = bombData != null ? math.max(0f, bombData.Damage) : 0f;
        byte bombAffectAll = bombData != null && bombData.AffectAllEnemiesInRadius ? (byte)1 : (byte)0;
        float dashDistance = dashData != null ? math.max(0f, dashData.Distance) : 0f;
        float dashDuration = dashData != null ? math.max(0.01f, dashData.Duration) : 0.01f;
        float dashSpeedTransitionInSeconds = dashData != null ? math.max(0f, dashData.SpeedTransitionInSeconds) : 0f;
        float dashSpeedTransitionOutSeconds = dashData != null ? math.max(0f, dashData.SpeedTransitionOutSeconds) : 0f;
        byte dashInvulnerability = dashData != null && dashData.GrantsInvulnerability ? (byte)1 : (byte)0;
        float dashInvulnerabilityExtraTime = dashData != null ? math.max(0f, dashData.InvulnerabilityExtraTime) : 0f;
        float bulletTimeDuration = bulletTimeData != null ? math.max(0.05f, bulletTimeData.Duration) : 0.05f;
        float bulletTimeEnemySlowPercent = bulletTimeData != null ? math.clamp(bulletTimeData.EnemySlowPercent, 0f, 100f) : 0f;
        ActiveToolKind toolKind = activeTool.ToolKind;

        if (toolKind == ActiveToolKind.Custom)
            toolKind = ActiveToolKind.Bomb;

        return new PlayerPowerUpSlotConfig
        {
            IsDefined = 1,
            ToolKind = toolKind,
            ActivationResource = activeTool.ActivationResource,
            MaintenanceResource = activeTool.MaintenanceResource,
            ChargeType = activeTool.ChargeType,
            MaximumEnergy = maximumEnergy,
            ActivationCost = activationCost,
            MaintenanceCostPerSecond = maintenanceCostPerSecond,
            ChargePerTrigger = chargePerTrigger,
            Toggleable = activeTool.Toggleable ? (byte)1 : (byte)0,
            FullChargeRequirement = activeTool.FullChargeRequirement ? (byte)1 : (byte)0,
            Unreplaceable = activeTool.Unreplaceable ? (byte)1 : (byte)0,
            BombPrefabEntity = bombPrefabEntity,
            Bomb = new BombPowerUpConfig
            {
                SpawnOffset = bombSpawnOffset,
                DeploySpeed = bombDeploySpeed,
                CollisionRadius = bombCollisionRadius,
                BounceOnWalls = bombBounceOnWalls,
                BounceDamping = bombBounceDamping,
                LinearDampingPerSecond = bombLinearDampingPerSecond,
                FuseSeconds = bombFuseSeconds,
                Radius = bombRadius,
                Damage = bombDamage,
                AffectAllEnemiesInRadius = bombAffectAll
            },
            Dash = new DashPowerUpConfig
            {
                Distance = dashDistance,
                Duration = dashDuration,
                SpeedTransitionInSeconds = dashSpeedTransitionInSeconds,
                SpeedTransitionOutSeconds = dashSpeedTransitionOutSeconds,
                GrantsInvulnerability = dashInvulnerability,
                InvulnerabilityExtraTime = dashInvulnerabilityExtraTime
            },
            BulletTime = new BulletTimePowerUpConfig
            {
                Duration = bulletTimeDuration,
                EnemySlowPercent = bulletTimeEnemySlowPercent
            }
        };
    }

    /// <summary>
    /// Validates bomb prefab references used by active tools.
    /// </summary>
    /// <param name="authoring">Owning player authoring instance.</param>
    /// <param name="bombPrefab">Bomb prefab candidate.</param>
    /// <returns>True when prefab is invalid for runtime usage.</returns>
    private static bool IsInvalidBombPrefab(PlayerAuthoring authoring, GameObject bombPrefab)
    {
        if (bombPrefab == null)
            return true;

        if (authoring != null && bombPrefab == authoring.gameObject)
            return true;

        if (bombPrefab.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    private static bool IsInvalidPowerUpPrefab(PlayerAuthoring authoring, GameObject optionalPrefab)
    {
        if (optionalPrefab == null)
            return true;

        if (authoring != null && optionalPrefab == authoring.gameObject)
            return true;

        if (optionalPrefab.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }
    
    #region Config Filling
    /// <summary>
    /// Populates the Movement configuration in the specified PlayerControllerConfigBlob using values from the provided
    /// MovementSettings.
    /// </summary>
    /// <param name="root">The PlayerControllerConfigBlob to update with movement configuration.</param>
    /// <param name="movementSettings">The MovementSettings containing values to apply.</param>
    private void FillMovementConfig(ref PlayerControllerConfigBlob root, MovementSettings movementSettings)
    {
        MovementConfig movementConfig = new MovementConfig
        {
            DirectionsMode = movementSettings.DirectionsMode,
            DiscreteDirectionCount = movementSettings.DiscreteDirectionCount,
            DirectionOffsetDegrees = movementSettings.DirectionOffsetDegrees,
            MovementReference = movementSettings.MovementReference,
            Values = new MovementValuesBlob
            {
                BaseSpeed = movementSettings.Values.BaseSpeed,
                MaxSpeed = movementSettings.Values.MaxSpeed,
                Acceleration = movementSettings.Values.Acceleration,
                Deceleration = movementSettings.Values.Deceleration,
                OppositeDirectionBrakeMultiplier = movementSettings.Values.OppositeDirectionBrakeMultiplier,
                WallBounceCoefficient = movementSettings.Values.WallBounceCoefficient,
                WallCollisionSkinWidth = movementSettings.Values.WallCollisionSkinWidth,
                InputDeadZone = movementSettings.Values.InputDeadZone,
                DigitalReleaseGraceSeconds = movementSettings.Values.DigitalReleaseGraceSeconds
            }
        };

        root.Movement = movementConfig;
    }

    /// <summary>
    /// Populates the Look configuration in the player controller config blob using the provided look settings and blob
    /// builder.
    /// </summary>
    /// <param name="root">Reference to the player controller config blob to update with look configuration.</param>
    /// <param name="lookSettings">Look settings used to configure the look behavior.</param>
    /// <param name="builder">Reference to the blob builder used for memory allocation.</param>
    private void FillLookConfig(ref PlayerControllerConfigBlob root, LookSettings lookSettings, ref BlobBuilder builder)
    {
        ref LookConfig lookConfig = ref root.Look;

        lookConfig.DirectionsMode = lookSettings.DirectionsMode;
        lookConfig.DiscreteDirectionCount = lookSettings.DiscreteDirectionCount;
        lookConfig.DirectionOffsetDegrees = lookSettings.DirectionOffsetDegrees;
        lookConfig.RotationMode = lookSettings.RotationMode;
        lookConfig.RotationSpeed = lookSettings.RotationSpeed;
        lookConfig.MultiplierSampling = lookSettings.MultiplierSampling;
        lookConfig.FrontCone = new ConeConfig
        {
            Enabled = lookSettings.FrontConeEnabled,
            AngleDegrees = lookSettings.FrontConeAngle,
            MaxSpeedMultiplier = lookSettings.FrontConeMaxSpeedMultiplier,
            AccelerationMultiplier = lookSettings.FrontConeAccelerationMultiplier
        };
        lookConfig.BackCone = new ConeConfig
        {
            Enabled = lookSettings.BackConeEnabled,
            AngleDegrees = lookSettings.BackConeAngle,
            MaxSpeedMultiplier = lookSettings.BackConeMaxSpeedMultiplier,
            AccelerationMultiplier = lookSettings.BackConeAccelerationMultiplier
        };
        lookConfig.LeftCone = new ConeConfig
        {
            Enabled = lookSettings.LeftConeEnabled,
            AngleDegrees = lookSettings.LeftConeAngle,
            MaxSpeedMultiplier = lookSettings.LeftConeMaxSpeedMultiplier,
            AccelerationMultiplier = lookSettings.LeftConeAccelerationMultiplier
        };
        lookConfig.RightCone = new ConeConfig
        {
            Enabled = lookSettings.RightConeEnabled,
            AngleDegrees = lookSettings.RightConeAngle,
            MaxSpeedMultiplier = lookSettings.RightConeMaxSpeedMultiplier,
            AccelerationMultiplier = lookSettings.RightConeAccelerationMultiplier
        };
        lookConfig.Values = new LookValuesBlob
        {
            RotationDamping = lookSettings.Values.RotationDamping,
            RotationMaxSpeed = lookSettings.Values.RotationMaxSpeed,
            RotationDeadZone = lookSettings.Values.RotationDeadZone,
            DigitalReleaseGraceSeconds = lookSettings.Values.DigitalReleaseGraceSeconds
        };

        int maxSpeedCount = lookSettings.DiscreteDirectionMaxSpeedMultipliers.Count;
        int accelerationCount = lookSettings.DiscreteDirectionAccelerationMultipliers.Count;
        int multipliersCount = Mathf.Max(maxSpeedCount, accelerationCount);

        BlobBuilderArray<float> maxSpeedArray = builder.Allocate(ref lookConfig.DiscreteMaxSpeedMultipliers, multipliersCount);
        BlobBuilderArray<float> accelerationArray = builder.Allocate(ref lookConfig.DiscreteAccelerationMultipliers, multipliersCount);

        for (int i = 0; i < multipliersCount; i++)
        {
            float maxSpeedMultiplier = i < maxSpeedCount ? lookSettings.DiscreteDirectionMaxSpeedMultipliers[i] : 1f;
            float accelerationMultiplier = i < accelerationCount ? lookSettings.DiscreteDirectionAccelerationMultipliers[i] : 1f;
            maxSpeedArray[i] = maxSpeedMultiplier;
            accelerationArray[i] = accelerationMultiplier;
        }

    }

    /// <summary>
    /// Populates the camera configuration in the player controller config blob using the specified camera settings.
    /// </summary>
    /// <param name="root">Reference to the player controller config blob to update with camera configuration.</param>
    /// <param name="cameraSettings">Camera settings used to fill the camera configuration.</param>
    private void FillCameraConfig(ref PlayerControllerConfigBlob root, CameraSettings cameraSettings)
    {
        CameraConfig cameraConfig = new CameraConfig
        {
            Behavior = cameraSettings.Behavior,
            FollowOffset = new float3(cameraSettings.FollowOffset.x, cameraSettings.FollowOffset.y, cameraSettings.FollowOffset.z),
            Values = new CameraValuesBlob
            {
                FollowSpeed = cameraSettings.Values.FollowSpeed,
                CameraLag = cameraSettings.Values.CameraLag,
                Damping = cameraSettings.Values.Damping,
                MaxFollowDistance = cameraSettings.Values.MaxFollowDistance,
                DeadZoneRadius = cameraSettings.Values.DeadZoneRadius
            }
        };

        root.Camera = cameraConfig;
    }

    /// <summary>
    /// Populates the shooting configuration in the player controller config blob using the specified shooting settings.
    /// </summary>
    /// <param name="root">Reference to the player controller config blob to update with shooting configuration.</param>
    /// <param name="shootingSettings">Shooting settings used to fill the shooting configuration.</param>
    private void FillShootingConfig(ref PlayerControllerConfigBlob root, ShootingSettings shootingSettings)
    {
        if (shootingSettings == null)
        {
            root.Shooting = default;
            return;
        }

        ShootingConfig shootingConfig = new ShootingConfig
        {
            TriggerMode = shootingSettings.TriggerMode,
            ProjectilesInheritPlayerSpeed = shootingSettings.ProjectilesInheritPlayerSpeed ? (byte)1 : (byte)0,
            ShootOffset = new float3(shootingSettings.ShootOffset.x, shootingSettings.ShootOffset.y, shootingSettings.ShootOffset.z),
            Values = new ShootingValuesBlob
            {
                ShootSpeed = shootingSettings.Values.ShootSpeed,
                RateOfFire = shootingSettings.Values.RateOfFire,
                Range = shootingSettings.Values.Range,
                Lifetime = shootingSettings.Values.Lifetime,
                Damage = shootingSettings.Values.Damage
            }
        };

        root.Shooting = shootingConfig;
    }
    #endregion

    #endregion

}
