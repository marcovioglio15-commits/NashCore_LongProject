using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#region Root Blob
public struct PlayerControllerConfigBlob
{
    public MovementConfig Movement;
    public LookConfig Look;
    public CameraConfig Camera;
}
#endregion

#region Movement
public struct MovementConfig
{
    public MovementDirectionsMode DirectionsMode;
    public int DiscreteDirectionCount;
    public float DirectionOffsetDegrees;
    public ReferenceFrame MovementReference;
    public MovementValuesBlob Values;
}

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

#region Look
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

public struct LookValuesBlob
{
    public float RotationDamping;
    public float RotationMaxSpeed;
    public float RotationDeadZone;
    public float DigitalReleaseGraceSeconds;
}

public struct ConeConfig
{
    public bool Enabled;
    public float AngleDegrees;
    public float MaxSpeedMultiplier;
    public float AccelerationMultiplier;
}
#endregion

#region Camera
public struct CameraConfig
{
    public CameraBehavior Behavior;
    public float3 FollowOffset;
    public CameraValuesBlob Values;
}

public struct CameraValuesBlob
{
    public float FollowSpeed;
    public float CameraLag;
    public float Damping;
    public float MaxFollowDistance;
    public float DeadZoneRadius;
}
#endregion
