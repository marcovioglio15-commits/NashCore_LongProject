using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Updates temporary VFX entities and releases expired pooled instances.
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
        state.EntityManager.CompleteDependencyBeforeRO<LocalTransform>();
        state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
        state.EntityManager.CompleteDependencyBeforeRO<EnemyRuntimeState>();
        state.EntityManager.CompleteDependencyBeforeRO<EnemyActive>();
        ComponentLookup<LocalTransform> targetLocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<LocalToWorld> targetLocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        ComponentLookup<EnemyRuntimeState> enemyRuntimeLookup = SystemAPI.GetComponentLookup<EnemyRuntimeState>(true);
        ComponentLookup<EnemyActive> enemyActiveLookup = SystemAPI.GetComponentLookup<EnemyActive>(true);

        foreach ((RefRW<PlayerPowerUpVfxLifetime> lifetime,
                  RefRW<LocalTransform> vfxTransform,
                  RefRO<PlayerPowerUpVfxFollowTarget> followTarget,
                  Entity vfxEntity)
                 in SystemAPI.Query<RefRW<PlayerPowerUpVfxLifetime>,
                                    RefRW<LocalTransform>,
                                    RefRO<PlayerPowerUpVfxFollowTarget>>()
                             .WithEntityAccess())
        {
            Entity targetEntity = followTarget.ValueRO.TargetEntity;

            if (followTarget.ValueRO.ValidationSpawnVersion > 0u)
            {
                Entity validationEntity = followTarget.ValueRO.ValidationEntity;

                if (validationEntity == Entity.Null || enemyRuntimeLookup.HasComponent(validationEntity) == false)
                {
                    ReleaseOrDestroyVfxEntity(state.EntityManager, ref commandBuffer, vfxEntity);
                    continue;
                }

                if (enemyActiveLookup.HasComponent(validationEntity) == false ||
                    enemyActiveLookup.IsComponentEnabled(validationEntity) == false)
                {
                    ReleaseOrDestroyVfxEntity(state.EntityManager, ref commandBuffer, vfxEntity);
                    continue;
                }

                EnemyRuntimeState validationRuntimeState = enemyRuntimeLookup[validationEntity];

                if (validationRuntimeState.SpawnVersion != followTarget.ValueRO.ValidationSpawnVersion)
                {
                    ReleaseOrDestroyVfxEntity(state.EntityManager, ref commandBuffer, vfxEntity);
                    continue;
                }
            }

            if (targetEntity == Entity.Null)
            {
                ReleaseOrDestroyVfxEntity(state.EntityManager, ref commandBuffer, vfxEntity);
                continue;
            }

            float3 targetPosition;

            if (targetLocalToWorldLookup.HasComponent(targetEntity))
            {
                LocalToWorld targetLocalToWorld = targetLocalToWorldLookup[targetEntity];
                targetPosition = targetLocalToWorld.Position;
            }
            else if (targetLocalTransformLookup.HasComponent(targetEntity))
            {
                LocalTransform targetTransform = targetLocalTransformLookup[targetEntity];
                targetPosition = targetTransform.Position;
            }
            else
            {
                ReleaseOrDestroyVfxEntity(state.EntityManager, ref commandBuffer, vfxEntity);
                continue;
            }

            LocalTransform currentTransform = vfxTransform.ValueRO;
            currentTransform.Position = targetPosition + followTarget.ValueRO.PositionOffset;
            vfxTransform.ValueRW = currentTransform;

            PlayerPowerUpVfxLifetime currentLifetime = lifetime.ValueRO;
            bool shouldDestroy = ConsumeLifetime(ref currentLifetime, deltaTime);

            if (shouldDestroy)
            {
                ReleaseOrDestroyVfxEntity(state.EntityManager, ref commandBuffer, vfxEntity);
                continue;
            }

            lifetime.ValueRW = currentLifetime;
        }

        foreach ((RefRW<PlayerPowerUpVfxLifetime> lifetime,
                  RefRW<LocalTransform> vfxTransform,
                  RefRO<PlayerPowerUpVfxVelocity> velocity,
                  Entity vfxEntity)
                 in SystemAPI.Query<RefRW<PlayerPowerUpVfxLifetime>,
                                    RefRW<LocalTransform>,
                                    RefRO<PlayerPowerUpVfxVelocity>>()
                             .WithNone<PlayerPowerUpVfxFollowTarget>()
                             .WithEntityAccess())
        {
            LocalTransform currentTransform = vfxTransform.ValueRO;
            currentTransform.Position += velocity.ValueRO.Value * deltaTime;
            vfxTransform.ValueRW = currentTransform;

            PlayerPowerUpVfxLifetime currentLifetime = lifetime.ValueRO;
            bool shouldDestroy = ConsumeLifetime(ref currentLifetime, deltaTime);

            if (shouldDestroy)
            {
                ReleaseOrDestroyVfxEntity(state.EntityManager, ref commandBuffer, vfxEntity);
                continue;
            }

            lifetime.ValueRW = currentLifetime;
        }

        foreach ((RefRW<PlayerPowerUpVfxLifetime> lifetime,
                  Entity vfxEntity)
                 in SystemAPI.Query<RefRW<PlayerPowerUpVfxLifetime>>()
                             .WithNone<PlayerPowerUpVfxFollowTarget, PlayerPowerUpVfxVelocity>()
                             .WithEntityAccess())
        {
            PlayerPowerUpVfxLifetime currentLifetime = lifetime.ValueRO;
            bool shouldDestroy = ConsumeLifetime(ref currentLifetime, deltaTime);

            if (shouldDestroy)
            {
                ReleaseOrDestroyVfxEntity(state.EntityManager, ref commandBuffer, vfxEntity);
                continue;
            }

            lifetime.ValueRW = currentLifetime;
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private static bool ConsumeLifetime(ref PlayerPowerUpVfxLifetime lifetime, float deltaTime)
    {
        float nextRemainingSeconds = lifetime.RemainingSeconds - deltaTime;

        if (nextRemainingSeconds <= 0f)
            return true;

        lifetime.RemainingSeconds = nextRemainingSeconds;
        return false;
    }

    private static void ReleaseOrDestroyVfxEntity(EntityManager entityManager,
                                                  ref EntityCommandBuffer commandBuffer,
                                                  Entity vfxEntity)
    {
        if (entityManager.HasComponent<PlayerPowerUpVfxPooled>(vfxEntity))
        {
            PlayerPowerUpVfxPoolUtility.ReleaseVfxEntity(entityManager, ref commandBuffer, vfxEntity);
            return;
        }

        commandBuffer.DestroyEntity(vfxEntity);
    }
    #endregion

    #endregion
}
