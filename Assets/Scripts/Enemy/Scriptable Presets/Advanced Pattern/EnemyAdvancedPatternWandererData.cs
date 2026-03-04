using System;
using UnityEngine;

/// <summary>
/// Contains payload values for Wanderer Basic movement.
/// </summary>
[Serializable]
public sealed class EnemyWandererBasicPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Radius around the enemy used to sample next wander destination candidates.")]
    [SerializeField] private float searchRadius = 9f;

    [Tooltip("Minimum distance from current position required for a valid wander destination.")]
    [SerializeField] private float minimumTravelDistance = 2f;

    [Tooltip("Maximum distance from current position allowed for a valid wander destination.")]
    [SerializeField] private float maximumTravelDistance = 8f;

    [Tooltip("Distance threshold used to consider the destination reached.")]
    [SerializeField] private float arrivalTolerance = 0.35f;

    [Tooltip("Seconds to wait after reaching a destination before selecting the next one.")]
    [SerializeField] private float waitCooldownSeconds = 0.7f;

    [Tooltip("Number of candidate positions sampled for each destination pick.")]
    [SerializeField] private int candidateSampleCount = 9;

    [Tooltip("When enabled, the picker scans the full 360° arc using Infinite Direction Step Degrees instead of Candidate Sample Count.")]
    [SerializeField] private bool useInfiniteDirectionSampling = true;

    [Tooltip("Angular resolution in degrees used by infinite direction sampling. Lower values are more exhaustive but more expensive.")]
    [SerializeField] private float infiniteDirectionStepDegrees = 8f;

    [Tooltip("Bias factor for preferring directions not recently explored. Applied only after free-trajectory checks and scoring.")]
    [SerializeField] private float unexploredDirectionPreference = 0.65f;

    [Tooltip("Bias factor for preferring candidates closer to the player. Applied only after free-trajectory checks and scoring.")]
    [SerializeField] private float towardPlayerPreference = 0.35f;

    [Tooltip("Minimum extra distance in meters kept from wall colliders during path validation to reduce corner-stuck cases.")]
    [SerializeField] private float minimumWallDistance = 0.25f;

    [Tooltip("Minimum extra clearance in meters kept from neighboring enemies when selecting a destination or trajectory.")]
    [SerializeField] private float minimumEnemyClearance = 0.2f;

    [Tooltip("Prediction time in seconds used to avoid trajectories likely to intersect moving neighbors.")]
    [SerializeField] private float trajectoryPredictionTime = 0.35f;

    [Tooltip("Weight assigned to free-space and collision-safe trajectories. Higher values prioritize safer paths over biases.")]
    [SerializeField] private float freeTrajectoryPreference = 4f;

    [Tooltip("Cooldown in seconds before retrying destination selection after blocked candidates.")]
    [SerializeField] private float blockedPathRetryDelay = 0.25f;
    #endregion

    #endregion

    #region Properties
    public float SearchRadius
    {
        get
        {
            return searchRadius;
        }
    }

    public float MinimumTravelDistance
    {
        get
        {
            return minimumTravelDistance;
        }
    }

    public float MaximumTravelDistance
    {
        get
        {
            return maximumTravelDistance;
        }
    }

    public float ArrivalTolerance
    {
        get
        {
            return arrivalTolerance;
        }
    }

    public float WaitCooldownSeconds
    {
        get
        {
            return waitCooldownSeconds;
        }
    }

    public int CandidateSampleCount
    {
        get
        {
            return candidateSampleCount;
        }
    }

    public bool UseInfiniteDirectionSampling
    {
        get
        {
            return useInfiniteDirectionSampling;
        }
    }

    public float InfiniteDirectionStepDegrees
    {
        get
        {
            return infiniteDirectionStepDegrees;
        }
    }

    public float UnexploredDirectionPreference
    {
        get
        {
            return unexploredDirectionPreference;
        }
    }

    public float TowardPlayerPreference
    {
        get
        {
            return towardPlayerPreference;
        }
    }

    public float MinimumWallDistance
    {
        get
        {
            return minimumWallDistance;
        }
    }

    public float MinimumEnemyClearance
    {
        get
        {
            return minimumEnemyClearance;
        }
    }

    public float TrajectoryPredictionTime
    {
        get
        {
            return trajectoryPredictionTime;
        }
    }

    public float FreeTrajectoryPreference
    {
        get
        {
            return freeTrajectoryPreference;
        }
    }

    public float BlockedPathRetryDelay
    {
        get
        {
            return blockedPathRetryDelay;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Normalizes Basic wanderer payload values.
    /// </summary>
    public void Validate()
    {
        if (searchRadius < 0.5f)
            searchRadius = 0.5f;

        if (minimumTravelDistance < 0f)
            minimumTravelDistance = 0f;

        if (maximumTravelDistance < 0.1f)
            maximumTravelDistance = 0.1f;

        if (minimumTravelDistance > maximumTravelDistance)
            minimumTravelDistance = maximumTravelDistance;

        if (arrivalTolerance < 0.05f)
            arrivalTolerance = 0.05f;

        if (waitCooldownSeconds < 0f)
            waitCooldownSeconds = 0f;

        if (candidateSampleCount < 1)
            candidateSampleCount = 1;

        if (candidateSampleCount > 32)
            candidateSampleCount = 32;

        if (infiniteDirectionStepDegrees < 0.5f)
            infiniteDirectionStepDegrees = 0.5f;

        if (infiniteDirectionStepDegrees > 90f)
            infiniteDirectionStepDegrees = 90f;

        if (unexploredDirectionPreference < 0f)
            unexploredDirectionPreference = 0f;

        if (towardPlayerPreference < 0f)
            towardPlayerPreference = 0f;

        if (minimumWallDistance < 0f)
            minimumWallDistance = 0f;

        if (minimumEnemyClearance < 0f)
            minimumEnemyClearance = 0f;

        if (trajectoryPredictionTime < 0f)
            trajectoryPredictionTime = 0f;

        if (freeTrajectoryPreference < 0f)
            freeTrajectoryPreference = 0f;

        if (blockedPathRetryDelay < 0f)
            blockedPathRetryDelay = 0f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Contains payload values for Wanderer DVD movement.
/// </summary>
[Serializable]
public sealed class EnemyWandererDvdPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Speed multiplier applied over Brain movement speed when DVD movement is active.")]
    [SerializeField] private float speedMultiplier = 1.05f;

    [Tooltip("Bounce damping applied after wall reflection. 1 keeps full speed, 0 fully stops.")]
    [SerializeField] private float bounceDamping = 1f;

    [Tooltip("When enabled, DVD movement starts with a pseudo-random diagonal direction per spawn.")]
    [SerializeField] private bool randomizeInitialDirection = true;

    [Tooltip("Fallback fixed initial direction in degrees used when randomization is disabled.")]
    [SerializeField] private float fixedInitialDirectionDegrees = 45f;

    [Tooltip("Small displacement in meters used to resolve corner-stuck situations.")]
    [SerializeField] private float cornerNudgeDistance = 0.08f;
    #endregion

    #endregion

    #region Properties
    public float SpeedMultiplier
    {
        get
        {
            return speedMultiplier;
        }
    }

    public float BounceDamping
    {
        get
        {
            return bounceDamping;
        }
    }

    public bool RandomizeInitialDirection
    {
        get
        {
            return randomizeInitialDirection;
        }
    }

    public float FixedInitialDirectionDegrees
    {
        get
        {
            return fixedInitialDirectionDegrees;
        }
    }

    public float CornerNudgeDistance
    {
        get
        {
            return cornerNudgeDistance;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Normalizes DVD wanderer payload values.
    /// </summary>
    public void Validate()
    {
        if (speedMultiplier < 0f)
            speedMultiplier = 0f;

        if (bounceDamping < 0f)
            bounceDamping = 0f;

        if (bounceDamping > 1f)
            bounceDamping = 1f;

        if (cornerNudgeDistance < 0f)
            cornerNudgeDistance = 0f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Groups Wanderer mode and mode-specific payloads.
/// </summary>
[Serializable]
public sealed class EnemyWandererModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Wanderer movement mode used at runtime.")]
    [SerializeField] private EnemyWandererMode mode = EnemyWandererMode.Basic;

    [Tooltip("Payload used when Wanderer mode is Basic.")]
    [SerializeField] private EnemyWandererBasicPayload basic = new EnemyWandererBasicPayload();

    [Tooltip("Payload used when Wanderer mode is DVD.")]
    [SerializeField] private EnemyWandererDvdPayload dvd = new EnemyWandererDvdPayload();
    #endregion

    #endregion

    #region Properties
    public EnemyWandererMode Mode
    {
        get
        {
            return mode;
        }
    }

    public EnemyWandererBasicPayload Basic
    {
        get
        {
            return basic;
        }
    }

    public EnemyWandererDvdPayload Dvd
    {
        get
        {
            return dvd;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Normalizes Wanderer module data and nested payloads.
    /// </summary>
    public void Validate()
    {
        switch (mode)
        {
            case EnemyWandererMode.Basic:
            case EnemyWandererMode.Dvd:
                break;

            default:
                mode = EnemyWandererMode.Basic;
                break;
        }

        if (basic == null)
            basic = new EnemyWandererBasicPayload();

        if (dvd == null)
            dvd = new EnemyWandererDvdPayload();

        basic.Validate();
        dvd.Validate();
    }
    #endregion

    #endregion
}
