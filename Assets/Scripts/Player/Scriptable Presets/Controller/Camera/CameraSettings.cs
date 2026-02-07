using System;
using UnityEngine.Serialization;
using UnityEngine;

[Serializable]
public sealed class CameraSettings
{
    #region Serialized Fields
    [Header("Camera Behavior")]
    [Tooltip("Defines the overall camera behavior for the player.")]
    [FormerlySerializedAs("m_Behavior")]
    [SerializeField] private CameraBehavior behavior = CameraBehavior.FollowWithAutoOffset;

    [Tooltip("Fixed follow offset when using FollowWithOffset behavior.")]
    [FormerlySerializedAs("m_FollowOffset")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 10f, -8f);

    [Tooltip("Anchor used when RoomFixed behavior is selected.")]
    [FormerlySerializedAs("m_RoomAnchor")]
    [SerializeField] private Transform roomAnchor;

    [Header("Camera Values")]
    [Tooltip("Numeric camera tuning values.")]
    [FormerlySerializedAs("m_Values")]
    [SerializeField] private CameraValues values = new CameraValues();
    #endregion

    #region Properties
    public CameraBehavior Behavior
    {
        get
        {
            return behavior;
        }
    }

    public Vector3 FollowOffset
    {
        get
        {
            return followOffset;
        }
    }

    public Transform RoomAnchor
    {
        get
        {
            return roomAnchor;
        }
    }

    public CameraValues Values
    {
        get
        {
            return values;
        }
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (values == null)
            values = new CameraValues();

        values.Validate();
    }
    #endregion
}

[Serializable]
public sealed class CameraValues
{
    #region Serialized Fields
    [Tooltip("Follow speed for camera target tracking.")]
    [FormerlySerializedAs("m_FollowSpeed")]
    [SerializeField] private float followSpeed = 8f;

    [Tooltip("Lag applied to the camera when following the target.")]
    [FormerlySerializedAs("m_CameraLag")]
    [SerializeField] private float cameraLag = 0.1f;

    [Tooltip("Damping applied to camera movement smoothing.")]
    [FormerlySerializedAs("m_Damping")]
    [SerializeField] private float damping = 0.15f;

    [Tooltip("Maximum distance the camera can lag behind.")]
    [FormerlySerializedAs("m_MaxFollowDistance")]
    [SerializeField] private float maxFollowDistance = 6f;

    [Tooltip("Radius around the target where the camera stays still.")]
    [FormerlySerializedAs("m_DeadZoneRadius")]
    [SerializeField] private float deadZoneRadius = 0.2f;
    #endregion

    #region Properties
    public float FollowSpeed
    {
        get
        {
            return followSpeed;
        }
    }

    public float CameraLag
    {
        get
        {
            return cameraLag;
        }
    }

    public float Damping
    {
        get
        {
            return damping;
        }
    }

    public float MaxFollowDistance
    {
        get
        {
            return maxFollowDistance;
        }
    }

    public float DeadZoneRadius
    {
        get
        {
            return deadZoneRadius;
        }
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (followSpeed < 0f)
            followSpeed = 0f;

        if (cameraLag < 0f)
            cameraLag = 0f;

        if (damping < 0f)
            damping = 0f;

        if (maxFollowDistance < 0f)
            maxFollowDistance = 0f;

        if (deadZoneRadius < 0f)
            deadZoneRadius = 0f;
    }
    #endregion
}
