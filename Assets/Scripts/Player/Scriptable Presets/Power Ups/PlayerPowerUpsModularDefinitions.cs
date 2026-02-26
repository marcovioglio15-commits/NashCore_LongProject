using System;
using System.Collections.Generic;
using UnityEngine;

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
    GateResource = 3,
    GateCooldown = 4,
    StateSuppressShooting = 5,
    EffectProjectilePatternCone = 6,
    EffectProjectileScale = 7,
    EffectProjectilePenetration = 8,
    EffectSpawnBomb = 9,
    EffectExplosionAreaDamage = 10,
    EffectDash = 11,
    EffectHealMissingHealth = 12,
    EffectTimeDilationEnemies = 13,
    HookOnPlayerMovementStep = 14,
    HookOnEnemyDeath = 15,
    HookOnProjectileSpawned = 16,
    HookOnProjectileWallHit = 17,
    HookOnProjectileDeath = 18,
    PassiveSpawnTrailSegment = 19,
    PassiveAreaTickApplyElement = 20,
    PassiveDeathExplosion = 21,
    PassiveProjectileOrbitOverride = 22,
    PassiveProjectileBounceOnWalls = 23,
    PassiveProjectileSplitOnDeath = 24
}

public enum ProjectilePenetrationMode
{
    None = 0,
    FixedHits = 1,
    Infinite = 2
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

    [Tooltip("When enabled, activation requires the slot to be fully charged.")]
    [SerializeField] private bool fullChargeRequirement;

    [Tooltip("Charge source used to refill energy over time/events.")]
    [SerializeField] private PowerUpChargeType chargeType = PowerUpChargeType.Time;

    [Tooltip("Recharge amount gained per trigger unit of the selected charge source.")]
    [SerializeField] private float chargePerTrigger = 100f;
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

