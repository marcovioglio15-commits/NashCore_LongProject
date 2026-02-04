using Unity.Entities;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookDirectionSystem))]
public partial struct PlayerLookMultiplierSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerMovementModifiers>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}
#endregion
