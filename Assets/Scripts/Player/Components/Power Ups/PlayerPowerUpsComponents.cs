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
    public byte HasElementalProjectiles;
    public ElementalProjectilesPassiveConfig ElementalProjectiles;
    public byte HasPerfectCircle;
    public PerfectCirclePassiveConfig PerfectCircle;
    public byte HasBouncingProjectiles;
    public BouncingProjectilesPassiveConfig BouncingProjectiles;
    public byte HasSplittingProjectiles;
    public SplittingProjectilesPassiveConfig SplittingProjectiles;
    public byte HasExplosion;
    public ExplosionPassiveConfig Explosion;
    public byte HasElementalTrail;
    public ElementalTrailPassiveConfig ElementalTrail;
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
/// Holds runtime state for the Bullet Time active tool.
/// </summary>
public struct PlayerBulletTimeState : IComponentData
{
    public float RemainingDuration;
    public float SlowPercent;
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

/// <summary>
/// Holds runtime timers for passive explosion logic.
/// </summary>
public struct PlayerPassiveExplosionState : IComponentData
{
    public float CooldownRemaining;
    public float PreviousObservedHealth;
}

/// <summary>
/// Stores runtime trail spawning state for elemental trail passive.
/// </summary>
public struct PlayerElementalTrailState : IComponentData
{
    public float3 LastSpawnPosition;
    public float SpawnTimer;
    public int ActiveSegments;
    public byte Initialized;
}

/// <summary>
/// Stores runtime handle of the attached elemental trail VFX entity.
/// </summary>
public struct PlayerElementalTrailAttachedVfxState : IComponentData
{
    public Entity VfxEntity;
    public Entity PrefabEntity;
}

/// <summary>
/// Tracks trail segment entities currently owned by one player.
/// </summary>
public struct PlayerElementalTrailSegmentElement : IBufferElementData
{
    public Entity SegmentEntity;
}

/// <summary>
/// Runtime payload of one elemental trail segment entity.
/// </summary>
public struct ElementalTrailSegment : IComponentData
{
    public Entity OwnerEntity;
    public float Radius;
    public float RemainingLifetime;
    public float ApplyIntervalSeconds;
    public float ApplyTimer;
    public float StacksPerTick;
    public ElementalEffectConfig Effect;
}

/// <summary>
/// Request to apply an explosion payload at a specific world position.
/// </summary>
public struct PlayerExplosionRequest : IBufferElementData
{
    public float3 Position;
    public float Radius;
    public float Damage;
    public byte AffectAllEnemiesInRadius;
    public Entity ExplosionVfxPrefabEntity;
    public byte ScaleVfxToRadius;
    public float VfxScaleMultiplier;
}

/// <summary>
/// Request to spawn one-shot VFX entities for passive/elemental feedback.
/// </summary>
public struct PlayerPowerUpVfxSpawnRequest : IBufferElementData
{
    public Entity PrefabEntity;
    public float3 Position;
    public quaternion Rotation;
    public float UniformScale;
    public float LifetimeSeconds;
    public Entity FollowTargetEntity;
    public float3 FollowPositionOffset;
    public Entity FollowValidationEntity;
    public uint FollowValidationSpawnVersion;
    public float3 Velocity;
}

/// <summary>
/// Pool slot containing one reusable VFX entity instance.
/// </summary>
public struct PlayerPowerUpVfxPoolElement : IBufferElementData
{
    public Entity PrefabEntity;
    public Entity VfxEntity;
}

/// <summary>
/// Lifetime tracker for temporary spawned VFX entities.
/// </summary>
public struct PlayerPowerUpVfxLifetime : IComponentData
{
    public float RemainingSeconds;
}

/// <summary>
/// Makes a spawned VFX follow a target entity using LocalTransform.
/// </summary>
public struct PlayerPowerUpVfxFollowTarget : IComponentData
{
    public Entity TargetEntity;
    public float3 PositionOffset;
    public Entity ValidationEntity;
    public uint ValidationSpawnVersion;
}

/// <summary>
/// Moves a spawned VFX with a constant velocity while alive.
/// </summary>
public struct PlayerPowerUpVfxVelocity : IComponentData
{
    public float3 Value;
}

/// <summary>
/// Marks VFX entities managed by the pooled VFX pipeline.
/// </summary>
public struct PlayerPowerUpVfxPooled : IComponentData
{
}
