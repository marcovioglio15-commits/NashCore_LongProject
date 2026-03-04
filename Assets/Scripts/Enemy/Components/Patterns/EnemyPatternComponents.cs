using Unity.Entities;
using Unity.Mathematics;

#region Pattern Components
/// <summary>
/// Stores compiled movement pattern configuration used at runtime.
/// </summary>
public struct EnemyPatternConfig : IComponentData
{
    public EnemyCompiledMovementPatternKind MovementKind;
    public byte StationaryFreezeRotation;
    public float BasicSearchRadius;
    public float BasicMinimumTravelDistance;
    public float BasicMaximumTravelDistance;
    public float BasicArrivalTolerance;
    public float BasicWaitCooldownSeconds;
    public int BasicCandidateSampleCount;
    public byte BasicUseInfiniteDirectionSampling;
    public float BasicInfiniteDirectionStepDegrees;
    public float BasicUnexploredDirectionPreference;
    public float BasicTowardPlayerPreference;
    public float BasicMinimumWallDistance;
    public float BasicMinimumEnemyClearance;
    public float BasicTrajectoryPredictionTime;
    public float BasicFreeTrajectoryPreference;
    public float BasicBlockedPathRetryDelay;
    public float DvdSpeedMultiplier;
    public float DvdBounceDamping;
    public byte DvdRandomizeInitialDirection;
    public float DvdFixedInitialDirectionDegrees;
    public float DvdCornerNudgeDistance;
}

/// <summary>
/// Stores mutable runtime state used by custom movement patterns.
/// </summary>
public struct EnemyPatternRuntimeState : IComponentData
{
    public float3 WanderTargetPosition;
    public float WanderWaitTimer;
    public float WanderRetryTimer;
    public float LastWanderDirectionAngle;
    public byte WanderHasTarget;
    public byte WanderInitialized;
    public float3 DvdDirection;
    public byte DvdInitialized;
}

/// <summary>
/// Runtime state that allows Shooter modules to temporarily lock movement.
/// </summary>
public struct EnemyShooterControlState : IComponentData
{
    public byte MovementLocked;
}

/// <summary>
/// Runtime shooter module configuration compiled from active patterns.
/// </summary>
public struct EnemyShooterConfigElement : IBufferElementData
{
    public EnemyShooterAimPolicy AimPolicy;
    public EnemyShooterMovementPolicy MovementPolicy;
    public float FireInterval;
    public int BurstCount;
    public float IntraBurstDelay;
    public byte UseMinimumRange;
    public float MinimumRange;
    public byte UseMaximumRange;
    public float MaximumRange;
    public int ProjectilesPerShot;
    public float SpreadAngleDegrees;
    public float ProjectileSpeed;
    public float ProjectileDamage;
    public float ProjectileRange;
    public float ProjectileLifetime;
    public float ProjectileExplosionRadius;
    public float ProjectileScaleMultiplier;
    public ProjectilePenetrationMode PenetrationMode;
    public int MaxPenetrations;
    public byte InheritShooterSpeed;
    public byte HasElementalPayload;
    public ElementalEffectConfig ElementalEffect;
    public float ElementalStacksPerHit;
}

/// <summary>
/// Mutable runtime shooter module state.
/// </summary>
public struct EnemyShooterRuntimeElement : IBufferElementData
{
    public float NextBurstTimer;
    public float NextShotInBurstTimer;
    public int RemainingBurstShots;
    public float3 LockedAimDirection;
    public byte HasLockedAimDirection;
}

/// <summary>
/// Tags enemies whose movement is driven by custom pattern systems instead of default steering.
/// </summary>
public struct EnemyCustomPatternMovementTag : IComponentData
{
}
#endregion
