using Unity.Entities;

#region Components
/// <summary>
/// Holds a reference to the player progression configuration blob asset.
/// </summary>
public struct PlayerProgressionConfig : IComponentData
{
    public BlobAssetReference<PlayerProgressionConfigBlob> Config;
}
#endregion
