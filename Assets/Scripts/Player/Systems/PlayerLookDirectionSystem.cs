using Unity.Entities;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerInputBridgeSystem))]
public partial struct PlayerLookDirectionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}
#endregion
