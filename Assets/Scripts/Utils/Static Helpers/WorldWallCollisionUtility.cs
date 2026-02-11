using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Provides shared wall-collision helpers using 3D physics queries on a dedicated wall layer.
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
    public static bool TryResolveBlockedDisplacement(float3 startPosition,
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
        Vector3 rayOrigin = new Vector3(startPosition.x, startPosition.y, startPosition.z);
        Vector3 rayDirection = new Vector3(direction.x, direction.y, direction.z);

        if (Physics.SphereCast(rayOrigin,
                               radius,
                               rayDirection,
                               out RaycastHit hitInfo,
                               distance + ContactSkinWidth,
                               wallsLayerMask,
                               QueryTriggerInteraction.Ignore) == false)
        {
            return false;
        }

        float travelDistance = math.max(0f, hitInfo.distance - ContactSkinWidth);
        allowedDisplacement = direction * travelDistance;

        Vector3 normal = hitInfo.normal;
        hitNormal = new float3(normal.x, normal.y, normal.z);
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

    #endregion
}
