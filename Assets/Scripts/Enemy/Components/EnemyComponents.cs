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
    public float SeparationRadius;
    public float SeparationWeight;
    public float BodyRadius;
    public float ContactRadius;
    public float ContactDamage;
    public float ContactInterval;
}

/// <summary>
/// Stores runtime enemy health values.
/// </summary>
public struct EnemyHealth : IComponentData
{
    public float Current;
    public float Max;
}

/// <summary>
/// Stores mutable runtime state for enemy simulation.
/// </summary>
public struct EnemyRuntimeState : IComponentData
{
    public float3 Velocity;
    public float ContactCooldown;
    public uint SpawnVersion;
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
