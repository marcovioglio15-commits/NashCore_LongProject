using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementSpeedSystem))]
public partial struct PlayerMovementApplySystem : ISystem
{
    #region Constants
    private const float PlayerCollisionRadius = 0.35f;
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        bool wallsEnabled = wallsLayerMask != 0;

        foreach ((RefRW<LocalTransform> localTransform, RefRW<PlayerMovementState> movementState) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<PlayerMovementState>>())
        {
            float3 displacement = movementState.ValueRO.Velocity * deltaTime;

            if (math.lengthsq(displacement) < 1e-8f)
                continue;

            if (wallsEnabled)
            {
                float3 currentPosition = localTransform.ValueRO.Position;
                bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                       currentPosition,
                                                                                       displacement,
                                                                                       PlayerCollisionRadius,
                                                                                       wallsLayerMask,
                                                                                       out float3 allowedDisplacement,
                                                                                       out float3 hitNormal);

                localTransform.ValueRW.Position = currentPosition + allowedDisplacement;

                if (hitWall)
                    movementState.ValueRW.Velocity = WorldWallCollisionUtility.RemoveVelocityIntoSurface(movementState.ValueRO.Velocity, hitNormal);

                continue;
            }

            localTransform.ValueRW.Position += displacement;
        }
    }
    #endregion
}
#endregion
