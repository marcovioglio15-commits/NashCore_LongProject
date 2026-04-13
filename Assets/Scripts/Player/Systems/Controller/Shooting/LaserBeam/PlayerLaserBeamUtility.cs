using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// Provides shared geometry and runtime helpers for the player Laser Beam override.
/// /params None.
/// /returns None.
/// </summary>
public static class PlayerLaserBeamUtility
{
    #region Constants
    internal const float BaseProjectileRadius = 0.05f;
    internal const float MinimumTravelDistance = 0.02f;
    internal const float MinimumCollisionRadius = 0.01f;
    internal const float SurfacePushDistance = 0.01f;
    internal const float DefaultUnboundedBeamDistance = 80f;
    public const float MaximumSupportedTravelDistance = 256f;
    public const float MaximumSupportedCollisionRadius = 8f;
    public const float MaximumSupportedBodyWidth = 12f;
    public const int MaximumSupportedBounceSegments = 12;
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current Laser Beam travel budget from active time, virtual projectile speed and base range or lifetime limits.
    /// /params activeSeconds Consecutive active time accumulated by the beam.
    /// /params projectileSpeed Current effective projectile speed inherited by the beam.
    /// /params rangeLimit Current effective range limit.
    /// /params lifetimeLimit Current effective lifetime limit.
    /// /returns The clamped travel budget used to build the current beam geometry.
    /// </summary>
    internal static float ResolveTravelDistance(float activeSeconds,
                                                float projectileSpeed,
                                                float rangeLimit,
                                                float lifetimeLimit)
    {
        float safeActiveSeconds = IsFinite(activeSeconds) ? math.max(0f, activeSeconds) : 0f;
        float safeProjectileSpeed = IsFinite(projectileSpeed) ? math.max(0f, projectileSpeed) : 0f;
        float travelDistance = safeActiveSeconds * safeProjectileSpeed;

        if (!IsFinite(travelDistance))
            travelDistance = MaximumSupportedTravelDistance;

        float maximumTravelDistance = ResolveMaximumTravelDistance(projectileSpeed, rangeLimit, lifetimeLimit);
        return math.clamp(travelDistance, 0f, maximumTravelDistance);
    }

    /// <summary>
    /// Resolves one effective collision radius from projectile scale and beam-local width tuning.
    /// /params projectileScaleMultiplier Effective projectile scale multiplier inherited from the shooting config.
    /// /params collisionWidthMultiplier Beam-local collision width multiplier.
    /// /returns The effective beam collision radius.
    /// </summary>
    internal static float ResolveCollisionRadius(float projectileScaleMultiplier,
                                                 float collisionWidthMultiplier)
    {
        float collisionRadius = BaseProjectileRadius * math.max(0.01f, projectileScaleMultiplier) * math.max(0.01f, collisionWidthMultiplier);
        return ClampCollisionRadius(collisionRadius);
    }

    /// <summary>
    /// Resolves one effective visual body width from projectile scale and beam-local width tuning.
    /// /params projectileScaleMultiplier Effective projectile scale multiplier inherited from the shooting config.
    /// /params bodyWidthMultiplier Beam-local visual width multiplier.
    /// /returns The effective beam body width used by the presentation system.
    /// </summary>
    internal static float ResolveBodyWidth(float projectileScaleMultiplier,
                                           float bodyWidthMultiplier)
    {
        float bodyWidth = BaseProjectileRadius * 2f * math.max(0.01f, projectileScaleMultiplier) * math.max(0.01f, bodyWidthMultiplier);
        return ClampBodyWidth(bodyWidth);
    }

    /// <summary>
    /// Resolves one evenly spread lane direction from the base look direction and shotgun cone settings.
    /// /params baseDirection Base shoot direction.
    /// /params laneIndex Zero-based lane index.
    /// /params laneCount Total lane count in the current primary emission.
    /// /params coneAngleDegrees Total spread angle in degrees.
    /// /returns The normalized lane direction.
    /// </summary>
    internal static float3 ResolveSpreadDirection(float3 baseDirection,
                                                  int laneIndex,
                                                  int laneCount,
                                                  float coneAngleDegrees)
    {
        float3 normalizedBaseDirection = math.normalizesafe(baseDirection, new float3(0f, 0f, 1f));

        if (laneCount <= 1)
            return normalizedBaseDirection;

        float halfCone = coneAngleDegrees * 0.5f;
        float step = coneAngleDegrees / math.max(1, laneCount - 1);
        float angleDegrees = -halfCone + step * laneIndex;
        quaternion rotationOffset = quaternion.AxisAngle(new float3(0f, 1f, 0f), math.radians(angleDegrees));
        float3 spreadDirection = math.rotate(rotationOffset, normalizedBaseDirection);

        if (math.lengthsq(spreadDirection) <= DirectionEpsilon)
            return normalizedBaseDirection;

        return math.normalizesafe(spreadDirection, normalizedBaseDirection);
    }

