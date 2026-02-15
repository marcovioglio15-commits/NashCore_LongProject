using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Updates trail segment lifetimes and applies elemental stacks to enemies overlapping active segments.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyProjectileHitSystem))]
[UpdateBefore(typeof(EnemyElementalEffectsSystem))]
public partial struct PlayerElementalTrailResolveSystem : ISystem
{
    #region Fields
    private EntityQuery enemyQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ElementalTrailSegment>();

        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        BufferLookup<EnemyElementStackElement> elementalStackLookup = SystemAPI.GetBufferLookup<EnemyElementStackElement>(false);

        int enemyCount = enemyQuery.CalculateEntityCount();
        NativeArray<Entity> enemyEntities = default;
        NativeArray<EnemyData> enemyDataArray = default;
        NativeArray<LocalTransform> enemyTransforms = default;

        if (enemyCount > 0)
        {
            enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
            enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(Allocator.Temp);
            enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        }

        foreach ((RefRW<ElementalTrailSegment> trailSegment,
                  RefRO<LocalTransform> trailTransform,
                  Entity segmentEntity)
                 in SystemAPI.Query<RefRW<ElementalTrailSegment>, RefRO<LocalTransform>>()
                             .WithEntityAccess())
        {
            ElementalTrailSegment segment = trailSegment.ValueRO;
            segment.RemainingLifetime -= deltaTime;
            segment.ApplyTimer -= deltaTime;

            if (segment.RemainingLifetime <= 0f)
            {
                trailSegment.ValueRW = segment;
                commandBuffer.DestroyEntity(segmentEntity);
                continue;
            }

            bool canApply = segment.ApplyTimer <= 0f && segment.StacksPerTick > 0f && segment.Radius > 0f;

            if (canApply && enemyCount > 0)
            {
                float radius = math.max(0f, segment.Radius);
                float3 trailPosition = trailTransform.ValueRO.Position;

                for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
                {
                    Entity enemyEntity = enemyEntities[enemyIndex];

                    if (entityManager.Exists(enemyEntity) == false)
                        continue;

                    float3 enemyPosition = enemyTransforms[enemyIndex].Position;
                    float3 delta = enemyPosition - trailPosition;
                    delta.y = 0f;
                    float distanceSquared = math.lengthsq(delta);
                    float bodyRadius = math.max(0f, enemyDataArray[enemyIndex].BodyRadius);
                    float totalRadius = radius + bodyRadius;

                    if (distanceSquared > totalRadius * totalRadius)
                        continue;

                    EnemyElementalStackUtility.TryApplyStacks(enemyEntity,
                                                              math.max(0f, segment.StacksPerTick),
                                                              segment.Effect,
                                                              ref elementalStackLookup,
                                                              out bool _);
                }

                segment.ApplyTimer = math.max(0.01f, segment.ApplyIntervalSeconds);
            }

            trailSegment.ValueRW = segment;
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        if (enemyCount > 0)
        {
            enemyEntities.Dispose();
            enemyDataArray.Dispose();
            enemyTransforms.Dispose();
        }
    }
    #endregion

    #endregion
}
