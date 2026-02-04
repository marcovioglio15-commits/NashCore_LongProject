using Unity.Entities;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerInputBridgeSystem))]
public partial struct PlayerMovementDirectionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}
#endregion
