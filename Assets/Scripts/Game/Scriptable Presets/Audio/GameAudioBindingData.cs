using System;
using UnityEngine;

/// <summary>
/// Global playback options applied by the Game Audio playback system before dispatching FMOD events.
/// /params None.
/// /returns None.
/// </summary>
[Serializable]
public sealed class GameAudioPlaybackSettings
{
    #region Fields

    #region Serialized Fields
    [Header("Runtime")]
    [Tooltip("Enables runtime playback for requests emitted by gameplay ECS systems.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Master scalar multiplied into every game audio event volume.")]
    [SerializeField] private float masterVolume = 1f;

    [Tooltip("Fallback minimum 3D attenuation distance used when an event binding does not override it.")]
    [SerializeField] private float defaultMinimumDistance = 1f;

    [Tooltip("Fallback maximum 3D attenuation distance used when an event binding does not override it.")]
    [SerializeField] private float defaultMaximumDistance = 45f;

    [Tooltip("Logs missing FMOD event paths while running in the editor or a development build.")]
    [SerializeField] private bool logMissingEventPaths = true;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public float MasterVolume
    {
        get
        {
            return masterVolume;
        }
    }

    public float DefaultMinimumDistance
    {
        get
        {
            return defaultMinimumDistance;
        }
    }

    public float DefaultMaximumDistance
    {
        get
        {
            return defaultMaximumDistance;
        }
    }

    public bool LogMissingEventPaths
    {
        get
        {
            return logMissingEventPaths;
        }
    }
    #endregion
}

/// <summary>
/// FMOD bus path and routing values stored in the audio manager preset for project-level mixing control.
/// /params None.
/// /returns None.
/// </summary>
[Serializable]
public sealed class GameAudioRoutingSettings
{
    #region Fields

    #region Serialized Fields
    [Header("FMOD Buses")]
    [Tooltip("FMOD master bus path used by game-wide volume automation.")]
    [SerializeField] private string masterBusPath = "bus:/";

    [Tooltip("FMOD SFX bus path used by moment-to-moment gameplay sound effects.")]
    [SerializeField] private string sfxBusPath = "bus:/SFX";

    [Tooltip("FMOD music bus path reserved for music control from future game management sections.")]
    [SerializeField] private string musicBusPath = "bus:/Music";

    [Tooltip("Default SFX bus volume scalar stored for tooling and future runtime bus automation.")]
    [SerializeField] private float sfxVolume = 1f;

    [Tooltip("Default music bus volume scalar stored for tooling and future runtime bus automation.")]
    [SerializeField] private float musicVolume = 1f;
    #endregion

    #endregion

    #region Properties
    public string MasterBusPath
    {
        get
        {
            return masterBusPath;
        }
    }

    public string SfxBusPath
    {
        get
        {
            return sfxBusPath;
        }
    }

    public string MusicBusPath
    {
        get
        {
            return musicBusPath;
        }
    }

    public float SfxVolume
    {
        get
        {
            return sfxVolume;
        }
    }

    public float MusicVolume
    {
        get
        {
            return musicVolume;
        }
    }
    #endregion
}

