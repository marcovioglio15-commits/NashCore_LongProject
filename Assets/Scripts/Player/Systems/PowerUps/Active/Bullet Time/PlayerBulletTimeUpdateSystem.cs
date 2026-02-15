using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Updates Bullet Time duration and writes global enemy time scale.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerBulletTimeUpdateSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerBulletTimeState>();

        EntityQuery timeScaleQuery = state.GetEntityQuery(ComponentType.ReadOnly<EnemyGlobalTimeScale>());

        if (timeScaleQuery.IsEmptyIgnoreFilter)
        {
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new EnemyGlobalTimeScale
            {
                Scale = 1f
            });
        }
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float maxSlowPercent = 0f;

        foreach (RefRW<PlayerBulletTimeState> bulletTimeState in SystemAPI.Query<RefRW<PlayerBulletTimeState>>())
        {
            float remainingDuration = bulletTimeState.ValueRO.RemainingDuration;

            if (remainingDuration <= 0f)
            {
                bulletTimeState.ValueRW.RemainingDuration = 0f;
                bulletTimeState.ValueRW.SlowPercent = 0f;
                continue;
            }

            remainingDuration -= deltaTime;

            if (remainingDuration < 0f)
                remainingDuration = 0f;

            bulletTimeState.ValueRW.RemainingDuration = remainingDuration;

            if (remainingDuration <= 0f)
            {
                bulletTimeState.ValueRW.SlowPercent = 0f;
                continue;
            }

            float slowPercent = math.clamp(bulletTimeState.ValueRO.SlowPercent, 0f, 100f);

            if (slowPercent > maxSlowPercent)
                maxSlowPercent = slowPercent;
        }

        float enemyTimeScale = math.saturate(1f - (maxSlowPercent * 0.01f));

        if (SystemAPI.TryGetSingletonRW<EnemyGlobalTimeScale>(out RefRW<EnemyGlobalTimeScale> enemyGlobalTimeScale))
        {
            enemyGlobalTimeScale.ValueRW.Scale = enemyTimeScale;
            return;
        }

        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new EnemyGlobalTimeScale
        {
            Scale = enemyTimeScale
        });
    }
    #endregion

    #endregion
}
