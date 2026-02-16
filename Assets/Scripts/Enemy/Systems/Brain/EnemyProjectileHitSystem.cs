using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves projectile hits against active enemies using a spatial hash.
/// Applies damage, elemental payloads and split-projectile generation.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySteeringSystem))]
public partial struct EnemyProjectileHitSystem : ISystem
{
    #region Constants
    private const float BaseProjectileHitRadius = 0.05f;
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Fields
    private EntityQuery enemyQuery;
    private EntityQuery projectileQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData, EnemyHealth, EnemyRuntimeState, LocalTransform, EnemyActive>()
            .WithNone<EnemyDespawnRequest>()
            .Build();

        projectileQuery = SystemAPI.QueryBuilder()
            .WithAll<Projectile, ProjectileOwner, ProjectileSplitState, ProjectileElementalPayload, LocalTransform, ProjectileActive>()
            .Build();

        state.RequireForUpdate(enemyQuery);
        state.RequireForUpdate(projectileQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        int enemyCount = enemyQuery.CalculateEntityCount();

        if (enemyCount <= 0)
            return;

        int projectileCount = projectileQuery.CalculateEntityCount();

        if (projectileCount <= 0)
            return;

        NativeArray<Entity> enemyEntities = enemyQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<EnemyData> enemyDataArray = enemyQuery.ToComponentDataArray<EnemyData>(Allocator.TempJob);
        NativeArray<EnemyHealth> enemyHealthArray = enemyQuery.ToComponentDataArray<EnemyHealth>(Allocator.TempJob);
        NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = enemyQuery.ToComponentDataArray<EnemyRuntimeState>(Allocator.TempJob);

        NativeArray<Entity> projectileEntities = projectileQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<Projectile> projectileDataArray = projectileQuery.ToComponentDataArray<Projectile>(Allocator.TempJob);
        NativeArray<ProjectileOwner> projectileOwnerArray = projectileQuery.ToComponentDataArray<ProjectileOwner>(Allocator.TempJob);
        NativeArray<ProjectileSplitState> projectileSplitArray = projectileQuery.ToComponentDataArray<ProjectileSplitState>(Allocator.TempJob);
        NativeArray<ProjectileElementalPayload> projectileElementalArray = projectileQuery.ToComponentDataArray<ProjectileElementalPayload>(Allocator.TempJob);
        NativeArray<LocalTransform> projectileTransforms = projectileQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        NativeArray<float3> enemyPositions = new NativeArray<float3>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> enemyRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        float maxRadius = 0.05f;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            enemyPositions[enemyIndex] = enemyTransforms[enemyIndex].Position;
            float bodyRadius = math.max(0.05f, enemyDataArray[enemyIndex].BodyRadius);
            enemyRadii[enemyIndex] = bodyRadius;

            if (bodyRadius > maxRadius)
                maxRadius = bodyRadius;
        }

