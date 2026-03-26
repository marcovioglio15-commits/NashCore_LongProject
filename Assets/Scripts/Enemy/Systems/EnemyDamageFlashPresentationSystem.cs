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
    private const float ColorEpsilon = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DamageFlashConfig>();
        state.RequireForUpdate<DamageFlashState>();
        state.RequireForUpdate<EnemyVisualConfig>();
        state.RequireForUpdate<EnemyShooterAimPulseVisualConfig>();
        state.RequireForUpdate<EnemyVisualFlashPresentationState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<DamageFlashConfig> damageFlashConfig,
                  RefRW<DamageFlashState> damageFlashState,
                  RefRO<EnemyShooterAimPulseVisualConfig> shooterAimPulseVisualConfig,
                  RefRW<EnemyVisualFlashPresentationState> visualFlashPresentationState,
                  DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                  RefRO<EnemyVisualConfig> visualConfig,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<DamageFlashConfig>,
                                    RefRW<DamageFlashState>,
                                    RefRO<EnemyShooterAimPulseVisualConfig>,
                                    RefRW<EnemyVisualFlashPresentationState>,
                                    DynamicBuffer<EnemyShooterRuntimeElement>,
                                    RefRO<EnemyVisualConfig>>()
                             .WithAll<EnemyActive>()
                             .WithEntityAccess())
        {
            DamageFlashState runtimeState = damageFlashState.ValueRO;
            float damageBlend = DamageFlashRuntimeUtility.Advance(ref runtimeState, in damageFlashConfig.ValueRO, deltaTime);
            EnemyVisualFlashPresentationState currentPresentationState = visualFlashPresentationState.ValueRO;
            float shooterTargetBlend = ResolveShooterAimPulseTargetBlend(shooterRuntime,
                                                                         shooterAimPulseVisualConfig.ValueRO);
            float shooterBlend = ResolveShooterAimPulseBlend(currentPresentationState.ShooterPulseBlend,
                                                             shooterTargetBlend,
                                                             shooterAimPulseVisualConfig.ValueRO,
                                                             deltaTime);
            float4 targetColor = damageFlashConfig.ValueRO.FlashColor;
            float targetBlend = damageBlend;

            if (shooterBlend > targetBlend)
            {
                targetBlend = shooterBlend;
                targetColor = shooterAimPulseVisualConfig.ValueRO.Color;
            }

            if (math.abs(currentPresentationState.AppliedBlend - targetBlend) <= BlendEpsilon &&
                HasApproximatelyEqualColor(currentPresentationState.AppliedColor, targetColor) &&
                math.abs(currentPresentationState.ShooterPulseBlend - shooterBlend) <= BlendEpsilon)
            {
                damageFlashState.ValueRW = runtimeState;
                currentPresentationState.ShooterPulseBlend = shooterBlend;
                visualFlashPresentationState.ValueRW = currentPresentationState;
                continue;
            }

            switch (visualConfig.ValueRO.Mode)
            {
                case EnemyVisualMode.CompanionAnimator:
                    ApplyCompanionFlash(entityManager,
                                        enemyEntity,
                                        DamageFlashRuntimeUtility.ToManagedColor(targetColor),
                                        targetBlend);
                    break;

                default:
                    EnemyDamageFlashRenderUtility.ApplyGpuFlash(entityManager,
                                                                enemyEntity,
                                                                targetColor,
                                                                targetBlend);
                    break;
            }

            runtimeState.AppliedBlend = damageBlend;
            damageFlashState.ValueRW = runtimeState;
            currentPresentationState.AppliedBlend = targetBlend;
            currentPresentationState.AppliedColor = targetColor;
            currentPresentationState.ShooterPulseBlend = shooterBlend;
            visualFlashPresentationState.ValueRW = currentPresentationState;
        }
    }
    #endregion

    #region Helpers
    private static float ResolveShooterAimPulseTargetBlend(DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                                           EnemyShooterAimPulseVisualConfig shooterAimPulseVisualConfig)
    {
        if (shooterAimPulseVisualConfig.Enabled == 0)
            return 0f;

        float maximumBlend = math.saturate(shooterAimPulseVisualConfig.MaximumBlend);
        float leadTimeSeconds = math.max(0f, shooterAimPulseVisualConfig.LeadTimeSeconds);

        if (maximumBlend <= 0f)
            return 0f;

        int shooterCount = shooterRuntime.Length;
        float bestBlend = 0f;

        for (int shooterIndex = 0; shooterIndex < shooterCount; shooterIndex++)
        {
            EnemyShooterRuntimeElement runtime = shooterRuntime[shooterIndex];

            if (runtime.IsPlayerInRange == 0)
                continue;

            float candidateBlend = 0f;

            if (runtime.RemainingBurstShots > 0 && runtime.ShotsFiredInCurrentBurst <= 0)
            {
                float windupDurationSeconds = math.max(0f, runtime.BurstWindupDurationSeconds);

                if (windupDurationSeconds > 0f)
                {
                    float normalizedProgress = 1f - math.saturate(runtime.NextShotInBurstTimer / windupDurationSeconds);
                    candidateBlend = math.saturate(normalizedProgress) * maximumBlend;
                }
            }
            else if (runtime.RemainingBurstShots <= 0 && leadTimeSeconds > 0f && runtime.NextBurstTimer > 0f && runtime.NextBurstTimer <= leadTimeSeconds)
            {
                float normalizedLead = 1f - math.saturate(runtime.NextBurstTimer / leadTimeSeconds);
                candidateBlend = math.saturate(normalizedLead) * maximumBlend;
            }

            if (candidateBlend > bestBlend)
                bestBlend = candidateBlend;
        }

        return bestBlend;
    }

    private static float ResolveShooterAimPulseBlend(float currentBlend,
                                                     float targetBlend,
                                                     EnemyShooterAimPulseVisualConfig shooterAimPulseVisualConfig,
                                                     float deltaTime)
    {
        if (targetBlend >= currentBlend)
            return targetBlend;

        float fadeOutSeconds = math.max(0f, shooterAimPulseVisualConfig.FadeOutSeconds);

        if (fadeOutSeconds <= 0f)
            return targetBlend;

        float fadeStep = math.max(0f, deltaTime) / fadeOutSeconds;
        return math.lerp(currentBlend, targetBlend, math.saturate(fadeStep));
    }

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

    private static bool HasApproximatelyEqualColor(float4 left, float4 right)
    {
        float4 absoluteDelta = math.abs(left - right);
        return math.cmax(absoluteDelta) <= ColorEpsilon;
    }

    #endregion

    #endregion
}
