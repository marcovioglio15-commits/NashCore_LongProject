using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns optional power-up VFX entities from queued requests.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(PlayerElementalTrailResolveSystem))]
[UpdateAfter(typeof(PlayerPassiveExplosionResolveSystem))]
public partial struct PlayerPowerUpVfxSpawnSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpVfxSpawnRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach (DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests in SystemAPI.Query<DynamicBuffer<PlayerPowerUpVfxSpawnRequest>>())
        {
            for (int requestIndex = 0; requestIndex < vfxRequests.Length; requestIndex++)
            {
                PlayerPowerUpVfxSpawnRequest request = vfxRequests[requestIndex];

                if (request.PrefabEntity == Entity.Null)
                    continue;

                if (entityManager.Exists(request.PrefabEntity) == false)
                    continue;

                Entity vfxEntity = commandBuffer.Instantiate(request.PrefabEntity);
                LocalTransform localTransform = LocalTransform.FromPositionRotationScale(request.Position,
                                                                                        request.Rotation,
                                                                                        math.max(0.01f, request.UniformScale));
                bool prefabHasLocalTransform = entityManager.HasComponent<LocalTransform>(request.PrefabEntity);

                if (prefabHasLocalTransform)
                    commandBuffer.SetComponent(vfxEntity, localTransform);
                else
                    commandBuffer.AddComponent(vfxEntity, localTransform);

                PlayerPowerUpVfxLifetime lifetime = new PlayerPowerUpVfxLifetime
                {
                    RemainingSeconds = math.max(0.01f, request.LifetimeSeconds)
                };

                bool prefabHasLifetime = entityManager.HasComponent<PlayerPowerUpVfxLifetime>(request.PrefabEntity);

                if (prefabHasLifetime)
                    commandBuffer.SetComponent(vfxEntity, lifetime);
                else
                    commandBuffer.AddComponent(vfxEntity, lifetime);
            }

            vfxRequests.Clear();
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #endregion
}
