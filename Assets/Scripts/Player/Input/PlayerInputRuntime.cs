using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using Object = UnityEngine.Object;

#region Runtime
/// <summary>
/// Provides runtime management for player input actions, including initialization, shutdown, and access to gameplay
/// actions.
/// </summary>
public static class PlayerInputRuntime
{
    #region Fields
    private static InputActionAsset runtimeAsset;
    private static InputActionAsset sourceAsset;
    private static InputAction moveAction;
    private static InputAction lookAction;
    private static InputAction shootAction;
    private static InputAction powerUpPrimaryAction;
    private static InputAction powerUpSecondaryAction;
    private static bool lookActionUsesMousePointer;

    private static string moveActionId;
    private static string lookActionId;
    private static string shootActionId;
    private static string powerUpPrimaryActionId;
    private static string powerUpSecondaryActionId;
    #endregion

    #region Properties
    public static bool IsReady
    {
        get
        {
            return runtimeAsset != null;
        }
    }

    public static InputAction MoveAction
    {
        get
        {
            return moveAction;
        }
    }

    public static InputAction LookAction
    {
        get
        {
            return lookAction;
        }
    }

    public static InputAction ShootAction
    {
        get
        {
            return shootAction;
        }
    }

    public static InputAction PowerUpPrimaryAction
    {
        get
        {
            return powerUpPrimaryAction;
        }
    }

    public static InputAction PowerUpSecondaryAction
    {
        get
        {
            return powerUpSecondaryAction;
        }
    }

    public static bool LookActionUsesMousePointer
    {
        get
        {
            return lookActionUsesMousePointer;
        }
    }

    /// <summary>
    /// Resolves whether mouse-pointer look should drive aim in the current runtime context.
    /// </summary>
    /// <returns>True when look supports mouse pointer and no controller-like devices are connected.</returns>
    public static bool ShouldUseMousePointerLook()
    {
        if (lookActionUsesMousePointer == false)
            return false;

        return IsMouseKeyboardOnlyContext();
    }

    /// <summary>
    /// Reads look input while excluding pointer controls when the action mixes mouse and controller bindings.
    /// </summary>
    /// <param name="lookValue">Resolved look vector.</param>
    /// <returns>True when a look vector was resolved from the runtime look action.</returns>
    public static bool TryReadControllerLookVector(out Vector2 lookValue)
    {
        lookValue = Vector2.zero;

        if (lookAction == null)
            return false;

        if (lookAction.enabled == false)
            return false;

        if (lookActionUsesMousePointer == false)
        {
            lookValue = lookAction.ReadValue<Vector2>();
            return true;
        }

        ReadOnlyArray<InputControl> controls = lookAction.controls;
        bool foundNonPointerControl = false;
        float bestMagnitudeSquared = -1f;

        for (int controlIndex = 0; controlIndex < controls.Count; controlIndex++)
        {
            InputControl control = controls[controlIndex];

            if (control == null)
                continue;

            if (IsPointerDevice(control.device))
                continue;

            if (TryReadLookControlVector2(control, out Vector2 candidateLookValue) == false)
                continue;

            float candidateMagnitudeSquared = candidateLookValue.sqrMagnitude;

            if (foundNonPointerControl && candidateMagnitudeSquared <= bestMagnitudeSquared)
                continue;

            foundNonPointerControl = true;
            bestMagnitudeSquared = candidateMagnitudeSquared;
            lookValue = candidateLookValue;
        }

        if (foundNonPointerControl)
            return true;

        InputControl activeControl = lookAction.activeControl;

        if (activeControl == null)
            return false;

        if (IsPointerDevice(activeControl.device))
            return false;

        lookValue = lookAction.ReadValue<Vector2>();
        return true;
    }

    /// <summary>
    /// Determines whether the current runtime input environment exposes only mouse and keyboard without gamepad/joystick devices.
    /// </summary>
    /// <returns>True when mouse and keyboard are available and no controller-like device is connected.</returns>
    public static bool IsMouseKeyboardOnlyContext()
    {
        if (Mouse.current == null)
            return false;

        if (Keyboard.current == null)
            return false;

        if (Gamepad.all.Count > 0)
            return false;

        if (Joystick.all.Count > 0)
            return false;

        return true;
    }
    #endregion

    #region Methods

