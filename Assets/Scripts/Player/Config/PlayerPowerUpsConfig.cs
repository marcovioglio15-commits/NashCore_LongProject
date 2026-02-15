using Unity.Entities;
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
/// Holds baked runtime configuration for a single passive-tool slot.
/// </summary>
public struct PlayerPassiveToolConfig
{
    public byte IsDefined;
    public PassiveToolKind ToolKind;
    public ProjectileSizePassiveConfig ProjectileSize;
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
#endregion
