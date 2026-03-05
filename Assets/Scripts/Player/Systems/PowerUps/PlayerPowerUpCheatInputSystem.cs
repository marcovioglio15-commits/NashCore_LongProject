using Unity.Entities;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Converts Ctrl+Shift+Number keyboard shortcuts into runtime commands that swap the whole power-up preset.
/// Number keys map directly to preset indices in the baked cheat snapshot list (0..9).
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerInputBridgeSystem))]
[UpdateBefore(typeof(PlayerPowerUpCheatSystem))]
public partial struct PlayerPowerUpCheatInputSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers update prerequisites for runtime cheat input processing.
    /// </summary>
    /// <param name="state">System state used to declare required components.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpCheatCommand>();
        state.RequireForUpdate<PlayerPowerUpCheatPresetEntry>();
    }

    /// <summary>
    /// Captures Ctrl+Shift+Number keyboard input and enqueues one preset-swap command for the local player entity.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (IsControlPressed(keyboard) == false)
            return;

        if (IsShiftPressed(keyboard) == false)
            return;

        int presetIndex;

        if (TryResolvePressedPresetIndex(keyboard, out presetIndex) == false)
            return;

        foreach ((DynamicBuffer<PlayerPowerUpCheatCommand> cheatCommands,
                  DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries) in SystemAPI.Query<DynamicBuffer<PlayerPowerUpCheatCommand>,
                                                                                                     DynamicBuffer<PlayerPowerUpCheatPresetEntry>>().WithAll<PlayerControllerConfig>())
        {
            if (cheatPresetEntries.Length <= 0)
                break;

            AddApplyPresetCommand(cheatCommands, presetIndex);
            break;
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Checks whether at least one Control key is currently pressed.
    /// </summary>
    /// <param name="keyboard">Current keyboard device.</param>
    /// <returns>True when left or right Control is pressed, otherwise false.</returns>
    private static bool IsControlPressed(Keyboard keyboard)
    {
        if (keyboard.leftCtrlKey.isPressed)
            return true;

        if (keyboard.rightCtrlKey.isPressed)
            return true;

        return false;
    }

    /// <summary>
    /// Checks whether at least one Shift key is currently pressed.
    /// </summary>
    /// <param name="keyboard">Current keyboard device.</param>
    /// <returns>True when left or right Shift is pressed, otherwise false.</returns>
    private static bool IsShiftPressed(Keyboard keyboard)
    {
        if (keyboard.leftShiftKey.isPressed)
            return true;

        if (keyboard.rightShiftKey.isPressed)
            return true;

        return false;
    }

    /// <summary>
    /// Resolves which numeric key was pressed this frame and converts it to a preset index.
    /// </summary>
    /// <param name="keyboard">Current keyboard device.</param>
    /// <param name="presetIndex">Resolved preset index when a valid key is pressed.</param>
    /// <returns>True when a supported numeric key was pressed this frame, otherwise false.</returns>
    private static bool TryResolvePressedPresetIndex(Keyboard keyboard, out int presetIndex)
    {
        if (IsDigitPressedThisFrame(keyboard.digit0Key, keyboard.numpad0Key))
        {
            presetIndex = 0;
            return true;
        }

        if (IsDigitPressedThisFrame(keyboard.digit1Key, keyboard.numpad1Key))
        {
            presetIndex = 1;
            return true;
        }

        if (IsDigitPressedThisFrame(keyboard.digit2Key, keyboard.numpad2Key))
        {
            presetIndex = 2;
            return true;
        }

        if (IsDigitPressedThisFrame(keyboard.digit3Key, keyboard.numpad3Key))
        {
            presetIndex = 3;
            return true;
        }

        if (IsDigitPressedThisFrame(keyboard.digit4Key, keyboard.numpad4Key))
        {
            presetIndex = 4;
            return true;
        }

        if (IsDigitPressedThisFrame(keyboard.digit5Key, keyboard.numpad5Key))
        {
            presetIndex = 5;
            return true;
        }

        if (IsDigitPressedThisFrame(keyboard.digit6Key, keyboard.numpad6Key))
        {
            presetIndex = 6;
            return true;
        }

        if (IsDigitPressedThisFrame(keyboard.digit7Key, keyboard.numpad7Key))
        {
            presetIndex = 7;
            return true;
        }

        if (IsDigitPressedThisFrame(keyboard.digit8Key, keyboard.numpad8Key))
        {
            presetIndex = 8;
            return true;
        }

        if (IsDigitPressedThisFrame(keyboard.digit9Key, keyboard.numpad9Key))
        {
            presetIndex = 9;
            return true;
        }

        presetIndex = -1;
        return false;
    }

    /// <summary>
    /// Checks whether either the main digit key or the matching numpad digit was pressed this frame.
    /// </summary>
    /// <param name="digitKey">Main keyboard digit key.</param>
    /// <param name="numpadKey">Numpad digit key.</param>
    /// <returns>True when one of the provided keys was pressed this frame, otherwise false.</returns>
    private static bool IsDigitPressedThisFrame(KeyControl digitKey, KeyControl numpadKey)
    {
        if (digitKey != null && digitKey.wasPressedThisFrame)
            return true;

        if (numpadKey != null && numpadKey.wasPressedThisFrame)
            return true;

        return false;
    }

    /// <summary>
    /// Enqueues one command that requests a complete preset replacement by index.
    /// </summary>
    /// <param name="cheatCommands">Target runtime cheat command buffer.</param>
    /// <param name="presetIndex">Preset index requested by user input.</param>
    private static void AddApplyPresetCommand(DynamicBuffer<PlayerPowerUpCheatCommand> cheatCommands, int presetIndex)
    {
        cheatCommands.Add(new PlayerPowerUpCheatCommand
        {
            CommandType = PlayerPowerUpCheatCommandType.ApplyPresetByIndex,
            PresetIndex = presetIndex
        });
    }
    #endregion

    #endregion
}
