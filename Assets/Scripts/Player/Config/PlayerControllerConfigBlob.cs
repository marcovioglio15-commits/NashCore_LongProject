using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#region Root Blob
/// <summary>
/// Holds all configuration data for the player controller, 
/// including movement, looking direction, camera behavior, and shooting mechanics.
/// </summary>
public struct PlayerControllerConfigBlob
{
    public MovementConfig Movement;
    public LookConfig Look;
    public CameraConfig Camera;
    public ShootingConfig Shooting;
}
#endregion

#region Movement
/// <summary>
/// Holds configuration data related to player movement, including direction modes, 
/// reference frames, and movement values such as speed, acceleration, and input dead zones.
/// 
/// </summary>
public struct MovementConfig
{
    public MovementDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public ReferenceFrame MovementReference;
    public MovementValuesBlob Values;
}

/// <summary>
/// This structure holds movement-related values such as base speed, max speed, acceleration,
/// deceleration, input dead zone, and digital release grace time.
/// </summary>
public struct MovementValuesBlob
{
    public float BaseSpeed;
    public float MaxSpeed;
    public float Acceleration;
    public float Deceleration;
    public float InputDeadZone;
    public float DigitalReleaseGraceSeconds;
}
#endregion

#region Shooting
/// <summary>
/// This structure holds configuration data related to player shooting mechanics, 
/// including trigger modes, projectile behavior, shooting offsets, 
/// and values such as shoot speed, rate of fire, range, lifetime, and damage.
/// </summary>
public struct ShootingConfig
{
    public ShootingTriggerMode TriggerMode;
    public byte ProjectilesInheritPlayerSpeed;
    public float3 ShootOffset;
    public ShootingValuesBlob Values;
}

/// <summary>
/// Holds shooting-related values such as shoot speed, 
/// rate of fire, range, lifetime, and damage for projectiles.
/// </summary>
public struct ShootingValuesBlob
{
    public float ShootSpeed;
    public float RateOfFire;
    public float Range;
    public float Lifetime;
    public float Damage;
}
#endregion

#region Look
/// <summary>
/// Holds configuration data related to player looking direction, 
/// including direction modes, rotation modes, 
/// speed multipliers based on look direction, and values such as rotation speed, 
/// damping, and dead zones.
/// </summary>
public struct LookConfig
{
    public LookDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public RotationMode RotationMode;
    public float RotationSpeed;
    public LookMultiplierSampling MultiplierSampling;
    public BlobArray<float> DiscreteMaxSpeedMultipliers;
    public BlobArray<float> DiscreteAccelerationMultipliers;
    public ConeConfig FrontCone;
    public ConeConfig BackCone;
    public ConeConfig LeftCone;
    public ConeConfig RightCone;
    public LookValuesBlob Values;
}

/// <summary>
/// Holds look-related values such as rotation speed, damping, max speed, 
/// input dead zones, and digital release grace time for look direction stabilization. 
/// </summary>
public struct LookValuesBlob
{
    public float RotationDamping;
    public float RotationMaxSpeed;
    public float RotationDeadZone;
    public float DigitalReleaseGraceSeconds;
}

/// <summary>
/// Holds configuration for a directional cone used in look direction 
/// speed multipliers, including whether the cone is enabled, its angle in degrees, 
/// and the max speed and acceleration multipliers applied when the look direction falls within the cone. 
/// </summary>
public struct ConeConfig
{
    public bool Enabled;
    public float AngleDegrees;
    public float MaxSpeedMultiplier;
    public float AccelerationMultiplier;
}
#endregion

#region Camera
/// <summary>
/// Holds configuration data related to camera behavior, 
/// including the camera follow behavior, offset from the player, 
/// and values such as follow speed, camera lag, damping,
/// </summary>
public struct CameraConfig
{
    public CameraBehavior Behavior;
    public float3 FollowOffset;
    public CameraValuesBlob Values;
}

/// <summary>
/// Holds camera-related values such as follow speed, camera lag, 
/// damping, max follow distance, and dead zone radius for camera movement.
/// </summary>
public struct CameraValuesBlob
{
    public float FollowSpeed;
    public float CameraLag;
    public float Damping;
    public float MaxFollowDistance;
    public float DeadZoneRadius;
}
#endregion
