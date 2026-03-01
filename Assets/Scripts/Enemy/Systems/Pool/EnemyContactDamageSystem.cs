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
        Entity playerEntity = Entity.Null;
        LocalTransform playerTransform = default;
        PlayerHealth playerHealth = default;

        foreach ((RefRO<LocalTransform> candidatePlayerTransform,
                  RefRO<PlayerHealth> candidatePlayerHealth,
                  Entity candidatePlayerEntity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerHealth>>()
                                                            .WithAll<PlayerControllerConfig>()
                                                            .WithEntityAccess())
        {
            playerEntity = candidatePlayerEntity;
            playerTransform = candidatePlayerTransform.ValueRO;
            playerHealth = candidatePlayerHealth.ValueRO;
            break;
        }

        if (playerEntity == Entity.Null)
            return;

        if (entityManager.Exists(playerEntity) == false)
            return;

        if (dashStateLookup.HasComponent(playerEntity))
        {
            PlayerDashState dashState = dashStateLookup[playerEntity];

            if (dashState.RemainingInvulnerability > 0f)
                return;
        }

        if (playerHealth.Current <= 0f)
            return;

        float3 playerPosition = playerTransform.Position;
        float enemyTimeScale = 1f;

        if (SystemAPI.TryGetSingleton<EnemyGlobalTimeScale>(out EnemyGlobalTimeScale enemyGlobalTimeScale))
            enemyTimeScale = math.clamp(enemyGlobalTimeScale.Scale, 0f, 1f);

        float deltaTime = SystemAPI.Time.DeltaTime * enemyTimeScale;
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
