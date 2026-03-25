using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves projectile hits against active enemies using a spatial hash.
/// Applies damage, elemental payloads and split-projectile generation.
/// Each projectile can hit all enemies overlapped by its runtime impact radius.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemySteeringSystem))]
public partial struct EnemyProjectileHitSystem : ISystem
{
    #region Constants
    private const float BaseProjectileHitRadius = 0.05f;
    #endregion

    #region Nested Types
    private struct ProjectileHitCandidate
    {
        public int EnemyIndex;
        public float DistanceSquared;
    }
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

        NativeArray<Entity> enemyEntities = new NativeArray<Entity>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<EnemyData> enemyDataArray = new NativeArray<EnemyData>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<EnemyHealth> enemyHealthArray = new NativeArray<EnemyHealth>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<LocalTransform> enemyTransforms = new NativeArray<LocalTransform>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<EnemyRuntimeState> enemyRuntimeArray = new NativeArray<EnemyRuntimeState>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        int enemyWriteIndex = 0;

        // Snapshot query data into contiguous arrays so Burst jobs can run over stable memory.
        foreach ((RefRO<EnemyData> enemyData,
                  RefRO<EnemyHealth> enemyHealth,
                  RefRO<LocalTransform> enemyTransform,
                  RefRO<EnemyRuntimeState> enemyRuntimeState,
                  Entity enemyEntity) in SystemAPI.Query<RefRO<EnemyData>,
                                                         RefRO<EnemyHealth>,
                                                         RefRO<LocalTransform>,
                                                         RefRO<EnemyRuntimeState>>()
                                                 .WithAll<EnemyActive>()
                                                 .WithNone<EnemyDespawnRequest>()
                                                 .WithEntityAccess())
        {
            if (enemyWriteIndex >= enemyCount)
                break;

            enemyEntities[enemyWriteIndex] = enemyEntity;
            enemyDataArray[enemyWriteIndex] = enemyData.ValueRO;
            enemyHealthArray[enemyWriteIndex] = enemyHealth.ValueRO;
            enemyTransforms[enemyWriteIndex] = enemyTransform.ValueRO;
            enemyRuntimeArray[enemyWriteIndex] = enemyRuntimeState.ValueRO;
            enemyWriteIndex++;
        }

        enemyCount = enemyWriteIndex;

        if (enemyCount <= 0)
        {
            enemyEntities.Dispose();
            enemyDataArray.Dispose();
            enemyHealthArray.Dispose();
            enemyTransforms.Dispose();
            enemyRuntimeArray.Dispose();
            return;
        }

        NativeArray<Entity> projectileEntities = new NativeArray<Entity>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<Projectile> projectileDataArray = new NativeArray<Projectile>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<ProjectileOwner> projectileOwnerArray = new NativeArray<ProjectileOwner>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<ProjectileSplitState> projectileSplitArray = new NativeArray<ProjectileSplitState>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<ProjectileElementalPayload> projectileElementalArray = new NativeArray<ProjectileElementalPayload>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<LocalTransform> projectileTransforms = new NativeArray<LocalTransform>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        int projectileWriteIndex = 0;
        ComponentLookup<PlayerControllerConfig> playerControllerLookup = SystemAPI.GetComponentLookup<PlayerControllerConfig>(true);

