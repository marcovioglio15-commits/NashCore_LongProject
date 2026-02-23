using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component that defines ECS enemy movement, steering, contact and visual settings.
/// Main configuration is sourced from EnemyMasterPreset and its sub-presets.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyAuthoring : MonoBehaviour
{
    #region Constants
    private static readonly Color ContactGizmoColor = new Color(1f, 0.25f, 0.25f, 0.9f);
    private static readonly Color SeparationGizmoColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    private static readonly Color BodyGizmoColor = new Color(1f, 0.9f, 0.2f, 0.9f);
    private static readonly Color VisualDistanceGizmoColor = new Color(0.15f, 1f, 0.75f, 0.9f);
    private static readonly Color ElementalAnchorGizmoColor = new Color(1f, 0.4f, 0.8f, 0.9f);
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Preset")]
    [Tooltip("Enemy master preset that resolves sub-presets used by this enemy.")]
    [SerializeField] private EnemyMasterPreset masterPreset;

    [Tooltip("Legacy direct brain preset fallback used when MasterPreset is missing or has no Brain preset assigned.")]
    [SerializeField] private EnemyBrainPreset brainPreset;

    [Tooltip("Legacy fallback move speed used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float moveSpeed = 3f;

    [Tooltip("Legacy fallback max speed used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxSpeed = 4f;

    [Tooltip("Legacy fallback acceleration used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float acceleration = 8f;

    [Tooltip("Legacy fallback deceleration used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float deceleration = 8f;

    [Tooltip("Legacy fallback separation radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float separationRadius = 1.1f;

    [Tooltip("Legacy fallback separation weight used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float separationWeight = 2f;

    [Tooltip("Legacy fallback body radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float bodyRadius = 0.55f;

    [Tooltip("Legacy fallback contact radius used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float contactRadius = 1.2f;

    [Tooltip("Legacy fallback contact damage used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float contactDamage = 5f;

    [Tooltip("Legacy fallback contact interval used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float contactInterval = 0.75f;

    [Tooltip("Legacy fallback max health used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxHealth = 30f;

    [Tooltip("Legacy fallback max shield used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxShield;

    [Tooltip("Legacy fallback visual mode used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private EnemyVisualMode visualMode = EnemyVisualMode.GpuBaked;

    [Tooltip("Legacy fallback visual animation speed used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float visualAnimationSpeed = 1f;

    [Tooltip("Legacy fallback GPU loop duration used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float gpuAnimationLoopDuration = 1f;

    [Tooltip("Legacy fallback distance culling toggle used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private bool enableDistanceCulling = true;

    [Tooltip("Legacy fallback max visible distance used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float maxVisibleDistance = 55f;

    [Tooltip("Legacy fallback culling hysteresis used when MasterPreset and BrainPreset are missing.")]
    [SerializeField]
    [HideInInspector] private float visibleDistanceHysteresis = 6f;

    [Header("Visual References")]
    [Tooltip("Optional Animator used when visual mode is CompanionAnimator.")]
    [SerializeField] private Animator animatorComponent;

    [Tooltip("Optional transform used as anchor for attached elemental status VFX.")]
    [SerializeField] private Transform elementalVfxAnchor;

    [Header("Debug Gizmos")]
    [Tooltip("Draw the contact radius preview when the authoring object is selected.")]
    [SerializeField] private bool drawContactRadiusGizmo = true;

    [Tooltip("Draw the separation radius preview when the authoring object is selected.")]
    [SerializeField] private bool drawSeparationRadiusGizmo;

    [Tooltip("Draw the body radius preview used for projectile hit checks.")]
    [SerializeField] private bool drawBodyRadiusGizmo;

    [Tooltip("Draw the visual distance culling radius preview when enabled.")]
    [SerializeField] private bool drawVisualDistanceGizmo = true;
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

    public float MoveSpeed
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = ResolveMovementSettings();

            if (movementSettings == null)
                return moveSpeed;

            return movementSettings.MoveSpeed;
        }
    }

    public float MaxSpeed
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = ResolveMovementSettings();

            if (movementSettings == null)
                return maxSpeed;

            return movementSettings.MaxSpeed;
        }
    }

    public float Acceleration
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = ResolveMovementSettings();

            if (movementSettings == null)
                return acceleration;

            return movementSettings.Acceleration;
        }
    }

    public float Deceleration
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = ResolveMovementSettings();

            if (movementSettings == null)
                return deceleration;

            return movementSettings.Deceleration;
        }
    }

    public float SeparationRadius
    {
        get
        {
            EnemyBrainSteeringSettings steeringSettings = ResolveSteeringSettings();

            if (steeringSettings == null)
                return separationRadius;

            return steeringSettings.SeparationRadius;
        }
    }

    public float SeparationWeight
    {
        get
        {
            EnemyBrainSteeringSettings steeringSettings = ResolveSteeringSettings();

            if (steeringSettings == null)
                return separationWeight;

            return steeringSettings.SeparationWeight;
        }
    }

    public float BodyRadius
    {
        get
        {
            EnemyBrainSteeringSettings steeringSettings = ResolveSteeringSettings();

            if (steeringSettings == null)
                return bodyRadius;

            return steeringSettings.BodyRadius;
        }
    }

    public float ContactRadius
    {
        get
        {
            EnemyBrainPlayerContactSettings contactSettings = ResolvePlayerContactSettings();

            if (contactSettings == null)
                return contactRadius;

            return contactSettings.ContactRadius;
        }
    }

    public float ContactDamage
    {
        get
        {
            EnemyBrainPlayerContactSettings contactSettings = ResolvePlayerContactSettings();

            if (contactSettings == null)
                return contactDamage;

            return contactSettings.ContactDamage;
        }
    }

    public float ContactInterval
    {
        get
        {
            EnemyBrainPlayerContactSettings contactSettings = ResolvePlayerContactSettings();

            if (contactSettings == null)
                return contactInterval;

            return contactSettings.ContactInterval;
        }
    }

    public float MaxHealth
    {
        get
        {
            EnemyBrainHealthStatisticsSettings healthSettings = ResolveHealthStatisticsSettings();

            if (healthSettings == null)
                return maxHealth;

            return healthSettings.MaxHealth;
        }
    }

    public float MaxShield
    {
        get
        {
            EnemyBrainHealthStatisticsSettings healthSettings = ResolveHealthStatisticsSettings();

            if (healthSettings == null)
                return maxShield;

            return healthSettings.MaxShield;
        }
    }

    public EnemyVisualMode VisualMode
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = ResolveVisualSettings();

            if (visualSettings == null)
                return visualMode;

            return visualSettings.VisualMode;
        }
    }

    public float VisualAnimationSpeed
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = ResolveVisualSettings();

            if (visualSettings == null)
                return visualAnimationSpeed;

            return visualSettings.VisualAnimationSpeed;
        }
    }

    public float GpuAnimationLoopDuration
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = ResolveVisualSettings();

            if (visualSettings == null)
                return gpuAnimationLoopDuration;

            return visualSettings.GpuAnimationLoopDuration;
        }
    }

    public bool EnableDistanceCulling
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = ResolveVisualSettings();

            if (visualSettings == null)
                return enableDistanceCulling;

            return visualSettings.EnableDistanceCulling;
        }
    }

    public float MaxVisibleDistance
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = ResolveVisualSettings();

            if (visualSettings == null)
                return maxVisibleDistance;

            return visualSettings.MaxVisibleDistance;
        }
    }

    public float VisibleDistanceHysteresis
    {
        get
        {
            EnemyBrainVisualSettings visualSettings = ResolveVisualSettings();

            if (visualSettings == null)
                return visibleDistanceHysteresis;

            return visualSettings.VisibleDistanceHysteresis;
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
    #endregion

    #region Methods

    #region Unity Methods
    private void OnValidate()
    {
        ValidateLegacyFallbackValues();

        if (masterPreset != null)
            masterPreset.ValidateValues();

        if (brainPreset != null)
            brainPreset.ValidateValues();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        if (drawContactRadiusGizmo)
            DrawWireRadius(center, math.max(0f, ContactRadius), ContactGizmoColor);

        if (drawSeparationRadiusGizmo)
            DrawWireRadius(center, math.max(0f, SeparationRadius), SeparationGizmoColor);

        if (drawBodyRadiusGizmo)
            DrawWireRadius(center, math.max(0f, BodyRadius), BodyGizmoColor);

        if (drawVisualDistanceGizmo && EnableDistanceCulling)
            DrawWireRadius(center, math.max(0f, MaxVisibleDistance), VisualDistanceGizmoColor);

        if (elementalVfxAnchor == null)
            return;

        Gizmos.color = ElementalAnchorGizmoColor;
        Vector3 anchorPosition = elementalVfxAnchor.position;
        Gizmos.DrawLine(center, anchorPosition);
        Gizmos.DrawWireSphere(anchorPosition, 0.14f);
    }
    #endregion

    #region Validation
    private void ValidateLegacyFallbackValues()
    {
        if (moveSpeed < 0f)
            moveSpeed = 0f;

        if (maxSpeed < 0f)
            maxSpeed = 0f;

        if (acceleration < 0f)
            acceleration = 0f;

        if (deceleration < 0f)
            deceleration = 0f;

        if (separationRadius < 0.1f)
            separationRadius = 0.1f;

        if (separationWeight < 0f)
            separationWeight = 0f;

        if (bodyRadius < 0.05f)
            bodyRadius = 0.05f;

        if (contactRadius < 0f)
            contactRadius = 0f;

        if (contactDamage < 0f)
            contactDamage = 0f;

        if (contactInterval < 0.01f)
            contactInterval = 0.01f;

        if (maxHealth < 1f)
            maxHealth = 1f;

        if (maxShield < 0f)
            maxShield = 0f;

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

    #region Helpers
    private EnemyBrainPreset ResolveBrainPreset()
    {
        if (masterPreset != null && masterPreset.BrainPreset != null)
            return masterPreset.BrainPreset;

        return brainPreset;
    }

    private EnemyBrainMovementSettings ResolveMovementSettings()
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset();

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Movement;
    }

    private EnemyBrainSteeringSettings ResolveSteeringSettings()
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset();

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Steering;
    }

    private EnemyBrainPlayerContactSettings ResolvePlayerContactSettings()
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset();

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.PlayerContact;
    }

    private EnemyBrainHealthStatisticsSettings ResolveHealthStatisticsSettings()
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset();

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.HealthStatistics;
    }

    private EnemyBrainVisualSettings ResolveVisualSettings()
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset();

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Visual;
    }

    private static void DrawWireRadius(Vector3 center, float radius, Color color)
    {
        if (radius <= 0f)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(center, radius);
    }
    #endregion

    #endregion
}

/// <summary>
/// Bakes EnemyAuthoring data into ECS enemy components.
/// </summary>
public sealed class EnemyAuthoringBaker : Baker<EnemyAuthoring>
{
    #region Methods

    #region Bake
    public override void Bake(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return;

        Entity entity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(entity, new EnemyData
        {
            MoveSpeed = math.max(0f, authoring.MoveSpeed),
            MaxSpeed = math.max(0f, authoring.MaxSpeed),
            Acceleration = math.max(0f, authoring.Acceleration),
            Deceleration = math.max(0f, authoring.Deceleration),
            SeparationRadius = math.max(0.1f, authoring.SeparationRadius),
            SeparationWeight = math.max(0f, authoring.SeparationWeight),
            BodyRadius = math.max(0.05f, authoring.BodyRadius),
            ContactRadius = math.max(0f, authoring.ContactRadius),
            ContactDamage = math.max(0f, authoring.ContactDamage),
            ContactInterval = math.max(0.01f, authoring.ContactInterval)
        });

        float bakedHealth = math.max(1f, authoring.MaxHealth);
        float bakedShield = math.max(0f, authoring.MaxShield);

        AddComponent(entity, new EnemyHealth
        {
            Current = bakedHealth,
            Max = bakedHealth,
            CurrentShield = bakedShield,
            MaxShield = bakedShield
        });

        AddComponent(entity, new EnemyRuntimeState
        {
            Velocity = float3.zero,
            ContactCooldown = 0f,
            SpawnVersion = 0u
        });

        EnemyVisualMode bakedVisualMode = ResolveBakedVisualMode(authoring, out Animator resolvedAnimatorComponent);

        AddComponent(entity, new EnemyVisualConfig
        {
            Mode = bakedVisualMode,
            AnimationSpeed = math.max(0f, authoring.VisualAnimationSpeed),
            GpuLoopDuration = math.max(0.05f, authoring.GpuAnimationLoopDuration),
            MaxVisibleDistance = math.max(0f, authoring.MaxVisibleDistance),
            VisibleDistanceHysteresis = math.max(0f, authoring.VisibleDistanceHysteresis),
            UseDistanceCulling = authoring.EnableDistanceCulling ? (byte)1 : (byte)0
        });

        AddComponent(entity, new EnemyVisualRuntimeState
        {
            AnimationTime = 0f,
            LastDistanceToPlayer = 0f,
            IsVisible = 1,
            CompanionInitialized = 0
        });

        switch (bakedVisualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
                AddComponentObject(entity, resolvedAnimatorComponent);
                AddComponent<EnemyVisualCompanionAnimator>(entity);
                break;

            default:
                AddComponent<EnemyVisualGpuBaked>(entity);
                break;
        }

        AddComponent(entity, new EnemyOwnerSpawner
        {
            SpawnerEntity = Entity.Null
        });

        Entity anchorEntity = Entity.Null;

        if (authoring.ElementalVfxAnchor != null)
            anchorEntity = GetEntity(authoring.ElementalVfxAnchor, TransformUsageFlags.Dynamic);

        AddComponent(entity, new EnemyElementalVfxAnchor
        {
            AnchorEntity = anchorEntity
        });

        AddComponent<EnemyActive>(entity);
        SetComponentEnabled<EnemyActive>(entity, false);
    }
    #endregion

    #region Helpers
    private static EnemyVisualMode ResolveBakedVisualMode(EnemyAuthoring authoring, out Animator resolvedAnimatorComponent)
    {
        resolvedAnimatorComponent = null;

        if (authoring == null)
            return EnemyVisualMode.GpuBaked;

        EnemyVisualMode requestedMode = authoring.VisualMode;

        switch (requestedMode)
        {
            case EnemyVisualMode.CompanionAnimator:
                resolvedAnimatorComponent = ResolveAnimatorComponent(authoring);

                if (resolvedAnimatorComponent != null)
                    return EnemyVisualMode.CompanionAnimator;

#if UNITY_EDITOR
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] CompanionAnimator requested on '{0}', but no valid scene Animator was resolved. Falling back to GpuBaked mode.",
                                               authoring.name),
                                 authoring);
#endif
                return EnemyVisualMode.GpuBaked;

            case EnemyVisualMode.GpuBaked:
                return EnemyVisualMode.GpuBaked;

            default:
                return EnemyVisualMode.GpuBaked;
        }
    }

    private static Animator ResolveAnimatorComponent(EnemyAuthoring authoring)
    {
        if (authoring == null)
            return null;

        Animator assignedAnimator = authoring.AnimatorComponent;

        if (assignedAnimator != null &&
            assignedAnimator.gameObject != null &&
            assignedAnimator.gameObject.scene.IsValid())
            return assignedAnimator;

        Animator fallbackAnimator = authoring.GetComponentInChildren<Animator>(true);

        if (fallbackAnimator != null &&
            fallbackAnimator.gameObject != null &&
            fallbackAnimator.gameObject.scene.IsValid())
            return fallbackAnimator;

        return null;
    }
    #endregion

    #endregion
}
