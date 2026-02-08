using System;
using UnityEngine.Serialization;
using UnityEngine;

/// <summary>
/// Represents configuration settings for character movement, including direction modes, discrete direction count,
/// angular offset, movement reference frame, and movement tuning values.
/// </summary>
[Serializable]
public sealed class MovementSettings
{
    #region Serialized Fields
    [Header("Movement Directions")]
    [Tooltip("Defines how many movement directions are allowed.")]
    [FormerlySerializedAs("m_DirectionsMode")]
    [SerializeField] private MovementDirectionsMode directionsMode = MovementDirectionsMode.AllDirections;

    [Tooltip("Number of discrete movement directions distributed over 360 degrees.")]
    [FormerlySerializedAs("m_DiscreteDirectionCount")]
    [SerializeField] private int discreteDirectionCount = 8;

    [Tooltip("Angular offset applied to the discrete direction grid (degrees).")]
    [FormerlySerializedAs("m_DirectionOffsetDegrees")]
    [SerializeField] private float directionOffsetDegrees = 0f;

    [Tooltip("Reference frame used to orient movement directions.")]
    [FormerlySerializedAs("m_MovementReference")]
    [SerializeField] private ReferenceFrame movementReference = ReferenceFrame.PlayerForward;

    [Header("Movement Values")]
    [Tooltip("Numeric movement tuning values.")]
    [FormerlySerializedAs("m_Values")]
    [SerializeField] private MovementValues values = new MovementValues();
    #endregion

    #region Properties
    public MovementDirectionsMode DirectionsMode
    {
        get
        {
            return directionsMode;
        }
    }

    public int DiscreteDirectionCount
    {
        get
        {
            return discreteDirectionCount;
        }
    }

    public float DirectionOffsetDegrees
    {
        get
        {
            return directionOffsetDegrees;
        }
    }

    public ReferenceFrame MovementReference
    {
        get
        {
            return movementReference;
        }
    }

    public MovementValues Values
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
        if (discreteDirectionCount < 1)
            discreteDirectionCount = 1;

        if (directionsMode == MovementDirectionsMode.DiscreteCount)
            directionOffsetDegrees = SnapOffsetToStep(directionOffsetDegrees, discreteDirectionCount);

        if (values == null)
            values = new MovementValues();

        values.Validate();
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

/// <summary>
/// Represents configurable movement parameters such as speed, 
/// acceleration, deceleration, input dead zone, and release
/// grace time.
/// </summary>
[Serializable]
public sealed class MovementValues
{
    #region Serialized Fields
    [Tooltip("Base movement speed before modifiers.")]
    [FormerlySerializedAs("m_BaseSpeed")]
    [SerializeField] private float baseSpeed = 4f;

    [Tooltip("Maximum allowed movement speed.")]
    [FormerlySerializedAs("m_MaxSpeed")]
    [SerializeField] private float maxSpeed = 6f;

    [Tooltip("Acceleration applied while input is active. Negative values snap to max speed instantly.")]
    [FormerlySerializedAs("m_Acceleration")]
    [SerializeField] private float acceleration = 12f;

    [Tooltip("Deceleration applied when input is released. Negative values stop instantly.")]
    [FormerlySerializedAs("m_Deceleration")]
    [SerializeField] private float deceleration = 14f;

    [Tooltip("Input dead zone applied to movement vectors.")]
    [FormerlySerializedAs("m_InputDeadZone")]
    [SerializeField] private float inputDeadZone = 0.1f;

    [Tooltip("Grace time (seconds) to keep the last diagonal movement direction when releasing keys.")]
    [FormerlySerializedAs("m_DigitalReleaseGraceSeconds")]
    [SerializeField] private float digitalReleaseGraceSeconds = 0.08f;
    #endregion

    #region Properties
    public float BaseSpeed
    {
        get
        {
            return baseSpeed;
        }
    }

    public float MaxSpeed
    {
        get
        {
            return maxSpeed;
        }
    }

    public float Acceleration
    {
        get
        {
            return acceleration;
        }
    }

    public float Deceleration
    {
        get
        {
            return deceleration;
        }
    }

    public float InputDeadZone
    {
        get
        {
            return inputDeadZone;
        }
    }

    public float DigitalReleaseGraceSeconds
    {
        get
        {
            return digitalReleaseGraceSeconds;
        }
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (baseSpeed < 0f)
            baseSpeed = 0f;

        if (maxSpeed < 0f)
            maxSpeed = 0f;

        if (inputDeadZone < 0f)
            inputDeadZone = 0f;

        if (digitalReleaseGraceSeconds < 0f)
            digitalReleaseGraceSeconds = 0f;
    }
    #endregion
}
