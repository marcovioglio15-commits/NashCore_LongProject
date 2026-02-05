using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component for configuring player settings and visualizing player-related gizmos in 
/// the Unity editor.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Master preset used to configure this player instance.")]
    [Header("Preset")]
    [SerializeField] private PlayerMasterPreset m_MasterPreset;

    [Tooltip("Optional radius for gizmo previews in the editor.")]
    [Header("Gizmos")]
    [SerializeField] private float m_GizmoRadius = 2f;
    #endregion

    #endregion

    #region Properties
    public PlayerMasterPreset MasterPreset
    {
        get
        {
            return m_MasterPreset;
        }
    }

    public float GizmoRadius
    {
        get
        {
            return m_GizmoRadius;
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
        if (m_MasterPreset == null)
            return null;

        return m_MasterPreset.ControllerPreset;
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

        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.9f);
        Vector3 center = transform.position;

        if (movementSettings.DirectionsMode == MovementDirectionsMode.AllDirections)
        {
            Gizmos.DrawWireSphere(center, m_GizmoRadius);
            return;
        }

        int count = Mathf.Max(1, movementSettings.DiscreteDirectionCount);
        float step = 360f / count;
        float offset = movementSettings.DirectionOffsetDegrees;

        for (int i = 0; i < count; i++)
        {
            float angle = (i * step) + offset;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Gizmos.DrawLine(center, center + dir * m_GizmoRadius);
        }

        Gizmos.DrawWireSphere(center, m_GizmoRadius);
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
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);

        if (lookSettings.DirectionsMode == LookDirectionsMode.AllDirections)
        {
            Gizmos.DrawWireSphere(center, m_GizmoRadius * 0.9f);
            return;
        }

        if (lookSettings.DirectionsMode == LookDirectionsMode.DiscreteCount)
        {
            int count = Mathf.Max(1, lookSettings.DiscreteDirectionCount);
            float step = 360f / count;
            float offset = lookSettings.DirectionOffsetDegrees;

            for (int i = 0; i < count; i++)
            {
                float angle = (i * step) + offset;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Gizmos.DrawLine(center, center + dir * m_GizmoRadius * 0.9f);
            }

            Gizmos.DrawWireSphere(center, m_GizmoRadius * 0.9f);
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

        Gizmos.DrawLine(center, center + left * m_GizmoRadius);
        Gizmos.DrawLine(center, center + right * m_GizmoRadius);
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

        if (cameraSettings.Behavior == CameraBehavior.FollowWithOffset)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Vector3 offsetPosition = transform.position + cameraSettings.FollowOffset;
            Gizmos.DrawLine(transform.position, offsetPosition);
            Gizmos.DrawWireSphere(offsetPosition, 0.2f);
            return;
        }

        if (cameraSettings.Behavior == CameraBehavior.RoomFixed && cameraSettings.RoomAnchor != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
            Vector3 anchorPosition = cameraSettings.RoomAnchor.position;
            Gizmos.DrawLine(transform.position, anchorPosition);
            Gizmos.DrawWireSphere(anchorPosition, 0.25f);
        }
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
    #endregion

    #endregion

}
