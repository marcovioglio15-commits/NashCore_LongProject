using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#region Enums
public enum PassiveModifierKind
{
    StatModifier = 0,
    GameplayModifier = 1
}

public enum PassiveStatType
{
    MaxHealth = 0,
    MoveSpeed = 1,
    ProjectileDamage = 2,
    FireRate = 3
}

public enum PassiveStatOperation
{
    Add = 0,
    Multiply = 1
}

public enum ActiveToolKind
{
    Bomb = 0,
    Dash = 1,
    BulletTime = 2,
    Custom = 3
}

public enum PowerUpResourceType
{
    None = 0,
    Energy = 1,
    Health = 2,
    Shield = 3
}

public enum PowerUpChargeType
{
    Time = 0,
    EnemiesDestroyed = 1,
    WavesCleared = 2,
    RoomsCleared = 3,
    DamageInflicted = 4,
    DamageTaken = 5
}

public enum PassiveToolKind
{
    ProjectileSize = 0,
    ElementalProjectiles = 1,
    PerfectCircle = 2,
    BouncingProjectiles = 3,
    SplittingProjectiles = 4,
    Explosion = 5,
    ElementalTrail = 6,
    Custom = 7
}

public enum ElementType
{
    Fire = 0,
    Ice = 1,
    Poison = 2,
    Custom = 3
}

public enum ElementalEffectKind
{
    Dots = 0,
    Impediment = 1
}

public enum ElementalProcMode
{
    ThresholdOnly = 0,
    ProgressiveUntilThreshold = 1
}

public enum ElementalProcReapplyMode
{
    AccumulateStacks = 0,
    RefreshActiveProc = 1,
    IgnoreWhileProcActive = 2
}

public enum PassiveExplosionTriggerMode
{
    Periodic = 0,
    OnPlayerDamaged = 1,
    OnEnemyKilled = 2
}

public enum ProjectileSplitDirectionMode
{
    Uniform = 0,
    CustomAngles = 1
}
#endregion

#region Common Data Structures
[Serializable]
public sealed class PowerUpCommonData
{
    #region Fields

    #region Serialized Fields
    [Header("Identity")]
    [Tooltip("Stable identifier for this power up entry.")]
    [SerializeField] private string powerUpId;

    [Tooltip("Display name shown to players (WIP).")]
    [SerializeField] private string displayName = "New Power Up";

    [Tooltip("Description shown in tooltips and codex entries(WIP).")]
    [SerializeField] private string description;

    [Header("Drop")]
    [Tooltip("Drop pools where this power up can appear(WIP).")]
    [SerializeField] private List<string> dropPools = new List<string>();

    [Tooltip("Rarity tier for this power up. Range: 1 to 5(WIP).")]
    [SerializeField] private int dropTier = 1;

    [Tooltip("Shop purchase cost associated with this power up(WIP).")]
    [SerializeField] private int purchaseCost;
    #endregion

    #endregion

    #region Properties
    public string PowerUpId
    {
        get
        {
            return powerUpId;
        }
    }

