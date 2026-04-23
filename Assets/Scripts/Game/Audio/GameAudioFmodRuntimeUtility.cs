using Unity.Mathematics;
using UnityEngine;

#if NASHCORE_FMOD
using FMOD;
using FMOD.Studio;
using FMODUnity;
#endif

/// <summary>
/// Dispatches runtime audio events through FMOD when the NASHCORE_FMOD scripting define is enabled.
/// /params None.
/// /returns None.
/// </summary>
public static class GameAudioFmodRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Plays one authored FMOD event path as a one-shot sound.
    /// /params eventPath FMOD event path resolved from the Audio Manager preset.
    /// /params position World-space playback position.
    /// /params hasPosition True when the event should receive 3D attributes.
    /// /params volume Playback volume after binding and global multipliers.
    /// /params pitch Playback pitch after binding and request multipliers.
    /// /params logMissingEventPath True when empty paths should be reported in development contexts.
    /// /returns None.
    /// </summary>
    public static void PlayOneShot(string eventPath,
                                   float3 position,
                                   bool hasPosition,
                                   float volume,
                                   float pitch,
                                   bool logMissingEventPath)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
        {
            LogMissingPath(logMissingEventPath);
            return;
        }

#if NASHCORE_FMOD
        EventInstance instance = RuntimeManager.CreateInstance(eventPath);
        instance.setVolume(Mathf.Max(0f, volume));
        instance.setPitch(Mathf.Max(0.0001f, pitch));

        if (hasPosition)
        {
            Vector3 unityPosition = new Vector3(position.x, position.y, position.z);
            ATTRIBUTES_3D attributes = RuntimeUtils.To3DAttributes(unityPosition);
            instance.set3DAttributes(attributes);
        }

        instance.start();
        instance.release();
#else
        LogFmodDisabled(eventPath, logMissingEventPath);
#endif
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Logs an empty path warning only in contexts where runtime diagnostics are useful.
    /// /params shouldLog True when the current preset allows missing-path logs.
    /// /returns None.
    /// </summary>
    private static void LogMissingPath(bool shouldLog)
    {
        if (!shouldLog)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning("[GameAudio] Skipped audio request because the FMOD event path is empty.");
#endif
    }

    /// <summary>
    /// Logs the disabled-backend state when FMOD integration has not been compiled into the project.
    /// /params eventPath Event path that would have been played.
    /// /params shouldLog True when the current preset allows diagnostic logs.
    /// /returns None.
    /// </summary>
    private static void LogFmodDisabled(string eventPath, bool shouldLog)
    {
        if (!shouldLog)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log("[GameAudio] FMOD backend is disabled. Define NASHCORE_FMOD after installing FMOD Unity integration to play: " + eventPath);
#endif
    }
    #endregion

    #endregion
}
