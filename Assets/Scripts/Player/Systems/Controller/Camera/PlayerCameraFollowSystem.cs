using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// This system is responsible for updating the main camera's position to follow the 
/// player based on the configuration specified in the PlayerControllerConfig component.
/// </summary>
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
    /// <summary>
    /// Configures the system to require updates 
    /// for entities that have the PlayerControllerConfig component, which contains
    /// a reference to the camera configuration that determines how the camera should follow the player.
    /// </summary>
    /// <param name="state"></param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    /// <summary>
    /// Updates the main camera's position based on the player's position and the specified camera behavior 
    /// in the PlayerControllerConfig. In detail it calculates the desired camera position using 
    /// the player's position and the configured follow offset,
    /// then smoothly moves the camera towards that position using the SmoothCameraPosition method from 
    /// PlayerControllerMath. It also handles different camera behaviors, such as maintaining
    /// a fixed offset or being a child of the player, 
    /// and ensures that the camera's position is updated accordingly.
    /// </summary>
    /// <param name="state"></param>
    public void OnUpdate(ref SystemState state)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;

        // Only support one player camera config at a time, so breaks after the first iteration.
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

            // For FollowWithAutoOffset, we calculate the initial offset from the camera to the player
            // and maintain that offset.
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

            // Handle camera behavior modes. For ChildOfPlayer, directly sets the camera's position and rotation
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
