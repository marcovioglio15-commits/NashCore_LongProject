using System;
using UnityEngine;

[Serializable]
public sealed class MovementSettings
{
    #region Serialized Fields
    [Tooltip("Defines how many movement directions are allowed.")]
    [Header("Movement Directions")]
    [SerializeField] private MovementDirectionsMode m_DirectionsMode = MovementDirectionsMode.AllDirections;

    [Tooltip("Number of discrete movement directions distributed over 360 degrees.")]
    [SerializeField] private int m_DiscreteDirectionCount = 8;

    [Tooltip("Angular offset applied to the discrete direction grid (degrees).")]
    [SerializeField] private float m_DirectionOffsetDegrees = 0f;

    [Tooltip("Reference frame used to orient movement directions.")]
    [SerializeField] private ReferenceFrame m_MovementReference = ReferenceFrame.PlayerForward;

    [Tooltip("Numeric movement tuning values.")]
    [Header("Movement Values")]
    [SerializeField] private MovementValues m_Values = new MovementValues();
    #endregion

    #region Properties
    public MovementDirectionsMode DirectionsMode
    {
        get
        {
            return m_DirectionsMode;
        }
    }

    public int DiscreteDirectionCount
    {
        get
        {
            return m_DiscreteDirectionCount;
        }
    }

    public float DirectionOffsetDegrees
    {
        get
        {
            return m_DirectionOffsetDegrees;
        }
    }

    public ReferenceFrame MovementReference
    {
        get
        {
            return m_MovementReference;
        }
    }

    public MovementValues Values
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
        if (m_DiscreteDirectionCount < 1)
            m_DiscreteDirectionCount = 1;

        if (m_DirectionsMode == MovementDirectionsMode.DiscreteCount)
            m_DirectionOffsetDegrees = SnapOffsetToStep(m_DirectionOffsetDegrees, m_DiscreteDirectionCount);

        if (m_Values == null)
            m_Values = new MovementValues();

        m_Values.Validate();
    }

    private float SnapOffsetToStep(float offset, int count)
    {
        int clampedCount = Mathf.Max(1, count);
        float step = 360f / clampedCount;
        float normalized = Mathf.Repeat(offset, 360f);
        float snapped = Mathf.Round(normalized / step) * step;
        return Mathf.Repeat(snapped, 360f);
    }

    #endregion
}

[Serializable]
public sealed class MovementValues
{
    #region Serialized Fields
    [Tooltip("Base movement speed before modifiers.")]
    [SerializeField] private float m_BaseSpeed = 4f;

    [Tooltip("Maximum allowed movement speed.")]
    [SerializeField] private float m_MaxSpeed = 6f;

    [Tooltip("Acceleration applied while input is active. Negative values snap to max speed instantly.")]
    [SerializeField] private float m_Acceleration = 12f;

    [Tooltip("Deceleration applied when input is released. Negative values stop instantly.")]
    [SerializeField] private float m_Deceleration = 14f;

    [Tooltip("Input dead zone applied to movement vectors.")]
    [SerializeField] private float m_InputDeadZone = 0.1f;

    [Tooltip("Grace time (seconds) to keep the last diagonal movement direction when releasing keys.")]
    [SerializeField] private float m_DigitalReleaseGraceSeconds = 0.08f;
    #endregion

    #region Properties
    public float BaseSpeed
    {
        get
        {
            return m_BaseSpeed;
        }
    }

    public float MaxSpeed
    {
        get
        {
            return m_MaxSpeed;
        }
    }

    public float Acceleration
    {
        get
        {
            return m_Acceleration;
        }
    }

    public float Deceleration
    {
        get
        {
            return m_Deceleration;
        }
    }

    public float InputDeadZone
    {
        get
        {
            return m_InputDeadZone;
        }
    }

    public float DigitalReleaseGraceSeconds
    {
        get
        {
            return m_DigitalReleaseGraceSeconds;
        }
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (m_BaseSpeed < 0f)
            m_BaseSpeed = 0f;

        if (m_MaxSpeed < 0f)
            m_MaxSpeed = 0f;

        if (m_InputDeadZone < 0f)
            m_InputDeadZone = 0f;

        if (m_DigitalReleaseGraceSeconds < 0f)
            m_DigitalReleaseGraceSeconds = 0f;
    }
    #endregion
}
