using Unity.Entities;
using UnityEngine.InputSystem;

/// <summary>
/// Converts keyboard cheat shortcuts into runtime power-up cheat commands.
/// Works in both editor and player builds.
/// Shortcut map (hold Ctrl):
/// F1 refill energy, F2 reset cooldowns, F3 swap active slots.
/// 1..6 set primary active kind (Bomb, Dash, BulletTime, Shotgun, ChargeShot, Heal).
/// Shift+1..6 set secondary active kind.
/// F4/F5 add-remove splitting passive, F6 clear all passives.
/// 7..9/0 add-remove projectile size and elemental-projectiles passives.
/// Hold Alt (Ctrl+Alt) for extended passive shortcuts:
/// F4/F5 add-remove explosion, F6/F7 add-remove elemental trail.
/// 7/8 add-remove perfect circle, 9/0 add-remove bouncing projectiles.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerInputBridgeSystem))]
[UpdateBefore(typeof(PlayerPowerUpCheatSystem))]
public partial struct PlayerPowerUpCheatInputSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpCheatCommand>();
    }

    public void OnUpdate(ref SystemState state)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        bool controlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;

        if (controlPressed == false)
            return;

        if (HasAnyCommandPressedThisFrame(keyboard) == false)
            return;

        bool shiftPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        bool altPressed = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

        foreach (DynamicBuffer<PlayerPowerUpCheatCommand> cheatBuffer in SystemAPI.Query<DynamicBuffer<PlayerPowerUpCheatCommand>>().WithAll<PlayerControllerConfig>())
        {
            EnqueueCommandsForKeyboard(cheatBuffer, keyboard, shiftPressed, altPressed);
            break;
        }
    }
    #endregion

    #region Helpers
    private static bool HasAnyCommandPressedThisFrame(Keyboard keyboard)
    {
        if (keyboard.f1Key.wasPressedThisFrame)
            return true;

        if (keyboard.f2Key.wasPressedThisFrame)
            return true;

        if (keyboard.f3Key.wasPressedThisFrame)
            return true;

        if (keyboard.f4Key.wasPressedThisFrame)
            return true;

        if (keyboard.f5Key.wasPressedThisFrame)
            return true;

        if (keyboard.f6Key.wasPressedThisFrame)
            return true;

        if (keyboard.f7Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit0Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit1Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit2Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit3Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit4Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit5Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit6Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit7Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit8Key.wasPressedThisFrame)
            return true;

        if (keyboard.digit9Key.wasPressedThisFrame)
            return true;

        return false;
    }

    private static void EnqueueCommandsForKeyboard(DynamicBuffer<PlayerPowerUpCheatCommand> cheatBuffer,
                                                   Keyboard keyboard,
                                                   bool shiftPressed,
                                                   bool altPressed)
    {
        if (keyboard.f1Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.RefillEnergy, ActiveToolKind.Custom, PassiveToolKind.Custom);

        if (keyboard.f2Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.ResetCooldowns, ActiveToolKind.Custom, PassiveToolKind.Custom);

        if (keyboard.f3Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.SwapActiveSlots, ActiveToolKind.Custom, PassiveToolKind.Custom);

        if (altPressed)
            EnqueueExtendedPassiveCommands(cheatBuffer, keyboard);
        else
            EnqueueDefaultPassiveCommands(cheatBuffer, keyboard);

        TryEnqueueActiveKindCommand(cheatBuffer, keyboard.digit1Key.wasPressedThisFrame, shiftPressed, ActiveToolKind.Bomb);
        TryEnqueueActiveKindCommand(cheatBuffer, keyboard.digit2Key.wasPressedThisFrame, shiftPressed, ActiveToolKind.Dash);
        TryEnqueueActiveKindCommand(cheatBuffer, keyboard.digit3Key.wasPressedThisFrame, shiftPressed, ActiveToolKind.BulletTime);
        TryEnqueueActiveKindCommand(cheatBuffer, keyboard.digit4Key.wasPressedThisFrame, shiftPressed, ActiveToolKind.Shotgun);
        TryEnqueueActiveKindCommand(cheatBuffer, keyboard.digit5Key.wasPressedThisFrame, shiftPressed, ActiveToolKind.ChargeShot);
        TryEnqueueActiveKindCommand(cheatBuffer, keyboard.digit6Key.wasPressedThisFrame, shiftPressed, ActiveToolKind.PortableHealthPack);
    }

    private static void EnqueueDefaultPassiveCommands(DynamicBuffer<PlayerPowerUpCheatCommand> cheatBuffer, Keyboard keyboard)
    {
        if (keyboard.f4Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.AddPassiveByKind, ActiveToolKind.Custom, PassiveToolKind.SplittingProjectiles);

        if (keyboard.f5Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.RemovePassiveByKind, ActiveToolKind.Custom, PassiveToolKind.SplittingProjectiles);

        if (keyboard.f6Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.ClearPassives, ActiveToolKind.Custom, PassiveToolKind.Custom);

        if (keyboard.digit7Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.AddPassiveByKind, ActiveToolKind.Custom, PassiveToolKind.ProjectileSize);

        if (keyboard.digit8Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.AddPassiveByKind, ActiveToolKind.Custom, PassiveToolKind.ElementalProjectiles);

        if (keyboard.digit9Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.RemovePassiveByKind, ActiveToolKind.Custom, PassiveToolKind.ProjectileSize);

        if (keyboard.digit0Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.RemovePassiveByKind, ActiveToolKind.Custom, PassiveToolKind.ElementalProjectiles);
    }

    private static void EnqueueExtendedPassiveCommands(DynamicBuffer<PlayerPowerUpCheatCommand> cheatBuffer, Keyboard keyboard)
    {
        if (keyboard.f4Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.AddPassiveByKind, ActiveToolKind.Custom, PassiveToolKind.Explosion);

        if (keyboard.f5Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.RemovePassiveByKind, ActiveToolKind.Custom, PassiveToolKind.Explosion);

        if (keyboard.f6Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.AddPassiveByKind, ActiveToolKind.Custom, PassiveToolKind.ElementalTrail);

        if (keyboard.f7Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.RemovePassiveByKind, ActiveToolKind.Custom, PassiveToolKind.ElementalTrail);

        if (keyboard.digit7Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.AddPassiveByKind, ActiveToolKind.Custom, PassiveToolKind.PerfectCircle);

        if (keyboard.digit8Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.RemovePassiveByKind, ActiveToolKind.Custom, PassiveToolKind.PerfectCircle);

        if (keyboard.digit9Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.AddPassiveByKind, ActiveToolKind.Custom, PassiveToolKind.BouncingProjectiles);

        if (keyboard.digit0Key.wasPressedThisFrame)
            AddCommand(cheatBuffer, PlayerPowerUpCheatCommandType.RemovePassiveByKind, ActiveToolKind.Custom, PassiveToolKind.BouncingProjectiles);
    }

    private static void TryEnqueueActiveKindCommand(DynamicBuffer<PlayerPowerUpCheatCommand> cheatBuffer,
                                                    bool keyPressed,
                                                    bool shiftPressed,
                                                    ActiveToolKind activeKind)
    {
        if (keyPressed == false)
            return;

        PlayerPowerUpCheatCommandType commandType = shiftPressed
            ? PlayerPowerUpCheatCommandType.SetSecondaryActiveKind
            : PlayerPowerUpCheatCommandType.SetPrimaryActiveKind;
        AddCommand(cheatBuffer, commandType, activeKind, PassiveToolKind.Custom);
    }

    private static void AddCommand(DynamicBuffer<PlayerPowerUpCheatCommand> cheatBuffer,
                                   PlayerPowerUpCheatCommandType commandType,
                                   ActiveToolKind activeKind,
                                   PassiveToolKind passiveKind)
    {
        cheatBuffer.Add(new PlayerPowerUpCheatCommand
        {
            CommandType = commandType,
            ActiveKind = activeKind,
            PassiveKind = passiveKind
        });
    }
    #endregion

    #endregion
}
