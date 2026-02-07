using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Utilities
public static class ProjectilePoolUtility
{
    #region Constants
    private static readonly float3 ParkingPosition = new float3(0f, -10000f, 0f);
    #endregion

    #region Public Methods
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
    }

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
