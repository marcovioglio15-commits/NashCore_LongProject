using System;
using UnityEngine;

[Serializable]
public sealed class EnemyBrainMovementSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Meters per second used as baseline enemy movement speed toward the player.")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Hard cap applied to the enemy velocity magnitude.")]
    [SerializeField] private float maxSpeed = 4f;

    [Tooltip("Meters per second squared used to accelerate toward desired velocity.")]
    [SerializeField] private float acceleration = 8f;

    [Tooltip("Reserved deceleration value for future braking behaviors. Currently unused at runtime.")]
    [SerializeField] private float deceleration = 8f;
    #endregion

    #endregion

    #region Properties
    public float MoveSpeed
    {
        get
        {
            return moveSpeed;
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
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (moveSpeed < 0f)
            moveSpeed = 0f;

        if (maxSpeed < 0f)
            maxSpeed = 0f;

        if (acceleration < 0f)
            acceleration = 0f;

        if (deceleration < 0f)
            deceleration = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class EnemyBrainSteeringSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Radius used to search neighboring enemies for separation steering.")]
    [SerializeField] private float separationRadius = 1.1f;

    [Tooltip("Weight applied to the separation vector before velocity clamping.")]
    [SerializeField] private float separationWeight = 2f;

    [Tooltip("Physical body radius used for projectile hit checks and overlap handling.")]
    [SerializeField] private float bodyRadius = 0.55f;
    #endregion

    #endregion

    #region Properties
    public float SeparationRadius
    {
        get
        {
            return separationRadius;
        }
    }

    public float SeparationWeight
    {
        get
        {
            return separationWeight;
        }
    }

    public float BodyRadius
    {
        get
        {
            return bodyRadius;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (separationRadius < 0.1f)
            separationRadius = 0.1f;

        if (separationWeight < 0f)
            separationWeight = 0f;

        if (bodyRadius < 0.05f)
            bodyRadius = 0.05f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class EnemyBrainPlayerContactSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Distance from enemy center to apply contact damage to the player.")]
    [SerializeField] private float contactRadius = 1.2f;

    [Tooltip("Damage applied to the player each time contact cooldown expires in range.")]
    [SerializeField] private float contactDamage = 5f;

    [Tooltip("Cooldown in seconds between two consecutive contact damage applications.")]
    [SerializeField] private float contactInterval = 0.75f;
    #endregion

    #endregion

    #region Properties
    public float ContactRadius
    {
        get
        {
            return contactRadius;
        }
    }

    public float ContactDamage
    {
        get
        {
            return contactDamage;
        }
    }

    public float ContactInterval
    {
        get
        {
            return contactInterval;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (contactRadius < 0f)
            contactRadius = 0f;

        if (contactDamage < 0f)
            contactDamage = 0f;

        if (contactInterval < 0.01f)
            contactInterval = 0.01f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class EnemyBrainHealthStatisticsSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Maximum and initial health assigned to this enemy when spawned from pool.")]
    [SerializeField] private float maxHealth = 30f;

    [Tooltip("Maximum shield reserve assigned to this enemy at spawn. Currently unused at runtime.")]
    [SerializeField] private float maxShield;
    #endregion

    #endregion

    #region Properties
    public float MaxHealth
    {
        get
        {
            return maxHealth;
        }
    }

    public float MaxShield
    {
        get
        {
            return maxShield;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (maxHealth < 1f)
            maxHealth = 1f;

        if (maxShield < 0f)
            maxShield = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class EnemyBrainVisualSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Visual runtime path: managed companion Animator (few actors) or GPU-baked playback (crowd scale).")]
    [SerializeField] private EnemyVisualMode visualMode = EnemyVisualMode.GpuBaked;

    [Tooltip("Playback speed multiplier used by both companion and GPU-baked visual paths.")]
    [SerializeField] private float visualAnimationSpeed = 1f;

    [Tooltip("Loop duration in seconds used by GPU-baked playback time wrapping.")]
    [SerializeField] private float gpuAnimationLoopDuration = 1f;

    [Tooltip("Enable distance-based visual culling while gameplay simulation remains fully active.")]
    [SerializeField] private bool enableDistanceCulling = true;

    [Tooltip("Maximum planar distance from player where visuals stay visible. Set to 0 to keep always visible.")]
    [SerializeField] private float maxVisibleDistance = 55f;

    [Tooltip("Additional distance band used to avoid visual popping when crossing the culling boundary.")]
    [SerializeField] private float visibleDistanceHysteresis = 6f;
    #endregion

    #endregion

    #region Properties
    public EnemyVisualMode VisualMode
    {
        get
        {
            return visualMode;
        }
    }

    public float VisualAnimationSpeed
    {
        get
        {
            return visualAnimationSpeed;
        }
    }

    public float GpuAnimationLoopDuration
    {
        get
        {
            return gpuAnimationLoopDuration;
        }
    }

    public bool EnableDistanceCulling
    {
        get
        {
            return enableDistanceCulling;
        }
    }

    public float MaxVisibleDistance
    {
        get
        {
            return maxVisibleDistance;
        }
    }

    public float VisibleDistanceHysteresis
    {
        get
        {
            return visibleDistanceHysteresis;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        switch (visualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
            case EnemyVisualMode.GpuBaked:
                break;

            default:
                visualMode = EnemyVisualMode.GpuBaked;
                break;
        }

        if (visualAnimationSpeed < 0f)
            visualAnimationSpeed = 0f;

        if (gpuAnimationLoopDuration < 0.05f)
            gpuAnimationLoopDuration = 0.05f;

        if (maxVisibleDistance < 0f)
            maxVisibleDistance = 0f;

        if (visibleDistanceHysteresis < 0f)
            visibleDistanceHysteresis = 0f;
    }
    #endregion

    #endregion
}

[CreateAssetMenu(fileName = "EnemyBrainPreset", menuName = "Enemy/Brain Preset", order = 10)]
public sealed class EnemyBrainPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Human-readable preset name for designers.")]
    [SerializeField] private string presetName = "New Enemy Brain Preset";

    [Tooltip("Short description of the preset use case.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Brain")]
    [Tooltip("Movement settings block.")]
    [SerializeField] private EnemyBrainMovementSettings movement = new EnemyBrainMovementSettings();

    [Tooltip("Steering settings block.")]
    [SerializeField] private EnemyBrainSteeringSettings steering = new EnemyBrainSteeringSettings();

    [Tooltip("Player contact settings block.")]
    [SerializeField] private EnemyBrainPlayerContactSettings playerContact = new EnemyBrainPlayerContactSettings();

    [Tooltip("Health and shield settings block.")]
    [SerializeField] private EnemyBrainHealthStatisticsSettings healthStatistics = new EnemyBrainHealthStatisticsSettings();

    [Tooltip("Visual behavior settings block.")]
    [SerializeField] private EnemyBrainVisualSettings visual = new EnemyBrainVisualSettings();
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public EnemyBrainMovementSettings Movement
    {
        get
        {
            return movement;
        }
    }

    public EnemyBrainSteeringSettings Steering
    {
        get
        {
            return steering;
        }
    }

    public EnemyBrainPlayerContactSettings PlayerContact
    {
        get
        {
            return playerContact;
        }
    }

    public EnemyBrainHealthStatisticsSettings HealthStatistics
    {
        get
        {
            return healthStatistics;
        }
    }

    public EnemyBrainVisualSettings Visual
    {
        get
        {
            return visual;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (movement == null)
            movement = new EnemyBrainMovementSettings();

        if (steering == null)
            steering = new EnemyBrainSteeringSettings();

        if (playerContact == null)
            playerContact = new EnemyBrainPlayerContactSettings();

        if (healthStatistics == null)
            healthStatistics = new EnemyBrainHealthStatisticsSettings();

        if (visual == null)
            visual = new EnemyBrainVisualSettings();

        movement.Validate();
        steering.Validate();
        playerContact.Validate();
        healthStatistics.Validate();
        visual.Validate();
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
