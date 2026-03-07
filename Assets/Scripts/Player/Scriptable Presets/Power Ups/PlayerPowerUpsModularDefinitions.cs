using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#region Enums
public enum PowerUpModuleStage
{
    Trigger = 0,
    PreGate = 1,
    Gate = 2,
    StateEnter = 3,
    Execute = 4,
    StateExit = 5,
    PostExecute = 6,
    Hook = 7
}

public enum PowerUpModuleKind
{
    TriggerPress = 0,
    TriggerRelease = 1,
    TriggerHoldCharge = 2,
    TriggerEvent = 3,
    GateResource = 4,
    StateSuppressShooting = 5,
    ProjectilesPatternCone = 6,
    ProjectilesTuning = 7,
    SpawnObject = 8,
    Dash = 9,
    TimeDilationEnemies = 10,
    Heal = 11,
    SpawnTrailSegment = 12,
    AreaTickApplyElement = 13,
    DeathExplosion = 14,
    OrbitalProjectiles = 15,
    BouncingProjectiles = 16,
    ProjectileSplit = 17
}

public enum PowerUpTriggerEventType
{
    OnEnemyKilled = 0,
    OnPlayerDamaged = 1,
    OnPlayerMovementStep = 2,
    OnProjectileSpawned = 3,
    OnProjectileWallHit = 4,
    OnProjectileDespawned = 5
}

public enum ProjectilePenetrationMode
{
    None = 0,
    FixedHits = 1,
    Infinite = 2
}

public static class PowerUpModuleKindUtility
{
    #region Methods

    #region Public API
    public static PowerUpModuleStage ResolveStageFromKind(PowerUpModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerPress:
            case PowerUpModuleKind.TriggerRelease:
            case PowerUpModuleKind.TriggerHoldCharge:
                return PowerUpModuleStage.Trigger;
            case PowerUpModuleKind.TriggerEvent:
                return PowerUpModuleStage.Hook;
            case PowerUpModuleKind.GateResource:
                return PowerUpModuleStage.Gate;
            case PowerUpModuleKind.StateSuppressShooting:
                return PowerUpModuleStage.StateEnter;
            case PowerUpModuleKind.ProjectilesPatternCone:
            case PowerUpModuleKind.ProjectilesTuning:
            case PowerUpModuleKind.SpawnObject:
            case PowerUpModuleKind.Dash:
            case PowerUpModuleKind.TimeDilationEnemies:
            case PowerUpModuleKind.Heal:
                return PowerUpModuleStage.Execute;
            case PowerUpModuleKind.SpawnTrailSegment:
            case PowerUpModuleKind.AreaTickApplyElement:
            case PowerUpModuleKind.DeathExplosion:
            case PowerUpModuleKind.OrbitalProjectiles:
            case PowerUpModuleKind.BouncingProjectiles:
            case PowerUpModuleKind.ProjectileSplit:
                return PowerUpModuleStage.Hook;
            default:
                return PowerUpModuleStage.Hook;
        }
    }
    #endregion

    #endregion
}
#endregion

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

