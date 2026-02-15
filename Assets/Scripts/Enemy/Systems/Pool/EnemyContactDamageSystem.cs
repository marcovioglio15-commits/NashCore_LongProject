using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Applies contact damage from active enemies to the player.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyProjectileHitSystem))]
public partial struct EnemyContactDamageSystem : ISystem
{
    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyActive>();
        state.RequireForUpdate<EnemyData>();
        state.RequireForUpdate<EnemyRuntimeState>();

        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform, PlayerHealth>()
            .Build();

        state.RequireForUpdate(playerQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        ComponentLookup<PlayerDashState> dashStateLookup = SystemAPI.GetComponentLookup<PlayerDashState>(true);
        NativeArray<Entity> playerEntities = playerQuery.ToEntityArray(Allocator.Temp);

        if (playerEntities.Length == 0)
        {
            playerEntities.Dispose();
            return;
        }

        Entity playerEntity = playerEntities[0];
        playerEntities.Dispose();

        if (entityManager.Exists(playerEntity) == false)
            return;

        if (dashStateLookup.HasComponent(playerEntity))
        {
            PlayerDashState dashState = dashStateLookup[playerEntity];

            if (dashState.RemainingInvulnerability > 0f)
                return;
        }

        LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
        PlayerHealth playerHealth = entityManager.GetComponentData<PlayerHealth>(playerEntity);

        if (playerHealth.Current <= 0f)
            return;

        float3 playerPosition = playerTransform.Position;
        float deltaTime = SystemAPI.Time.DeltaTime;
        float accumulatedDamage = 0f;

        foreach ((RefRO<EnemyData> enemyData,
                  RefRW<EnemyRuntimeState> runtimeState,
                  RefRO<LocalTransform> enemyTransform) in SystemAPI.Query<RefRO<EnemyData>, RefRW<EnemyRuntimeState>, RefRO<LocalTransform>>()
                                                                      .WithAll<EnemyActive>()
                                                                      .WithNone<EnemyDespawnRequest>())
        {
            EnemyRuntimeState nextState = runtimeState.ValueRO;
            nextState.ContactCooldown -= deltaTime;

            if (nextState.ContactCooldown < 0f)
                nextState.ContactCooldown = 0f;

            float contactRadius = math.max(0f, enemyData.ValueRO.ContactRadius);

            if (contactRadius <= 0f)
            {
                runtimeState.ValueRW = nextState;
                continue;
            }

            float3 delta = enemyTransform.ValueRO.Position - playerPosition;
            delta.y = 0f;
            float sqrDistance = math.lengthsq(delta);
            float contactRadiusSquared = contactRadius * contactRadius;

            if (sqrDistance <= contactRadiusSquared && nextState.ContactCooldown <= 0f)
            {
                accumulatedDamage += math.max(0f, enemyData.ValueRO.ContactDamage);
                nextState.ContactCooldown = math.max(0.01f, enemyData.ValueRO.ContactInterval);
            }

            runtimeState.ValueRW = nextState;
        }

        if (accumulatedDamage <= 0f)
            return;

        playerHealth.Current -= accumulatedDamage;

        if (playerHealth.Current < 0f)
            playerHealth.Current = 0f;

        entityManager.SetComponentData(playerEntity, playerHealth);
    }
    #endregion

    #endregion
}
