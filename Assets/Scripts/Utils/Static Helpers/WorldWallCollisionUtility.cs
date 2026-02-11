using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

/// <summary>
/// Provides shared wall-collision helpers using DOTS Unity Physics queries on a dedicated wall layer.
/// </summary>
public static class WorldWallCollisionUtility
{
    #region Constants
    public const string DefaultWallsLayerName = "Walls";

    private const float MinimumTravelDistance = 1e-6f;
    private const float MinimumSweepRadius = 0.001f;
    private const float ContactSkinWidth = 0.02f;
    private const float BlockingDotThreshold = -1e-4f;
    #endregion

    #region Fields
    private static int cachedWallsLayerMask = int.MinValue;
#if UNITY_EDITOR
    private static bool warnedMissingWallsLayer;
#endif
    #endregion

    #region Methods

    #region Layer Resolution
    public static int ResolveWallsLayerMask()
    {
        if (cachedWallsLayerMask != int.MinValue)
            return cachedWallsLayerMask;

        int layerIndex = LayerMask.NameToLayer(DefaultWallsLayerName);

        if (layerIndex < 0)
        {
            cachedWallsLayerMask = 0;

#if UNITY_EDITOR
            if (warnedMissingWallsLayer == false)
            {
                warnedMissingWallsLayer = true;
                Debug.LogWarning("[WorldWallCollisionUtility] Missing 'Walls' layer. Wall collision checks are disabled until the layer is created.");
            }
#endif
            return cachedWallsLayerMask;
        }

        cachedWallsLayerMask = 1 << layerIndex;
        return cachedWallsLayerMask;
    }
    #endregion

    #region Collision Queries
    /// <summary>
    /// This method attempts to resolve a desired displacement for an object with a given collision radius,
    /// starting from a specified position, 
    /// while checking for potential collisions against walls defined by a layer mask. 
    /// If a collision is
    /// </summary>
    /// <param name="physicsWorldSingleton"></param>
    /// <param name="startPosition"></param>
    /// <param name="desiredDisplacement"></param>
    /// <param name="collisionRadius"></param>
    /// <param name="wallsLayerMask"></param>
    /// <param name="allowedDisplacement"></param>
    /// <param name="hitNormal"></param>
    public static bool TryResolveBlockedDisplacement(in PhysicsWorldSingleton physicsWorldSingleton,
                                                     float3 startPosition,
                                                     float3 desiredDisplacement,
                                                     float collisionRadius,
                                                     int wallsLayerMask,
                                                     out float3 allowedDisplacement,
                                                     out float3 hitNormal)
    {
        return TryResolveBlockedDisplacement(physicsWorldSingleton,
                                             startPosition,
                                             desiredDisplacement,
                                             collisionRadius,
                                             wallsLayerMask,
                                             ContactSkinWidth,
                                             out allowedDisplacement,
                                             out hitNormal);
    }


    /// <summary>
    /// This method attempts to resolve a desired displacement for an object with a given collision radius, 
    /// and a specified contact skin width, starting from a specified position, 
    /// while checking for potential collisions against walls defined by a layer mask.
    /// </summary>
    /// <param name="physicsWorldSingleton"></param>
    /// <param name="startPosition"></param>
    /// <param name="desiredDisplacement"></param>
    /// <param name="collisionRadius"></param>
    /// <param name="wallsLayerMask"></param>
    /// <param name="contactSkinWidth"></param>
    /// <param name="allowedDisplacement"></param>
    /// <param name="hitNormal"></param>
    /// <returns></returns>
    public static bool TryResolveBlockedDisplacement(in PhysicsWorldSingleton physicsWorldSingleton,
                                                     float3 startPosition,
                                                     float3 desiredDisplacement,
                                                     float collisionRadius,
                                                     int wallsLayerMask,
                                                     float contactSkinWidth,
                                                     out float3 allowedDisplacement,
                                                     out float3 hitNormal)
    {
        allowedDisplacement = desiredDisplacement;
        hitNormal = float3.zero;

        if (wallsLayerMask == 0)
            return false;

        float distance = math.length(desiredDisplacement);

        if (distance <= MinimumTravelDistance)
            return false;

        float3 direction = desiredDisplacement / distance;
        float radius = math.max(MinimumSweepRadius, collisionRadius);
        float clampedContactSkinWidth = math.max(0f, contactSkinWidth);
        CollisionFilter filter = BuildWallsCollisionFilter(wallsLayerMask);
        float maxDistance = distance + clampedContactSkinWidth;

        if (physicsWorldSingleton.SphereCast(startPosition,
                                             radius,
                                             direction,
                                             maxDistance,
                                             out ColliderCastHit hitInfo,
                                             filter,
                                             QueryInteraction.IgnoreTriggers) == false)
        {
            return false;
        }

        float hitDistance = hitInfo.Fraction * maxDistance;
        float approachDot = math.dot(direction, hitInfo.SurfaceNormal);

        if (approachDot >= BlockingDotThreshold)
            return false;

        float travelDistance = math.max(0f, hitDistance - clampedContactSkinWidth);
        float depenetrationDistance = math.max(0f, clampedContactSkinWidth - hitDistance);
        allowedDisplacement = direction * travelDistance + hitInfo.SurfaceNormal * depenetrationDistance;
        hitNormal = hitInfo.SurfaceNormal;
        return true;
    }

