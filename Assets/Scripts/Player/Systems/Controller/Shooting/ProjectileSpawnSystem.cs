using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerShootingIntentSystem))]
[UpdateAfter(typeof(ProjectilePoolInitializeSystem))]
public partial struct ProjectileSpawnSystem : ISystem
{
    #region Fields
    private EntityQuery shootersWithRequestsQuery;
    #endregion


    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        shootersWithRequestsQuery = SystemAPI.QueryBuilder()
            .WithAll<ShooterProjectilePrefab, ProjectilePoolState, ProjectilePoolElement, ShootRequest>()
            .Build();

        state.RequireForUpdate(shootersWithRequestsQuery);
    }
    #endregion

    #region Update
    /// <summary>
    /// Processes shooter entities with pending shoot requests, expands projectile pools as needed, and spawns and
    /// initializes projectiles based on the requests.
    /// </summary>
    /// <param name="state">The current system state used to access the EntityManager and other ECS data.</param>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        NativeArray<Entity> shooterEntities = shootersWithRequestsQuery.ToEntityArray(Allocator.Temp);

        // Iterate through each shooter entity that has shoot requests
        for (int shooterIndex = 0; shooterIndex < shooterEntities.Length; shooterIndex++)
        {
            Entity shooterEntity = shooterEntities[shooterIndex];

            if (entityManager.Exists(shooterEntity) == false)
                continue;

            if (entityManager.HasComponent<Projectile>(shooterEntity))
                continue;

            DynamicBuffer<ShootRequest> shootRequests = entityManager.GetBuffer<ShootRequest>(shooterEntity);

            if (shootRequests.Length == 0)
                continue;

            ProjectilePoolState poolState = entityManager.GetComponentData<ProjectilePoolState>(shooterEntity);

            if (poolState.Initialized == 0)
                continue;


            ShooterProjectilePrefab projectilePrefab = entityManager.GetComponentData<ShooterProjectilePrefab>(shooterEntity);
            Entity prefabEntity = projectilePrefab.PrefabEntity;

            if (prefabEntity == Entity.Null || entityManager.Exists(prefabEntity) == false)
            {
                shootRequests.Clear();
                continue;
            }

            DynamicBuffer<ProjectilePoolElement> projectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);
            int missingProjectiles = shootRequests.Length - projectilePool.Length;

            if (missingProjectiles > 0)
            {
                int expandBatch = math.max(1, poolState.ExpandBatch);
                int expandCount = math.max(expandBatch, missingProjectiles);
                ProjectilePoolUtility.ExpandPool(entityManager, shooterEntity, prefabEntity, expandCount);
                shootRequests = entityManager.GetBuffer<ShootRequest>(shooterEntity);
                projectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);
            }

            int requestsCount = shootRequests.Length;

            for (int requestIndex = 0; requestIndex < requestsCount; requestIndex++)
            {
                if (projectilePool.Length == 0)
                    break;

                int lastIndex = projectilePool.Length - 1;
                Entity projectileEntity = projectilePool[lastIndex].ProjectileEntity;
                projectilePool.RemoveAt(lastIndex);

                if (entityManager.Exists(projectileEntity) == false)
                    continue;

                ShootRequest request = shootRequests[requestIndex];
                float3 direction = math.normalizesafe(request.Direction, new float3(0f, 0f, 1f));

                LocalTransform projectileTransform = entityManager.GetComponentData<LocalTransform>(projectileEntity);
                projectileTransform.Position = request.Position;
                projectileTransform.Rotation = quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));
                entityManager.SetComponentData(projectileEntity, projectileTransform);

                Projectile projectileData = new Projectile
                {
                    Velocity = direction * math.max(0f, request.Speed),
                    Damage = math.max(0f, request.Damage),
                    MaxRange = request.Range,
                    MaxLifetime = request.Lifetime,
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
                entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, true);
            }

            shootRequests.Clear();
        }

        shooterEntities.Dispose();
    }
    #endregion
}
