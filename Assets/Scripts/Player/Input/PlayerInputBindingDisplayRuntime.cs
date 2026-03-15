using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// Tracks the most recently used input family and resolves context-aware binding labels for player prompts.
/// /params none.
/// /returns none.
/// </summary>
public static class PlayerInputBindingDisplayRuntime
{
    #region Fields
    private const string KeyboardMouseBindingGroupName = "Keyboard&Mouse";
    private const string GamepadBindingGroupName = "Gamepad";
    private static BindingDisplayDeviceFamily preferredBindingDisplayDeviceFamily;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Initializes recent-device tracking for the current runtime input actions.
    /// /params none.
    /// /returns void.
    /// </summary>
    public static void Initialize()
    {
        preferredBindingDisplayDeviceFamily = ResolveCurrentBindingDisplayDeviceFamily();
        RegisterActionActivityCallback(PlayerInputRuntime.MoveAction);
        RegisterActionActivityCallback(PlayerInputRuntime.LookAction);
        RegisterActionActivityCallback(PlayerInputRuntime.ShootAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpPrimaryAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpSecondaryAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpSwapSlotsAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerInteractAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerReplacePrimaryAction);
        RegisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerReplaceSecondaryAction);
        RegisterActionActivityCallback(PlayerInputRuntime.UINavigateAction);
        RegisterActionActivityCallback(PlayerInputRuntime.UISubmitAction);
        RegisterActionActivityCallback(PlayerInputRuntime.UICancelAction);
    }

    /// <summary>
    /// Unregisters recent-device tracking callbacks and clears cached state.
    /// /params none.
    /// /returns void.
    /// </summary>
    public static void Shutdown()
    {
        UnregisterActionActivityCallback(PlayerInputRuntime.MoveAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.LookAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.ShootAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpPrimaryAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpSecondaryAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpSwapSlotsAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerInteractAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerReplacePrimaryAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.PowerUpContainerReplaceSecondaryAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.UINavigateAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.UISubmitAction);
        UnregisterActionActivityCallback(PlayerInputRuntime.UICancelAction);
        preferredBindingDisplayDeviceFamily = BindingDisplayDeviceFamily.Unknown;
    }

    /// <summary>
    /// Resolves one binding display string that matches the currently active input device family whenever possible.
    /// /params action: Input action whose binding label must be displayed.
    /// /params fallback: Fallback string used when no matching binding can be resolved.
    /// /returns Context-aware binding label for prompts and HUD text.
    /// </summary>
    public static string ResolveBindingDisplayString(InputAction action, string fallback)
    {
        if (action == null)
            return fallback;

        BindingDisplayDeviceFamily currentDeviceFamily = ResolveCurrentBindingDisplayDeviceFamily();

        if (TryResolveBindingDisplayString(action, currentDeviceFamily, out string bindingDisplayString))
            return bindingDisplayString;

        if (currentDeviceFamily != BindingDisplayDeviceFamily.KeyboardMouse &&
            TryResolveBindingDisplayString(action, BindingDisplayDeviceFamily.KeyboardMouse, out bindingDisplayString))
        {
            return bindingDisplayString;
        }

        if (currentDeviceFamily != BindingDisplayDeviceFamily.Gamepad &&
            TryResolveBindingDisplayString(action, BindingDisplayDeviceFamily.Gamepad, out bindingDisplayString))
        {
            return bindingDisplayString;
        }

        bindingDisplayString = action.GetBindingDisplayString();

        if (string.IsNullOrWhiteSpace(bindingDisplayString))
            return fallback;

        return bindingDisplayString;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Registers one input action for recent-device tracking.
    /// /params action: Input action subscribed to activity callbacks.
    /// /returns void.
    /// </summary>
    private static void RegisterActionActivityCallback(InputAction action)
    {
        if (action == null)
            return;

        action.started += HandleActionActivity;
        action.performed += HandleActionActivity;
    }

    /// <summary>
    /// Unregisters one input action from recent-device tracking.
    /// /params action: Input action unsubscribed from activity callbacks.
    /// /returns void.
    /// </summary>
    private static void UnregisterActionActivityCallback(InputAction action)
    {
        if (action == null)
            return;

        action.started -= HandleActionActivity;
        action.performed -= HandleActionActivity;
    }

    /// <summary>
    /// Records the device family that produced the most recent meaningful input activity.
    /// /params context: Input callback context raised by the active runtime action.
    /// /returns void.
    /// </summary>
    private static void HandleActionActivity(InputAction.CallbackContext context)
    {
        InputControl control = context.control;

        if (control == null)
            return;

        BindingDisplayDeviceFamily deviceFamily = ResolveBindingDisplayDeviceFamily(control.device);

        if (deviceFamily == BindingDisplayDeviceFamily.Unknown)
            return;

        preferredBindingDisplayDeviceFamily = deviceFamily;
    }

    /// <summary>
    /// Resolves the currently preferred device family used to select prompt binding labels.
    /// /params none.
    /// /returns Preferred device family when available; otherwise Unknown.
    /// </summary>
    private static BindingDisplayDeviceFamily ResolveCurrentBindingDisplayDeviceFamily()
    {
        if (preferredBindingDisplayDeviceFamily == BindingDisplayDeviceFamily.KeyboardMouse && HasKeyboardMouseDevices())
            return BindingDisplayDeviceFamily.KeyboardMouse;

        if (preferredBindingDisplayDeviceFamily == BindingDisplayDeviceFamily.Gamepad && HasControllerDevices())
            return BindingDisplayDeviceFamily.Gamepad;

        if (HasKeyboardMouseDevices())
            return BindingDisplayDeviceFamily.KeyboardMouse;

        if (HasControllerDevices())
            return BindingDisplayDeviceFamily.Gamepad;

        return BindingDisplayDeviceFamily.Unknown;
    }

    /// <summary>
    /// Returns whether at least one keyboard or mouse device is currently available.
    /// /params none.
    /// /returns True when keyboard or mouse devices are present.
    /// </summary>
    private static bool HasKeyboardMouseDevices()
    {
        return Keyboard.current != null || Mouse.current != null;
    }

    /// <summary>
    /// Returns whether at least one controller-like device is currently available.
    /// /params none.
    /// /returns True when gamepad or joystick devices are present.
    /// </summary>
    private static bool HasControllerDevices()
    {
        if (Gamepad.all.Count > 0)
            return true;

        return Joystick.all.Count > 0;
    }

    /// <summary>
    /// Resolves the binding display string that best matches the requested device family.
    /// /params action: Input action whose bindings are inspected.
    /// /params deviceFamily: Preferred device family for the displayed binding.
    /// /params bindingDisplayString: Resolved display string when found.
    /// /returns True when a matching binding display string was found.
    /// </summary>
    private static bool TryResolveBindingDisplayString(InputAction action,
                                                       BindingDisplayDeviceFamily deviceFamily,
                                                       out string bindingDisplayString)
    {
        bindingDisplayString = null;

        if (action == null)
            return false;

        ReadOnlyArray<InputBinding> bindings = action.bindings;

        for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
        {
            InputBinding binding = bindings[bindingIndex];

            if (binding.isPartOfComposite)
                continue;

            if (!BindingMatchesDeviceFamily(action, bindingIndex, deviceFamily))
                continue;

            string displayString = action.GetBindingDisplayString(bindingIndex);

            if (string.IsNullOrWhiteSpace(displayString))
                continue;

            bindingDisplayString = displayString;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether one action binding belongs to the requested device family, including composite roots.
    /// /params action: Input action that owns the inspected binding.
    /// /params bindingIndex: Binding index inspected inside the action.
    /// /params deviceFamily: Device family used as the filter.
    /// /returns True when the binding belongs to the requested family.
    /// </summary>
    private static bool BindingMatchesDeviceFamily(InputAction action, int bindingIndex, BindingDisplayDeviceFamily deviceFamily)
    {
        if (deviceFamily == BindingDisplayDeviceFamily.Unknown)
            return true;

        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            return false;

        InputBinding binding = action.bindings[bindingIndex];

        if (binding.isComposite)
            return CompositeMatchesDeviceFamily(action, bindingIndex, deviceFamily);

        return NonCompositeBindingMatchesDeviceFamily(binding, deviceFamily);
    }

    /// <summary>
    /// Returns whether at least one part of a composite binding belongs to the requested device family.
    /// /params action: Input action that owns the composite binding.
    /// /params compositeBindingIndex: Composite root binding index.
    /// /params deviceFamily: Device family used as the filter.
    /// /returns True when one composite part belongs to the requested family.
    /// </summary>
    private static bool CompositeMatchesDeviceFamily(InputAction action,
                                                     int compositeBindingIndex,
                                                     BindingDisplayDeviceFamily deviceFamily)
    {
        int bindingCount = action.bindings.Count;

        for (int partIndex = compositeBindingIndex + 1; partIndex < bindingCount; partIndex++)
        {
            InputBinding partBinding = action.bindings[partIndex];

            if (!partBinding.isPartOfComposite)
                break;

            if (NonCompositeBindingMatchesDeviceFamily(partBinding, deviceFamily))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether one non-composite binding belongs to the requested device family.
    /// /params binding: Binding inspected for group and path matching.
    /// /params deviceFamily: Device family used as the filter.
    /// /returns True when the binding belongs to the requested family.
    /// </summary>
    private static bool NonCompositeBindingMatchesDeviceFamily(InputBinding binding, BindingDisplayDeviceFamily deviceFamily)
    {
        if (GroupsMatchDeviceFamily(binding.groups, deviceFamily))
            return true;

        string bindingPath = string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.path : binding.effectivePath;
        return PathMatchesDeviceFamily(bindingPath, deviceFamily);
    }

    /// <summary>
    /// Returns whether one binding-group string references the requested device family.
    /// /params groups: Binding groups string stored on the binding.
    /// /params deviceFamily: Device family used as the filter.
    /// /returns True when the groups string contains the requested family.
    /// </summary>
    private static bool GroupsMatchDeviceFamily(string groups, BindingDisplayDeviceFamily deviceFamily)
    {
        if (string.IsNullOrWhiteSpace(groups))
            return false;

        switch (deviceFamily)
        {
            case BindingDisplayDeviceFamily.KeyboardMouse:
                return groups.IndexOf(KeyboardMouseBindingGroupName, StringComparison.OrdinalIgnoreCase) >= 0;
            case BindingDisplayDeviceFamily.Gamepad:
                return groups.IndexOf(GamepadBindingGroupName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       groups.IndexOf("Joystick", StringComparison.OrdinalIgnoreCase) >= 0;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether one binding path references the requested device family.
    /// /params bindingPath: Effective or authored binding path inspected for device layouts.
    /// /params deviceFamily: Device family used as the filter.
    /// /returns True when the path references the requested family.
    /// </summary>
    private static bool PathMatchesDeviceFamily(string bindingPath, BindingDisplayDeviceFamily deviceFamily)
    {
        if (string.IsNullOrWhiteSpace(bindingPath))
            return false;

        switch (deviceFamily)
        {
            case BindingDisplayDeviceFamily.KeyboardMouse:
                return bindingPath.IndexOf("<Keyboard>", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       bindingPath.IndexOf("<Mouse>", StringComparison.OrdinalIgnoreCase) >= 0;
            case BindingDisplayDeviceFamily.Gamepad:
                return bindingPath.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       bindingPath.IndexOf("Joystick", StringComparison.OrdinalIgnoreCase) >= 0;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves the prompt-binding device family represented by the provided runtime device.
    /// /params device: Runtime input device inspected for family classification.
    /// /returns Resolved device family used by prompt binding selection.
    /// </summary>
    private static BindingDisplayDeviceFamily ResolveBindingDisplayDeviceFamily(InputDevice device)
    {
        if (device == null)
            return BindingDisplayDeviceFamily.Unknown;

        if (device is Keyboard || device is Mouse)
            return BindingDisplayDeviceFamily.KeyboardMouse;

        if (device is Gamepad || device is Joystick)
            return BindingDisplayDeviceFamily.Gamepad;

        return BindingDisplayDeviceFamily.Unknown;
    }
    #endregion

    #region Nested Types
    /// <summary>
    /// Identifies the high-level device family used to select context-aware prompt binding labels.
    /// /params none.
    /// /returns none.
    /// </summary>
    private enum BindingDisplayDeviceFamily : byte
    {
        Unknown = 0,
        KeyboardMouse = 1,
        Gamepad = 2
    }
    #endregion

    #endregion
}
