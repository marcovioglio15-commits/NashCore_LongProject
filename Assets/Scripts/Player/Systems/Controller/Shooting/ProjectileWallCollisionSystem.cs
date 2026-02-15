using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Despawns active projectiles when they hit wall colliders on the configured wall layer.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(ProjectileSimulationSystem))]
[UpdateBefore(typeof(ProjectileDespawnSystem))]
public partial struct ProjectileWallCollisionSystem : ISystem
{
    #region Constants
    private const float BaseProjectileCollisionRadius = 0.05f;
    private const float MovementEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Projectile>();
        state.RequireForUpdate<ProjectileOwner>();
        state.RequireForUpdate<ProjectileActive>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) && worldLayersConfig.WallsLayerMask != 0)
            wallsLayerMask = worldLayersConfig.WallsLayerMask;

        if (wallsLayerMask == 0)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        EntityManager entityManager = state.EntityManager;
        BufferLookup<ProjectilePoolElement> poolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        ComponentLookup<PlayerMovementState> movementStateLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);

        foreach ((RefRO<Projectile> projectile,
                  RefRO<ProjectileOwner> owner,
                  RefRO<LocalTransform> projectileTransform,
                  Entity projectileEntity) in SystemAPI.Query<RefRO<Projectile>, RefRO<ProjectileOwner>, RefRO<LocalTransform>>()
                                                       .WithAll<ProjectileActive>()
                                                       .WithEntityAccess())
        {
            float3 velocity = projectile.ValueRO.Velocity;

            if (projectile.ValueRO.InheritPlayerSpeed != 0)
                velocity += ResolveInheritedVelocity(in projectile.ValueRO, in owner.ValueRO, in movementStateLookup);

            float3 displacement = velocity * deltaTime;

            if (math.lengthsq(displacement) <= MovementEpsilon)
                continue;

            float3 endPosition = projectileTransform.ValueRO.Position;
            float3 startPosition = endPosition - displacement;
            float projectileScale = math.max(0.01f, projectileTransform.ValueRO.Scale);
            float collisionRadius = math.max(0.005f, BaseProjectileCollisionRadius * projectileScale);
            bool hitWall = WorldWallCollisionUtility.TryResolveBlockedDisplacement(physicsWorldSingleton,
                                                                                   startPosition,
                                                                                   displacement,
                                                                                   collisionRadius,
                                                                                   wallsLayerMask,
                                                                                   out float3 _,
                                                                                   out float3 _);

            if (hitWall == false)
                continue;

            ProjectilePoolUtility.SetProjectileParked(entityManager, projectileEntity);
            entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);

            Entity shooterEntity = owner.ValueRO.ShooterEntity;

            if (poolLookup.HasBuffer(shooterEntity) == false)
                continue;

            DynamicBuffer<ProjectilePoolElement> shooterPool = poolLookup[shooterEntity];
            shooterPool.Add(new ProjectilePoolElement
            {
                ProjectileEntity = projectileEntity
            });
        }
    }
    #endregion

    #region Helpers
    private static float3 ResolveInheritedVelocity(in Projectile projectile,
                                                   in ProjectileOwner owner,
                                                   in ComponentLookup<PlayerMovementState> movementStateLookup)
    {
        if (movementStateLookup.HasComponent(owner.ShooterEntity) == false)
            return float3.zero;

        float3 inheritedVelocity = movementStateLookup[owner.ShooterEntity].Velocity;
        inheritedVelocity.y = 0f;
        float speedSquared = math.lengthsq(projectile.Velocity);

        if (speedSquared <= 1e-6f)
            return inheritedVelocity;

        float projectionScale = math.dot(inheritedVelocity, projectile.Velocity) / speedSquared;
        inheritedVelocity -= projectile.Velocity * projectionScale;
        return inheritedVelocity;
    }
    #endregion

    #endregion
}
