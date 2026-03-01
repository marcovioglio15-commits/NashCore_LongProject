using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// This system processes shooter entities with pending shoot requests, 
/// ensuring that their projectile pools are expanded as needed. It is updated after the 
/// PlayerShootingIntentSystem, which generates shoot requests based on player input and shooting state,
/// and after the ProjectilePoolInitializeSystem, which initializes projectile pools for shooters.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerShootingIntentSystem))]
[UpdateAfter(typeof(ProjectilePoolInitializeSystem))]
public partial struct ProjectileSpawnSystem : ISystem
{
    #region Constants
    private const float MinimumProjectileScale = 0.0001f;
    #endregion

    #region Fields
    private EntityQuery shootersWithRequestsQuery;
    #endregion


    #region Lifecycle
    /// <summary>
    /// Configures the system to require updates for shooter entities that have a ShooterProjectilePrefab, 
    /// ProjectilePoolState, ProjectilePoolElement buffer, and ShootRequest buffer.
    /// </summary>
    /// <param name="state"></param>
    public void OnCreate(ref SystemState state)
    {
        // Define an EntityQuery to select shooter entities that have the necessary components
        // for processing shoot requests. This includes the ShooterProjectilePrefab to identify
        // the projectile prefab to use,
        shootersWithRequestsQuery = SystemAPI.QueryBuilder()
            .WithAll<ShooterProjectilePrefab, ProjectilePoolState, ProjectilePoolElement, ShootRequest>()
            .Build();

        // Require the query for updates to ensure the system only runs when there are shooter entities with shoot requests to process
        state.RequireForUpdate(shootersWithRequestsQuery);
    }
    
    /// <summary>
    /// Processes shooter entities with pending shoot requests, expands projectile pools as needed, and spawns and
    /// initializes projectiles based on the requests.
    /// </summary>
    /// <param name="state">The current system state used to access the EntityManager and other ECS data.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        ComponentLookup<PlayerPassiveToolsState> passiveToolsLookup = SystemAPI.GetComponentLookup<PlayerPassiveToolsState>(true);

        foreach ((DynamicBuffer<ShootRequest> shootRequests,
                  DynamicBuffer<ProjectilePoolElement> projectilePool,
                  RefRO<ShooterProjectilePrefab> projectilePrefab,
                  RefRO<ProjectilePoolState> poolStateValue,
                  Entity shooterEntity) in SystemAPI.Query<DynamicBuffer<ShootRequest>,
                                                           DynamicBuffer<ProjectilePoolElement>,
                                                           RefRO<ShooterProjectilePrefab>,
                                                           RefRO<ProjectilePoolState>>()
                                                   .WithEntityAccess())
        {
            if (entityManager.Exists(shooterEntity) == false)
                continue;

            if (entityManager.HasComponent<Projectile>(shooterEntity))
                continue;

            if (shootRequests.Length == 0)
                continue;

            ProjectilePoolState poolState = poolStateValue.ValueRO;

            if (poolState.Initialized == 0)
                continue;

            DynamicBuffer<ShootRequest> shooterShootRequests = shootRequests;
            DynamicBuffer<ProjectilePoolElement> shooterProjectilePool = projectilePool;
            Entity prefabEntity = projectilePrefab.ValueRO.PrefabEntity;

            if (prefabEntity == Entity.Null || entityManager.Exists(prefabEntity) == false)
            {
                shooterShootRequests.Clear();
                continue;
            }

            PlayerPassiveToolsState passiveToolsState = ResolvePassiveToolsState(shooterEntity, in passiveToolsLookup);
            int missingProjectiles = shooterShootRequests.Length - shooterProjectilePool.Length;

            if (missingProjectiles > 0)
            {
                int expandBatch = math.max(1, poolState.ExpandBatch);
                int expandCount = math.max(expandBatch, missingProjectiles);
                ProjectilePoolUtility.ExpandPool(entityManager, shooterEntity, prefabEntity, expandCount);
                shooterShootRequests = entityManager.GetBuffer<ShootRequest>(shooterEntity);
                shooterProjectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);
            }

            int requestsCount = shooterShootRequests.Length;

