using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Lightweight helpers used by ECS systems to enqueue gameplay audio requests.
/// /params None.
/// /returns None.
/// </summary>
public static class GameAudioEventRequestUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds one positioned audio event request to an already resolved singleton request buffer.
    /// /params requests Mutable singleton audio request buffer.
    /// /params eventId Gameplay audio event to request.
    /// /params position World-space position for 3D playback.
    /// /returns None.
    /// </summary>
    public static void EnqueuePositioned(DynamicBuffer<GameAudioEventRequest> requests,
                                         GameAudioEventId eventId,
                                         float3 position)
    {
        Enqueue(requests, eventId, position, true, 1f, 1f);
    }

    /// <summary>
    /// Adds one non-positioned audio event request to an already resolved singleton request buffer.
    /// /params requests Mutable singleton audio request buffer.
    /// /params eventId Gameplay audio event to request.
    /// /returns None.
    /// </summary>
    public static void EnqueueGlobal(DynamicBuffer<GameAudioEventRequest> requests, GameAudioEventId eventId)
    {
        Enqueue(requests, eventId, float3.zero, false, 1f, 1f);
    }

    /// <summary>
    /// Adds one audio request with explicit playback multipliers.
    /// /params requests Mutable singleton audio request buffer.
    /// /params eventId Gameplay audio event to request.
    /// /params position World-space position for 3D playback.
    /// /params hasPosition True when position should be used.
    /// /params volumeMultiplier Request-local volume multiplier.
    /// /params pitchMultiplier Request-local pitch multiplier.
    /// /returns None.
    /// </summary>
    public static void Enqueue(DynamicBuffer<GameAudioEventRequest> requests,
                               GameAudioEventId eventId,
                               float3 position,
                               bool hasPosition,
                               float volumeMultiplier,
                               float pitchMultiplier)
    {
        if (!requests.IsCreated)
            return;

        if (eventId == GameAudioEventId.None)
            return;

        requests.Add(new GameAudioEventRequest
        {
            EventId = eventId,
            Position = position,
            HasPosition = hasPosition ? (byte)1 : (byte)0,
            VolumeMultiplier = volumeMultiplier,
            PitchMultiplier = pitchMultiplier
        });
    }
    #endregion

    #endregion
}
