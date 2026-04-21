using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Singleton runtime configuration baked from GameAudioManagerPreset.
/// /params None.
/// /returns None.
/// </summary>
public struct GameAudioRuntimeConfig : IComponentData
{
    public byte Enabled;
    public byte LogMissingEventPaths;
    public float MasterVolume;
    public float DefaultMinimumDistance;
    public float DefaultMaximumDistance;
}

/// <summary>
/// Baked FMOD event binding used by runtime audio request playback.
/// /params None.
/// /returns None.
/// </summary>
public struct GameAudioEventBindingElement : IBufferElementData
{
    public GameAudioEventId EventId;
    public FixedString64Bytes EventCode;
    public FixedString512Bytes EventPath;
    public float Volume;
    public float Pitch;
    public byte Spatialize;
    public float MinimumDistance;
    public float MaximumDistance;
    public byte RateLimitEnabled;
    public int MaxPlaysPerWindow;
    public float WindowSeconds;
}

/// <summary>
/// Runtime request emitted by gameplay systems and consumed by GameAudioPlaybackSystem.
/// /params None.
/// /returns None.
/// </summary>
public struct GameAudioEventRequest : IBufferElementData
{
    public GameAudioEventId EventId;
    public float3 Position;
    public byte HasPosition;
    public float VolumeMultiplier;
    public float PitchMultiplier;
}

/// <summary>
/// Runtime rate-limit state tracked per event ID by the playback system.
/// /params None.
/// /returns None.
/// </summary>
public struct GameAudioRateLimitStateElement : IBufferElementData
{
    public GameAudioEventId EventId;
    public float WindowStartTime;
    public int PlaysInWindow;
}
