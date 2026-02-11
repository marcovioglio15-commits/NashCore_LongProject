using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Advances bomb fuse timers and requests explosion when fuse reaches zero.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerBombSpawnSystem))]
public partial struct PlayerBombFuseSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BombFuseState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRW<BombFuseState> fuseState,
                  Entity bombEntity) in SystemAPI.Query<RefRW<BombFuseState>>().WithEntityAccess())
        {
            if (fuseState.ValueRO.FuseRemaining > deltaTime)
            {
                fuseState.ValueRW.FuseRemaining -= deltaTime;
                continue;
            }

            fuseState.ValueRW.FuseRemaining = 0f;

            if (entityManager.HasComponent<BombExplodeRequest>(bombEntity))
                continue;

            commandBuffer.AddComponent<BombExplodeRequest>(bombEntity);
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #endregion
}
