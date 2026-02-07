using UnityEngine;
using UnityEngine.InputSystem;

#region Runtime
/// <summary>
/// Provides runtime management for player input actions, including initialization, shutdown, and access to move and
/// look actions.
/// </summary>
public static class PlayerInputRuntime
{
    #region Fields
    private static InputActionAsset runtimeAsset; // Active runtime input asset instance.
    private static InputActionAsset sourceAsset; // Source asset used to build the runtime instance.
    private static InputAction moveAction; // Cached move action.
    private static InputAction lookAction; // Cached look action.
    private static InputAction shootAction; // Cached shoot action.
    private static string moveActionId; // Cached move action id.
    private static string lookActionId; // Cached look action id.
    private static string shootActionId; // Cached shoot action id.
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
    #endregion

    #region Lifecycle
    /// <summary>
    /// Initializes the input system with the specified input action asset and action identifiers.
    /// </summary>
    /// <param name="sourceAsset">The input action asset to use as the source.</param>
    /// <param name="moveActionId">The identifier for the move action.</param>
    /// <param name="lookActionId">The identifier for the look action.</param>
    /// <param name="shootActionId">The identifier for the shoot action.</param>
    public static void Initialize(InputActionAsset sourceAsset, string moveActionId, string lookActionId, string shootActionId)
    {
        if (sourceAsset == null)
        {
            Shutdown();
            return;
        }

        if (ShouldReuseRuntime(sourceAsset, moveActionId, lookActionId, shootActionId))
            return;

        Shutdown();

        InputActionAsset runtimeAsset = Object.Instantiate(sourceAsset);

        if (runtimeAsset == null)
        {
            Shutdown();
            return;
        }

        runtimeAsset.Enable();

        PlayerInputRuntime.runtimeAsset = runtimeAsset;
        PlayerInputRuntime.sourceAsset = sourceAsset;
        PlayerInputRuntime.moveActionId = moveActionId;
        PlayerInputRuntime.lookActionId = lookActionId;
        PlayerInputRuntime.shootActionId = shootActionId;
        moveAction = ResolveAction(runtimeAsset, moveActionId, "Move");
        lookAction = ResolveAction(runtimeAsset, lookActionId, "Look");
        shootAction = ResolveAction(runtimeAsset, shootActionId, "Shoot");

#if UNITY_EDITOR
        LogInitializationStatus(runtimeAsset, moveAction, lookAction, shootAction);
#endif
    }

    /// <summary>
    /// Releases and cleans up all runtime input assets and related resources.
    /// </summary>
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
        moveActionId = null;
        lookActionId = null;
        shootActionId = null;
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Determines whether the existing runtime should be reused based on the provided input action asset and action
    /// IDs.
    /// </summary>
    /// <param name="sourceAsset">The input action asset to compare against the current runtime.</param>
    /// <param name="moveActionId">The identifier for the move action to compare.</param>
    /// <param name="lookActionId">The identifier for the look action to compare.</param>
    /// <param name="shootActionId">The identifier for the shoot action to compare.</param>
    /// <returns>True if the runtime can be reused with the specified asset and action IDs; otherwise, false.</returns>
    private static bool ShouldReuseRuntime(InputActionAsset sourceAsset, string moveActionId, string lookActionId, string shootActionId)
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

        return true;
    }

    /// <summary>
    /// Finds and returns an InputAction from the given asset by action ID, or by fallback name if not found.
    /// </summary>
    /// <param name="asset">The InputActionAsset to search for the action.</param>
    /// <param name="actionId">The unique identifier of the action to find.</param>
    /// <param name="fallbackName">The fallback name to use if the action ID is not found.</param>
    /// <returns>The resolved InputAction if found; otherwise, null.</returns>
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

#if UNITY_EDITOR
    #region Editor Debug
    private static void LogInitializationStatus(InputActionAsset asset, InputAction moveAction, InputAction lookAction, InputAction shootAction)
    {
        if (asset == null)
            return;

        string assetName = asset.name;
        string moveStatus = BuildActionStatus(moveAction);
        string lookStatus = BuildActionStatus(lookAction);
        string shootStatus = BuildActionStatus(shootAction);
        string message = string.Format("[PlayerInputRuntime] Initialized asset '{0}'. Move: {1} | Look: {2} | Shoot: {3}", assetName, moveStatus, lookStatus, shootStatus);
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
