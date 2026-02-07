using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerCameraFollowSystem : ISystem
{
    #region Fields
    private bool hasAutoOffset;
    private float3 autoOffset;
    private bool hasChildOffset;
    private float3 childLocalOffset;
    private CameraBehavior lastBehavior;
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<LocalTransform> localTransform, RefRO<PlayerControllerConfig> controllerConfig) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerControllerConfig>>())
        {
            ref CameraConfig cameraConfig = ref controllerConfig.ValueRO.Config.Value.Camera;

            if (cameraConfig.Behavior == CameraBehavior.RoomFixed)
                continue;

            if (cameraConfig.Behavior != lastBehavior)
            {
                hasAutoOffset = false;
                hasChildOffset = false;
                lastBehavior = cameraConfig.Behavior;
            }

            float3 offset = cameraConfig.FollowOffset;

            switch (cameraConfig.Behavior)
            {
                case CameraBehavior.FollowWithAutoOffset:
                    if (hasAutoOffset == false)
                    {
                        autoOffset = (float3)camera.transform.position - localTransform.ValueRO.Position;
                        hasAutoOffset = true;
                    }

                    offset = autoOffset;
                    break;
                case CameraBehavior.ChildOfPlayer:
                    if (hasChildOffset == false)
                    {
                        float3 worldOffset = (float3)camera.transform.position - localTransform.ValueRO.Position;
                        quaternion inverseRotation = math.inverse(localTransform.ValueRO.Rotation);
                        childLocalOffset = math.rotate(inverseRotation, worldOffset);
                        hasChildOffset = true;
                    }

                    offset = childLocalOffset;
                    break;
            }

            float3 targetPosition = localTransform.ValueRO.Position + offset;

            switch (cameraConfig.Behavior)
            {
                case CameraBehavior.ChildOfPlayer:
                    float3 rotatedOffset = math.rotate(localTransform.ValueRO.Rotation, offset);
                    camera.transform.position = localTransform.ValueRO.Position + rotatedOffset;
                    camera.transform.rotation = localTransform.ValueRO.Rotation;
                    break;
                default:
                    float3 newPosition = PlayerControllerMath.SmoothCameraPosition(camera.transform.position, targetPosition, cameraConfig.Values, deltaTime);
                    camera.transform.position = newPosition;
                    break;
            }

            break;
        }
    }
    #endregion
}
#endregion
