using Unity.Entities;
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
    #region Constants
    private static readonly Color ContactGizmoColor = new Color(1f, 0.25f, 0.25f, 0.9f);
    private static readonly Color AreaGizmoColor = new Color(1f, 0.55f, 0.15f, 0.9f);
    private static readonly Color SeparationGizmoColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    private static readonly Color BodyGizmoColor = new Color(1f, 0.9f, 0.2f, 0.9f);
    private static readonly Color VisualDistanceGizmoColor = new Color(0.15f, 1f, 0.75f, 0.9f);
    private static readonly Color ElementalAnchorGizmoColor = new Color(1f, 0.4f, 0.8f, 0.9f);
    private static readonly Color WorldSpaceBarsGizmoColor = new Color(0.25f, 0.95f, 0.45f, 0.9f);
    #endregion

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

    [Header("Visual References")]
    [Tooltip("Optional Animator used when visual mode is CompanionAnimator.")]
    [SerializeField] private Animator animatorComponent;

    [Tooltip("Optional transform used as anchor for attached elemental status VFX.")]
    [SerializeField] private Transform elementalVfxAnchor;

    [Tooltip("Optional world-space status bars view used to display fillable health and shield images above this enemy.")]
    [SerializeField] private EnemyWorldSpaceStatusBarsView worldSpaceStatusBarsView;

    [Header("Debug Gizmos")]
    [Tooltip("Draw the contact radius preview when the authoring object is selected.")]
    [SerializeField] private bool drawContactRadiusGizmo = true;

    [Tooltip("Draw the area damage radius preview when the authoring object is selected.")]
    [SerializeField] private bool drawAreaRadiusGizmo = true;

    [Tooltip("Draw the separation radius preview when the authoring object is selected.")]
    [SerializeField] private bool drawSeparationRadiusGizmo;

    [Tooltip("Draw the body radius preview used for projectile hit checks.")]
    [SerializeField] private bool drawBodyRadiusGizmo;

    [Tooltip("Draw the visual distance culling radius preview when enabled.")]
    [SerializeField] private bool drawVisualDistanceGizmo = true;

    [Tooltip("Draw a link gizmo from enemy pivot to world-space health and shield bars view.")]
    [SerializeField] private bool drawWorldSpaceBarsGizmo = true;
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
            return ResolveAdvancedPatternPreset();
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

    public float RotationSpeedDegreesPerSecond
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = ResolveMovementSettings();

            if (movementSettings == null)
                return rotationSpeedDegreesPerSecond;

            return movementSettings.RotationSpeedDegreesPerSecond;
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
            EnemyBrainDamageSettings damageSettings = ResolveDamageSettings();

            if (damageSettings == null)
                return contactRadius;

            return damageSettings.ContactRadius;
        }
    }

    public bool ContactDamageEnabled
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = ResolveDamageSettings();

            if (damageSettings == null)
                return contactDamageEnabled;

            return damageSettings.ContactDamageEnabled;
        }
    }

    public float ContactAmountPerTick
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = ResolveDamageSettings();

            if (damageSettings == null)
                return contactAmountPerTick;

            return damageSettings.ContactAmountPerTick;
        }
    }

    public float ContactTickInterval
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = ResolveDamageSettings();

            if (damageSettings == null)
                return contactTickInterval;

            return damageSettings.ContactTickInterval;
        }
    }

    public bool AreaDamageEnabled
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = ResolveDamageSettings();

            if (damageSettings == null)
                return areaDamageEnabled;

            return damageSettings.AreaDamageEnabled;
        }
    }

    public float AreaRadius
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = ResolveDamageSettings();

            if (damageSettings == null)
                return areaRadius;

            return damageSettings.AreaRadius;
        }
    }

    public float AreaAmountPerTickPercent
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = ResolveDamageSettings();

            if (damageSettings == null)
                return areaAmountPerTickPercent;

            return damageSettings.AreaAmountPerTickPercent;
        }
    }

    public float AreaTickInterval
    {
        get
        {
            EnemyBrainDamageSettings damageSettings = ResolveDamageSettings();

            if (damageSettings == null)
                return areaTickInterval;

            return damageSettings.AreaTickInterval;
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

    public int PriorityTier
    {
        get
        {
            EnemyBrainMovementSettings movementSettings = ResolveMovementSettings();

            if (movementSettings != null)
                return math.clamp(movementSettings.PriorityTier, -128, 128);

            EnemyBrainVisualSettings visualSettings = ResolveVisualSettings();

            if (visualSettings != null)
                return math.clamp(visualSettings.VisibilityPriorityTier, -128, 128);

            return math.clamp(priorityTier, -128, 128);
        }
    }

    public int VisibilityPriorityTier
    {
        get
        {
            return PriorityTier;
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
        ValidateFallbackValues();

        if (masterPreset != null)
            masterPreset.ValidateValues();

        if (brainPreset != null)
            brainPreset.ValidateValues();

        if (advancedPatternPreset != null)
            advancedPatternPreset.ValidateValues();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        if (drawContactRadiusGizmo && ContactDamageEnabled)
            DrawWireRadius(center, math.max(0f, ContactRadius), ContactGizmoColor);

        if (drawAreaRadiusGizmo && AreaDamageEnabled)
            DrawWireRadius(center, math.max(0f, AreaRadius), AreaGizmoColor);

        if (drawSeparationRadiusGizmo)
            DrawWireRadius(center, math.max(0f, SeparationRadius), SeparationGizmoColor);

        if (drawBodyRadiusGizmo)
            DrawWireRadius(center, math.max(0f, BodyRadius), BodyGizmoColor);

        if (drawVisualDistanceGizmo && EnableDistanceCulling)
            DrawWireRadius(center, math.max(0f, MaxVisibleDistance), VisualDistanceGizmoColor);

        if (elementalVfxAnchor == null)
        {
            DrawWorldSpaceBarsGizmo(center);
            return;
        }

        Gizmos.color = ElementalAnchorGizmoColor;
        Vector3 anchorPosition = elementalVfxAnchor.position;
        Gizmos.DrawLine(center, anchorPosition);
        Gizmos.DrawWireSphere(anchorPosition, 0.14f);
        DrawWorldSpaceBarsGizmo(center);
    }
    #endregion

    #region Validation
    private void ValidateFallbackValues()
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

        if (separationRadius < 0.1f)
            separationRadius = 0.1f;

        if (separationWeight < 0f)
            separationWeight = 0f;

        if (bodyRadius < 0.05f)
            bodyRadius = 0.05f;

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

        priorityTier = math.clamp(priorityTier, -128, 128);
    }
    #endregion

    #region Helpers
    private EnemyBrainPreset ResolveBrainPreset()
    {
        if (masterPreset != null && masterPreset.BrainPreset != null)
            return masterPreset.BrainPreset;

        return brainPreset;
    }

    private EnemyAdvancedPatternPreset ResolveAdvancedPatternPreset()
    {
        if (masterPreset != null && masterPreset.AdvancedPatternPreset != null)
            return masterPreset.AdvancedPatternPreset;

        return advancedPatternPreset;
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

    private EnemyBrainDamageSettings ResolveDamageSettings()
    {
        EnemyBrainPreset resolvedBrainPreset = ResolveBrainPreset();

        if (resolvedBrainPreset == null)
            return null;

        return resolvedBrainPreset.Damage;
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

    private void DrawWorldSpaceBarsGizmo(Vector3 center)
    {
        if (drawWorldSpaceBarsGizmo == false)
        {
            return;
        }

        if (worldSpaceStatusBarsView == null)
        {
            return;
        }

        Transform barsTransform = worldSpaceStatusBarsView.transform;

        if (barsTransform == null)
        {
            return;
        }

        Gizmos.color = WorldSpaceBarsGizmoColor;
        Vector3 barsPosition = barsTransform.position;
        Gizmos.DrawLine(center, barsPosition);
        Gizmos.DrawWireCube(barsPosition, new Vector3(0.2f, 0.08f, 0.02f));
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
            RotationSpeedDegreesPerSecond = authoring.RotationSpeedDegreesPerSecond,
            SeparationRadius = math.max(0.1f, authoring.SeparationRadius),
            SeparationWeight = math.max(0f, authoring.SeparationWeight),
            BodyRadius = math.max(0.05f, authoring.BodyRadius),
            PriorityTier = math.clamp(authoring.PriorityTier, -128, 128),
            ContactDamageEnabled = authoring.ContactDamageEnabled ? (byte)1 : (byte)0,
            ContactRadius = math.max(0f, authoring.ContactRadius),
            ContactAmountPerTick = math.max(0f, authoring.ContactAmountPerTick),
            ContactTickInterval = math.max(0.01f, authoring.ContactTickInterval),
            AreaDamageEnabled = authoring.AreaDamageEnabled ? (byte)1 : (byte)0,
            AreaRadius = math.max(0f, authoring.AreaRadius),
            AreaAmountPerTickPercent = math.max(0f, authoring.AreaAmountPerTickPercent),
            AreaTickInterval = math.max(0.01f, authoring.AreaTickInterval)
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
            ContactDamageCooldown = 0f,
            AreaDamageCooldown = 0f,
            SpawnVersion = 0u
        });

        EnemyCompiledPatternBakeResult compiledPattern = EnemyAdvancedPatternBakeUtility.Compile(authoring.AdvancedPatternPreset);

        AddComponent(entity, compiledPattern.PatternConfig);
        AddComponent(entity, new EnemyPatternRuntimeState
        {
            WanderTargetPosition = float3.zero,
            WanderWaitTimer = 0f,
            WanderRetryTimer = 0f,
            LastWanderDirectionAngle = 0f,
            WanderHasTarget = 0,
            WanderInitialized = 0,
            DvdDirection = float3.zero,
            DvdInitialized = 0
        });
        AddComponent(entity, new EnemyShooterControlState
        {
            MovementLocked = 0
        });

        if (compiledPattern.HasCustomMovement)
            AddComponent<EnemyCustomPatternMovementTag>(entity);

        DynamicBuffer<EnemyShooterConfigElement> shooterConfigs = AddBuffer<EnemyShooterConfigElement>(entity);
        DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = AddBuffer<EnemyShooterRuntimeElement>(entity);

        for (int shooterIndex = 0; shooterIndex < compiledPattern.ShooterConfigs.Count; shooterIndex++)
        {
            shooterConfigs.Add(compiledPattern.ShooterConfigs[shooterIndex]);
            shooterRuntime.Add(new EnemyShooterRuntimeElement
            {
                NextBurstTimer = 0f,
                NextShotInBurstTimer = 0f,
                RemainingBurstShots = 0,
                LockedAimDirection = float3.zero,
                HasLockedAimDirection = 0
            });
        }

        if (compiledPattern.ShooterConfigs.Count > 0)
            TryBakeShooterRuntime(authoring, entity, compiledPattern);

        EnemyVisualMode bakedVisualMode = ResolveBakedVisualMode(authoring, out Animator resolvedAnimatorComponent);

        AddComponent(entity, new EnemyVisualConfig
        {
            Mode = bakedVisualMode,
            AnimationSpeed = math.max(0f, authoring.VisualAnimationSpeed),
            GpuLoopDuration = math.max(0.05f, authoring.GpuAnimationLoopDuration),
            MaxVisibleDistance = math.max(0f, authoring.MaxVisibleDistance),
            VisibleDistanceHysteresis = math.max(0f, authoring.VisibleDistanceHysteresis),
            UseDistanceCulling = authoring.EnableDistanceCulling ? (byte)1 : (byte)0,
            VisibilityPriorityTier = math.clamp(authoring.PriorityTier, -128, 128)
        });

        AddComponent(entity, new EnemyVisualRuntimeState
        {
            AnimationTime = 0f,
            LastDistanceToPlayer = 0f,
            IsVisible = 1,
            CompanionInitialized = 0,
            AppliedVisibilityPriorityTier = int.MinValue
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

        EnemyWorldSpaceStatusBarsView resolvedStatusBarsView = ResolveWorldSpaceStatusBarsView(authoring);
        Entity statusBarsViewEntity = RegisterStatusBarsViewEntity(resolvedStatusBarsView);
        AddComponent(entity, new EnemyWorldSpaceStatusBarsLink
        {
            ViewEntity = statusBarsViewEntity
        });
        AddComponent(entity, new EnemyWorldSpaceStatusBarsRuntimeLink
        {
            ViewEntity = Entity.Null
        });

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

    private static EnemyWorldSpaceStatusBarsView ResolveWorldSpaceStatusBarsView(EnemyAuthoring authoring)
    {
        if (authoring == null)
        {
            return null;
        }

        EnemyWorldSpaceStatusBarsView assignedStatusBarsView = authoring.WorldSpaceStatusBarsView;

        if (assignedStatusBarsView != null &&
            assignedStatusBarsView.gameObject != null)
        {
            return assignedStatusBarsView;
        }

        EnemyWorldSpaceStatusBarsView fallbackStatusBarsView = authoring.GetComponentInChildren<EnemyWorldSpaceStatusBarsView>(true);

        if (fallbackStatusBarsView != null &&
            fallbackStatusBarsView.gameObject != null)
        {
            return fallbackStatusBarsView;
        }

        return null;
    }

    private void TryBakeShooterRuntime(EnemyAuthoring authoring, Entity entity, EnemyCompiledPatternBakeResult compiledPattern)
    {
        if (authoring == null)
            return;

        if (compiledPattern == null)
            return;

        GameObject projectilePrefabObject = compiledPattern.ShooterProjectilePrefab;

        if (IsInvalidShooterProjectilePrefab(authoring, projectilePrefabObject))
        {
#if UNITY_EDITOR
            if (projectilePrefabObject == null)
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Shooter modules are active on '{0}', but Runtime Projectile prefab is not assigned in the resolved Shooter payload.", authoring.name), authoring);
            else
                Debug.LogWarning(string.Format("[EnemyAuthoringBaker] Invalid Runtime Projectile prefab '{0}' on '{1}'. Assign a dedicated projectile prefab without authoring components.", projectilePrefabObject.name, authoring.name), authoring);
#endif
            return;
        }

        Entity projectilePrefabEntity = GetEntity(projectilePrefabObject, TransformUsageFlags.Dynamic);
        AddComponent(entity, new ShooterProjectilePrefab
        {
            PrefabEntity = projectilePrefabEntity
        });
        AddComponent(entity, new ProjectilePoolState
        {
            InitialCapacity = math.max(0, compiledPattern.ShooterProjectilePoolInitialCapacity),
            ExpandBatch = math.max(1, compiledPattern.ShooterProjectilePoolExpandBatch),
            Initialized = 0
        });
        AddBuffer<ShootRequest>(entity);
        AddBuffer<ProjectilePoolElement>(entity);
    }

    private static bool IsInvalidShooterProjectilePrefab(EnemyAuthoring authoring, GameObject projectilePrefabObject)
    {
        if (projectilePrefabObject == null)
            return true;

        if (authoring != null && projectilePrefabObject == authoring.gameObject)
            return true;

        if (projectilePrefabObject.scene.IsValid())
            return true;

        if (projectilePrefabObject.GetComponent<EnemyAuthoring>() != null)
            return true;

        if (projectilePrefabObject.GetComponent<PlayerAuthoring>() != null)
            return true;

        return false;
    }

    private Entity RegisterStatusBarsViewEntity(EnemyWorldSpaceStatusBarsView statusBarsView)
    {
        if (statusBarsView == null)
        {
            return Entity.Null;
        }

        GameObject viewGameObject = statusBarsView.gameObject;

        if (viewGameObject == null)
        {
            return Entity.Null;
        }

        return GetEntity(viewGameObject, TransformUsageFlags.Dynamic);
    }
    #endregion

    #endregion
}
