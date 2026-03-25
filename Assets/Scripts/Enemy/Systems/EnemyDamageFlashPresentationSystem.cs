using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Updates enemy hit-flash feedback for both managed companion renderers and ECS-rendered visuals.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyVisualDistanceCullingSystem))]
public partial struct EnemyDamageFlashPresentationSystem : ISystem
{
    #region Constants
    private const float BlendEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DamageFlashConfig>();
        state.RequireForUpdate<DamageFlashState>();
        state.RequireForUpdate<EnemyVisualConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<DamageFlashConfig> damageFlashConfig,
                  RefRW<DamageFlashState> damageFlashState,
                  RefRO<EnemyVisualConfig> visualConfig,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<DamageFlashConfig>,
                                    RefRW<DamageFlashState>,
                                    RefRO<EnemyVisualConfig>>()
                             .WithAll<EnemyActive>()
                             .WithEntityAccess())
        {
            DamageFlashState runtimeState = damageFlashState.ValueRO;
            float targetBlend = DamageFlashRuntimeUtility.Advance(ref runtimeState, in damageFlashConfig.ValueRO, deltaTime);

            if (math.abs(runtimeState.AppliedBlend - targetBlend) <= BlendEpsilon)
            {
                damageFlashState.ValueRW = runtimeState;
                continue;
            }

            switch (visualConfig.ValueRO.Mode)
            {
                case EnemyVisualMode.CompanionAnimator:
                    ApplyCompanionFlash(entityManager,
                                        enemyEntity,
                                        DamageFlashRuntimeUtility.ToManagedColor(damageFlashConfig.ValueRO.FlashColor),
                                        targetBlend);
                    break;

                default:
                    EnemyDamageFlashRenderUtility.ApplyGpuFlash(entityManager,
                                                                enemyEntity,
                                                                in damageFlashConfig.ValueRO,
                                                                targetBlend);
                    break;
            }

            runtimeState.AppliedBlend = targetBlend;
            damageFlashState.ValueRW = runtimeState;
        }
    }
    #endregion

    #region Helpers
    private static void ApplyCompanionFlash(EntityManager entityManager,
                                            Entity enemyEntity,
                                            Color flashColor,
                                            float targetBlend)
    {
        if (!entityManager.HasComponent<Animator>(enemyEntity))
            return;

        Animator animator = entityManager.GetComponentObject<Animator>(enemyEntity);

        if (animator == null)
            return;

        ManagedDamageFlashRendererUtility.ApplyToAnimator(animator, flashColor, targetBlend);
    }

    #endregion

    #endregion
}
