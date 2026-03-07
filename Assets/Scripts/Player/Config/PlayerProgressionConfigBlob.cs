using Unity.Entities;

#region Root Blob
/// <summary>
/// Holds progression configuration data used to initialize scalable runtime stats.
/// </summary>
public struct PlayerProgressionConfigBlob
{
    public BlobArray<PlayerScalableStatBlob> ScalableStats;
}
#endregion

#region Scalable Stats
/// <summary>
/// Stores one scalable stat entry baked from progression presets.
/// </summary>
public struct PlayerScalableStatBlob
{
    public BlobString Name;
    public byte Type;
    public float DefaultValue;
}
#endregion
