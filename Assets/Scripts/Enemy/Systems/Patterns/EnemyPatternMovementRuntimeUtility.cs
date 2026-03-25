using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes runtime helpers shared by custom enemy movement patterns.
/// </summary>
public static class EnemyPatternMovementRuntimeUtility
{
    #region Constants
    private static readonly float3 ForwardAxis = new float3(0f, 0f, 1f);
    private const float DirectionEpsilon = 1e-6f;
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
        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererBasic)
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

    #endregion
}
