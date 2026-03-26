using System;
using UnityEngine;

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
    #endregion

    #region Methods

    #region Unity Methods
    /// <summary>
    /// Ensures the preset keeps a stable ID after edits and duplications.
    ///  None.
    /// returns None.
    /// </summary>
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");
    }
    #endregion

    #endregion
}
