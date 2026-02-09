using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Flags far enemies for pooled despawn.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyContactDamageSystem))]
public partial struct EnemyDespawnSystem : ISystem
{
    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyActive>();
        state.RequireForUpdate<EnemyOwnerSpawner>();
        state.RequireForUpdate<LocalTransform>();

        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform>()
            .Build();

        state.RequireForUpdate(playerQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
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

        float3 playerPosition = entityManager.GetComponentData<LocalTransform>(playerEntity).Position;
        ComponentLookup<EnemySpawner> spawnerLookup = SystemAPI.GetComponentLookup<EnemySpawner>(true);
        ComponentLookup<EnemyDespawnRequest> despawnRequestLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((RefRO<EnemyOwnerSpawner> ownerSpawner,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity) in SystemAPI.Query<RefRO<EnemyOwnerSpawner>, RefRO<LocalTransform>>().WithAll<EnemyActive>().WithEntityAccess())
        {
            if (despawnRequestLookup.HasComponent(enemyEntity))
                continue;

            Entity spawnerEntity = ownerSpawner.ValueRO.SpawnerEntity;
            bool shouldDespawn = ShouldDespawn(spawnerEntity, enemyTransform.ValueRO.Position, playerPosition, ref spawnerLookup);

            if (shouldDespawn == false)
                continue;

            commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
            {
                Reason = EnemyDespawnReason.Distance
            });
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private static bool ShouldDespawn(Entity spawnerEntity, float3 enemyPosition, float3 playerPosition, ref ComponentLookup<EnemySpawner> spawnerLookup)
    {
        if (spawnerEntity == Entity.Null)
            return true;

        if (spawnerLookup.HasComponent(spawnerEntity) == false)
            return true;

        EnemySpawner spawner = spawnerLookup[spawnerEntity];
        float despawnDistance = spawner.DespawnDistance;

        if (despawnDistance <= 0f)
            return false;

        float3 delta = enemyPosition - playerPosition;
        delta.y = 0f;
        float sqrDistance = math.lengthsq(delta);
        float despawnDistanceSquared = despawnDistance * despawnDistance;
        return sqrDistance >= despawnDistanceSquared;
    }
    #endregion

    #endregion
}
