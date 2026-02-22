using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component that defines ECS enemy movement, separation, and contact-damage settings.
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
    [Header("Movement")]
    [Tooltip("Meters per second used as baseline enemy movement speed toward the player.")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Hard cap applied to the enemy velocity magnitude.")]
    [SerializeField] private float maxSpeed = 4f;

    [Tooltip("Meters per second squared used to accelerate toward desired velocity.")]
    [SerializeField] private float acceleration = 8f;

    [Header("Separation")]
    [Tooltip("Radius used to search neighboring enemies for separation steering.")]
    [SerializeField] private float separationRadius = 1.1f;

    [Tooltip("Weight applied to the separation vector before velocity clamping.")]
    [SerializeField] private float separationWeight = 2f;

    [Tooltip("Physical body radius used for projectile hit checks and short-range overlap handling.")]
    [SerializeField] private float bodyRadius = 0.55f;

    [Header("Contact Damage")]
    [Tooltip("Distance from enemy center to apply contact damage to the player.")]
    [SerializeField] private float contactRadius = 1.2f;

    [Tooltip("Damage applied to the player each time contact cooldown expires in range.")]
    [SerializeField] private float contactDamage = 5f;

    [Tooltip("Cooldown in seconds between two consecutive contact damage applications.")]
    [SerializeField] private float contactInterval = 0.75f;

    [Header("Health")]
    [Tooltip("Maximum and initial health assigned to this enemy when spawned from pool.")]
    [SerializeField] private float maxHealth = 30f;

    [Header("Visual")]
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

    [Tooltip("Optional Animator used when visual mode is CompanionAnimator.")]
    [SerializeField] private Animator animatorComponent;

    [Header("VFX Anchors")]
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

    public float BodyRadius
    {
        get
        {
            return bodyRadius;
        }
    }

    public float MaxHealth
    {
        get
        {
            return maxHealth;
        }
    }

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
        if (moveSpeed < 0f)
            moveSpeed = 0f;

        if (maxSpeed < 0f)
            maxSpeed = 0f;

        if (acceleration < 0f)
            acceleration = 0f;

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

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        if (drawContactRadiusGizmo)
        {
            float radius = math.max(0f, contactRadius);

            if (radius > 0f)
            {
                Gizmos.color = ContactGizmoColor;
                Gizmos.DrawWireSphere(center, radius);
            }
        }

        if (drawSeparationRadiusGizmo)
        {
            float radius = math.max(0f, separationRadius);

            if (radius > 0f)
            {
                Gizmos.color = SeparationGizmoColor;
                Gizmos.DrawWireSphere(center, radius);
            }
        }

        if (drawBodyRadiusGizmo)
        {
            float radius = math.max(0f, bodyRadius);

            if (radius > 0f)
            {
                Gizmos.color = BodyGizmoColor;
                Gizmos.DrawWireSphere(center, radius);
            }
        }

        if (drawVisualDistanceGizmo && enableDistanceCulling)
        {
            float radius = math.max(0f, maxVisibleDistance);

            if (radius > 0f)
            {
                Gizmos.color = VisualDistanceGizmoColor;
                Gizmos.DrawWireSphere(center, radius);
            }
        }

        if (elementalVfxAnchor != null)
        {
            Gizmos.color = ElementalAnchorGizmoColor;
            Vector3 anchorPosition = elementalVfxAnchor.position;
            Gizmos.DrawLine(center, anchorPosition);
            Gizmos.DrawWireSphere(anchorPosition, 0.14f);
        }
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
            SeparationRadius = math.max(0.1f, authoring.SeparationRadius),
            SeparationWeight = math.max(0f, authoring.SeparationWeight),
            BodyRadius = math.max(0.05f, authoring.BodyRadius),
            ContactRadius = math.max(0f, authoring.ContactRadius),
            ContactDamage = math.max(0f, authoring.ContactDamage),
            ContactInterval = math.max(0.01f, authoring.ContactInterval)
        });

        float bakedHealth = math.max(1f, authoring.MaxHealth);

        AddComponent(entity, new EnemyHealth
        {
            Current = bakedHealth,
            Max = bakedHealth
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