    public string DisplayName
    {
        get
        {
            return displayName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public IReadOnlyList<string> DropPools
    {
        get
        {
            return dropPools;
        }
    }

    public int DropTier
    {
        get
        {
            return dropTier;
        }
    }

    public int PurchaseCost
    {
        get
        {
            return purchaseCost;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(powerUpId))
            powerUpId = Guid.NewGuid().ToString("N");

        if (dropPools == null)
            dropPools = new List<string>();

        if (dropTier < 1)
            dropTier = 1;

        if (dropTier > 5)
            dropTier = 5;

        if (purchaseCost < 0)
            purchaseCost = 0;
    }
    #endregion

    #endregion
}
#endregion

#region Passive

[Serializable]
public sealed class PassiveStatModifier
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Target stat modified by this passive effect.")]
    [SerializeField] private PassiveStatType statType = PassiveStatType.MaxHealth;

    [Tooltip("Operation used to apply Value to the target stat.")]
    [SerializeField] private PassiveStatOperation operation = PassiveStatOperation.Add;

    [Tooltip("Modifier value applied to the selected stat.")]
    [SerializeField] private float value = 1f;
    #endregion

    #endregion

    #region Properties
    public PassiveStatType StatType
    {
        get
        {
            return statType;
        }
    }

    public PassiveStatOperation Operation
    {
        get
        {
            return operation;
        }
    }

    public float Value
    {
        get
        {
            return value;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (operation == PassiveStatOperation.Multiply && value < 0f)
            value = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PassiveModifierDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Common metadata and drop data for this passive modifier.")]
    [SerializeField] private PowerUpCommonData commonData = new PowerUpCommonData();

    [Tooltip("Passive modifier behavior category.")]
    [SerializeField] private PassiveModifierKind modifierKind = PassiveModifierKind.StatModifier;

    [Tooltip("List of stat modifiers applied by this passive modifier.")]
    [SerializeField] private List<PassiveStatModifier> statModifiers = new List<PassiveStatModifier>();
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

    public PassiveModifierKind ModifierKind
    {
        get
        {
            return modifierKind;
        }
    }

    public IReadOnlyList<PassiveStatModifier> StatModifiers
    {
        get
        {
            return statModifiers;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (commonData == null)
            commonData = new PowerUpCommonData();

        commonData.Validate();

        if (statModifiers == null)
            statModifiers = new List<PassiveStatModifier>();

        for (int index = 0; index < statModifiers.Count; index++)
        {
            PassiveStatModifier modifier = statModifiers[index];

            if (modifier == null)
                continue;

            modifier.Validate();
        }
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class ProjectileSizePassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Projectile Size Passive")]
    [Tooltip("Multiplier applied to projectile transform scale and collision radius. 1 keeps default size.")]
    [SerializeField] private float projectileSizeMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile damage. 1 keeps default damage.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile speed. 1 keeps default speed.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile lifetime in seconds. 1 keeps default temporal lifetime.")]
    [SerializeField] private float lifetimeSecondsMultiplier = 1f;

    [Tooltip("Multiplier applied to projectile max range distance. 1 keeps default distance lifetime.")]
    [SerializeField] private float lifetimeRangeMultiplier = 1f;
    #endregion

    #endregion

    #region Properties
    public float ProjectileSizeMultiplier
    {
        get
        {
            return projectileSizeMultiplier;
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

    public float LifetimeSecondsMultiplier
    {
        get
        {
            return lifetimeSecondsMultiplier;
        }
    }

    public float LifetimeRangeMultiplier
    {
        get
        {
            return lifetimeRangeMultiplier;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (projectileSizeMultiplier < 0.01f)
            projectileSizeMultiplier = 0.01f;

        if (damageMultiplier < 0f)
            damageMultiplier = 0f;

        if (speedMultiplier < 0f)
            speedMultiplier = 0f;

        if (lifetimeSecondsMultiplier < 0f)
            lifetimeSecondsMultiplier = 0f;

        if (lifetimeRangeMultiplier < 0f)
            lifetimeRangeMultiplier = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class ElementalEffectDefinitionData
{
    #region Fields

    #region Serialized Fields
    [Header("Element")]
    [Tooltip("Element identifier used by this payload.")]
    [SerializeField] private ElementType elementType = ElementType.Fire;

    [Tooltip("Gameplay effect generated by this element.")]
    [SerializeField] private ElementalEffectKind effectKind = ElementalEffectKind.Dots;

    [Tooltip("How stacks apply effect strength before threshold.")]
    [SerializeField] private ElementalProcMode procMode = ElementalProcMode.ThresholdOnly;

    [Tooltip("How incoming stacks behave while this element proc is already active on the same target.")]
    [SerializeField] private ElementalProcReapplyMode reapplyMode = ElementalProcReapplyMode.AccumulateStacks;

    [Tooltip("Stacks required to trigger full element proc.")]
    [SerializeField] private float procThresholdStacks = 5f;

    [Tooltip("Maximum stack amount kept on a target for this element.")]
    [SerializeField] private float maximumStacks = 20f;

    [Tooltip("Stacks removed per second when the target is not refreshed.")]
    [SerializeField] private float stackDecayPerSecond = 1f;

    [Tooltip("When enabled, stacks are consumed when threshold proc triggers.")]
    [SerializeField] private bool consumeStacksOnProc;

    [Header("Dots Effect")]
    [Tooltip("Damage applied every tick when Dots effect is active.")]
    [SerializeField] private float dotDamagePerTick = 4f;

    [Tooltip("Seconds between two consecutive dot ticks.")]
    [SerializeField] private float dotTickInterval = 0.5f;

    [Tooltip("Duration in seconds of the dots payload when proc occurs.")]
    [SerializeField] private float dotDurationSeconds = 2f;

    [Header("Impediment Effect")]
    [Tooltip("Slow percentage gained per stack in progressive mode. Example: 10 means 10%.")]
    [SerializeField] private float impedimentSlowPercentPerStack = 10f;

    [Tooltip("Slow percentage applied when threshold proc occurs. Example: 50 means 50%.")]
    [SerializeField] private float impedimentProcSlowPercent = 50f;

    [Tooltip("Maximum total slow percentage cap. 100 means complete immobilization.")]
    [SerializeField] private float impedimentMaxSlowPercent = 100f;

    [Tooltip("Duration in seconds of threshold impediment proc.")]
    [SerializeField] private float impedimentDurationSeconds = 2f;
    #endregion

    #endregion

    #region Properties
    public ElementType ElementType
    {
        get
        {
            return elementType;
        }
    }

    public ElementalEffectKind EffectKind
    {
        get
        {
            return effectKind;
        }
    }

    public ElementalProcMode ProcMode
    {
        get
        {
            return procMode;
        }
    }

    public ElementalProcReapplyMode ReapplyMode
    {
        get
        {
            return reapplyMode;
        }
    }

    public float ProcThresholdStacks
    {
        get
        {
            return procThresholdStacks;
        }
    }

    public float MaximumStacks
    {
        get
        {
            return maximumStacks;
        }
    }

    public float StackDecayPerSecond
    {
        get
        {
            return stackDecayPerSecond;
        }
    }

    public bool ConsumeStacksOnProc
    {
        get
        {
            return consumeStacksOnProc;
        }
    }

    public float DotDamagePerTick
    {
        get
        {
            return dotDamagePerTick;
        }
    }

    public float DotTickInterval
    {
        get
        {
            return dotTickInterval;
        }
    }

    public float DotDurationSeconds
    {
        get
        {
            return dotDurationSeconds;
        }
    }

    public float ImpedimentSlowPercentPerStack
    {
        get
        {
            return impedimentSlowPercentPerStack;
        }
    }

    public float ImpedimentProcSlowPercent
    {
        get
        {
            return impedimentProcSlowPercent;
        }
    }

    public float ImpedimentMaxSlowPercent
    {
        get
        {
            return impedimentMaxSlowPercent;
        }
    }

    public float ImpedimentDurationSeconds
    {
        get
        {
            return impedimentDurationSeconds;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (procThresholdStacks < 0.1f)
            procThresholdStacks = 0.1f;

        if (maximumStacks < 0.1f)
            maximumStacks = 0.1f;

        if (maximumStacks < procThresholdStacks)
            maximumStacks = procThresholdStacks;

        if (stackDecayPerSecond < 0f)
            stackDecayPerSecond = 0f;

        if (dotDamagePerTick < 0f)
            dotDamagePerTick = 0f;

        if (dotTickInterval < 0.01f)
            dotTickInterval = 0.01f;

        if (dotDurationSeconds < 0.05f)
            dotDurationSeconds = 0.05f;

        if (impedimentSlowPercentPerStack < 0f)
            impedimentSlowPercentPerStack = 0f;

        if (impedimentProcSlowPercent < 0f)
            impedimentProcSlowPercent = 0f;

        if (impedimentMaxSlowPercent < 0f)
            impedimentMaxSlowPercent = 0f;

        if (impedimentMaxSlowPercent > 100f)
            impedimentMaxSlowPercent = 100f;

        if (impedimentProcSlowPercent > impedimentMaxSlowPercent)
            impedimentProcSlowPercent = impedimentMaxSlowPercent;

        if (impedimentDurationSeconds < 0.05f)
            impedimentDurationSeconds = 0.05f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class ElementalProjectilesPassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Elemental Projectiles")]
    [Tooltip("Elemental effect payload applied when a projectile hits an enemy.")]
    [SerializeField] private ElementalEffectDefinitionData effectData = new ElementalEffectDefinitionData();

    [Tooltip("Stacks added to the hit enemy by each projectile impact.")]
    [SerializeField] private float stacksPerHit = 1f;

    [Header("Stack VFX (Optional)")]
    [Tooltip("When enabled, a VFX is spawned at each stack application.")]
    [SerializeField] private bool spawnStackVfx;

    [Tooltip("Optional prefab used for stack application VFX.")]
    [SerializeField] private GameObject stackVfxPrefab;

    [Tooltip("Scale multiplier applied to stack VFX spawn.")]
    [SerializeField] private float stackVfxScaleMultiplier = 1f;

    [Header("Proc VFX (Optional)")]
    [Tooltip("When enabled, a VFX is spawned when threshold proc occurs.")]
    [SerializeField] private bool spawnProcVfx;

    [Tooltip("Optional prefab used for threshold proc VFX.")]
    [SerializeField] private GameObject procVfxPrefab;

    [Tooltip("Scale multiplier applied to proc VFX spawn.")]
    [SerializeField] private float procVfxScaleMultiplier = 1f;
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

    public float StacksPerHit
    {
        get
        {
            return stacksPerHit;
        }
    }

    public bool SpawnStackVfx
    {
        get
        {
            return spawnStackVfx;
        }
    }

    public GameObject StackVfxPrefab
    {
        get
        {
            return stackVfxPrefab;
        }
    }

    public float StackVfxScaleMultiplier
    {
        get
        {
            return stackVfxScaleMultiplier;
        }
    }

    public bool SpawnProcVfx
    {
        get
        {
            return spawnProcVfx;
        }
    }

    public GameObject ProcVfxPrefab
    {
        get
        {
            return procVfxPrefab;
        }
    }

    public float ProcVfxScaleMultiplier
    {
        get
        {
            return procVfxScaleMultiplier;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (effectData == null)
            effectData = new ElementalEffectDefinitionData();

        effectData.Validate();

        if (stacksPerHit < 0f)
            stacksPerHit = 0f;

        if (stackVfxScaleMultiplier < 0.01f)
            stackVfxScaleMultiplier = 0.01f;

        if (procVfxScaleMultiplier < 0.01f)
            procVfxScaleMultiplier = 0.01f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PerfectCirclePassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Perfect Circle")]
    [Tooltip("Entry speed while projectiles move away from the player before full orbit.")]
    [SerializeField] private float radialEntrySpeed = 12f;

    [Tooltip("Tangential speed used while projectiles orbit around the player.")]
    [SerializeField] private float orbitalSpeed = 8f;

    [Tooltip("Minimum orbit radius reached during rhythmic orbit pulsation.")]
    [SerializeField] private float orbitRadiusMin = 4f;

    [Tooltip("Maximum orbit radius reached during rhythmic orbit pulsation.")]
    [SerializeField] private float orbitRadiusMax = 6f;

    [Tooltip("Frequency in hertz used to expand and retract orbit radius rhythmically.")]
    [SerializeField] private float orbitPulseFrequency = 0.7f;

    [Tooltip("Ratio of target radius used as entry threshold before projectile switches to orbit mode.")]
    [SerializeField] private float orbitEntryRatio = 0.9f;

    [Tooltip("Seconds used to blend from radial motion to orbit motion.")]
    [SerializeField] private float orbitBlendDuration = 0.3f;

    [Tooltip("Vertical offset from player origin used for orbit simulation.")]
    [SerializeField] private float heightOffset;

    [Tooltip("Golden-angle step in degrees used to distribute orbit angles for consecutive spawns.")]
    [SerializeField] private float goldenAngleDegrees = 137.5f;
    #endregion

    #endregion

    #region Properties
    public float RadialEntrySpeed
    {
        get
        {
            return radialEntrySpeed;
        }
    }

    public float OrbitalSpeed
    {
        get
        {
            return orbitalSpeed;
        }
    }

    public float OrbitRadiusMin
    {
        get
        {
            return orbitRadiusMin;
        }
    }

    public float OrbitRadiusMax
    {
        get
        {
            return orbitRadiusMax;
        }
    }

    public float OrbitPulseFrequency
    {
        get
        {
            return orbitPulseFrequency;
        }
    }

    public float OrbitEntryRatio
    {
        get
        {
            return orbitEntryRatio;
        }
    }

    public float OrbitBlendDuration
    {
        get
        {
            return orbitBlendDuration;
        }
    }

    public float HeightOffset
    {
        get
        {
            return heightOffset;
        }
    }

    public float GoldenAngleDegrees
    {
        get
        {
            return goldenAngleDegrees;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (radialEntrySpeed < 0f)
            radialEntrySpeed = 0f;

        if (orbitalSpeed < 0f)
            orbitalSpeed = 0f;

        if (orbitRadiusMin < 0f)
            orbitRadiusMin = 0f;

        if (orbitRadiusMax < 0f)
            orbitRadiusMax = 0f;

        if (orbitRadiusMax < orbitRadiusMin)
            orbitRadiusMax = orbitRadiusMin;

        if (orbitPulseFrequency < 0f)
            orbitPulseFrequency = 0f;

        orbitEntryRatio = Mathf.Clamp01(orbitEntryRatio);

        if (orbitBlendDuration < 0f)
            orbitBlendDuration = 0f;

        if (goldenAngleDegrees < 0f)
            goldenAngleDegrees = Mathf.Abs(goldenAngleDegrees);
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class BouncingProjectilesPassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Bouncing Projectiles")]
    [Tooltip("Maximum wall bounces allowed for each spawned projectile.")]
    [SerializeField] private int maxBounces = 2;

    [Tooltip("Speed percentage delta applied each bounce. Example: -20 means 20% slower, 15 means 15% faster.")]
    [SerializeField] private float speedPercentChangePerBounce = -5f;

    [Tooltip("Minimum resulting speed multiplier after bounce scaling.")]
    [SerializeField] private float minimumSpeedMultiplierAfterBounce = 0.1f;

    [Tooltip("Maximum resulting speed multiplier after bounce scaling.")]
    [SerializeField] private float maximumSpeedMultiplierAfterBounce = 3f;
    #endregion

    #endregion

    #region Properties
    public int MaxBounces
    {
        get
        {
            return maxBounces;
        }
    }

    public float SpeedPercentChangePerBounce
    {
        get
        {
            return speedPercentChangePerBounce;
        }
    }

    public float MinimumSpeedMultiplierAfterBounce
    {
        get
        {
            return minimumSpeedMultiplierAfterBounce;
        }
    }

    public float MaximumSpeedMultiplierAfterBounce
    {
        get
        {
            return maximumSpeedMultiplierAfterBounce;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (maxBounces < 0)
            maxBounces = 0;

        if (minimumSpeedMultiplierAfterBounce < 0f)
            minimumSpeedMultiplierAfterBounce = 0f;

        if (maximumSpeedMultiplierAfterBounce < minimumSpeedMultiplierAfterBounce)
            maximumSpeedMultiplierAfterBounce = minimumSpeedMultiplierAfterBounce;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class SplittingProjectilesPassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Splitting Projectiles")]
    [Tooltip("How split projectile directions are generated.")]
    [SerializeField] private ProjectileSplitDirectionMode directionMode = ProjectileSplitDirectionMode.Uniform;

    [Tooltip("Amount of split projectiles spawned on enemy hit when Uniform mode is active.")]
    [SerializeField] private int splitProjectileCount = 2;

    [Tooltip("Angular offset in degrees used as rotation start for uniform split distribution.")]
    [SerializeField] private float splitOffsetDegrees;

    [Tooltip("Custom split angles in degrees relative to impact direction when Custom Angles mode is active.")]
    [SerializeField] private List<float> customAnglesDegrees = new List<float>
    {
        -20f,
        20f
    };

    [Tooltip("Damage percentage for each split projectile relative to original. 100 means equal damage.")]
    [SerializeField] private float splitDamagePercentFromOriginal = 50f;

    [Tooltip("Size percentage for each split projectile relative to original. 100 means equal size.")]
    [SerializeField] private float splitSizePercentFromOriginal = 75f;

    [Tooltip("Speed percentage for each split projectile relative to original. 100 means equal speed.")]
    [SerializeField] private float splitSpeedPercentFromOriginal = 85f;

    [Tooltip("Lifetime percentage for each split projectile relative to original. 100 means equal lifetime.")]
    [SerializeField] private float splitLifetimePercentFromOriginal = 65f;
    #endregion

    #endregion

    #region Properties
    public ProjectileSplitDirectionMode DirectionMode
    {
        get
        {
            return directionMode;
        }
    }

    public int SplitProjectileCount
    {
        get
        {
            return splitProjectileCount;
        }
    }

    public float SplitOffsetDegrees
    {
        get
        {
            return splitOffsetDegrees;
        }
    }

    public IReadOnlyList<float> CustomAnglesDegrees
    {
        get
        {
            return customAnglesDegrees;
        }
    }

    public float SplitDamagePercentFromOriginal
    {
        get
        {
            return splitDamagePercentFromOriginal;
        }
    }

    public float SplitSizePercentFromOriginal
    {
        get
        {
            return splitSizePercentFromOriginal;
        }
    }

    public float SplitSpeedPercentFromOriginal
    {
        get
        {
            return splitSpeedPercentFromOriginal;
        }
    }

    public float SplitLifetimePercentFromOriginal
    {
        get
        {
            return splitLifetimePercentFromOriginal;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (splitProjectileCount < 1)
            splitProjectileCount = 1;

        if (customAnglesDegrees == null)
            customAnglesDegrees = new List<float>();

        if (splitDamagePercentFromOriginal < 0f)
            splitDamagePercentFromOriginal = 0f;

        if (splitSizePercentFromOriginal < 0f)
            splitSizePercentFromOriginal = 0f;

        if (splitSpeedPercentFromOriginal < 0f)
            splitSpeedPercentFromOriginal = 0f;

        if (splitLifetimePercentFromOriginal < 0f)
            splitLifetimePercentFromOriginal = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class ExplosionPassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Passive Explosion")]
    [Tooltip("Event that triggers this explosion passive.")]
    [SerializeField] private PassiveExplosionTriggerMode triggerMode = PassiveExplosionTriggerMode.Periodic;

    [Tooltip("Minimum seconds required between two explosion triggers in any trigger mode.")]
    [SerializeField] private float cooldownSeconds = 2f;

    [Tooltip("Explosion radius in meters.")]
    [SerializeField] private float radius = 4f;

    [Tooltip("Explosion damage applied to valid enemies.")]
    [SerializeField] private float damage = 20f;

    [Tooltip("When enabled, all enemies inside radius are damaged. Otherwise only closest enemy is damaged.")]
    [SerializeField] private bool affectAllEnemiesInRadius = true;

    [Tooltip("Optional local offset applied to explosion center from trigger origin.")]
    [SerializeField] private Vector3 triggerOffset = Vector3.zero;

    [Header("Explosion VFX (Optional)")]
    [Tooltip("Optional VFX prefab spawned at explosion center.")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Tooltip("When enabled, explosion VFX scale is multiplied by radius.")]
    [SerializeField] private bool scaleVfxToRadius = true;

    [Tooltip("Additional scale multiplier applied to explosion VFX.")]
    [SerializeField] private float vfxScaleMultiplier = 1f;
    #endregion

    #endregion

    #region Properties
    public PassiveExplosionTriggerMode TriggerMode
    {
        get
        {
            return triggerMode;
        }
    }

    public float CooldownSeconds
    {
        get
        {
            return cooldownSeconds;
        }
    }

    public float Radius
    {
        get
        {
            return radius;
        }
    }

    public float Damage
    {
        get
        {
            return damage;
        }
    }

    public bool AffectAllEnemiesInRadius
    {
        get
        {
            return affectAllEnemiesInRadius;
        }
    }

    public Vector3 TriggerOffset
    {
        get
        {
            return triggerOffset;
        }
    }

    public GameObject ExplosionVfxPrefab
    {
        get
        {
            return explosionVfxPrefab;
        }
    }

    public bool ScaleVfxToRadius
    {
        get
        {
            return scaleVfxToRadius;
        }
    }

    public float VfxScaleMultiplier
    {
        get
        {
            return vfxScaleMultiplier;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (cooldownSeconds < 0f)
            cooldownSeconds = 0f;

        if (radius < 0f)
            radius = 0f;

        if (damage < 0f)
            damage = 0f;

        if (float.IsNaN(triggerOffset.x) ||
            float.IsNaN(triggerOffset.y) ||
            float.IsNaN(triggerOffset.z) ||
            float.IsInfinity(triggerOffset.x) ||
            float.IsInfinity(triggerOffset.y) ||
            float.IsInfinity(triggerOffset.z))
        {
            triggerOffset = Vector3.zero;
        }

        if (vfxScaleMultiplier < 0.01f)
            vfxScaleMultiplier = 0.01f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class ElementalTrailPassiveToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Elemental Trail")]
    [Tooltip("Elemental effect payload applied by trail segments.")]
    [SerializeField] private ElementalEffectDefinitionData effectData = new ElementalEffectDefinitionData();

    [Tooltip("Seconds each trail segment remains active in the world.")]
    [SerializeField] private float trailSegmentLifetimeSeconds = 1.5f;

    [Tooltip("Distance in meters required to spawn the next segment.")]
    [SerializeField] private float trailSpawnDistance = 0.6f;

    [Tooltip("Fallback time interval in seconds to spawn segment even with low movement.")]
    [SerializeField] private float trailSpawnIntervalSeconds = 0.1f;

    [Tooltip("Radius in meters used by each trail segment to detect enemies.")]
    [SerializeField] private float trailRadius = 1.2f;

    [Tooltip("Maximum active trail segments per player.")]
    [SerializeField] private int maxActiveSegmentsPerPlayer = 30;

    [Tooltip("Stacks applied on each trail damage tick.")]
    [SerializeField] private float stacksPerTick = 1f;

    [Tooltip("Seconds between two stack applications while an enemy remains inside trail area.")]
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

    #region Validation
    public void Validate()
    {
        if (effectData == null)
            effectData = new ElementalEffectDefinitionData();

        effectData.Validate();

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

        if (stacksPerTick < 0f)
            stacksPerTick = 0f;

        if (applyIntervalSeconds < 0.01f)
            applyIntervalSeconds = 0.01f;

    }
    #endregion

    #endregion
}

[Serializable]
public sealed class PassiveToolDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Common metadata and drop data for this passive tool.")]
    [SerializeField] private PowerUpCommonData commonData = new PowerUpCommonData();

    [Tooltip("Passive tool behavior type.")]
    [SerializeField] private PassiveToolKind toolKind = PassiveToolKind.ProjectileSize;

    [Header("Tool Specific")]
    [Tooltip("Projectile size passive settings.")]
    [SerializeField] private ProjectileSizePassiveToolData projectileSizeData = new ProjectileSizePassiveToolData();

    [Tooltip("Elemental projectile passive settings.")]
    [SerializeField] private ElementalProjectilesPassiveToolData elementalProjectilesData = new ElementalProjectilesPassiveToolData();

    [Tooltip("Perfect circle passive settings.")]
    [SerializeField] private PerfectCirclePassiveToolData perfectCircleData = new PerfectCirclePassiveToolData();

    [Tooltip("Bouncing projectile passive settings.")]
    [SerializeField] private BouncingProjectilesPassiveToolData bouncingProjectilesData = new BouncingProjectilesPassiveToolData();

    [Tooltip("Splitting projectile passive settings.")]
    [SerializeField] private SplittingProjectilesPassiveToolData splittingProjectilesData = new SplittingProjectilesPassiveToolData();

    [Tooltip("Explosion passive settings.")]
    [SerializeField] private ExplosionPassiveToolData explosionData = new ExplosionPassiveToolData();

    [Tooltip("Elemental trail passive settings.")]
    [SerializeField] private ElementalTrailPassiveToolData elementalTrailData = new ElementalTrailPassiveToolData();
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

    public PassiveToolKind ToolKind
    {
        get
        {
            return toolKind;
        }
    }

    public ProjectileSizePassiveToolData ProjectileSizeData
    {
        get
        {
            return projectileSizeData;
        }
    }

    public ElementalProjectilesPassiveToolData ElementalProjectilesData
    {
        get
        {
            return elementalProjectilesData;
        }
    }

    public PerfectCirclePassiveToolData PerfectCircleData
    {
        get
        {
            return perfectCircleData;
        }
    }

    public BouncingProjectilesPassiveToolData BouncingProjectilesData
    {
        get
        {
            return bouncingProjectilesData;
        }
    }

    public SplittingProjectilesPassiveToolData SplittingProjectilesData
    {
        get
        {
            return splittingProjectilesData;
        }
    }

    public ExplosionPassiveToolData ExplosionData
    {
        get
        {
            return explosionData;
        }
    }

    public ElementalTrailPassiveToolData ElementalTrailData
    {
        get
        {
            return elementalTrailData;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (commonData == null)
            commonData = new PowerUpCommonData();

        commonData.Validate();

        if (toolKind == PassiveToolKind.Custom)
            toolKind = PassiveToolKind.ProjectileSize;

        if (projectileSizeData == null)
            projectileSizeData = new ProjectileSizePassiveToolData();

        if (elementalProjectilesData == null)
            elementalProjectilesData = new ElementalProjectilesPassiveToolData();

        if (perfectCircleData == null)
            perfectCircleData = new PerfectCirclePassiveToolData();

        if (bouncingProjectilesData == null)
            bouncingProjectilesData = new BouncingProjectilesPassiveToolData();

        if (splittingProjectilesData == null)
            splittingProjectilesData = new SplittingProjectilesPassiveToolData();

        if (explosionData == null)
            explosionData = new ExplosionPassiveToolData();

        if (elementalTrailData == null)
            elementalTrailData = new ElementalTrailPassiveToolData();

        projectileSizeData.Validate();
        elementalProjectilesData.Validate();
        perfectCircleData.Validate();
        bouncingProjectilesData.Validate();
        splittingProjectilesData.Validate();
        explosionData.Validate();
        elementalTrailData.Validate();
    }
    #endregion

    #endregion
}
#endregion

#region Active
[Serializable]
public sealed class BombToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Bomb")]
    [Tooltip("Prefab spawned when this bomb tool is activated.")]
    [SerializeField] private GameObject bombPrefab;

    [Tooltip("Local-space spawn offset from player origin, rotated by player rotation at activation time.")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, 1.2f);

    [Tooltip("Initial planar speed applied to the bomb when deployed.")]
    [SerializeField] private float deploySpeed = 4.5f;

    [Tooltip("Collision radius used for bomb wall interaction.")]
    [SerializeField] private float collisionRadius = 0.18f;

    [Tooltip("When enabled, the bomb bounces on wall impact instead of stopping.")]
    [SerializeField] private bool bounceOnWalls = true;

    [Tooltip("Velocity multiplier applied after each wall bounce. Range: 0 to 1.")]
    [SerializeField] private float bounceDamping = 0.65f;

    [Tooltip("Linear speed damping applied every second while the bomb moves.")]
    [SerializeField] private float linearDampingPerSecond = 1.2f;

    [Tooltip("Fuse duration in seconds before the bomb explodes.")]
    [SerializeField] private float fuseSeconds = 1.25f;

    [Tooltip("Explosion radius applied when the bomb detonates.")]
    [SerializeField] private float radius = 7f;

    [Tooltip("Damage dealt to enemies inside the explosion radius.")]
    [SerializeField] private float damage = 120f;

    [Tooltip("When enabled, all enemies in radius are affected by the explosion.")]
    [SerializeField] private bool affectAllEnemiesInRadius = true;
    #endregion

    #endregion

    #region Properties
    public GameObject BombPrefab
    {
        get
        {
            return bombPrefab;
        }
    }

    public Vector3 SpawnOffset
    {
        get
        {
            return spawnOffset;
        }
    }

    public float DeploySpeed
    {
        get
        {
            return deploySpeed;
        }
    }

    public float CollisionRadius
    {
        get
        {
            return collisionRadius;
        }
    }

    public bool BounceOnWalls
    {
        get
        {
            return bounceOnWalls;
        }
    }

    public float BounceDamping
    {
        get
        {
            return bounceDamping;
        }
    }

    public float LinearDampingPerSecond
    {
        get
        {
            return linearDampingPerSecond;
        }
    }

    public float FuseSeconds
    {
        get
        {
            return fuseSeconds;
        }
    }

    public float Radius
    {
        get
        {
            return radius;
        }
    }

    public float Damage
    {
        get
        {
            return damage;
        }
    }

    public bool AffectAllEnemiesInRadius
    {
        get
        {
            return affectAllEnemiesInRadius;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (float.IsNaN(spawnOffset.x) ||
            float.IsNaN(spawnOffset.y) ||
            float.IsNaN(spawnOffset.z) ||
            float.IsInfinity(spawnOffset.x) ||
            float.IsInfinity(spawnOffset.y) ||
            float.IsInfinity(spawnOffset.z))
        {
            spawnOffset = new Vector3(0f, 0f, 1.2f);
        }

        if (deploySpeed < 0f)
            deploySpeed = 0f;

        if (collisionRadius < 0.01f)
            collisionRadius = 0.01f;

        if (bounceDamping < 0f)
            bounceDamping = 0f;

        if (bounceDamping > 1f)
            bounceDamping = 1f;

        if (linearDampingPerSecond < 0f)
            linearDampingPerSecond = 0f;

        if (fuseSeconds < 0.05f)
            fuseSeconds = 0.05f;

        if (radius < 0.1f)
            radius = 0.1f;

        if (damage < 0f)
            damage = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class DashToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Dash")]
    [Tooltip("Distance covered by the dash movement.")]
    [SerializeField] private float distance = 6f;

    [Tooltip("Duration in seconds used to complete the dash movement.")]
    [SerializeField] private float duration = 0.18f;

    [Tooltip("Seconds used to blend from current movement speed to dash speed.")]
    [SerializeField] private float speedTransitionInSeconds = 0.06f;

    [Tooltip("Seconds used to blend from dash speed back to current movement speed.")]
    [SerializeField] private float speedTransitionOutSeconds = 0.08f;

    [Tooltip("When enabled, the player ignores damage during the dash.")]
    [SerializeField] private bool grantsInvulnerability = true;

    [Tooltip("Extra invulnerability time after dash end.")]
    [SerializeField] private float invulnerabilityExtraTime = 0.1f;
    #endregion

    #endregion

    #region Properties
    public float Distance
    {
        get
        {
            return distance;
        }
    }

    public float Duration
    {
        get
        {
            return duration;
        }
    }

    public bool GrantsInvulnerability
    {
        get
        {
            return grantsInvulnerability;
        }
    }

    public float SpeedTransitionInSeconds
    {
        get
        {
            return speedTransitionInSeconds;
        }
    }

    public float SpeedTransitionOutSeconds
    {
        get
        {
            return speedTransitionOutSeconds;
        }
    }

    public float InvulnerabilityExtraTime
    {
        get
        {
            return invulnerabilityExtraTime;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (distance < 0f)
            distance = 0f;

        if (duration < 0.01f)
            duration = 0.01f;

        if (speedTransitionInSeconds < 0f)
            speedTransitionInSeconds = 0f;

        if (speedTransitionOutSeconds < 0f)
            speedTransitionOutSeconds = 0f;

        if (invulnerabilityExtraTime < 0f)
            invulnerabilityExtraTime = 0f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class BulletTimeToolData
{
    #region Fields

    #region Serialized Fields
    [Header("Bullet Time")]
    [Tooltip("Duration in seconds while enemy simulation is slowed.")]
    [SerializeField] private float duration = 3f;

    [Tooltip("Enemy slowdown percentage while Bullet Time is active. 0 means no slowdown, 100 means full stop.")]
    [SerializeField] private float enemySlowPercent = 40f;
    #endregion

    #endregion

    #region Properties
    public float Duration
    {
        get
        {
            return duration;
        }
    }

    public float EnemySlowPercent
    {
        get
        {
            return enemySlowPercent;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (duration < 0.05f)
            duration = 0.05f;

        if (enemySlowPercent < 0f)
            enemySlowPercent = 0f;

        if (enemySlowPercent > 100f)
            enemySlowPercent = 100f;
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class ActiveToolDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Common metadata and drop data for this active tool.")]
    [SerializeField] private PowerUpCommonData commonData = new PowerUpCommonData();

    [Tooltip("Active tool behavior type.")]
    [SerializeField] private ActiveToolKind toolKind = ActiveToolKind.Bomb;

    [Header("Resources")]
    [Tooltip("Maximum energy reserve for this tool. Set 0 for tools that do not use energy.")]
    [SerializeField] private float maximumEnergy = 100f;

    [Tooltip("When enabled, activation toggles ON/OFF state instead of one-shot behavior.")]
    [SerializeField] private bool toggleable;

    [Tooltip("Resource consumed on activation.")]
    [SerializeField] private PowerUpResourceType activationResource = PowerUpResourceType.Energy;

    [Tooltip("Amount of ActivationResource consumed when tool is activated.")]
    [SerializeField] private float activationCost = 25f;

    [Tooltip("Resource consumed each second while toggleable tool remains active.")]
    [SerializeField] private PowerUpResourceType maintenanceResource = PowerUpResourceType.Energy;

    [Tooltip("Amount consumed per second while toggleable tool remains active.")]
    [SerializeField] private float maintenanceCostPerSecond;

    [Tooltip("Event type that grants recharge to this tool.")]
    [SerializeField] private PowerUpChargeType chargeType = PowerUpChargeType.EnemiesDestroyed;

    [Tooltip("Recharge amount granted for each charge event.")]
    [SerializeField] private float chargePerTrigger = 10f;

    [Tooltip("When enabled, activation requires full maximum energy.")]
    [SerializeField] private bool fullChargeRequirement;

    [Tooltip("When enabled, this tool cannot be replaced from slots.")]
    [SerializeField] private bool unreplaceable;

    [Header("Tool Specific")]
    [Tooltip("Bomb-specific payload data.")]
    [SerializeField] private BombToolData bombData = new BombToolData();

    [Tooltip("Dash-specific payload data.")]
    [SerializeField] private DashToolData dashData = new DashToolData();

    [Tooltip("Bullet Time-specific payload data.")]
    [SerializeField] private BulletTimeToolData bulletTimeData = new BulletTimeToolData();
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

    public ActiveToolKind ToolKind
    {
        get
        {
            return toolKind;
        }
    }

    public float MaximumEnergy
    {
        get
        {
            return maximumEnergy;
        }
    }

    public bool Toggleable
    {
        get
        {
            return toggleable;
        }
    }

    public PowerUpResourceType ActivationResource
    {
        get
        {
            return activationResource;
        }
    }

    public float ActivationCost
    {
        get
        {
            return activationCost;
        }
    }

    public PowerUpResourceType MaintenanceResource
    {
        get
        {
            return maintenanceResource;
        }
    }

    public float MaintenanceCostPerSecond
    {
        get
        {
            return maintenanceCostPerSecond;
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

    public bool FullChargeRequirement
    {
        get
        {
            return fullChargeRequirement;
        }
    }

    public bool Unreplaceable
    {
        get
        {
            return unreplaceable;
        }
    }

    public BombToolData BombData
    {
        get
        {
            return bombData;
        }
    }

    public DashToolData DashData
    {
        get
        {
            return dashData;
        }
    }

    public BulletTimeToolData BulletTimeData
    {
        get
        {
            return bulletTimeData;
        }
    }
    #endregion

    #region Methods

    #region Validation
    public void Validate()
    {
        if (commonData == null)
            commonData = new PowerUpCommonData();

        commonData.Validate();

        if (toolKind == ActiveToolKind.Custom)
            toolKind = ActiveToolKind.Bomb;

        if (maximumEnergy < 0f)
            maximumEnergy = 0f;

        if (activationCost < 0f)
            activationCost = 0f;

        if (maintenanceCostPerSecond < 0f)
            maintenanceCostPerSecond = 0f;

        if (chargePerTrigger < 0f)
            chargePerTrigger = 0f;

        if (fullChargeRequirement && maximumEnergy <= 0f)
            fullChargeRequirement = false;

        if (bombData == null)
            bombData = new BombToolData();

        if (dashData == null)
            dashData = new DashToolData();

        if (bulletTimeData == null)
            bulletTimeData = new BulletTimeToolData();

        bombData.Validate();
        dashData.Validate();
        bulletTimeData.Validate();
    }
    #endregion

    #endregion
}
#endregion

#region Preset
[CreateAssetMenu(fileName = "PlayerPowerUpsPreset", menuName = "Player/Power Ups Preset", order = 13)]
public sealed class PlayerPowerUpsPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this power ups preset.")]
    [FormerlySerializedAs("m_PresetId")]
    [SerializeField] private string presetId;

    [Tooltip("Human-readable power ups preset name for designers.")]
    [FormerlySerializedAs("m_PresetName")]
    [SerializeField] private string presetName = "New Power Ups Preset";

    [Tooltip("Description of the preset intent and usage.")]
    [FormerlySerializedAs("m_Description")]
    [SerializeField] private string description;

    [Tooltip("Semantic version of this preset.")]
    [FormerlySerializedAs("m_Version")]
    [SerializeField] private string version = "1.0.0";

    [Header("Input")]
    [Tooltip("Input Action ID used for the primary active tool slot.")]
    [SerializeField] private string primaryToolActionId;

    [Tooltip("Input Action ID used for the secondary active tool slot.")]
    [SerializeField] private string secondaryToolActionId;

    [Header("Drop Pools")]
    [Tooltip("Global pool catalog available for power-up entries.")]
    [SerializeField] private List<string> dropPoolCatalog = new List<string>
    {
        "Milestone",
        "Shop",
        "Boss"
    };

    [Header("Passive Tools")]
    [Tooltip("Passive tools available in this preset.")]
    [FormerlySerializedAs("passiveModifiers")]
    [SerializeField] private List<PassiveToolDefinition> passiveTools = new List<PassiveToolDefinition>();

    [Header("Active Tools")]
    [Tooltip("Active tools available in this preset.")]
    [SerializeField] private List<ActiveToolDefinition> activeTools = new List<ActiveToolDefinition>();

    [Header("Loadout")]
    [Tooltip("PowerUpId assigned to primary slot at runtime initialization.")]
    [SerializeField] private string primaryActiveToolId;

    [Tooltip("PowerUpId assigned to secondary slot at runtime initialization.")]
    [SerializeField] private string secondaryActiveToolId;

    [Tooltip("PowerUpId list assigned as equipped passive tools at runtime initialization.")]
    [SerializeField] private List<string> equippedPassiveToolIds = new List<string>();

    [Tooltip("Legacy field used to migrate old primary passive loadout data.")]
    [FormerlySerializedAs("primaryPassiveToolId")]
    [HideInInspector]
    [SerializeField] private string legacyPrimaryPassiveToolId;

    [Tooltip("Legacy field used to migrate old secondary passive loadout data.")]
    [FormerlySerializedAs("secondaryPassiveToolId")]
    [HideInInspector]
    [SerializeField] private string legacySecondaryPassiveToolId;
    #endregion

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public string PrimaryToolActionId
    {
        get
        {
            return primaryToolActionId;
        }
    }

    public string SecondaryToolActionId
    {
        get
        {
            return secondaryToolActionId;
        }
    }

    public IReadOnlyList<string> DropPoolCatalog
    {
        get
        {
            return dropPoolCatalog;
        }
    }

    public IReadOnlyList<PassiveToolDefinition> PassiveTools
    {
        get
        {
            return passiveTools;
        }
    }

    public IReadOnlyList<ActiveToolDefinition> ActiveTools
    {
        get
        {
            return activeTools;
        }
    }

    public string PrimaryActiveToolId
    {
        get
        {
            return primaryActiveToolId;
        }
    }

    public string SecondaryActiveToolId
    {
        get
        {
            return secondaryActiveToolId;
        }
    }

    public IReadOnlyList<string> EquippedPassiveToolIds
    {
        get
        {
            return equippedPassiveToolIds;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnValidate()
    {
        ValidateMetadata();
        ValidateCollections();
        ValidateEntries();
    }
    #endregion

    #region Validation
    private void ValidateMetadata()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(presetName))
            presetName = "New Power Ups Preset";

        if (string.IsNullOrWhiteSpace(version))
            version = "1.0.0";

        if (string.IsNullOrWhiteSpace(primaryToolActionId))
            primaryToolActionId = "PowerUpPrimary";

        if (string.IsNullOrWhiteSpace(secondaryToolActionId))
            secondaryToolActionId = "PowerUpSecondary";
    }

    private void ValidateCollections()
    {
        if (dropPoolCatalog == null)
            dropPoolCatalog = new List<string>();

        if (passiveTools == null)
            passiveTools = new List<PassiveToolDefinition>();

        if (activeTools == null)
            activeTools = new List<ActiveToolDefinition>();

        if (equippedPassiveToolIds == null)
            equippedPassiveToolIds = new List<string>();

        if (dropPoolCatalog.Count == 0)
        {
            dropPoolCatalog.Add("Milestone");
            dropPoolCatalog.Add("Shop");
            dropPoolCatalog.Add("Boss");
        }
    }

    private void ValidateEntries()
    {
        for (int index = 0; index < passiveTools.Count; index++)
        {
            PassiveToolDefinition passiveTool = passiveTools[index];

            if (passiveTool == null)
                continue;

            passiveTool.Validate();
        }

        for (int index = 0; index < activeTools.Count; index++)
        {
            ActiveToolDefinition activeTool = activeTools[index];

            if (activeTool == null)
                continue;

            activeTool.Validate();
        }

        if (string.IsNullOrWhiteSpace(primaryActiveToolId) == false &&
            HasActiveToolWithId(primaryActiveToolId) == false)
            primaryActiveToolId = string.Empty;

        if (string.IsNullOrWhiteSpace(secondaryActiveToolId) == false &&
            HasActiveToolWithId(secondaryActiveToolId) == false)
            secondaryActiveToolId = string.Empty;

        if (string.IsNullOrWhiteSpace(primaryActiveToolId) && activeTools.Count > 0)
            primaryActiveToolId = GetFirstValidActiveToolId();

        if (string.IsNullOrWhiteSpace(secondaryActiveToolId) && activeTools.Count > 1)
            secondaryActiveToolId = GetSecondValidActiveToolId();

        ValidateEquippedPassiveToolIds();
    }

    private bool HasActiveToolWithId(string powerUpId)
    {
        if (string.IsNullOrWhiteSpace(powerUpId))
            return false;

        for (int index = 0; index < activeTools.Count; index++)
        {
            ActiveToolDefinition activeTool = activeTools[index];

            if (activeTool == null)
                continue;

            PowerUpCommonData commonData = activeTool.CommonData;

            if (commonData == null)
                continue;

            if (string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase) == false)
                continue;

            return true;
        }

        return false;
    }

    private string GetFirstValidActiveToolId()
    {
        for (int index = 0; index < activeTools.Count; index++)
        {
            ActiveToolDefinition activeTool = activeTools[index];

            if (activeTool == null)
                continue;

            PowerUpCommonData commonData = activeTool.CommonData;

            if (commonData == null)
                continue;

            if (string.IsNullOrWhiteSpace(commonData.PowerUpId))
                continue;

            return commonData.PowerUpId;
        }

        return string.Empty;
    }

    private string GetSecondValidActiveToolId()
    {
        int foundCount = 0;

        for (int index = 0; index < activeTools.Count; index++)
        {
            ActiveToolDefinition activeTool = activeTools[index];

            if (activeTool == null)
                continue;

            PowerUpCommonData commonData = activeTool.CommonData;

            if (commonData == null)
                continue;

            if (string.IsNullOrWhiteSpace(commonData.PowerUpId))
                continue;

            foundCount++;

            if (foundCount < 2)
                continue;

            return commonData.PowerUpId;
        }

        return string.Empty;
    }

    private bool HasPassiveToolWithId(string powerUpId)
    {
        if (string.IsNullOrWhiteSpace(powerUpId))
            return false;

        for (int index = 0; index < passiveTools.Count; index++)
        {
            PassiveToolDefinition passiveTool = passiveTools[index];

            if (passiveTool == null)
                continue;

            PowerUpCommonData commonData = passiveTool.CommonData;

            if (commonData == null)
                continue;

            if (string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase) == false)
                continue;

            return true;
        }

        return false;
    }

    private string GetFirstValidPassiveToolId()
    {
        for (int index = 0; index < passiveTools.Count; index++)
        {
            PassiveToolDefinition passiveTool = passiveTools[index];

            if (passiveTool == null)
                continue;

            PowerUpCommonData commonData = passiveTool.CommonData;

            if (commonData == null)
                continue;

            if (string.IsNullOrWhiteSpace(commonData.PowerUpId))
                continue;

            return commonData.PowerUpId;
        }

        return string.Empty;
    }

    private void ValidateEquippedPassiveToolIds()
    {
        MigrateLegacyPassiveToolIds();

        if (equippedPassiveToolIds == null)
            equippedPassiveToolIds = new List<string>();

        HashSet<string> equippedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < equippedPassiveToolIds.Count; index++)
        {
            string equippedPassiveToolId = equippedPassiveToolIds[index];

            if (string.IsNullOrWhiteSpace(equippedPassiveToolId))
            {
                equippedPassiveToolIds.RemoveAt(index);
                index--;
                continue;
            }

            if (HasPassiveToolWithId(equippedPassiveToolId) == false)
            {
                equippedPassiveToolIds.RemoveAt(index);
                index--;
                continue;
            }

            if (equippedIds.Add(equippedPassiveToolId))
                continue;

            equippedPassiveToolIds.RemoveAt(index);
            index--;
        }

        if (equippedPassiveToolIds.Count > 0)
            return;

        string firstValidPassiveToolId = GetFirstValidPassiveToolId();

        if (string.IsNullOrWhiteSpace(firstValidPassiveToolId))
            return;

        equippedPassiveToolIds.Add(firstValidPassiveToolId);
    }

    private void MigrateLegacyPassiveToolIds()
    {
        if (equippedPassiveToolIds == null)
            equippedPassiveToolIds = new List<string>();

        if (equippedPassiveToolIds.Count > 0)
        {
            ClearLegacyPassiveToolIds();
            return;
        }

        TryAppendLegacyPassiveToolId(legacyPrimaryPassiveToolId);
        TryAppendLegacyPassiveToolId(legacySecondaryPassiveToolId);
        ClearLegacyPassiveToolIds();
    }

    private void TryAppendLegacyPassiveToolId(string legacyPassiveToolId)
    {
        if (string.IsNullOrWhiteSpace(legacyPassiveToolId))
            return;

        if (HasPassiveToolWithId(legacyPassiveToolId) == false)
            return;

        for (int index = 0; index < equippedPassiveToolIds.Count; index++)
        {
            if (string.Equals(equippedPassiveToolIds[index], legacyPassiveToolId, StringComparison.OrdinalIgnoreCase))
                return;
        }

        equippedPassiveToolIds.Add(legacyPassiveToolId);
    }

    private void ClearLegacyPassiveToolIds()
    {
        legacyPrimaryPassiveToolId = string.Empty;
        legacySecondaryPassiveToolId = string.Empty;
    }
    #endregion

    #endregion
}
#endregion
