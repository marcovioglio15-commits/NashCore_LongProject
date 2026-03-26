using Unity.Entities;

/// <summary>
/// Declares the scroll direction used by the run timer.
/// /params none.
/// /returns none.
/// </summary>
public enum PlayerRunTimerDirection : byte
{
    Forward = 0,
    Backward = 1
}

/// <summary>
/// Stores the authoritative run-timer configuration bound to the local player entity.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRunTimerConfig : IComponentData
{
    #region Fields
    public PlayerRunTimerDirection Direction;
    public float InitialSeconds;
    #endregion
}

/// <summary>
/// Stores mutable runtime state for the local run timer.
/// /params none.
/// /returns none.
/// </summary>
public struct PlayerRunTimerState : IComponentData
{
    #region Fields
    public float CurrentSeconds;
    public byte Expired;
    #endregion
}
