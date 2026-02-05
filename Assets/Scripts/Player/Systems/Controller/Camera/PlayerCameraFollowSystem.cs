using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerCameraFollowSystem : ISystem
{
    private bool m_HasAutoOffset;
    private float3 m_AutoOffset;
    private bool m_HasChildOffset;
    private float3 m_ChildLocalOffset;
    private CameraBehavior m_LastBehavior;

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

            if (cameraConfig.Behavior != m_LastBehavior)
            {
                m_HasAutoOffset = false;
                m_HasChildOffset = false;
                m_LastBehavior = cameraConfig.Behavior;
            }

            float3 offset = cameraConfig.FollowOffset;

            switch (cameraConfig.Behavior)
            {
                case CameraBehavior.FollowWithAutoOffset:
                    if (m_HasAutoOffset == false)
                    {
                        m_AutoOffset = (float3)camera.transform.position - localTransform.ValueRO.Position;
                        m_HasAutoOffset = true;
                    }

                    offset = m_AutoOffset;
                    break;
                case CameraBehavior.ChildOfPlayer:
                    if (m_HasChildOffset == false)
                    {
                        float3 worldOffset = (float3)camera.transform.position - localTransform.ValueRO.Position;
                        quaternion inverseRotation = math.inverse(localTransform.ValueRO.Rotation);
                        m_ChildLocalOffset = math.rotate(inverseRotation, worldOffset);
                        m_HasChildOffset = true;
                    }

                    offset = m_ChildLocalOffset;
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
