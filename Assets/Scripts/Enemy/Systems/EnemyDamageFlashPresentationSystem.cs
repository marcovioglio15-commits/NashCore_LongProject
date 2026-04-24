using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Updates enemy damage flash presentation and offensive engagement feedback for both managed companion renderers and ECS-rendered visuals.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyVisualDistanceCullingSystem))]
public partial struct EnemyDamageFlashPresentationSystem : ISystem
{
    #region Constants
    private const float BlendEpsilon = 0.0001f;
    private const float ColorEpsilon = 0.0001f;
    private const float CameraResolveRetryIntervalSeconds = 0.5f;
    #endregion

    #region Fields
    private static Transform cachedMainCameraTransform;
    private static float nextCameraResolveTime;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DamageFlashConfig>();
        state.RequireForUpdate<DamageFlashState>();
        state.RequireForUpdate<EnemyVisualConfig>();
        state.RequireForUpdate<EnemyVisualFlashPresentationState>();
        state.RequireForUpdate<EnemyOffensiveEngagementConfigElement>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;
        Transform cameraTransform = ResolveMainCameraTransform((float)SystemAPI.Time.ElapsedTime);
        BufferLookup<EnemyOffensiveEngagementConfigElement> offensiveConfigLookup = SystemAPI.GetBufferLookup<EnemyOffensiveEngagementConfigElement>(true);
        BufferLookup<EnemyShooterRuntimeElement> shooterRuntimeLookup = SystemAPI.GetBufferLookup<EnemyShooterRuntimeElement>(true);
        ComponentLookup<EnemyPatternConfig> patternConfigLookup = SystemAPI.GetComponentLookup<EnemyPatternConfig>(true);
        ComponentLookup<EnemyPatternRuntimeState> patternRuntimeStateLookup = SystemAPI.GetComponentLookup<EnemyPatternRuntimeState>(true);

