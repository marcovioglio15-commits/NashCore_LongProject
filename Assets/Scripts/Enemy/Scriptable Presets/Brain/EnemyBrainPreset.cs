using System;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Tooltip("Self-rotation speed in degrees per second. Positive rotates clockwise around Y, negative counter-clockwise.")]
    [SerializeField] private float rotationSpeedDegreesPerSecond;

    [Tooltip("General enemy priority tier used by steering and visual overlap rules. Higher values keep right-of-way over lower tiers.")]
    [SerializeField] private int priorityTier;

    [Tooltip("Scales steering and clearance reactivity. Higher values produce stronger side-step and avoidance corrections.")]
    [SerializeField] private float steeringAggressiveness = 1f;
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

    public float RotationSpeedDegreesPerSecond
    {
        get
        {
            return rotationSpeedDegreesPerSecond;
        }
    }

    public int PriorityTier
    {
        get
        {
            return priorityTier;
        }
    }

    public float SteeringAggressiveness
    {
        get
        {
            return steeringAggressiveness;
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

        if (float.IsNaN(rotationSpeedDegreesPerSecond) || float.IsInfinity(rotationSpeedDegreesPerSecond))
            rotationSpeedDegreesPerSecond = 0f;

        priorityTier = Mathf.Clamp(priorityTier, -128, 128);

        if (float.IsNaN(steeringAggressiveness) || float.IsInfinity(steeringAggressiveness))
            steeringAggressiveness = 1f;
        else
            steeringAggressiveness = Mathf.Clamp(steeringAggressiveness, 0f, 2.5f);
    }

    public void MigrateLegacyVisibilityPriorityIfNeeded(int legacyVisibilityPriorityTier)
    {
        if (priorityTier != 0)
            return;

        if (legacyVisibilityPriorityTier == 0)
            return;

        priorityTier = Mathf.Clamp(legacyVisibilityPriorityTier, -128, 128);
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
public sealed class EnemyBrainDamageSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, enemies apply flat periodic damage while the player is inside contact radius.")]
    [SerializeField] private bool contactDamageEnabled = true;

    [Tooltip("Distance from enemy center used to trigger contact damage ticks.")]
    [SerializeField] private float contactRadius = 1.2f;

    [Tooltip("Flat damage amount subtracted from player health at each contact tick.")]
    [FormerlySerializedAs("contactDamage")]
    [SerializeField] private float contactAmountPerTick = 5f;

    [Tooltip("Interval in seconds between two contact damage ticks.")]
    [FormerlySerializedAs("contactInterval")]
    [SerializeField] private float contactTickInterval = 0.75f;

    [Tooltip("When enabled, enemies apply periodic percentage damage while the player is inside area radius.")]
    [SerializeField] private bool areaDamageEnabled;

    [Tooltip("Distance from enemy center used to trigger area damage ticks.")]
    [SerializeField] private float areaRadius = 2.25f;

    [Tooltip("Percent of player max health applied per area damage tick. Example: 2 means 2%.")]
    [SerializeField] private float areaAmountPerTickPercent = 2f;

    [Tooltip("Interval in seconds between two area damage ticks.")]
    [SerializeField] private float areaTickInterval = 1f;
    #endregion

    #endregion

    #region Properties
    public bool ContactDamageEnabled
    {
        get
        {
            return contactDamageEnabled;
        }
    }

    public float ContactRadius
    {
        get
        {
            return contactRadius;
        }
    }

    public float ContactAmountPerTick
    {
        get
        {
            return contactAmountPerTick;
        }
    }

    public float ContactTickInterval
    {
        get
        {
            return contactTickInterval;
        }
    }

    public bool AreaDamageEnabled
    {
        get
        {
            return areaDamageEnabled;
        }
    }

    public float AreaRadius
    {
        get
        {
            return areaRadius;
        }
    }

    public float AreaAmountPerTickPercent
    {
        get
        {
            return areaAmountPerTickPercent;
        }
    }

    public float AreaTickInterval
    {
        get
        {
            return areaTickInterval;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (contactRadius < 0f)
            contactRadius = 0f;

        if (contactAmountPerTick < 0f)
            contactAmountPerTick = 0f;

        if (contactTickInterval < 0.01f)
            contactTickInterval = 0.01f;

        if (areaRadius < 0f)
            areaRadius = 0f;

        if (areaAmountPerTickPercent < 0f)
            areaAmountPerTickPercent = 0f;

        if (areaTickInterval < 0.01f)
            areaTickInterval = 0.01f;
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

    [Tooltip("Maximum shield reserve assigned to this enemy at spawn. Shield absorbs incoming damage before health.")]
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

    [Tooltip("Relative visibility priority used when enemies overlap visually. Higher values render on top where supported by renderer path.")]
    [SerializeField] private int visibilityPriorityTier;

    [Tooltip("Optional one-shot VFX prefab spawned every time this enemy receives a projectile hit.")]
    [SerializeField] private GameObject hitVfxPrefab;

    [Tooltip("Lifetime in seconds assigned to each spawned hit VFX instance.")]
    [SerializeField] private float hitVfxLifetimeSeconds = 0.35f;

    [Tooltip("Uniform scale multiplier applied to the spawned hit VFX instance.")]
    [SerializeField] private float hitVfxScaleMultiplier = 1f;
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

    public int VisibilityPriorityTier
    {
        get
        {
            return visibilityPriorityTier;
        }
    }

    public GameObject HitVfxPrefab
    {
        get
        {
            return hitVfxPrefab;
        }
    }

    public float HitVfxLifetimeSeconds
    {
        get
        {
            return hitVfxLifetimeSeconds;
        }
    }

    public float HitVfxScaleMultiplier
    {
        get
        {
            return hitVfxScaleMultiplier;
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

        visibilityPriorityTier = Mathf.Clamp(visibilityPriorityTier, -128, 128);

        if (float.IsNaN(hitVfxLifetimeSeconds) || float.IsInfinity(hitVfxLifetimeSeconds) || hitVfxLifetimeSeconds < 0.05f)
            hitVfxLifetimeSeconds = 0.05f;

        if (float.IsNaN(hitVfxScaleMultiplier) || float.IsInfinity(hitVfxScaleMultiplier) || hitVfxScaleMultiplier < 0.01f)
            hitVfxScaleMultiplier = 0.01f;
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

    [Tooltip("Preset name.")]
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

    [Tooltip("Damage settings block.")]
    [FormerlySerializedAs("playerContact")]
    [SerializeField] private EnemyBrainDamageSettings damage = new EnemyBrainDamageSettings();

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

    public EnemyBrainDamageSettings Damage
    {
        get
        {
            return damage;
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

        if (damage == null)
            damage = new EnemyBrainDamageSettings();

        if (healthStatistics == null)
            healthStatistics = new EnemyBrainHealthStatisticsSettings();

        if (visual == null)
            visual = new EnemyBrainVisualSettings();

        movement.Validate();
        steering.Validate();
        damage.Validate();
        healthStatistics.Validate();
        visual.Validate();
        movement.MigrateLegacyVisibilityPriorityIfNeeded(visual.VisibilityPriorityTier);
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
