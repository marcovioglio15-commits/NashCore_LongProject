#region Root Blob
/// <summary>
/// Holds progression configuration data for player initialization.
/// </summary>
public struct PlayerProgressionConfigBlob
{
    public PlayerProgressionBaseStatsBlob BaseStats;
}
#endregion

#region Base Stats
/// <summary>
/// Holds baseline player progression stats.
/// </summary>
public struct PlayerProgressionBaseStatsBlob
{
    public float Health;
    public float Experience;
}
#endregion
