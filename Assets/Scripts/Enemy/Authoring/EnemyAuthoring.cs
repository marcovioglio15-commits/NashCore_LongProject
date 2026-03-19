using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Authoring component that defines ECS enemy movement, steering, damage and visual settings.
/// Main configuration is sourced from EnemyMasterPreset and its sub-presets.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyAuthoring : MonoBehaviour
{
    #region Fields
    #region Serialized Fields
    [Header("Preset")]
    [Tooltip("Enemy master preset that resolves sub-presets used by this enemy.")]
    [SerializeField] private EnemyMasterPreset masterPreset;
    [Tooltip(" direct brain preset fallback used when MasterPreset is missing or has no Brain preset assigned.")]
    [SerializeField] private EnemyBrainPreset brainPreset;
    [Tooltip(" direct advanced pattern preset fallback used when MasterPreset is missing or has no Advanced Pattern preset assigned.")]
    [SerializeField] private EnemyAdvancedPatternPreset advancedPatternPreset;
    [Tooltip(" fallback move speed used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float moveSpeed = 3f;
    [Tooltip(" fallback max speed used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxSpeed = 4f;
    [Tooltip(" fallback acceleration used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float acceleration = 8f;
    [Tooltip(" fallback deceleration used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float deceleration = 8f;
    [Tooltip(" fallback self-rotation speed in degrees per second used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float rotationSpeedDegreesPerSecond;
    [Tooltip(" fallback separation radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float separationRadius = 1.1f;
    [Tooltip(" fallback separation weight used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float separationWeight = 2f;
    [Tooltip(" fallback body radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float bodyRadius = 0.55f;
    [Tooltip(" fallback contact radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float contactRadius = 1.2f;
    [Tooltip(" fallback contact damage enable used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private bool contactDamageEnabled = true;
    [Tooltip(" fallback contact amount per tick used when MasterPreset and BrainPreset are missing.")]
    [FormerlySerializedAs("contactDamage")]
    [SerializeField]
    [HideInInspector] private float contactAmountPerTick = 5f;
    [Tooltip(" fallback contact tick interval used when MasterPreset and BrainPreset are missing.")]
    [FormerlySerializedAs("contactInterval")]
    [SerializeField]
    [HideInInspector] private float contactTickInterval = 0.75f;
    [Tooltip(" fallback area damage enable used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private bool areaDamageEnabled;
    [Tooltip(" fallback area radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float areaRadius = 2.25f;
    [Tooltip(" fallback area amount per tick percent used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float areaAmountPerTickPercent = 2f;
    [Tooltip(" fallback area tick interval used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float areaTickInterval = 1f;
    [Tooltip(" fallback max health used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxHealth = 30f;
    [Tooltip(" fallback max shield used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxShield;
    [Tooltip(" fallback visual mode used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private EnemyVisualMode visualMode = EnemyVisualMode.GpuBaked;
    [Tooltip(" fallback visual animation speed used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float visualAnimationSpeed = 1f;

    [Tooltip(" fallback GPU loop duration used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float gpuAnimationLoopDuration = 1f;

    [Tooltip(" fallback distance culling toggle used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private bool enableDistanceCulling = true;

    [Tooltip(" fallback max visible distance used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxVisibleDistance = 55f;

    [Tooltip(" fallback culling hysteresis used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float visibleDistanceHysteresis = 6f;

    [Tooltip(" fallback general priority tier used for steering right-of-way and visual overlap ordering.")]
    [FormerlySerializedAs("visibilityPriorityTier")]
    [SerializeField]
    [HideInInspector] private int priorityTier;

    [Tooltip(" fallback steering and clearance reactivity scalar used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float steeringAggressiveness = 1f;

    [Tooltip(" fallback one-shot VFX prefab spawned each time this enemy receives a projectile hit.")]
    [SerializeField]
    [HideInInspector] private GameObject hitVfxPrefab;

    [Tooltip(" fallback hit VFX lifetime in seconds used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float hitVfxLifetimeSeconds = 0.35f;

    [Tooltip(" fallback hit VFX scale multiplier used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float hitVfxScaleMultiplier = 1f;

    [Header("Visual References")]
    [Tooltip("Optional Animator used when visual mode is CompanionAnimator.")]
    [SerializeField] private Animator animatorComponent;

    [Tooltip("Optional transform used as anchor for attached elemental status VFX.")]
    [SerializeField] private Transform elementalVfxAnchor;

    [Tooltip("Optional world-space status bars view used to display fillable health and shield images above this enemy.")]
    [SerializeField] private EnemyWorldSpaceStatusBarsView worldSpaceStatusBarsView;

    #endregion

    #endregion

    #region Properties
    public EnemyMasterPreset MasterPreset
    {
        get
        {
            return masterPreset;
        }
    }

    public EnemyBrainPreset BrainPreset
    {
        get
        {
            return brainPreset;
        }
    }

    public EnemyAdvancedPatternPreset AdvancedPatternPreset
    {
        get
        {
            return EnemyAuthoringPresetResolverUtility.ResolveAdvancedPatternPreset(masterPreset, advancedPatternPreset);
        }
    }

    public float MoveSpeed
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = EnemyAuthoringPresetResolverUtility.ResolveMovementSettings(masterPreset, brainPreset);

            if (movementSettings == null)
                return moveSpeed;

            return movementSettings.MoveSpeed;
        }
    }

    public float MaxSpeed
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = EnemyAuthoringPresetResolverUtility.ResolveMovementSettings(masterPreset, brainPreset);

            if (movementSettings == null)
                return maxSpeed;

            return movementSettings.MaxSpeed;
        }
    }

    public float Acceleration
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = EnemyAuthoringPresetResolverUtility.ResolveMovementSettings(masterPreset, brainPreset);

            if (movementSettings == null)
                return acceleration;

            return movementSettings.Acceleration;
        }
    }

    public float Deceleration
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = EnemyAuthoringPresetResolverUtility.ResolveMovementSettings(masterPreset, brainPreset);

            if (movementSettings == null)
                return deceleration;

            return movementSettings.Deceleration;
        }
    }

    public float RotationSpeedDegreesPerSecond
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = EnemyAuthoringPresetResolverUtility.ResolveMovementSettings(masterPreset, brainPreset);

            if (movementSettings == null)
                return rotationSpeedDegreesPerSecond;

            return movementSettings.RotationSpeedDegreesPerSecond;
        }
    }

    public float SeparationRadius
    {
        get
        {
            EnemyBrainSteeringSettings steeringSettings = EnemyAuthoringPresetResolverUtility.ResolveSteeringSettings(masterPreset, brainPreset);

            if (steeringSettings == null)
                return separationRadius;

            return steeringSettings.SeparationRadius;
        }
    }

    public float SeparationWeight
    {
        get
        {
            EnemyBrainSteeringSettings steeringSettings = EnemyAuthoringPresetResolverUtility.ResolveSteeringSettings(masterPreset, brainPreset);

            if (steeringSettings == null)
                return separationWeight;

            return steeringSettings.SeparationWeight;
        }
    }

    public float BodyRadius
    {
        get
        {
            EnemyBrainSteeringSettings steeringSettings = EnemyAuthoringPresetResolverUtility.ResolveSteeringSettings(masterPreset, brainPreset);

            if (steeringSettings == null)
                return bodyRadius;

            return steeringSettings.BodyRadius;
        }
    }

    public float ContactRadius
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = EnemyAuthoringPresetResolverUtility.ResolveDamageSettings(masterPreset, brainPreset);

            if (damageSettings == null)
                return contactRadius;

            return damageSettings.ContactRadius;
        }
    }

    public bool ContactDamageEnabled
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = EnemyAuthoringPresetResolverUtility.ResolveDamageSettings(masterPreset, brainPreset);

            if (damageSettings == null)
                return contactDamageEnabled;

            return damageSettings.ContactDamageEnabled;
        }
    }

    public float ContactAmountPerTick
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = EnemyAuthoringPresetResolverUtility.ResolveDamageSettings(masterPreset, brainPreset);

            if (damageSettings == null)
                return contactAmountPerTick;

            return damageSettings.ContactAmountPerTick;
        }
    }

    public float ContactTickInterval
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = EnemyAuthoringPresetResolverUtility.ResolveDamageSettings(masterPreset, brainPreset);

            if (damageSettings == null)
                return contactTickInterval;

            return damageSettings.ContactTickInterval;
        }
    }

    public bool AreaDamageEnabled
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = EnemyAuthoringPresetResolverUtility.ResolveDamageSettings(masterPreset, brainPreset);

            if (damageSettings == null)
                return areaDamageEnabled;

            return damageSettings.AreaDamageEnabled;
        }
    }

    public float AreaRadius
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = EnemyAuthoringPresetResolverUtility.ResolveDamageSettings(masterPreset, brainPreset);

            if (damageSettings == null)
                return areaRadius;

            return damageSettings.AreaRadius;
        }
    }

    public float AreaAmountPerTickPercent
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = EnemyAuthoringPresetResolverUtility.ResolveDamageSettings(masterPreset, brainPreset);

            if (damageSettings == null)
                return areaAmountPerTickPercent;

            return damageSettings.AreaAmountPerTickPercent;
        }
    }

    public float AreaTickInterval
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = EnemyAuthoringPresetResolverUtility.ResolveDamageSettings(masterPreset, brainPreset);

            if (damageSettings == null)
                return areaTickInterval;

            return damageSettings.AreaTickInterval;
        }
    }

    public float MaxHealth
    {
        get
        {
            EnemyBrainHealthStatisticsSettings healthSettings = EnemyAuthoringPresetResolverUtility.ResolveHealthStatisticsSettings(masterPreset, brainPreset);

            if (healthSettings == null)
                return maxHealth;

            return healthSettings.MaxHealth;
        }
    }

    public float MaxShield
    {
        get
        {
            EnemyBrainHealthStatisticsSettings healthSettings = EnemyAuthoringPresetResolverUtility.ResolveHealthStatisticsSettings(masterPreset, brainPreset);

            if (healthSettings == null)
                return maxShield;

            return healthSettings.MaxShield;
        }
    }

    public EnemyVisualMode VisualMode
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings == null)
                return visualMode;

            return visualSettings.VisualMode;
        }
    }

    public float VisualAnimationSpeed
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings == null)
                return visualAnimationSpeed;

            return visualSettings.VisualAnimationSpeed;
        }
    }

    public float GpuAnimationLoopDuration
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings == null)
                return gpuAnimationLoopDuration;

            return visualSettings.GpuAnimationLoopDuration;
        }
    }

    public bool EnableDistanceCulling
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings == null)
                return enableDistanceCulling;

            return visualSettings.EnableDistanceCulling;
        }
    }

    public float MaxVisibleDistance
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings == null)
                return maxVisibleDistance;

            return visualSettings.MaxVisibleDistance;
        }
    }

    public float VisibleDistanceHysteresis
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings == null)
                return visibleDistanceHysteresis;

            return visualSettings.VisibleDistanceHysteresis;
        }
    }

    public int PriorityTier
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = EnemyAuthoringPresetResolverUtility.ResolveMovementSettings(masterPreset, brainPreset);

            if (movementSettings != null)
                return math.clamp(movementSettings.PriorityTier, -128, 128);

            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings != null)
                return math.clamp(visualSettings.VisibilityPriorityTier, -128, 128);

            return math.clamp(priorityTier, -128, 128);
        }
    }

    public float SteeringAggressiveness
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = EnemyAuthoringPresetResolverUtility.ResolveMovementSettings(masterPreset, brainPreset);

            if (movementSettings != null)
            {
                float resolvedAggressiveness = movementSettings.SteeringAggressiveness;

                if (float.IsNaN(resolvedAggressiveness) || float.IsInfinity(resolvedAggressiveness))
                    return 1f;

                return math.clamp(resolvedAggressiveness, 0f, 2.5f);
            }

            if (float.IsNaN(steeringAggressiveness) || float.IsInfinity(steeringAggressiveness))
                return 1f;

            return math.clamp(steeringAggressiveness, 0f, 2.5f);
        }
    }

    public int VisibilityPriorityTier
    {
        get
        {
            return PriorityTier;
        }
    }

    public GameObject HitVfxPrefab
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings == null)
                return hitVfxPrefab;

            return visualSettings.HitVfxPrefab;
        }
    }

    public float HitVfxLifetimeSeconds
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings == null)
                return hitVfxLifetimeSeconds;

            return visualSettings.HitVfxLifetimeSeconds;
        }
    }

    public float HitVfxScaleMultiplier
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = EnemyAuthoringPresetResolverUtility.ResolveVisualSettings(masterPreset, brainPreset);

            if (visualSettings == null)
                return hitVfxScaleMultiplier;

            return visualSettings.HitVfxScaleMultiplier;
        }
    }

    public Animator AnimatorComponent
    {
        get
        {
            return animatorComponent;
        }
    }

    public Transform ElementalVfxAnchor
    {
        get
        {
            return elementalVfxAnchor;
        }
    }

    public EnemyWorldSpaceStatusBarsView WorldSpaceStatusBarsView
    {
        get
        {
            return worldSpaceStatusBarsView;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnValidate()
    {
        EnemyAuthoringFallbackValidationUtility.ValidateFallbackValues(ref moveSpeed,
                                                                      ref maxSpeed,
                                                                      ref acceleration,
                                                                      ref deceleration,
                                                                      ref rotationSpeedDegreesPerSecond,
                                                                      ref separationRadius,
                                                                      ref separationWeight,
                                                                      ref bodyRadius,
                                                                      ref contactRadius,
                                                                      ref contactAmountPerTick,
                                                                      ref contactTickInterval,
                                                                      ref areaRadius,
                                                                      ref areaAmountPerTickPercent,
                                                                      ref areaTickInterval,
                                                                      ref maxHealth,
                                                                      ref maxShield,
                                                                      ref visualMode,
                                                                      ref visualAnimationSpeed,
                                                                      ref gpuAnimationLoopDuration,
                                                                      ref maxVisibleDistance,
                                                                      ref visibleDistanceHysteresis,
                                                                      ref priorityTier,
                                                                      ref steeringAggressiveness,
                                                                      ref hitVfxLifetimeSeconds,
                                                                      ref hitVfxScaleMultiplier);

        if (masterPreset != null)
            masterPreset.ValidateValues();

        if (brainPreset != null)
            brainPreset.ValidateValues();

        if (advancedPatternPreset != null)
            advancedPatternPreset.ValidateValues();
    }

    #endregion

    #endregion
}
