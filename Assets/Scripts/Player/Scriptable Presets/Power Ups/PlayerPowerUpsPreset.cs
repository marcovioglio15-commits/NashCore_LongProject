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
    Custom = 2
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
    Custom = 1
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

        projectileSizeData.Validate();
    }
    #endregion

    #endregion
}

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

        bombData.Validate();
        dashData.Validate();
    }
    #endregion

    #endregion
}
#endregion

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
