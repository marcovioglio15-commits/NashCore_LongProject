using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Stores the current combo value, damage observations, time-decay carry, and presentation data resolved from the active combo rank.
/// none.
/// returns none.
/// </summary>
public struct PlayerComboCounterState : IComponentData
{
    public int CurrentValue;
    public int CurrentRankIndex;
    public int CurrentRankRequiredValue;
    public int NextRankRequiredValue;
    public float ProgressNormalized;
    public float DecayPointsCarry;
    public float GainPointsCarry;
    public float PreviousObservedHealth;
    public float PreviousObservedShield;
    public FixedString64Bytes CurrentRankId;
    public byte Initialized;
}
