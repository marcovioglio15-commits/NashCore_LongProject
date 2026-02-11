using UnityEngine;
using UnityEngine.InputSystem;

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
    #endregion

#if UNITY_EDITOR
    #region Editor Debug
    private static void LogInitializationStatus(InputActionAsset asset)
    {
        if (asset == null)
            return;

        string message = string.Format("[PlayerInputRuntime] Initialized '{0}'. Move: {1} | Look: {2} | Shoot: {3} | PowerUpPrimary: {4} | PowerUpSecondary: {5}",
                                       asset.name,
                                       BuildActionStatus(moveAction),
                                       BuildActionStatus(lookAction),
                                       BuildActionStatus(shootAction),
                                       BuildActionStatus(powerUpPrimaryAction),
                                       BuildActionStatus(powerUpSecondaryAction));
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
