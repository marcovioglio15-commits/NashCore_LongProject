using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Resolves enemy-owned projectile hits against the player.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyContactDamageSystem))]
[UpdateBefore(typeof(EnemyDespawnSystem))]
public partial struct EnemyProjectileHitPlayerSystem : ISystem
{
    #region Constants
    private const float BaseProjectileHitRadius = 0.05f;
    private const float PlayerHitRadius = 0.55f;
    #endregion

    #region Fields
    private EntityQuery playerQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, LocalTransform, PlayerHealth, PlayerShield>()
            .Build();

        state.RequireForUpdate(playerQuery);
        state.RequireForUpdate<ProjectileActive>();
        state.RequireForUpdate<ProjectileOwner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        ComponentLookup<PlayerDashState> dashStateLookup = SystemAPI.GetComponentLookup<PlayerDashState>(true);
        ComponentLookup<PlayerControllerConfig> playerControllerLookup = SystemAPI.GetComponentLookup<PlayerControllerConfig>(true);
        BufferLookup<ProjectilePoolElement> projectilePoolLookup = SystemAPI.GetBufferLookup<ProjectilePoolElement>(false);
        BufferLookup<PlayerElementStackElement> playerElementStackLookup = SystemAPI.GetBufferLookup<PlayerElementStackElement>(false);
        Entity playerEntity = Entity.Null;
        LocalTransform playerTransform = default;
        PlayerHealth playerHealth = default;
        PlayerShield playerShield = default;

        foreach ((RefRO<LocalTransform> candidatePlayerTransform,
                  RefRO<PlayerHealth> candidatePlayerHealth,
                  RefRO<PlayerShield> candidatePlayerShield,
                  Entity candidatePlayerEntity)
                 in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerHealth>, RefRO<PlayerShield>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            playerEntity = candidatePlayerEntity;
            playerTransform = candidatePlayerTransform.ValueRO;
            playerHealth = candidatePlayerHealth.ValueRO;
            playerShield = candidatePlayerShield.ValueRO;
            break;
        }

        if (playerEntity == Entity.Null)
            return;

        if (!entityManager.Exists(playerEntity))
            return;

        if (playerHealth.Current <= 0f)
            return;

        if (dashStateLookup.HasComponent(playerEntity))
        {
            PlayerDashState dashState = dashStateLookup[playerEntity];

            if (dashState.RemainingInvulnerability > 0f)
                return;
        }

        float3 playerPosition = playerTransform.Position;
        float accumulatedDamage = 0f;

        foreach ((RefRO<Projectile> projectile,
                  RefRO<ProjectileOwner> projectileOwner,
                  RefRO<ProjectileElementalPayload> elementalPayload,
                  RefRO<LocalTransform> projectileTransform,
                  Entity projectileEntity)
                 in SystemAPI.Query<RefRO<Projectile>,
                                    RefRO<ProjectileOwner>,
                                    RefRO<ProjectileElementalPayload>,
                                    RefRO<LocalTransform>>()
                             .WithAll<ProjectileActive>()
                             .WithEntityAccess())
        {
            Entity shooterEntity = projectileOwner.ValueRO.ShooterEntity;

            if (playerControllerLookup.HasComponent(shooterEntity))
                continue;

            float3 delta = projectileTransform.ValueRO.Position - playerPosition;
            delta.y = 0f;
            float projectileScale = math.max(0.01f, projectileTransform.ValueRO.Scale);
            float hitRadius = BaseProjectileHitRadius * projectileScale + math.max(0f, projectile.ValueRO.ExplosionRadius) + PlayerHitRadius;

            if (math.lengthsq(delta) > hitRadius * hitRadius)
                continue;

            accumulatedDamage += math.max(0f, projectile.ValueRO.Damage);

            if (elementalPayload.ValueRO.Enabled != 0 && elementalPayload.ValueRO.StacksPerHit > 0f)
            {
                bool thresholdTriggered;
                PlayerElementalStackUtility.TryApplyStacks(playerEntity,
                                                           math.max(0f, elementalPayload.ValueRO.StacksPerHit),
                                                           in elementalPayload.ValueRO.Effect,
                                                           ref playerElementStackLookup,
                                                           out thresholdTriggered);
            }

            DespawnProjectile(entityManager,
                              projectileEntity,
                              shooterEntity,
                              ref projectilePoolLookup);
        }

        if (accumulatedDamage <= 0f)
            return;

        PlayerDamageUtility.ApplyFlatShieldDamage(ref playerHealth, ref playerShield, accumulatedDamage);

        entityManager.SetComponentData(playerEntity, playerHealth);
        entityManager.SetComponentData(playerEntity, playerShield);
    }
    #endregion

    #region Helpers
    private static void DespawnProjectile(EntityManager entityManager,
                                          Entity projectileEntity,
                                          Entity shooterEntity,
                                          ref BufferLookup<ProjectilePoolElement> projectilePoolLookup)
    {
        ProjectilePoolUtility.SetProjectileParked(entityManager, projectileEntity);
        entityManager.SetComponentEnabled<ProjectileActive>(projectileEntity, false);

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
}
