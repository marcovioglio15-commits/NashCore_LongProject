using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves the authoritative world-space projectile origin for player-fired shots.
/// Animated muzzle data is preferred, then baked muzzle anchors, then the player transform fallback.
/// /params None.
/// /returns None.
/// </summary>
public static class PlayerShootOriginUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the world-space spawn position for one player-fired projectile while preserving authored shoot offset.
    /// /params shooterEntity: Player entity requesting the projectile spawn.
    /// /params shooterTransform: Current player transform used as the final fallback reference pose.
    /// /params shootOffset: Authored local shoot offset rotated by the resolved muzzle rotation.
    /// /params animatedMuzzleLookup: Lookup used to read the runtime animated muzzle pose.
    /// /params muzzleLookup: Lookup used to read the baked ECS muzzle anchor entity.
    /// /params transformLookup: Lookup used to read fallback LocalTransform data from the baked muzzle anchor.
    /// /params localToWorldLookup: Lookup used to read the most accurate world pose from the baked muzzle anchor.
    /// /returns World-space projectile spawn position.
    /// </summary>
    public static float3 ResolveSpawnPosition(Entity shooterEntity,
                                              in LocalTransform shooterTransform,
                                              in float3 shootOffset,
                                              in ComponentLookup<PlayerAnimatedMuzzleWorldPose> animatedMuzzleLookup,
                                              in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                              in ComponentLookup<LocalTransform> transformLookup,
                                              in ComponentLookup<LocalToWorld> localToWorldLookup)
    {
        float3 referencePosition = shooterTransform.Position;
        quaternion referenceRotation = shooterTransform.Rotation;

        if (TryResolveAnimatedMuzzlePose(shooterEntity,
                                         in animatedMuzzleLookup,
                                         out referencePosition,
                                         out referenceRotation))
        {
            float3 animatedOffset = math.rotate(referenceRotation, shootOffset);
            return referencePosition + animatedOffset;
        }

        if (TryResolveBakedMuzzlePose(shooterEntity,
                                      in muzzleLookup,
                                      in transformLookup,
                                      in localToWorldLookup,
                                      out referencePosition,
                                      out referenceRotation))
        {
            float3 bakedOffset = math.rotate(referenceRotation, shootOffset);
            return referencePosition + bakedOffset;
        }

        float3 fallbackOffset = math.rotate(referenceRotation, shootOffset);
        return referencePosition + fallbackOffset;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Attempts to read the animated muzzle pose cached on the shooter entity.
    /// /params shooterEntity: Player entity requesting the spawn origin.
    /// /params animatedMuzzleLookup: Lookup used to read the cached animated muzzle pose.
    /// /params position: Resolved animated muzzle position.
    /// /params rotation: Resolved animated muzzle rotation.
    /// /returns True when a valid animated muzzle pose exists, otherwise false.
    /// </summary>
    private static bool TryResolveAnimatedMuzzlePose(Entity shooterEntity,
                                                     in ComponentLookup<PlayerAnimatedMuzzleWorldPose> animatedMuzzleLookup,
                                                     out float3 position,
                                                     out quaternion rotation)
    {
        if (animatedMuzzleLookup.HasComponent(shooterEntity))
        {
            PlayerAnimatedMuzzleWorldPose animatedMuzzlePose = animatedMuzzleLookup[shooterEntity];

            if (animatedMuzzlePose.IsValid != 0)
            {
                position = animatedMuzzlePose.Position;
                rotation = animatedMuzzlePose.Rotation;
                return true;
            }
        }

        position = float3.zero;
        rotation = quaternion.identity;
        return false;
    }

    /// <summary>
    /// Attempts to resolve the baked ECS muzzle anchor pose when no animated muzzle pose is available.
    /// /params shooterEntity: Player entity requesting the spawn origin.
    /// /params muzzleLookup: Lookup used to read the baked muzzle anchor entity.
    /// /params transformLookup: Lookup used to read LocalTransform fallback data for the anchor.
    /// /params localToWorldLookup: Lookup used to read the world-space anchor transform.
    /// /params position: Resolved baked muzzle position.
    /// /params rotation: Resolved baked muzzle rotation.
    /// /returns True when a valid baked muzzle pose exists, otherwise false.
    /// </summary>
    private static bool TryResolveBakedMuzzlePose(Entity shooterEntity,
                                                  in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                                  in ComponentLookup<LocalTransform> transformLookup,
                                                  in ComponentLookup<LocalToWorld> localToWorldLookup,
                                                  out float3 position,
                                                  out quaternion rotation)
    {
        if (muzzleLookup.HasComponent(shooterEntity))
        {
            Entity muzzleEntity = muzzleLookup[shooterEntity].AnchorEntity;

            if (localToWorldLookup.HasComponent(muzzleEntity))
            {
                LocalToWorld localToWorld = localToWorldLookup[muzzleEntity];
                position = localToWorld.Value.c3.xyz;
                rotation = quaternion.LookRotationSafe(localToWorld.Value.c2.xyz, localToWorld.Value.c1.xyz);
                return true;
            }

            if (transformLookup.HasComponent(muzzleEntity))
            {
                LocalTransform muzzleTransform = transformLookup[muzzleEntity];
                position = muzzleTransform.Position;
                rotation = muzzleTransform.Rotation;
                return true;
            }
        }

        position = float3.zero;
        rotation = quaternion.identity;
        return false;
    }
    #endregion

    #endregion
}
