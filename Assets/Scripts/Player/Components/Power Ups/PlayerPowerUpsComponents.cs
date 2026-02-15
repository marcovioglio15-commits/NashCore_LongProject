using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Holds runtime state for power-up slots and activation inputs.
/// </summary>
public struct PlayerPowerUpsState : IComponentData
{
    public float PrimaryEnergy;
    public float SecondaryEnergy;
    public byte PreviousPrimaryPressed;
    public byte PreviousSecondaryPressed;
    public uint LastObservedGlobalKillCount;
    public float3 LastValidMovementDirection;
}

/// <summary>
/// Holds aggregated runtime multipliers from equipped passive tools.
/// </summary>
public struct PlayerPassiveToolsState : IComponentData
{
    public float ProjectileSizeMultiplier;
    public float ProjectileDamageMultiplier;
    public float ProjectileSpeedMultiplier;
    public float ProjectileLifetimeSecondsMultiplier;
    public float ProjectileLifetimeRangeMultiplier;
}

/// <summary>
/// Holds runtime dash motion and invulnerability state.
/// </summary>
public struct PlayerDashState : IComponentData
{
    public byte IsDashing;
    public byte Phase;
    public float PhaseRemaining;
    public float HoldDuration;
    public float RemainingInvulnerability;
    public float3 Direction;
    public float3 EntryVelocity;
    public float Speed;
    public float TransitionInDuration;
    public float TransitionOutDuration;
}

/// <summary>
/// Enqueued request to spawn a bomb entity for delayed explosion.
/// </summary>
public struct PlayerBombSpawnRequest : IBufferElementData
{
    public Entity BombPrefabEntity;
    public float3 Position;
    public quaternion Rotation;
    public float3 Velocity;
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
/// Runtime fuse state for spawned bomb entities.
/// </summary>
public struct BombFuseState : IComponentData
{
    public float3 Position;
    public float3 Velocity;
    public float CollisionRadius;
    public byte BounceOnWalls;
    public float BounceDamping;
    public float LinearDampingPerSecond;
    public float FuseRemaining;
    public float Radius;
    public float Damage;
    public byte AffectAllEnemiesInRadius;
}

/// <summary>
/// Marks bombs that must execute explosion logic this frame.
/// </summary>
public struct BombExplodeRequest : IComponentData
{
}
