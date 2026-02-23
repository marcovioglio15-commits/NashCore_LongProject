using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyMasterPreset", menuName = "Enemy/Master Preset", order = 9)]
public sealed class EnemyMasterPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this enemy master preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Human-readable enemy master preset name for designers.")]
    [SerializeField] private string presetName = "New Enemy Master Preset";

    [Tooltip("Short description of this enemy master preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this enemy master preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Sub Presets")]
    [Tooltip("Brain preset reference used by this enemy master preset.")]
    [SerializeField] private EnemyBrainPreset brainPreset;
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

    public EnemyBrainPreset BrainPreset
    {
        get
        {
            return brainPreset;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (brainPreset != null)
            brainPreset.ValidateValues();
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}
