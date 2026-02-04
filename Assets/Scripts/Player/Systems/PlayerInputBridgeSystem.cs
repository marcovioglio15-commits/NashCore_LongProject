using Unity.Entities;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
public partial struct PlayerInputBridgeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}
#endregion
