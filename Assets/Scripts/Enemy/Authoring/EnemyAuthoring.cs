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

    [Header("Debug Gizmos")]
    [Tooltip("Draw the contact radius preview when the authoring object is selected.")]
    [SerializeField] private bool drawContactRadiusGizmo = true;

    [Tooltip("Draw the separation radius preview when the authoring object is selected.")]
    [SerializeField] private bool drawSeparationRadiusGizmo;

    [Tooltip("Draw the body radius preview used for projectile hit checks.")]
    [SerializeField] private bool drawBodyRadiusGizmo;
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
            ContactCooldown = 0f
        });

        AddComponent(entity, new EnemyOwnerSpawner
        {
            SpawnerEntity = Entity.Null
        });

        AddComponent<EnemyActive>(entity);
        SetComponentEnabled<EnemyActive>(entity, false);
    }
    #endregion

    #endregion
}