[Serializable]
public sealed class PowerUpProjectilePatternConeModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Projectile count spawned by this cone pattern.")]
    [SerializeField] private int projectileCount = 6;

    [Tooltip("Total cone angle in degrees.")]
    [SerializeField] private float coneAngleDegrees = 45f;
    #endregion

    #endregion

    #region Properties
    public int ProjectileCount
    {
        get
        {
            return projectileCount;
        }
    }

    public float ConeAngleDegrees
    {
        get
        {
            return coneAngleDegrees;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(int projectileCountValue, float coneAngleDegreesValue)
    {
        projectileCount = projectileCountValue;
        coneAngleDegrees = coneAngleDegreesValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (projectileCount < 1)
            projectileCount = 1;

        if (coneAngleDegrees < 0f)
            coneAngleDegrees = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpProjectileScaleModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Multiplier applied to projectile visual and collision scale.")]
    [SerializeField] private float sizeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile damage.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile speed.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile max range.")]
    [SerializeField] private float rangeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile lifetime seconds.")]
    [SerializeField] private float lifetimeMultiplier = 1f;
    #endregion

    #endregion

    #region Properties
    public float SizeMultiplier
    {
        get
        {
            return sizeMultiplier;
        }
    }

    public float DamageMultiplier
    {
        get
        {
            return damageMultiplier;
        }
    }

    public float SpeedMultiplier
    {
        get
        {
            return speedMultiplier;
        }
    }

    public float RangeMultiplier
    {
        get
        {
            return rangeMultiplier;
        }
    }

    public float LifetimeMultiplier
    {
        get
        {
            return lifetimeMultiplier;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float sizeMultiplierValue,
                          float damageMultiplierValue,
                          float speedMultiplierValue,
                          float rangeMultiplierValue,
                          float lifetimeMultiplierValue)
    {
        sizeMultiplier = sizeMultiplierValue;
        damageMultiplier = damageMultiplierValue;
        speedMultiplier = speedMultiplierValue;
        rangeMultiplier = rangeMultiplierValue;
        lifetimeMultiplier = lifetimeMultiplierValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (sizeMultiplier < 0.01f)
            sizeMultiplier = 0.01f;

        if (damageMultiplier < 0f)
            damageMultiplier = 0f;

        if (speedMultiplier < 0f)
            speedMultiplier = 0f;

        if (rangeMultiplier < 0f)
            rangeMultiplier = 0f;

        if (lifetimeMultiplier < 0f)
            lifetimeMultiplier = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpProjectilePenetrationModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Penetration behavior applied to the projectile.")]
    [SerializeField] private ProjectilePenetrationMode mode = ProjectilePenetrationMode.None;

    [Tooltip("Number of extra enemy penetrations when mode is FixedHits.")]
    [SerializeField] private int maxPenetrations;
    #endregion

    #endregion

    #region Properties
    public ProjectilePenetrationMode Mode
    {
        get
        {
            return mode;
        }
    }

    public int MaxPenetrations
    {
        get
        {
            return maxPenetrations;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(ProjectilePenetrationMode modeValue, int maxPenetrationsValue)
    {
        mode = modeValue;
        maxPenetrations = maxPenetrationsValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (maxPenetrations < 0)
            maxPenetrations = 0;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpProjectileTuningModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Multiplier applied to projectile visual and collision scale.")]
    [SerializeField] private float sizeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile damage.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile speed.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile max range.")]
    [SerializeField] private float rangeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile lifetime seconds.")]
    [SerializeField] private float lifetimeMultiplier = 1f;

    [Tooltip("Penetration behavior applied to the projectile.")]
    [SerializeField] private ProjectilePenetrationMode penetrationMode = ProjectilePenetrationMode.None;

    [Tooltip("Number of extra enemy penetrations when mode is FixedHits.")]
    [SerializeField] private int maxPenetrations;

    [Header("Elemental Stacks On Hit")]
    [Tooltip("When enabled, each projectile hit applies elemental stacks using the payload below.")]
    [SerializeField] private bool applyElementalOnHit;

    [Tooltip("Elemental effect payload applied on projectile hit when elemental stacks are enabled.")]
    [SerializeField] private ElementalEffectDefinitionData elementalEffectData = new ElementalEffectDefinitionData();

    [Tooltip("Elemental stacks applied on each projectile hit when elemental stacks are enabled.")]
    [SerializeField] private float elementalStacksPerHit = 1f;
    #endregion

    #endregion

    #region Properties
    public float SizeMultiplier
    {
        get
        {
            return sizeMultiplier;
        }
    }

    public float DamageMultiplier
    {
        get
        {
            return damageMultiplier;
        }
    }

    public float SpeedMultiplier
    {
        get
        {
            return speedMultiplier;
        }
    }

    public float RangeMultiplier
    {
        get
        {
            return rangeMultiplier;
        }
    }

    public float LifetimeMultiplier
    {
        get
        {
            return lifetimeMultiplier;
        }
    }

    public ProjectilePenetrationMode PenetrationMode
    {
        get
        {
            return penetrationMode;
        }
    }

    public int MaxPenetrations
    {
        get
        {
            return maxPenetrations;
        }
    }

    public bool ApplyElementalOnHit
    {
        get
        {
            return applyElementalOnHit;
        }
    }

    public ElementalEffectDefinitionData ElementalEffectData
    {
        get
        {
            return elementalEffectData;
        }
    }

    public float ElementalStacksPerHit
    {
        get
        {
            return elementalStacksPerHit;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float sizeMultiplierValue,
                          float damageMultiplierValue,
                          float speedMultiplierValue,
                          float rangeMultiplierValue,
                          float lifetimeMultiplierValue,
                          ProjectilePenetrationMode penetrationModeValue,
                          int maxPenetrationsValue,
                          bool applyElementalOnHitValue = false,
                          ElementalEffectDefinitionData elementalEffectDataValue = null,
                          float elementalStacksPerHitValue = 0f)
    {
        sizeMultiplier = sizeMultiplierValue;
        damageMultiplier = damageMultiplierValue;
        speedMultiplier = speedMultiplierValue;
        rangeMultiplier = rangeMultiplierValue;
        lifetimeMultiplier = lifetimeMultiplierValue;
        penetrationMode = penetrationModeValue;
        maxPenetrations = maxPenetrationsValue;
        applyElementalOnHit = applyElementalOnHitValue;
        elementalEffectData = elementalEffectDataValue != null ? elementalEffectDataValue : new ElementalEffectDefinitionData();
        elementalStacksPerHit = elementalStacksPerHitValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (sizeMultiplier < 0.01f)
            sizeMultiplier = 0.01f;

        if (damageMultiplier < 0f)
            damageMultiplier = 0f;

        if (speedMultiplier < 0f)
            speedMultiplier = 0f;

        if (rangeMultiplier < 0f)
            rangeMultiplier = 0f;

        if (lifetimeMultiplier < 0f)
            lifetimeMultiplier = 0f;

        if (maxPenetrations < 0)
            maxPenetrations = 0;

        if (elementalEffectData == null)
            elementalEffectData = new ElementalEffectDefinitionData();

        elementalEffectData.Validate();

        if (elementalStacksPerHit < 0f)
            elementalStacksPerHit = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpHealMissingHealthModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Healing mode. Instant applies one-time heal, OverTime distributes healing across ticks.")]
    [SerializeField] private PowerUpHealApplicationMode applyMode = PowerUpHealApplicationMode.Instant;

    [Tooltip("Total heal amount. For instant mode this is applied immediately, otherwise distributed in time.")]
    [SerializeField] private float healAmount = 30f;

    [Tooltip("Duration in seconds used by OverTime mode.")]
    [SerializeField] private float durationSeconds = 2f;

    [Tooltip("Tick interval in seconds used by OverTime mode.")]
    [SerializeField] private float tickIntervalSeconds = 0.2f;

    [Tooltip("Behavior when a new heal-over-time is applied while another is active.")]
    [SerializeField] private PowerUpHealStackPolicy stackPolicy = PowerUpHealStackPolicy.Refresh;
    #endregion

    #endregion

    #region Properties
    public PowerUpHealApplicationMode ApplyMode
    {
        get
        {
            return applyMode;
        }
    }

    public float HealAmount
    {
        get
        {
            return healAmount;
        }
    }

    public float DurationSeconds
    {
        get
        {
            return durationSeconds;
        }
    }

    public float TickIntervalSeconds
    {
        get
        {
            return tickIntervalSeconds;
        }
    }

    public PowerUpHealStackPolicy StackPolicy
    {
        get
        {
            return stackPolicy;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float healAmountValue)
    {
        Configure(PowerUpHealApplicationMode.Instant,
                  healAmountValue,
                  0f,
                  0.2f,
                  PowerUpHealStackPolicy.Refresh);
    }

    public void Configure(PowerUpHealApplicationMode applyModeValue,
                          float healAmountValue,
                          float durationSecondsValue,
                          float tickIntervalSecondsValue,
                          PowerUpHealStackPolicy stackPolicyValue)
    {
        applyMode = applyModeValue;
        healAmount = healAmountValue;
        durationSeconds = durationSecondsValue;
        tickIntervalSeconds = tickIntervalSecondsValue;
        stackPolicy = stackPolicyValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (healAmount < 0f)
            healAmount = 0f;

        if (durationSeconds < 0f)
            durationSeconds = 0f;

        if (tickIntervalSeconds < 0.01f)
            tickIntervalSeconds = 0.01f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpElementalAreaTickModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Elemental effect payload applied by area ticks.")]
    [SerializeField] private ElementalEffectDefinitionData effectData = new ElementalEffectDefinitionData();

    [Tooltip("Stacks applied on each area tick.")]
    [SerializeField] private float stacksPerTick = 1f;

    [Tooltip("Seconds between area stack applications while inside area.")]
    [SerializeField] private float applyIntervalSeconds = 0.2f;
    #endregion

    #endregion

    #region Properties
    public ElementalEffectDefinitionData EffectData
    {
        get
        {
            return effectData;
        }
    }

    public float StacksPerTick
    {
        get
        {
            return stacksPerTick;
        }
    }

    public float ApplyIntervalSeconds
    {
        get
        {
            return applyIntervalSeconds;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(ElementalEffectDefinitionData effectDataValue, float stacksPerTickValue, float applyIntervalSecondsValue)
    {
        effectData = effectDataValue;
        stacksPerTick = stacksPerTickValue;
        applyIntervalSeconds = applyIntervalSecondsValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (effectData == null)
            effectData = new ElementalEffectDefinitionData();

        effectData.Validate();

        if (stacksPerTick < 0f)
            stacksPerTick = 0f;

        if (applyIntervalSeconds < 0.01f)
            applyIntervalSeconds = 0.01f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpTrailSpawnModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Seconds each spawned trail segment remains active.")]
    [SerializeField] private float trailSegmentLifetimeSeconds = 1.5f;

    [Tooltip("Distance moved before spawning the next trail segment.")]
    [SerializeField] private float trailSpawnDistance = 0.6f;

    [Tooltip("Fallback interval for segment spawn while moving slowly.")]
    [SerializeField] private float trailSpawnIntervalSeconds = 0.1f;

    [Tooltip("Radius used by each trail segment for enemy checks.")]
    [SerializeField] private float trailRadius = 1.2f;

    [Tooltip("Maximum alive trail segments per player.")]
    [SerializeField] private int maxActiveSegmentsPerPlayer = 30;

    [Tooltip("Local offset applied to the attached trail VFX.")]
    [SerializeField] private Vector3 trailAttachedVfxOffset = new Vector3(0f, 0.08f, 0f);
    #endregion

    #endregion

    #region Properties
    public float TrailSegmentLifetimeSeconds
    {
        get
        {
            return trailSegmentLifetimeSeconds;
        }
    }

    public float TrailSpawnDistance
    {
        get
        {
            return trailSpawnDistance;
        }
    }

    public float TrailSpawnIntervalSeconds
    {
        get
        {
            return trailSpawnIntervalSeconds;
        }
    }

    public float TrailRadius
    {
        get
        {
            return trailRadius;
        }
    }

    public int MaxActiveSegmentsPerPlayer
    {
        get
        {
            return maxActiveSegmentsPerPlayer;
        }
    }

    public Vector3 TrailAttachedVfxOffset
    {
        get
        {
            return trailAttachedVfxOffset;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float trailSegmentLifetimeSecondsValue,
                          float trailSpawnDistanceValue,
                          float trailSpawnIntervalSecondsValue,
                          float trailRadiusValue,
                          int maxActiveSegmentsPerPlayerValue,
                          Vector3 trailAttachedVfxOffsetValue)
    {
        trailSegmentLifetimeSeconds = trailSegmentLifetimeSecondsValue;
        trailSpawnDistance = trailSpawnDistanceValue;
        trailSpawnIntervalSeconds = trailSpawnIntervalSecondsValue;
        trailRadius = trailRadiusValue;
        maxActiveSegmentsPerPlayer = maxActiveSegmentsPerPlayerValue;
        trailAttachedVfxOffset = trailAttachedVfxOffsetValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (trailSegmentLifetimeSeconds < 0.05f)
            trailSegmentLifetimeSeconds = 0.05f;

        if (trailSpawnDistance < 0f)
            trailSpawnDistance = 0f;

        if (trailSpawnIntervalSeconds < 0.01f)
            trailSpawnIntervalSeconds = 0.01f;

        if (trailRadius < 0f)
            trailRadius = 0f;

        if (maxActiveSegmentsPerPlayer < 1)
            maxActiveSegmentsPerPlayer = 1;

        if (float.IsNaN(trailAttachedVfxOffset.x) ||
            float.IsNaN(trailAttachedVfxOffset.y) ||
            float.IsNaN(trailAttachedVfxOffset.z) ||
            float.IsInfinity(trailAttachedVfxOffset.x) ||
            float.IsInfinity(trailAttachedVfxOffset.y) ||
            float.IsInfinity(trailAttachedVfxOffset.z))
        {
            trailAttachedVfxOffset = Vector3.zero;
        }
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpModuleData
{
    #region Fields

    #region Serialized Fields
    [Header("Trigger - Hold Charge")]
    [Tooltip("Hold-charge settings used by TriggerHoldCharge modules.")]
    [SerializeField] private PowerUpHoldChargeModuleData holdCharge = new PowerUpHoldChargeModuleData();

    [Header("Trigger - Event")]
    [Tooltip("Event trigger settings used by TriggerEvent modules.")]
    [SerializeField] private PowerUpTriggerEventModuleData triggerEvent = new PowerUpTriggerEventModuleData();

    [Header("Gate - Resource")]
    [Tooltip("Resource-gate settings used by GateResource modules.")]
    [SerializeField] private PowerUpResourceGateModuleData resourceGate = new PowerUpResourceGateModuleData();

    [Header("State - Suppress Shooting")]
    [Tooltip("Shooting suppression settings used by StateSuppressShooting modules.")]
    [SerializeField] private PowerUpSuppressShootingModuleData suppressShooting = new PowerUpSuppressShootingModuleData();

    [Header("Execute - Projectile Pattern")]
    [Tooltip("Projectile cone settings used by ProjectilesPatternCone modules.")]
    [SerializeField] private PowerUpProjectilePatternConeModuleData projectilePatternCone = new PowerUpProjectilePatternConeModuleData();

    [Header("Execute - Projectile Tuning")]
    [Tooltip("Combined projectile tuning settings used by ProjectilesTuning modules.")]
    [SerializeField] private PowerUpProjectileTuningModuleData projectileTuning = new PowerUpProjectileTuningModuleData();

    [Header("Execute - Spawn Object")]
    [Tooltip("Spawn-object settings used by SpawnObject modules.")]
    [SerializeField] private BombToolData bomb = new BombToolData();

    [Header("Execute - Dash")]
    [Tooltip("Dash settings used by Dash modules.")]
    [SerializeField] private DashToolData dash = new DashToolData();

    [Header("Execute - Time Dilation")]
    [Tooltip("Time dilation settings used by TimeDilationEnemies modules.")]
    [SerializeField] private BulletTimeToolData bulletTime = new BulletTimeToolData();

    [Header("Execute - Heal")]
    [Tooltip("Healing settings used by Heal modules.")]
    [SerializeField] private PowerUpHealMissingHealthModuleData healMissingHealth = new PowerUpHealMissingHealthModuleData();

    [Header("Hook - Death Explosion")]
    [Tooltip("Explosion settings used by DeathExplosion modules.")]
    [SerializeField] private ExplosionPassiveToolData deathExplosion = new ExplosionPassiveToolData();

    [Header("Hook - Orbital Projectiles")]
    [Tooltip("Orbit settings used by OrbitalProjectiles modules.")]
    [SerializeField] private PerfectCirclePassiveToolData projectileOrbitOverride = new PerfectCirclePassiveToolData();

    [Header("Hook - Bouncing Projectiles")]
    [Tooltip("Bounce settings used by BouncingProjectiles modules.")]
    [SerializeField] private BouncingProjectilesPassiveToolData projectileBounceOnWalls = new BouncingProjectilesPassiveToolData();

    [Header("Hook - Projectile Split")]
    [Tooltip("Split settings used by ProjectileSplit modules.")]
    [FormerlySerializedAs("projectileSplitOnDeath")]
    [SerializeField] private SplittingProjectilesPassiveToolData projectileSplit = new SplittingProjectilesPassiveToolData();

    [Header("Hook - Trail Spawn")]
    [Tooltip("Trail spawn settings used by SpawnTrailSegment modules.")]
    [SerializeField] private PowerUpTrailSpawnModuleData trailSpawn = new PowerUpTrailSpawnModuleData();

    [Header("Hook - Elemental Area Tick")]
    [Tooltip("Area tick elemental settings used by AreaTickApplyElement modules.")]
    [SerializeField] private PowerUpElementalAreaTickModuleData elementalAreaTick = new PowerUpElementalAreaTickModuleData();
    #endregion

    #endregion

    #region Properties
    public PowerUpHoldChargeModuleData HoldCharge
    {
        get
        {
            return holdCharge;
        }
    }

    public PowerUpResourceGateModuleData ResourceGate
    {
        get
        {
            return resourceGate;
        }
    }

    public PowerUpTriggerEventModuleData TriggerEvent
    {
        get
        {
            return triggerEvent;
        }
    }

    public PowerUpSuppressShootingModuleData SuppressShooting
    {
        get
        {
            return suppressShooting;
        }
    }

    public PowerUpProjectilePatternConeModuleData ProjectilePatternCone
    {
        get
        {
            return projectilePatternCone;
        }
    }

    public PowerUpProjectileTuningModuleData ProjectileTuning
    {
        get
        {
            return projectileTuning;
        }
    }

    public BombToolData Bomb
    {
        get
        {
            return bomb;
        }
    }

    public DashToolData Dash
    {
        get
        {
            return dash;
        }
    }

    public BulletTimeToolData BulletTime
    {
        get
        {
            return bulletTime;
        }
    }

    public PowerUpHealMissingHealthModuleData HealMissingHealth
    {
        get
        {
            return healMissingHealth;
        }
    }

    public ExplosionPassiveToolData DeathExplosion
    {
        get
        {
            return deathExplosion;
        }
    }

    public PerfectCirclePassiveToolData ProjectileOrbitOverride
    {
        get
        {
            return projectileOrbitOverride;
        }
    }

    public BouncingProjectilesPassiveToolData ProjectileBounceOnWalls
    {
        get
        {
            return projectileBounceOnWalls;
        }
    }

    public SplittingProjectilesPassiveToolData ProjectileSplit
    {
        get
        {
            return projectileSplit;
        }
    }

    public PowerUpTrailSpawnModuleData TrailSpawn
    {
        get
        {
            return trailSpawn;
        }
    }

    public PowerUpElementalAreaTickModuleData ElementalAreaTick
    {
        get
        {
            return elementalAreaTick;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (holdCharge == null)
            holdCharge = new PowerUpHoldChargeModuleData();

        if (resourceGate == null)
            resourceGate = new PowerUpResourceGateModuleData();

        if (triggerEvent == null)
            triggerEvent = new PowerUpTriggerEventModuleData();

        if (suppressShooting == null)
            suppressShooting = new PowerUpSuppressShootingModuleData();

        if (projectilePatternCone == null)
            projectilePatternCone = new PowerUpProjectilePatternConeModuleData();

        if (projectileTuning == null)
            projectileTuning = new PowerUpProjectileTuningModuleData();

        if (bomb == null)
            bomb = new BombToolData();

        if (dash == null)
            dash = new DashToolData();

        if (bulletTime == null)
            bulletTime = new BulletTimeToolData();

        if (healMissingHealth == null)
            healMissingHealth = new PowerUpHealMissingHealthModuleData();

        if (deathExplosion == null)
            deathExplosion = new ExplosionPassiveToolData();

        if (projectileOrbitOverride == null)
            projectileOrbitOverride = new PerfectCirclePassiveToolData();

        if (projectileBounceOnWalls == null)
            projectileBounceOnWalls = new BouncingProjectilesPassiveToolData();

        if (projectileSplit == null)
            projectileSplit = new SplittingProjectilesPassiveToolData();

        if (trailSpawn == null)
            trailSpawn = new PowerUpTrailSpawnModuleData();

        if (elementalAreaTick == null)
            elementalAreaTick = new PowerUpElementalAreaTickModuleData();

        holdCharge.Validate();
        resourceGate.Validate();
        projectilePatternCone.Validate();
        projectileTuning.Validate();
        bomb.Validate();
        dash.Validate();
        bulletTime.Validate();
        healMissingHealth.Validate();
        deathExplosion.Validate();
        projectileOrbitOverride.Validate();
        projectileBounceOnWalls.Validate();
        projectileSplit.Validate();
        trailSpawn.Validate();
        elementalAreaTick.Validate();
    }
    #endregion

    #endregion
}
#endregion

#region Module Definitions
[Serializable]
public sealed class PowerUpModuleDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Stable identifier of this module definition.")]
    [SerializeField] private string moduleId;

    [Tooltip("Display name shown in module pickers.")]
    [SerializeField] private string displayName = "New Module";

    [Tooltip("Behavior implemented by this module.")]
    [SerializeField] private PowerUpModuleKind moduleKind;

    [Tooltip("Legacy serialized stage. Stage is now derived from module kind.")]
    [HideInInspector]
    [SerializeField] private PowerUpModuleStage defaultStage = PowerUpModuleStage.Execute;

    [Tooltip("Optional notes for this module.")]
    [SerializeField] private string notes;

    [Tooltip("Payload used by this module kind.")]
    [SerializeField] private PowerUpModuleData data = new PowerUpModuleData();
    #endregion

    #endregion

    #region Properties
    public string ModuleId
    {
        get
        {
            return moduleId;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public PowerUpModuleKind ModuleKind
    {
        get
        {
            return moduleKind;
        }
    }

    public PowerUpModuleStage DefaultStage
    {
        get
        {
            return PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);
        }
    }

    public string Notes
    {
        get
        {
            return notes;
        }
    }

    public PowerUpModuleData Data
    {
        get
        {
            return data;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(string moduleIdValue,
                          string displayNameValue,
                          PowerUpModuleKind moduleKindValue,
                          PowerUpModuleStage defaultStageValue,
                          string notesValue,
                          PowerUpModuleData dataValue)
    {
        moduleId = moduleIdValue;
        displayName = displayNameValue;
        moduleKind = moduleKindValue;
        defaultStage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKindValue);
        notes = notesValue;
        data = dataValue;
    }

    public void SetModuleKind(PowerUpModuleKind moduleKindValue)
    {
        moduleKind = moduleKindValue;
        defaultStage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKindValue);
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            moduleId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "New Module";

        if (data == null)
            data = new PowerUpModuleData();

        defaultStage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);
        data.Validate();
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PowerUpModuleBinding
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Referenced ModuleId inside Modules Management.")]
    [SerializeField] private string moduleId;

    [Tooltip("Legacy serialized stage. Stage is now derived from module kind.")]
    [HideInInspector]
    [SerializeField] private PowerUpModuleStage stage = PowerUpModuleStage.Execute;

    [Tooltip("When disabled, this module binding is ignored at bake/runtime compile.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("When enabled, this binding uses the override payload instead of module defaults.")]
    [SerializeField] private bool useOverridePayload;

    [Tooltip("Override payload used when Use Override Payload is enabled.")]
    [SerializeField] private PowerUpModuleData overridePayload = new PowerUpModuleData();
    #endregion

    #endregion

    #region Properties
    public string ModuleId
    {
        get
        {
            return moduleId;
        }
    }

    public PowerUpModuleStage Stage
    {
        get
        {
            return stage;
        }
    }

    public bool IsEnabled
    {
        get
        {
            return isEnabled;
        }
    }

    public bool UseOverridePayload
    {
        get
        {
            return useOverridePayload;
        }
    }

    public PowerUpModuleData OverridePayload
    {
        get
        {
            return overridePayload;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(string moduleIdValue, bool isEnabledValue)
    {
        moduleId = moduleIdValue;
        stage = PowerUpModuleStage.Execute;
        isEnabled = isEnabledValue;
    }

    public void Configure(string moduleIdValue, PowerUpModuleStage stageValue, bool isEnabledValue)
    {
        moduleId = moduleIdValue;
        stage = stageValue;
        isEnabled = isEnabledValue;
    }

    public void ConfigureOverride(bool useOverridePayloadValue, PowerUpModuleData overridePayloadValue)
    {
        useOverridePayload = useOverridePayloadValue;
        overridePayload = overridePayloadValue;
    }
    #endregion

    #region Helpers
    public PowerUpModuleData ResolvePayload(PowerUpModuleDefinition moduleDefinition)
    {
        if (useOverridePayload && overridePayload != null)
            return overridePayload;

        if (moduleDefinition != null && moduleDefinition.Data != null)
            return moduleDefinition.Data;

        return null;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (overridePayload == null)
            overridePayload = new PowerUpModuleData();

        overridePayload.Validate();
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class ModularPowerUpDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Common metadata and drop data for this composed power up.")]
    [SerializeField] private PowerUpCommonData commonData = new PowerUpCommonData();

    [Tooltip("Ordered list of module bindings composing this power up.")]
    [SerializeField] private List<PowerUpModuleBinding> moduleBindings = new List<PowerUpModuleBinding>();

    [Tooltip("When enabled, this power up cannot be replaced from runtime slots.")]
    [SerializeField] private bool unreplaceable;
    #endregion

    #endregion

    #region Properties
    public PowerUpCommonData CommonData
    {
        get
        {
            return commonData;
        }
    }

    public IReadOnlyList<PowerUpModuleBinding> ModuleBindings
    {
        get
        {
            return moduleBindings;
        }
    }

    public bool Unreplaceable
    {
        get
        {
            return unreplaceable;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(PowerUpCommonData commonDataValue, bool unreplaceableValue)
    {
        commonData = commonDataValue;
        unreplaceable = unreplaceableValue;

        if (moduleBindings == null)
            moduleBindings = new List<PowerUpModuleBinding>();
    }

    public void ClearBindings()
    {
        if (moduleBindings == null)
            moduleBindings = new List<PowerUpModuleBinding>();

        moduleBindings.Clear();
    }

    public void AddBinding(PowerUpModuleBinding binding)
    {
        if (binding == null)
            return;

        if (moduleBindings == null)
            moduleBindings = new List<PowerUpModuleBinding>();

        moduleBindings.Add(binding);
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (commonData == null)
            commonData = new PowerUpCommonData();

        commonData.Validate();

        if (moduleBindings == null)
            moduleBindings = new List<PowerUpModuleBinding>();

        for (int index = 0; index < moduleBindings.Count; index++)
        {
            PowerUpModuleBinding binding = moduleBindings[index];

            if (binding == null)
                continue;

            binding.Validate();
        }
    }
    #endregion

    #endregion
}
#endregion
