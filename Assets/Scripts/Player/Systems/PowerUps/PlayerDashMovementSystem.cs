using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies dash kinematics and manages dash invulnerability timers.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementSpeedSystem))]
[UpdateBefore(typeof(PlayerMovementApplySystem))]
public partial struct PlayerDashMovementSystem : ISystem
{
    #region Constants
    private const float MinimumBlendDuration = 0.0001f;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerDashState>();
        state.RequireForUpdate<PlayerMovementState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<PlayerDashState> dashState,
                  RefRW<PlayerMovementState> movementState) in SystemAPI.Query<RefRW<PlayerDashState>, RefRW<PlayerMovementState>>())
        {
            UpdateInvulnerability(ref dashState.ValueRW, deltaTime);

            if (dashState.ValueRO.IsDashing == 0)
                continue;

            float3 baseVelocity = movementState.ValueRO.Velocity;
            float3 dashDirection = math.normalizesafe(dashState.ValueRO.Direction, new float3(0f, 0f, 1f));
            float3 dashVelocity = dashDirection * dashState.ValueRO.Speed;

            switch (dashState.ValueRO.Phase)
            {
                case 1:
                    ApplyTransitionIn(ref dashState.ValueRW,
                                      ref movementState.ValueRW,
                                      dashVelocity,
                                      deltaTime);
                    continue;
                case 2:
                    ApplyHold(ref dashState.ValueRW,
                              ref movementState.ValueRW,
                              dashVelocity,
                              deltaTime);
                    continue;
                case 3:
                    ApplyTransitionOut(ref dashState.ValueRW,
                                       ref movementState.ValueRW,
                                       dashVelocity,
                                       baseVelocity,
                                       deltaTime);
                    continue;
                default:
                    EndDash(ref dashState.ValueRW);
                    movementState.ValueRW.Velocity = baseVelocity;
                    continue;
            }
        }
    }
    #endregion

    #region Helpers
    private static void UpdateInvulnerability(ref PlayerDashState dashState, float deltaTime)
    {
        if (dashState.RemainingInvulnerability <= 0f)
            return;

        float nextInvulnerability = dashState.RemainingInvulnerability - deltaTime;

        if (nextInvulnerability < 0f)
            nextInvulnerability = 0f;

        dashState.RemainingInvulnerability = nextInvulnerability;
    }

    private static void ApplyTransitionIn(ref PlayerDashState dashState,
                                          ref PlayerMovementState movementState,
                                          float3 dashVelocity,
                                          float deltaTime)
    {
        float duration = math.max(MinimumBlendDuration, dashState.TransitionInDuration);
        float nextPhaseRemaining = dashState.PhaseRemaining - deltaTime;
        float elapsed = duration - math.max(nextPhaseRemaining, 0f);
        float blend = math.saturate(elapsed / duration);
        float3 blendedVelocity = math.lerp(dashState.EntryVelocity, dashVelocity, blend);

        movementState.Velocity = blendedVelocity;

        if (nextPhaseRemaining > 0f)
        {
            dashState.PhaseRemaining = nextPhaseRemaining;
            return;
        }

        if (dashState.HoldDuration > 0f)
        {
            dashState.Phase = 2;
            dashState.PhaseRemaining = dashState.HoldDuration;
            return;
        }

        if (dashState.TransitionOutDuration > 0f)
        {
            dashState.Phase = 3;
            dashState.PhaseRemaining = dashState.TransitionOutDuration;
            return;
        }

        EndDash(ref dashState);
    }

    private static void ApplyHold(ref PlayerDashState dashState,
                                  ref PlayerMovementState movementState,
                                  float3 dashVelocity,
                                  float deltaTime)
    {
        movementState.Velocity = dashVelocity;

        float nextPhaseRemaining = dashState.PhaseRemaining - deltaTime;

        if (nextPhaseRemaining > 0f)
        {
            dashState.PhaseRemaining = nextPhaseRemaining;
            return;
        }

        if (dashState.TransitionOutDuration > 0f)
        {
            dashState.Phase = 3;
            dashState.PhaseRemaining = dashState.TransitionOutDuration;
            return;
        }

        EndDash(ref dashState);
    }

    private static void ApplyTransitionOut(ref PlayerDashState dashState,
                                           ref PlayerMovementState movementState,
                                           float3 dashVelocity,
                                           float3 baseVelocity,
                                           float deltaTime)
    {
        float duration = math.max(MinimumBlendDuration, dashState.TransitionOutDuration);
        float nextPhaseRemaining = dashState.PhaseRemaining - deltaTime;
        float elapsed = duration - math.max(nextPhaseRemaining, 0f);
        float blend = math.saturate(elapsed / duration);
        float3 blendedVelocity = math.lerp(dashVelocity, baseVelocity, blend);

        movementState.Velocity = blendedVelocity;

        if (nextPhaseRemaining > 0f)
        {
            dashState.PhaseRemaining = nextPhaseRemaining;
            return;
        }

        movementState.Velocity = baseVelocity;
        EndDash(ref dashState);
    }

    private static void EndDash(ref PlayerDashState dashState)
    {
        dashState.IsDashing = 0;
        dashState.Phase = 0;
        dashState.PhaseRemaining = 0f;
        dashState.HoldDuration = 0f;
        dashState.Direction = float3.zero;
        dashState.EntryVelocity = float3.zero;
        dashState.Speed = 0f;
        dashState.TransitionInDuration = 0f;
        dashState.TransitionOutDuration = 0f;
    }
    #endregion

    #endregion
}