        foreach ((RefRO<Projectile> projectileData,
                  RefRO<ProjectileOwner> projectileOwner,
                  RefRO<ProjectileSplitState> projectileSplitState,
                  RefRO<ProjectileElementalPayload> projectileElementalPayload,
                  RefRO<LocalTransform> projectileTransform,
                  Entity projectileEntity) in SystemAPI.Query<RefRO<Projectile>,
                                                              RefRO<ProjectileOwner>,
                                                              RefRO<ProjectileSplitState>,
                                                              RefRO<ProjectileElementalPayload>,
                                                              RefRO<LocalTransform>>()
                                                      .WithAll<ProjectileActive>()
                                                      .WithEntityAccess())
        {
            if (projectileWriteIndex >= projectileCount)
                break;

            Entity shooterEntity = projectileOwner.ValueRO.ShooterEntity;

            if (!playerControllerLookup.HasComponent(shooterEntity))
                continue;

            projectileEntities[projectileWriteIndex] = projectileEntity;
            projectileDataArray[projectileWriteIndex] = projectileData.ValueRO;
            projectileOwnerArray[projectileWriteIndex] = projectileOwner.ValueRO;
            projectileSplitArray[projectileWriteIndex] = projectileSplitState.ValueRO;
            projectileElementalArray[projectileWriteIndex] = projectileElementalPayload.ValueRO;
            projectileTransforms[projectileWriteIndex] = projectileTransform.ValueRO;
            projectileWriteIndex++;
        }

        projectileCount = projectileWriteIndex;

        if (projectileCount <= 0)
        {
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
            return;
        }

        NativeArray<float3> enemyPositions = new NativeArray<float3>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> enemyRadii = new NativeArray<float>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        float maxEnemyRadius = 0.05f;

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            enemyPositions[enemyIndex] = enemyTransforms[enemyIndex].Position;
            float bodyRadius = math.max(0.05f, enemyDataArray[enemyIndex].BodyRadius);
            enemyRadii[enemyIndex] = bodyRadius;

