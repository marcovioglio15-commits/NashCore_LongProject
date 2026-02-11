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
    private const float ContactSkinWidth = 0.01f;
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
    public static bool TryResolveBlockedDisplacement(in PhysicsWorldSingleton physicsWorldSingleton,
                                                     float3 startPosition,
                                                     float3 desiredDisplacement,
                                                     float collisionRadius,
                                                     int wallsLayerMask,
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
        CollisionFilter filter = BuildWallsCollisionFilter(wallsLayerMask);
        float maxDistance = distance + ContactSkinWidth;

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
        float travelDistance = math.max(0f, hitDistance - ContactSkinWidth);
        allowedDisplacement = direction * travelDistance;
        hitNormal = hitInfo.SurfaceNormal;
        return true;
    }

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
    #endregion

    #region Filter Helpers
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
