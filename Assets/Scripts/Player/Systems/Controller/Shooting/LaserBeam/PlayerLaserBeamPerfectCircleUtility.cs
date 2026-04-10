using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Builds Laser Beam lane polylines that follow the same Perfect Circle trajectory family used by projectile simulation.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerLaserBeamPerfectCircleUtility
{
    #region Constants
    private const float MinimumSimulationDeltaTime = 1f / 240f;
    private const float MaximumSimulationDeltaTime = 1f / 20f;
    private const float TargetSegmentLength = 0.85f;
    private const float MaximumAngularStepRadians = 0.22f;
    private const int MaximumSimulationIterations = 256;
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves one Laser Beam lane by sampling the Perfect Circle projectile path over the current active-time window.
    /// /params laneBuffer Output segment buffer.
    /// /params laneIndex Stable lane index assigned to all appended segments.
    /// /params isSplitChild True when the lane belongs to one split child branch.
    /// /params shooterEntity Player entity used for deterministic seed reconstruction.
    /// /params shooterPosition Current shooter position used by orbit phases.
    /// /params shooterVelocity Current shooter velocity used during the radial entry phase.
    /// /params startPoint World-space lane origin.
    /// /params direction Current lane forward direction.
    /// /params activeSeconds Consecutive active time currently accumulated by the beam.
    /// /params globalTime Current world elapsed time in seconds.
    /// /params rangeLimit Effective projectile range inherited by the beam.
    /// /params lifetimeLimit Effective projectile lifetime inherited by the beam.
    /// /params speedMultiplier Beam-local speed multiplier applied on top of Perfect Circle motion speeds.
    /// /params collisionRadius Effective lane collision radius.
    /// /params visualWidth Effective lane visual width.
    /// /params damageMultiplier Lane-local damage multiplier.
    /// /params perfectCircleConfig Aggregated Perfect Circle passive configuration.
    /// /params physicsWorldSingleton Physics world used for optional wall clipping.
    /// /params wallsCollisionFilter Collision filter used to detect world walls.
    /// /params reachedVirtualDespawn True when the sampled lane has reached a virtual despawn condition and can emit split-on-despawn branches.
    /// /params wallsEnabled True when wall clipping should be evaluated.
    /// /returns True when at least one beam segment was appended.
    /// </summary>
    internal static bool TryAppendPerfectCircleLaneSegments(ref DynamicBuffer<PlayerLaserBeamLaneElement> laneBuffer,
                                                            int laneIndex,
                                                            bool isSplitChild,
                                                            Entity shooterEntity,
                                                            float3 shooterPosition,
                                                            float3 shooterVelocity,
                                                            float3 startPoint,
                                                            float3 direction,
                                                            float activeSeconds,
                                                            float globalTime,
                                                            float rangeLimit,
                                                            float lifetimeLimit,
                                                            float speedMultiplier,
                                                            float collisionRadius,
                                                            float visualWidth,
                                                            float damageMultiplier,
                                                            in PerfectCirclePassiveConfig perfectCircleConfig,
                                                            in PhysicsWorldSingleton physicsWorldSingleton,
                                                            in CollisionFilter wallsCollisionFilter,
                                                            out bool reachedVirtualDespawn,
                                                            bool wallsEnabled)
    {
        reachedVirtualDespawn = false;
        float maximumSimulationSeconds = ResolveMaximumSimulationSeconds(activeSeconds, lifetimeLimit);

        if (maximumSimulationSeconds <= 0f)
            return false;

        ProjectilePerfectCircleState perfectCircleState = BuildPerfectCircleState(in perfectCircleConfig,
                                                                                  laneIndex,
                                                                                  shooterEntity,
                                                                                  startPoint,
                                                                                  direction,
                                                                                  speedMultiplier);
        float activationStartGlobalTime = globalTime - math.max(0f, activeSeconds);
        float maximumTravelDistance = ResolveMaximumTravelDistance(rangeLimit, lifetimeLimit);
        float simulatedSeconds = 0f;
        float accumulatedDistance = 0f;
        float3 currentPosition = startPoint;
        float3 terminalNormal = float3.zero;
        bool terminalBlockedByWall = false;
        int laneStartIndex = laneBuffer.Length;
        int simulationIterationCount = 0;

        while (simulatedSeconds < maximumSimulationSeconds &&
               accumulatedDistance < maximumTravelDistance &&
               simulationIterationCount < MaximumSimulationIterations)
        {
            simulationIterationCount++;
            float sampleGlobalTime = activationStartGlobalTime + simulatedSeconds;
            float simulationDeltaTime = ResolveSimulationDeltaTime(in perfectCircleState,
                                                                  in perfectCircleConfig,
                                                                  speedMultiplier,
                                                                  sampleGlobalTime);
            float remainingSeconds = maximumSimulationSeconds - simulatedSeconds;

            if (simulationDeltaTime > remainingSeconds)
                simulationDeltaTime = remainingSeconds;

            if (simulationDeltaTime <= 0f)
                break;

            float3 nextPosition = ResolveSamplePosition(ref perfectCircleState,
                                                        shooterPosition,
                                                        shooterVelocity,
                                                        currentPosition,
                                                        simulationDeltaTime,
                                                        sampleGlobalTime + simulationDeltaTime,
                                                        speedMultiplier,
                                                        in perfectCircleConfig);
            float3 requestedDisplacement = nextPosition - currentPosition;
            float requestedLength = math.length(requestedDisplacement);
            simulatedSeconds += simulationDeltaTime;

            if (requestedLength <= DirectionEpsilon)
                continue;

            if (accumulatedDistance + requestedLength > maximumTravelDistance)
            {
                float remainingDistance = maximumTravelDistance - accumulatedDistance;

                if (remainingDistance <= PlayerLaserBeamUtility.MinimumTravelDistance)
                    break;

                float clampedFraction = remainingDistance / requestedLength;
                nextPosition = currentPosition + requestedDisplacement * clampedFraction;
                requestedDisplacement = nextPosition - currentPosition;
                requestedLength = remainingDistance;
            }

            if (requestedLength <= PlayerLaserBeamUtility.MinimumTravelDistance)
            {
                accumulatedDistance += requestedLength;
                currentPosition = nextPosition;
                continue;
            }

            if (!PlayerLaserBeamUtility.TryResolveSegment(currentPosition,
                                                          nextPosition,
                                                          collisionRadius,
                                                          in physicsWorldSingleton,
                                                          in wallsCollisionFilter,
                                                          wallsEnabled,
                                                          out float3 resolvedEndPoint,
                                                          out float3 resolvedDirection,
                                                          out float resolvedLength,
                                                          out bool hitWall,
                                                          out float3 wallNormal))
            {
                if (wallsEnabled)
                {
                    terminalBlockedByWall = true;
                    terminalNormal = wallNormal;
                }

                break;
            }

            PlayerLaserBeamUtility.AppendLaneSegment(ref laneBuffer,
                                                     laneIndex,
                                                     isSplitChild,
                                                     currentPosition,
                                                     resolvedEndPoint,
                                                     resolvedDirection,
                                                     resolvedLength,
                                                     collisionRadius,
                                                     visualWidth,
                                                     damageMultiplier,
                                                     false,
                                                     false,
                                                     float3.zero);
            accumulatedDistance += resolvedLength;
            currentPosition = resolvedEndPoint;

            if (!hitWall)
                continue;

            terminalBlockedByWall = true;
            terminalNormal = wallNormal;
            break;
        }

        FinalizeLaneSegments(ref laneBuffer,
                             laneStartIndex,
                             terminalBlockedByWall,
                             terminalNormal);
        bool reachedLifetimeCap = lifetimeLimit > 0f && activeSeconds >= lifetimeLimit;
        bool reachedRangeCap = rangeLimit > 0f &&
                               accumulatedDistance + PlayerLaserBeamUtility.MinimumTravelDistance >= maximumTravelDistance;
        reachedVirtualDespawn = terminalBlockedByWall || reachedLifetimeCap || reachedRangeCap;
        return laneBuffer.Length > laneStartIndex;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the effective simulation time window allowed for the current beam lane.
    /// /params activeSeconds Consecutive active time currently accumulated by the beam.
    /// /params lifetimeLimit Effective projectile lifetime inherited by the beam.
    /// /returns The time window that can still produce valid geometry.
    /// </summary>
    private static float ResolveMaximumSimulationSeconds(float activeSeconds,
                                                         float lifetimeLimit)
    {
        float maximumSimulationSeconds = math.max(0f, activeSeconds);

        if (lifetimeLimit > 0f)
            maximumSimulationSeconds = math.min(maximumSimulationSeconds, lifetimeLimit);

        return maximumSimulationSeconds;
    }

    /// <summary>
    /// Resolves the maximum path distance allowed by projectile range or the beam fallback cap when no range or lifetime exists.
    /// /params rangeLimit Effective projectile range inherited by the beam.
    /// /params lifetimeLimit Effective projectile lifetime inherited by the beam.
    /// /returns The maximum path distance that can be sampled for the current lane.
    /// </summary>
    private static float ResolveMaximumTravelDistance(float rangeLimit,
                                                      float lifetimeLimit)
    {
        if (rangeLimit > 0f)
            return math.max(PlayerLaserBeamUtility.MinimumTravelDistance, rangeLimit);

        if (lifetimeLimit > 0f)
            return float.MaxValue;

        return PlayerLaserBeamUtility.DefaultUnboundedBeamDistance;
    }

    /// <summary>
    /// Resolves one sampling delta that keeps curved beam reconstruction smooth without exploding segment counts.
    /// /params perfectCircleState Current simulated Perfect Circle state.
    /// /params perfectCircleConfig Aggregated Perfect Circle configuration.
    /// /params speedMultiplier Beam-local speed multiplier applied to motion speeds.
    /// /params globalTime Absolute world time associated with the current sample start.
    /// /returns The clamped simulation delta to use for the next sampled step.
    /// </summary>
    private static float ResolveSimulationDeltaTime(in ProjectilePerfectCircleState perfectCircleState,
                                                    in PerfectCirclePassiveConfig perfectCircleConfig,
                                                    float speedMultiplier,
                                                    float globalTime)
    {
        float effectiveSpeedMultiplier = math.max(0f, speedMultiplier);
        float effectiveLinearSpeed = math.max(0.05f, perfectCircleConfig.RadialEntrySpeed * effectiveSpeedMultiplier);
        float angularSpeedRadiansPerSecond = 0f;

        if (perfectCircleState.HasEnteredOrbit != 0)
        {
            switch (perfectCircleConfig.PathMode)
            {
                case ProjectileOrbitPathMode.GoldenSpiral:
                    angularSpeedRadiansPerSecond = math.radians(math.max(0f,
                                                                         perfectCircleConfig.SpiralAngularSpeedDegreesPerSecond *
                                                                         effectiveSpeedMultiplier));
                    effectiveLinearSpeed = math.max(0.05f,
                                                    angularSpeedRadiansPerSecond *
                                                    math.max(0.05f,
                                                             perfectCircleState.CurrentRadius));
                    break;
                default:
                    float orbitRadius = ResolveCircularOrbitRadius(globalTime, in perfectCircleConfig);
                    effectiveLinearSpeed = math.max(0.05f, perfectCircleConfig.OrbitalSpeed * effectiveSpeedMultiplier);

                    if (orbitRadius > 0.001f)
                        angularSpeedRadiansPerSecond = effectiveLinearSpeed / orbitRadius;

                    break;
            }
        }

        float deltaFromDistance = TargetSegmentLength / effectiveLinearSpeed;
        float deltaFromAngularStep = angularSpeedRadiansPerSecond > 1e-6f
            ? MaximumAngularStepRadians / angularSpeedRadiansPerSecond
            : MaximumSimulationDeltaTime;
        float resolvedDeltaTime = math.min(deltaFromDistance, deltaFromAngularStep);
        return math.clamp(resolvedDeltaTime, MinimumSimulationDeltaTime, MaximumSimulationDeltaTime);
    }

    /// <summary>
    /// Resolves the next world-space point of one sampled Perfect Circle step.
    /// /params perfectCircleState Mutable Perfect Circle state advanced by the sample.
    /// /params shooterPosition Current shooter position used by orbit phases.
    /// /params shooterVelocity Current shooter velocity used during radial entry.
    /// /params fallbackPosition Previous sampled position returned when no movement can be produced.
    /// /params deltaTime Step delta applied to the simulated trajectory.
    /// /params globalTime Absolute world time associated with the sample end.
    /// /params speedMultiplier Beam-local speed multiplier applied to motion speeds.
    /// /params perfectCircleConfig Aggregated Perfect Circle configuration.
    /// /returns The resolved world-space position reached by the sampled trajectory step.
    /// </summary>
    private static float3 ResolveSamplePosition(ref ProjectilePerfectCircleState perfectCircleState,
                                                float3 shooterPosition,
                                                float3 shooterVelocity,
                                                float3 fallbackPosition,
                                                float deltaTime,
                                                float globalTime,
                                                float speedMultiplier,
                                                in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        if (perfectCircleState.Enabled == 0 || deltaTime <= 0f)
            return fallbackPosition;

        float3 entryDirection = perfectCircleState.RadialDirection;
        entryDirection.y = 0f;

        if (math.lengthsq(entryDirection) <= DirectionEpsilon)
        {
            entryDirection = fallbackPosition - perfectCircleState.EntryOrigin;
            entryDirection.y = 0f;
            entryDirection = math.normalizesafe(entryDirection, new float3(0f, 0f, 1f));
            perfectCircleState.RadialDirection = entryDirection;
        }

        float orbitEntryThreshold = ResolveOrbitEntryThreshold(globalTime, in perfectCircleConfig);
        float radialSpeed = math.max(0f, perfectCircleConfig.RadialEntrySpeed * math.max(0f, speedMultiplier));

        if (perfectCircleState.HasEnteredOrbit == 0)
        {
            float entryStepDistance = radialSpeed * deltaTime;
            perfectCircleState.CurrentRadius += entryStepDistance;
            perfectCircleState.EntryOrigin += shooterVelocity * deltaTime;
            float traveledEntryDistance = perfectCircleState.CurrentRadius;
            float3 entryPosition = perfectCircleState.EntryOrigin + entryDirection * traveledEntryDistance;
            entryPosition.y = perfectCircleState.EntryOrigin.y + perfectCircleConfig.HeightOffset;
            perfectCircleState.RadialDirection = entryDirection;

            if (traveledEntryDistance < orbitEntryThreshold)
                return entryPosition;

            float3 entryOffset = entryPosition - shooterPosition;
            entryOffset.y = 0f;
            float entryRadius = math.length(entryOffset);
            float3 orbitEntryDirection = entryRadius > DirectionEpsilon
                ? entryOffset / entryRadius
                : entryDirection;
            perfectCircleState.HasEnteredOrbit = 1;
            perfectCircleState.OrbitBlendProgress = 1f;
            perfectCircleState.CurrentRadius = math.max(0.05f, entryRadius);
            perfectCircleState.OrbitAngle = math.atan2(orbitEntryDirection.z, orbitEntryDirection.x);
            perfectCircleState.AccumulatedOrbitRadians = 0f;
            perfectCircleState.CompletedFullOrbit = 0;
        }

        perfectCircleState.RadialDirection = entryDirection;

        switch (perfectCircleConfig.PathMode)
        {
            case ProjectileOrbitPathMode.GoldenSpiral:
                return ResolveGoldenSpiralSamplePosition(ref perfectCircleState,
                                                         shooterPosition,
                                                         deltaTime,
                                                         speedMultiplier,
                                                         in perfectCircleConfig);
            default:
                return ResolveCircularOrbitSamplePosition(ref perfectCircleState,
                                                          shooterPosition,
                                                          deltaTime,
                                                          globalTime,
                                                          speedMultiplier,
                                                          in perfectCircleConfig);
        }
    }

    /// <summary>
    /// Resolves one circular-orbit sample position using the same pulsing radius logic as projectile simulation.
    /// /params perfectCircleState Mutable Perfect Circle state advanced by the circular sample.
    /// /params shooterPosition Current shooter position used as the orbit center.
    /// /params deltaTime Step delta applied to the trajectory.
    /// /params globalTime Absolute world time associated with the sample end.
    /// /params speedMultiplier Beam-local speed multiplier applied to orbit speed.
    /// /params perfectCircleConfig Aggregated Perfect Circle configuration.
    /// /returns The sampled circular-orbit world-space position.
    /// </summary>
    private static float3 ResolveCircularOrbitSamplePosition(ref ProjectilePerfectCircleState perfectCircleState,
                                                             float3 shooterPosition,
                                                             float deltaTime,
                                                             float globalTime,
                                                             float speedMultiplier,
                                                             in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        float orbitRadius = ResolveCircularOrbitRadius(globalTime, in perfectCircleConfig);
        float orbitSpeed = math.max(0f, perfectCircleConfig.OrbitalSpeed * math.max(0f, speedMultiplier));
        float angularSpeed = orbitRadius > 0.001f ? orbitSpeed / orbitRadius : 0f;
        float angularStep = angularSpeed * deltaTime;
        perfectCircleState.CurrentRadius = orbitRadius;
        perfectCircleState.OrbitAngle += angularStep;

        if (perfectCircleState.CompletedFullOrbit == 0)
        {
            perfectCircleState.AccumulatedOrbitRadians += math.abs(angularStep);

            if (perfectCircleState.AccumulatedOrbitRadians >= math.PI * 2f)
                perfectCircleState.CompletedFullOrbit = 1;
        }

        float cos = math.cos(perfectCircleState.OrbitAngle);
        float sin = math.sin(perfectCircleState.OrbitAngle);
        float3 orbitOffset = new float3(cos * orbitRadius, 0f, sin * orbitRadius);
        float3 orbitPosition = shooterPosition + orbitOffset;
        orbitPosition.y = shooterPosition.y + perfectCircleConfig.HeightOffset;
        return orbitPosition;
    }

    /// <summary>
    /// Resolves one golden-spiral sample position using the same exponential radius growth as projectile simulation.
    /// /params perfectCircleState Mutable Perfect Circle state advanced by the spiral sample.
    /// /params shooterPosition Current shooter position used as the orbit center.
    /// /params deltaTime Step delta applied to the trajectory.
    /// /params speedMultiplier Beam-local speed multiplier applied to spiral angular speed.
    /// /params perfectCircleConfig Aggregated Perfect Circle configuration.
    /// /returns The sampled golden-spiral world-space position.
    /// </summary>
    private static float3 ResolveGoldenSpiralSamplePosition(ref ProjectilePerfectCircleState perfectCircleState,
                                                            float3 shooterPosition,
                                                            float deltaTime,
                                                            float speedMultiplier,
                                                            in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        const float GoldenRatio = 1.61803398875f;
        float spiralStartRadius = math.max(0.05f, perfectCircleConfig.SpiralStartRadius);
        float spiralMaximumRadius = math.max(spiralStartRadius, perfectCircleConfig.SpiralMaximumRadius);
        float angularSpeedRadiansPerSecond = math.radians(math.max(0f,
                                                                   perfectCircleConfig.SpiralAngularSpeedDegreesPerSecond *
                                                                   math.max(0f, speedMultiplier)));
        float directionSign = perfectCircleConfig.SpiralClockwise != 0 ? -1f : 1f;
        float angularStep = angularSpeedRadiansPerSecond * deltaTime * directionSign;
        float growthMultiplier = math.max(0f, perfectCircleConfig.SpiralGrowthMultiplier);
        float growthExponent = growthMultiplier > 0f ? math.log(GoldenRatio) * (2f / math.PI) * growthMultiplier : 0f;
        perfectCircleState.OrbitAngle += angularStep;
        perfectCircleState.AccumulatedOrbitRadians += math.abs(angularStep);

        float orbitRadius = growthExponent > 0f
            ? spiralStartRadius * math.exp(growthExponent * perfectCircleState.AccumulatedOrbitRadians)
            : spiralStartRadius;

        if (orbitRadius > spiralMaximumRadius)
            orbitRadius = spiralMaximumRadius;

        perfectCircleState.CurrentRadius = orbitRadius;

        if (perfectCircleState.CompletedFullOrbit == 0)
        {
            float despawnAngleThreshold = math.max(0.1f, perfectCircleConfig.SpiralTurnsBeforeDespawn) * (math.PI * 2f);

            if (perfectCircleState.AccumulatedOrbitRadians >= despawnAngleThreshold ||
                orbitRadius + 0.001f >= spiralMaximumRadius)
            {
                perfectCircleState.CompletedFullOrbit = 1;
            }
        }

        float cos = math.cos(perfectCircleState.OrbitAngle);
        float sin = math.sin(perfectCircleState.OrbitAngle);
        float3 orbitOffset = new float3(cos * orbitRadius, 0f, sin * orbitRadius);
        float3 orbitPosition = shooterPosition + orbitOffset;
        orbitPosition.y = shooterPosition.y + perfectCircleConfig.HeightOffset;
        return orbitPosition;
    }

    /// <summary>
    /// Resolves the orbit-entry threshold used to transition from the radial phase to the orbit phase.
    /// /params globalTime Absolute world time used by pulsing-circle mode.
    /// /params perfectCircleConfig Aggregated Perfect Circle configuration.
    /// /returns The radial distance at which the simulated path enters orbit.
    /// </summary>
    private static float ResolveOrbitEntryThreshold(float globalTime,
                                                    in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        switch (perfectCircleConfig.PathMode)
        {
            case ProjectileOrbitPathMode.GoldenSpiral:
                return math.max(0.05f, perfectCircleConfig.SpiralStartRadius);
            default:
                float orbitRadius = ResolveCircularOrbitRadius(globalTime, in perfectCircleConfig);
                float orbitEntryRatio = math.clamp(perfectCircleConfig.OrbitEntryRatio, 0f, 1f);
                return math.max(0.05f, orbitRadius * orbitEntryRatio);
        }
    }

    /// <summary>
    /// Resolves the pulsing circle radius used by circular Perfect Circle mode.
    /// /params globalTime Absolute world time used by the pulsing phase.
    /// /params perfectCircleConfig Aggregated Perfect Circle configuration.
    /// /returns The resolved circular orbit radius at the sampled world time.
    /// </summary>
    private static float ResolveCircularOrbitRadius(float globalTime,
                                                    in PerfectCirclePassiveConfig perfectCircleConfig)
    {
        float minimumRadius = math.max(0f, perfectCircleConfig.OrbitRadiusMin);
        float maximumRadius = math.max(minimumRadius, perfectCircleConfig.OrbitRadiusMax);
        float pulseFrequency = math.max(0f, perfectCircleConfig.OrbitPulseFrequency);
        float pulsePhase = globalTime * pulseFrequency * (math.PI * 2f);
        float pulse = pulseFrequency > 0f ? math.sin(pulsePhase) * 0.5f + 0.5f : 1f;
        return math.lerp(minimumRadius, maximumRadius, pulse);
    }

    /// <summary>
    /// Rebuilds the initial Perfect Circle runtime state exactly as spawned projectiles do before their first simulation step.
    /// /params perfectCircleConfig Aggregated Perfect Circle configuration.
    /// /params laneIndex Stable lane index used as request index surrogate.
    /// /params shooterEntity Player entity used for deterministic seed reconstruction.
    /// /params startPoint World-space origin used as entry origin.
    /// /params direction Initial radial direction of the sampled lane.
    /// /params speedMultiplier Beam-local speed multiplier applied to entry velocity.
    /// /returns One initialized Perfect Circle state ready for sampled simulation.
    /// </summary>
    private static ProjectilePerfectCircleState BuildPerfectCircleState(in PerfectCirclePassiveConfig perfectCircleConfig,
                                                                        int laneIndex,
                                                                        Entity shooterEntity,
                                                                        float3 startPoint,
                                                                        float3 direction,
                                                                        float speedMultiplier)
    {
        float seed = laneIndex + shooterEntity.Index * 13f;
        float angleRadians = math.radians(math.max(0f, perfectCircleConfig.GoldenAngleDegrees) * seed);
        float3 radialDirection = direction;

        if (math.lengthsq(radialDirection) <= DirectionEpsilon)
            radialDirection = new float3(math.cos(angleRadians), 0f, math.sin(angleRadians));

        radialDirection = math.normalizesafe(radialDirection, new float3(0f, 0f, 1f));
        float radialEntrySpeed = math.max(0f, perfectCircleConfig.RadialEntrySpeed * math.max(0f, speedMultiplier));
        return new ProjectilePerfectCircleState
        {
            Enabled = 1,
            HasEnteredOrbit = 0,
            CompletedFullOrbit = 0,
            EntryOrigin = startPoint,
            OrbitAngle = angleRadians,
            OrbitBlendProgress = 0f,
            CurrentRadius = 0f,
            AccumulatedOrbitRadians = 0f,
            RadialDirection = radialDirection,
            EntryVelocity = radialDirection * radialEntrySpeed
        };
    }

    /// <summary>
    /// Marks the final appended segment of the current lane as terminal and propagates optional wall metadata.
    /// /params laneBuffer Output segment buffer that already contains the current lane geometry.
    /// /params laneStartIndex Buffer index where the current lane started appending.
    /// /params terminalBlockedByWall True when the lane ended because of a wall clip.
    /// /params terminalNormal Final wall normal stored on the terminal segment.
    /// /returns None.
    /// </summary>
    private static void FinalizeLaneSegments(ref DynamicBuffer<PlayerLaserBeamLaneElement> laneBuffer,
                                             int laneStartIndex,
                                             bool terminalBlockedByWall,
                                             float3 terminalNormal)
    {
        if (laneBuffer.Length <= laneStartIndex)
            return;

        int lastIndex = laneBuffer.Length - 1;
        PlayerLaserBeamLaneElement lastSegment = laneBuffer[lastIndex];
        lastSegment.IsTerminalSegment = 1;
        lastSegment.TerminalBlockedByWall = terminalBlockedByWall ? (byte)1 : (byte)0;
        lastSegment.TerminalNormal = terminalBlockedByWall ? math.normalizesafe(terminalNormal, float3.zero) : float3.zero;
        laneBuffer[lastIndex] = lastSegment;
    }
    #endregion

    #endregion
}
