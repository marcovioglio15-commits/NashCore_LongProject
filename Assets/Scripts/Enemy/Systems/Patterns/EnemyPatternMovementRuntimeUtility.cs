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
    /// /params patternConfig: Current compiled pattern configuration.
    /// /params enemyData: Current immutable enemy data.
    /// /returns Additional wall-clearance radius in meters.
    /// </summary>
    public static float ResolveWallClearanceForMovement(in EnemyPatternConfig patternConfig, in EnemyData enemyData)
    {
        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererBasic &&
            patternConfig.MovementKind != EnemyCompiledMovementPatternKind.Coward)
            return 0f;

        return math.max(0f, enemyData.MinimumWallDistance);
    }

    /// <summary>
    /// Computes how much requested displacement was blocked by collision resolution.
    /// /params requestedDistance: Requested displacement length.
    /// /params allowedDistance: Allowed displacement length.
    /// /returns Blocked ratio in the [0..1] range.
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
    /// /params baseVelocity: Current desired planar velocity before wall comfort correction.
    /// /params enemyPosition: Current enemy world position.
    /// /params bodyRadius: Current immutable body radius.
    /// /params minimumWallDistance: Additional authored wall clearance distance.
    /// /params desiredSpeed: Desired planar speed magnitude used for the comfort correction.
    /// /params physicsWorldSingleton: Physics world singleton used for wall distance queries.
    /// /params wallsLayerMask: Walls layer mask.
    /// /params wallsEnabled: Whether wall queries are enabled.
    /// /params wallComfortPressure: Output normalized wall-pressure scalar in the [0..1] range.
    /// /returns Corrected planar velocity that biases movement away from the wall comfort shell.
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
    /// /params patternRuntimeState: Mutable Wanderer runtime state.
    /// /params patternConfig: Current compiled pattern configuration.
    /// /returns None.
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
    /// /params enemyEntity: Current enemy entity.
    /// /params patternConfig: Current compiled pattern configuration.
    /// /params patternRuntimeState: Mutable Wanderer runtime state.
    /// /params moveSpeed: Resolved movement speed.
    /// /params maxSpeed: Resolved maximum movement speed.
    /// /params elapsedTime: Elapsed world time in seconds.
    /// /returns Desired planar velocity for the current frame.
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
    /// /params bodyRadius: Current immutable body radius.
    /// /params minimumWallDistance: Additional authored wall clearance distance.
    /// /returns Extra comfort shell thickness in meters.
    /// </summary>
    private static float ResolveWallComfortBand(float bodyRadius, float minimumWallDistance)
    {
        float distanceDrivenBand = minimumWallDistance * 0.55f;
        float bodyDrivenBand = math.max(0.05f, bodyRadius) * 0.18f;
        float resolvedBand = math.max(MinimumWallComfortBand, math.max(distanceDrivenBand, bodyDrivenBand));
        return math.min(minimumWallDistance, resolvedBand);
    }
    #endregion

    #endregion
}
