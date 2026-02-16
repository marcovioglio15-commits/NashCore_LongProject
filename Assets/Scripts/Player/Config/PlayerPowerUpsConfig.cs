using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

#region Components
/// <summary>
/// Holds baked runtime configuration for the player's power-up slots.
/// </summary>
public struct PlayerPowerUpsConfig : IComponentData
{
    public PlayerPowerUpSlotConfig PrimarySlot;
    public PlayerPowerUpSlotConfig SecondarySlot;
}

/// <summary>
/// Holds baked runtime configuration for a single active-tool slot.
/// </summary>
public struct PlayerPowerUpSlotConfig
{
    public byte IsDefined;
    public ActiveToolKind ToolKind;
    public PowerUpResourceType ActivationResource;
    public PowerUpResourceType MaintenanceResource;
    public PowerUpChargeType ChargeType;
    public float MaximumEnergy;
    public float ActivationCost;
    public float MaintenanceCostPerSecond;
    public float ChargePerTrigger;
    public byte Toggleable;
    public byte FullChargeRequirement;
    public byte Unreplaceable;
    public Entity BombPrefabEntity;
    public BombPowerUpConfig Bomb;
    public DashPowerUpConfig Dash;
    public BulletTimePowerUpConfig BulletTime;
}

/// <summary>
/// Holds baked runtime configuration for the Bomb active tool.
/// </summary>
public struct BombPowerUpConfig
{
    public float3 SpawnOffset;
    public float DeploySpeed;
    public float CollisionRadius;
    public byte BounceOnWalls;
    public float BounceDamping;
    public float LinearDampingPerSecond;
    public float FuseSeconds;
    public float Radius;
    public float Damage;
    public byte AffectAllEnemiesInRadius;
}

/// <summary>
/// Holds baked runtime configuration for the Dash active tool.
/// </summary>
public struct DashPowerUpConfig
{
    public float Distance;
    public float Duration;
    public float SpeedTransitionInSeconds;
    public float SpeedTransitionOutSeconds;
    public byte GrantsInvulnerability;
    public float InvulnerabilityExtraTime;
}

/// <summary>
/// Holds baked runtime configuration for the Bullet Time active tool.
/// </summary>
public struct BulletTimePowerUpConfig
{
    public float Duration;
    public float EnemySlowPercent;
}

/// <summary>
/// Holds baked runtime configuration for a single passive-tool slot.
/// </summary>
public struct PlayerPassiveToolConfig
{
    public byte IsDefined;
    public PassiveToolKind ToolKind;
    public ProjectileSizePassiveConfig ProjectileSize;
    public ElementalProjectilesPassiveConfig ElementalProjectiles;
    public PerfectCirclePassiveConfig PerfectCircle;
    public BouncingProjectilesPassiveConfig BouncingProjectiles;
    public SplittingProjectilesPassiveConfig SplittingProjectiles;
    public ExplosionPassiveConfig Explosion;
    public ElementalTrailPassiveConfig ElementalTrail;
}

/// <summary>
/// Buffer entry representing one equipped passive tool in the player's startup loadout.
/// </summary>
public struct EquippedPassiveToolElement : IBufferElementData
{
    public PlayerPassiveToolConfig Tool;
}

/// <summary>
/// Holds baked runtime configuration for the Projectile Size passive tool.
/// </summary>
public struct ProjectileSizePassiveConfig
{
    public float SizeMultiplier;
    public float DamageMultiplier;
    public float SpeedMultiplier;
    public float LifetimeSecondsMultiplier;
    public float LifetimeRangeMultiplier;
}

/// <summary>
/// Shared elemental payload configuration used by elemental projectiles and elemental trail.
/// </summary>
public struct ElementalEffectConfig
{
    public ElementType ElementType;
    public ElementalEffectKind EffectKind;
    public ElementalProcMode ProcMode;
    public ElementalProcReapplyMode ReapplyMode;
    public float ProcThresholdStacks;
    public float MaximumStacks;
    public float StackDecayPerSecond;
    public byte ConsumeStacksOnProc;
    public float DotDamagePerTick;
    public float DotTickInterval;
    public float DotDurationSeconds;
    public float ImpedimentSlowPercentPerStack;
    public float ImpedimentProcSlowPercent;
    public float ImpedimentMaxSlowPercent;
    public float ImpedimentDurationSeconds;
}

