using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Utilities
/// <summary>
/// Provides utility methods for managing projectile entity pools, including pool expansion, 
/// component setup, and parking logic.
/// </summary>
public static class ProjectilePoolUtility
{
    #region Constants
    private static readonly float3 ParkingPosition = new float3(0f, -10000f, 0f);
    private const float MinimumBaseScale = 0.0001f;
    #endregion

    #region Public Methods
    /// <summary>
    /// Expands the projectile pool for a shooter entity by instantiating additional projectile entities and
    /// adding them to the pool buffer. Also ensures that each new projectile entity has the necessary components 
    /// and is parked at a designated position.
    /// </summary>
    /// <param name="entityManager">The EntityManager used to manage entities and components.</param>
    /// <param name="shooterEntity">The entity representing the shooter whose projectile pool is to be expanded.</param>
    /// <param name="projectilePrefab">The prefab entity used to instantiate new projectile entities.</param>
    /// <param name="count">The number of projectile entities to add to the pool.</param>
    public static void ExpandPool(EntityManager entityManager, Entity shooterEntity, Entity projectilePrefab, int count)
    {
        if (count <= 0)
            return;

        if (entityManager.HasBuffer<ProjectilePoolElement>(shooterEntity) == false)
            return;

        NativeArray<Entity> spawnedProjectiles = new NativeArray<Entity>(count, Allocator.Temp);
        entityManager.Instantiate(projectilePrefab, spawnedProjectiles);

        for (int index = 0; index < spawnedProjectiles.Length; index++)
        {
            Entity projectileEntity = spawnedProjectiles[index];
            EnsureProjectileComponents(entityManager, projectileEntity);
            EnsureProjectileBaseScale(entityManager, projectileEntity);
            SetProjectileParked(entityManager, projectileEntity);
            entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);
        }

        DynamicBuffer<ProjectilePoolElement> projectilePool = entityManager.GetBuffer<ProjectilePoolElement>(shooterEntity);

        for (int index = 0; index < spawnedProjectiles.Length; index++)
            projectilePool.Add(new ProjectilePoolElement
            {
                ProjectileEntity = spawnedProjectiles[index]
            });

        spawnedProjectiles.Dispose();
    }


    /// <summary>
    /// Ensures that the specified projectile entity has all required projectile-related components, adding any that are
    /// missing.
    /// </summary>
    /// <param name="entityManager">The EntityManager used to query and add components.</param>
    /// <param name="projectileEntity">The entity representing the projectile to check and update.</param>
    public static void EnsureProjectileComponents(EntityManager entityManager, Entity projectileEntity)
    {
        if (entityManager.HasComponent<LocalTransform>(projectileEntity) == false)
            entityManager.AddComponentData(projectileEntity, LocalTransform.Identity);

        if (entityManager.HasComponent<Projectile>(projectileEntity) == false)
            entityManager.AddComponentData(projectileEntity, default(Projectile));

        if (entityManager.HasComponent<ProjectileRuntimeState>(projectileEntity) == false)
            entityManager.AddComponentData(projectileEntity, default(ProjectileRuntimeState));

        if (entityManager.HasComponent<ProjectileOwner>(projectileEntity) == false)
            entityManager.AddComponentData(projectileEntity, default(ProjectileOwner));

        if (entityManager.HasComponent<ProjectileActive>(projectileEntity) == false)
            entityManager.AddComponent<ProjectileActive>(projectileEntity);

        if (entityManager.HasComponent<ProjectileBaseScale>(projectileEntity) == false)
            entityManager.AddComponentData(projectileEntity, new ProjectileBaseScale
            {
                Value = 1f
            });

        if (entityManager.HasComponent<ProjectilePerfectCircleState>(projectileEntity) == false)
            entityManager.AddComponentData(projectileEntity, default(ProjectilePerfectCircleState));

        if (entityManager.HasComponent<ProjectileBounceState>(projectileEntity) == false)
            entityManager.AddComponentData(projectileEntity, default(ProjectileBounceState));

        if (entityManager.HasComponent<ProjectileSplitState>(projectileEntity) == false)
            entityManager.AddComponentData(projectileEntity, default(ProjectileSplitState));

        if (entityManager.HasComponent<ProjectileElementalPayload>(projectileEntity) == false)
            entityManager.AddComponentData(projectileEntity, default(ProjectileElementalPayload));
    }

    /// <summary>
    /// Synchronizes the cached base scale component with the current projectile transform scale.
    /// </summary>
    /// <param name="entityManager">The EntityManager used to access and modify entity components.</param>
    /// <param name="projectileEntity">The entity representing the projectile to update.</param>
    public static void EnsureProjectileBaseScale(EntityManager entityManager, Entity projectileEntity)
    {
        if (entityManager.HasComponent<LocalTransform>(projectileEntity) == false)
            return;

        if (entityManager.HasComponent<ProjectileBaseScale>(projectileEntity) == false)
            return;

        LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(projectileEntity);
        float baseScale = math.max(MinimumBaseScale, localTransform.Scale);
        entityManager.SetComponentData(projectileEntity, new ProjectileBaseScale
        {
            Value = baseScale
        });
    }

    /// <summary>
    /// Sets the position of the specified projectile entity to the designated parking position if it has a
    /// LocalTransform component.
    /// </summary>
    /// <param name="entityManager">The EntityManager used to access and modify entity components.</param>
    /// <param name="projectileEntity">The projectile entity whose position will be set.</param>
    public static void SetProjectileParked(EntityManager entityManager, Entity projectileEntity)
    {
        if (entityManager.HasComponent<LocalTransform>(projectileEntity) == false)
            return;

        LocalTransform parkedTransform = entityManager.GetComponentData<LocalTransform>(projectileEntity);
        parkedTransform.Position = ParkingPosition;
        entityManager.SetComponentData(projectileEntity, parkedTransform);
    }
    #endregion
}
#endregion