            if (bodyRadius > maxEnemyRadius)
            {
                maxEnemyRadius = bodyRadius;
            }
        }

        float cellSize = EnemySpatialHashUtility.ResolveCellSize(maxEnemyRadius);
        float inverseCellSize = 1f / cellSize;
        NativeParallelMultiHashMap<int, int> enemyCellMap = new NativeParallelMultiHashMap<int, int>(enemyCount, Allocator.TempJob);
        EnemySpatialHashUtility.BuildCellMap(in enemyPositions, inverseCellSize, ref enemyCellMap);

        NativeArray<float3> projectilePositions = new NativeArray<float3>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<float> projectileRadii = new NativeArray<float>(projectileCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            projectilePositions[projectileIndex] = projectileTransforms[projectileIndex].Position;
            float projectileScale = math.max(0.01f, projectileTransforms[projectileIndex].Scale);
            float explosionRadius = math.max(0f, projectileDataArray[projectileIndex].ExplosionRadius);
            projectileRadii[projectileIndex] = math.max(0.005f, BaseProjectileHitRadius * projectileScale + explosionRadius);
        }

        NativeStream projectileHitStream = new NativeStream(projectileCount, Allocator.TempJob);

        EnemyProjectileHitCollectJob hitCollectJob = new EnemyProjectileHitCollectJob
        {
            ProjectilePositions = projectilePositions,
            ProjectileRadii = projectileRadii,
            EnemyPositions = enemyPositions,
            EnemyRadii = enemyRadii,
            CellMap = enemyCellMap,
            InverseCellSize = inverseCellSize,
            MaxEnemyRadius = maxEnemyRadius,
            HitStreamWriter = projectileHitStream.AsWriter()
        };

        JobHandle hitCollectHandle = hitCollectJob.Schedule(projectileCount, 64, state.Dependency);
        hitCollectHandle.Complete();

        NativeArray<EnemyHealth> projectedEnemyHealth = new NativeArray<EnemyHealth>(enemyHealthArray, Allocator.Temp);
        BufferLookup<ProjectilePoolElement> projectilePoolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<ShootRequest> shootRequestLookup = SystemAPI.GetBufferLookup<ShootRequest>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        BufferLookup<EnemyElementStackElement> elementalStackLookup = SystemAPI.GetBufferLookup<EnemyElementStackElement>(false);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);
        ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup = SystemAPI.GetComponentLookup<EnemyElementalVfxAnchor>(true);
        ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerElementalVfxConfig>(true);
        ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup = SystemAPI.GetComponentLookup<EnemyHitVfxConfig>(true);
        NativeStream.Reader projectileHitReader = projectileHitStream.AsReader();
        NativeList<ProjectileHitCandidate> hitCandidates = new NativeList<ProjectileHitCandidate>(16, Allocator.Temp);

        // Hits are resolved sequentially per projectile so penetration modes can update projected health correctly.
        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            int hitCount = projectileHitReader.BeginForEachIndex(projectileIndex);

            if (hitCount <= 0)
            {
                projectileHitReader.EndForEachIndex();
                continue;
            }

            Projectile projectileData = projectileDataArray[projectileIndex];
            ProjectileOwner projectileOwner = projectileOwnerArray[projectileIndex];
            ProjectileSplitState splitState = projectileSplitArray[projectileIndex];
            ProjectileElementalPayload elementalPayload = projectileElementalArray[projectileIndex];
            LocalTransform projectileTransform = projectileTransforms[projectileIndex];
            float currentScaleMultiplier = ResolveCurrentScaleMultiplier(projectileEntities[projectileIndex],
                                                                        projectileTransform.Scale,
                                                                        in projectileBaseScaleLookup);
            bool hasValidHit = false;
            bool canProjectileContinue = false;
            bool enemyKilledByProjectile = false;
            ElementalVfxDefinitionConfig elementalVfxConfig = default;
            bool canEnqueueShooterVfxRequests = vfxRequestLookup.HasBuffer(projectileOwner.ShooterEntity);
            DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests = default;

            if (canEnqueueShooterVfxRequests)
                shooterVfxRequests = vfxRequestLookup[projectileOwner.ShooterEntity];

            if (canEnqueueShooterVfxRequests && elementalPayload.Enabled != 0 && elementalPayload.StacksPerHit > 0f)
            {
                elementalVfxConfig = ResolveElementalVfxDefinition(projectileOwner.ShooterEntity,
                                                                   elementalPayload.Effect.ElementType,
                                                                   in elementalVfxConfigLookup);
            }

            hitCandidates.Clear();

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                int enemyIndex = projectileHitReader.Read<int>();

                if (enemyIndex < 0 || enemyIndex >= enemyCount)
                    continue;

                float3 delta = projectilePositions[projectileIndex] - enemyPositions[enemyIndex];
                delta.y = 0f;
                AddHitCandidateSorted(ref hitCandidates, enemyIndex, math.lengthsq(delta));
            }

            projectileHitReader.EndForEachIndex();

            if (hitCandidates.Length <= 0)
                continue;

            switch (projectileData.PenetrationMode)
            {
                case ProjectilePenetrationMode.Infinite:
                    for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
                    {
                        int enemyIndex = hitCandidates[candidateIndex].EnemyIndex;

                        if (!TryApplyFlatDamageHit(ref projectedEnemyHealth, enemyIndex, math.max(0f, projectileData.Damage), out bool enemyKilled))
                            continue;

                        hasValidHit = true;
                        enemyKilledByProjectile = enemyKilledByProjectile || enemyKilled;
                        ApplyHitPayloads(enemyIndex,
                                         in elementalPayload,
                                         in elementalVfxConfig,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         canEnqueueShooterVfxRequests,
                                         ref shooterVfxRequests,
                                         ref elementalStackLookup);
                    }

                    canProjectileContinue = hasValidHit;
                    break;
                case ProjectilePenetrationMode.FixedHits:
                    int originalRemainingPenetrations = math.max(0, projectileData.RemainingPenetrations);
                    int maximumHitCount = 1 + originalRemainingPenetrations;
                    int appliedHitCount = 0;

                    for (int candidateIndex = 0; candidateIndex < hitCandidates.Length && appliedHitCount < maximumHitCount; candidateIndex++)
                    {
                        int enemyIndex = hitCandidates[candidateIndex].EnemyIndex;

                        if (!TryApplyFlatDamageHit(ref projectedEnemyHealth, enemyIndex, math.max(0f, projectileData.Damage), out bool enemyKilled))
                            continue;

                        hasValidHit = true;
                        appliedHitCount++;
                        enemyKilledByProjectile = enemyKilledByProjectile || enemyKilled;
                        ApplyHitPayloads(enemyIndex,
                                         in elementalPayload,
                                         in elementalVfxConfig,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         canEnqueueShooterVfxRequests,
                                         ref shooterVfxRequests,
                                         ref elementalStackLookup);
                    }

                    projectileData.RemainingPenetrations = math.max(0, originalRemainingPenetrations - appliedHitCount);
                    canProjectileContinue = hasValidHit && appliedHitCount <= originalRemainingPenetrations;
                    break;
                case ProjectilePenetrationMode.DamageBased:
                    int originalDamagePenetrations = math.max(0, projectileData.RemainingPenetrations);
                    int consumedPenetrations = 0;
                    float remainingDamage = math.max(0f, projectileData.Damage);

                    for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
                    {
                        int enemyIndex = hitCandidates[candidateIndex].EnemyIndex;
                        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

                        if (enemyHealth.Current <= 0f)
                            continue;

                        hasValidHit = true;
                        bool enemyKilled = false;
                        float leftoverDamage = 0f;

                        if (remainingDamage > 0f)
                        {
                            leftoverDamage = ApplyDamageBasedHit(ref projectedEnemyHealth, enemyIndex, remainingDamage, out enemyKilled);
                        }

                        enemyKilledByProjectile = enemyKilledByProjectile || enemyKilled;
                        ApplyHitPayloads(enemyIndex,
                                         in elementalPayload,
                                         in elementalVfxConfig,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         canEnqueueShooterVfxRequests,
                                         ref shooterVfxRequests,
                                         ref elementalStackLookup);

                        if (remainingDamage <= 0f || leftoverDamage <= 0f || !enemyKilled)
                        {
                            remainingDamage = 0f;
                            break;
                        }

                        consumedPenetrations++;
                        remainingDamage = leftoverDamage;

                        if (consumedPenetrations > originalDamagePenetrations)
                        {
                            remainingDamage = 0f;
                            break;
                        }
                    }

                    projectileData.Damage = remainingDamage;
                    projectileData.RemainingPenetrations = math.max(0, originalDamagePenetrations - consumedPenetrations);
                    canProjectileContinue = hasValidHit && remainingDamage > 0f && consumedPenetrations <= originalDamagePenetrations;
                    break;
                default:
                    for (int candidateIndex = 0; candidateIndex < hitCandidates.Length; candidateIndex++)
                    {
                        int enemyIndex = hitCandidates[candidateIndex].EnemyIndex;

                        if (!TryApplyFlatDamageHit(ref projectedEnemyHealth, enemyIndex, math.max(0f, projectileData.Damage), out bool enemyKilled))
                            continue;

                        hasValidHit = true;
                        enemyKilledByProjectile = enemyKilledByProjectile || enemyKilled;
                        ApplyHitPayloads(enemyIndex,
                                         in elementalPayload,
                                         in elementalVfxConfig,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         canEnqueueShooterVfxRequests,
                                         ref shooterVfxRequests,
                                         ref elementalStackLookup);
                        break;
                    }

                    canProjectileContinue = false;
                    break;
            }

            if (!hasValidHit)
                continue;

            bool shouldSplitOnHitEvent = ProjectileSplitUtility.ShouldSplitOnHitEvent(in splitState, enemyKilledByProjectile);

            if (canProjectileContinue)
            {
                entityManager.SetComponentData(projectileEntities[projectileIndex], projectileData);

                if (shouldSplitOnHitEvent)
                {
                    // Split creation is centralized in ProjectileSplitUtility to keep hit/despawn behavior consistent.
                    ProjectileSplitUtility.TryEnqueueSplitRequests(in projectileData,
                                                                   in splitState,
                                                                   in projectileTransform,
                                                                   currentScaleMultiplier,
                                                                   in elementalPayload,
                                                                   in projectileOwner,
                                                                   ref shootRequestLookup);
                    splitState.CanSplit = 0;
                    entityManager.SetComponentData(projectileEntities[projectileIndex], splitState);
                }

                continue;
            }

            bool shouldSplitOnDespawnEvent = ProjectileSplitUtility.ShouldSplitOnDespawn(in splitState);

            if (shouldSplitOnHitEvent || shouldSplitOnDespawnEvent)
            {
                // Same utility path for despawn-triggered splits to avoid duplicate split math in this system.
                ProjectileSplitUtility.TryEnqueueSplitRequests(in projectileData,
                                                               in splitState,
                                                               in projectileTransform,
                                                               currentScaleMultiplier,
                                                               in elementalPayload,
                                                               in projectileOwner,
                                                               ref shootRequestLookup);
                splitState.CanSplit = 0;
                entityManager.SetComponentData(projectileEntities[projectileIndex], splitState);
            }

            DespawnProjectile(entityManager, projectileEntities[projectileIndex], projectileOwner, ref projectilePoolLookup);
        }

        ComponentLookup<EnemyDespawnRequest> despawnLookup = SystemAPI.GetComponentLookup<EnemyDespawnRequest>(true);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
        {
            if (projectedEnemyHealth[enemyIndex].Current == enemyHealthArray[enemyIndex].Current &&
                projectedEnemyHealth[enemyIndex].CurrentShield == enemyHealthArray[enemyIndex].CurrentShield)
            {
                continue;
            }

            Entity enemyEntity = enemyEntities[enemyIndex];

            if (!entityManager.Exists(enemyEntity))
                continue;

            EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];
            entityManager.SetComponentData(enemyEntity, enemyHealth);
            DamageFlashRuntimeUtility.Trigger(entityManager, enemyEntity);

            if (enemyHealth.Current <= 0f)
            {
                if (!despawnLookup.HasComponent(enemyEntity))
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
        projectileHitStream.Dispose();
        enemyCellMap.Dispose();
        projectedEnemyHealth.Dispose();
        hitCandidates.Dispose();
    }
    #endregion

    #region Helpers
    private static void AddHitCandidateSorted(ref NativeList<ProjectileHitCandidate> hitCandidates,
                                              int enemyIndex,
                                              float distanceSquared)
    {
        hitCandidates.Add(new ProjectileHitCandidate
        {
            EnemyIndex = enemyIndex,
            DistanceSquared = distanceSquared
        });

        for (int candidateIndex = hitCandidates.Length - 1; candidateIndex > 0; candidateIndex--)
        {
            ProjectileHitCandidate currentCandidate = hitCandidates[candidateIndex];
            ProjectileHitCandidate previousCandidate = hitCandidates[candidateIndex - 1];

            if (previousCandidate.DistanceSquared <= currentCandidate.DistanceSquared)
                break;

            hitCandidates[candidateIndex - 1] = currentCandidate;
            hitCandidates[candidateIndex] = previousCandidate;
        }
    }

    private static bool TryApplyFlatDamageHit(ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                              int enemyIndex,
                                              float damage,
                                              out bool enemyKilled)
    {
        enemyKilled = false;

        if (enemyIndex < 0 || enemyIndex >= projectedEnemyHealth.Length)
            return false;

        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return false;

        EnemyDamageUtility.ApplyFlatShieldDamage(ref enemyHealth, damage);
        enemyKilled = enemyHealth.Current <= 0f;
        projectedEnemyHealth[enemyIndex] = enemyHealth;
        return true;
    }

    private static float ApplyDamageBasedHit(ref NativeArray<EnemyHealth> projectedEnemyHealth,
                                             int enemyIndex,
                                             float damage,
                                             out bool enemyKilled)
    {
        enemyKilled = false;

        if (enemyIndex < 0 || enemyIndex >= projectedEnemyHealth.Length)
            return 0f;

        EnemyHealth enemyHealth = projectedEnemyHealth[enemyIndex];

        if (enemyHealth.Current <= 0f)
            return 0f;

        float leftoverDamage = EnemyDamageUtility.ConsumeFlatShieldDamage(ref enemyHealth, damage);
        enemyKilled = enemyHealth.Current <= 0f;
        projectedEnemyHealth[enemyIndex] = enemyHealth;
        return leftoverDamage;
    }

    private static void ApplyHitPayloads(int enemyIndex,
                                         in ProjectileElementalPayload elementalPayload,
                                         in ElementalVfxDefinitionConfig elementalVfxConfig,
                                         NativeArray<Entity> enemyEntities,
                                         NativeArray<float3> enemyPositions,
                                         NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                         in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                         in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                         bool canEnqueueShooterVfxRequests,
                                         ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                         ref BufferLookup<EnemyElementStackElement> elementalStackLookup)
    {
        Entity enemyEntity = enemyEntities[enemyIndex];
        float3 enemyPosition = enemyPositions[enemyIndex];
        EnemyRuntimeState enemyRuntimeState = enemyRuntimeArray[enemyIndex];
        TryApplyElementalPayload(enemyEntity,
                                 enemyPosition,
                                 in elementalPayload,
                                 in enemyRuntimeState,
                                 in elementalVfxConfig,
                                 in elementalVfxAnchorLookup,
                                 canEnqueueShooterVfxRequests,
                                 ref shooterVfxRequests,
                                 ref elementalStackLookup);
        TryEnqueueEnemyHitVfx(enemyEntity,
                              enemyPosition,
                              in enemyRuntimeState,
                              in enemyHitVfxConfigLookup,
                              canEnqueueShooterVfxRequests,
                              ref shooterVfxRequests);
    }

    private static float ResolveCurrentScaleMultiplier(Entity projectileEntity,
                                                       float currentScale,
                                                       in ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup)
    {
        if (!projectileBaseScaleLookup.HasComponent(projectileEntity))
            return math.max(0.01f, currentScale);

        float baseScale = math.max(0.0001f, projectileBaseScaleLookup[projectileEntity].Value);
        return math.max(0.01f, currentScale / baseScale);
    }

    private static void TryApplyElementalPayload(Entity enemyEntity,
                                                 float3 enemyPosition,
                                                 in ProjectileElementalPayload elementalPayload,
                                                 in EnemyRuntimeState enemyRuntimeState,
                                                 in ElementalVfxDefinitionConfig elementalVfxConfig,
                                                 in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                                 bool canEnqueueVfxRequests,
                                                 ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                                                 ref BufferLookup<EnemyElementStackElement> elementalStackLookup)
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

        if (!applied)
            return;

        if (!canEnqueueVfxRequests)
            return;

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

        if (elementalVfxConfig.SpawnStackVfx != 0)
            EnqueueElementalVfx(ref vfxRequests,
                                elementalVfxConfig.StackVfxPrefabEntity,
                                vfxPosition,
                                elementalVfxConfig.StackVfxScaleMultiplier,
                                followTargetEntity,
                                enemyEntity,
                                enemyRuntimeState.SpawnVersion,
                                stackVfxLifetimeSeconds);

        if (procTriggered && elementalVfxConfig.SpawnProcVfx != 0)
            EnqueueElementalVfx(ref vfxRequests,
                                elementalVfxConfig.ProcVfxPrefabEntity,
                                vfxPosition,
                                elementalVfxConfig.ProcVfxScaleMultiplier,
                                followTargetEntity,
                                enemyEntity,
                                enemyRuntimeState.SpawnVersion,
                                procVfxLifetimeSeconds);
    }

    /// <summary>
    /// Queues a one-shot hit-react VFX request when the target enemy has a valid hit VFX configuration.
    /// </summary>
    /// <param name="enemyEntity">Enemy entity that received the hit.</param>
    /// <param name="enemyPosition">World position used as spawn anchor for the one-shot VFX.</param>
    /// <param name="enemyRuntimeState">Enemy runtime data used for spawn-version validation metadata.</param>
    /// <param name="enemyHitVfxConfigLookup">Lookup used to resolve baked hit VFX settings.</param>
    /// <param name="canEnqueueVfxRequests">True when the shooter has a writable VFX request buffer.</param>
    /// <param name="vfxRequests">Writable shooter-side VFX request buffer.</param>
    private static void TryEnqueueEnemyHitVfx(Entity enemyEntity,
                                              float3 enemyPosition,
                                              in EnemyRuntimeState enemyRuntimeState,
                                              in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                              bool canEnqueueVfxRequests,
                                              ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests)
    {
        if (!canEnqueueVfxRequests)
            return;

        if (!enemyHitVfxConfigLookup.HasComponent(enemyEntity))
            return;

        EnemyHitVfxConfig hitVfxConfig = enemyHitVfxConfigLookup[enemyEntity];

        if (hitVfxConfig.PrefabEntity == Entity.Null)
            return;

        vfxRequests.Add(new PlayerPowerUpVfxSpawnRequest
        {
            PrefabEntity = hitVfxConfig.PrefabEntity,
            Position = enemyPosition,
            Rotation = quaternion.identity,
            UniformScale = math.max(0.01f, hitVfxConfig.ScaleMultiplier),
            LifetimeSeconds = math.max(0.05f, hitVfxConfig.LifetimeSeconds),
            FollowTargetEntity = Entity.Null,
            FollowPositionOffset = float3.zero,
            FollowValidationEntity = enemyEntity,
            FollowValidationSpawnVersion = enemyRuntimeState.SpawnVersion,
            Velocity = float3.zero
        });
    }

    private static ElementalVfxDefinitionConfig ResolveElementalVfxDefinition(Entity shooterEntity,
                                                                              ElementType elementType,
                                                                              in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup)
    {
        if (shooterEntity == Entity.Null || shooterEntity.Index < 0)
            return default;

        if (!elementalVfxConfigLookup.HasComponent(shooterEntity))
            return default;

        PlayerElementalVfxConfig elementalVfxConfig = elementalVfxConfigLookup[shooterEntity];

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

    private static void DespawnProjectile(EntityManager entityManager,
                                          Entity projectileEntity,
                                          ProjectileOwner projectileOwner,
                                          ref BufferLookup<ProjectilePoolElement> projectilePoolLookup)
    {
        if (!entityManager.Exists(projectileEntity))
            return;

        ProjectilePoolUtility.SetProjectileParked(entityManager, projectileEntity);
        entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);

        Entity shooterEntity = projectileOwner.ShooterEntity;

        if (!projectilePoolLookup.HasBuffer(shooterEntity))
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
    private struct EnemyProjectileHitCollectJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> ProjectilePositions;
        [ReadOnly] public NativeArray<float> ProjectileRadii;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<float> EnemyRadii;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> CellMap;
        [ReadOnly] public float InverseCellSize;
        [ReadOnly] public float MaxEnemyRadius;
        public NativeStream.Writer HitStreamWriter;

        public void Execute(int index)
        {
            float3 projectilePosition = ProjectilePositions[index];
            float projectileRadius = math.max(0.005f, ProjectileRadii[index]);
            float searchRadius = math.max(0.01f, projectileRadius + math.max(0.05f, MaxEnemyRadius));
            EnemySpatialHashUtility.ResolveCellBounds(projectilePosition,
                                                      searchRadius,
                                                      InverseCellSize,
                                                      out int minCellX,
                                                      out int maxCellX,
                                                      out int minCellY,
                                                      out int maxCellY);

            NativeStream.Writer streamWriter = HitStreamWriter;
            streamWriter.BeginForEachIndex(index);

            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    int key = EnemySpatialHashUtility.EncodeCell(cellX, cellY);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    int enemyIndex;

                    if (!CellMap.TryGetFirstValue(key, out enemyIndex, out iterator))
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

                        streamWriter.Write(enemyIndex);
                    }
                    while (CellMap.TryGetNextValue(out enemyIndex, ref iterator));
                }
            }

            streamWriter.EndForEachIndex();
        }
    }
    #endregion
}