    #region Lifecycle
    public static void Initialize(InputActionAsset sourceAsset,
                                  string moveActionId,
                                  string lookActionId,
                                  string shootActionId,
                                  string powerUpPrimaryActionId,
                                  string powerUpSecondaryActionId)
    {
        if (sourceAsset == null)
        {
            Shutdown();
            return;
        }

        if (ShouldReuseRuntime(sourceAsset,
                               moveActionId,
                               lookActionId,
                               shootActionId,
                               powerUpPrimaryActionId,
                               powerUpSecondaryActionId))
            return;

        Shutdown();

        InputActionAsset instantiatedAsset = Object.Instantiate(sourceAsset);

        if (instantiatedAsset == null)
        {
            Shutdown();
            return;
        }

        instantiatedAsset.Enable();

        runtimeAsset = instantiatedAsset;
        PlayerInputRuntime.sourceAsset = sourceAsset;
        PlayerInputRuntime.moveActionId = moveActionId;
        PlayerInputRuntime.lookActionId = lookActionId;
        PlayerInputRuntime.shootActionId = shootActionId;
        PlayerInputRuntime.powerUpPrimaryActionId = powerUpPrimaryActionId;
        PlayerInputRuntime.powerUpSecondaryActionId = powerUpSecondaryActionId;

        moveAction = ResolveAction(instantiatedAsset, moveActionId, "Move");
        lookAction = ResolveAction(instantiatedAsset, lookActionId, "Look");
        shootAction = ResolveAction(instantiatedAsset, shootActionId, "Shoot");
        powerUpPrimaryAction = ResolveAction(instantiatedAsset, powerUpPrimaryActionId, "PowerUpPrimary");
        powerUpSecondaryAction = ResolveAction(instantiatedAsset, powerUpSecondaryActionId, "PowerUpSecondary");
        lookActionUsesMousePointer = ResolveLookActionUsesMousePointer(lookAction);

#if UNITY_EDITOR
        LogInitializationStatus(instantiatedAsset);
#endif
    }

    public static void Shutdown()
    {
        if (runtimeAsset != null)
        {
            runtimeAsset.Disable();
            Object.Destroy(runtimeAsset);
        }

        runtimeAsset = null;
        sourceAsset = null;
        moveAction = null;
        lookAction = null;
        shootAction = null;
        powerUpPrimaryAction = null;
        powerUpSecondaryAction = null;
        lookActionUsesMousePointer = false;

        moveActionId = null;
        lookActionId = null;
        shootActionId = null;
        powerUpPrimaryActionId = null;
        powerUpSecondaryActionId = null;
    }
    #endregion

    #region Helpers
    private static bool ShouldReuseRuntime(InputActionAsset sourceAsset,
                                           string moveActionId,
                                           string lookActionId,
                                           string shootActionId,
                                           string powerUpPrimaryActionId,
                                           string powerUpSecondaryActionId)
    {
        if (runtimeAsset == null)
            return false;

        if (PlayerInputRuntime.sourceAsset != sourceAsset)
            return false;

        if (string.Equals(PlayerInputRuntime.moveActionId, moveActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.lookActionId, lookActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.shootActionId, shootActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.powerUpPrimaryActionId, powerUpPrimaryActionId) == false)
            return false;

        if (string.Equals(PlayerInputRuntime.powerUpSecondaryActionId, powerUpSecondaryActionId) == false)
            return false;

        return true;
    }

    private static InputAction ResolveAction(InputActionAsset asset, string actionId, string fallbackName)
    {
        if (asset == null)
            return null;

        if (string.IsNullOrWhiteSpace(actionId) == false)
        {
            InputAction action = asset.FindAction(actionId, false);

            if (action != null)
                return action;
        }

        if (string.IsNullOrWhiteSpace(fallbackName))
            return null;

        return asset.FindAction(fallbackName, false);
    }

    /// <summary>
    /// Checks whether the resolved look action includes at least one mouse binding path.
    /// </summary>
    /// <param name="action">Resolved look action from the runtime input asset.</param>
    /// <returns>True when at least one binding path references the Mouse device.</returns>
    private static bool ResolveLookActionUsesMousePointer(InputAction action)
    {
        if (action == null)
            return false;

        for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
        {
            InputBinding binding = action.bindings[bindingIndex];
            string bindingPath = binding.effectivePath;

            if (string.IsNullOrWhiteSpace(bindingPath))
                bindingPath = binding.path;

            if (string.IsNullOrWhiteSpace(bindingPath))
                continue;

            if (bindingPath.IndexOf("<Mouse>", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool IsPointerDevice(InputDevice device)
    {
        if (device == null)
            return false;

        if (device is Pointer)
            return true;

        return false;
    }

    private static bool TryReadLookControlVector2(InputControl control, out Vector2 lookValue)
    {
        lookValue = Vector2.zero;

        Vector2Control vector2Control = control as Vector2Control;

        if (vector2Control == null)
            return false;

        lookValue = vector2Control.ReadValue();
        return true;
    }
    #endregion

#if UNITY_EDITOR
    #region Editor Debug
    private static void LogInitializationStatus(InputActionAsset asset)
    {
        if (asset == null)
            return;

        string message = string.Format("[PlayerInputRuntime] Initialized '{0}'. Move: {1} | Look: {2} | Shoot: {3} | PowerUpPrimary: {4} | PowerUpSecondary: {5} | MousePointerLook: {6}",
                                       asset.name,
                                       BuildActionStatus(moveAction),
                                       BuildActionStatus(lookAction),
                                       BuildActionStatus(shootAction),
                                       BuildActionStatus(powerUpPrimaryAction),
                                       BuildActionStatus(powerUpSecondaryAction),
                                       lookActionUsesMousePointer);
        Debug.Log(message, asset);
    }

    private static string BuildActionStatus(InputAction action)
    {
        if (action == null)
            return "MISSING";

        string mapName = action.actionMap != null ? action.actionMap.name : "none";

        if (action.enabled == false)
            return string.Format("FOUND (disabled) map '{0}'", mapName);

        return string.Format("FOUND (enabled) map '{0}'", mapName);
    }
    #endregion
#endif

    #endregion
}
#endregion
