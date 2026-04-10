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
            .WithAll<EnemyData, EnemyHealth, EnemyRuntimeState, EnemyKnockbackState, LocalTransform, EnemyActive>()
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
        NativeArray<EnemyKnockbackState> enemyKnockbackArray = new NativeArray<EnemyKnockbackState>(enemyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        int enemyWriteIndex = 0;

        // Snapshot query data into contiguous arrays so Burst jobs can run over stable memory.
        foreach ((RefRO<EnemyData> enemyData,
                  RefRO<EnemyHealth> enemyHealth,
                  RefRO<LocalTransform> enemyTransform,
                  RefRO<EnemyRuntimeState> enemyRuntimeState,
                  RefRO<EnemyKnockbackState> enemyKnockbackState,
                  Entity enemyEntity) in SystemAPI.Query<RefRO<EnemyData>,
                                                         RefRO<EnemyHealth>,
                                                         RefRO<LocalTransform>,
                                                         RefRO<EnemyRuntimeState>,
                                                         RefRO<EnemyKnockbackState>>()
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
            enemyKnockbackArray[enemyWriteIndex] = enemyKnockbackState.ValueRO;
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
            enemyKnockbackArray.Dispose();
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
            enemyKnockbackArray.Dispose();
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
        NativeArray<EnemyKnockbackState> projectedEnemyKnockback = new NativeArray<EnemyKnockbackState>(enemyKnockbackArray, Allocator.Temp);
        BufferLookup<ProjectilePoolElement> projectilePoolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<ShootRequest> shootRequestLookup = SystemAPI.GetBufferLookup<ShootRequest>(false);
        BufferLookup<PlayerPowerUpVfxSpawnRequest> vfxRequestLookup = SystemAPI.GetBufferLookup<PlayerPowerUpVfxSpawnRequest>(false);
        BufferLookup<EnemyElementStackElement> elementalStackLookup = SystemAPI.GetBufferLookup<EnemyElementStackElement>(false);
        ComponentLookup<ProjectileBaseScale> projectileBaseScaleLookup = SystemAPI.GetComponentLookup<ProjectileBaseScale>(true);
        ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup = SystemAPI.GetComponentLookup<EnemyElementalVfxAnchor>(true);
        ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup = SystemAPI.GetComponentLookup<PlayerElementalVfxConfig>(true);
        ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup = SystemAPI.GetComponentLookup<EnemyHitVfxConfig>(true);
        ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup = SystemAPI.GetComponentLookup<EnemySpawnInactivityLock>(true);
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
            bool canEnqueueShooterVfxRequests = vfxRequestLookup.HasBuffer(projectileOwner.ShooterEntity);
            DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests = default;

            if (canEnqueueShooterVfxRequests)
                shooterVfxRequests = vfxRequestLookup[projectileOwner.ShooterEntity];

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
                                         projectileOwner.ShooterEntity,
                                         in projectileData,
                                         in projectileTransform,
                                         in elementalPayload,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         ref projectedEnemyKnockback,
                                         in elementalVfxConfigLookup,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         in spawnInactivityLockLookup,
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
                                         projectileOwner.ShooterEntity,
                                         in projectileData,
                                         in projectileTransform,
                                         in elementalPayload,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         ref projectedEnemyKnockback,
                                         in elementalVfxConfigLookup,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         in spawnInactivityLockLookup,
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
                                         projectileOwner.ShooterEntity,
                                         in projectileData,
                                         in projectileTransform,
                                         in elementalPayload,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         ref projectedEnemyKnockback,
                                         in elementalVfxConfigLookup,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         in spawnInactivityLockLookup,
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
                                         projectileOwner.ShooterEntity,
                                         in projectileData,
                                         in projectileTransform,
                                         in elementalPayload,
                                         enemyEntities,
                                         enemyPositions,
                                         enemyRuntimeArray,
                                         ref projectedEnemyKnockback,
                                         in elementalVfxConfigLookup,
                                         in elementalVfxAnchorLookup,
                                         in enemyHitVfxConfigLookup,
                                         in spawnInactivityLockLookup,
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
            Entity enemyEntity = enemyEntities[enemyIndex];

            if (!entityManager.Exists(enemyEntity))
                continue;

            bool healthChanged = projectedEnemyHealth[enemyIndex].Current != enemyHealthArray[enemyIndex].Current ||
                                 projectedEnemyHealth[enemyIndex].CurrentShield != enemyHealthArray[enemyIndex].CurrentShield;
            bool knockbackChanged = DidKnockbackStateChange(projectedEnemyKnockback[enemyIndex], enemyKnockbackArray[enemyIndex]);

            if (!healthChanged && !knockbackChanged)
                continue;

            if (knockbackChanged)
                entityManager.SetComponentData(enemyEntity, projectedEnemyKnockback[enemyIndex]);

            if (!healthChanged)
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
        enemyKnockbackArray.Dispose();

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
        projectedEnemyKnockback.Dispose();
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
                                         Entity shooterEntity,
                                         in Projectile projectileData,
                                         in LocalTransform projectileTransform,
                                         in ProjectileElementalPayload elementalPayload,
                                         NativeArray<Entity> enemyEntities,
                                         NativeArray<float3> enemyPositions,
                                         NativeArray<EnemyRuntimeState> enemyRuntimeArray,
                                         ref NativeArray<EnemyKnockbackState> projectedEnemyKnockback,
                                         in ComponentLookup<PlayerElementalVfxConfig> elementalVfxConfigLookup,
                                         in ComponentLookup<EnemyElementalVfxAnchor> elementalVfxAnchorLookup,
                                         in ComponentLookup<EnemyHitVfxConfig> enemyHitVfxConfigLookup,
                                         in ComponentLookup<EnemySpawnInactivityLock> spawnInactivityLockLookup,
                                         bool canEnqueueShooterVfxRequests,
                                         ref DynamicBuffer<PlayerPowerUpVfxSpawnRequest> shooterVfxRequests,
                                         ref BufferLookup<EnemyElementStackElement> elementalStackLookup)
    {
        float3 impactPosition = enemyPositions[enemyIndex];
        EnemyHitPayloadRuntimeUtility.ApplyEnemyHitPayloads(enemyIndex,
                                                            shooterEntity,
                                                            impactPosition,
                                                            in projectileData,
                                                            in projectileTransform,
                                                            in elementalPayload,
                                                            enemyEntities,
                                                            enemyPositions,
                                                            enemyRuntimeArray,
                                                            ref projectedEnemyKnockback,
                                                            in elementalVfxConfigLookup,
                                                            in elementalVfxAnchorLookup,
                                                            in enemyHitVfxConfigLookup,
                                                            in spawnInactivityLockLookup,
                                                            canEnqueueShooterVfxRequests,
                                                            ref shooterVfxRequests,
                                                            ref elementalStackLookup);
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

    private static bool DidKnockbackStateChange(EnemyKnockbackState leftValue, EnemyKnockbackState rightValue)
    {
        return leftValue.RemainingTime != rightValue.RemainingTime ||
               leftValue.Velocity.x != rightValue.Velocity.x ||
               leftValue.Velocity.y != rightValue.Velocity.y ||
               leftValue.Velocity.z != rightValue.Velocity.z;
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
