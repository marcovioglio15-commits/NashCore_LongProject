using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Centralizes target following, fallback motion and navigation blending for Coward movement.
/// none.
/// returns none.
/// </summary>
public static class EnemyPatternCowardMovementUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float ClearancePredictionMinimumSeconds = 0.1f;
    private const float PathBlockedAllowanceRatio = 0.94f;
    private const float WallProbeSeconds = 0.28f;
    private const float MinimumWallProbeDistance = 0.45f;
    private const float TargetAlignmentRepathThreshold = 0.18f;
    private const float DeadlockVelocityThreshold = 0.18f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves target-following movement with yield handling and optional retreat-navigation guidance.
    /// enemyEntity Current enemy entity.
    /// selfPriorityTier Current enemy priority tier.
    /// enemyPosition Current enemy position.
    /// bodyRadius Current enemy body radius.
    /// minimumWallDistance Extra distance kept from walls.
    /// desiredSpeed Current movement speed.
    /// steeringAggressiveness Resolved steering aggressiveness scalar.
    /// patternConfig Compiled pattern config.
    /// patternRuntimeState Mutable pattern runtime state.
    /// allowRetreatNavigationGuidance Whether navigation-aware retreat steering may override blocked direct segments.
    /// retryFallbackVelocity Velocity used while retry cooldown is active.
    /// physicsWorldSingleton Physics world singleton.
    /// wallsLayerMask Walls layer mask.
    /// wallsEnabled Whether wall collision checks are enabled.
    /// navigationFlowReady Whether the shared flow field is currently valid.
    /// navigationGridState Shared navigation grid state.
    /// navigationCells Shared navigation cells buffer.
    /// occupancyContext Occupancy context used for clearance and trajectory scoring.
    /// resolvedVelocity Output planar target velocity.
    /// returns True when a target-related velocity was resolved; otherwise false.
    /// </summary>
    public static bool TryResolveVelocityTowardTarget(Entity enemyEntity,
                                                      int selfPriorityTier,
                                                      float3 enemyPosition,
                                                      float bodyRadius,
                                                      float minimumWallDistance,
                                                      float desiredSpeed,
                                                      float steeringAggressiveness,
                                                      in EnemyPatternConfig patternConfig,
                                                      ref EnemyPatternRuntimeState patternRuntimeState,
                                                      bool allowRetreatNavigationGuidance,
                                                      float3 retryFallbackVelocity,
                                                      in PhysicsWorldSingleton physicsWorldSingleton,
                                                      int wallsLayerMask,
                                                      bool wallsEnabled,
                                                      bool navigationFlowReady,
                                                      in EnemyNavigationGridState navigationGridState,
                                                      DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                      in EnemyPatternWandererUtility.OccupancyContext occupancyContext,
                                                      out float3 resolvedVelocity)
    {
        resolvedVelocity = float3.zero;

        if (patternRuntimeState.WanderHasTarget == 0)
            return false;

        float3 toTarget = patternRuntimeState.WanderTargetPosition - enemyPosition;
        toTarget.y = 0f;
        float distanceToTarget = math.length(toTarget);

        if (distanceToTarget <= math.max(0.05f, patternConfig.BasicArrivalTolerance))
        {
            patternRuntimeState.WanderHasTarget = 0;
            patternRuntimeState.WanderWaitTimer = math.max(patternRuntimeState.WanderWaitTimer,
                                                           EnemyPatternCowardSharedUtility.ResolveDecisionCooldown(enemyEntity,
                                                                                                                  0.03f,
                                                                                                                  0.06f));
            return false;
        }

        if (patternRuntimeState.WanderRetryTimer > 0f)
        {
            resolvedVelocity = retryFallbackVelocity;
            return true;
        }

        float3 desiredVelocity = math.normalizesafe(toTarget, float3.zero) * desiredSpeed;

        if (allowRetreatNavigationGuidance)
        {
            desiredVelocity = ResolvePathAwareRetreatVelocity(enemyPosition,
                                                              toTarget,
                                                              bodyRadius,
                                                              minimumWallDistance,
                                                              desiredSpeed,
                                                              in patternConfig,
                                                              in physicsWorldSingleton,
                                                              wallsLayerMask,
                                                              wallsEnabled,
                                                              navigationFlowReady,
                                                              in navigationGridState,
                                                              navigationCells,
                                                              desiredVelocity);
        }

        desiredVelocity = ResolveWallAwareVelocity(enemyEntity,
                                                   enemyPosition,
                                                   desiredVelocity,
                                                   bodyRadius,
                                                   minimumWallDistance,
                                                   desiredSpeed,
                                                   in physicsWorldSingleton,
                                                   wallsLayerMask,
                                                   wallsEnabled);

        float resolvedSteeringAggressiveness = EnemyPatternWandererMovementUtility.ResolveSteeringAggressiveness(steeringAggressiveness);
        float minimumEnemyClearance = EnemyPatternCowardSharedUtility.ResolveRetreatEnemyClearance(bodyRadius,
                                                                                                   patternConfig.BasicMinimumEnemyClearance);
        float predictionTime = math.max(ClearancePredictionMinimumSeconds, patternConfig.BasicTrajectoryPredictionTime);

        if (EnemyPatternWandererMovementUtility.ShouldYieldToNeighbor(enemyEntity,
                                                                     selfPriorityTier,
                                                                     enemyPosition,
                                                                     desiredVelocity,
                                                                     distanceToTarget,
                                                                     bodyRadius,
                                                                     minimumEnemyClearance,
                                                                     predictionTime,
                                                                     resolvedSteeringAggressiveness,
                                                                     in occupancyContext))
        {
            float3 yieldClearanceVelocity = EnemyPatternWandererUtility.ResolveLocalClearanceVelocity(enemyEntity,
                                                                                                      selfPriorityTier,
                                                                                                      enemyPosition,
                                                                                                      bodyRadius,
                                                                                                      minimumEnemyClearance,
                                                                                                      desiredSpeed,
                                                                                                      resolvedSteeringAggressiveness,
                                                                                                      out float _,
                                                                                                      out float _,
                                                                                                      in occupancyContext);
            float3 yieldCorrectionVelocity = EnemyPatternWandererMovementUtility.ComposeYieldCorrectionVelocity(desiredVelocity,
                                                                                                                yieldClearanceVelocity,
                                                                                                                resolvedSteeringAggressiveness);

            if (math.lengthsq(yieldCorrectionVelocity) > DirectionEpsilon)
            {
                float yieldCorrectionRetrySeconds = math.max(0.02f, patternConfig.BasicBlockedPathRetryDelay * 0.45f);
                patternRuntimeState.WanderRetryTimer = math.max(patternRuntimeState.WanderRetryTimer, yieldCorrectionRetrySeconds * 0.55f);
                resolvedVelocity = ResolveWallAwareVelocity(enemyEntity,
                                                            enemyPosition,
                                                            yieldCorrectionVelocity,
                                                            bodyRadius,
                                                            minimumWallDistance,
                                                            desiredSpeed,
                                                            in physicsWorldSingleton,
                                                            wallsLayerMask,
                                                            wallsEnabled);
                return true;
            }

            float yieldRetrySeconds = math.max(0.02f, patternConfig.BasicBlockedPathRetryDelay * 0.45f);
            patternRuntimeState.WanderRetryTimer = math.max(patternRuntimeState.WanderRetryTimer, yieldRetrySeconds);
            resolvedVelocity = float3.zero;
            return true;
        }

        float3 towardTargetDirection = math.normalizesafe(toTarget, float3.zero);

        if (math.lengthsq(towardTargetDirection) > DirectionEpsilon)
        {
            float3 resolvedDirection = math.normalizesafe(desiredVelocity, float3.zero);
            float targetAlignment = math.dot(resolvedDirection, towardTargetDirection);
            bool targetDirectionBroken = targetAlignment < TargetAlignmentRepathThreshold;
            bool targetDeadlocked = math.lengthsq(desiredVelocity) < desiredSpeed * desiredSpeed * DeadlockVelocityThreshold * DeadlockVelocityThreshold;

            if (targetDirectionBroken || targetDeadlocked)
            {
                patternRuntimeState.WanderHasTarget = 0;
                patternRuntimeState.WanderWaitTimer = math.max(patternRuntimeState.WanderWaitTimer,
                                                               EnemyPatternCowardSharedUtility.ResolveDecisionCooldown(enemyEntity,
                                                                                                                      0.02f,
                                                                                                                      0.05f));

                if (math.lengthsq(retryFallbackVelocity) > DirectionEpsilon)
                {
                    resolvedVelocity = retryFallbackVelocity;
                    return true;
                }

                return false;
            }
        }

        resolvedVelocity = desiredVelocity;
        return true;
    }

    /// <summary>
    /// Blends a direct retreat vector with the navigation flow field when direct escape segments are blocked by walls.
    /// enemyPosition Current enemy position.
    /// toTarget Current target displacement.
    /// bodyRadius Current enemy body radius.
    /// minimumWallDistance Extra distance kept from walls.
    /// desiredSpeed Current desired speed.
    /// patternConfig Compiled pattern config.
    /// physicsWorldSingleton Physics world singleton.
    /// wallsLayerMask Walls layer mask.
    /// wallsEnabled Whether wall collision checks are enabled.
    /// navigationFlowReady Whether the shared flow field is currently valid.
    /// navigationGridState Shared navigation grid state.
    /// navigationCells Shared navigation cells buffer.
    /// baseVelocity Direct retreat velocity before navigation blending.
    /// returns Planar velocity corrected with retreat navigation when needed.
    /// </summary>
    public static float3 ResolvePathAwareRetreatVelocity(float3 enemyPosition,
                                                         float3 toTarget,
                                                         float bodyRadius,
                                                         float minimumWallDistance,
                                                         float desiredSpeed,
                                                         in EnemyPatternConfig patternConfig,
                                                         in PhysicsWorldSingleton physicsWorldSingleton,
                                                         int wallsLayerMask,
                                                         bool wallsEnabled,
                                                         bool navigationFlowReady,
                                                         in EnemyNavigationGridState navigationGridState,
                                                         DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                         float3 baseVelocity)
    {
        if (!navigationFlowReady)
            return baseVelocity;

        if (patternConfig.CowardNavigationPreference <= 0f)
            return baseVelocity;

        float collisionRadius = math.max(0.01f, bodyRadius + math.max(0f, minimumWallDistance));
        float navigationBlend = 0f;
        bool shouldUseNavigation = false;

        if (wallsEnabled)
        {
            bool nearWall = WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                                 enemyPosition,
                                                                                 collisionRadius,
                                                                                 wallsLayerMask,
                                                                                 out float3 _,
                                                                                 out float3 _);

            if (nearWall)
            {
                shouldUseNavigation = true;
                navigationBlend = math.max(navigationBlend, math.saturate(0.22f + patternConfig.CowardNavigationPreference * 0.1f));
            }

            bool pathBlocked = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                       enemyPosition,
                                                                                       toTarget,
                                                                                       collisionRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 allowedDisplacement,
                                                                                       out float3 _);

            if (pathBlocked)
            {
                float requestedDistanceSquared = math.lengthsq(toTarget);
                float allowedDistanceSquared = math.lengthsq(allowedDisplacement);

                if (requestedDistanceSquared > DirectionEpsilon &&
                    allowedDistanceSquared < requestedDistanceSquared * PathBlockedAllowanceRatio)
                {
                    shouldUseNavigation = true;
                    navigationBlend = math.max(navigationBlend, math.saturate(0.52f + patternConfig.CowardNavigationPreference * 0.16f));
                }
            }
        }

        if (!shouldUseNavigation)
            return baseVelocity;

        if (!EnemyNavigationFlowFieldUtility.TryResolveRetreatNavigationVelocity(enemyPosition,
                                                                                 desiredSpeed,
                                                                                 in navigationGridState,
                                                                                 navigationCells,
                                                                                 out float3 retreatNavigationVelocity))
        {
            return baseVelocity;
        }

        float3 blendedDirection = math.normalizesafe(baseVelocity + retreatNavigationVelocity * navigationBlend,
                                                     math.normalizesafe(retreatNavigationVelocity, float3.zero));
        return blendedDirection * desiredSpeed;
    }

    /// <summary>
    /// Resolves a short retreat fallback velocity used while the system waits for a better flee destination.
    /// enemyEntity Current enemy entity.
    /// selfPriorityTier Current enemy priority tier.
    /// enemyPosition Current enemy position.
    /// playerPosition Current player position.
    /// bodyRadius Current enemy body radius.
    /// minimumWallDistance Extra distance kept from walls.
    /// desiredSpeed Current movement speed.
    /// steeringAggressiveness Resolved steering aggressiveness scalar.
    /// elapsedTime Elapsed world time.
    /// patternConfig Compiled pattern config.
    /// physicsWorldSingleton Physics world singleton.
    /// wallsLayerMask Walls layer mask.
    /// wallsEnabled Whether wall collision checks are enabled.
    /// navigationFlowReady Whether the shared flow field is currently valid.
    /// navigationGridState Shared navigation grid state.
    /// navigationCells Shared navigation cells buffer.
    /// occupancyContext Occupancy context used for local clearance.
    /// returns Planar fallback flee velocity.
    /// </summary>
    public static float3 ResolveRetreatFallbackVelocity(Entity enemyEntity,
                                                        int selfPriorityTier,
                                                        float3 enemyPosition,
                                                        float3 playerPosition,
                                                        float bodyRadius,
                                                        float minimumWallDistance,
                                                        float desiredSpeed,
                                                        float steeringAggressiveness,
                                                        float elapsedTime,
                                                        in EnemyPatternConfig patternConfig,
                                                        in PhysicsWorldSingleton physicsWorldSingleton,
                                                        int wallsLayerMask,
                                                        bool wallsEnabled,
                                                        bool navigationFlowReady,
                                                        in EnemyNavigationGridState navigationGridState,
                                                        DynamicBuffer<EnemyNavigationCellElement> navigationCells,
                                                        in EnemyPatternWandererUtility.OccupancyContext occupancyContext)
    {
        float3 retreatDirection = EnemyPatternCowardSharedUtility.ResolveRetreatDirection(enemyEntity,
                                                                                          enemyPosition,
                                                                                          playerPosition,
                                                                                          elapsedTime);
        float3 baseVelocity = retreatDirection * math.max(0f, desiredSpeed) * 0.72f;
        baseVelocity = ResolvePathAwareRetreatVelocity(enemyPosition,
                                                       retreatDirection,
                                                       bodyRadius,
                                                       minimumWallDistance,
                                                       math.max(0f, desiredSpeed),
                                                       in patternConfig,
                                                       in physicsWorldSingleton,
                                                       wallsLayerMask,
                                                       wallsEnabled,
                                                       navigationFlowReady,
                                                       in navigationGridState,
                                                       navigationCells,
                                                       baseVelocity);
        return ComposeFallbackVelocity(enemyEntity,
                                       selfPriorityTier,
                                       enemyPosition,
                                       bodyRadius,
                                       math.max(0f, patternConfig.BasicMinimumEnemyClearance),
                                       desiredSpeed,
                                       steeringAggressiveness,
                                       baseVelocity,
                                       in occupancyContext);
    }

    /// <summary>
    /// Applies a cheap continuous wall-avoidance correction so Cowards stop scraping along walls and escape corners earlier.
    /// enemyEntity Current enemy entity.
    /// enemyPosition Current enemy position.
    /// desiredVelocity Base desired velocity before wall correction.
    /// bodyRadius Current enemy body radius.
    /// minimumWallDistance Extra distance kept from walls.
    /// desiredSpeed Current desired speed magnitude.
    /// physicsWorldSingleton Physics world singleton.
    /// wallsLayerMask Walls layer mask.
    /// wallsEnabled Whether wall checks are enabled.
    /// returns Wall-aware planar velocity.
    /// </summary>
    public static float3 ResolveWallAwareVelocity(Entity enemyEntity,
                                                  float3 enemyPosition,
                                                  float3 desiredVelocity,
                                                  float bodyRadius,
                                                  float minimumWallDistance,
                                                  float desiredSpeed,
                                                  in PhysicsWorldSingleton physicsWorldSingleton,
                                                  int wallsLayerMask,
                                                  bool wallsEnabled)
    {
        if (!wallsEnabled)
            return desiredVelocity;

        if (desiredSpeed <= DirectionEpsilon)
            return desiredVelocity;

        float clearanceRadius = math.max(0.01f, bodyRadius + math.max(0f, minimumWallDistance));
        float3 correctedVelocity = desiredVelocity;
        bool violatesCurrentClearance = WorldWallCollisionUtility.TryResolveMinimumClearance(physicsWorldSingleton,
                                                                                             enemyPosition,
                                                                                             clearanceRadius,
                                                                                             wallsLayerMask,
                                                                                             out float3 correctionDisplacement,
                                                                                             out float3 currentWallNormal);

        if (violatesCurrentClearance)
        {
            float3 wallEscapeDirection = math.normalizesafe(correctionDisplacement, float3.zero);
            correctedVelocity = math.normalizesafe(correctedVelocity + wallEscapeDirection * desiredSpeed * 1.35f,
                                                   wallEscapeDirection) * desiredSpeed;
        }

        float3 probeDirection = math.normalizesafe(correctedVelocity, float3.zero);

        if (math.lengthsq(probeDirection) <= DirectionEpsilon)
        {
            if (!violatesCurrentClearance)
                return correctedVelocity;

            float3 escapeDirection = math.normalizesafe(new float3(currentWallNormal.x, 0f, currentWallNormal.z), float3.zero);
            return escapeDirection * desiredSpeed;
        }

        float probeDistance = math.max(MinimumWallProbeDistance, desiredSpeed * WallProbeSeconds);
        bool blocked = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                               enemyPosition,
                                                                               probeDirection * probeDistance,
                                                                               clearanceRadius,
                                                                               wallsLayerMask,
                                                                               out float3 allowedDisplacement,
                                                                               out float3 hitNormal);

        if (!blocked)
            return correctedVelocity;

        float blockedRatio = EnemyPatternMovementRuntimeUtility.ResolveBlockedDisplacementRatio(probeDistance,
                                                                                                math.length(allowedDisplacement));

        if (blockedRatio <= DirectionEpsilon)
            return correctedVelocity;

        float3 escapeWallNormal = hitNormal;

        if (violatesCurrentClearance)
            escapeWallNormal = math.normalizesafe(currentWallNormal + hitNormal, hitNormal);

        float3 slideVelocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(correctedVelocity, escapeWallNormal);
        float3 slideDirection = math.normalizesafe(slideVelocity, float3.zero);

        if (math.lengthsq(slideDirection) <= DirectionEpsilon)
            slideDirection = EnemyPatternCowardSharedUtility.ResolveWallTangentDirection(enemyEntity, escapeWallNormal);

        float3 wallNormalDirection = math.normalizesafe(new float3(escapeWallNormal.x, 0f, escapeWallNormal.z), float3.zero);
        float normalBias = violatesCurrentClearance
            ? 0.2f + blockedRatio * 0.72f
            : 0.28f + blockedRatio * 1.15f;
        float3 blendedDirection = math.normalizesafe(slideDirection + wallNormalDirection * normalBias,
                                                     wallNormalDirection);
        return blendedDirection * desiredSpeed;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Composes one fallback velocity with local clearance so nearby Cowards keep dancing instead of stacking.
    /// enemyEntity Current enemy entity.
    /// selfPriorityTier Current enemy priority tier.
    /// enemyPosition Current enemy position.
    /// bodyRadius Current enemy body radius.
    /// minimumEnemyClearance Minimum clearance from neighbors.
    /// desiredSpeed Current desired speed.
    /// steeringAggressiveness Resolved steering aggressiveness scalar.
    /// baseVelocity Base fallback velocity before clearance.
    /// occupancyContext Occupancy context used for local clearance.
    /// returns Clearance-aware fallback velocity.
    /// </summary>
    private static float3 ComposeFallbackVelocity(Entity enemyEntity,
                                                  int selfPriorityTier,
                                                  float3 enemyPosition,
                                                  float bodyRadius,
                                                  float minimumEnemyClearance,
                                                  float desiredSpeed,
                                                  float steeringAggressiveness,
                                                  float3 baseVelocity,
                                                  in EnemyPatternWandererUtility.OccupancyContext occupancyContext)
    {
        float effectiveMinimumEnemyClearance = EnemyPatternCowardSharedUtility.ResolveRetreatEnemyClearance(bodyRadius,
                                                                                                             minimumEnemyClearance);
        float3 clearanceVelocity = EnemyPatternWandererUtility.ResolveLocalClearanceVelocity(enemyEntity,
                                                                                             selfPriorityTier,
                                                                                             enemyPosition,
                                                                                             bodyRadius,
                                                                                             effectiveMinimumEnemyClearance,
                                                                                             desiredSpeed,
                                                                                             steeringAggressiveness,
                                                                                             out float _,
                                                                                             out float _,
                                                                                             in occupancyContext);

        if (math.lengthsq(clearanceVelocity) <= DirectionEpsilon)
            return baseVelocity;

        return EnemyPatternWandererMovementUtility.ComposeYieldCorrectionVelocity(baseVelocity,
                                                                                  clearanceVelocity,
                                                                                  steeringAggressiveness);
    }
    #endregion

    #endregion
}
