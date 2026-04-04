using System;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Centralizes direct right-click opening for management-tool color editing.
/// /params None.
/// /returns None.
/// </summary>
internal static class ManagementToolColorTriggerUtility
{
    #region Constants
    private const int RightMouseButton = 1;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Opens the provided action on right mouse down and suppresses competing default UI reactions.
    /// /params evt Mouse event emitted by the recolorable tool element.
    /// /params openAction Action that opens the dedicated color inspector.
    /// /returns None.
    /// </summary>
    public static void HandleRightMouseDown(MouseDownEvent evt, Action openAction)
    {
        if (evt == null)
            return;

        if (openAction == null)
            return;

        if (evt.button != RightMouseButton)
            return;

        evt.StopImmediatePropagation();
        evt.PreventDefault();

        EditorApplication.delayCall += () =>
        {
            openAction.Invoke();
        };
    }
    #endregion

    #endregion
}
