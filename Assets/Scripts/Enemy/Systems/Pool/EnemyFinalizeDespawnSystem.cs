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
                    runtimeState.ContactDamageCooldown = 0f;
                    runtimeState.AreaDamageCooldown = 0f;
                    runtimeLookup[enemyEntity] = runtimeState;
                }

                if (entityManager.HasComponent<EnemyPatternRuntimeState>(enemyEntity))
                {
                    EnemyPatternRuntimeState patternRuntimeState = entityManager.GetComponentData<EnemyPatternRuntimeState>(enemyEntity);
                    patternRuntimeState.WanderTargetPosition = float3.zero;
                    patternRuntimeState.WanderWaitTimer = 0f;
                    patternRuntimeState.WanderRetryTimer = 0f;
                    patternRuntimeState.LastWanderDirectionAngle = 0f;
                    patternRuntimeState.WanderHasTarget = 0;
                    patternRuntimeState.WanderInitialized = 0;
                    patternRuntimeState.DvdDirection = float3.zero;
                    patternRuntimeState.DvdInitialized = 0;
                    entityManager.SetComponentData(enemyEntity, patternRuntimeState);
                }

                if (entityManager.HasComponent<EnemyShooterControlState>(enemyEntity))
                {
                    EnemyShooterControlState shooterControlState = entityManager.GetComponentData<EnemyShooterControlState>(enemyEntity);
                    shooterControlState.MovementLocked = 0;
                    entityManager.SetComponentData(enemyEntity, shooterControlState);
                }

                if (entityManager.HasBuffer<EnemyShooterRuntimeElement>(enemyEntity))
                {
                    DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = entityManager.GetBuffer<EnemyShooterRuntimeElement>(enemyEntity);
                    shooterRuntime.Clear();
                }

                if (healthLookup.HasComponent(enemyEntity))
                {
                    EnemyHealth enemyHealth = healthLookup[enemyEntity];

                    if (enemyHealth.Max < 1f)
                        enemyHealth.Max = 1f;

                    if (enemyHealth.MaxShield < 0f)
                        enemyHealth.MaxShield = 0f;

                    enemyHealth.Current = enemyHealth.Max;
                    enemyHealth.CurrentShield = enemyHealth.MaxShield;
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

                EnemyPoolUtility.ResetVisualRuntimeState(entityManager, enemyEntity, 0);
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
