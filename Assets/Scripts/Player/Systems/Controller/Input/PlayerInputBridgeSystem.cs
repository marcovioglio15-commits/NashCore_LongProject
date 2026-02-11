using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
public partial struct PlayerInputBridgeSystem : ISystem
{
    #if UNITY_EDITOR
    #region Editor Debug
    private static bool loggedInput;
    #endregion
    #endif

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        InputAction moveAction = PlayerInputRuntime.MoveAction;
        InputAction lookAction = PlayerInputRuntime.LookAction;
        InputAction shootAction = PlayerInputRuntime.ShootAction;
        InputAction powerUpPrimaryAction = PlayerInputRuntime.PowerUpPrimaryAction;
        InputAction powerUpSecondaryAction = PlayerInputRuntime.PowerUpSecondaryAction;
        float2 move = float2.zero;
        float2 look = float2.zero;
        float shoot = 0f;
        float powerUpPrimary = 0f;
        float powerUpSecondary = 0f;
        bool isInputReady = PlayerInputRuntime.IsReady;

        if (isInputReady)
        {
            if (moveAction != null)
            {
                Vector2 moveValue = moveAction.ReadValue<Vector2>();
                move = new float2(moveValue.x, moveValue.y);
            }

            if (lookAction != null)
            {
                Vector2 lookValue = lookAction.ReadValue<Vector2>();
                look = new float2(lookValue.x, lookValue.y);
            }

            if (shootAction != null)
            {
                shoot = shootAction.IsPressed() ? 1f : 0f;
            }

            if (powerUpPrimaryAction != null)
            {
                powerUpPrimary = powerUpPrimaryAction.IsPressed() ? 1f : 0f;
            }

            if (powerUpSecondaryAction != null)
            {
                powerUpSecondary = powerUpSecondaryAction.IsPressed() ? 1f : 0f;
            }
        }

        foreach (RefRW<PlayerInputState> inputState in SystemAPI.Query<RefRW<PlayerInputState>>())
        {
            inputState.ValueRW.Move = move;
            inputState.ValueRW.Look = look;
            inputState.ValueRW.Shoot = shoot;
            inputState.ValueRW.PowerUpPrimary = powerUpPrimary;
            inputState.ValueRW.PowerUpSecondary = powerUpSecondary;
        }

        #if UNITY_EDITOR
        if (loggedInput == false && (math.lengthsq(move) > 0f || math.lengthsq(look) > 0f || shoot > 0f))
        {
            loggedInput = true;
            Debug.Log(string.Format("[PlayerInputBridgeSystem] Input detected. Move: {0} | Look: {1} | Shoot: {2}", move, look, shoot));
        }

        #endif
    }
    #endregion
}
#endregion
