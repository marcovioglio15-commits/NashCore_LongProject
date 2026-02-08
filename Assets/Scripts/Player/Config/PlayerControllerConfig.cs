using Unity.Entities;

#region Components
/// <summary>
/// This component holds a reference to the player controller configuration blob asset,
/// which contains all the necessary configuration data for the player controller,
/// such as input action IDs, movement parameters, shooting parameters, ecc.
/// </summary>
public struct PlayerControllerConfig : IComponentData
{
    public BlobAssetReference<PlayerControllerConfigBlob> Config;
}

/// <summary>
/// This component holds a reference to the player camera anchor entity,
/// which is used to determine the position and orientation of the camera relative to the player entity.
/// </summary>
public struct PlayerCameraAnchor : IComponentData
{
    public Entity AnchorEntity;
}
#endregion
