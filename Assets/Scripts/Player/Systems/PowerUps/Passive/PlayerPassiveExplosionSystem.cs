using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Emits passive explosion requests based on configured trigger mode.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
public partial struct PlayerPassiveExplosionSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<PlayerPassiveExplosionState>();
        state.RequireForUpdate<PlayerExplosionRequest>();
        state.RequireForUpdate<LocalTransform>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        bool hasKilledEvents = SystemAPI.TryGetSingletonBuffer<EnemyKilledEventElement>(out DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer);
        ComponentLookup<PlayerHealth> healthLookup = SystemAPI.GetComponentLookup<PlayerHealth>(true);

        foreach ((RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRW<PlayerPassiveExplosionState> passiveExplosionState,
                  RefRO<LocalTransform> playerTransform,
                  DynamicBuffer<PlayerExplosionRequest> explosionRequests,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerPassiveToolsState>,
                                    RefRW<PlayerPassiveExplosionState>,
                                    RefRO<LocalTransform>,
                                    DynamicBuffer<PlayerExplosionRequest>>()
                             .WithEntityAccess())
        {
            if (passiveToolsState.ValueRO.HasExplosion == 0)
                continue;

            ExplosionPassiveConfig explosionConfig = passiveToolsState.ValueRO.Explosion;

            if (explosionConfig.Radius <= 0f || explosionConfig.Damage <= 0f)
                continue;

            float cooldownRemaining = math.max(0f, passiveExplosionState.ValueRO.CooldownRemaining - deltaTime);

            switch (explosionConfig.TriggerMode)
            {
                case PassiveExplosionTriggerMode.Cooldown:
                    if (cooldownRemaining > 0f)
                        break;

                    EnqueueExplosion(explosionRequests, in explosionConfig, playerTransform.ValueRO.Position);
                    cooldownRemaining = math.max(0f, explosionConfig.CooldownSeconds);
                    break;
                case PassiveExplosionTriggerMode.OnPlayerDamaged:
                    if (healthLookup.HasComponent(playerEntity) == false)
                        break;

                    float currentHealth = healthLookup[playerEntity].Current;
                    float previousHealth = passiveExplosionState.ValueRO.PreviousObservedHealth;

                    if (previousHealth < 0f)
                    {
                        previousHealth = currentHealth;
                    }
                    else if (currentHealth < previousHealth - 1e-4f && cooldownRemaining <= 0f)
                    {
                        EnqueueExplosion(explosionRequests, in explosionConfig, playerTransform.ValueRO.Position);
                        cooldownRemaining = math.max(0f, explosionConfig.CooldownSeconds);
                    }

                    passiveExplosionState.ValueRW.PreviousObservedHealth = currentHealth;
                    break;
                case PassiveExplosionTriggerMode.OnEnemyKilled:
                    if (hasKilledEvents == false)
                        break;

                    if (cooldownRemaining > 0f)
                        break;

                    if (killedEventsBuffer.Length == 0)
                        break;

                    EnqueueExplosion(explosionRequests, in explosionConfig, killedEventsBuffer[0].Position);
                    cooldownRemaining = math.max(0f, explosionConfig.CooldownSeconds);
                    break;
            }

            passiveExplosionState.ValueRW.CooldownRemaining = cooldownRemaining;
        }
    }
    #endregion

    #region Helpers
    private static void EnqueueExplosion(DynamicBuffer<PlayerExplosionRequest> explosionRequests,
                                         in ExplosionPassiveConfig explosionConfig,
                                         float3 triggerOrigin)
    {
        float3 worldPosition = triggerOrigin + explosionConfig.TriggerOffset;

        explosionRequests.Add(new PlayerExplosionRequest
        {
            Position = worldPosition,
            Radius = math.max(0f, explosionConfig.Radius),
            Damage = math.max(0f, explosionConfig.Damage),
            AffectAllEnemiesInRadius = explosionConfig.AffectAllEnemiesInRadius,
            ExplosionVfxPrefabEntity = explosionConfig.ExplosionVfxPrefabEntity,
            ScaleVfxToRadius = explosionConfig.ScaleVfxToRadius,
            VfxScaleMultiplier = math.max(0.01f, explosionConfig.VfxScaleMultiplier)
        });
    }
    #endregion

    #endregion
}
