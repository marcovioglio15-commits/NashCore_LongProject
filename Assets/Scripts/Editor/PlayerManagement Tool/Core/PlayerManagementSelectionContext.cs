/// <summary>
/// Stores transient Player Management Tool selection context shared across editor panels.
/// </summary>
public static class PlayerManagementSelectionContext
{
    #region Fields
    private static PlayerMasterPreset activeMasterPreset;
    private static PlayerPowerUpsPreset activePowerUpsPreset;
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

    /// <summary>
    /// Gets the currently selected power-ups preset in Player Management Tool.
    /// </summary>
    public static PlayerPowerUpsPreset ActivePowerUpsPreset
    {
        get
        {
            return activePowerUpsPreset;
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

    /// <summary>
    /// Updates the active power-ups preset context used by progression tier dropdown helpers.
    /// </summary>
    /// <param name="powerUpsPreset">Power-ups preset currently selected in the dedicated power-ups panel.</param>
    /// <returns>Void.</returns>
    public static void SetActivePowerUpsPreset(PlayerPowerUpsPreset powerUpsPreset)
    {
        activePowerUpsPreset = powerUpsPreset;
    }
    #endregion

    #endregion
}