        float cellSize = math.max(0.25f, maxRadius);
        float inverseCellSize = 1f / cellSize;
        NativeParallelMultiHashMap<int, int> enemyCellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.TempJob);

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            float3 enemyPosition = enemyPositions[enemyIndex];
            int cellX = (int)math.floor(enemyPosition.x * inverseCellSize);
            int cellY = (int)math.floor(enemyPosition.z * inverseCellSize);
            enemyCellMap.Add(EncodeCell(cellX, cellY), enemyIndex);
        }

        NativeArray<float3> projectilePositions = new NativeArray<float3>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> projectileRadii = new NativeArray<float>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            projectilePositions[projectileIndex] = projectileTransforms[projectileIndex].Position;
            float projectileScale = math.max(0.01f, projectileTransforms[projectileIndex].Scale);
            projectileRadii[projectileIndex] = math.max(0.005f, BaseProjectileHitRadius * projectileScale);
        }

        NativeArray<int> hitEnemyIndices = new NativeArray<int>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        EnemyProjectileHitJob hitJob = new EnemyProjectileHitJob
        {
            ProjectilePositions = projectilePositions,
            ProjectileRadii = projectileRadii,
            EnemyPositions = enemyPositions,
            EnemyRadii = enemyRadii,
            CellMap = enemyCellMap,
            InverseCellSize = inverseCellSize,
            HitEnemyIndices = hitEnemyIndices
        };

        JobHandle handle = hitJob.Schedule(projectileCount, 64, state.Dependency);
        handle.Complete();

        NativeArray<float> damageByEnemy = new NativeArray<float>(enemyCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
        BufferLookup<ProjectilePoolElement> projectilePoolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<ShootRequest> shootRequestLookup = SystemAPI.GetBufferLookup<ShootRequest>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        BufferLookup<EnemyElementStackElement> elementalStackLookup = SystemAPI.GetBufferLookup<EnemyElementStackElement>(false);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);
        ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup = SystemAPI.GetComponentLookup<EnemyElementalVfxAnchor>(true);

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            int enemyIndex = hitEnemyIndices[projectileIndex];

            if (enemyIndex < 0)
                continue;

            if (enemyIndex >= enemyCount)
                continue;

            Projectile projectileData = projectileDataArray[projectileIndex];
            ProjectileOwner projectileOwner = projectileOwnerArray[projectileIndex];
            ProjectileSplitState splitState = projectileSplitArray[projectileIndex];
            ProjectileElementalPayload elementalPayload = projectileElementalArray[projectileIndex];
            LocalTransform projectileTransform = projectileTransforms[projectileIndex];
            float currentScaleMultiplier = ResolveCurrentScaleMultiplier(projectileEntities[projectileIndex],
                                                                        projectileTransform.Scale,
                                                                        in projectileBaseScaleLookup);
            Entity enemyEntity = enemyEntities[enemyIndex];
            float3 enemyPosition = enemyTransforms[enemyIndex].Position;
            EnemyRuntimeState enemyRuntimeState = enemyRuntimeArray[enemyIndex];

            damageByEnemy[enemyIndex] += math.max(0f, projectileData.Damage);
            TryEnqueueSplitRequests(in projectileData,
                                   in splitState,
                                   in projectileTransform,
                                   currentScaleMultiplier,
                                   in projectileOwner,
                                   ref shootRequestLookup);
            TryApplyElementalPayload(enemyEntity,
                                     enemyPosition,
                                     in elementalPayload,
                                     in projectileOwner,
                                     in enemyRuntimeState,
                                     in elementalVfxAnchorLookup,
                                     ref elementalStackLookup,
                                     ref vfxRequestLookup);
            DespawnProjectile(entityManager, projectileEntities[projectileIndex], projectileOwner, ref projectilePoolLookup);
        }

        ComponentLookup<EnemyDespawnRequest> despawnLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            float damage = damageByEnemy[enemyIndex];

            if (damage <= 0f)
                continue;

            Entity enemyEntity = enemyEntities[enemyIndex];

            if (entityManager.Exists(enemyEntity) == false)
                continue;

            EnemyHealth enemyHealth = enemyHealthArray[enemyIndex];
            enemyHealth.Current -= damage;

            if (enemyHealth.Current < 0f)
                enemyHealth.Current = 0f;

            entityManager.SetComponentData(enemyEntity, enemyHealth);

            if (enemyHealth.Current <= 0f)
            {
                if (despawnLookup.HasComponent(enemyEntity) == false)
                {
                    commandBuffer.AddComponent(enemyEntity, new EnemyDespawnRequest
                    {
                        Reason = EnemyDespawnReason.Killed
                    });
                }
            }
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();

        enemyEntities.Dispose();
        enemyDataArray.Dispose();
        enemyHealthArray.Dispose();
        enemyTransforms.Dispose();
        enemyRuntimeArray.Dispose();

        projectileEntities.Dispose();
        projectileDataArray.Dispose();
        projectileOwnerArray.Dispose();
        projectileSplitArray.Dispose();
        projectileElementalArray.Dispose();
        projectileTransforms.Dispose();

        enemyPositions.Dispose();
        enemyRadii.Dispose();
        projectilePositions.Dispose();
        projectileRadii.Dispose();
        hitEnemyIndices.Dispose();
        enemyCellMap.Dispose();
        damageByEnemy.Dispose();
    }
    #endregion

    #region Helpers
    private static int EncodeCell(int x, int y)
    {
        unchecked
        {
            return (x * 73856093) ^ (y * 19349663);
        }
    }

    private static void TryEnqueueSplitRequests(in Projectile projectileData,
                                                in ProjectileSplitState splitState,
                                                in LocalTransform projectileTransform,
                                                float currentScaleMultiplier,
                                                in ProjectileOwner projectileOwner,
                                                ref BufferLookup<ShootRequest> shootRequestLookup)
    {
        if (splitState.CanSplit == 0)
            return;

        Entity shooterEntity = projectileOwner.ShooterEntity;

        if (shootRequestLookup.HasBuffer(shooterEntity) == false)
            return;

        DynamicBuffer<ShootRequest> shootRequests = shootRequestLookup[shooterEntity];
        float3 baseDirection = projectileData.Velocity;
        baseDirection.y = 0f;

        if (math.lengthsq(baseDirection) <= DirectionEpsilon)
            baseDirection = math.forward(projectileTransform.Rotation);

        baseDirection.y = 0f;
        baseDirection = math.normalizesafe(baseDirection, new float3(0f, 0f, 1f));

        float projectileSpeed = math.length(projectileData.Velocity);
        float splitSpeed = math.max(0f, projectileSpeed * math.max(0f, splitState.SplitSpeedMultiplier));
        float splitRange = math.max(0f, projectileData.MaxRange * math.max(0f, splitState.SplitLifetimeMultiplier));
        float splitLifetime = math.max(0f, projectileData.MaxLifetime * math.max(0f, splitState.SplitLifetimeMultiplier));
        float splitDamage = math.max(0f, projectileData.Damage * math.max(0f, splitState.SplitDamageMultiplier));
        float splitScaleMultiplier = math.max(0.01f, currentScaleMultiplier * math.max(0f, splitState.SplitSizeMultiplier));

        switch (splitState.DirectionMode)
        {
            case ProjectileSplitDirectionMode.Uniform:
                AddUniformSplitRequests(ref shootRequests,
                                        in splitState,
                                        projectileTransform.Position,
                                        baseDirection,
                                        splitSpeed,
                                        splitRange,
                                        splitLifetime,
                                        splitDamage,
                                        splitScaleMultiplier,
                                        projectileData.InheritPlayerSpeed);
                return;
            case ProjectileSplitDirectionMode.CustomAngles:
                if (splitState.CustomAnglesDegrees.Length <= 0)
                {
                    AddUniformSplitRequests(ref shootRequests,
                                            in splitState,
                                            projectileTransform.Position,
                                            baseDirection,
                                            splitSpeed,
                                            splitRange,
                                            splitLifetime,
                                            splitDamage,
                                            splitScaleMultiplier,
                                            projectileData.InheritPlayerSpeed);
                    return;
                }

                AddCustomAngleSplitRequests(ref shootRequests,
                                            in splitState,
                                            projectileTransform.Position,
                                            baseDirection,
                                            splitSpeed,
                                            splitRange,
                                            splitLifetime,
                                            splitDamage,
                                            splitScaleMultiplier,
                                            projectileData.InheritPlayerSpeed);
                return;
        }
    }

    private static void AddUniformSplitRequests(ref DynamicBuffer<ShootRequest> shootRequests,
                                                in ProjectileSplitState splitState,
                                                float3 spawnPosition,
                                                float3 baseDirection,
                                                float splitSpeed,
                                                float splitRange,
                                                float splitLifetime,
                                                float splitDamage,
                                                float splitScaleMultiplier,
                                                byte inheritPlayerSpeed)
    {
        int splitCount = math.max(1, splitState.SplitProjectileCount);
        float stepDegrees = 360f / splitCount;
        float baseAngleDegrees = ResolveDirectionAngleDegrees(baseDirection);

        for (int splitIndex = 0; splitIndex < splitCount; splitIndex++)
        {
            float angleDegrees = baseAngleDegrees + splitState.SplitOffsetDegrees + stepDegrees * splitIndex;
            float3 direction = ResolvePlanarDirectionFromAngleDegrees(angleDegrees);
            AddSplitShootRequest(ref shootRequests,
                                 spawnPosition,
                                 direction,
                                 splitSpeed,
                                 splitRange,
                                 splitLifetime,
                                 splitDamage,
                                 splitScaleMultiplier,
                                 inheritPlayerSpeed);
        }
    }

    private static void AddCustomAngleSplitRequests(ref DynamicBuffer<ShootRequest> shootRequests,
                                                    in ProjectileSplitState splitState,
                                                    float3 spawnPosition,
                                                    float3 baseDirection,
                                                    float splitSpeed,
                                                    float splitRange,
                                                    float splitLifetime,
                                                    float splitDamage,
                                                    float splitScaleMultiplier,
                                                    byte inheritPlayerSpeed)
    {
        float baseAngleDegrees = ResolveDirectionAngleDegrees(baseDirection);

        for (int splitIndex = 0; splitIndex < splitState.CustomAnglesDegrees.Length; splitIndex++)
        {
            float angleDegrees = baseAngleDegrees + splitState.CustomAnglesDegrees[splitIndex] + splitState.SplitOffsetDegrees;
            float3 direction = ResolvePlanarDirectionFromAngleDegrees(angleDegrees);
            AddSplitShootRequest(ref shootRequests,
                                 spawnPosition,
                                 direction,
                                 splitSpeed,
                                 splitRange,
                                 splitLifetime,
                                 splitDamage,
                                 splitScaleMultiplier,
                                 inheritPlayerSpeed);
        }
    }

    private static void AddSplitShootRequest(ref DynamicBuffer<ShootRequest> shootRequests,
                                             float3 spawnPosition,
                                             float3 direction,
                                             float splitSpeed,
                                             float splitRange,
                                             float splitLifetime,
                                             float splitDamage,
                                             float splitScaleMultiplier,
                                             byte inheritPlayerSpeed)
    {
        shootRequests.Add(new ShootRequest
        {
            Position = spawnPosition,
            Direction = direction,
            Speed = splitSpeed,
            Range = splitRange,
            Lifetime = splitLifetime,
            Damage = splitDamage,
            ProjectileScaleMultiplier = splitScaleMultiplier,
            InheritPlayerSpeed = inheritPlayerSpeed,
            IsSplitChild = 1
        });
    }

    private static float ResolveCurrentScaleMultiplier(Entity projectileEntity,
                                                       float currentScale,
                                                       in ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup)
    {
        if (projectileBaseScaleLookup.HasComponent(projectileEntity) == false)
            return math.max(0.01f, currentScale);

        float baseScale = math.max(0.0001f, projectileBaseScaleLookup[projectileEntity].Value);
        return math.max(0.01f, currentScale / baseScale);
    }

    private static float ResolveDirectionAngleDegrees(float3 direction)
    {
        float3 normalizedDirection = math.normalizesafe(direction, new float3(0f, 0f, 1f));
        return math.degrees(math.atan2(normalizedDirection.x, normalizedDirection.z));
    }

    private static float3 ResolvePlanarDirectionFromAngleDegrees(float angleDegrees)
    {
        float radians = math.radians(angleDegrees);
        return new float3(math.sin(radians), 0f, math.cos(radians));
    }

    private static void TryApplyElementalPayload(Entity enemyEntity,
                                                 float3 enemyPosition,
                                                 in ProjectileElementalPayload elementalPayload,
                                                 in ProjectileOwner projectileOwner,
                                                 in EnemyRuntimeState enemyRuntimeState,
                                                 in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                                 ref BufferLookup<EnemyElementStackElement> elementalStackLookup,
                                                 ref BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup)
    {
        if (elementalPayload.Enabled == 0)
            return;

        if (elementalPayload.StacksPerHit <= 0f)
            return;

        bool procTriggered;
        bool applied = EnemyElementalStackUtility.TryApplyStacks(enemyEntity,
                                                                 math.max(0f, elementalPayload.StacksPerHit),
                                                                 elementalPayload.Effect,
                                                                 ref elementalStackLookup,
                                                                 out procTriggered);

        if (applied == false)
            return;

        Entity shooterEntity = projectileOwner.ShooterEntity;

        if (vfxRequestLookup.HasBuffer(shooterEntity) == false)
            return;

        DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests = vfxRequestLookup[shooterEntity];
        float3 vfxPosition = enemyPosition;
        Entity followTargetEntity = enemyEntity;
        float stackVfxLifetimeSeconds = 0.35f;
        float procVfxLifetimeSeconds = ResolveProcVfxLifetimeSeconds(in elementalPayload.Effect);

        if (elementalVfxAnchorLookup.HasComponent(enemyEntity))
        {
            Entity anchorEntity = elementalVfxAnchorLookup[enemyEntity].AnchorEntity;

            if (anchorEntity != Entity.Null)
                followTargetEntity = anchorEntity;
        }

        if (elementalPayload.SpawnStackVfx != 0)
            EnqueueElementalVfx(ref vfxRequests,
                                elementalPayload.StackVfxPrefabEntity,
                                vfxPosition,
                                elementalPayload.StackVfxScaleMultiplier,
                                followTargetEntity,
                                enemyEntity,
                                enemyRuntimeState.SpawnVersion,
                                stackVfxLifetimeSeconds);

        if (procTriggered && elementalPayload.SpawnProcVfx != 0)
            EnqueueElementalVfx(ref vfxRequests,
                                elementalPayload.ProcVfxPrefabEntity,
                                vfxPosition,
                                elementalPayload.ProcVfxScaleMultiplier,
                                followTargetEntity,
                                enemyEntity,
                                enemyRuntimeState.SpawnVersion,
                                procVfxLifetimeSeconds);
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

    private static void DespawnProjectile(EntityManager entityManager,
                                          Entity projectileEntity,
                                          ProjectileOwner projectileOwner,
                                          ref BufferLookup<ProjectilePoolElement> projectilePoolLookup)
    {
        if (entityManager.Exists(projectileEntity) == false)
            return;

        ProjectilePoolUtility.SetProjectileParked(entityManager, projectileEntity);
        entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);

        Entity shooterEntity = projectileOwner.ShooterEntity;

        if (projectilePoolLookup.HasBuffer(shooterEntity) == false)
            return;

        DynamicBuffer<ProjectilePoolElement> shooterPool = projectilePoolLookup[shooterEntity];
        shooterPool.Add(new ProjectilePoolElement
        {
            ProjectileEntity = projectileEntity
        });
    }
    #endregion

    #endregion

    #region Jobs
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    private struct EnemyProjectileHitJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> ProjectilePositions;
        [ReadOnly] public NativeArray<float> ProjectileRadii;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<float> EnemyRadii;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> CellMap;
        [ReadOnly] public float InverseCellSize;
        public NativeArray<int> HitEnemyIndices;

        public void Execute(int index)
        {
            float3 projectilePosition = ProjectilePositions[index];
            float projectileRadius = math.max(0.005f, ProjectileRadii[index]);
            int centerCellX = (int)math.floor(projectilePosition.x * InverseCellSize);
            int centerCellY = (int)math.floor(projectilePosition.z * InverseCellSize);

            int closestEnemyIndex = -1;
            float closestDistanceSquared = float.MaxValue;

            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int key = EncodeCell(centerCellX + offsetX, centerCellY + offsetY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int enemyIndex;

                    if (CellMap.TryGetFirstValue(key, out enemyIndex, out iterator) == false)
                        continue;

                    do
                    {
                        float3 delta = projectilePosition - EnemyPositions[enemyIndex];
                        delta.y = 0f;
                        float sqrDistance = math.lengthsq(delta);
                        float radius = math.max(0.01f, EnemyRadii[enemyIndex] + projectileRadius);
                        float radiusSquared = radius * radius;

                        if (sqrDistance > radiusSquared)
                            continue;

                        if (sqrDistance >= closestDistanceSquared)
                            continue;

                        closestDistanceSquared = sqrDistance;
                        closestEnemyIndex = enemyIndex;
                    }
                    while (CellMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            HitEnemyIndices[index] = closestEnemyIndex;
        }

        private static int EncodeCell(int x, int y)
        {
            unchecked
            {
                return (x * 73856093) ^ (y * 19349663);
            }
        }
    }
    #endregion
}
