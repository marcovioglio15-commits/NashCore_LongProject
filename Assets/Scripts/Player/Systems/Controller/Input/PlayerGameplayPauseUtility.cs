using UnityEngine;

/// <summary>
/// Centralizes the hard-pause check used by gameplay ECS systems that must freeze their mutable runtime state while UI owns the simulation.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerGameplayPauseUtility
{
    #region Constants
    private const float HardPauseTimeScaleThreshold = 0.0001f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether gameplay is currently under a hard pause driven by UI or end-of-run flows.
    /// /params None.
    /// /returns True when simulation-facing gameplay state must remain frozen for the current frame.
    /// </summary>
    public static bool IsHardGameplayPauseActive()
    {
        return Time.timeScale <= HardPauseTimeScaleThreshold;
    }
    #endregion

    #endregion
}
