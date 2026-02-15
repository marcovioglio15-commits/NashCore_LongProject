using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// This component represents the shooting state of a player entity, 
/// including whether automatic shooting is enabled,
/// </summary>
public struct PlayerShootingState : IComponentData
{
    public byte AutomaticEnabled;
    public byte PreviousShootPressed;
    public float NextShotTime;
}

/// <summary>
/// This component holds a reference to the projectile prefab entity 
/// that the shooter will instantiate when shooting.
/// </summary>
public struct ShooterProjectilePrefab : IComponentData
{
    public Entity PrefabEntity;
}

/// <summary>
/// This component holds a reference to the muzzle anchor entity,
/// which is the point from which projectiles will be spawned when the shooter fires.
/// </summary>
public struct ShooterMuzzleAnchor : IComponentData
{
    public Entity AnchorEntity;
}

/// <summary>
/// This component represents the state of the projectile pool for a shooter entity,
/// which is used to manage a pool of projectile entities for efficient shooting 
/// without runtime instantiation overhead.
/// </summary>
public struct ProjectilePoolState : IComponentData
{
    public int InitialCapacity;
    public int ExpandBatch;
    public byte Initialized;
}

/// <summary>
/// This component represents a shoot request, which is created when a player entity initiates a shooting action.
/// </summary>
public struct ShootRequest : IBufferElementData
{
    public float3 Position;
    public float3 Direction;
    public float Speed;
    public float Range;
    public float Lifetime;
    public float Damage;
    public float ProjectileScaleMultiplier;
    public byte InheritPlayerSpeed;
    public byte IsSplitChild;
}

public struct ProjectilePoolElement : IBufferElementData
{
    public Entity ProjectileEntity;
}

/// <summary>
/// This component represents a projectile entity, 
/// which includes its velocity, damage, maximum range, lifetime, 
/// and whether it inherits the player's speed when spawned (Hoctagon style).
/// </summary>
public struct Projectile : IComponentData
{
    public float3 Velocity;
    public float Damage;
    public float MaxRange;
    public float MaxLifetime;
    public byte InheritPlayerSpeed;
}

/// <summary>
/// This component represents the runtime state of a projectile entity,
/// which includes the distance it has traveled and the elapsed time since it was spawned.
/// </summary>
public struct ProjectileRuntimeState : IComponentData
{
    public float TraveledDistance;
    public float ElapsedLifetime;
}

/// <summary>
/// This component holds a reference to the shooter entity that owns a projectile,
/// which is used to associate projectiles with their source shooter for 
/// applying player modifiers to projectile behavior 
/// or handling interactions between projectiles and the shooter (e.g., avoiding self-collision).
/// </summary>
public struct ProjectileOwner : IComponentData
{
    public Entity ShooterEntity;
}

/// <summary>
/// This component is used to mark a projectile entity as active, indicating that it is currently 
/// in flight and should be processed by the projectile movement and collision systems.
/// </summary>
public struct ProjectileActive : IComponentData, IEnableableComponent
{
}

/// <summary>
/// Stores the projectile prefab base scale so runtime modifiers can be reapplied without cumulative drift.
/// </summary>
public struct ProjectileBaseScale : IComponentData
{
    public float Value;
}

/// <summary>
/// Runtime state used by the Perfect Circle passive to move projectiles around the player.
/// </summary>
public struct ProjectilePerfectCircleState : IComponentData
{
    public byte Enabled;
    public byte HasEnteredOrbit;
    public byte CompletedFullOrbit;
    public float3 EntryOrigin;
    public float OrbitAngle;
    public float OrbitBlendProgress;
    public float CurrentRadius;
    public float AccumulatedOrbitRadians;
    public float3 RadialDirection;
    public float3 EntryVelocity;
}

/// <summary>
/// Runtime state used by bouncing projectiles to keep bounce counters and speed scaling.
/// </summary>
public struct ProjectileBounceState : IComponentData
{
    public int RemainingBounces;
    public float SpeedPercentChangePerBounce;
    public float MinimumSpeedMultiplierAfterBounce;
    public float MaximumSpeedMultiplierAfterBounce;
    public float CurrentSpeedMultiplier;
}

/// <summary>
/// Runtime split payload stored on original projectiles.
/// </summary>
public struct ProjectileSplitState : IComponentData
{
    public byte CanSplit;
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
/// Runtime elemental payload carried by projectiles.
/// </summary>
public struct ProjectileElementalPayload : IComponentData
{
    public byte Enabled;
    public ElementalEffectConfig Effect;
    public float StacksPerHit;
    public byte SpawnStackVfx;
    public Entity StackVfxPrefabEntity;
    public float StackVfxScaleMultiplier;
    public byte SpawnProcVfx;
    public Entity ProcVfxPrefabEntity;
    public float ProcVfxScaleMultiplier;
}
