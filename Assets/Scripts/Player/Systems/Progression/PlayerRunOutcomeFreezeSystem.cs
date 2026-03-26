using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Freezes all player-driven runtime state once a run outcome becomes final so no late input, dashes, milestones, or time-scale resumes can continue.
///  None.
/// returns None.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerRunOutcomeFreezeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime state required to freeze gameplay after victory or defeat.
    ///  state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();
        state.RequireForUpdate<PlayerRunOutcomeState>();
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<PlayerShootingState>();
    }

    /// <summary>
    /// Clears active player runtime state and milestone runtime side effects while keeping the finalized run outcome intact.
    ///  state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerDashState> dashLookup = SystemAPI.GetComponentLookup<PlayerDashState>(false);
        ComponentLookup<PlayerMilestonePowerUpSelectionState> milestoneSelectionLookup = SystemAPI.GetComponentLookup<PlayerMilestonePowerUpSelectionState>(false);
        ComponentLookup<PlayerMilestoneTimeScaleResumeState> milestoneResumeLookup = SystemAPI.GetComponentLookup<PlayerMilestoneTimeScaleResumeState>(false);
        BufferLookup<PlayerMilestonePowerUpSelectionOfferElement> milestoneOfferLookup = SystemAPI.GetBufferLookup<PlayerMilestonePowerUpSelectionOfferElement>(false);
        BufferLookup<PlayerMilestonePowerUpSelectionCommand> milestoneCommandLookup = SystemAPI.GetBufferLookup<PlayerMilestonePowerUpSelectionCommand>(false);
        bool anyFinalizedRunFound = false;

        // Reset all runtime-driven player state exactly once per finalized run.
        foreach ((RefRW<PlayerRunOutcomeState> runOutcomeState,
                  RefRW<PlayerInputState> inputState,
                  RefRW<PlayerMovementState> movementState,
                  RefRW<PlayerLookState> lookState,
                  RefRW<PlayerShootingState> shootingState,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerRunOutcomeState>,
                                    RefRW<PlayerInputState>,
                                    RefRW<PlayerMovementState>,
                                    RefRW<PlayerLookState>,
                                    RefRW<PlayerShootingState>>()
                             .WithAll<PlayerControllerConfig>()
                             .WithEntityAccess())
        {
            if (runOutcomeState.ValueRO.IsFinalized == 0)
                continue;

            anyFinalizedRunFound = true;

            if (runOutcomeState.ValueRO.RuntimeFreezeApplied == 0)
            {
                ResetInputState(ref inputState.ValueRW);
                ResetMovementState(ref movementState.ValueRW);
                ResetLookState(ref lookState.ValueRW);
                ResetShootingState(ref shootingState.ValueRW);
                ResetDashState(entity, ref dashLookup);
                ResetMilestoneRuntimeState(entity,
                                           ref milestoneSelectionLookup,
                                           ref milestoneResumeLookup,
                                           ref milestoneOfferLookup,
                                           ref milestoneCommandLookup);
                runOutcomeState.ValueRW.RuntimeFreezeApplied = 1;
            }
        }

        if (anyFinalizedRunFound)
            Time.timeScale = 0f;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Clears all live player input channels so later gameplay systems observe a fully idle controller.
    ///  inputState: Mutable runtime input state stored on the player entity.
    /// returns None.
    /// </summary>
    private static void ResetInputState(ref PlayerInputState inputState)
    {
        inputState.Move = float2.zero;
        inputState.Look = float2.zero;
        inputState.Shoot = 0f;
        inputState.PowerUpPrimary = 0f;
        inputState.PowerUpSecondary = 0f;
        inputState.SwapPowerUpSlots = 0f;
    }

    /// <summary>
    /// Stops all runtime movement immediately, including any held digital-direction bookkeeping.
    ///  movementState: Mutable movement state stored on the player entity.
    /// returns None.
    /// </summary>
    private static void ResetMovementState(ref PlayerMovementState movementState)
    {
        movementState.DesiredDirection = float3.zero;
        movementState.Velocity = float3.zero;
        movementState.PrevMoveMask = 0;
        movementState.CurrMoveMask = 0;
        movementState.MovePressTimes = float4.zero;
        movementState.ReleaseHoldMask = 0;
        movementState.ReleaseHoldUntilTime = 0f;
    }

    /// <summary>
    /// Freezes look state on the current facing direction and clears digital-look bookkeeping.
    ///  lookState: Mutable look state stored on the player entity.
    /// returns None.
    /// </summary>
    private static void ResetLookState(ref PlayerLookState lookState)
    {
        float3 frozenDirection = PlayerControllerMath.NormalizePlanar(lookState.CurrentDirection, new float3(0f, 0f, 1f));
        lookState.DesiredDirection = frozenDirection;
        lookState.CurrentDirection = frozenDirection;
        lookState.AngularSpeed = 0f;
        lookState.PrevLookMask = 0;
        lookState.CurrLookMask = 0;
        lookState.LookPressTimes = float4.zero;
        lookState.ReleaseHoldMask = 0;
        lookState.ReleaseHoldUntilTime = 0f;
    }

    /// <summary>
    /// Stops all shooting state so automatic modes cannot continue firing after the run outcome is final.
    ///  shootingState: Mutable shooting state stored on the player entity.
    /// returns None.
    /// </summary>
    private static void ResetShootingState(ref PlayerShootingState shootingState)
    {
        shootingState.AutomaticEnabled = 0;
        shootingState.PreviousShootPressed = 0;
        shootingState.VisualShootingActive = 0;
    }

    /// <summary>
    /// Ends any active dash immediately when the player run reaches a terminal outcome.
    ///  entity: Player entity whose optional dash state should be cleared.
    ///  dashLookup: Component lookup used to mutate PlayerDashState.
    /// returns None.
    /// </summary>
    private static void ResetDashState(Entity entity, ref ComponentLookup<PlayerDashState> dashLookup)
    {
        if (!dashLookup.HasComponent(entity))
            return;

        dashLookup[entity] = default;
    }

    /// <summary>
    /// Cancels any active milestone selection flow and clears its queued commands and offers.
    ///  entity: Player entity that owns the milestone runtime state.
    ///  milestoneSelectionLookup: Lookup used to mutate selection state.
    ///  milestoneResumeLookup: Lookup used to mutate time-scale resume state.
    ///  milestoneOfferLookup: Lookup used to clear rolled milestone offers.
    ///  milestoneCommandLookup: Lookup used to clear queued HUD commands.
    /// returns None.
    /// </summary>
    private static void ResetMilestoneRuntimeState(Entity entity,
                                                   ref ComponentLookup<PlayerMilestonePowerUpSelectionState> milestoneSelectionLookup,
                                                   ref ComponentLookup<PlayerMilestoneTimeScaleResumeState> milestoneResumeLookup,
                                                   ref BufferLookup<PlayerMilestonePowerUpSelectionOfferElement> milestoneOfferLookup,
                                                   ref BufferLookup<PlayerMilestonePowerUpSelectionCommand> milestoneCommandLookup)
    {
        if (milestoneSelectionLookup.HasComponent(entity))
            milestoneSelectionLookup[entity] = default;

        if (milestoneResumeLookup.HasComponent(entity))
            milestoneResumeLookup[entity] = PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState();

        if (milestoneOfferLookup.HasBuffer(entity))
            milestoneOfferLookup[entity].Clear();

        if (milestoneCommandLookup.HasBuffer(entity))
            milestoneCommandLookup[entity].Clear();
    }
    #endregion

    #endregion
}
