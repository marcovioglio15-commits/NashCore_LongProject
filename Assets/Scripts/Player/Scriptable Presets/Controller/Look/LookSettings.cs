using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class LookSettings
{
    #region Serialized Fields
    [Tooltip("Defines which look directions are allowed.")]
    [Header("Look Directions")]
    [SerializeField] private LookDirectionsMode m_DirectionsMode = LookDirectionsMode.AllDirections;

    [Tooltip("Number of discrete look directions distributed over 360 degrees.")]
    [SerializeField] private int m_DiscreteDirectionCount = 8;

    [Tooltip("Angular offset applied to the discrete direction grid (degrees).")]
    [SerializeField] private float m_DirectionOffsetDegrees = 0f;

    [Tooltip("Enable the front cone as an allowed look sector.")]
    [Header("Cones")]
    [SerializeField] private bool m_FrontConeEnabled = true;

    [Tooltip("Front cone angle in degrees.")]
    [SerializeField] private float m_FrontConeAngle = 90f;

    [Tooltip("Max speed multiplier while the look direction is inside the front cone.")]
    [FormerlySerializedAs("m_FrontConeSpeedMultiplier")]
    [SerializeField] private float m_FrontConeMaxSpeedMultiplier = 1f;

    [Tooltip("Acceleration multiplier while the look direction is inside the front cone.")]
    [SerializeField] private float m_FrontConeAccelerationMultiplier = 1f;

    [Tooltip("Enable the back cone as an allowed look sector.")]
    [SerializeField] private bool m_BackConeEnabled = false;

    [Tooltip("Back cone angle in degrees.")]
    [SerializeField] private float m_BackConeAngle = 90f;

    [Tooltip("Max speed multiplier while the look direction is inside the back cone.")]
    [FormerlySerializedAs("m_BackConeSpeedMultiplier")]
    [SerializeField] private float m_BackConeMaxSpeedMultiplier = 1f;

    [Tooltip("Acceleration multiplier while the look direction is inside the back cone.")]
    [SerializeField] private float m_BackConeAccelerationMultiplier = 1f;

    [Tooltip("Enable the left cone as an allowed look sector.")]
    [SerializeField] private bool m_LeftConeEnabled = false;

    [Tooltip("Left cone angle in degrees.")]
    [SerializeField] private float m_LeftConeAngle = 90f;

    [Tooltip("Max speed multiplier while the look direction is inside the left cone.")]
    [FormerlySerializedAs("m_LeftConeSpeedMultiplier")]
    [SerializeField] private float m_LeftConeMaxSpeedMultiplier = 1f;

    [Tooltip("Acceleration multiplier while the look direction is inside the left cone.")]
    [SerializeField] private float m_LeftConeAccelerationMultiplier = 1f;

    [Tooltip("Enable the right cone as an allowed look sector.")]
    [SerializeField] private bool m_RightConeEnabled = false;

    [Tooltip("Right cone angle in degrees.")]
    [SerializeField] private float m_RightConeAngle = 90f;

    [Tooltip("Max speed multiplier while the look direction is inside the right cone.")]
    [FormerlySerializedAs("m_RightConeSpeedMultiplier")]
    [SerializeField] private float m_RightConeMaxSpeedMultiplier = 1f;

    [Tooltip("Acceleration multiplier while the look direction is inside the right cone.")]
    [SerializeField] private float m_RightConeAccelerationMultiplier = 1f;

    [Tooltip("Defines how rotation should transition between allowed directions.")]
    [Header("Rotation")]
    [SerializeField] private RotationMode m_RotationMode = RotationMode.SnapToAllowedDirections;

    [Tooltip("Rotation speed when continuous rotation is enabled (degrees per second).")]
    [SerializeField] private float m_RotationSpeed = 540f;

    [Tooltip("Defines how max speed and acceleration multipliers are sampled for discrete look arcs.")]
    [Header("Movement Speed Multipliers")]
    [SerializeField] private LookMultiplierSampling m_MultiplierSampling = LookMultiplierSampling.DirectionalBlend;

    [Tooltip("Max speed multipliers for discrete look arcs.")]
    [FormerlySerializedAs("m_DiscreteDirectionSpeedMultipliers")]
    [SerializeField] private List<float> m_DiscreteDirectionMaxSpeedMultipliers = new List<float>();

    [Tooltip("Acceleration multipliers for discrete look arcs.")]
    [SerializeField] private List<float> m_DiscreteDirectionAccelerationMultipliers = new List<float>();

    [Tooltip("Numeric look tuning values.")]
    [Header("Look Values")]
    [SerializeField] private LookValues m_Values = new LookValues();
    #endregion

    #region Properties
    public LookDirectionsMode DirectionsMode
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

    public bool FrontConeEnabled
    {
        get
        {
            return m_FrontConeEnabled;
        }
    }

    public float FrontConeAngle
    {
        get
        {
            return m_FrontConeAngle;
        }
    }

    public float FrontConeMaxSpeedMultiplier
    {
        get
        {
            return m_FrontConeMaxSpeedMultiplier;
        }
    }

    public float FrontConeAccelerationMultiplier
    {
        get
        {
            return m_FrontConeAccelerationMultiplier;
        }
    }

    public bool BackConeEnabled
    {
        get
        {
            return m_BackConeEnabled;
        }
    }

    public float BackConeAngle
    {
        get
        {
            return m_BackConeAngle;
        }
    }

    public float BackConeMaxSpeedMultiplier
    {
        get
        {
            return m_BackConeMaxSpeedMultiplier;
        }
    }

    public float BackConeAccelerationMultiplier
    {
        get
        {
            return m_BackConeAccelerationMultiplier;
        }
    }

    public bool LeftConeEnabled
    {
        get
        {
            return m_LeftConeEnabled;
        }
    }

    public float LeftConeAngle
    {
        get
        {
            return m_LeftConeAngle;
        }
    }

    public float LeftConeMaxSpeedMultiplier
    {
        get
        {
            return m_LeftConeMaxSpeedMultiplier;
        }
    }

    public float LeftConeAccelerationMultiplier
    {
        get
        {
            return m_LeftConeAccelerationMultiplier;
        }
    }

    public bool RightConeEnabled
    {
        get
        {
            return m_RightConeEnabled;
        }
    }

    public float RightConeAngle
    {
        get
        {
            return m_RightConeAngle;
        }
    }

    public float RightConeMaxSpeedMultiplier
    {
        get
        {
            return m_RightConeMaxSpeedMultiplier;
        }
    }

    public float RightConeAccelerationMultiplier
    {
        get
        {
            return m_RightConeAccelerationMultiplier;
        }
    }

    public RotationMode RotationMode
    {
        get
        {
            return m_RotationMode;
        }
    }

    public float RotationSpeed
    {
        get
        {
            return m_RotationSpeed;
        }
    }

    public LookMultiplierSampling MultiplierSampling
    {
        get
        {
            return m_MultiplierSampling;
        }
    }

    public IReadOnlyList<float> DiscreteDirectionMaxSpeedMultipliers
    {
        get
        {
            return m_DiscreteDirectionMaxSpeedMultipliers;
        }
    }

    public IReadOnlyList<float> DiscreteDirectionAccelerationMultipliers
    {
        get
        {
            return m_DiscreteDirectionAccelerationMultipliers;
        }
    }

    public LookValues Values
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

        if (m_DirectionsMode == LookDirectionsMode.DiscreteCount)
            m_DirectionOffsetDegrees = SnapOffsetToStep(m_DirectionOffsetDegrees, m_DiscreteDirectionCount);

        if (m_DiscreteDirectionMaxSpeedMultipliers == null)
            m_DiscreteDirectionMaxSpeedMultipliers = new List<float>();

        if (m_DiscreteDirectionAccelerationMultipliers == null)
            m_DiscreteDirectionAccelerationMultipliers = new List<float>();

        EnsureDiscreteMultipliers(m_DiscreteDirectionMaxSpeedMultipliers, m_DiscreteDirectionCount);
        EnsureDiscreteMultipliers(m_DiscreteDirectionAccelerationMultipliers, m_DiscreteDirectionCount);

        ClampDiscreteMultipliers(m_DiscreteDirectionMaxSpeedMultipliers);
        ClampDiscreteMultipliers(m_DiscreteDirectionAccelerationMultipliers);

        m_FrontConeAngle = ClampConeAngle(m_FrontConeAngle);
        m_BackConeAngle = ClampConeAngle(m_BackConeAngle);
        m_LeftConeAngle = ClampConeAngle(m_LeftConeAngle);
        m_RightConeAngle = ClampConeAngle(m_RightConeAngle);

        m_FrontConeMaxSpeedMultiplier = ClampMultiplier(m_FrontConeMaxSpeedMultiplier);
        m_FrontConeAccelerationMultiplier = ClampMultiplier(m_FrontConeAccelerationMultiplier);
        m_BackConeMaxSpeedMultiplier = ClampMultiplier(m_BackConeMaxSpeedMultiplier);
        m_BackConeAccelerationMultiplier = ClampMultiplier(m_BackConeAccelerationMultiplier);
        m_LeftConeMaxSpeedMultiplier = ClampMultiplier(m_LeftConeMaxSpeedMultiplier);
        m_LeftConeAccelerationMultiplier = ClampMultiplier(m_LeftConeAccelerationMultiplier);
        m_RightConeMaxSpeedMultiplier = ClampMultiplier(m_RightConeMaxSpeedMultiplier);
        m_RightConeAccelerationMultiplier = ClampMultiplier(m_RightConeAccelerationMultiplier);

        if (m_RotationSpeed < 0f)
            m_RotationSpeed = 0f;

        if (m_Values == null)
            m_Values = new LookValues();

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

    private void EnsureDiscreteMultipliers(List<float> multipliers, int targetCount)
    {
        if (multipliers == null)
            return;

        if (multipliers.Count > targetCount)
            multipliers.RemoveRange(targetCount, multipliers.Count - targetCount);

        while (multipliers.Count < targetCount)
            multipliers.Add(1f);
    }

    private void ClampDiscreteMultipliers(List<float> multipliers)
    {
        if (multipliers == null)
            return;

        for (int i = 0; i < multipliers.Count; i++)
            multipliers[i] = ClampMultiplier(multipliers[i]);
    }

    private float ClampMultiplier(float value)
    {
        return Mathf.Clamp01(value);
    }

    private float ClampConeAngle(float angle)
    {
        if (angle < 1f)
            return 1f;

        if (angle > 360f)
            return 360f;

        return angle;
    }

    #endregion
}