    public bool FullChargeRequirement
    {
        get
        {
            return fullChargeRequirement;
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
    #endregion

    #region Methods

    #region Setup
    public void Configure(PowerUpResourceType activationResourceValue,
                          PowerUpResourceType maintenanceResourceValue,
                          float maximumEnergyValue,
                          float activationCostValue,
                          float maintenanceCostPerSecondValue,
                          bool fullChargeRequirementValue,
                          PowerUpChargeType chargeTypeValue,
                          float chargePerTriggerValue)
    {
        activationResource = activationResourceValue;
        maintenanceResource = maintenanceResourceValue;
        maximumEnergy = maximumEnergyValue;
        activationCost = activationCostValue;
        maintenanceCostPerSecond = maintenanceCostPerSecondValue;
        fullChargeRequirement = fullChargeRequirementValue;
        chargeType = chargeTypeValue;
        chargePerTrigger = chargePerTriggerValue;
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

        if (chargePerTrigger < 0f)
            chargePerTrigger = 0f;
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
    #endregion

    #region Methods

    #region Setup
    public void Configure(bool suppressBaseShootingWhileActiveValue)
    {
        suppressBaseShootingWhileActive = suppressBaseShootingWhileActiveValue;
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
public sealed class PowerUpHealMissingHealthModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Flat health restored on activation, clamped to missing HP.")]
    [SerializeField] private float healAmount = 30f;
    #endregion

    #endregion

    #region Properties
    public float HealAmount
    {
        get
        {
            return healAmount;
        }
    }
    #endregion

    #region Methods

    #region Setup
    public void Configure(float healAmountValue)
    {
        healAmount = healAmountValue;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (healAmount < 0f)
            healAmount = 0f;
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

    [Header("Gate - Resource")]
    [Tooltip("Resource-gate settings used by GateResource modules.")]
    [SerializeField] private PowerUpResourceGateModuleData resourceGate = new PowerUpResourceGateModuleData();

    [Header("Gate - Cooldown")]
    [Tooltip("Cooldown settings used by GateCooldown modules.")]
    [SerializeField] private PowerUpCooldownGateModuleData cooldownGate = new PowerUpCooldownGateModuleData();

    [Header("State - Suppress Shooting")]
    [Tooltip("Shooting suppression settings used by StateSuppressShooting modules.")]
    [SerializeField] private PowerUpSuppressShootingModuleData suppressShooting = new PowerUpSuppressShootingModuleData();

    [Header("Effect - Projectile Pattern")]
    [Tooltip("Projectile cone settings used by EffectProjectilePatternCone modules.")]
    [SerializeField] private PowerUpProjectilePatternConeModuleData projectilePatternCone = new PowerUpProjectilePatternConeModuleData();

    [Header("Effect - Projectile Scale")]
    [Tooltip("Projectile scaling settings used by EffectProjectileScale modules.")]
    [SerializeField] private PowerUpProjectileScaleModuleData projectileScale = new PowerUpProjectileScaleModuleData();

    [Header("Effect - Projectile Penetration")]
    [Tooltip("Projectile penetration settings used by EffectProjectilePenetration modules.")]
    [SerializeField] private PowerUpProjectilePenetrationModuleData projectilePenetration = new PowerUpProjectilePenetrationModuleData();

    [Header("Effect - Spawn Bomb")]
    [Tooltip("Bomb settings used by EffectSpawnBomb modules.")]
    [SerializeField] private BombToolData bomb = new BombToolData();

    [Header("Effect - Dash")]
    [Tooltip("Dash settings used by EffectDash modules.")]
    [SerializeField] private DashToolData dash = new DashToolData();

    [Header("Effect - Time Dilation")]
    [Tooltip("Time dilation settings used by EffectTimeDilationEnemies modules.")]
    [SerializeField] private BulletTimeToolData bulletTime = new BulletTimeToolData();

    [Header("Effect - Heal")]
    [Tooltip("Healing settings used by EffectHealMissingHealth modules.")]
    [SerializeField] private PowerUpHealMissingHealthModuleData healMissingHealth = new PowerUpHealMissingHealthModuleData();

    [Header("Passive - Death Explosion")]
    [Tooltip("Explosion settings used by PassiveDeathExplosion modules.")]
    [SerializeField] private ExplosionPassiveToolData deathExplosion = new ExplosionPassiveToolData();

    [Header("Passive - Orbit Override")]
    [Tooltip("Orbit settings used by PassiveProjectileOrbitOverride modules.")]
    [SerializeField] private PerfectCirclePassiveToolData projectileOrbitOverride = new PerfectCirclePassiveToolData();

    [Header("Passive - Bounce On Walls")]
    [Tooltip("Bounce settings used by PassiveProjectileBounceOnWalls modules.")]
    [SerializeField] private BouncingProjectilesPassiveToolData projectileBounceOnWalls = new BouncingProjectilesPassiveToolData();

    [Header("Passive - Split On Death")]
    [Tooltip("Split settings used by PassiveProjectileSplitOnDeath modules.")]
    [SerializeField] private SplittingProjectilesPassiveToolData projectileSplitOnDeath = new SplittingProjectilesPassiveToolData();

    [Header("Passive - Trail Spawn")]
    [Tooltip("Trail spawn settings used by PassiveSpawnTrailSegment modules.")]
    [SerializeField] private PowerUpTrailSpawnModuleData trailSpawn = new PowerUpTrailSpawnModuleData();

    [Header("Passive - Elemental Area Tick")]
    [Tooltip("Area tick elemental settings used by PassiveAreaTickApplyElement modules.")]
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

    public PowerUpCooldownGateModuleData CooldownGate
    {
        get
        {
            return cooldownGate;
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

    public PowerUpProjectileScaleModuleData ProjectileScale
    {
        get
        {
            return projectileScale;
        }
    }

    public PowerUpProjectilePenetrationModuleData ProjectilePenetration
    {
        get
        {
            return projectilePenetration;
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

    public SplittingProjectilesPassiveToolData ProjectileSplitOnDeath
    {
        get
        {
            return projectileSplitOnDeath;
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

        if (cooldownGate == null)
            cooldownGate = new PowerUpCooldownGateModuleData();

        if (suppressShooting == null)
            suppressShooting = new PowerUpSuppressShootingModuleData();

        if (projectilePatternCone == null)
            projectilePatternCone = new PowerUpProjectilePatternConeModuleData();

        if (projectileScale == null)
            projectileScale = new PowerUpProjectileScaleModuleData();

        if (projectilePenetration == null)
            projectilePenetration = new PowerUpProjectilePenetrationModuleData();

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

        if (projectileSplitOnDeath == null)
            projectileSplitOnDeath = new SplittingProjectilesPassiveToolData();

        if (trailSpawn == null)
            trailSpawn = new PowerUpTrailSpawnModuleData();

        if (elementalAreaTick == null)
            elementalAreaTick = new PowerUpElementalAreaTickModuleData();

        holdCharge.Validate();
        resourceGate.Validate();
        cooldownGate.Validate();
        projectilePatternCone.Validate();
        projectileScale.Validate();
        projectilePenetration.Validate();
        bomb.Validate();
        dash.Validate();
        bulletTime.Validate();
        healMissingHealth.Validate();
        deathExplosion.Validate();
        projectileOrbitOverride.Validate();
        projectileBounceOnWalls.Validate();
        projectileSplitOnDeath.Validate();
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

    [Tooltip("Default execution stage for this module.")]
    [SerializeField] private PowerUpModuleStage defaultStage = PowerUpModuleStage.Execute;

    [Tooltip("Optional designer notes for this module.")]
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
            return defaultStage;
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
        defaultStage = defaultStageValue;
        notes = notesValue;
        data = dataValue;
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

    [Tooltip("Execution stage used by this binding.")]
    [SerializeField] private PowerUpModuleStage stage = PowerUpModuleStage.Execute;

    [Tooltip("Order value used inside the same stage.")]
    [SerializeField] private int order;

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

    public int Order
    {
        get
        {
            return order;
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
    public void Configure(string moduleIdValue, PowerUpModuleStage stageValue, int orderValue, bool isEnabledValue)
    {
        moduleId = moduleIdValue;
        stage = stageValue;
        order = orderValue;
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
