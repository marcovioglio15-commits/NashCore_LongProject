using Unity.Collections;
using Unity.Entities;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerControllerInitializeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRO<PlayerControllerConfig> _, Entity entity) in SystemAPI.Query<RefRO<PlayerControllerConfig>>().WithEntityAccess())
        {

            if (state.EntityManager.HasComponent<PlayerInputState>(entity) == false)
            {
                commandBuffer.AddComponent(entity, new PlayerInputState());
            }

            if (state.EntityManager.HasComponent<PlayerMovementState>(entity) == false)
            {
                commandBuffer.AddComponent(entity, new PlayerMovementState());
            }

            if (state.EntityManager.HasComponent<PlayerLookState>(entity) == false)
            {
                commandBuffer.AddComponent(entity, new PlayerLookState());
            }

            if (state.EntityManager.HasComponent<PlayerMovementModifiers>(entity) == false)
            {
                commandBuffer.AddComponent(entity, new PlayerMovementModifiers
                {
                    MaxSpeedMultiplier = 1f,
                    AccelerationMultiplier = 1f
                });
            }
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
}
#endregion
