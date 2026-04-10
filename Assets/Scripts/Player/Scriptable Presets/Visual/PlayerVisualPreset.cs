using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
/// <summary>
/// Stores player outline presentation settings applied to managed renderers.
/// returns None.
/// </summary>
[Serializable]
public sealed class PlayerVisualOutlineSettings
{
    #region Constants
    private const float MinimumOutlineThickness = 0f;
    private const float MaximumOutlineThickness = 10f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, compatible player renderers receive outline property overrides from this preset.")]
    [SerializeField] private bool enableOutline = true;

    [Tooltip("Outline thickness written to compatible player materials exposing _OutlineThickness. Matches the shader authoring range 0-10.")]
    [Range(MinimumOutlineThickness, MaximumOutlineThickness)]
    [SerializeField] private float outlineThickness = 1f;

    [Tooltip("Outline color written to compatible player materials exposing _OutlineColor.")]
    [SerializeField] private Color outlineColor = Color.black;
    #endregion

    #endregion

    #region Properties
    public bool EnableOutline
    {
        get
        {
            return enableOutline;
        }
    }

    public float OutlineThickness
    {
        get
        {
            return outlineThickness;
        }
    }

    public Color OutlineColor
    {
        get
        {
            return outlineColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates outline authored values after inspector edits.
    /// None.
    /// returns None.
    /// </summary>
    public void Validate()
    {
        outlineColor.a = Mathf.Clamp01(outlineColor.a);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores the editable colors used by one Laser Beam palette entry.
/// returns None.
/// </summary>
[Serializable]
public sealed class PlayerLaserBeamPaletteSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Laser Beam palette enum served by this entry.")]
    [SerializeField] private LaserBeamVisualPalette visualPalette = LaserBeamVisualPalette.AntibioticBlue;

    [Tooltip("Primary liquid body color sent to the beam shader gradient.")]
    [SerializeField] private Color bodyColorA = Color.white;

    [Tooltip("Secondary liquid body color sent to the beam shader gradient.")]
    [SerializeField] private Color bodyColorB = Color.white;

    [Tooltip("Bright central highlight color used by the liquid beam core.")]
    [SerializeField] private Color coreColor = Color.white;

    [Tooltip("Outer contour color used by the beam rim and impact bloom.")]
    [SerializeField] private Color rimColor = Color.white;
    #endregion

    #endregion

    #region Properties
    public LaserBeamVisualPalette VisualPalette
    {
        get
        {
            return visualPalette;
        }
    }

    public Color BodyColorA
    {
        get
        {
            return bodyColorA;
        }
    }

    public Color BodyColorB
    {
        get
        {
            return bodyColorB;
        }
    }

    public Color CoreColor
    {
        get
        {
            return coreColor;
        }
    }

    public Color RimColor
    {
        get
        {
            return rimColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Overwrites this entry with one complete palette assignment.
    /// None.
    /// returns None.
    /// </summary>
    public void Assign(LaserBeamVisualPalette paletteValue,
                       Color bodyColorAValue,
                       Color bodyColorBValue,
                       Color coreColorValue,
                       Color rimColorValue)
    {
        visualPalette = paletteValue;
        bodyColorA = bodyColorAValue;
        bodyColorB = bodyColorBValue;
        coreColor = coreColorValue;
        rimColor = rimColorValue;
        Validate();
    }

    /// <summary>
    /// Clamps authored palette colors to a valid alpha range.
    /// None.
    /// returns None.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores editable shared assets and palette mappings used by the Laser Beam managed presentation runtime.
/// returns None.
/// </summary>
[Serializable]
public sealed class PlayerLaserBeamVisualSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Material asset used by Laser Beam body and cap visuals. Assign the NashCore liquid beam material or a compatible replacement exposing the same shader properties.")]
    [SerializeField] private Material beamMaterial;

    [Tooltip("Material asset used by the bubble cluster visuals rendered near the emission origin.")]
    [SerializeField] private Material sourceBubbleMaterial;

    [Tooltip("Material asset used by the splash visuals rendered at the terminal point of each lane.")]
    [SerializeField] private Material impactSplashMaterial;

    [Tooltip("Vertical lift in world units applied to Laser Beam visuals to avoid floor z-fighting.")]
    [SerializeField] private float verticalLift = PlayerLaserBeamVisualDefaultsUtility.DefaultVerticalLift;

    [Tooltip("Minimum rendered segment length used by the pooled Laser Beam body visuals.")]
    [SerializeField] private float minimumSegmentLength = PlayerLaserBeamVisualDefaultsUtility.DefaultMinimumSegmentLength;

    [Tooltip("Editable color mappings used to translate Laser Beam palette enums into actual shader colors.")]
    [SerializeField] private List<PlayerLaserBeamPaletteSettings> palettes = new List<PlayerLaserBeamPaletteSettings>();
    #endregion

    #endregion

    #region Properties
    public Material BeamMaterial
    {
        get
        {
            return beamMaterial;
        }
    }

    public float VerticalLift
    {
        get
        {
            return verticalLift;
        }
    }

    public float MinimumSegmentLength
    {
        get
        {
            return minimumSegmentLength;
        }
    }

    public IReadOnlyList<PlayerLaserBeamPaletteSettings> Palettes
    {
        get
        {
            return palettes;
        }
    }

    public Material SourceBubbleMaterial
    {
        get
        {
            return sourceBubbleMaterial;
        }
    }

    public Material ImpactSplashMaterial
    {
        get
        {
            return impactSplashMaterial;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates material, distances, and palette coverage for Laser Beam visuals.
    /// None.
    /// returns None.
    /// </summary>
    public void Validate()
    {
        if (palettes == null)
            palettes = new List<PlayerLaserBeamPaletteSettings>();

#if UNITY_EDITOR
        if (beamMaterial == null)
            beamMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultBodyMaterialPath);

        if (sourceBubbleMaterial == null)
            sourceBubbleMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultSourceBubbleMaterialPath);

        if (impactSplashMaterial == null)
            impactSplashMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultImpactSplashMaterialPath);
#endif

        PlayerLaserBeamVisualDefaultsUtility.EnsurePaletteCoverage(palettes);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores runtime bridge, damage feedback and player-facing power-up VFX settings shared by one visual setup.
/// returns None.
/// </summary>
[CreateAssetMenu(fileName = "PlayerVisualPreset", menuName = "Player/Visual Preset", order = 10)]
public sealed class PlayerVisualPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this visual preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Visual preset name shown in the Player Management Tool.")]
    [SerializeField] private string presetName = "New Player Visual Preset";

    [Tooltip("Short description of the visual setup handled by this preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this visual preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Runtime Bridge")]
    [Tooltip("Optional visual-only prefab instantiated at runtime when no valid companion Animator exists on the player entity.")]
    [SerializeField] private GameObject runtimeVisualBridgePrefab;

    [Tooltip("When enabled, the runtime visual bridge is spawned only when the player entity has no valid Animator companion.")]
    [SerializeField] private bool spawnRuntimeVisualBridgeWhenAnimatorMissing = true;

    [Tooltip("When enabled, the runtime visual bridge follows the ECS player rotation.")]
    [SerializeField] private bool runtimeVisualBridgeSyncRotation = true;

    [Tooltip("Local-space position offset applied to the runtime visual bridge relative to the ECS player transform.")]
    [SerializeField] private Vector3 runtimeVisualBridgeOffset = Vector3.zero;

    [Header("Outline")]
    [Tooltip("Outline settings applied to compatible player renderers.")]
    [SerializeField] private PlayerVisualOutlineSettings outline = new PlayerVisualOutlineSettings();

    [Header("Damage Feedback")]
    [Tooltip("Tint color applied during the brief damage flash after the player receives valid damage.")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("Flash duration in seconds. Use very small values for a 1-3 frame reaction.")]
    [SerializeField] private float damageFlashDurationSeconds = 0.06f;

    [Tooltip("Maximum overlay strength reached immediately after a valid hit.")]
    [SerializeField] private float damageFlashMaximumBlend = 0.85f;

    [Header("Power-Ups VFX")]
    [Tooltip("Optional attached VFX prefab activated while Elemental Trail passive is enabled.")]
    [SerializeField] private GameObject elementalTrailAttachedVfxPrefab;

    [Tooltip("Scale multiplier applied to the attached Elemental Trail VFX instance.")]
    [SerializeField] private float elementalTrailAttachedVfxScaleMultiplier = 1f;

    [Tooltip("Per-element enemy VFX assignments used when elemental player bullets or trail effects apply stacks and procs.")]
    [SerializeField] private List<ElementalVfxByElementData> elementalEnemyVfxByElement = new List<ElementalVfxByElementData>();

    [Tooltip("Maximum number of identical one-shot VFX allowed in the same spatial cell. Set 0 to disable this cap.")]
    [SerializeField] private int maxIdenticalOneShotVfxPerCell = 1;

    [Tooltip("Cell size in meters used by the one-shot VFX per-cell cap.")]
    [SerializeField] private float oneShotVfxCellSize = 1.5f;

    [Tooltip("Maximum number of identical attached elemental VFX allowed on the same target. Set 0 to disable this cap.")]
    [SerializeField] private int maxAttachedElementalVfxPerTarget = 1;

    [Tooltip("Maximum number of active one-shot power-up VFX managed by one player. Set 0 to disable this cap.")]
    [SerializeField] private int maxActiveOneShotPowerUpVfx = 300;

    [Tooltip("When enabled, hitting the attached-target cap refreshes lifetime of the existing VFX.")]
    [SerializeField] private bool refreshAttachedElementalVfxLifetimeOnCapHit = true;

    [Tooltip("Shared material and palette settings used by the Laser Beam managed visuals.")]
    [SerializeField] private PlayerLaserBeamVisualSettings laserBeam = new PlayerLaserBeamVisualSettings();
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

    public GameObject RuntimeVisualBridgePrefab
    {
        get
        {
            return runtimeVisualBridgePrefab;
        }
    }

    public bool SpawnRuntimeVisualBridgeWhenAnimatorMissing
    {
        get
        {
            return spawnRuntimeVisualBridgeWhenAnimatorMissing;
        }
    }

    public bool RuntimeVisualBridgeSyncRotation
    {
        get
        {
            return runtimeVisualBridgeSyncRotation;
        }
    }

    public Vector3 RuntimeVisualBridgeOffset
    {
        get
        {
            return runtimeVisualBridgeOffset;
        }
    }

    public PlayerVisualOutlineSettings Outline
    {
        get
        {
            return outline;
        }
    }

    public Color DamageFlashColor
    {
        get
        {
            return damageFlashColor;
        }
    }

    public float DamageFlashDurationSeconds
    {
        get
        {
            return damageFlashDurationSeconds;
        }
    }

    public float DamageFlashMaximumBlend
    {
        get
        {
            return damageFlashMaximumBlend;
        }
    }

    public GameObject ElementalTrailAttachedVfxPrefab
    {
        get
        {
            return elementalTrailAttachedVfxPrefab;
        }
    }

    public float ElementalTrailAttachedVfxScaleMultiplier
    {
        get
        {
            return elementalTrailAttachedVfxScaleMultiplier;
        }
    }

    public IReadOnlyList<ElementalVfxByElementData> ElementalEnemyVfxByElement
    {
        get
        {
            return elementalEnemyVfxByElement;
        }
    }

    public int MaxIdenticalOneShotVfxPerCell
    {
        get
        {
            return maxIdenticalOneShotVfxPerCell;
        }
    }

    public float OneShotVfxCellSize
    {
        get
        {
            return oneShotVfxCellSize;
        }
    }

    public int MaxAttachedElementalVfxPerTarget
    {
        get
        {
            return maxAttachedElementalVfxPerTarget;
        }
    }

    public int MaxActiveOneShotPowerUpVfx
    {
        get
        {
            return maxActiveOneShotPowerUpVfx;
        }
    }

    public bool RefreshAttachedElementalVfxLifetimeOnCapHit
    {
        get
        {
            return refreshAttachedElementalVfxLifetimeOnCapHit;
        }
    }

    public PlayerLaserBeamVisualSettings LaserBeam
    {
        get
        {
            return laserBeam;
        }
    }
    #endregion

    #region Methods

    #region Internal API
    internal List<ElementalVfxByElementData> ElementalEnemyVfxByElementMutable
    {
        get
        {
            return elementalEnemyVfxByElement;
        }
        set
        {
            elementalEnemyVfxByElement = value;
        }
    }

    /// <summary>
    /// Imports legacy elemental enemy VFX assignments from another preset when this visual preset does not already own authored data.
    /// /params sourceAssignments Legacy assignment list to copy.
    /// /returns True when the visual preset was updated.
    /// </summary>
    internal bool ImportElementalEnemyVfxAssignments(IReadOnlyList<ElementalVfxByElementData> sourceAssignments)
    {
        if (PlayerElementalVfxAssignmentUtility.HasAnyConfiguredVfx(elementalEnemyVfxByElement))
            return false;

        return PlayerElementalVfxAssignmentUtility.CopyAssignments(sourceAssignments, elementalEnemyVfxByElement);
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Ensures the preset keeps a stable ID after edits and duplications.
    /// None.
    /// returns None.
    /// </summary>
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (outline == null)
            outline = new PlayerVisualOutlineSettings();

        if (elementalEnemyVfxByElement == null)
            elementalEnemyVfxByElement = new List<ElementalVfxByElementData>();

        if (laserBeam == null)
            laserBeam = new PlayerLaserBeamVisualSettings();

        outline.Validate();
        laserBeam.Validate();
        PlayerElementalVfxAssignmentUtility.ValidateAssignments(elementalEnemyVfxByElement);
    }
    #endregion

    #endregion
}
