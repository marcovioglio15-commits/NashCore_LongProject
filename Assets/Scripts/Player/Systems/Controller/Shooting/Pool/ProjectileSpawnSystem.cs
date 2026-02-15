using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

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
        // Get the EntityManager and the array of shooter entities that have shoot requests
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

            // Get the shoot requests buffer for the shooter entity
            DynamicBuffer<ShootRequest> shootRequests = entityManager.GetBuffer<ShootRequest>(shooterEntity);

            if (shootRequests.Length == 0)
                continue;

            ProjectilePoolState poolState = entityManager.GetComponentData<ProjectilePoolState>(shooterEntity);

            if (poolState.Initialized == 0)
                continue;


            // Get the projectile prefab entity from the ShooterProjectilePrefab component of the shooter entity
            ShooterProjectilePrefab projectilePrefab = entityManager.GetComponentData<ShooterProjectilePrefab>(shooterEntity);
            Entity prefabEntity = projectilePrefab.PrefabEntity;

            // If the projectile prefab entity is not valid, clear the shoot requests and skip processing for this shooter entity
            if (prefabEntity == Entity.Null || entityManager.Exists(prefabEntity) == false)
            {
                shootRequests.Clear();
                continue;
            }

            DynamicBuffer<ProjectilePoolElement> projectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);
            int missingProjectiles = shootRequests.Length - projectilePool.Length;

            // If there are more shoot requests than available projectiles in the pool, expand the pool
            if (missingProjectiles > 0)
            {
                int expandBatch = math.max(1, poolState.ExpandBatch);
                int expandCount = math.max(expandBatch, missingProjectiles);
                ProjectilePoolUtility.ExpandPool(entityManager, shooterEntity, prefabEntity, expandCount);
                // Refresh the projectile pool buffer reference after expansion
                shootRequests = entityManager.GetBuffer<ShootRequest>(shooterEntity);
                projectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);
            }

            int requestsCount = shootRequests.Length;

            // Spawn and initialize projectiles based on the shoot requests,
            // using available projectiles from the pool
            for (int requestIndex = 0; requestIndex < requestsCount; requestIndex++)
            {
                if (projectilePool.Length == 0)
                    break;

                // Get a projectile entity from the pool and remove it from the pool buffer
                int lastIndex = projectilePool.Length - 1;
                Entity projectileEntity = projectilePool[lastIndex].ProjectileEntity;
                projectilePool.RemoveAt(lastIndex);

                // If the projectile entity from the pool is not valid,
                // skip processing for this shoot request
                if (entityManager.Exists(projectileEntity) == false)
                    continue;

                // Get the shoot request data and calculate the projectile's initial direction
                ShootRequest request = shootRequests[requestIndex];
                float3 direction = math.normalizesafe(request.Direction, new float3(0f, 0f, 1f));


                // Set the projectile's initial position and rotation based on the shoot request data
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
                    Velocity = direction * math.max(0f, request.Speed),
                    Damage = math.max(0f, request.Damage),
                    MaxRange = request.Range,
                    MaxLifetime = request.Lifetime,
                    InheritPlayerSpeed = request.InheritPlayerSpeed
                };

                // Initialize the projectile's components with the data from the shoot request
                // and set it as active
                entityManager.SetComponentData(projectileEntity, projectileData);
                entityManager.SetComponentData(projectileEntity, new ProjectileRuntimeState
                {
                    TraveledDistance = 0f,
                    ElapsedLifetime = 0f
                });
                // Set the shooter entity as the owner of the projectile
                entityManager.SetComponentData(projectileEntity, new ProjectileOwner
                {
                    ShooterEntity = shooterEntity
                });
                // Enable the ProjectileActive component to mark the projectile as active in the simulation
                entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, true);
            }
            // Clear the shoot requests buffer after processing all requests for the shooter entity
            shootRequests.Clear();
        }
        // Dispose of the temporary array of shooter entities to free up memory
        shooterEntities.Dispose();
    }
    
    #endregion

}
