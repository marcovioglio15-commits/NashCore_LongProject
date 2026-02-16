using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Finalizes queued enemy despawns and returns them to the owning pools.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyDespawnSystem))]
public partial struct EnemyFinalizeDespawnSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyDespawnRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        ComponentLookup<EnemyOwnerSpawner> ownerLookup = SystemAPI.GetComponentLookup<EnemyOwnerSpawner>(true);
        ComponentLookup<EnemyRuntimeState> runtimeLookup = SystemAPI.GetComponentLookup<EnemyRuntimeState>(false);
        ComponentLookup<EnemyHealth> healthLookup = SystemAPI.GetComponentLookup<EnemyHealth>(false);
        ComponentLookup<EnemySpawnerState> spawnerStateLookup = SystemAPI.GetComponentLookup<EnemySpawnerState>(false);
        BufferLookup<EnemyPoolElement> poolLookup = SystemAPI.GetBufferLookup<EnemyPoolElement>(false);

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRO<EnemyDespawnRequest> _,
                  Entity enemyEntity) in SystemAPI.Query<RefRO<EnemyDespawnRequest>>().WithEntityAccess())
        {
            bool isActive = false;

            if (entityManager.HasComponent<EnemyActive>(enemyEntity))
                isActive = entityManager.IsComponentEnabled<EnemyActive>(enemyEntity);

            if (isActive)
            {
                if (runtimeLookup.HasComponent(enemyEntity))
                {
                    EnemyRuntimeState runtimeState = runtimeLookup[enemyEntity];
                    runtimeState.Velocity = float3.zero;
                    runtimeState.ContactCooldown = 0f;
                    runtimeLookup[enemyEntity] = runtimeState;
                }

                if (healthLookup.HasComponent(enemyEntity))
                {
                    EnemyHealth enemyHealth = healthLookup[enemyEntity];

                    if (enemyHealth.Max < 1f)
                        enemyHealth.Max = 1f;

                    enemyHealth.Current = enemyHealth.Max;
                    healthLookup[enemyEntity] = enemyHealth;
                }

                if (entityManager.HasBuffer<EnemyElementStackElement>(enemyEntity))
                {
                    DynamicBuffer<EnemyElementStackElement> elementalStacks = entityManager.GetBuffer<EnemyElementStackElement>(enemyEntity);
                    elementalStacks.Clear();
                }

                if (entityManager.HasComponent<EnemyElementalRuntimeState>(enemyEntity))
                {
                    EnemyElementalRuntimeState elementalRuntime = entityManager.GetComponentData<EnemyElementalRuntimeState>(enemyEntity);
                    elementalRuntime.SlowPercent = 0f;
                    entityManager.SetComponentData(enemyEntity, elementalRuntime);
                }

                EnemyPoolUtility.ParkEnemy(entityManager, enemyEntity);
                entityManager.SetComponentEnabled<EnemyActive>(enemyEntity, false);

                if (ownerLookup.HasComponent(enemyEntity))
                {
                    Entity spawnerEntity = ownerLookup[enemyEntity].SpawnerEntity;

                    if (spawnerEntity != Entity.Null)
                    {
                        if (poolLookup.HasBuffer(spawnerEntity))
                        {
                            DynamicBuffer<EnemyPoolElement> pool = poolLookup[spawnerEntity];
                            pool.Add(new EnemyPoolElement
                            {
                                EnemyEntity = enemyEntity
                            });
                        }

                        if (spawnerStateLookup.HasComponent(spawnerEntity))
                        {
                            EnemySpawnerState spawnerState = spawnerStateLookup[spawnerEntity];

                            if (spawnerState.AliveCount > 0)
                                spawnerState.AliveCount--;

                            spawnerStateLookup[spawnerEntity] = spawnerState;
                        }
                    }
                }
            }

            commandBuffer.RemoveComponent<EnemyDespawnRequest>(enemyEntity);
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #endregion
}