    /// <summary>
    /// Clamps one requested straight-lane travel distance to the runtime safety envelope and to the nearest blocking wall when present.
    /// /params startPoint World-space lane origin.
    /// /params direction Requested lane direction.
    /// /params travelDistance Requested travel budget before safety clamping.
    /// /params collisionRadius Effective wall-query radius used by the lane.
    /// /params physicsWorldSingleton Physics world used for wall clipping.
    /// /params wallsCollisionFilter Collision filter used to detect world walls.
    /// /params wallsEnabled True when wall clipping should be evaluated.
    /// /returns Safe travel distance used by the straight-lane builder.
    /// </summary>
    internal static float ClampStraightLaneTravelDistance(float3 startPoint,
                                                          float3 direction,
                                                          float travelDistance,
                                                          float collisionRadius,
                                                          in PhysicsWorldSingleton physicsWorldSingleton,
                                                          in CollisionFilter wallsCollisionFilter,
                                                          bool wallsEnabled)
    {
        float safeTravelDistance = ClampRequestedTravelDistance(travelDistance);

        if (safeTravelDistance < MinimumTravelDistance)
            return 0f;

        if (!IsFinite(startPoint) || !IsFinite(direction))
            return 0f;

        if (!wallsEnabled)
            return safeTravelDistance;

        float3 safeDirection = math.normalizesafe(direction, new float3(0f, 0f, 1f));

        if (WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                    startPoint,
                                                                    safeDirection * safeTravelDistance,
                                                                    ClampCollisionRadius(collisionRadius),
                                                                    wallsCollisionFilter,
                                                                    out float3 allowedDisplacement,
                                                                    out float3 _))
        {
            return math.min(safeTravelDistance, math.length(allowedDisplacement));
        }

