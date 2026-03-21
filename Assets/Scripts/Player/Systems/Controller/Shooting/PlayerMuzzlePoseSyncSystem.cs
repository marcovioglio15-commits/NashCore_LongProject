using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Copies the managed muzzle transform pose into ECS so shooting systems can use an origin aligned with the animated weapon.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateBefore(typeof(PlayerShootingIntentSystem))]
public partial struct PlayerMuzzlePoseSyncSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime data required by the muzzle pose sync.
    /// /params state: Current ECS system state.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerAnimatedMuzzleWorldPose>();
    }

    /// <summary>
    /// Reads the current managed muzzle transform and stores a runtime-safe world pose on each player entity.
    /// /params state: Current ECS system state.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        EntityManager entityManager = state.EntityManager;

        foreach ((RefRW<PlayerAnimatedMuzzleWorldPose> muzzleWorldPose,
                  RefRO<LocalTransform> localTransform,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerAnimatedMuzzleWorldPose>,
                                    RefRO<LocalTransform>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            if (!entityManager.HasComponent<PlayerVisualMuzzleAnchor>(entity))
            {
                ClearPose(ref muzzleWorldPose.ValueRW, in localTransform.ValueRO);
                continue;
            }

            PlayerVisualMuzzleAnchor muzzleAnchor = entityManager.GetComponentObject<PlayerVisualMuzzleAnchor>(entity);

            if (muzzleAnchor == null)
            {
                ClearPose(ref muzzleWorldPose.ValueRW, in localTransform.ValueRO);
                continue;
            }

            Transform muzzleTransform = muzzleAnchor.MuzzleTransform;

            if (muzzleTransform == null)
            {
                ClearPose(ref muzzleWorldPose.ValueRW, in localTransform.ValueRO);
                continue;
            }

            muzzleWorldPose.ValueRW.Position = muzzleTransform.position;
            muzzleWorldPose.ValueRW.Rotation = muzzleTransform.rotation;
            muzzleWorldPose.ValueRW.IsValid = 1;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Clears the animated muzzle pose while preserving a stable fallback from the current player transform.
    /// /params muzzleWorldPose: Mutable runtime muzzle pose to reset.
    /// /params localTransform: Current player transform used to seed the fallback pose.
    /// /returns None.
    /// </summary>
    private static void ClearPose(ref PlayerAnimatedMuzzleWorldPose muzzleWorldPose, in LocalTransform localTransform)
    {
        muzzleWorldPose.Position = localTransform.Position;
        muzzleWorldPose.Rotation = localTransform.Rotation;
        muzzleWorldPose.IsValid = 0;
    }
    #endregion

    #endregion
}
