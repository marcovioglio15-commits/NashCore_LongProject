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
    private static bool s_LoggedInput;
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
        float2 move = float2.zero;
        float2 look = float2.zero;

        if (PlayerInputRuntime.IsReady)
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
        }

        foreach (RefRW<PlayerInputState> inputState in SystemAPI.Query<RefRW<PlayerInputState>>())
        {
            inputState.ValueRW.Move = move;
            inputState.ValueRW.Look = look;
            inputState.ValueRW.PrimaryAction = 0f;
            inputState.ValueRW.SecondaryAction = 0f;
        }

        #if UNITY_EDITOR
        if (s_LoggedInput == false && (math.lengthsq(move) > 0f || math.lengthsq(look) > 0f))
        {
            s_LoggedInput = true;
            Debug.Log(string.Format("[PlayerInputBridgeSystem] Input detected. Move: {0} | Look: {1}", move, look));
        }

        #endif
    }
    #endregion
}
#endregion
