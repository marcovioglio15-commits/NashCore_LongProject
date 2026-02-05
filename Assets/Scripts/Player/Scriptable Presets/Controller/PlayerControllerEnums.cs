using System;

#region Enums
[Serializable]
public enum ReferenceFrame
{
    PlayerForward = 0,
    WorldForward = 1,
    CameraForward = 2
}

[Serializable]
public enum MovementDirectionsMode
{
    AllDirections = 0,
    DiscreteCount = 1
}

[Serializable]
public enum LookDirectionsMode
{
    AllDirections = 0,
    DiscreteCount = 1,
    Cones = 2,
    FollowMovementDirection = 3
}

[Serializable]
public enum RotationMode
{
    SnapToAllowedDirections = 0,
    Continuous = 1
}

[Serializable]
public enum LookMultiplierSampling
{
    DirectionalBlend = 0,
    ArcConstant = 1
}

[Serializable]
public enum CameraBehavior
{
    ChildOfPlayer = 0,
    FollowWithOffset = 1,
    FollowWithAutoOffset = 2,
    RoomFixed = 3
}
#endregion
