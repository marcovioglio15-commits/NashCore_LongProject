using Unity.Mathematics;

/// <summary>
/// Centralizes runtime loadout mutations for active-slot state initialization, replacement, and swapping.
/// </summary>
internal static class PlayerPowerUpLoadoutRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates the initial runtime state for one player power-up loadout.
    /// </summary>
    /// <param name="powerUpsConfig">Current runtime power-up config used to seed slot values.</param>
    /// <param name="currentKillCount">Current global kill count tracked by power-up charge systems.</param>
    /// <returns>Initialized runtime state ready to add as PlayerPowerUpsState.</returns>
    public static PlayerPowerUpsState CreateInitialState(in PlayerPowerUpsConfig powerUpsConfig, uint currentKillCount)
    {
        PlayerPowerUpsState powerUpsState = new PlayerPowerUpsState
        {
            LastObservedGlobalKillCount = currentKillCount,
            LastValidMovementDirection = float3.zero
        };
        ResetRuntimeState(ref powerUpsState, in powerUpsConfig);
        return powerUpsState;
    }

    /// <summary>
    /// Resets transient runtime slot state to match the currently equipped loadout.
    /// </summary>
    /// <param name="powerUpsState">Runtime power-up state to reset.</param>
    /// <param name="powerUpsConfig">Current runtime config used to initialize slot values.</param>
    public static void ResetRuntimeState(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpsConfig powerUpsConfig)
    {
        powerUpsState.PrimaryEnergy = math.max(0f, powerUpsConfig.PrimarySlot.MaximumEnergy);
        powerUpsState.SecondaryEnergy = math.max(0f, powerUpsConfig.SecondarySlot.MaximumEnergy);
        powerUpsState.PrimaryCooldownRemaining = 0f;
        powerUpsState.SecondaryCooldownRemaining = 0f;
        powerUpsState.PrimaryCharge = 0f;
        powerUpsState.SecondaryCharge = 0f;
        powerUpsState.PrimaryIsCharging = 0;
        powerUpsState.SecondaryIsCharging = 0;
        powerUpsState.IsShootingSuppressed = 0;
        powerUpsState.PreviousPrimaryPressed = 0;
        powerUpsState.PreviousSecondaryPressed = 0;
        powerUpsState.PreviousSwapSlotsPressed = 0;
        AssignInitialEquipOrders(ref powerUpsState, in powerUpsConfig);
    }

    /// <summary>
    /// Equips a newly acquired active power-up into the oldest currently occupied slot.
    /// </summary>
    /// <param name="activeSlotConfig">Unlocked active-slot payload to equip.</param>
    /// <param name="powerUpsConfig">Runtime loadout config to mutate.</param>
    /// <param name="powerUpsState">Runtime slot state to update.</param>
    /// <param name="targetSlotIndex">Resolved slot index receiving the new active power-up.</param>
    /// <returns>True when the active power-up was equipped; otherwise false.</returns>
    public static bool TryEquipIntoOldestSlot(in PlayerPowerUpSlotConfig activeSlotConfig,
                                              ref PlayerPowerUpsConfig powerUpsConfig,
                                              ref PlayerPowerUpsState powerUpsState,
                                              out int targetSlotIndex)
    {
        targetSlotIndex = -1;

        if (activeSlotConfig.IsDefined == 0)
            return false;

        targetSlotIndex = ResolveOldestActiveSlotIndex(in powerUpsConfig, in powerUpsState);

        switch (targetSlotIndex)
        {
            case 0:
                powerUpsConfig.PrimarySlot = activeSlotConfig;
                ResetPrimaryRuntimeState(ref powerUpsState, in activeSlotConfig);
                powerUpsState.PrimaryEquipOrder = ConsumeNextEquipOrder(ref powerUpsState);
                return true;
            case 1:
                powerUpsConfig.SecondarySlot = activeSlotConfig;
                ResetSecondaryRuntimeState(ref powerUpsState, in activeSlotConfig);
                powerUpsState.SecondaryEquipOrder = ConsumeNextEquipOrder(ref powerUpsState);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Swaps active-slot configs and the runtime data that belongs to each equipped power-up.
    /// </summary>
    /// <param name="powerUpsConfig">Runtime active-slot config to mutate.</param>
    /// <param name="powerUpsState">Runtime active-slot state to mutate.</param>
    public static void SwapActiveSlotRuntimeData(ref PlayerPowerUpsConfig powerUpsConfig, ref PlayerPowerUpsState powerUpsState)
    {
        SwapValues(ref powerUpsConfig.PrimarySlot, ref powerUpsConfig.SecondarySlot);
        SwapValues(ref powerUpsState.PrimaryEnergy, ref powerUpsState.SecondaryEnergy);
        SwapValues(ref powerUpsState.PrimaryCooldownRemaining, ref powerUpsState.SecondaryCooldownRemaining);
        SwapValues(ref powerUpsState.PrimaryEquipOrder, ref powerUpsState.SecondaryEquipOrder);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Seeds deterministic age ordering for the active slots currently present in the runtime loadout.
    /// </summary>
    /// <param name="powerUpsState">Runtime state receiving the initial order markers.</param>
    /// <param name="powerUpsConfig">Runtime config inspected for defined slots.</param>
    private static void AssignInitialEquipOrders(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpsConfig powerUpsConfig)
    {
        int nextEquipOrder = 1;
        powerUpsState.PrimaryEquipOrder = 0;
        powerUpsState.SecondaryEquipOrder = 0;

        if (powerUpsConfig.PrimarySlot.IsDefined != 0)
        {
            powerUpsState.PrimaryEquipOrder = nextEquipOrder;
            nextEquipOrder += 1;
        }

        if (powerUpsConfig.SecondarySlot.IsDefined != 0)
        {
            powerUpsState.SecondaryEquipOrder = nextEquipOrder;
            nextEquipOrder += 1;
        }

        powerUpsState.NextEquipOrder = nextEquipOrder;
    }

    /// <summary>
    /// Resolves the oldest currently equipped active slot.
    /// </summary>
    /// <param name="powerUpsConfig">Current runtime loadout config.</param>
    /// <param name="powerUpsState">Current runtime slot state containing equip-order metadata.</param>
    /// <returns>0 for primary or 1 for secondary.</returns>
    private static int ResolveOldestActiveSlotIndex(in PlayerPowerUpsConfig powerUpsConfig, in PlayerPowerUpsState powerUpsState)
    {
        bool hasPrimary = powerUpsConfig.PrimarySlot.IsDefined != 0;
        bool hasSecondary = powerUpsConfig.SecondarySlot.IsDefined != 0;

        if (!hasPrimary && !hasSecondary)
            return 0;

        if (hasPrimary && !hasSecondary)
            return 0;

        if (!hasPrimary)
            return 1;

        int primaryOrder = powerUpsState.PrimaryEquipOrder > 0 ? powerUpsState.PrimaryEquipOrder : 1;
        int secondaryOrder = powerUpsState.SecondaryEquipOrder > 0 ? powerUpsState.SecondaryEquipOrder : 2;
        return primaryOrder <= secondaryOrder ? 0 : 1;
    }

    /// <summary>
    /// Resets the runtime resource state owned by the primary active slot.
    /// </summary>
    /// <param name="powerUpsState">Runtime state to mutate.</param>
    /// <param name="slotConfig">Slot config that now owns the primary slot.</param>
    private static void ResetPrimaryRuntimeState(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpSlotConfig slotConfig)
    {
        powerUpsState.PrimaryEnergy = math.max(0f, slotConfig.MaximumEnergy);
        powerUpsState.PrimaryCooldownRemaining = 0f;
        powerUpsState.PrimaryCharge = 0f;
        powerUpsState.PrimaryIsCharging = 0;
    }

    /// <summary>
    /// Resets the runtime resource state owned by the secondary active slot.
    /// </summary>
    /// <param name="powerUpsState">Runtime state to mutate.</param>
    /// <param name="slotConfig">Slot config that now owns the secondary slot.</param>
    private static void ResetSecondaryRuntimeState(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpSlotConfig slotConfig)
    {
        powerUpsState.SecondaryEnergy = math.max(0f, slotConfig.MaximumEnergy);
        powerUpsState.SecondaryCooldownRemaining = 0f;
        powerUpsState.SecondaryCharge = 0f;
        powerUpsState.SecondaryIsCharging = 0;
    }

    /// <summary>
    /// Consumes the next monotonically increasing equip-order value.
    /// </summary>
    /// <param name="powerUpsState">Runtime state that stores the next available order value.</param>
    /// <returns>Equip-order value assigned to the newly equipped active power-up.</returns>
    private static int ConsumeNextEquipOrder(ref PlayerPowerUpsState powerUpsState)
    {
        int nextEquipOrder = powerUpsState.NextEquipOrder > 0 ? powerUpsState.NextEquipOrder : 1;

        if (nextEquipOrder < int.MaxValue)
            powerUpsState.NextEquipOrder = nextEquipOrder + 1;

        return nextEquipOrder;
    }

    /// <summary>
    /// Swaps two values in place.
    /// </summary>
    /// <typeparam name="T">Value type being exchanged.</typeparam>
    /// <param name="leftValue">First value to swap.</param>
    /// <param name="rightValue">Second value to swap.</param>
    private static void SwapValues<T>(ref T leftValue, ref T rightValue)
    {
        T temporaryValue = leftValue;
        leftValue = rightValue;
        rightValue = temporaryValue;
    }
    #endregion

    #endregion
}