    /// <summary>
    /// This method checks if a given position is within a specified minimum clearance distance from any walls defined by a layer mask,
    /// if it is, it calculates a correction displacement to move the position outside of the minimum clearance distance,
    /// and it returns the normal of the closest wall surface.
    /// </summary>
    /// <param name="physicsWorldSingleton"></param>
    /// <param name="position"></param>
    /// <param name="minimumClearanceDistance"></param>
    /// <param name="wallsLayerMask"></param>
    /// <param name="correctionDisplacement"></param>
    /// <param name="hitNormal"></param>
    /// <returns></returns>
    public static bool TryResolveMinimumClearance(in PhysicsWorldSingleton physicsWorldSingleton,
                                                  float3 position,
                                                  float minimumClearanceDistance,
                                                  int wallsLayerMask,
                                                  out float3 correctionDisplacement,
                                                  out float3 hitNormal)
    {
        correctionDisplacement = float3.zero;
        hitNormal = float3.zero;

        if (wallsLayerMask == 0)
            return false;

        float clampedMinimumClearance = math.max(0f, minimumClearanceDistance);

        if (clampedMinimumClearance <= 1e-6f)
            return false;

        PointDistanceInput distanceInput = new PointDistanceInput
        {
            Position = position,
            MaxDistance = clampedMinimumClearance,
            Filter = BuildWallsCollisionFilter(wallsLayerMask)
        };

        if (physicsWorldSingleton.CalculateDistance(distanceInput, out DistanceHit distanceHit) == false)
            return false;

        float requiredCorrectionDistance = clampedMinimumClearance - distanceHit.Distance;

        if (requiredCorrectionDistance <= 1e-6f)
            return false;

        float3 normal = math.normalizesafe(distanceHit.SurfaceNormal, float3.zero);

        if (math.lengthsq(normal) <= 1e-6f)
            normal = math.normalizesafe(position - distanceHit.Position, float3.zero);

        if (math.lengthsq(normal) <= 1e-6f)
            return false;

        correctionDisplacement = normal * requiredCorrectionDistance;
        hitNormal = normal;
        return true;
    }

    /// <summary>
    /// This method takes an input velocity and a surface normal, 
    /// and it removes any component of the velocity that is directed into the surface defined by the normal.
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="surfaceNormal"></param>
    /// <returns></returns>
    public static float3 RemoveVelocityIntoSurface(float3 velocity, float3 surfaceNormal)
    {
        float normalLengthSquared = math.lengthsq(surfaceNormal);

        if (normalLengthSquared <= 1e-6f)
            return velocity;

        float3 normalizedSurfaceNormal = math.normalize(surfaceNormal);
        float normalVelocity = math.dot(velocity, normalizedSurfaceNormal);

        if (normalVelocity >= 0f)
            return velocity;

        return velocity - normalizedSurfaceNormal * normalVelocity;
    }

    /// <summary>
    /// This method computes a new velocity vector after a collision with a surface, given the incoming velocity,
    /// and the normal of the surface, and a bounce coefficient that determines how much of the velocity is retained after the bounce.
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="surfaceNormal"></param>
    /// <param name="bounceCoefficient"></param>
    /// <returns></returns>
    public static float3 ComputeBounceVelocity(float3 velocity, float3 surfaceNormal, float bounceCoefficient)
    {
        float normalLengthSquared = math.lengthsq(surfaceNormal);

        if (normalLengthSquared <= 1e-6f)
            return velocity;

        float clampedBounceCoefficient = math.clamp(bounceCoefficient, 0f, 1f);

        if (clampedBounceCoefficient <= 1e-6f)
            return RemoveVelocityIntoSurface(velocity, surfaceNormal);

        float3 normalizedSurfaceNormal = math.normalize(surfaceNormal);
        float normalVelocity = math.dot(velocity, normalizedSurfaceNormal);

        if (normalVelocity >= 0f)
            return velocity;

        float3 tangentialVelocity = velocity - normalizedSurfaceNormal * normalVelocity;
        float bouncedNormalVelocity = -normalVelocity * clampedBounceCoefficient;
        return tangentialVelocity + normalizedSurfaceNormal * bouncedNormalVelocity;
    }
    #endregion

    #region Filter Helpers
    /// <summary>
    /// This method constructs a CollisionFilter for wall collision 
    /// queries based on the provided walls layer mask (from PlayerWorldLayersConfig or resolved default).
    /// </summary>
    /// <param name="wallsLayerMask"></param>
    /// <returns></returns>
    public static CollisionFilter BuildWallsCollisionFilter(int wallsLayerMask)
    {
        uint collidesWithMask = wallsLayerMask > 0 ? (uint)wallsLayerMask : 0u;

        if (collidesWithMask == 0u)
            return CollisionFilter.Zero;

        return new CollisionFilter
        {
            BelongsTo = uint.MaxValue,
            CollidesWith = collidesWithMask,
            GroupIndex = 0
        };
    }
    #endregion

    #endregion
}
