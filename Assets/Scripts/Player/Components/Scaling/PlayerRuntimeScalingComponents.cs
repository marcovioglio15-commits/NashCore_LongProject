using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#region Controller Runtime Components
/// <summary>
/// Stores the current runtime movement config after scalable-stat formulas are resolved.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimeMovementConfig : IComponentData
{
    public MovementDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public ReferenceFrame MovementReference;
    public MovementValuesBlob Values;
}

/// <summary>
/// Stores the immutable baseline movement config used to rebuild runtime-scaled values.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerBaseMovementConfig : IComponentData
{
    public MovementDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public ReferenceFrame MovementReference;
    public MovementValuesBlob Values;
}

/// <summary>
/// Stores the current runtime look config after scalable-stat formulas are resolved.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimeLookConfig : IComponentData
{
    public LookDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public RotationMode RotationMode;
    public float RotationSpeed;
    public LookMultiplierSampling MultiplierSampling;
    public ConeConfig FrontCone;
    public ConeConfig BackCone;
    public ConeConfig LeftCone;
    public ConeConfig RightCone;
    public LookValuesBlob Values;
}

/// <summary>
/// Stores the immutable baseline look config used to rebuild runtime-scaled values.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerBaseLookConfig : IComponentData
{
    public LookDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public RotationMode RotationMode;
    public float RotationSpeed;
    public LookMultiplierSampling MultiplierSampling;
    public ConeConfig FrontCone;
    public ConeConfig BackCone;
    public ConeConfig LeftCone;
    public ConeConfig RightCone;
    public LookValuesBlob Values;
}

/// <summary>
/// Stores the current runtime camera config after scalable-stat formulas are resolved.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimeCameraConfig : IComponentData
{
    public CameraBehavior Behavior;
    public float3 FollowOffset;
    public CameraValuesBlob Values;
}

/// <summary>
/// Stores the immutable baseline camera config used to rebuild runtime-scaled values.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerBaseCameraConfig : IComponentData
{
    public CameraBehavior Behavior;
    public float3 FollowOffset;
    public CameraValuesBlob Values;
}

/// <summary>
/// Stores the current runtime shooting config after scalable-stat formulas are resolved.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimeShootingConfig : IComponentData
{
    public ShootingTriggerMode TriggerMode;
    public byte ProjectilesInheritPlayerSpeed;
    public float3 ShootOffset;
    public ShootingValuesBlob Values;
}

/// <summary>
/// Stores the immutable baseline shooting config used to rebuild runtime-scaled values.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerBaseShootingConfig : IComponentData
{
    public ShootingTriggerMode TriggerMode;
    public byte ProjectilesInheritPlayerSpeed;
    public float3 ShootOffset;
    public ShootingValuesBlob Values;
}

/// <summary>
/// Stores current runtime health/shield limits after scalable-stat formulas are resolved.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimeHealthStatisticsConfig : IComponentData
{
    public float MaxHealth;
    public float MaxShield;
}

/// <summary>
/// Stores immutable baseline health/shield limits used to rebuild runtime-scaled values.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerBaseHealthStatisticsConfig : IComponentData
{
    public float MaxHealth;
    public float MaxShield;
}
#endregion

#region Progression Runtime Components
/// <summary>
/// Stores one immutable baseline phase used to rebuild runtime progression requirements.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerBaseGamePhaseElement : IBufferElementData
{
    public int StartsAtLevel;
    public float StartingRequiredLevelUpExp;
    public float RequiredExperienceGrouth;
}

/// <summary>
/// Stores one current runtime phase after scalable-stat formulas are resolved.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimeGamePhaseElement : IBufferElementData
{
    public int StartsAtLevel;
    public float StartingRequiredLevelUpExp;
    public float RequiredExperienceGrouth;
}
#endregion

#region Power-Up Runtime Components
/// <summary>
/// Stores immutable base active/passive configs for one modular power-up entry.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerPowerUpBaseConfigElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public PlayerPowerUpUnlockKind UnlockKind;
    public PlayerPowerUpSlotConfig ActiveSlotConfig;
    public PlayerPassiveToolConfig PassiveToolConfig;
}
#endregion

