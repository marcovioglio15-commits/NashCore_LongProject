using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Maintains one pooled attached VFX instance on the player while Elemental Trail passive is enabled.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerElementalTrailAttachedVfxSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<PlayerElementalTrailAttachedVfxState>();
        state.RequireForUpdate<PlayerPowerUpVfxPoolElement>();
        state.RequireForUpdate<LocalTransform>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRO<LocalTransform> playerTransform,
                  RefRW<PlayerElementalTrailAttachedVfxState> trailAttachedVfxState,
                  DynamicBuffer<PlayerPowerUpVfxPoolElement> vfxPool)
                 in SystemAPI.Query<RefRO<PlayerPassiveToolsState>,
                                    RefRO<LocalTransform>,
                                    RefRW<PlayerElementalTrailAttachedVfxState>,
                                    DynamicBuffer<PlayerPowerUpVfxPoolElement>>())
        {
            PlayerElementalTrailAttachedVfxState currentTrailVfxState = trailAttachedVfxState.ValueRO;
            ElementalTrailPassiveConfig trailConfig = passiveToolsState.ValueRO.ElementalTrail;
            Entity desiredPrefabEntity = trailConfig.TrailAttachedVfxPrefabEntity;
            bool shouldBeActive = passiveToolsState.ValueRO.HasElementalTrail != 0 && desiredPrefabEntity != Entity.Null;

            if (shouldBeActive == false)
            {
                ReleaseCurrentTrailVfx(entityManager, ref commandBuffer, ref currentTrailVfxState);
                trailAttachedVfxState.ValueRW = currentTrailVfxState;
                continue;
            }

            bool currentVfxIsValid = IsValidEntity(entityManager, currentTrailVfxState.VfxEntity);

            if (currentVfxIsValid && currentTrailVfxState.PrefabEntity != desiredPrefabEntity)
            {
                ReleaseCurrentTrailVfx(entityManager, ref commandBuffer, ref currentTrailVfxState);
                currentVfxIsValid = false;
            }

            bool entityExistsNow = true;

            if (currentVfxIsValid == false)
            {
                bool reusedInstance;
                Entity acquiredVfxEntity = PlayerPowerUpVfxPoolUtility.AcquireVfxEntity(entityManager,
                                                                                         ref commandBuffer,
                                                                                         vfxPool,
                                                                                         desiredPrefabEntity,
                                                                                         out reusedInstance);

                if (acquiredVfxEntity == Entity.Null)
                {
                    trailAttachedVfxState.ValueRW = currentTrailVfxState;
                    continue;
                }

                currentTrailVfxState.VfxEntity = acquiredVfxEntity;
                currentTrailVfxState.PrefabEntity = desiredPrefabEntity;
                entityExistsNow = reusedInstance;
            }
            else if (entityManager.IsEnabled(currentTrailVfxState.VfxEntity) == false)
            {
                commandBuffer.SetEnabled(currentTrailVfxState.VfxEntity, true);
            }

            float vfxScale = math.max(0.01f, trailConfig.TrailAttachedVfxScaleMultiplier * math.max(0.1f, trailConfig.TrailRadius));
            LocalTransform desiredTransform = LocalTransform.FromPositionRotationScale(playerTransform.ValueRO.Position, quaternion.identity, vfxScale);
            SetOrAddComponent(entityManager,
                              ref commandBuffer,
                              currentTrailVfxState.VfxEntity,
                              desiredPrefabEntity,
                              entityExistsNow,
                              desiredTransform);
            SetOrAddComponent(entityManager,
                              ref commandBuffer,
                              currentTrailVfxState.VfxEntity,
                              desiredPrefabEntity,
                              entityExistsNow,
                              new PlayerPowerUpVfxPooled());
            commandBuffer.RemoveComponent<PlayerPowerUpVfxLifetime>(currentTrailVfxState.VfxEntity);
            commandBuffer.RemoveComponent<PlayerPowerUpVfxFollowTarget>(currentTrailVfxState.VfxEntity);
            commandBuffer.RemoveComponent<PlayerPowerUpVfxVelocity>(currentTrailVfxState.VfxEntity);
            trailAttachedVfxState.ValueRW = currentTrailVfxState;
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private static void ReleaseCurrentTrailVfx(EntityManager entityManager,
                                               ref EntityCommandBuffer commandBuffer,
                                               ref PlayerElementalTrailAttachedVfxState trailAttachedVfxState)
    {
        if (IsValidEntity(entityManager, trailAttachedVfxState.VfxEntity))
            PlayerPowerUpVfxPoolUtility.ReleaseVfxEntity(entityManager, ref commandBuffer, trailAttachedVfxState.VfxEntity);

        trailAttachedVfxState.VfxEntity = Entity.Null;
        trailAttachedVfxState.PrefabEntity = Entity.Null;
    }

    private static bool IsValidEntity(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null)
            return false;

        if (entity.Index < 0)
            return false;

        if (entityManager.Exists(entity) == false)
            return false;

        return true;
    }

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
