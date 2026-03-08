using Unity.Entities;

#region Root Blob
/// <summary>
/// Holds progression configuration data used to initialize level-up thresholds and scalable runtime stats.
/// </summary>
public struct PlayerProgressionConfigBlob
{
    public float ExperiencePickupRadius;
    public BlobArray<PlayerGamePhaseBlob> GamePhases;
    public BlobArray<PlayerScalableStatBlob> ScalableStats;
}
#endregion

#region Game Phases
/// <summary>
/// Stores one runtime game phase used to resolve level-up experience requirements.
/// </summary>
public struct PlayerGamePhaseBlob
{
    public BlobString PhaseID;
    public int StartsAtLevel;
    public float StartingRequiredLevelUpExp;
    public float RequiredExperienceGrouth;
    public BlobArray<PlayerLevelUpMilestoneBlob> Milestones;
}
#endregion

#region Milestones
/// <summary>
/// Stores one milestone override for the required experience of a specific player level.
/// </summary>
public struct PlayerLevelUpMilestoneBlob
{
    public int MilestoneLevel;
    public float SpecialExpRequirement;
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
