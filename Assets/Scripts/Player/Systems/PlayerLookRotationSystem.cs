using Unity.Entities;
using Unity.Transforms;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookMultiplierSystem))]
public partial struct PlayerLookRotationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}
#endregion