            for (int requestIndex = 0; requestIndex < requestsCount; requestIndex++)
            {
                if (shooterProjectilePool.Length == 0)
                    break;

                int lastIndex = shooterProjectilePool.Length - 1;
                Entity projectileEntity = shooterProjectilePool[lastIndex].ProjectileEntity;
                shooterProjectilePool.RemoveAt(lastIndex);

                if (entityManager.Exists(projectileEntity) == false)
                    continue;

                ShootRequest request = shooterShootRequests[requestIndex];
                float3 direction = math.normalizesafe(request.Direction, new float3(0f, 0f, 1f));
                float speed = math.max(0f, request.Speed);

                if (passiveToolsState.HasPerfectCircle != 0)
                    speed = math.max(0f, passiveToolsState.PerfectCircle.RadialEntrySpeed);

                LocalTransform projectileTransform = entityManager.GetComponentData<LocalTransform>(projectileEntity);
                projectileTransform.Position = request.Position;
                projectileTransform.Rotation = quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));

                float baseScale = 1f;

                if (entityManager.HasComponent<ProjectileBaseScale>(projectileEntity))
                    baseScale = math.max(MinimumProjectileScale, entityManager.GetComponentData<ProjectileBaseScale>(projectileEntity).Value);
                else
                {
                    baseScale = math.max(MinimumProjectileScale, projectileTransform.Scale);
                    entityManager.AddComponentData(projectileEntity, new ProjectileBaseScale
                    {
                        Value = baseScale
                    });
                }

                float scaleMultiplier = math.max(0.01f, request.ProjectileScaleMultiplier);
                projectileTransform.Scale = baseScale * scaleMultiplier;
                entityManager.SetComponentData(projectileEntity, projectileTransform);

                Projectile projectileData = new Projectile
                {
                    Velocity = direction * speed,
                    Damage = math.max(0f, request.Damage),
                    ExplosionRadius = math.max(0f, request.ExplosionRadius),
                    MaxRange = request.Range,
                    MaxLifetime = request.Lifetime,
                    PenetrationMode = request.PenetrationMode,
                    RemainingPenetrations = math.max(0, request.MaxPenetrations),
                    InheritPlayerSpeed = request.InheritPlayerSpeed
                };

                entityManager.SetComponentData(projectileEntity, projectileData);
                entityManager.SetComponentData(projectileEntity, new ProjectileRuntimeState
                {
                    TraveledDistance = 0f,
                    ElapsedLifetime = 0f
                });
                entityManager.SetComponentData(projectileEntity, new ProjectileOwner
                {
                    ShooterEntity = shooterEntity
                });

                ProjectilePerfectCircleState perfectCircleState = BuildPerfectCircleState(in passiveToolsState.PerfectCircle,
                                                                                          requestIndex,
                                                                                          shooterEntity,
                                                                                          request.Position,
                                                                                          direction,
                                                                                          projectileData.Velocity,
                                                                                          passiveToolsState.HasPerfectCircle != 0);
                entityManager.SetComponentData(projectileEntity, perfectCircleState);

                ProjectileBounceState bounceState = BuildBounceState(in passiveToolsState.BouncingProjectiles, passiveToolsState.HasBouncingProjectiles != 0);
                entityManager.SetComponentData(projectileEntity, bounceState);

                ProjectileSplitState splitState = BuildSplitState(in passiveToolsState.SplittingProjectiles, passiveToolsState.HasSplittingProjectiles != 0, request.IsSplitChild != 0);
                entityManager.SetComponentData(projectileEntity, splitState);

                ProjectileElementalPayload elementalPayload = ResolveElementalPayload(in request,
                                                                                      in passiveToolsState.ElementalProjectiles,
                                                                                      passiveToolsState.HasElementalProjectiles != 0);
                entityManager.SetComponentData(projectileEntity, elementalPayload);

                entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, true);
            }

            shooterShootRequests.Clear();
        }
    }

    private static PlayerPassiveToolsState ResolvePassiveToolsState(Entity shooterEntity, in ComponentLookup<PlayerPassiveToolsState> passiveToolsLookup)
    {
        if (passiveToolsLookup.HasComponent(shooterEntity))
            return passiveToolsLookup[shooterEntity];

        return default;
    }

    private static ProjectilePerfectCircleState BuildPerfectCircleState(in PerfectCirclePassiveConfig perfectCircleConfig,
                                                                        int requestIndex,
                                                                        Entity shooterEntity,
                                                                        float3 spawnPosition,
                                                                        float3 direction,
                                                                        float3 entryVelocity,
                                                                        bool isEnabled)
    {
        if (isEnabled == false)
            return default;

        float seed = requestIndex + shooterEntity.Index * 13f;
        float angleRadians = math.radians(math.max(0f, perfectCircleConfig.GoldenAngleDegrees) * seed);
        float3 radialDirection = direction;

        if (math.lengthsq(radialDirection) <= 1e-6f)
            radialDirection = new float3(math.cos(angleRadians), 0f, math.sin(angleRadians));

        radialDirection = math.normalizesafe(radialDirection, new float3(0f, 0f, 1f));

        return new ProjectilePerfectCircleState
        {
            Enabled = 1,
            HasEnteredOrbit = 0,
            CompletedFullOrbit = 0,
            EntryOrigin = spawnPosition,
            OrbitAngle = angleRadians,
            OrbitBlendProgress = 0f,
            CurrentRadius = 0f,
            AccumulatedOrbitRadians = 0f,
            RadialDirection = radialDirection,
            EntryVelocity = entryVelocity
        };
    }

    private static ProjectileBounceState BuildBounceState(in BouncingProjectilesPassiveConfig bouncingProjectilesConfig, bool isEnabled)
    {
        if (isEnabled == false || bouncingProjectilesConfig.MaxBounces <= 0)
            return default;

        float minimumSpeedMultiplier = math.max(0f, bouncingProjectilesConfig.MinimumSpeedMultiplierAfterBounce);
        float maximumSpeedMultiplier = math.max(minimumSpeedMultiplier, bouncingProjectilesConfig.MaximumSpeedMultiplierAfterBounce);

        return new ProjectileBounceState
        {
            RemainingBounces = math.max(0, bouncingProjectilesConfig.MaxBounces),
            SpeedPercentChangePerBounce = bouncingProjectilesConfig.SpeedPercentChangePerBounce,
            MinimumSpeedMultiplierAfterBounce = minimumSpeedMultiplier,
            MaximumSpeedMultiplierAfterBounce = maximumSpeedMultiplier,
            CurrentSpeedMultiplier = 1f
        };
    }

    private static ProjectileSplitState BuildSplitState(in SplittingProjectilesPassiveConfig splittingProjectilesConfig, bool isEnabled, bool isSplitChild)
    {
        if (isEnabled == false || isSplitChild)
            return default;

        return new ProjectileSplitState
        {
            CanSplit = 1,
            TriggerMode = splittingProjectilesConfig.TriggerMode,
            DirectionMode = splittingProjectilesConfig.DirectionMode,
            SplitProjectileCount = math.max(1, splittingProjectilesConfig.SplitProjectileCount),
            SplitOffsetDegrees = splittingProjectilesConfig.SplitOffsetDegrees,
            CustomAnglesDegrees = splittingProjectilesConfig.CustomAnglesDegrees,
            SplitDamageMultiplier = math.max(0f, splittingProjectilesConfig.SplitDamageMultiplier),
            SplitSizeMultiplier = math.max(0f, splittingProjectilesConfig.SplitSizeMultiplier),
            SplitSpeedMultiplier = math.max(0f, splittingProjectilesConfig.SplitSpeedMultiplier),
            SplitLifetimeMultiplier = math.max(0f, splittingProjectilesConfig.SplitLifetimeMultiplier)
        };
    }

    private static ProjectileElementalPayload ResolveElementalPayload(in ShootRequest request,
                                                                      in ElementalProjectilesPassiveConfig passiveElementalProjectilesConfig,
                                                                      bool hasPassiveElementalPayload)
    {
        ProjectileElementalPayload requestPayload = BuildElementalPayloadFromRequest(in request);

        if (requestPayload.Enabled != 0)
            return requestPayload;

        return BuildElementalPayloadFromPassive(in passiveElementalProjectilesConfig, hasPassiveElementalPayload);
    }

    private static ProjectileElementalPayload BuildElementalPayloadFromRequest(in ShootRequest request)
    {
        if (request.HasElementalPayloadOverride == 0 || request.ElementalStacksPerHitOverride <= 0f)
            return default;

        return new ProjectileElementalPayload
        {
            Enabled = 1,
            Effect = request.ElementalEffectOverride,
            StacksPerHit = math.max(0f, request.ElementalStacksPerHitOverride)
        };
    }

    private static ProjectileElementalPayload BuildElementalPayloadFromPassive(in ElementalProjectilesPassiveConfig elementalProjectilesConfig, bool isEnabled)
    {
        if (isEnabled == false || elementalProjectilesConfig.StacksPerHit <= 0f)
            return default;

        return new ProjectileElementalPayload
        {
            Enabled = 1,
            Effect = elementalProjectilesConfig.Effect,
            StacksPerHit = math.max(0f, elementalProjectilesConfig.StacksPerHit)
        };
    }

    #endregion

}
