using System;
using UnityEngine;

#region Module Payloads
[Serializable]
public sealed class PowerUpHoldChargeModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Charge amount required to release the charged effect.")]
    [SerializeField] private float requiredCharge = 500f;

    [Tooltip("Upper cap for accumulated charge while the trigger is held.")]
    [SerializeField] private float maximumCharge = 500f;

    [Tooltip("Charge gained per second while the trigger is held.")]
    [SerializeField] private float chargeRatePerSecond = 125f;
    #endregion

    #endregion

    #region Properties
    public float RequiredCharge
    {
        get
        {
            return requiredCharge;
        }
    }

    public float MaximumCharge
    {
        get
        {
            return maximumCharge;
        }
    }

    public float ChargeRatePerSecond
    {
        get
        {
            return chargeRatePerSecond;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float requiredChargeValue, float maximumChargeValue, float chargeRatePerSecondValue)
    {
        requiredCharge = requiredChargeValue;
        maximumCharge = maximumChargeValue;
        chargeRatePerSecond = chargeRatePerSecondValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (requiredCharge < 0f)
            requiredCharge = 0f;

        if (maximumCharge < requiredCharge)
            maximumCharge = requiredCharge;

        if (chargeRatePerSecond < 0f)
            chargeRatePerSecond = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpTriggerEventModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Runtime event that triggers execution for modules bound to this trigger.")]
    [SerializeField] private PowerUpTriggerEventType eventType = PowerUpTriggerEventType.OnEnemyKilled;
    #endregion

    #endregion

    #region Properties
    public PowerUpTriggerEventType EventType
    {
        get
        {
            return eventType;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(PowerUpTriggerEventType eventTypeValue)
    {
        eventType = eventTypeValue;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpResourceGateModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Resource used to pay activation.")]
    [SerializeField] private PowerUpResourceType activationResource = PowerUpResourceType.Energy;

    [Tooltip("Resource used for maintenance while active; currently reserved for future toggle tools.")]
    [SerializeField] private PowerUpResourceType maintenanceResource = PowerUpResourceType.Energy;

    [Tooltip("Maximum energy capacity for this active power up.")]
    [SerializeField] private float maximumEnergy = 100f;

    [Tooltip("Resource amount consumed on activation.")]
    [SerializeField] private float activationCost = 100f;

    [Tooltip("Maintenance cost consumed per second when toggled on.")]
    [SerializeField] private float maintenanceCostPerSecond;

    [Tooltip("Minimum energy percentage required to activate. 0 disables this gate.")]
    [SerializeField] private float minimumActivationEnergyPercent;

    [Tooltip("Charge source used to refill energy over time/events.")]
    [SerializeField] private PowerUpChargeType chargeType = PowerUpChargeType.Time;

    [Tooltip("Recharge amount gained per trigger unit of the selected charge source.")]
    [SerializeField] private float chargePerTrigger = 100f;

    [Tooltip("Cooldown in seconds applied after a successful activation.")]
    [SerializeField] private float cooldownSeconds = 1f;
    #endregion

    #endregion

    #region Properties
    public PowerUpResourceType ActivationResource
    {
        get
        {
            return activationResource;
        }
    }

    public PowerUpResourceType MaintenanceResource
    {
        get
        {
            return maintenanceResource;
        }
    }

    public float MaximumEnergy
    {
        get
        {
            return maximumEnergy;
        }
    }

    public float ActivationCost
    {
        get
        {
            return activationCost;
        }
    }

    public float MaintenanceCostPerSecond
    {
        get
        {
            return maintenanceCostPerSecond;
        }
    }

    public float MinimumActivationEnergyPercent
    {
        get
        {
            return minimumActivationEnergyPercent;
        }
    }

    public PowerUpChargeType ChargeType
    {
        get
        {
            return chargeType;
        }
    }

    public float ChargePerTrigger
    {
        get
        {
            return chargePerTrigger;
        }
    }

    public float CooldownSeconds
    {
        get
        {
            return cooldownSeconds;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(PowerUpResourceType activationResourceValue,
                          PowerUpResourceType maintenanceResourceValue,
                          float maximumEnergyValue,
                          float activationCostValue,
                          float maintenanceCostPerSecondValue,
                          float minimumActivationEnergyPercentValue,
                          PowerUpChargeType chargeTypeValue,
                          float chargePerTriggerValue)
    {
        Configure(activationResourceValue,
                  maintenanceResourceValue,
                  maximumEnergyValue,
                  activationCostValue,
                  maintenanceCostPerSecondValue,
                  minimumActivationEnergyPercentValue,
                  chargeTypeValue,
                  chargePerTriggerValue,
                  0f);
    }

    public void Configure(PowerUpResourceType activationResourceValue,
                          PowerUpResourceType maintenanceResourceValue,
                          float maximumEnergyValue,
                          float activationCostValue,
                          float maintenanceCostPerSecondValue,
                          float minimumActivationEnergyPercentValue,
                          PowerUpChargeType chargeTypeValue,
                          float chargePerTriggerValue,
                          float cooldownSecondsValue)
    {
        activationResource = activationResourceValue;
        maintenanceResource = maintenanceResourceValue;
        maximumEnergy = maximumEnergyValue;
        activationCost = activationCostValue;
        maintenanceCostPerSecond = maintenanceCostPerSecondValue;
        minimumActivationEnergyPercent = minimumActivationEnergyPercentValue;
        chargeType = chargeTypeValue;
        chargePerTrigger = chargePerTriggerValue;
        cooldownSeconds = cooldownSecondsValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (maximumEnergy < 0f)
            maximumEnergy = 0f;

        if (activationCost < 0f)
            activationCost = 0f;

        if (maintenanceCostPerSecond < 0f)
            maintenanceCostPerSecond = 0f;

        if (minimumActivationEnergyPercent < 0f)
            minimumActivationEnergyPercent = 0f;

        if (minimumActivationEnergyPercent > 100f)
            minimumActivationEnergyPercent = 100f;

        if (maximumEnergy <= 0f)
            minimumActivationEnergyPercent = 0f;

        if (chargePerTrigger < 0f)
            chargePerTrigger = 0f;

        if (cooldownSeconds < 0f)
            cooldownSeconds = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpCooldownGateModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Seconds that must elapse before this power up can be activated again.")]
    [SerializeField] private float cooldownSeconds = 1f;
    #endregion

    #endregion

    #region Properties
    public float CooldownSeconds
    {
        get
        {
            return cooldownSeconds;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float cooldownSecondsValue)
    {
        cooldownSeconds = cooldownSecondsValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (cooldownSeconds < 0f)
            cooldownSeconds = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpSuppressShootingModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, base shooting is blocked while this state is active.")]
    [SerializeField] private bool suppressBaseShootingWhileActive = true;

    [Tooltip("When enabled, activation interrupts charging or active effects on the opposite slot.")]
    [SerializeField] private bool interruptOtherSlotOnEnter;

    [Tooltip("When enabled, interruption clears only opposite-slot charge state.")]
    [SerializeField] private bool interruptOtherSlotChargingOnly = true;
    #endregion

    #endregion

    #region Properties
    public bool SuppressBaseShootingWhileActive
    {
        get
        {
            return suppressBaseShootingWhileActive;
        }
    }

    public bool InterruptOtherSlotOnEnter
    {
        get
        {
            return interruptOtherSlotOnEnter;
        }
    }

    public bool InterruptOtherSlotChargingOnly
    {
        get
        {
            return interruptOtherSlotChargingOnly;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(bool suppressBaseShootingWhileActiveValue)
    {
        Configure(suppressBaseShootingWhileActiveValue, false, true);
    }

    public void Configure(bool suppressBaseShootingWhileActiveValue,
                          bool interruptOtherSlotOnEnterValue,
                          bool interruptOtherSlotChargingOnlyValue)
    {
        suppressBaseShootingWhileActive = suppressBaseShootingWhileActiveValue;
        interruptOtherSlotOnEnter = interruptOtherSlotOnEnterValue;
        interruptOtherSlotChargingOnly = interruptOtherSlotChargingOnlyValue;
    }
    #endregion

    #endregion
}
#endregion
