using System;
using UnityEngine;

[Serializable]
public sealed class CameraSettings
{
    #region Serialized Fields
    [Tooltip("Defines the overall camera behavior for the player.")]
    [Header("Camera Behavior")]
    [SerializeField] private CameraBehavior m_Behavior = CameraBehavior.FollowWithAutoOffset;

    [Tooltip("Fixed follow offset when using FollowWithOffset behavior.")]
    [SerializeField] private Vector3 m_FollowOffset = new Vector3(0f, 10f, -8f);

    [Tooltip("Anchor used when RoomFixed behavior is selected.")]
    [SerializeField] private Transform m_RoomAnchor;

    [Tooltip("Numeric camera tuning values.")]
    [Header("Camera Values")]
    [SerializeField] private CameraValues m_Values = new CameraValues();
    #endregion

    #region Properties
    public CameraBehavior Behavior
    {
        get
        {
            return m_Behavior;
        }
    }

    public Vector3 FollowOffset
    {
        get
        {
            return m_FollowOffset;
        }
    }

    public Transform RoomAnchor
    {
        get
        {
            return m_RoomAnchor;
        }
    }

    public CameraValues Values
    {
        get
        {
            return m_Values;
        }
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (m_Values == null)
            m_Values = new CameraValues();

        m_Values.Validate();
    }
    #endregion
}

[Serializable]
public sealed class CameraValues
{
    #region Serialized Fields
    [Tooltip("Follow speed for camera target tracking.")]
    [SerializeField] private float m_FollowSpeed = 8f;

    [Tooltip("Lag applied to the camera when following the target.")]
    [SerializeField] private float m_CameraLag = 0.1f;

    [Tooltip("Damping applied to camera movement smoothing.")]
    [SerializeField] private float m_Damping = 0.15f;

    [Tooltip("Maximum distance the camera can lag behind.")]
    [SerializeField] private float m_MaxFollowDistance = 6f;

    [Tooltip("Radius around the target where the camera stays still.")]
    [SerializeField] private float m_DeadZoneRadius = 0.2f;
    #endregion

    #region Properties
    public float FollowSpeed
    {
        get
        {
            return m_FollowSpeed;
        }
    }

    public float CameraLag
    {
        get
        {
            return m_CameraLag;
        }
    }

    public float Damping
    {
        get
        {
            return m_Damping;
        }
    }

    public float MaxFollowDistance
    {
        get
        {
            return m_MaxFollowDistance;
        }
    }

    public float DeadZoneRadius
    {
        get
        {
            return m_DeadZoneRadius;
        }
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (m_FollowSpeed < 0f)
            m_FollowSpeed = 0f;

        if (m_CameraLag < 0f)
            m_CameraLag = 0f;

        if (m_Damping < 0f)
            m_Damping = 0f;

        if (m_MaxFollowDistance < 0f)
            m_MaxFollowDistance = 0f;

        if (m_DeadZoneRadius < 0f)
            m_DeadZoneRadius = 0f;
    }
    #endregion
}