        return safeTravelDistance;
    }

    /// <summary>
    /// Clamps one requested travel distance to the runtime safety envelope used by beam geometry and queries.
    /// /params travelDistance Requested travel distance.
    /// /returns Safe travel distance.
    /// </summary>
    internal static float ClampRequestedTravelDistance(float travelDistance)
    {
        if (!IsFinite(travelDistance))
            return 0f;

        return math.clamp(travelDistance, 0f, MaximumSupportedTravelDistance);
    }

    /// <summary>
    /// Clamps one beam collision radius to the runtime safety envelope used by wall queries and hit resolution.
    /// /params collisionRadius Requested collision radius.
    /// /returns Safe collision radius.
    /// </summary>
    internal static float ClampCollisionRadius(float collisionRadius)
    {
        if (!IsFinite(collisionRadius))
            return MinimumCollisionRadius;

        return math.clamp(collisionRadius, MinimumCollisionRadius, MaximumSupportedCollisionRadius);
    }

    /// <summary>
    /// Clamps one beam body width to the runtime safety envelope used by lane storage and presentation.
    /// /params bodyWidth Requested beam body width.
    /// /returns Safe beam body width.
    /// </summary>
    internal static float ClampBodyWidth(float bodyWidth)
    {
        if (!IsFinite(bodyWidth))
            return 0.02f;

        return math.clamp(bodyWidth, 0.02f, MaximumSupportedBodyWidth);
    }

    /// <summary>
    /// Resolves one clipped beam segment against walls and returns the final world-space segment data.
    /// /params startPoint Requested world-space segment start.
    /// /params endPoint Requested world-space segment end.
    /// /params collisionRadius Effective collision radius used for wall casts.
    /// /params physicsWorldSingleton Physics world used for wall casts.
    /// /params wallsCollisionFilter Collision filter used to query world walls.
    /// /params wallsEnabled True when wall clipping should be evaluated.
    /// /params resolvedEndPoint Final segment end after wall clipping.
    /// /params resolvedDirection Final normalized direction after clipping.
    /// /params resolvedLength Final segment length after clipping.
    /// /params hitWall True when the requested segment was clipped by a wall.
    /// /params wallNormal Wall normal returned by the blocking cast when available.
    /// /returns True when the resolved segment still has a usable non-zero length.
    /// </summary>
    internal static bool TryResolveSegment(float3 startPoint,
                                           float3 endPoint,
                                           float collisionRadius,
                                           in PhysicsWorldSingleton physicsWorldSingleton,
                                           in CollisionFilter wallsCollisionFilter,
                                           bool wallsEnabled,
                                           out float3 resolvedEndPoint,
                                           out float3 resolvedDirection,
                                           out float resolvedLength,
                                           out bool hitWall,
                                           out float3 wallNormal)
    {
        if (!IsFinite(startPoint) || !IsFinite(endPoint))
        {
            resolvedEndPoint = startPoint;
            resolvedDirection = new float3(0f, 0f, 1f);
            resolvedLength = 0f;
            hitWall = false;
            wallNormal = float3.zero;
            return false;
        }

        float3 displacement = endPoint - startPoint;
        resolvedEndPoint = startPoint;
        resolvedDirection = math.normalizesafe(displacement, new float3(0f, 0f, 1f));
        resolvedLength = math.length(displacement);
        hitWall = false;
        wallNormal = float3.zero;
        collisionRadius = ClampCollisionRadius(collisionRadius);

        if (resolvedLength < MinimumTravelDistance)
            return false;

        float3 allowedDisplacement = displacement;

        if (wallsEnabled)
        {
            hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                              startPoint,
                                                                              displacement,
                                                                              collisionRadius,
                                                                              wallsCollisionFilter,
                                                                              out allowedDisplacement,
                                                                              out wallNormal);
        }

        resolvedLength = math.length(allowedDisplacement);

        if (resolvedLength < MinimumTravelDistance)
            return false;

        resolvedDirection = math.normalizesafe(allowedDisplacement, resolvedDirection);
        resolvedEndPoint = startPoint + allowedDisplacement;
        return true;
    }

    /// <summary>
    /// Appends one already-resolved Laser Beam segment to the output buffer.
    /// /params laneBuffer Output segment buffer.
    /// /params laneIndex Stable lane index assigned to the segment.
    /// /params isSplitChild True when the segment belongs to a split child lane.
    /// /params startPoint Segment start point.
    /// /params endPoint Segment end point.
    /// /params direction Segment direction.
    /// /params length Segment length.
    /// /params collisionRadius Effective collision radius used by gameplay checks.
    /// /params visualWidth Effective visual width used by the presentation system.
    /// /params damageMultiplier Lane-local damage multiplier.
    /// /params isTerminalSegment True when the segment is the final segment for the lane.
    /// /params terminalBlockedByWall True when the terminal segment ended on a wall clip.
    /// /params terminalNormal Final wall normal stored for debugging and cap logic.
    /// /returns None.
    /// </summary>
    internal static void AppendLaneSegment(ref DynamicBuffer<PlayerLaserBeamLaneElement> laneBuffer,
                                           int laneIndex,
                                           bool isSplitChild,
                                           float3 startPoint,
                                           float3 endPoint,
                                           float3 direction,
                                           float length,
                                           float collisionRadius,
                                           float visualWidth,
                                           float damageMultiplier,
                                           bool isTerminalSegment,
                                           bool terminalBlockedByWall,
                                           float3 terminalNormal)
    {
        if (!IsFinite(startPoint) || !IsFinite(endPoint) || !IsFinite(direction))
            return;

        laneBuffer.Add(new PlayerLaserBeamLaneElement
        {
            LaneIndex = laneIndex,
            IsSplitChild = isSplitChild ? (byte)1 : (byte)0,
            IsTerminalSegment = isTerminalSegment ? (byte)1 : (byte)0,
            TerminalBlockedByWall = terminalBlockedByWall ? (byte)1 : (byte)0,
            StartPoint = startPoint,
            EndPoint = endPoint,
            Direction = direction,
            Length = length,
            CollisionRadius = ClampCollisionRadius(collisionRadius),
            VisualWidth = ClampBodyWidth(visualWidth),
            DamageMultiplier = math.max(0f, damageMultiplier),
            TerminalNormal = terminalBlockedByWall ? math.normalizesafe(terminalNormal, float3.zero) : float3.zero
        });
    }

    /// <summary>
    /// Builds one bounced beam path and appends all resolved segments to the output buffer.
    /// /params laneBuffer Output segment buffer.
    /// /params laneIndex Stable lane index assigned to all appended segments.
    /// /params isSplitChild True when the lane belongs to a split branch.
    /// /params startPoint World-space origin of the lane.
    /// /params direction Initial lane direction.
    /// /params travelDistance Total travel budget available for the lane.
    /// /params collisionRadius Effective collision radius.
    /// /params maximumBounceSegments Maximum reflected wall segments supported by the lane.
    /// /params physicsWorldSingleton Physics world used for wall casts.
    /// /params wallsCollisionFilter Collision filter used to detect world walls.
    /// /params wallsEnabled True when wall tests should be evaluated.
    /// /returns True when at least one segment was appended.
    /// </summary>
    internal static bool TryAppendLaneSegments(ref DynamicBuffer<PlayerLaserBeamLaneElement> laneBuffer,
                                               int laneIndex,
                                               bool isSplitChild,
                                               float3 startPoint,
                                               float3 direction,
                                               float travelDistance,
                                               float collisionRadius,
                                               float visualWidth,
                                               float damageMultiplier,
                                               int maximumBounceSegments,
                                               in PhysicsWorldSingleton physicsWorldSingleton,
                                               in CollisionFilter wallsCollisionFilter,
                                               bool wallsEnabled)
    {
        if (!IsFinite(startPoint) || !IsFinite(direction))
            return false;

        float remainingDistance = ClampRequestedTravelDistance(travelDistance);
        float3 segmentStart = startPoint;
        float3 segmentDirection = math.normalizesafe(direction, new float3(0f, 0f, 1f));
        collisionRadius = ClampCollisionRadius(collisionRadius);
        visualWidth = ClampBodyWidth(visualWidth);

        if (remainingDistance < MinimumTravelDistance)
            return false;

        int appendedSegments = 0;
        int remainingBounces = math.clamp(maximumBounceSegments, 0, MaximumSupportedBounceSegments);

        while (remainingDistance >= MinimumTravelDistance)
        {
            float3 requestedEndPoint = segmentStart + segmentDirection * remainingDistance;

            if (!TryResolveSegment(segmentStart,
                                   requestedEndPoint,
                                   collisionRadius,
                                   in physicsWorldSingleton,
                                   in wallsCollisionFilter,
                                   wallsEnabled,
                                   out float3 resolvedEndPoint,
                                   out float3 resolvedDirection,
                                   out float segmentLength,
                                   out bool hitWall,
                                   out float3 wallNormal))
            {
                break;
            }

            bool isTerminalSegment = !hitWall || remainingBounces <= 0;
            AppendLaneSegment(ref laneBuffer,
                              laneIndex,
                              isSplitChild,
                              segmentStart,
                              resolvedEndPoint,
                              resolvedDirection,
                              segmentLength,
                              collisionRadius,
                              visualWidth,
                              damageMultiplier,
                              isTerminalSegment,
                              hitWall && isTerminalSegment,
                              wallNormal);
            appendedSegments++;
            remainingDistance -= segmentLength;

            if (!hitWall || remainingBounces <= 0)
                break;

            float3 normalizedNormal = math.normalizesafe(wallNormal, float3.zero);

            if (math.lengthsq(normalizedNormal) <= DirectionEpsilon)
                break;

            segmentDirection = math.normalizesafe(math.reflect(segmentDirection, normalizedNormal), segmentDirection);
            segmentStart = resolvedEndPoint + segmentDirection * SurfacePushDistance;
            remainingBounces--;
        }

        return appendedSegments > 0;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the absolute maximum travel distance allowed by the inherited range and lifetime caps.
    /// /params projectileSpeed Current effective projectile speed.
    /// /params rangeLimit Effective range cap.
    /// /params lifetimeLimit Effective lifetime cap.
    /// /returns Maximum beam travel distance before wall clipping.
    /// </summary>
    internal static float ResolveMaximumTravelDistance(float projectileSpeed,
                                                       float rangeLimit,
                                                       float lifetimeLimit)
    {
        float rangeTravelDistance = rangeLimit > 0f ? rangeLimit : float.MaxValue;
        float lifetimeTravelDistance = lifetimeLimit > 0f
            ? math.max(0f, lifetimeLimit) * math.max(0f, projectileSpeed)
            : float.MaxValue;
        float maximumTravelDistance = math.min(rangeTravelDistance, lifetimeTravelDistance);

        if (maximumTravelDistance == float.MaxValue)
            maximumTravelDistance = DefaultUnboundedBeamDistance;

        return math.max(MinimumTravelDistance, ClampRequestedTravelDistance(maximumTravelDistance));
    }

    /// <summary>
    /// Resolves whether one scalar value can be consumed safely by beam math.
    /// /params value Scalar value to validate.
    /// /returns True when the value is finite.
    /// </summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Resolves whether one float3 can be consumed safely by beam math.
    /// /params value Float3 value to validate.
    /// /returns True when every component is finite.
    /// </summary>
    private static bool IsFinite(float3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }
    #endregion

    #endregion
}
