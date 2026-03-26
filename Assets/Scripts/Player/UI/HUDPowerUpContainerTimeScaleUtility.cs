using UnityEngine;

/// <summary>
/// Centralizes unscaled Time.timeScale resume state updates used by dropped-container overlays.
/// </summary>
internal static class HUDPowerUpContainerTimeScaleUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Starts the unscaled Time.timeScale resume state for the overlay.
    ///  isResuming: Mutable flag tracking whether a resume is currently active.
    ///  startTimeScale: Mutable cached start Time.timeScale used for interpolation.
    ///  targetTimeScale: Mutable cached target Time.timeScale used for interpolation.
    ///  durationSeconds: Mutable total resume duration in seconds.
    ///  elapsedSeconds: Mutable elapsed unscaled time since the resume started.
    ///  configuredDurationSeconds: Resume duration requested by the runtime interaction config.
    /// returns void.
    /// </summary>
    public static void BeginResume(ref bool isResuming,
                                   ref float startTimeScale,
                                   ref float targetTimeScale,
                                   ref float durationSeconds,
                                   ref float elapsedSeconds,
                                   float configuredDurationSeconds)
    {
        durationSeconds = Mathf.Max(0f, configuredDurationSeconds);

        if (durationSeconds <= 0f)
        {
            Time.timeScale = 1f;
            StopResume(ref isResuming,
                       ref startTimeScale,
                       ref targetTimeScale,
                       ref durationSeconds,
                       ref elapsedSeconds);
            return;
        }

        startTimeScale = Mathf.Clamp01(Time.timeScale);
        targetTimeScale = 1f;
        elapsedSeconds = 0f;
        isResuming = true;
    }

    /// <summary>
    /// Advances the active Time.timeScale resume and reports whether the interpolation completed.
    ///  isResuming: Mutable flag tracking whether a resume is currently active.
    ///  startTimeScale: Mutable cached start Time.timeScale used for interpolation.
    ///  targetTimeScale: Mutable cached target Time.timeScale used for interpolation.
    ///  durationSeconds: Mutable total resume duration in seconds.
    ///  elapsedSeconds: Mutable elapsed unscaled time since the resume started.
    ///  milestoneSelectionActive: True when another HUD flow must keep the game paused.
    /// returns True when the resume has fully completed or was already inactive.
    /// </summary>
    public static bool UpdateResume(ref bool isResuming,
                                    ref float startTimeScale,
                                    ref float targetTimeScale,
                                    ref float durationSeconds,
                                    ref float elapsedSeconds,
                                    bool milestoneSelectionActive)
    {
        if (!isResuming || milestoneSelectionActive)
            return !isResuming;

        if (durationSeconds <= 0f)
        {
            Time.timeScale = targetTimeScale;
            StopResume(ref isResuming,
                       ref startTimeScale,
                       ref targetTimeScale,
                       ref durationSeconds,
                       ref elapsedSeconds);
            return true;
        }

        elapsedSeconds += Time.unscaledDeltaTime;
        float normalizedProgress = Mathf.Clamp01(elapsedSeconds / durationSeconds);
        Time.timeScale = Mathf.Lerp(startTimeScale, targetTimeScale, normalizedProgress);

        if (normalizedProgress < 1f)
            return false;

        Time.timeScale = targetTimeScale;
        StopResume(ref isResuming,
                   ref startTimeScale,
                   ref targetTimeScale,
                   ref durationSeconds,
                   ref elapsedSeconds);
        return true;
    }

    /// <summary>
    /// Clears the active Time.timeScale resume state.
    ///  isResuming: Mutable flag tracking whether a resume is currently active.
    ///  startTimeScale: Mutable cached start Time.timeScale used for interpolation.
    ///  targetTimeScale: Mutable cached target Time.timeScale used for interpolation.
    ///  durationSeconds: Mutable total resume duration in seconds.
    ///  elapsedSeconds: Mutable elapsed unscaled time since the resume started.
    /// returns void.
    /// </summary>
    public static void StopResume(ref bool isResuming,
                                  ref float startTimeScale,
                                  ref float targetTimeScale,
                                  ref float durationSeconds,
                                  ref float elapsedSeconds)
    {
        isResuming = false;
        startTimeScale = 0f;
        targetTimeScale = 1f;
        durationSeconds = 0f;
        elapsedSeconds = 0f;
    }
    #endregion

    #endregion
}
