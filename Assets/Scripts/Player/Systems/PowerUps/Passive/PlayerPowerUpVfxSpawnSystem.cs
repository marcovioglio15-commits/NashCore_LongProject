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

        foreach ((DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                  DynamicBuffer<PlayerPowerUpVfxPoolElement> vfxPool)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpVfxSpawnRequest>, DynamicBuffer<PlayerPowerUpVfxPoolElement>>())
        {
            for (int requestIndex = 0; requestIndex < vfxRequests.Length; requestIndex++)
            {
                PlayerPowerUpVfxSpawnRequest request = vfxRequests[requestIndex];

                if (request.PrefabEntity == Entity.Null)
                    continue;

                if (entityManager.Exists(request.PrefabEntity) == false)
                    continue;

                bool reusedInstance;
                Entity vfxEntity = PlayerPowerUpVfxPoolUtility.AcquireVfxEntity(entityManager,
                                                                                ref commandBuffer,
                                                                                vfxPool,
                                                                                request.PrefabEntity,
                                                                                out reusedInstance);

                if (vfxEntity == Entity.Null)
                    continue;

                LocalTransform localTransform = LocalTransform.FromPositionRotationScale(request.Position,
                                                                                        request.Rotation,
                                                                                        math.max(0.01f, request.UniformScale));
                SetOrAddComponent(entityManager,
                                  ref commandBuffer,
                                  vfxEntity,
                                  request.PrefabEntity,
                                  reusedInstance,
                                  localTransform);

                PlayerPowerUpVfxLifetime lifetime = new PlayerPowerUpVfxLifetime
                {
                    RemainingSeconds = math.max(0.01f, request.LifetimeSeconds)
                };
                SetOrAddComponent(entityManager,
                                  ref commandBuffer,
                                  vfxEntity,
                                  request.PrefabEntity,
                                  reusedInstance,
                                  lifetime);
                SetOrAddComponent(entityManager,
                                  ref commandBuffer,
                                  vfxEntity,
                                  request.PrefabEntity,
                                  reusedInstance,
                                  new PlayerPowerUpVfxPooled());

                if (request.FollowTargetEntity != Entity.Null)
                {
                    PlayerPowerUpVfxFollowTarget followTarget = new PlayerPowerUpVfxFollowTarget
                    {
                        TargetEntity = request.FollowTargetEntity,
                        PositionOffset = request.FollowPositionOffset,
                        ValidationEntity = request.FollowValidationEntity,
                        ValidationSpawnVersion = request.FollowValidationSpawnVersion
                    };
                    SetOrAddComponent(entityManager,
                                      ref commandBuffer,
                                      vfxEntity,
                                      request.PrefabEntity,
                                      reusedInstance,
                                      followTarget);
                    commandBuffer.RemoveComponent<PlayerPowerUpVfxVelocity>(vfxEntity);
                }
                else
                {
                    float velocitySquaredLength = math.lengthsq(request.Velocity);

                    if (velocitySquaredLength > 1e-6f)
                    {
                        PlayerPowerUpVfxVelocity velocity = new PlayerPowerUpVfxVelocity
                        {
                            Value = request.Velocity
                        };
                        SetOrAddComponent(entityManager,
                                          ref commandBuffer,
                                          vfxEntity,
                                          request.PrefabEntity,
                                          reusedInstance,
                                          velocity);
                        commandBuffer.RemoveComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity);
                    }
                    else
                    {
                        commandBuffer.RemoveComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity);
                        commandBuffer.RemoveComponent<PlayerPowerUpVfxVelocity>(vfxEntity);
                    }
                }
            }

            vfxRequests.Clear();
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private static void SetOrAddComponent<TComponent>(EntityManager entityManager,
                                                      ref EntityCommandBuffer commandBuffer,
                                                      Entity entity,
                                                      Entity prefabEntity,
                                                      bool entityExistsNow,
                                                      in TComponent component)
        where TComponent : unmanaged, IComponentData
    {
        bool hasComponent;

        if (entityExistsNow)
            hasComponent = entityManager.HasComponent<TComponent>(entity);
        else
            hasComponent = entityManager.HasComponent<TComponent>(prefabEntity);

        if (hasComponent)
            commandBuffer.SetComponent(entity, component);
        else
            commandBuffer.AddComponent(entity, component);
    }
    #endregion

    #endregion
}
