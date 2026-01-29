using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public sealed class PlayerAuthoring : MonoBehaviour
{
    #region Serialized Fields
    [Tooltip("Master preset used to configure this player instance.")]
    [Header("Preset")]
    [SerializeField] private PlayerMasterPreset m_MasterPreset;

    [Tooltip("Optional radius for gizmo previews in the editor.")]
    [Header("Gizmos")]
    [SerializeField] private float m_GizmoRadius = 2f;
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

    #region Unity Methods
    private void OnDrawGizmosSelected()
    {
        PlayerControllerPreset controllerPreset = GetControllerPreset();

        if (controllerPreset == null)
            return;

        DrawMovementGizmo(controllerPreset);
        DrawLookGizmo(controllerPreset);
        DrawCameraGizmo(controllerPreset);
    }
    #endregion

    #region Gizmos
    private PlayerControllerPreset GetControllerPreset()
    {
        if (m_MasterPreset == null)
            return null;

        return m_MasterPreset.ControllerPreset;
    }

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
}

public sealed class PlayerAuthoringBaker : Baker<PlayerAuthoring>
{
    public override void Bake(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return;

        PlayerMasterPreset masterPreset = authoring.MasterPreset;

        if (masterPreset == null)
            return;

        PlayerControllerPreset controllerPreset = masterPreset.ControllerPreset;

        if (controllerPreset == null)
            return;

        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        BlobAssetReference<PlayerControllerConfigBlob> blob = BuildConfigBlob(controllerPreset);

        PlayerControllerConfig config = new PlayerControllerConfig
        {
            Config = blob
        };

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
                InputDeadZone = movementSettings.Values.InputDeadZone
            }
        };

        root.Movement = movementConfig;
    }

    private void FillLookConfig(ref PlayerControllerConfigBlob root, LookSettings lookSettings, ref BlobBuilder builder)
    {
        LookConfig lookConfig = new LookConfig
        {
            DirectionsMode = lookSettings.DirectionsMode,
            DiscreteDirectionCount = lookSettings.DiscreteDirectionCount,
            DirectionOffsetDegrees = lookSettings.DirectionOffsetDegrees,
            LookReference = lookSettings.LookReference,
            RotationMode = lookSettings.RotationMode,
            RotationSpeed = lookSettings.RotationSpeed,
            MultiplierSampling = lookSettings.MultiplierSampling,
            FrontCone = new ConeConfig
            {
                Enabled = lookSettings.FrontConeEnabled,
                AngleDegrees = lookSettings.FrontConeAngle,
                MaxSpeedMultiplier = lookSettings.FrontConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.FrontConeAccelerationMultiplier
            },
            BackCone = new ConeConfig
            {
                Enabled = lookSettings.BackConeEnabled,
                AngleDegrees = lookSettings.BackConeAngle,
                MaxSpeedMultiplier = lookSettings.BackConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.BackConeAccelerationMultiplier
            },
            LeftCone = new ConeConfig
            {
                Enabled = lookSettings.LeftConeEnabled,
                AngleDegrees = lookSettings.LeftConeAngle,
                MaxSpeedMultiplier = lookSettings.LeftConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.LeftConeAccelerationMultiplier
            },
            RightCone = new ConeConfig
            {
                Enabled = lookSettings.RightConeEnabled,
                AngleDegrees = lookSettings.RightConeAngle,
                MaxSpeedMultiplier = lookSettings.RightConeMaxSpeedMultiplier,
                AccelerationMultiplier = lookSettings.RightConeAccelerationMultiplier
            },
            Values = new LookValuesBlob
            {
                RotationDamping = lookSettings.Values.RotationDamping,
                RotationMaxSpeed = lookSettings.Values.RotationMaxSpeed,
                RotationDeadZone = lookSettings.Values.RotationDeadZone
            }
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

        root.Look = lookConfig;
    }

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
}
