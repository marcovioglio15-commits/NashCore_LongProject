using Unity.Entities;
using Unity.Transforms;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementSpeedSystem))]
public partial struct PlayerMovementApplySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<LocalTransform>();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}
#endregion
