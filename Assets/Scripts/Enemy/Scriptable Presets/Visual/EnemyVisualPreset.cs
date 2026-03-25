using System;
using UnityEngine;

/// <summary>
/// Stores visibility-related presentation settings shared by one enemy type.
/// /returns None.
/// </summary>
[Serializable]
public sealed class EnemyVisualVisibilitySettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Visual runtime path: managed companion Animator (few actors) or GPU-baked playback (crowd scale).")]
    [SerializeField] private EnemyVisualMode visualMode = EnemyVisualMode.GpuBaked;

    [Tooltip("Playback speed multiplier used by both companion and GPU-baked visual paths.")]
    [SerializeField] private float visualAnimationSpeed = 1f;

    [Tooltip("Loop duration in seconds used by GPU-baked playback time wrapping.")]
    [SerializeField] private float gpuAnimationLoopDuration = 1f;

    [Tooltip("Enable distance-based visual culling while gameplay simulation remains fully active.")]
    [SerializeField] private bool enableDistanceCulling = true;

    [Tooltip("Maximum planar distance from player where visuals stay visible. Set to 0 to keep always visible.")]
    [SerializeField] private float maxVisibleDistance = 55f;

    [Tooltip("Additional distance band used to avoid visual popping when crossing the culling boundary.")]
    [SerializeField] private float visibleDistanceHysteresis = 6f;
    #endregion

    #endregion

    #region Properties
    public EnemyVisualMode VisualMode
    {
        get
        {
            return visualMode;
        }
    }

    public float VisualAnimationSpeed
    {
        get
        {
            return visualAnimationSpeed;
        }
    }

    public float GpuAnimationLoopDuration
    {
        get
        {
            return gpuAnimationLoopDuration;
        }
    }

    public bool EnableDistanceCulling
    {
        get
        {
            return enableDistanceCulling;
        }
    }

    public float MaxVisibleDistance
    {
        get
        {
            return maxVisibleDistance;
        }
    }

    public float VisibleDistanceHysteresis
    {
        get
        {
            return visibleDistanceHysteresis;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes visibility settings after asset edits.
    /// /returns None.
    /// </summary>
    public void Validate()
    {
        switch (visualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
            case EnemyVisualMode.GpuBaked:
                break;

            default:
                visualMode = EnemyVisualMode.GpuBaked;
                break;
        }

        if (visualAnimationSpeed < 0f)
            visualAnimationSpeed = 0f;

        if (gpuAnimationLoopDuration < 0.05f)
            gpuAnimationLoopDuration = 0.05f;

        if (maxVisibleDistance < 0f)
            maxVisibleDistance = 0f;

        if (visibleDistanceHysteresis < 0f)
            visibleDistanceHysteresis = 0f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores short hit-flash presentation tuning used when this enemy receives damage.
/// /returns None.
/// </summary>
[Serializable]
public sealed class EnemyVisualDamageFeedbackSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Tint color applied during the brief damage flash.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("Flash duration in seconds. Use small values for a 1-3 frame reaction.")]
    [SerializeField] private float flashDurationSeconds = 0.06f;

    [Tooltip("Maximum overlay strength reached immediately after a valid hit.")]
    [SerializeField] private float flashMaximumBlend = 0.85f;
    #endregion

    #endregion

    #region Properties
    public Color FlashColor
    {
        get
        {
            return flashColor;
        }
    }

    public float FlashDurationSeconds
    {
        get
        {
            return flashDurationSeconds;
        }
    }

    public float FlashMaximumBlend
    {
        get
        {
            return flashMaximumBlend;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes damage flash values after asset edits.
    /// /returns None.
    /// </summary>
    public void Validate()
    {
        flashColor.a = Mathf.Clamp01(flashColor.a);

        if (flashDurationSeconds < 0f)
            flashDurationSeconds = 0f;

        if (flashMaximumBlend < 0f)
            flashMaximumBlend = 0f;

        if (flashMaximumBlend > 1f)
            flashMaximumBlend = 1f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores prefab references and paint color metadata used by one enemy type.
/// /returns None.
/// </summary>
[Serializable]
public sealed class EnemyVisualPrefabSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enemy prefab associated with this enemy type. This prefab must contain EnemyAuthoring.")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("Optional one-shot VFX prefab spawned every time this enemy receives a projectile hit.")]
    [SerializeField] private GameObject hitVfxPrefab;

    [Tooltip("Lifetime in seconds assigned to each spawned hit VFX instance.")]
    [SerializeField] private float hitVfxLifetimeSeconds = 0.35f;

    [Tooltip("Uniform scale multiplier applied to the spawned hit VFX instance.")]
    [SerializeField] private float hitVfxScaleMultiplier = 1f;

    [Tooltip("Color used by the wave painter and scene preview for this enemy type.")]
    [SerializeField] private Color spawnPaintColor = new Color(1f, 0.3f, 0.3f, 1f);
    #endregion

    #endregion

    #region Properties
    public GameObject EnemyPrefab
    {
        get
        {
            return enemyPrefab;
        }
    }

    public GameObject HitVfxPrefab
    {
        get
        {
            return hitVfxPrefab;
        }
    }

    public float HitVfxLifetimeSeconds
    {
        get
        {
            return hitVfxLifetimeSeconds;
        }
    }

    public float HitVfxScaleMultiplier
    {
        get
        {
            return hitVfxScaleMultiplier;
        }
    }

    public Color SpawnPaintColor
    {
        get
        {
            return spawnPaintColor;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Sanitizes prefab settings after asset edits.
    /// /returns None.
    /// </summary>
    public void Validate()
    {
        if (float.IsNaN(hitVfxLifetimeSeconds) || float.IsInfinity(hitVfxLifetimeSeconds) || hitVfxLifetimeSeconds < 0.05f)
            hitVfxLifetimeSeconds = 0.05f;

        if (float.IsNaN(hitVfxScaleMultiplier) || float.IsInfinity(hitVfxScaleMultiplier) || hitVfxScaleMultiplier < 0.01f)
            hitVfxScaleMultiplier = 0.01f;

        spawnPaintColor.a = Mathf.Clamp01(spawnPaintColor.a);
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores presentation settings resolved by enemy master presets and wave tools.
/// /returns None.
/// </summary>
[CreateAssetMenu(fileName = "EnemyVisualPreset", menuName = "Enemy/Visual Preset", order = 11)]
public sealed class EnemyVisualPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Preset name.")]
    [SerializeField] private string presetName = "New Enemy Visual Preset";

    [Tooltip("Short description of the preset use case.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Visual")]
    [Tooltip("Visibility settings block.")]
    [SerializeField] private EnemyVisualVisibilitySettings visibility = new EnemyVisualVisibilitySettings();

    [Tooltip("Damage feedback settings block.")]
    [SerializeField] private EnemyVisualDamageFeedbackSettings damageFeedback = new EnemyVisualDamageFeedbackSettings();

    [Tooltip("Prefab and paint metadata block.")]
    [SerializeField] private EnemyVisualPrefabSettings prefabs = new EnemyVisualPrefabSettings();
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

    public EnemyVisualVisibilitySettings Visibility
    {
        get
        {
            return visibility;
        }
    }

    public EnemyVisualPrefabSettings Prefabs
    {
        get
        {
            return prefabs;
        }
    }

    public EnemyVisualDamageFeedbackSettings DamageFeedback
    {
        get
        {
            return damageFeedback;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates nested settings and guarantees stable metadata defaults.
    /// /returns None.
    /// </summary>
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (visibility == null)
            visibility = new EnemyVisualVisibilitySettings();

        if (damageFeedback == null)
            damageFeedback = new EnemyVisualDamageFeedbackSettings();

        if (prefabs == null)
            prefabs = new EnemyVisualPrefabSettings();

        visibility.Validate();
        damageFeedback.Validate();
        prefabs.Validate();
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Revalidates the asset after inspector changes.
    /// /returns None.
    /// </summary>
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
