using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Updates and destroys temporary VFX entities spawned by power-up systems.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpVfxSpawnSystem))]
public partial struct PlayerPowerUpVfxLifetimeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpVfxLifetime>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRW<PlayerPowerUpVfxLifetime> lifetime,
                  Entity vfxEntity)
                 in SystemAPI.Query<RefRW<PlayerPowerUpVfxLifetime>>()
                             .WithEntityAccess())
        {
            float nextRemainingSeconds = lifetime.ValueRO.RemainingSeconds - deltaTime;

            if (nextRemainingSeconds <= 0f)
            {
                commandBuffer.DestroyEntity(vfxEntity);
                continue;
            }

            lifetime.ValueRW.RemainingSeconds = nextRemainingSeconds;
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #endregion
}
