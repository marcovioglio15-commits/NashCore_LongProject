using Unity.Entities;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerCameraFollowSystem))]
public partial struct PlayerCameraRoomAnchorSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerCameraAnchor>();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}
#endregion
