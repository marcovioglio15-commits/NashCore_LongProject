using Unity.Entities;

#region Components
public struct PlayerControllerConfig : IComponentData
{
    public BlobAssetReference<PlayerControllerConfigBlob> Config;
}

public struct PlayerCameraAnchor : IComponentData
{
    public Entity AnchorEntity;
}
#endregion
