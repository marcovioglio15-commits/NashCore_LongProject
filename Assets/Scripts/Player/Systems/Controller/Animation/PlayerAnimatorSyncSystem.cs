using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Pushes ECS player state into a managed Animator while keeping gameplay authority in ECS.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct PlayerAnimatorSyncSystem : ISystem
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private static readonly float3 DefaultForward = new float3(0f, 0f, 1f);
    private static readonly float3 WorldUp = new float3(0f, 1f, 0f);
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerAnimatorParameterConfig>();
        state.RequireForUpdate<PlayerAnimatorRuntimeState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerShootingState>();
        state.RequireForUpdate<LocalTransform>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        EntityManager entityManager = state.EntityManager;
        ComponentLookup<PlayerDashState> dashLookup = SystemAPI.GetComponentLookup<PlayerDashState>(true);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<PlayerMovementState> movementState,
                  RefRO<PlayerLookState> lookState,
                  RefRO<PlayerShootingState> shootingState,
                  RefRO<LocalTransform> localTransform,
                  RefRO<PlayerAnimatorParameterConfig> parameterConfig,
                  RefRW<PlayerAnimatorRuntimeState> animatorRuntimeState,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerMovementState>,
                                    RefRO<PlayerLookState>,
                                    RefRO<PlayerShootingState>,
                                    RefRO<LocalTransform>,
                                    RefRO<PlayerAnimatorParameterConfig>,
                                    RefRW<PlayerAnimatorRuntimeState>>()
                             .WithEntityAccess())
        {
            PlayerAnimatorReference animatorReference = entityManager.GetComponentObject<PlayerAnimatorReference>(entity);

            if (animatorReference == null)
                continue;

            Animator animator = animatorReference.Animator;

            if (animator == null)
                continue;

            ApplyAnimatorController(animatorReference, animator);

            if (parameterConfig.ValueRO.DisableRootMotion != 0 && animator.applyRootMotion)
                animator.applyRootMotion = false;

            float3 forward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRO.Rotation), DefaultForward);
            float3 right = math.normalize(math.cross(WorldUp, forward));
            float3 desiredMoveDirection = movementState.ValueRO.DesiredDirection;
            float3 desiredLookDirection = ResolveLookDirection(lookState.ValueRO, forward);
            float2 localMove = ToLocalPlanar(desiredMoveDirection, right, forward);
            float2 localAim = ToLocalPlanar(desiredLookDirection, right, forward);
            float moveSpeed = math.length(movementState.ValueRO.Velocity);
            bool isMoving = moveSpeed > math.max(0f, parameterConfig.ValueRO.MovingSpeedThreshold);
            bool isShooting = shootingState.ValueRO.PreviousShootPressed != 0;
            bool isDashing = dashLookup.HasComponent(entity) && dashLookup[entity].IsDashing != 0;

            WriteFloatParameter(animator,
                                in parameterConfig.ValueRO,
                                parameterConfig.ValueRO.HasMoveX,
                                parameterConfig.ValueRO.MoveXHash,
                                localMove.x,
                                deltaTime);
            WriteFloatParameter(animator,
                                in parameterConfig.ValueRO,
                                parameterConfig.ValueRO.HasMoveY,
                                parameterConfig.ValueRO.MoveYHash,
                                localMove.y,
                                deltaTime);
            WriteFloatParameter(animator,
                                in parameterConfig.ValueRO,
                                parameterConfig.ValueRO.HasMoveSpeed,
                                parameterConfig.ValueRO.MoveSpeedHash,
                                moveSpeed,
                                deltaTime);
            WriteFloatParameter(animator,
                                in parameterConfig.ValueRO,
                                parameterConfig.ValueRO.HasAimX,
                                parameterConfig.ValueRO.AimXHash,
                                localAim.x,
                                deltaTime);
            WriteFloatParameter(animator,
                                in parameterConfig.ValueRO,
                                parameterConfig.ValueRO.HasAimY,
                                parameterConfig.ValueRO.AimYHash,
                                localAim.y,
                                deltaTime);

            if (parameterConfig.ValueRO.HasIsMoving != 0)
                animator.SetBool(parameterConfig.ValueRO.IsMovingHash, isMoving);

            if (parameterConfig.ValueRO.HasIsShooting != 0)
                animator.SetBool(parameterConfig.ValueRO.IsShootingHash, isShooting);

            if (parameterConfig.ValueRO.HasIsDashing != 0)
                animator.SetBool(parameterConfig.ValueRO.IsDashingHash, isDashing);

            UpdateProceduralParameters(animator,
                                       in parameterConfig.ValueRO,
                                       ref animatorRuntimeState.ValueRW,
                                       localMove,
                                       desiredLookDirection,
                                       isShooting,
                                       deltaTime);

            if (parameterConfig.ValueRO.HasShotPulse != 0 && isShooting && animatorRuntimeState.ValueRO.PreviousShooting == 0)
                animator.SetTrigger(parameterConfig.ValueRO.ShotPulseHash);

            animatorRuntimeState.ValueRW.PreviousShooting = isShooting ? (byte)1 : (byte)0;
        }
    }
    #endregion

    #region Helpers
    private static void ApplyAnimatorController(PlayerAnimatorReference animatorReference, Animator animator)
    {
        if (animatorReference.ControllerAssigned != 0)
            return;

        RuntimeAnimatorController targetController = animatorReference.AnimatorController;

        if (targetController != null && animator.runtimeAnimatorController != targetController)
            animator.runtimeAnimatorController = targetController;

        animatorReference.ControllerAssigned = 1;
    }

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
