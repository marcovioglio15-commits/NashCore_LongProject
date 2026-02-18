using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerCameraRoomAnchorSystem : ISystem
{
    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCameraAnchor>();
    }

    public void OnUpdate(ref SystemState state)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach ((RefRO<PlayerCameraAnchor> cameraAnchor, RefRO<PlayerControllerConfig> controllerConfig) in SystemAPI.Query<RefRO<PlayerCameraAnchor>, RefRO<PlayerControllerConfig>>())
        {
            ref CameraConfig cameraConfig = ref controllerConfig.ValueRO.Config.Value.Camera;

            switch (cameraConfig.Behavior)
            {
                case CameraBehavior.RoomFixed:
                    break;
                default:
                    continue;
            }

            Entity anchorEntity = cameraAnchor.ValueRO.AnchorEntity;

            if (state.EntityManager.Exists(anchorEntity) == false)
                continue;

            float3 anchorPosition;

            if (localToWorldLookup.HasComponent(anchorEntity))
                anchorPosition = localToWorldLookup[anchorEntity].Position;
            else if (localTransformLookup.HasComponent(anchorEntity))
                anchorPosition = localTransformLookup[anchorEntity].Position;
            else
                continue;

            float3 newPosition = PlayerControllerMath.SmoothCameraPosition(camera.transform.position, anchorPosition, cameraConfig.Values, deltaTime);
            camera.transform.position = newPosition;
            break;
        }
    }

    #endregion

}
