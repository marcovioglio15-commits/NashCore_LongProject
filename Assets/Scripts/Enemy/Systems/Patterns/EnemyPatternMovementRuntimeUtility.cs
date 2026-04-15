using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Centralizes runtime helpers shared by custom enemy movement patterns.
/// </summary>
public static class EnemyPatternMovementRuntimeUtility
{
    #region Constants
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float DirectionEpsilon = 1e-6f;
    private const float MinimumWallComfortBand = 0.06f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves additional wall-clearance radius enforced during runtime movement.
    /// patternConfig: Current compiled pattern configuration.
    /// enemyData: Current immutable enemy data.
    /// returns Additional wall-clearance radius in meters.
    /// </summary>
    public static float ResolveWallClearanceForMovement(in EnemyPatternConfig patternConfig, in EnemyData enemyData)
    {
        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererBasic &&
            patternConfig.MovementKind != EnemyCompiledMovementPatternKind.Coward)
            return 0f;

        return math.max(0f, enemyData.MinimumWallDistance);
    }

    /// <summary>
    /// Resolves whether the optional short-range interaction is currently active by using activation distance plus release hysteresis.
    /// patternConfig: Current compiled pattern configuration.
    /// patternRuntimeState: Current mutable pattern runtime state.
    /// playerDistance: Current planar distance from enemy to player.
    /// returns True when the short-range interaction should drive the active movement slot.
    /// </summary>
    public static bool ResolveShortRangeInteractionActive(in EnemyPatternConfig patternConfig,
                                                          in EnemyPatternRuntimeState patternRuntimeState,
                                                          float playerDistance)
    {
        if (patternConfig.HasShortRangeInteraction == 0)
            return false;

        float activationRange = math.max(0f, patternConfig.ShortRangeActivationRange);
        float releaseDistance = activationRange + math.max(0f, patternConfig.ShortRangeReleaseDistanceBuffer);

        if (patternRuntimeState.ShortRangeInteractionActive != 0)
            return playerDistance <= releaseDistance;

        return playerDistance <= activationRange;
    }

    /// <summary>
    /// Resolves the movement kind that should currently be considered active after short-range overrides.
    /// patternConfig: Current compiled pattern configuration.
    /// patternRuntimeState: Current mutable pattern runtime state.
    /// playerDistance: Current planar distance from enemy to player.
    /// returns Active movement kind for this frame.
    /// </summary>
    public static EnemyCompiledMovementPatternKind ResolveActiveMovementKind(in EnemyPatternConfig patternConfig,
                                                                             in EnemyPatternRuntimeState patternRuntimeState,
                                                                             float playerDistance)
    {
        if (patternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash &&
            EnemyPatternShortRangeDashUtility.IsCommitted(in patternRuntimeState))
        {
            return EnemyCompiledMovementPatternKind.ShortRangeDash;
        }

        bool shortRangeInteractionActive = ResolveShortRangeInteractionActive(in patternConfig, in patternRuntimeState, playerDistance);

        if (!shortRangeInteractionActive)
            return patternConfig.MovementKind;

        if (patternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash &&
            !EnemyPatternShortRangeDashUtility.IsAvailableForTakeover(in patternRuntimeState))
        {
            return patternConfig.MovementKind;
        }

        return patternConfig.ShortRangeMovementKind;
    }

    /// <summary>
    /// Returns whether the current frame is driven by a non-grunt short-range interaction override.
    /// patternConfig: Current compiled pattern configuration.
    /// patternRuntimeState: Current mutable pattern runtime state.
    /// shortRangeInteractionActive: Whether the short-range interaction is active this frame.
    /// returns True when the short-range override currently owns movement decisions.
    /// </summary>
    public static bool IsShortRangeOverrideDriving(in EnemyPatternConfig patternConfig,
                                                   in EnemyPatternRuntimeState patternRuntimeState,
                                                   bool shortRangeInteractionActive)
    {
        if (patternConfig.HasShortRangeInteraction == 0)
            return false;

        if (patternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash &&
            EnemyPatternShortRangeDashUtility.IsCommitted(in patternRuntimeState))
        {
            return true;
        }

        if (!shortRangeInteractionActive)
            return false;

        if (patternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash)
            return EnemyPatternShortRangeDashUtility.IsAvailableForTakeover(in patternRuntimeState);

        return patternConfig.ShortRangeMovementKind != EnemyCompiledMovementPatternKind.Grunt;
    }

    /// <summary>
    /// Clears transient long-range target state when a short-range interaction takes over movement.
    /// patternConfig: Current compiled pattern configuration.
    /// patternRuntimeState: Current mutable pattern runtime state.
    /// returns None.
    /// </summary>
    public static void PrepareShortRangeTakeover(in EnemyPatternConfig patternConfig,
                                                 ref EnemyPatternRuntimeState patternRuntimeState)
    {
        if (patternConfig.HasShortRangeInteraction == 0)
            return;

        patternRuntimeState.WanderHasTarget = 0;
        patternRuntimeState.WanderWaitTimer = 0f;
        patternRuntimeState.WanderRetryTimer = 0f;
    }

    /// <summary>
    /// Builds the effective pattern config used by movement systems after short-range overrides are resolved.
    /// patternConfig: Current compiled pattern configuration.
    /// patternRuntimeState: Current mutable pattern runtime state.
    /// playerDistance: Current planar distance from enemy to player.
    /// returns Effective pattern config for the current frame.
    /// </summary>
    public static EnemyPatternConfig BuildActivePatternConfig(in EnemyPatternConfig patternConfig,
                                                              in EnemyPatternRuntimeState patternRuntimeState,
                                                              float playerDistance)
    {
        if (patternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash &&
            EnemyPatternShortRangeDashUtility.IsCommitted(in patternRuntimeState))
        {
            EnemyPatternConfig committedDashPatternConfig = patternConfig;
            committedDashPatternConfig.MovementKind = EnemyCompiledMovementPatternKind.ShortRangeDash;
            return committedDashPatternConfig;
        }

        bool shortRangeInteractionActive = ResolveShortRangeInteractionActive(in patternConfig, in patternRuntimeState, playerDistance);

        if (!shortRangeInteractionActive)
            return patternConfig;

        if (patternConfig.ShortRangeMovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash &&
            !EnemyPatternShortRangeDashUtility.IsAvailableForTakeover(in patternRuntimeState))
        {
            return patternConfig;
        }

        EnemyPatternConfig activePatternConfig = patternConfig;

        switch (patternConfig.ShortRangeMovementKind)
        {
            case EnemyCompiledMovementPatternKind.Coward:
                ApplyShortRangeCowardOverride(ref activePatternConfig);
                break;

            case EnemyCompiledMovementPatternKind.ShortRangeDash:
                activePatternConfig.MovementKind = EnemyCompiledMovementPatternKind.ShortRangeDash;
                break;

            case EnemyCompiledMovementPatternKind.Grunt:
                activePatternConfig.MovementKind = EnemyCompiledMovementPatternKind.Grunt;
                break;
        }

        return activePatternConfig;
    }

    /// <summary>
    /// Resolves whether the effective movement for the current frame still requires the custom movement system.
    /// patternConfig: Current compiled pattern configuration.
    /// patternRuntimeState: Current mutable pattern runtime state.
    /// playerDistance: Current planar distance from enemy to player.
    /// returns True when custom movement should stay in control for this frame.
    /// </summary>
    public static bool UsesCustomMovement(in EnemyPatternConfig patternConfig,
                                          in EnemyPatternRuntimeState patternRuntimeState,
                                          float playerDistance)
    {
        EnemyCompiledMovementPatternKind activeMovementKind = ResolveActiveMovementKind(in patternConfig, in patternRuntimeState, playerDistance);
        return activeMovementKind != EnemyCompiledMovementPatternKind.Grunt;
    }

    /// <summary>
    /// Computes how much requested displacement was blocked by collision resolution.
    /// requestedDistance: Requested displacement length.
    /// allowedDistance: Allowed displacement length.
    /// returns Blocked ratio in the [0..1] range.
    /// </summary>
    public static float ResolveBlockedDisplacementRatio(float requestedDistance, float allowedDistance)
    {
        if (requestedDistance <= DirectionEpsilon)
            return 0f;

        float allowedRatio = math.saturate(allowedDistance / requestedDistance);
        return 1f - allowedRatio;
    }

    /// <summary>
    /// Resolves a lightweight wall-discomfort response used by Wanderer-based patterns to leave tight wall-adjacent spots early.
    /// baseVelocity: Current desired planar velocity before wall comfort correction.
    /// enemyPosition: Current enemy world position.
    /// bodyRadius: Current immutable body radius.
    /// minimumWallDistance: Additional authored wall clearance distance.
    /// desiredSpeed: Desired planar speed magnitude used for the comfort correction.
    /// physicsWorldSingleton: Physics world singleton used for wall distance queries.
    /// wallsLayerMask: Walls layer mask.
    /// wallsEnabled: Whether wall queries are enabled.
    /// wallComfortPressure: Output normalized wall-pressure scalar in the [0..1] range.
    /// returns Corrected planar velocity that biases movement away from the wall comfort shell.
    /// </summary>
    public static float3 ResolveWallComfortVelocity(float3 baseVelocity,
                                                    float3 enemyPosition,
                                                    float bodyRadius,
                                                    float minimumWallDistance,
                                                    float desiredSpeed,
                                                    in PhysicsWorldSingleton physicsWorldSingleton,
                                                    int wallsLayerMask,
                                                    bool wallsEnabled,
                                                    out float wallComfortPressure)
    {
        wallComfortPressure = 0f;

        if (!wallsEnabled)
            return baseVelocity;

        float resolvedMinimumWallDistance = math.max(0f, minimumWallDistance);

        if (resolvedMinimumWallDistance <= DirectionEpsilon)
            return baseVelocity;

        float comfortBand = ResolveWallComfortBand(bodyRadius, resolvedMinimumWallDistance);

        if (comfortBand <= DirectionEpsilon)
            return baseVelocity;

        float planarBaseSpeed = math.length(new float3(baseVelocity.x, 0f, baseVelocity.z));
        float resolvedSpeed = math.max(math.max(0f, desiredSpeed), planarBaseSpeed);

        if (resolvedSpeed <= DirectionEpsilon)
            return baseVelocity;

        float clearanceRadius = math.max(0.01f, math.max(0.05f, bodyRadius) + resolvedMinimumWallDistance);
        float comfortRadius = clearanceRadius + comfortBand;
        bool insideComfortShell = WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                                       enemyPosition,
                                                                                       comfortRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 correctionDisplacement,
                                                                                       out float3 hitNormal);

        if (!insideComfortShell)
            return baseVelocity;

        float correctionDistance = math.length(correctionDisplacement);

        if (correctionDistance <= DirectionEpsilon)
            return baseVelocity;

        wallComfortPressure = math.saturate(correctionDistance / comfortBand);
        float3 wallNormalDirection = math.normalizesafe(new float3(hitNormal.x, 0f, hitNormal.z),
                                                        math.normalizesafe(new float3(correctionDisplacement.x, 0f, correctionDisplacement.z), float3.zero));

        if (math.lengthsq(wallNormalDirection) <= DirectionEpsilon)
            return baseVelocity;

        float3 planarVelocity = new float3(baseVelocity.x, 0f, baseVelocity.z);
        float3 surfaceSafeVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(planarVelocity, wallNormalDirection);
        float outwardSpeed = resolvedSpeed * math.lerp(0.38f, 1.2f, wallComfortPressure);
        float3 resolvedDirection = math.normalizesafe(surfaceSafeVelocity + wallNormalDirection * outwardSpeed, wallNormalDirection);
        return resolvedDirection * resolvedSpeed;
    }

    /// <summary>
    /// Consumes the active Wanderer target when wall clearance invalidates remaining progress.
    /// patternRuntimeState: Mutable Wanderer runtime state.
    /// patternConfig: Current compiled pattern configuration.
    /// returns None.
    /// </summary>
    public static void ConsumeWanderTargetOnClearance(ref EnemyPatternRuntimeState patternRuntimeState,
                                                      in EnemyPatternConfig patternConfig)
    {
        if (patternRuntimeState.WanderHasTarget == 0)
            return;

        patternRuntimeState.WanderHasTarget = 0;
        patternRuntimeState.WanderWaitTimer = math.min(0.08f, math.max(0f, patternConfig.BasicWaitCooldownSeconds * 0.2f));
        patternRuntimeState.WanderRetryTimer = math.max(0.02f, patternConfig.BasicBlockedPathRetryDelay * 0.5f);
    }

    /// <summary>
    /// Resolves current Wanderer DVD desired velocity and initializes the first direction if needed.
    /// enemyEntity: Current enemy entity.
    /// patternConfig: Current compiled pattern configuration.
    /// patternRuntimeState: Mutable Wanderer runtime state.
    /// moveSpeed: Resolved movement speed.
    /// maxSpeed: Resolved maximum movement speed.
    /// elapsedTime: Elapsed world time in seconds.
    /// returns Desired planar velocity for the current frame.
    /// </summary>
    public static float3 ResolveWandererDvdVelocity(Entity enemyEntity,
                                                    in EnemyPatternConfig patternConfig,
                                                    ref EnemyPatternRuntimeState patternRuntimeState,
                                                    float moveSpeed,
                                                    float maxSpeed,
                                                    float elapsedTime)
    {
        if (patternRuntimeState.DvdInitialized == 0)
        {
            float angleDegrees = patternConfig.DvdFixedInitialDirectionDegrees;

            if (patternConfig.DvdRandomizeInitialDirection != 0)
            {
                uint seed = math.hash(new int3(enemyEntity.Index, enemyEntity.Version, (int)(elapsedTime * 13f)));
                int diagonalIndex = (int)(seed % 4u);

                switch (diagonalIndex)
                {
                    case 0:
                        angleDegrees = 45f;
                        break;

                    case 1:
                        angleDegrees = 135f;
                        break;

                    case 2:
                        angleDegrees = 225f;
                        break;

                    default:
                        angleDegrees = 315f;
                        break;
                }
            }

            float radians = math.radians(angleDegrees);
            patternRuntimeState.DvdDirection = math.normalizesafe(new float3(math.sin(radians), 0f, math.cos(radians)), ForwardAxis);
            patternRuntimeState.DvdInitialized = 1;
        }

        float movementSpeed = maxSpeed > 0f ? math.min(moveSpeed, maxSpeed) : moveSpeed;
        movementSpeed *= math.max(0f, patternConfig.DvdSpeedMultiplier);
        return patternRuntimeState.DvdDirection * movementSpeed;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the extra comfort shell added over the authored wall clearance so wall-adjacent wanderers start leaving earlier.
    /// bodyRadius: Current immutable body radius.
    /// minimumWallDistance: Additional authored wall clearance distance.
    /// returns Extra comfort shell thickness in meters.
    /// </summary>
    private static float ResolveWallComfortBand(float bodyRadius, float minimumWallDistance)
    {
        float distanceDrivenBand = minimumWallDistance * 0.55f;
        float bodyDrivenBand = math.max(0.05f, bodyRadius) * 0.18f;
        float resolvedBand = math.max(MinimumWallComfortBand, math.max(distanceDrivenBand, bodyDrivenBand));
        return math.min(minimumWallDistance, resolvedBand);
    }

    /// <summary>
    /// Copies the short-range coward category fields into the legacy coward slots consumed by the existing movement helpers.
    /// activePatternConfig Mutable active pattern config for the current frame.
    /// returns None.
    /// </summary>
    private static void ApplyShortRangeCowardOverride(ref EnemyPatternConfig activePatternConfig)
    {
        activePatternConfig.MovementKind = EnemyCompiledMovementPatternKind.Coward;
        activePatternConfig.BasicSearchRadius = math.max(0.5f, activePatternConfig.ShortRangeSearchRadius);
        activePatternConfig.BasicMinimumTravelDistance = math.max(0f, activePatternConfig.ShortRangeMinimumTravelDistance);
        activePatternConfig.BasicMaximumTravelDistance = math.max(activePatternConfig.BasicMinimumTravelDistance,
                                                                 activePatternConfig.ShortRangeMaximumTravelDistance);
        activePatternConfig.BasicArrivalTolerance = math.max(0.05f, activePatternConfig.ShortRangeArrivalTolerance);
        activePatternConfig.BasicWaitCooldownSeconds = 0f;
        activePatternConfig.BasicCandidateSampleCount = math.clamp(math.max(1, activePatternConfig.ShortRangeCandidateSampleCount), 1, 64);
        activePatternConfig.BasicUseInfiniteDirectionSampling = activePatternConfig.ShortRangeUseInfiniteDirectionSampling;
        activePatternConfig.BasicInfiniteDirectionStepDegrees = math.clamp(activePatternConfig.ShortRangeInfiniteDirectionStepDegrees, 0.5f, 90f);
        activePatternConfig.BasicUnexploredDirectionPreference = 0f;
        activePatternConfig.BasicTowardPlayerPreference = 0f;
        activePatternConfig.BasicMinimumEnemyClearance = math.max(0f, activePatternConfig.ShortRangeMinimumEnemyClearance);
        activePatternConfig.BasicTrajectoryPredictionTime = math.max(0f, activePatternConfig.ShortRangeTrajectoryPredictionTime);
        activePatternConfig.BasicFreeTrajectoryPreference = math.max(0f, activePatternConfig.ShortRangeFreeTrajectoryPreference);
        activePatternConfig.BasicBlockedPathRetryDelay = math.max(0f, activePatternConfig.ShortRangeBlockedPathRetryDelay);
        activePatternConfig.CowardDetectionRadius = math.max(0f,
                                                             activePatternConfig.ShortRangeActivationRange +
                                                             math.max(0f, activePatternConfig.ShortRangeReleaseDistanceBuffer));
        activePatternConfig.CowardReleaseDistanceBuffer = 0f;
        activePatternConfig.CowardRetreatDirectionPreference = math.saturate(activePatternConfig.ShortRangeRetreatDirectionPreference);
        activePatternConfig.CowardOpenSpacePreference = math.saturate(activePatternConfig.ShortRangeOpenSpacePreference);
        activePatternConfig.CowardNavigationPreference = math.saturate(activePatternConfig.ShortRangeNavigationPreference);
        activePatternConfig.CowardPatrolRadius = 0f;
        activePatternConfig.CowardPatrolWaitSeconds = 0f;
        activePatternConfig.CowardPatrolSpeedMultiplier = 0f;
        activePatternConfig.CowardRetreatSpeedMultiplierFar = math.max(0f, activePatternConfig.ShortRangeRetreatSpeedMultiplierFar);
        activePatternConfig.CowardRetreatSpeedMultiplierNear = math.max(activePatternConfig.CowardRetreatSpeedMultiplierFar,
                                                                        activePatternConfig.ShortRangeRetreatSpeedMultiplierNear);
    }
    #endregion

    #endregion
}