        foreach ((RefRO<DamageFlashConfig> damageFlashConfig,
                  RefRW<DamageFlashState> damageFlashState,
                  RefRW<EnemyVisualFlashPresentationState> visualFlashPresentationState,
                  RefRO<EnemyVisualConfig> visualConfig,
                  RefRO<EnemyVisualRuntimeState> visualRuntimeState,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<DamageFlashConfig>,
                                    RefRW<DamageFlashState>,
                                    RefRW<EnemyVisualFlashPresentationState>,
                                    RefRO<EnemyVisualConfig>,
                                    RefRO<EnemyVisualRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyActive>()
                             .WithEntityAccess())
        {
            DynamicBuffer<EnemyOffensiveEngagementConfigElement> offensiveEngagementConfigs = offensiveConfigLookup[enemyEntity];
            DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime = shooterRuntimeLookup[enemyEntity];
            EnemyPatternConfig currentPatternConfig = patternConfigLookup[enemyEntity];
            EnemyPatternRuntimeState currentPatternRuntimeState = patternRuntimeStateLookup[enemyEntity];
            DamageFlashState runtimeState = damageFlashState.ValueRO;
            float damageBlend = DamageFlashRuntimeUtility.Advance(ref runtimeState, in damageFlashConfig.ValueRO, deltaTime);
            EnemyVisualFlashPresentationState currentPresentationState = visualFlashPresentationState.ValueRO;
            EnemyOffensiveEngagementBlendResult offensiveBlendResult = EnemyOffensiveEngagementPresentationUtility.ResolveBlendResult(offensiveEngagementConfigs,
                                                                                                                                     shooterRuntime,
                                                                                                                                     in currentPatternConfig,
                                                                                                                                     in currentPatternRuntimeState);
            float offensiveBlend = EnemyOffensiveEngagementPresentationUtility.ResolveDisplayedBlend(currentPresentationState.OffensiveEngagementBlend,
                                                                                                    currentPresentationState.OffensiveEngagementFadeOutSeconds,
                                                                                                    offensiveBlendResult,
                                                                                                    deltaTime,
                                                                                                    out float rememberedFadeOutSeconds);
            float4 offensiveColor = ResolveOffensiveColor(currentPresentationState, offensiveBlendResult);
            bool enemyVisible = visualRuntimeState.ValueRO.IsVisible != 0;

            SyncOffensiveBillboard(entityManager,
                                   enemyEntity,
                                   enemyVisible,
                                   enemyTransform.ValueRO.Position,
                                   cameraTransform,
                                   offensiveEngagementConfigs,
                                   shooterRuntime,
                                   in currentPatternConfig,
                                   in currentPatternRuntimeState);

            float4 targetColor = damageFlashConfig.ValueRO.FlashColor;
            float targetBlend = damageBlend;

            if (offensiveBlend > targetBlend)
            {
                targetBlend = offensiveBlend;
                targetColor = offensiveColor;
            }

            if (HasUnchangedPresentationState(currentPresentationState,
                                              targetBlend,
                                              targetColor,
                                              offensiveBlend,
                                              offensiveColor,
                                              rememberedFadeOutSeconds))
            {
                damageFlashState.ValueRW = runtimeState;
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
            currentPresentationState.OffensiveEngagementColor = offensiveColor;
            currentPresentationState.OffensiveEngagementBlend = offensiveBlend;
            currentPresentationState.OffensiveEngagementFadeOutSeconds = rememberedFadeOutSeconds;
            visualFlashPresentationState.ValueRW = currentPresentationState;
        }

        EnemyOffensiveEngagementBillboardRuntimeUtility.ReleaseInactiveViews(entityManager);
    }

    public void OnDestroy(ref SystemState state)
    {
        cachedMainCameraTransform = null;
        nextCameraResolveTime = 0f;
        EnemyOffensiveEngagementBillboardRuntimeUtility.Shutdown();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves the current main camera transform with a small retry interval so presentation systems do not repeatedly scan cameras every frame.
    /// /params elapsedTime Current world elapsed time.
    /// /returns The resolved main camera transform, or null when no active camera is currently available.
    /// </summary>
    private static Transform ResolveMainCameraTransform(float elapsedTime)
    {
        if (cachedMainCameraTransform != null)
        {
            return cachedMainCameraTransform;
        }

        if (elapsedTime < nextCameraResolveTime)
        {
            return null;
        }

        nextCameraResolveTime = elapsedTime + CameraResolveRetryIntervalSeconds;
        Camera resolvedCamera = Camera.main;

        if (resolvedCamera == null)
        {
            Camera[] allCameras = Camera.allCameras;

            for (int cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
            {
                Camera candidateCamera = allCameras[cameraIndex];

                if (candidateCamera == null)
                {
                    continue;
                }

                if (!candidateCamera.isActiveAndEnabled)
                {
                    continue;
                }

                resolvedCamera = candidateCamera;
                break;
            }
        }

        if (resolvedCamera == null)
        {
            cachedMainCameraTransform = null;
            return null;
        }

        cachedMainCameraTransform = resolvedCamera.transform;
        return cachedMainCameraTransform;
    }

    /// <summary>
    /// Resolves which offensive color should currently be remembered for fade-out continuity.
    /// /params currentPresentationState Current stored presentation state.
    /// /params offensiveBlendResult Strongest active offensive blend result for the current frame.
    /// /returns Offensive color that should be used for the current frame and stored back into runtime state.
    /// </summary>
    private static float4 ResolveOffensiveColor(EnemyVisualFlashPresentationState currentPresentationState,
                                                EnemyOffensiveEngagementBlendResult offensiveBlendResult)
    {
        if (!offensiveBlendResult.IsActive)
        {
            return currentPresentationState.OffensiveEngagementColor;
        }

        if (offensiveBlendResult.Blend >= currentPresentationState.OffensiveEngagementBlend)
        {
            return offensiveBlendResult.Color;
        }

        return currentPresentationState.OffensiveEngagementColor;
    }

    /// <summary>
    /// Updates the managed offensive billboard view when one is available on the current enemy entity.
    /// /params entityManager Entity manager used to resolve the managed billboard companion component.
    /// /params enemyEntity Current enemy entity.
    /// /params enemyVisible Whether the enemy is currently visible after distance culling.
    /// /params enemyPosition Current enemy world position.
    /// /params cameraTransform Active camera transform used for billboarding.
    /// /params offensiveEngagementConfigs Baked offensive engagement configs for the current enemy.
    /// /params shooterRuntime Current shooter runtime buffer used by weapon timing evaluation.
    /// /params patternConfig Current compiled pattern config used by short-range timing evaluation.
    /// /params patternRuntimeState Current mutable pattern runtime state used by short-range timing evaluation.
    /// /returns None.
    /// </summary>
    private static void SyncOffensiveBillboard(EntityManager entityManager,
                                               Entity enemyEntity,
                                               bool enemyVisible,
                                               float3 enemyPosition,
                                               Transform cameraTransform,
                                               DynamicBuffer<EnemyOffensiveEngagementConfigElement> offensiveEngagementConfigs,
                                               DynamicBuffer<EnemyShooterRuntimeElement> shooterRuntime,
                                               in EnemyPatternConfig patternConfig,
                                               in EnemyPatternRuntimeState patternRuntimeState)
    {
        if (!EnemyOffensiveEngagementBillboardRuntimeUtility.TryResolveRuntimeView(entityManager,
                                                                                  enemyEntity,
                                                                                  out EnemyOffensiveEngagementBillboardView billboardView))
        {
            return;
        }

        if (!enemyVisible || cameraTransform == null)
        {
            billboardView.Hide();
            return;
        }

        EnemyOffensiveEngagementBillboardResult billboardResult = EnemyOffensiveEngagementPresentationUtility.ResolveBillboardResult(offensiveEngagementConfigs,
                                                                                                                                   shooterRuntime,
                                                                                                                                   in patternConfig,
                                                                                                                                   in patternRuntimeState);

        if (!billboardResult.IsActive)
        {
            billboardView.Hide();
            return;
        }

        Vector3 worldPosition = new Vector3(enemyPosition.x, enemyPosition.y, enemyPosition.z);
        float3 offsetFloat3 = billboardResult.Offset;
        Vector3 worldOffset = new Vector3(offsetFloat3.x, offsetFloat3.y, offsetFloat3.z);
        billboardView.Render(worldPosition,
                             cameraTransform,
                             billboardResult.Source,
                             billboardResult.UseOverrideVisualSettings,
                             DamageFlashRuntimeUtility.ToManagedColor(billboardResult.Color),
                             worldOffset,
                             billboardResult.UniformScale);
    }

    /// <summary>
    /// Returns whether the currently computed presentation values match the state already applied to renderers.
    /// /params currentPresentationState Current stored presentation state.
    /// /params targetBlend Final composed blend that would be applied this frame.
    /// /params targetColor Final composed color that would be applied this frame.
    /// /params offensiveBlend Current offensive-only displayed blend.
    /// /params offensiveColor Current offensive-only remembered color.
    /// /params rememberedFadeOutSeconds Current remembered offensive fade-out duration.
    /// /returns True when renderers already match the requested frame state.
    /// </summary>
    private static bool HasUnchangedPresentationState(EnemyVisualFlashPresentationState currentPresentationState,
                                                      float targetBlend,
                                                      float4 targetColor,
                                                      float offensiveBlend,
                                                      float4 offensiveColor,
                                                      float rememberedFadeOutSeconds)
    {
        if (math.abs(currentPresentationState.AppliedBlend - targetBlend) > BlendEpsilon)
        {
            return false;
        }

        if (!HasApproximatelyEqualColor(currentPresentationState.AppliedColor, targetColor))
        {
            return false;
        }

        if (math.abs(currentPresentationState.OffensiveEngagementBlend - offensiveBlend) > BlendEpsilon)
        {
            return false;
        }

        if (!HasApproximatelyEqualColor(currentPresentationState.OffensiveEngagementColor, offensiveColor))
        {
            return false;
        }

        return math.abs(currentPresentationState.OffensiveEngagementFadeOutSeconds - rememberedFadeOutSeconds) <= BlendEpsilon;
    }

    /// <summary>
    /// Applies the resolved flash values to a managed Animator-based visual companion.
    /// /params entityManager Entity manager used to resolve the Animator component object.
    /// /params enemyEntity Current enemy entity.
    /// /params flashColor Final flash color resolved for this frame.
    /// /params targetBlend Final flash blend resolved for this frame.
    /// /returns None.
    /// </summary>
    private static void ApplyCompanionFlash(EntityManager entityManager,
                                            Entity enemyEntity,
                                            Color flashColor,
                                            float targetBlend)
    {
        if (!entityManager.HasComponent<Animator>(enemyEntity))
        {
            return;
        }

        Animator animator = entityManager.GetComponentObject<Animator>(enemyEntity);

        if (animator == null)
        {
            return;
        }

        ManagedDamageFlashRendererUtility.ApplyToAnimator(animator, flashColor, targetBlend);
    }

    /// <summary>
    /// Checks whether two linear colors are approximately equal using the shared presentation epsilon.
    /// /params left Left-hand color.
    /// /params right Right-hand color.
    /// /returns True when the largest component delta stays below the configured epsilon.
    /// </summary>
    private static bool HasApproximatelyEqualColor(float4 left, float4 right)
    {
        float4 absoluteDelta = math.abs(left - right);
        return math.cmax(absoluteDelta) <= ColorEpsilon;
    }
    #endregion

    #endregion
}
