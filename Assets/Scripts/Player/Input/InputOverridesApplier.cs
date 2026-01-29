using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public static class InputOverridesApplier
{
    #region Public Methods
    /// <summary>
    /// Applies a list of input binding overrides to the given input action asset.
    /// </summary>
    /// <param name="asset"></param>
    /// <param name="overrides"></param>
    public static void ApplyOverrides(InputActionAsset asset, IReadOnlyList<InputBindingOverride> overrides)
    {
        if (asset == null || overrides == null)
            return;

        for (int i = 0; i < overrides.Count; i++)
        {
            InputBindingOverride bindingOverride = overrides[i];

            if (string.IsNullOrWhiteSpace(bindingOverride.ActionId))
                continue;

            InputAction action = asset.FindAction(bindingOverride.ActionId, false);

            if (action == null)
                continue;

            Guid bindingGuid;

            if (Guid.TryParse(bindingOverride.BindingId, out bindingGuid) == false)
                continue;

            int bindingIndex = FindBindingIndex(action, bindingGuid);

            if (bindingIndex < 0)
                continue;

            InputBinding binding = action.bindings[bindingIndex];
            binding.overridePath = bindingOverride.OverridePath;
            binding.overrideInteractions = bindingOverride.OverrideInteractions;
            binding.overrideProcessors = bindingOverride.OverrideProcessors;
            action.ApplyBindingOverride(bindingIndex, binding);
        }
    }
    #endregion

    #region Private Methods
    private static int FindBindingIndex(InputAction action, Guid bindingGuid)
    {
        if (action == null)
            return -1;

        IReadOnlyList<InputBinding> bindings = action.bindings;

        for (int i = 0; i < bindings.Count; i++)
        {
            if (bindings[i].id == bindingGuid)
                return i;
        }

        return -1;
    }
    #endregion
}
