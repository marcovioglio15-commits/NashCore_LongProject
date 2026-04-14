using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Centralizes mutable Laser Beam runtime-state operations shared by simulation, damage and presentation paths.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerLaserBeamStateUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resets all transient Laser Beam runtime timers and flags to their idle state.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /returns None.
    /// </summary>
    public static void ResetBeamState(ref PlayerLaserBeamState laserBeamState)
    {
        laserBeamState.IsActive = 0;
        laserBeamState.IsOverheated = 0;
        laserBeamState.IsTickReady = 0;
        laserBeamState.LastResolvedPrimaryLaneCount = 0;
        laserBeamState.CooldownRemaining = 0f;
        laserBeamState.ConsecutiveActiveElapsed = 0f;
        laserBeamState.DamageTickTimer = 0f;
        laserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
        ClearStormBurst(ref laserBeamState);
        ClearStormTickPulses(ref laserBeamState);
        ClearTriggeredActiveLaser(ref laserBeamState);
        ClearChargeImpulse(ref laserBeamState);
    }

    /// <summary>
    /// Synchronizes the transient electrical-storm burst timer with the currently started storm pulse.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params laserBeamConfig Runtime Laser Beam config that provides pulse travel and hold timing.
    /// /params deltaTime Unused frame delta kept to preserve the shared update-call shape.
    /// /returns None.
    /// </summary>
    public static void UpdateStormBurstTimer(ref PlayerLaserBeamState laserBeamState,
                                             in LaserBeamPassiveConfig laserBeamConfig,
                                             float deltaTime)
    {
        float totalDurationSeconds = ResolveStormTickTotalDurationSeconds(in laserBeamConfig);
        laserBeamState.StormBurstRemainingSeconds = ResolveCurrentStormBurstRemainingSeconds(in laserBeamState,
                                                                                             totalDurationSeconds);
    }

    /// <summary>
    /// Clears the transient electrical-storm burst state when the beam stops or resets.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /returns None.
    /// </summary>
    public static void ClearStormBurst(ref PlayerLaserBeamState laserBeamState)
    {
        laserBeamState.StormBurstRemainingSeconds = 0f;
    }

    /// <summary>
    /// Advances every active traveling damage packet while preserving its previous progress for the current frame.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params laserBeamConfig Aggregated Laser Beam passive configuration.
    /// /params deltaTime Frame delta used to advance packet travel.
    /// /returns None.
    /// </summary>
    public static void AdvanceStormTickPulses(ref PlayerLaserBeamState laserBeamState,
                                              in LaserBeamPassiveConfig laserBeamConfig,
                                              float deltaTime)
    {
        if (laserBeamState.StormTickPulses.Length <= 0)
            return;

        if (math.max(0f, laserBeamConfig.StormTickTravelSpeed) <= 0f)
        {
            ClearStormTickPulses(ref laserBeamState);
            return;
        }

        float safeDeltaTime = math.max(0f, deltaTime);

        if (safeDeltaTime <= 0f)
            return;

        for (int pulseIndex = 0; pulseIndex < laserBeamState.StormTickPulses.Length; pulseIndex++)
        {
            PlayerLaserBeamStormTickPulse pulse = laserBeamState.StormTickPulses[pulseIndex];
            pulse.PreviousElapsedSeconds = pulse.CurrentElapsedSeconds;
            pulse.CurrentElapsedSeconds += safeDeltaTime;
            laserBeamState.StormTickPulses[pulseIndex] = pulse;
        }
    }

    /// <summary>
    /// Removes completed traveling damage packets once their travel and post-travel hold have fully elapsed.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params laserBeamConfig Aggregated Laser Beam passive configuration.
    /// /returns None.
    /// </summary>
    public static void RemoveCompletedStormTickPulses(ref PlayerLaserBeamState laserBeamState,
                                                      in LaserBeamPassiveConfig laserBeamConfig)
    {
        if (laserBeamState.StormTickPulses.Length <= 0)
            return;

        float totalDurationSeconds = ResolveStormTickTotalDurationSeconds(in laserBeamConfig);

        if (totalDurationSeconds <= 0f)
        {
            ClearStormTickPulses(ref laserBeamState);
            return;
        }

        for (int pulseIndex = laserBeamState.StormTickPulses.Length - 1; pulseIndex >= 0; pulseIndex--)
        {
            PlayerLaserBeamStormTickPulse pulse = laserBeamState.StormTickPulses[pulseIndex];

            if (pulse.CurrentElapsedSeconds < totalDurationSeconds)
                continue;

            laserBeamState.StormTickPulses.RemoveAt(pulseIndex);
        }
    }

    /// <summary>
    /// Clears the transient tick-highlight packet queue stored on the Laser Beam runtime state.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /returns None.
    /// </summary>
    public static void ClearStormTickPulses(ref PlayerLaserBeamState laserBeamState)
    {
        laserBeamState.StormTickPulses.Clear();
    }

    /// <summary>
    /// Advances the active timed Laser Beam snapshot triggered by non-toggle projectile actives.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params deltaTime Frame delta used to decrease the remaining active time.
    /// /returns None.
    /// </summary>
    public static void UpdateTriggeredActiveLaser(ref PlayerLaserBeamState laserBeamState,
                                                  float deltaTime)
    {
        if (laserBeamState.TriggeredActiveRemainingSeconds <= 0f)
            return;

        laserBeamState.TriggeredActiveRemainingSeconds = math.max(0f,
                                                                  laserBeamState.TriggeredActiveRemainingSeconds - math.max(0f, deltaTime));

        if (laserBeamState.TriggeredActiveRemainingSeconds > 0f)
            return;

        ClearTriggeredActiveLaser(ref laserBeamState);
    }

    /// <summary>
    /// Stores one timed Laser Beam snapshot emitted by a non-toggle projectile active.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params durationSeconds Authored active duration in seconds.
    /// /params penetrationMode Projectile penetration mode resolved at trigger time.
    /// /params maximumPenetrations Maximum penetration budget resolved at trigger time.
    /// /params projectileTemplate Projectile snapshot resolved at trigger time.
    /// /params passiveToolsSnapshot Aggregated passive snapshot resolved at trigger time.
    /// /returns None.
    /// </summary>
    public static void ActivateTriggeredActiveLaser(ref PlayerLaserBeamState laserBeamState,
                                                    float durationSeconds,
                                                    ProjectilePenetrationMode penetrationMode,
                                                    int maximumPenetrations,
                                                    in PlayerProjectileRequestTemplate projectileTemplate,
                                                    in PlayerPassiveToolsState passiveToolsSnapshot)
    {
        ClearChargeImpulse(ref laserBeamState);
        laserBeamState.TriggeredActiveRemainingSeconds = math.max(0.05f, durationSeconds);
        laserBeamState.TriggeredActivePenetrationMode = penetrationMode;
        laserBeamState.TriggeredActiveMaxPenetrations = math.max(0, maximumPenetrations);
        laserBeamState.TriggeredActiveProjectileTemplate = projectileTemplate;
        laserBeamState.TriggeredActivePassiveToolsState = passiveToolsSnapshot;
        laserBeamState.DamageTickTimer = 0f;
        laserBeamState.ContinuousDamageAccumulatorSeconds = 0f;
    }

    /// <summary>
    /// Clears the timed Laser Beam snapshot emitted by non-toggle projectile actives.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /returns None.
    /// </summary>
    public static void ClearTriggeredActiveLaser(ref PlayerLaserBeamState laserBeamState)
    {
        laserBeamState.TriggeredActiveRemainingSeconds = 0f;
        laserBeamState.TriggeredActivePenetrationMode = ProjectilePenetrationMode.None;
        laserBeamState.TriggeredActiveMaxPenetrations = 0;
        laserBeamState.TriggeredActiveProjectileTemplate = default;
        laserBeamState.TriggeredActivePassiveToolsState = default;
    }

    /// <summary>
    /// Queues one or more serialized traveling damage packets after consuming Laser Beam tick budget.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params laserBeamConfig Runtime Laser Beam config that provides pulse travel and post-travel hold timing.
    /// /params pendingTickCount Number of damage ticks consumed during the current frame.
    /// /returns None.
    /// </summary>
    public static void EnqueueStormTickPulses(ref PlayerLaserBeamState laserBeamState,
                                              in LaserBeamPassiveConfig laserBeamConfig,
                                              int pendingTickCount)
    {
        if (pendingTickCount <= 0)
            return;

        float totalDurationSeconds = ResolveStormTickTotalDurationSeconds(in laserBeamConfig);

        if (totalDurationSeconds <= 0f)
            return;

        for (int pulseIndex = 0; pulseIndex < pendingTickCount; pulseIndex++)
        {
            float initialElapsedSeconds = ResolveQueuedStormTickInitialElapsedSeconds(in laserBeamState,
                                                                                      totalDurationSeconds);

            if (laserBeamState.StormTickPulses.Length >= laserBeamState.StormTickPulses.Capacity)
                laserBeamState.StormTickPulses.RemoveAt(0);

            laserBeamState.StormTickPulses.Add(new PlayerLaserBeamStormTickPulse
            {
                PreviousElapsedSeconds = initialElapsedSeconds,
                CurrentElapsedSeconds = initialElapsedSeconds
            });
        }

        laserBeamState.StormBurstRemainingSeconds = ResolveCurrentStormBurstRemainingSeconds(in laserBeamState,
                                                                                             totalDurationSeconds);
    }

    /// <summary>
    /// Converts one pulse elapsed time into normalized beam-length progress.
    /// /params elapsedSeconds Pulse travel time in seconds.
    /// /params travelSpeed Authored normalized travel speed.
    /// /returns Normalized pulse progress in the 0-1 range.
    /// </summary>
    public static float ResolveNormalizedStormTickProgress(float elapsedSeconds,
                                                           float travelSpeed)
    {
        float safeTravelSpeed = math.max(0f, travelSpeed);

        if (safeTravelSpeed <= 0f)
            return 1f;

        return math.saturate(math.max(0f, elapsedSeconds) * safeTravelSpeed);
    }

    /// <summary>
    /// Resolves the travel duration required by one storm packet to cross the full beam length.
    /// /params travelSpeed Authored normalized travel speed.
    /// /returns Packet travel duration in seconds.
    /// </summary>
    public static float ResolveStormTickTravelDurationSeconds(float travelSpeed)
    {
        return 1f / math.max(0.0001f, travelSpeed);
    }

    /// <summary>
    /// Resolves the total lifetime of one storm pulse, including travel and post-travel hold.
    /// /params laserBeamConfig Runtime Laser Beam config that provides travel speed and hold time.
    /// /returns Total pulse lifetime in seconds.
    /// </summary>
    public static float ResolveStormTickTotalDurationSeconds(in LaserBeamPassiveConfig laserBeamConfig)
    {
        if (laserBeamConfig.StormTickTravelSpeed <= 0f)
            return 0f;

        float travelDurationSeconds = ResolveStormTickTravelDurationSeconds(laserBeamConfig.StormTickTravelSpeed);
        return travelDurationSeconds + math.max(0f, laserBeamConfig.StormTickPostTravelHoldSeconds);
    }

    /// <summary>
    /// Resolves whether a timed Laser Beam snapshot emitted by a non-toggle projectile active is currently alive.
    /// /params laserBeamState Runtime Laser Beam state.
    /// /returns True when the triggered active snapshot is still active.
    /// </summary>
    public static bool HasTriggeredActiveLaser(in PlayerLaserBeamState laserBeamState)
    {
        return laserBeamState.TriggeredActiveRemainingSeconds > 0f &&
               laserBeamState.TriggeredActivePassiveToolsState.HasLaserBeam != 0;
    }

    /// <summary>
    /// Resolves the passive snapshot that should drive the current Laser Beam frame.
    /// /params passiveToolsState Aggregated always-on passive state.
    /// /params laserBeamState Runtime Laser Beam state.
    /// /returns Effective passive snapshot for the current frame.
    /// </summary>
    public static PlayerPassiveToolsState ResolveEffectivePassiveToolsState(in PlayerPassiveToolsState passiveToolsState,
                                                                            in PlayerLaserBeamState laserBeamState)
    {
        if (HasTriggeredActiveLaser(in laserBeamState))
            return laserBeamState.TriggeredActivePassiveToolsState;

        return passiveToolsState;
    }

    /// <summary>
    /// Advances the transient Charge Shot impulse timer carried by the Laser Beam runtime state.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params deltaTime Frame delta used to decrease timers.
    /// /returns None.
    /// </summary>
    public static void UpdateChargeImpulse(ref PlayerLaserBeamState laserBeamState,
                                           float deltaTime)
    {
        if (laserBeamState.ChargeImpulseRemainingSeconds > 0f)
            laserBeamState.ChargeImpulseRemainingSeconds = math.max(0f, laserBeamState.ChargeImpulseRemainingSeconds - math.max(0f, deltaTime));

        if (laserBeamState.ChargeImpulseRemainingSeconds > 0f)
            return;

        ClearChargeImpulse(ref laserBeamState);
    }

    /// <summary>
    /// Clears the transient Charge Shot impulse modifiers applied to the current beam.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /returns None.
    /// </summary>
    public static void ClearChargeImpulse(ref PlayerLaserBeamState laserBeamState)
    {
        laserBeamState.ChargeImpulseRemainingSeconds = 0f;
        laserBeamState.ChargeImpulseDamageMultiplier = 0f;
        laserBeamState.ChargeImpulseWidthMultiplier = 0f;
        laserBeamState.ChargeImpulseTravelDistance = 0f;
    }

    /// <summary>
    /// Advances Laser Beam cooldown timers and clears the overheated state once cooldown expires.
    /// /params laserBeamState Mutable Laser Beam runtime state.
    /// /params laserBeamConfig Aggregated Laser Beam passive configuration.
    /// /params deltaTime Frame delta used to decrease timers.
    /// /returns None.
    /// </summary>
    public static void UpdateCooldown(ref PlayerLaserBeamState laserBeamState,
                                      in LaserBeamPassiveConfig laserBeamConfig,
                                      float deltaTime)
    {
        if (laserBeamState.CooldownRemaining > 0f)
            laserBeamState.CooldownRemaining = math.max(0f, laserBeamState.CooldownRemaining - math.max(0f, deltaTime));

        if (laserBeamState.IsOverheated == 0)
            return;

        if (math.max(0f, laserBeamConfig.CooldownSeconds) <= 0f || laserBeamState.CooldownRemaining <= 0f)
            laserBeamState.IsOverheated = 0;
    }

    /// <summary>
    /// Evaluates whether the current uninterrupted activation window has reached the configured overheating threshold.
    /// /params laserBeamConfig Aggregated Laser Beam passive configuration.
    /// /params consecutiveActiveElapsed Current uninterrupted active time.
    /// /returns True when Laser Beam must enter cooldown.
    /// </summary>
    public static bool ShouldOverheat(in LaserBeamPassiveConfig laserBeamConfig,
                                      float consecutiveActiveElapsed)
    {
        if (math.max(0f, laserBeamConfig.CooldownSeconds) <= 0f)
            return false;

        float maximumContinuousActiveSeconds = math.max(0f, laserBeamConfig.MaximumContinuousActiveSeconds);

        if (maximumContinuousActiveSeconds <= 0f)
            return false;

        return consecutiveActiveElapsed >= maximumContinuousActiveSeconds;
    }

    /// <summary>
    /// Resolves the effective bounce budget inherited by the beam from the projectile bounce passive.
    /// /params passiveToolsState Aggregated passive runtime state.
    /// /params laserBeamConfig Aggregated Laser Beam passive configuration.
    /// /returns Effective bounce count used to build reflected segments.
    /// </summary>
    public static int ResolveMaximumBounceSegments(in PlayerPassiveToolsState passiveToolsState,
                                                   in LaserBeamPassiveConfig laserBeamConfig)
    {
        if (passiveToolsState.HasBouncingProjectiles == 0)
            return 0;

        int inheritedMaximumBounces = math.max(0, passiveToolsState.BouncingProjectiles.MaxBounces);
        int laserBeamBounceCap = math.max(0, laserBeamConfig.MaximumBounceSegments);

        if (laserBeamBounceCap <= 0)
            return inheritedMaximumBounces;

        return math.min(inheritedMaximumBounces, laserBeamBounceCap);
    }

    /// <summary>
    /// Resolves the last segment currently stored for one lane index.
    /// /params laserBeamLanes Current lane buffer.
    /// /params laneIndex Lane index to inspect.
    /// /params terminalSegment Last segment found for the requested lane.
    /// /returns True when the requested lane exists in the buffer.
    /// </summary>
    public static bool TryResolveTerminalSegment(DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                                                 int laneIndex,
                                                 out PlayerLaserBeamLaneElement terminalSegment)
    {
        terminalSegment = default;
        bool foundLane = false;

        for (int segmentIndex = 0; segmentIndex < laserBeamLanes.Length; segmentIndex++)
        {
            PlayerLaserBeamLaneElement currentSegment = laserBeamLanes[segmentIndex];

            if (currentSegment.LaneIndex != laneIndex)
                continue;

            terminalSegment = currentSegment;
            foundLane = true;
        }

        return foundLane;
    }

    /// <summary>
    /// Rotates one planar forward direction around the world up axis by the requested angle in degrees.
    /// /params direction Source forward direction.
    /// /params angleDegrees Signed planar angle in degrees.
    /// /returns The normalized rotated planar direction.
    /// </summary>
    public static float3 RotatePlanarDirection(float3 direction,
                                               float angleDegrees)
    {
        float radians = math.radians(angleDegrees);
        quaternion rotationOffset = quaternion.AxisAngle(new float3(0f, 1f, 0f), radians);
        float3 rotatedDirection = math.rotate(rotationOffset, math.normalizesafe(direction, new float3(0f, 0f, 1f)));
        return math.normalizesafe(rotatedDirection, new float3(0f, 0f, 1f));
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the elapsed-time seed assigned to the next queued pulse so pulses remain serialized without overlap.
    /// /params laserBeamState Runtime beam state containing the existing pulse queue.
    /// /params totalDurationSeconds Total duration of one pulse including travel and hold.
    /// /returns Initial elapsed time assigned to the newly queued pulse.
    /// </summary>
    private static float ResolveQueuedStormTickInitialElapsedSeconds(in PlayerLaserBeamState laserBeamState,
                                                                     float totalDurationSeconds)
    {
        if (laserBeamState.StormTickPulses.Length <= 0)
            return 0f;

        PlayerLaserBeamStormTickPulse lastPulse = laserBeamState.StormTickPulses[laserBeamState.StormTickPulses.Length - 1];
        return lastPulse.CurrentElapsedSeconds - totalDurationSeconds;
    }

    /// <summary>
    /// Resolves the remaining burst lifetime of the oldest started pulse currently driving the storm visuals.
    /// /params laserBeamState Runtime beam state containing the pulse queue.
    /// /params totalDurationSeconds Total duration of one pulse including travel and hold.
    /// /returns Remaining burst lifetime in seconds, or 0 when no pulse is currently started.
    /// </summary>
    private static float ResolveCurrentStormBurstRemainingSeconds(in PlayerLaserBeamState laserBeamState,
                                                                  float totalDurationSeconds)
    {
        if (laserBeamState.StormTickPulses.Length <= 0 || totalDurationSeconds <= 0f)
            return 0f;

        for (int pulseIndex = 0; pulseIndex < laserBeamState.StormTickPulses.Length; pulseIndex++)
        {
            PlayerLaserBeamStormTickPulse pulse = laserBeamState.StormTickPulses[pulseIndex];

            if (pulse.CurrentElapsedSeconds < 0f || pulse.CurrentElapsedSeconds >= totalDurationSeconds)
                continue;

            return totalDurationSeconds - pulse.CurrentElapsedSeconds;
        }

        return 0f;
    }
    #endregion

    #endregion
}
