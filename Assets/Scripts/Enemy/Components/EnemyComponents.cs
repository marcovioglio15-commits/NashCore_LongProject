using Unity.Entities;
using Unity.Mathematics;

#region Enemy Components
/// <summary>
/// Stores immutable movement and combat parameters for enemy entities.
/// </summary>
public struct EnemyData : IComponentData
{
    public float MoveSpeed;
    public float MaxSpeed;
    public float Acceleration;
    public float Deceleration;
    public float RotationSpeedDegreesPerSecond;
    public float SeparationRadius;
    public float SeparationWeight;
    public float BodyRadius;
    public int PriorityTier;
    public byte ContactDamageEnabled;
    public float ContactRadius;
    public float ContactAmountPerTick;
    public float ContactTickInterval;
    public byte AreaDamageEnabled;
    public float AreaRadius;
    public float AreaAmountPerTickPercent;
    public float AreaTickInterval;
}

/// <summary>
/// Stores runtime enemy health and shield values.
/// </summary>
public struct EnemyHealth : IComponentData
{
    public float Current;
    public float Max;
    public float CurrentShield;
    public float MaxShield;
}

/// <summary>
/// Stores mutable runtime state for enemy simulation.
/// </summary>
public struct EnemyRuntimeState : IComponentData
{
    public float3 Velocity;
    public float ContactDamageCooldown;
    public float AreaDamageCooldown;
    public uint SpawnVersion;
}

/// <summary>
/// Selects which visual runtime path an enemy should use.
/// </summary>
public enum EnemyVisualMode : byte
{
    CompanionAnimator = 0,
    GpuBaked = 1
}

/// <summary>
/// Stores immutable visual settings used by presentation systems.
/// </summary>
public struct EnemyVisualConfig : IComponentData
{
    public EnemyVisualMode Mode;
    public float AnimationSpeed;
    public float GpuLoopDuration;
    public float MaxVisibleDistance;
    public float VisibleDistanceHysteresis;
    public byte UseDistanceCulling;
    public int VisibilityPriorityTier;
}

/// <summary>
/// Stores mutable visual runtime values for culling and animation playback.
/// </summary>
public struct EnemyVisualRuntimeState : IComponentData
{
    public float AnimationTime;
    public float LastDistanceToPlayer;
    public byte IsVisible;
    public byte CompanionInitialized;
    public int AppliedVisibilityPriorityTier;
}

/// <summary>
/// Tags enemies driven by managed companion Animator components.
/// </summary>
public struct EnemyVisualCompanionAnimator : IComponentData
{
}

/// <summary>
/// Tags enemies driven by GPU-baked animation playback data.
/// </summary>
public struct EnemyVisualGpuBaked : IComponentData
{
}

/// <summary>
/// Stores the entity that owns the world-space status bars companion view for an enemy.
/// </summary>
public struct EnemyWorldSpaceStatusBarsLink : IComponentData
{
    public Entity ViewEntity;
}

/// <summary>
/// Caches the resolved world-space status bars view entity instance used at runtime.
/// </summary>
public struct EnemyWorldSpaceStatusBarsRuntimeLink : IComponentData
{
    public Entity ViewEntity;
}

/// <summary>
/// Stores the owner spawner of an enemy entity.
/// </summary>
public struct EnemyOwnerSpawner : IComponentData
{
    public Entity SpawnerEntity;
}

/// <summary>
/// Optional transform anchor used as follow target for attached elemental VFX.
/// </summary>
public struct EnemyElementalVfxAnchor : IComponentData
{
    public Entity AnchorEntity;
}

/// <summary>
/// Marks active pooled enemies that must be simulated.
/// </summary>
public struct EnemyActive : IComponentData, IEnableableComponent
{
}

/// <summary>
/// Reason code for enemy despawn requests.
/// </summary>
public enum EnemyDespawnReason : byte
{
    None = 0,
    Distance = 1,
    Killed = 2
}

/// <summary>
/// Marks an enemy for pooled despawn at the end of the enemy pipeline.
/// </summary>
public struct EnemyDespawnRequest : IComponentData
{
    public EnemyDespawnReason Reason;
}
#endregion

#region Spawner Components
/// <summary>
/// Stores immutable spawn and pool configuration for a spawner.
/// </summary>
public struct EnemySpawner : IComponentData
{
    public Entity EnemyPrefab;
    public int SpawnPerTick;
    public float SpawnInterval;
    public float InitialSpawnDelay;
    public int InitialPoolCapacity;
    public int ExpandBatch;
    public int MaxAliveCount;
    public float SpawnRadius;
    public float SpawnHeightOffset;
    public float DespawnDistance;
}

/// <summary>
/// Stores mutable spawn runtime state.
/// </summary>
public struct EnemySpawnerState : IComponentData
{
    public float NextSpawnTime;
    public int AliveCount;
    public uint RandomState;
    public byte Initialized;
}

/// <summary>
/// Stores pooled enemy entity references per spawner.
/// </summary>
public struct EnemyPoolElement : IBufferElementData
{
    public Entity EnemyEntity;
}
#endregion