[Serializable]
public sealed class LookValues
{
    #region Serialized Fields
    [Tooltip("Damping time (seconds) used to smooth rotation speed changes.")]
    [SerializeField] private float m_RotationDamping = 0.1f;

    [Tooltip("Maximum allowed rotation speed.")]
    [SerializeField] private float m_RotationMaxSpeed = 720f;

    [Tooltip("Input dead zone applied to look vectors.")]
    [SerializeField] private float m_RotationDeadZone = 0.1f;

    [Tooltip("Grace time (seconds) to keep the last diagonal look direction when releasing keys.")]
    [SerializeField] private float m_DigitalReleaseGraceSeconds = 0.08f;

    #endregion

    #region Properties
    public float RotationDamping
    {
        get
        {
            return m_RotationDamping;
        }
    }

    public float RotationMaxSpeed
    {
        get
        {
            return m_RotationMaxSpeed;
        }
    }

    public float RotationDeadZone
    {
        get
        {
            return m_RotationDeadZone;
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
        if (m_RotationDamping < 0f)
            m_RotationDamping = 0f;

        if (m_RotationMaxSpeed < 0f)
            m_RotationMaxSpeed = 0f;

        if (m_RotationDeadZone < 0f)
            m_RotationDeadZone = 0f;

        if (m_DigitalReleaseGraceSeconds < 0f)
            m_DigitalReleaseGraceSeconds = 0f;

    }
    #endregion
}
