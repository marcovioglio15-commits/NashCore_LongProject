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
