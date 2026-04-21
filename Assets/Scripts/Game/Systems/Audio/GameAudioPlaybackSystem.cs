using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Consumes gameplay audio requests from the audio singleton and dispatches them to the FMOD wrapper.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct GameAudioPlaybackSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the singleton buffers required for runtime playback.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameAudioRuntimeConfig>();
        state.RequireForUpdate<GameAudioEventBindingElement>();
        state.RequireForUpdate<GameAudioEventRequest>();
        state.RequireForUpdate<GameAudioRateLimitStateElement>();
    }

    /// <summary>
    /// Resolves queued audio requests, applies per-event caps, and clears consumed requests.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        Entity audioEntity = SystemAPI.GetSingletonEntity<GameAudioRuntimeConfig>();
        EntityManager entityManager = state.EntityManager;
        GameAudioRuntimeConfig runtimeConfig = entityManager.GetComponentData<GameAudioRuntimeConfig>(audioEntity);
        DynamicBuffer<GameAudioEventRequest> requests = entityManager.GetBuffer<GameAudioEventRequest>(audioEntity);

        if (requests.Length <= 0)
            return;

        if (runtimeConfig.Enabled == 0)
        {
            requests.Clear();
            return;
        }

        DynamicBuffer<GameAudioEventBindingElement> bindings = entityManager.GetBuffer<GameAudioEventBindingElement>(audioEntity);
        DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStates = entityManager.GetBuffer<GameAudioRateLimitStateElement>(audioEntity);
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        for (int requestIndex = 0; requestIndex < requests.Length; requestIndex++)
        {
            GameAudioEventRequest request = requests[requestIndex];

            if (!TryResolveBinding(bindings, request.EventId, out GameAudioEventBindingElement binding))
                continue;

            if (!CanPlayNow(rateLimitStates, binding, elapsedTime))
                continue;

            float volume = math.max(0f, runtimeConfig.MasterVolume) *
                           math.max(0f, binding.Volume) *
                           math.max(0f, request.VolumeMultiplier);
            float pitch = math.max(0.0001f, binding.Pitch) *
                          math.max(0.0001f, request.PitchMultiplier);

            GameAudioFmodRuntimeUtility.PlayOneShot(binding.EventPath.ToString(),
                                                    request.Position,
                                                    request.HasPosition != 0 && binding.Spatialize != 0,
                                                    volume,
                                                    pitch,
                                                    runtimeConfig.LogMissingEventPaths != 0);
        }

        requests.Clear();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Finds the first binding matching a requested event ID.
    /// /params bindings Baked binding buffer.
    /// /params eventId Requested event identifier.
    /// /params binding Output binding when found.
    /// /returns True when a matching binding exists.
    /// </summary>
    private static bool TryResolveBinding(DynamicBuffer<GameAudioEventBindingElement> bindings,
                                          GameAudioEventId eventId,
                                          out GameAudioEventBindingElement binding)
    {
        for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
        {
            GameAudioEventBindingElement candidate = bindings[bindingIndex];

            if (candidate.EventId != eventId)
                continue;

            binding = candidate;
            return true;
        }

        binding = default;
        return false;
    }

    /// <summary>
    /// Applies one binding's runtime rate limit and records the accepted play when allowed.
    /// /params rateLimitStates Mutable singleton buffer storing event windows.
    /// /params binding Event binding being evaluated.
    /// /params elapsedTime Current world elapsed time in seconds.
    /// /returns True when playback may proceed.
    /// </summary>
    private static bool CanPlayNow(DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStates,
                                   GameAudioEventBindingElement binding,
                                   float elapsedTime)
    {
        if (binding.RateLimitEnabled == 0)
            return true;

        if (binding.MaxPlaysPerWindow <= 0 || binding.WindowSeconds <= 0f)
            return true;

        int stateIndex = FindRateLimitStateIndex(rateLimitStates, binding.EventId);

        if (stateIndex < 0)
        {
            rateLimitStates.Add(new GameAudioRateLimitStateElement
            {
                EventId = binding.EventId,
                WindowStartTime = elapsedTime,
                PlaysInWindow = 1
            });
            return true;
        }

        GameAudioRateLimitStateElement rateLimitState = rateLimitStates[stateIndex];
        float elapsedInWindow = elapsedTime - rateLimitState.WindowStartTime;

        if (elapsedInWindow >= binding.WindowSeconds || elapsedInWindow < 0f)
        {
            rateLimitState.WindowStartTime = elapsedTime;
            rateLimitState.PlaysInWindow = 0;
        }

        if (rateLimitState.PlaysInWindow >= binding.MaxPlaysPerWindow)
        {
            rateLimitStates[stateIndex] = rateLimitState;
            return false;
        }

        rateLimitState.PlaysInWindow++;
        rateLimitStates[stateIndex] = rateLimitState;
        return true;
    }

    /// <summary>
    /// Finds the buffer index for a rate-limit state entry.
    /// /params rateLimitStates Mutable singleton state buffer.
    /// /params eventId Event identifier to search.
    /// /returns Buffer index when found; otherwise -1.
    /// </summary>
    private static int FindRateLimitStateIndex(DynamicBuffer<GameAudioRateLimitStateElement> rateLimitStates,
                                               GameAudioEventId eventId)
    {
        for (int index = 0; index < rateLimitStates.Length; index++)
        {
            if (rateLimitStates[index].EventId != eventId)
                continue;

            return index;
        }

        return -1;
    }
    #endregion

    #endregion
}