/// <summary>
/// Holds baked runtime VFX assignment for one elemental type.
/// </summary>
public struct ElementalVfxDefinitionConfig
{
    public byte SpawnStackVfx;
    public Entity StackVfxPrefabEntity;
    public float StackVfxScaleMultiplier;
    public byte SpawnProcVfx;
    public Entity ProcVfxPrefabEntity;
    public float ProcVfxScaleMultiplier;
}

/// <summary>
/// Holds baked runtime VFX assignments for all elemental types.
/// </summary>
public struct PlayerElementalVfxConfig : IComponentData
{
    public ElementalVfxDefinitionConfig Fire;
    public ElementalVfxDefinitionConfig Ice;
    public ElementalVfxDefinitionConfig Poison;
    public ElementalVfxDefinitionConfig Custom;
}

/// <summary>
/// Holds baked runtime configuration for the Elemental Projectiles passive tool.
/// </summary>
public struct ElementalProjectilesPassiveConfig
{
    public ElementalEffectConfig Effect;
    public float StacksPerHit;
}

/// <summary>
/// Holds baked runtime configuration for the Perfect Circle passive tool.
/// </summary>
public struct PerfectCirclePassiveConfig
{
    public float RadialEntrySpeed;
    public float OrbitalSpeed;
    public float OrbitRadiusMin;
    public float OrbitRadiusMax;
    public float OrbitPulseFrequency;
    public float OrbitEntryRatio;
    public float OrbitBlendDuration;
    public float HeightOffset;
    public float GoldenAngleDegrees;
}

/// <summary>
/// Holds baked runtime configuration for the Bouncing Projectiles passive tool.
/// </summary>
public struct BouncingProjectilesPassiveConfig
{
    public int MaxBounces;
    public float SpeedPercentChangePerBounce;
    public float MinimumSpeedMultiplierAfterBounce;
    public float MaximumSpeedMultiplierAfterBounce;
}

/// <summary>
/// Holds baked runtime configuration for the Splitting Projectiles passive tool.
/// </summary>
public struct SplittingProjectilesPassiveConfig
{
    public ProjectileSplitDirectionMode DirectionMode;
    public int SplitProjectileCount;
    public float SplitOffsetDegrees;
    public FixedList128Bytes<float> CustomAnglesDegrees;
    public float SplitDamageMultiplier;
    public float SplitSizeMultiplier;
    public float SplitSpeedMultiplier;
    public float SplitLifetimeMultiplier;
}

/// <summary>
/// Holds baked runtime configuration for the Explosion passive tool.
/// </summary>
public struct ExplosionPassiveConfig
{
    public PassiveExplosionTriggerMode TriggerMode;
    public float CooldownSeconds;
    public float Radius;
    public float Damage;
    public byte AffectAllEnemiesInRadius;
    public float3 TriggerOffset;
    public Entity ExplosionVfxPrefabEntity;
    public byte ScaleVfxToRadius;
    public float VfxScaleMultiplier;
}

/// <summary>
/// Holds baked runtime configuration for the Elemental Trail passive tool.
/// </summary>
public struct ElementalTrailPassiveConfig
{
    public ElementalEffectConfig Effect;
    public float TrailSegmentLifetimeSeconds;
    public float TrailSpawnDistance;
    public float TrailSpawnIntervalSeconds;
    public float TrailRadius;
    public int MaxActiveSegmentsPerPlayer;
    public float StacksPerTick;
    public float ApplyIntervalSeconds;
    public Entity TrailAttachedVfxPrefabEntity;
    public float TrailAttachedVfxScaleMultiplier;
    public float3 TrailAttachedVfxOffset;
}
#endregion
