using UnityEngine;

/// <summary>
/// Stores transient Player Management Tool selection context shared across editor panels.
/// </summary>
public static class PlayerManagementSelectionContext
{
    #region Fields
    private static PlayerMasterPreset activeMasterPreset;
    #endregion

    #region Properties
    /// <summary>
    /// Gets the currently selected master preset in Player Management Tool.
    /// </summary>
    public static PlayerMasterPreset ActiveMasterPreset
    {
        get
        {
            return activeMasterPreset;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Updates the active master preset context used by cross-panel editor helpers.
    /// </summary>
    /// <param name="masterPreset">Master preset currently selected in the master panel.</param>
    /// <returns>Void.</returns>
    public static void SetActiveMasterPreset(PlayerMasterPreset masterPreset)
    {
        activeMasterPreset = masterPreset;
    }
    #endregion

    #endregion
}
