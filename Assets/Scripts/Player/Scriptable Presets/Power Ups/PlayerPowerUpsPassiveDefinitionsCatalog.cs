using System;
using UnityEngine;

[Serializable]
public sealed class ElementalVfxByElementData
{
    #region Fields

    #region Serialized Fields
    [Header("Element")]
    [Tooltip("Element type associated with this VFX assignment.")]
    [SerializeField] private ElementType elementType = ElementType.Fire;

    [Header("Stack VFX (Optional)")]
    [Tooltip("When enabled, stack VFX is spawned when this element applies stacks.")]
    [SerializeField] private bool spawnStackVfx;

    [Tooltip("Optional prefab used for stack VFX of this element.")]
    [SerializeField] private GameObject stackVfxPrefab;

    [Tooltip("Scale multiplier applied to stack VFX for this element.")]
    [SerializeField] private float stackVfxScaleMultiplier = 1f;

    [Header("Proc VFX (Optional)")]
    [Tooltip("When enabled, proc VFX is spawned when this element reaches proc threshold.")]
    [SerializeField] private bool spawnProcVfx;

    [Tooltip("Optional prefab used for proc VFX of this element.")]
    [SerializeField] private GameObject procVfxPrefab;

    [Tooltip("Scale multiplier applied to proc VFX for this element.")]
    [SerializeField] private float procVfxScaleMultiplier = 1f;
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

    #region Setup
    public void SetElementType(ElementType value)
    {
        elementType = value;
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (stackVfxScaleMultiplier < 0.01f)
            stackVfxScaleMultiplier = 0.01f;

        if (procVfxScaleMultiplier < 0.01f)
            procVfxScaleMultiplier = 0.01f;
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