/// <summary>
/// Per-event cap used to avoid flooding FMOD with dense ECS gameplay events.
/// /params None.
/// /returns None.
/// </summary>
[Serializable]
public sealed class GameAudioRateLimitSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, this binding can only play a limited number of times inside the configured time window.")]
    [SerializeField] private bool enabled;

    [Tooltip("Maximum play count allowed inside one rate-limit window. Values below one are reported by validation.")]
    [SerializeField] private int maxPlaysPerWindow;

    [Tooltip("Window length in seconds used by the per-event rate cap.")]
    [SerializeField] private float windowSeconds = 1f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public int MaxPlaysPerWindow
    {
        get
        {
            return maxPlaysPerWindow;
        }
    }

    public float WindowSeconds
    {
        get
        {
            return windowSeconds;
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Writes the rate-limit values used by default enemy projectile audio to prevent burst spam.
    /// /params maxPlays Maximum allowed plays in one window.
    /// /params seconds Window length in seconds.
    /// /returns None.
    /// </summary>
    public void Configure(int maxPlays, float seconds)
    {
        enabled = true;
        maxPlaysPerWindow = maxPlays;
        windowSeconds = seconds;
    }

    /// <summary>
    /// Disables the cap while keeping the authored numeric values visible in the inspector.
    /// /params None.
    /// /returns None.
    /// </summary>
    public void Disable()
    {
        enabled = false;
    }
    #endregion
}

/// <summary>
/// One FMOD event binding exposed in the Audio Manager section and baked into ECS for runtime lookup.
/// /params None.
/// /returns None.
/// </summary>
[Serializable]
public sealed class GameAudioEventBinding
{
    #region Fields

    #region Serialized Fields
    [Header("Identity")]
    [Tooltip("Stable gameplay event identifier used by ECS systems.")]
    [SerializeField] private GameAudioEventId eventId;

    [Tooltip("Production-facing event code shown in the Game Management Tool.")]
    [SerializeField] private string eventCode;

    [Tooltip("Readable label used by tool panels.")]
    [SerializeField] private string displayName;

    [Tooltip("Short description of the gameplay moment that requests this event.")]
    [SerializeField] private string description;

    [Header("FMOD Event")]
    [Tooltip("FMOD event path, for example event:/SFX/Player/Shoot.")]
    [SerializeField] private string eventPath;

    [Tooltip("Volume scalar applied to this event before the global master volume.")]
    [SerializeField] private float volume = 1f;

    [Tooltip("Pitch scalar applied when the FMOD backend is enabled.")]
    [SerializeField] private float pitch = 1f;

    [Tooltip("When enabled and a request position is available, the event is emitted as 3D audio.")]
    [SerializeField] private bool spatialize = true;

    [Tooltip("Minimum 3D attenuation distance used by FMOD for this event.")]
    [SerializeField] private float minimumDistance = 1f;

    [Tooltip("Maximum 3D attenuation distance used by FMOD for this event.")]
    [SerializeField] private float maximumDistance = 45f;

    [Header("Rate Limit")]
    [Tooltip("Optional per-event cap that limits dense repeated requests over a short time window.")]
    [SerializeField] private GameAudioRateLimitSettings rateLimit = new GameAudioRateLimitSettings();
    #endregion

    #endregion

    #region Properties
    public GameAudioEventId EventId
    {
        get
        {
            return eventId;
        }
    }

    public string EventCode
    {
        get
        {
            return eventCode;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string EventPath
    {
        get
        {
            return eventPath;
        }
    }

    public float Volume
    {
        get
        {
            return volume;
        }
    }

    public float Pitch
    {
        get
        {
            return pitch;
        }
    }

    public bool Spatialize
    {
        get
        {
            return spatialize;
        }
    }

    public float MinimumDistance
    {
        get
        {
            return minimumDistance;
        }
    }

    public float MaximumDistance
    {
        get
        {
            return maximumDistance;
        }
    }

    public GameAudioRateLimitSettings RateLimit
    {
        get
        {
            return rateLimit;
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Applies stable default identity metadata without touching user-authored FMOD event paths.
    /// /params definition Default descriptor used by the preset factory.
    /// /returns None.
    /// </summary>
    public void ConfigureIdentity(GameAudioDefaultEventDefinition definition)
    {
        eventId = definition.EventId;
        eventCode = definition.EventCode;
        displayName = definition.DisplayName;
        description = definition.Description;

        if (rateLimit == null)
            rateLimit = new GameAudioRateLimitSettings();
    }

    /// <summary>
    /// Applies the default enemy projectile cap used by newly created presets.
    /// /params maxPlays Maximum allowed plays in one window.
    /// /params seconds Window length in seconds.
    /// /returns None.
    /// </summary>
    public void ConfigureEnemyProjectileCap(int maxPlays, float seconds)
    {
        if (rateLimit == null)
            rateLimit = new GameAudioRateLimitSettings();

        rateLimit.Configure(maxPlays, seconds);
    }
    #endregion
}
