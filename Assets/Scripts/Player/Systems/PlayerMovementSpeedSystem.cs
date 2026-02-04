using Unity.Entities;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementDirectionSystem))]
public partial struct PlayerMovementSpeedSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerMovementModifiers>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}
#endregion
