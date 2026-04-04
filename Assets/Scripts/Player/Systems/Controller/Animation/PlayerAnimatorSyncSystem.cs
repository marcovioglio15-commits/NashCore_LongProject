using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Pushes ECS player state into a managed Animator while keeping gameplay authority in ECS.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(PlayerManagedVisualAnimatorBridgeSystem))]
public partial struct PlayerAnimatorSyncSystem : ISystem
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private static readonly float3 DefaultForward = new float3(0f, 0f, 1f);
    private static readonly float3 WorldUp = new float3(0f, 1f, 0f);
    private byte loggedNoAnimatorEntityWarning;
    private byte loggedMissingAnimatorComponentWarning;
    private byte loggedNullAnimatorWarning;
    private byte loggedMissingStateWarning;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerAnimatorParameterConfig>();
        state.RequireForUpdate<PlayerAnimatorRuntimeState>();
        state.RequireForUpdate<OutlineVisualConfig>();
        loggedNoAnimatorEntityWarning = 0;
        loggedMissingAnimatorComponentWarning = 0;
        loggedNullAnimatorWarning = 0;
        loggedMissingStateWarning = 0;
    }

    public void OnDestroy(ref SystemState state)
    {
        ManagedOutlineRendererUtility.ClearCache();
        PlayerOutlineRuntimeMaterialSyncUtility.ClearCache();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        ComponentLookup<PlayerDashState> dashLookup = SystemAPI.GetComponentLookup<PlayerDashState>(true);
        ComponentLookup<PlayerMovementState> movementLookup = SystemAPI.GetComponentLookup<PlayerMovementState>(true);
        ComponentLookup<PlayerLookState> lookLookup = SystemAPI.GetComponentLookup<PlayerLookState>(true);
        ComponentLookup<PlayerShootingState> shootingLookup = SystemAPI.GetComponentLookup<PlayerShootingState>(true);
        ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<PlayerAnimatorControllerReference> animatorControllerLookup = SystemAPI.GetComponentLookup<PlayerAnimatorControllerReference>(true);
        ComponentLookup<PlayerAnimatorAvatarReference> animatorAvatarLookup = SystemAPI.GetComponentLookup<PlayerAnimatorAvatarReference>(true);
        EntityManager entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;
        int processedAnimatorEntities = 0;

        foreach ((RefRW<PlayerAnimatorParameterConfig> parameterConfig,
                  RefRO<OutlineVisualConfig> outlineConfig,
                  RefRW<PlayerAnimatorRuntimeState> animatorRuntimeState,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerAnimatorParameterConfig>,
                                    RefRO<OutlineVisualConfig>,
                                    RefRW<PlayerAnimatorRuntimeState>>()
                             .WithEntityAccess())
        {
            processedAnimatorEntities++;

            if (entityManager.HasComponent<Animator>(entity) == false)
            {
                if (loggedMissingAnimatorComponentWarning == 0)
                {
                    Debug.LogWarning("[PlayerAnimatorSyncSystem] Animator component missing on player entity. Verify RuntimeVisualBridgePrefab configuration for runtime visual spawn.");
                    loggedMissingAnimatorComponentWarning = 1;
                }

                continue;
            }

            Animator animator = entityManager.GetComponentObject<Animator>(entity);

            if (animator == null)
            {
                if (loggedNullAnimatorWarning == 0)
                {
                    Debug.LogWarning("[PlayerAnimatorSyncSystem] Animator component is null at runtime. Verify companion bake or runtime visual bridge prefab setup.");
                    loggedNullAnimatorWarning = 1;
                }

                continue;
            }

            bool hasMovementState = movementLookup.HasComponent(entity);
            bool hasLookState = lookLookup.HasComponent(entity);
            bool hasShootingState = shootingLookup.HasComponent(entity);
            PlayerMovementState movementState = hasMovementState ? movementLookup[entity] : default;
            PlayerLookState lookState = hasLookState ? lookLookup[entity] : default;
            PlayerShootingState shootingState = hasShootingState ? shootingLookup[entity] : default;

            if (loggedMissingStateWarning == 0 && (hasMovementState == false || hasLookState == false || hasShootingState == false))
            {
                Debug.LogWarning("[PlayerAnimatorSyncSystem] Missing movement/look/shooting state on player entity. Fallback values are being used.");
                loggedMissingStateWarning = 1;
            }

            EnsureAnimatorBindings(animator,
                                   entity,
                                   in animatorControllerLookup,
                                   in animatorAvatarLookup);

            EnsureAnimatorRuntimeAnimatorInstance(ref animatorRuntimeState.ValueRW,
                                                 ref parameterConfig.ValueRW,
                                                 animator);

            ValidateAnimatorParameters(animator,
                                       ref parameterConfig.ValueRW,
                                       ref animatorRuntimeState.ValueRW);

            EnsureAnimatorRuntimeSettings(animator, ref animatorRuntimeState.ValueRW);
            PlayerOutlineRuntimeMaterialSyncUtility.ApplyFromOutlineConfig(in outlineConfig.ValueRO);
            EnsureAnimatorOutline(animator, in outlineConfig.ValueRO);

            if (animator.enabled == false)
                animator.enabled = true;

            if (parameterConfig.ValueRO.DisableRootMotion != 0 && animator.applyRootMotion)
                animator.applyRootMotion = false;

            PlayerAnimatorParameterConfig resolvedParameterConfig = parameterConfig.ValueRO;

            float3 forward = DefaultForward;

            if (localTransformLookup.HasComponent(entity))
                forward = PlayerControllerMath.NormalizePlanar(math.forward(localTransformLookup[entity].Rotation), DefaultForward);

            float3 right = math.normalize(math.cross(WorldUp, forward));
            float3 desiredLookDirection = ResolveLookDirection(lookState, forward);
            float2 localMove = ResolveLocalMoveDirection(movementState,
                                                         in animatorRuntimeState.ValueRO,
                                                         right,
                                                         forward);
            float2 localAim = ToLocalPlanar(desiredLookDirection, right, forward);
            float moveSpeed = math.length(movementState.Velocity);
            bool isMoving = moveSpeed > math.max(0f, parameterConfig.ValueRO.MovingSpeedThreshold);
            bool isShooting = shootingState.VisualShootingActive != 0;
            bool isDashing = dashLookup.HasComponent(entity) && dashLookup[entity].IsDashing != 0;

            WriteFloatParameter(animator,
                                in resolvedParameterConfig,
                                resolvedParameterConfig.HasMoveX,
                                resolvedParameterConfig.MoveXHash,
                                localMove.x,
                                deltaTime);
            WriteFloatParameter(animator,
                                in resolvedParameterConfig,
                                resolvedParameterConfig.HasMoveY,
                                resolvedParameterConfig.MoveYHash,
                                localMove.y,
                                deltaTime);
            WriteFloatParameter(animator,
                                in resolvedParameterConfig,
                                resolvedParameterConfig.HasMoveSpeed,
                                resolvedParameterConfig.MoveSpeedHash,
                                moveSpeed,
                                deltaTime);
            WriteFloatParameter(animator,
                                in resolvedParameterConfig,
                                resolvedParameterConfig.HasAimX,
                                resolvedParameterConfig.AimXHash,
                                localAim.x,
                                deltaTime);
            WriteFloatParameter(animator,
                                in resolvedParameterConfig,
                                resolvedParameterConfig.HasAimY,
                                resolvedParameterConfig.AimYHash,
                                localAim.y,
                                deltaTime);

            if (resolvedParameterConfig.HasIsMoving != 0)
                animator.SetBool(resolvedParameterConfig.IsMovingHash, isMoving);

            if (resolvedParameterConfig.HasIsShooting != 0)
                animator.SetBool(resolvedParameterConfig.IsShootingHash, isShooting);

            if (resolvedParameterConfig.HasIsDashing != 0)
                animator.SetBool(resolvedParameterConfig.IsDashingHash, isDashing);

            UpdateProceduralParameters(animator,
                                       in resolvedParameterConfig,
                                       ref animatorRuntimeState.ValueRW,
                                       localMove,
                                       desiredLookDirection,
                                       isShooting,
                                       deltaTime);

            if (resolvedParameterConfig.HasShotPulse != 0 && isShooting && animatorRuntimeState.ValueRO.PreviousShooting == 0)
                animator.SetTrigger(resolvedParameterConfig.ShotPulseHash);

            animatorRuntimeState.ValueRW.PreviousShooting = isShooting ? (byte)1 : (byte)0;
            animatorRuntimeState.ValueRW.LastMoveX = localMove.x;
            animatorRuntimeState.ValueRW.LastMoveY = localMove.y;
        }

        if (processedAnimatorEntities == 0 && loggedNoAnimatorEntityWarning == 0)
        {
            Debug.LogWarning("[PlayerAnimatorSyncSystem] No animator-configured player entity was found. Verify PlayerAnimationBindingsPreset bake and player SubScene conversion.");
            loggedNoAnimatorEntityWarning = 1;
        }
    }
    #endregion

    #region Helpers
    private static float3 ResolveLookDirection(in PlayerLookState lookState, float3 fallback)
    {
        float3 lookDirection = lookState.DesiredDirection;

        if (math.lengthsq(lookDirection) <= DirectionEpsilon)
            lookDirection = lookState.CurrentDirection;

        return PlayerControllerMath.NormalizePlanar(lookDirection, fallback);
    }

    private static float2 ToLocalPlanar(float3 worldDirection, float3 right, float3 forward)
    {
        if (math.lengthsq(worldDirection) <= DirectionEpsilon)
            return float2.zero;

        float3 normalizedDirection = PlayerControllerMath.NormalizePlanar(worldDirection, forward);
        return new float2(math.dot(normalizedDirection, right), math.dot(normalizedDirection, forward));
    }

    private static void EnsureAnimatorBindings(Animator animator,
                                               Entity entity,
                                               in ComponentLookup<PlayerAnimatorControllerReference> animatorControllerLookup,
                                               in ComponentLookup<PlayerAnimatorAvatarReference> animatorAvatarLookup)
    {
        if (animator == null)
            return;

        bool requiresRebind = false;

        if (animator.runtimeAnimatorController == null && animatorControllerLookup.HasComponent(entity))
        {
            RuntimeAnimatorController fallbackController = animatorControllerLookup[entity].Controller.Value;

            if (fallbackController != null)
            {
                animator.runtimeAnimatorController = fallbackController;
                requiresRebind = true;
            }
        }

        if (animator.avatar == null && animatorAvatarLookup.HasComponent(entity))
        {
            Avatar fallbackAvatar = animatorAvatarLookup[entity].Avatar.Value;

            if (fallbackAvatar != null)
            {
                animator.avatar = fallbackAvatar;
                requiresRebind = true;
            }
        }

        if (requiresRebind == false)
            return;

        animator.Rebind();
        animator.Update(0f);
    }

    private static void EnsureAnimatorRuntimeAnimatorInstance(ref PlayerAnimatorRuntimeState runtimeState,
                                                              ref PlayerAnimatorParameterConfig config,
                                                              Animator animator)
    {
        if (animator == null)
        {
            return;
        }

        int currentAnimatorInstanceId = animator.GetInstanceID();

        if (runtimeState.BoundAnimatorInstanceId == currentAnimatorInstanceId)
        {
            return;
        }

        runtimeState.BoundAnimatorInstanceId = currentAnimatorInstanceId;
        runtimeState.ParametersValidated = 0;
        runtimeState.Initialized = 0;
        RestoreParameterPresenceFlagsFromHashes(ref config);
    }

    private static void RestoreParameterPresenceFlagsFromHashes(ref PlayerAnimatorParameterConfig config)
    {
        config.HasMoveX = ResolveHasParameterFromHash(config.MoveXHash);
        config.HasMoveY = ResolveHasParameterFromHash(config.MoveYHash);
        config.HasMoveSpeed = ResolveHasParameterFromHash(config.MoveSpeedHash);
        config.HasAimX = ResolveHasParameterFromHash(config.AimXHash);
        config.HasAimY = ResolveHasParameterFromHash(config.AimYHash);
        config.HasIsMoving = ResolveHasParameterFromHash(config.IsMovingHash);
        config.HasIsShooting = ResolveHasParameterFromHash(config.IsShootingHash);
        config.HasIsDashing = ResolveHasParameterFromHash(config.IsDashingHash);
        config.HasShotPulse = ResolveHasParameterFromHash(config.ShotPulseHash);
        config.HasProceduralRecoil = ResolveHasParameterFromHash(config.ProceduralRecoilHash);
        config.HasProceduralAimWeight = ResolveHasParameterFromHash(config.ProceduralAimWeightHash);
        config.HasProceduralLean = ResolveHasParameterFromHash(config.ProceduralLeanHash);
    }

    private static byte ResolveHasParameterFromHash(int parameterHash)
    {
        if (parameterHash == 0)
        {
            return 0;
        }

        return 1;
    }

    private static bool ValidateAnimatorParameters(Animator animator,
                                                   ref PlayerAnimatorParameterConfig config,
                                                   ref PlayerAnimatorRuntimeState runtimeState)
    {
        if (animator == null)
        {
            return false;
        }

        if (runtimeState.ParametersValidated != 0)
        {
            return false;
        }

        bool mismatchDetected = false;
        AnimatorControllerParameter[] runtimeParameters = animator.parameters;
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasMoveX,
                                                 config.MoveXHash,
                                                 AnimatorControllerParameterType.Float);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasMoveY,
                                                 config.MoveYHash,
                                                 AnimatorControllerParameterType.Float);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasMoveSpeed,
                                                 config.MoveSpeedHash,
                                                 AnimatorControllerParameterType.Float);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasAimX,
                                                 config.AimXHash,
                                                 AnimatorControllerParameterType.Float);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasAimY,
                                                 config.AimYHash,
                                                 AnimatorControllerParameterType.Float);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasIsMoving,
                                                 config.IsMovingHash,
                                                 AnimatorControllerParameterType.Bool);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasIsShooting,
                                                 config.IsShootingHash,
                                                 AnimatorControllerParameterType.Bool);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasIsDashing,
                                                 config.IsDashingHash,
                                                 AnimatorControllerParameterType.Bool);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasShotPulse,
                                                 config.ShotPulseHash,
                                                 AnimatorControllerParameterType.Trigger);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasProceduralRecoil,
                                                 config.ProceduralRecoilHash,
                                                 AnimatorControllerParameterType.Float);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasProceduralAimWeight,
                                                 config.ProceduralAimWeightHash,
                                                 AnimatorControllerParameterType.Float);
        mismatchDetected |= ValidateParameterHash(runtimeParameters,
                                                 ref config.HasProceduralLean,
                                                 config.ProceduralLeanHash,
                                                 AnimatorControllerParameterType.Float);
        runtimeState.ParametersValidated = 1;
        return mismatchDetected;
    }

    private static bool ValidateParameterHash(AnimatorControllerParameter[] runtimeParameters,
                                              ref byte hasParameter,
                                              int parameterHash,
                                              AnimatorControllerParameterType expectedParameterType)
    {
        if (hasParameter == 0)
        {
            return false;
        }

        bool hasMatchingRuntimeParameter = HasAnimatorParameterHashAndType(runtimeParameters,
                                                                           parameterHash,
                                                                           expectedParameterType);

        if (hasMatchingRuntimeParameter)
        {
            return false;
        }

        hasParameter = 0;
        return true;
    }

    private static bool HasAnimatorParameterHashAndType(AnimatorControllerParameter[] runtimeParameters,
                                                        int parameterHash,
                                                        AnimatorControllerParameterType expectedParameterType)
    {
        if (runtimeParameters == null || runtimeParameters.Length == 0)
        {
            return false;
        }

        for (int parameterIndex = 0; parameterIndex < runtimeParameters.Length; parameterIndex++)
        {
            AnimatorControllerParameter runtimeParameter = runtimeParameters[parameterIndex];

            if (runtimeParameter.nameHash != parameterHash)
            {
                continue;
            }

            return runtimeParameter.type == expectedParameterType;
        }

        return false;
    }

    private static void EnsureAnimatorRuntimeSettings(Animator animator, ref PlayerAnimatorRuntimeState runtimeState)
    {
        if (animator == null)
            return;

        if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (animator.updateMode != AnimatorUpdateMode.Normal)
            animator.updateMode = AnimatorUpdateMode.Normal;

        if (animator.speed <= 0f)
            animator.speed = 1f;

        if (runtimeState.Initialized != 0)
            return;

        int layerCount = animator.layerCount;

        for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            if (animator.GetLayerWeight(layerIndex) <= 0f)
                animator.SetLayerWeight(layerIndex, 1f);
        }

        animator.Rebind();
        animator.Update(0f);
        runtimeState.Initialized = 1;
    }

    private static void EnsureAnimatorOutline(Animator animator, in OutlineVisualConfig outlineConfig)
    {
        if (animator == null)
            return;

        ManagedOutlineRendererUtility.ApplyToAnimator(animator,
                                                     outlineConfig.Enabled != 0,
                                                     DamageFlashRuntimeUtility.ToManagedColor(outlineConfig.Color),
                                                     outlineConfig.Thickness);
    }

    private static float2 ResolveLocalMoveDirection(in PlayerMovementState movementState,
                                                    in PlayerAnimatorRuntimeState runtimeState,
                                                    float3 right,
                                                    float3 forward)
    {
        float2 localMove = ToLocalPlanar(movementState.DesiredDirection, right, forward);

        if (math.lengthsq(localMove) > DirectionEpsilon)
            return localMove;

        float2 velocityMove = ToLocalPlanar(movementState.Velocity, right, forward);

        if (math.lengthsq(velocityMove) > DirectionEpsilon)
            return velocityMove;

        float2 previousMove = new float2(runtimeState.LastMoveX, runtimeState.LastMoveY);

        if (math.lengthsq(previousMove) > DirectionEpsilon)
            return previousMove;

        // Keep a deterministic non-zero direction so 2D blend trees without a center idle clip don't fall back to bind pose.
        return new float2(0f, 1f);
    }

    private static void UpdateProceduralParameters(Animator animator,
                                                   in PlayerAnimatorParameterConfig config,
                                                   ref PlayerAnimatorRuntimeState runtimeState,
                                                   float2 localMove,
                                                   float3 desiredLookDirection,
                                                   bool isShooting,
                                                   float deltaTime)
    {
        if (config.ProceduralRecoilEnabled != 0)
        {
            if (isShooting && runtimeState.PreviousShooting == 0)
                runtimeState.RecoilValue = math.saturate(runtimeState.RecoilValue + math.max(0f, config.ProceduralRecoilKick));

            runtimeState.RecoilValue -= math.max(0f, config.ProceduralRecoilRecoveryPerSecond) * deltaTime;

            if (runtimeState.RecoilValue < 0f)
                runtimeState.RecoilValue = 0f;
        }
        else
            runtimeState.RecoilValue = 0f;

        if (config.HasProceduralRecoil != 0)
            WriteFloatParameter(animator,
                                in config,
                                config.HasProceduralRecoil,
                                config.ProceduralRecoilHash,
                                runtimeState.RecoilValue,
                                deltaTime);

        float targetAimWeight = math.lengthsq(desiredLookDirection) > DirectionEpsilon ? 1f : 0f;

        if (config.ProceduralAimWeightEnabled != 0)
            runtimeState.AimWeightValue = SmoothTowards(runtimeState.AimWeightValue,
                                                        targetAimWeight,
                                                        config.ProceduralAimWeightSmoothing,
                                                        deltaTime);
        else
            runtimeState.AimWeightValue = targetAimWeight;

        if (config.HasProceduralAimWeight != 0)
            WriteFloatParameter(animator,
                                in config,
                                config.HasProceduralAimWeight,
                                config.ProceduralAimWeightHash,
                                runtimeState.AimWeightValue,
                                deltaTime);

        float targetLean = math.clamp(localMove.x, -1f, 1f);

        if (config.ProceduralLeanEnabled != 0)
            runtimeState.LeanValue = SmoothTowards(runtimeState.LeanValue,
                                                   targetLean,
                                                   config.ProceduralLeanSmoothing,
                                                   deltaTime);
        else
            runtimeState.LeanValue = 0f;

        if (config.HasProceduralLean != 0)
            WriteFloatParameter(animator,
                                in config,
                                config.HasProceduralLean,
                                config.ProceduralLeanHash,
                                runtimeState.LeanValue,
                                deltaTime);
    }

    private static float SmoothTowards(float current, float target, float smoothing, float deltaTime)
    {
        float clampedSmoothing = math.max(0f, smoothing);

        if (clampedSmoothing <= DirectionEpsilon)
            return target;

        float blend = 1f - math.exp(-clampedSmoothing * deltaTime);
        return math.lerp(current, target, blend);
    }

    private static void WriteFloatParameter(Animator animator,
                                            in PlayerAnimatorParameterConfig config,
                                            byte hasParameter,
                                            int parameterHash,
                                            float value,
                                            float deltaTime)
    {
        if (hasParameter == 0)
            return;

        if (config.UseFloatDamping != 0)
        {
            animator.SetFloat(parameterHash,
                              value,
                              math.max(0f, config.FloatDampTime),
                              deltaTime);
            return;
        }

        animator.SetFloat(parameterHash, value);
    }
    #endregion

    #endregion
}
