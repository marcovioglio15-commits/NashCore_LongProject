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
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerElementalVfxConfig>(true);
        ComponentLookup<EnemyRuntimeState> enemyRuntimeLookup = SystemAPI.GetComponentLookup<EnemyRuntimeState>(true);
        ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup = SystemAPI.GetComponentLookup<EnemyElementalVfxAnchor>(true);

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
                float procVfxLifetimeSeconds = ResolveProcVfxLifetimeSeconds(in segment.Effect);
                Entity ownerEntity = segment.OwnerEntity;
                bool canSpawnElementalVfx = false;
                DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = default;
                ElementalVfxDefinitionConfig elementalVfxConfig = default;

                if (ownerEntity != Entity.Null &&
                    ownerEntity.Index >= 0 &&
                    vfxRequestLookup.HasBuffer(ownerEntity) &&
                    elementalVfxConfigLookup.HasComponent(ownerEntity))
                {
                    vfxRequests = vfxRequestLookup[ownerEntity];
                    PlayerElementalVfxConfig ownerElementalVfxConfig = elementalVfxConfigLookup[ownerEntity];
                    elementalVfxConfig = ResolveElementalVfxDefinition(in ownerElementalVfxConfig, segment.Effect.ElementType);
                    canSpawnElementalVfx = elementalVfxConfig.SpawnStackVfx != 0 || elementalVfxConfig.SpawnProcVfx != 0;
                }

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

                    bool procTriggered;
                    bool applied = EnemyElementalStackUtility.TryApplyStacks(enemyEntity,
                                                                             math.max(0f, segment.StacksPerTick),
                                                                             segment.Effect,
                                                                             ref elementalStackLookup,
                                                                             out procTriggered);

                    if (applied == false || canSpawnElementalVfx == false)
                        continue;

                    if (enemyRuntimeLookup.HasComponent(enemyEntity) == false)
                        continue;

                    EnemyRuntimeState enemyRuntimeState = enemyRuntimeLookup[enemyEntity];
                    Entity followTargetEntity = enemyEntity;

                    if (elementalVfxAnchorLookup.HasComponent(enemyEntity))
                    {
                        Entity anchorEntity = elementalVfxAnchorLookup[enemyEntity].AnchorEntity;

                        if (anchorEntity != Entity.Null)
                            followTargetEntity = anchorEntity;
                    }

                    if (elementalVfxConfig.SpawnStackVfx != 0)
                        EnqueueElementalVfx(ref vfxRequests,
                                            elementalVfxConfig.StackVfxPrefabEntity,
                                            enemyPosition,
                                            elementalVfxConfig.StackVfxScaleMultiplier,
                                            followTargetEntity,
                                            enemyEntity,
                                            enemyRuntimeState.SpawnVersion,
                                            0.35f);

                    if (procTriggered && elementalVfxConfig.SpawnProcVfx != 0)
                        EnqueueElementalVfx(ref vfxRequests,
                                            elementalVfxConfig.ProcVfxPrefabEntity,
                                            enemyPosition,
                                            elementalVfxConfig.ProcVfxScaleMultiplier,
                                            followTargetEntity,
                                            enemyEntity,
                                            enemyRuntimeState.SpawnVersion,
                                            procVfxLifetimeSeconds);
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

    #region Helpers
    private static ElementalVfxDefinitionConfig ResolveElementalVfxDefinition(in PlayerElementalVfxConfig elementalVfxConfig, ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Fire:
                return elementalVfxConfig.Fire;
            case ElementType.Ice:
                return elementalVfxConfig.Ice;
            case ElementType.Poison:
                return elementalVfxConfig.Poison;
            default:
                return elementalVfxConfig.Custom;
        }
    }

    private static void EnqueueElementalVfx(ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                                            Entity prefabEntity,
                                            float3 position,
                                            float scaleMultiplier,
                                            Entity followTargetEntity,
                                            Entity followValidationEntity,
                                            uint followValidationSpawnVersion,
                                            float lifetimeSeconds)
    {
        if (prefabEntity == Entity.Null)
            return;

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = prefabEntity,
            Position = position,
            Rotation = quaternion.identity,
            UniformScale = math.max(0.01f, scaleMultiplier),
            LifetimeSeconds = math.max(0.05f, lifetimeSeconds),
            FollowTargetEntity = followTargetEntity,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = followValidationEntity,
            FollowValidationSpawnVersion = followValidationSpawnVersion,
            Velocity = float3.zero
        });
    }

    private static float ResolveProcVfxLifetimeSeconds(in ElementalEffectConfig effectConfig)
    {
        switch (effectConfig.EffectKind)
        {
            case ElementalEffectKind.Dots:
                return math.max(0.05f, effectConfig.DotDurationSeconds);
            case ElementalEffectKind.Impediment:
                return math.max(0.05f, effectConfig.ImpedimentDurationSeconds);
            default:
                return 0.5f;
        }
    }
    #endregion

    #endregion
}