#region Runtime Scaling State
/// <summary>
/// Tracks the last scalable-stat hash synchronized into runtime-scaled configs.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimeScalingState : IComponentData
{
    public uint LastScalableStatsHash;
    public byte Initialized;
}
#endregion

#region Scaling Metadata
/// <summary>
/// Identifies one controller runtime field that can be rewritten from a scaling formula.
/// /params none.
/// /returns none.
/// </summary>
public enum PlayerRuntimeControllerFieldId : byte
{
    MovementDiscreteDirectionCount = 0,
    MovementDirectionOffsetDegrees = 1,
    MovementBaseSpeed = 2,
    MovementMaxSpeed = 3,
    MovementAcceleration = 4,
    MovementDeceleration = 5,
    MovementOppositeDirectionBrakeMultiplier = 6,
    MovementWallBounceCoefficient = 7,
    MovementWallCollisionSkinWidth = 8,
    MovementInputDeadZone = 9,
    MovementDigitalReleaseGraceSeconds = 10,
    LookDiscreteDirectionCount = 11,
    LookDirectionOffsetDegrees = 12,
    LookRotationSpeed = 13,
    LookFrontConeAngleDegrees = 14,
    LookFrontConeMaxSpeedMultiplier = 15,
    LookFrontConeAccelerationMultiplier = 16,
    LookBackConeAngleDegrees = 17,
    LookBackConeMaxSpeedMultiplier = 18,
    LookBackConeAccelerationMultiplier = 19,
    LookLeftConeAngleDegrees = 20,
    LookLeftConeMaxSpeedMultiplier = 21,
    LookLeftConeAccelerationMultiplier = 22,
    LookRightConeAngleDegrees = 23,
    LookRightConeMaxSpeedMultiplier = 24,
    LookRightConeAccelerationMultiplier = 25,
    LookRotationDamping = 26,
    LookRotationMaxSpeed = 27,
    LookRotationDeadZone = 28,
    LookDigitalReleaseGraceSeconds = 29,
    CameraFollowOffsetX = 30,
    CameraFollowOffsetY = 31,
    CameraFollowOffsetZ = 32,
    CameraFollowSpeed = 33,
    CameraLag = 34,
    CameraDamping = 35,
    CameraMaxFollowDistance = 36,
    CameraDeadZoneRadius = 37,
    ShootingShootOffsetX = 38,
    ShootingShootOffsetY = 39,
    ShootingShootOffsetZ = 40,
    ShootingShootSpeed = 41,
    ShootingRateOfFire = 42,
    ShootingProjectileSizeMultiplier = 43,
    ShootingExplosionRadius = 44,
    ShootingRange = 45,
    ShootingLifetime = 46,
    ShootingDamage = 47,
    ShootingMaxPenetrations = 48,
    HealthMaxHealth = 49,
    HealthMaxShield = 50
}

/// <summary>
/// Stores one controller scaling entry baked from Add Scaling authoring data.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimeControllerScalingElement : IBufferElementData
{
    public PlayerRuntimeControllerFieldId FieldId;
    public float BaseValue;
    public byte IsInteger;
    public FixedString512Bytes Formula;
}

/// <summary>
/// Identifies one progression runtime field that can be rewritten from a scaling formula.
/// /params none.
/// /returns none.
/// </summary>
public enum PlayerRuntimeProgressionFieldId : byte
{
    PhaseStartingRequiredLevelUpExp = 0,
    PhaseRequiredExperienceGrouth = 1
}

/// <summary>
/// Stores one progression scaling entry baked from Add Scaling authoring data.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimeProgressionScalingElement : IBufferElementData
{
    public int PhaseIndex;
    public PlayerRuntimeProgressionFieldId FieldId;
    public float BaseValue;
    public byte IsInteger;
    public FixedString512Bytes Formula;
}

/// <summary>
/// Stores one power-up scaling entry baked from Add Scaling authoring data.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRuntimePowerUpScalingElement : IBufferElementData
{
    public FixedString64Bytes PowerUpId;
    public PlayerPowerUpUnlockKind UnlockKind;
    public FixedString128Bytes PayloadPath;
    public float BaseValue;
    public byte IsInteger;
    public FixedString512Bytes Formula;
}
#endregion
