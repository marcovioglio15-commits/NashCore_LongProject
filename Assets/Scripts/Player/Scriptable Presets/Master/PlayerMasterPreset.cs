using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMasterPreset", menuName = "Player/Master Preset", order = 9)]
public sealed class PlayerMasterPreset : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("Unique ID for this master preset, used for stable references.")]
    [Header("Metadata")]
    [SerializeField] private string m_PresetId;

    [Tooltip("Human-readable master preset name for designers.")]
    [SerializeField] private string m_PresetName = "New Player Master Preset";

    [Tooltip("Short description of the master preset use case.")]
    [SerializeField] private string m_Description;

    [Tooltip("Optional semantic version string for this master preset.")]
    [SerializeField] private string m_Version = "1.0.0";

    [Tooltip("Controller preset reference.")]
    [Header("Sub Presets")]
    [SerializeField] private PlayerControllerPreset m_ControllerPreset;

    [Tooltip("Level-up and progression preset reference.")]
    [SerializeField] private PlayerProgressionPreset m_ProgressionPreset;

    [Tooltip("Craftable power-ups preset reference.")]
    [SerializeField] private PlayerCraftablePowerUpsPreset m_PowerUpsPreset;

    [Tooltip("Animation bindings preset reference.")]
    [SerializeField] private PlayerAnimationBindingsPreset m_AnimationBindingsPreset;
    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return m_PresetId;
        }
    }

    public string PresetName
    {
        get
        {
            return m_PresetName;
        }
    }

    public string Description
    {
        get
        {
            return m_Description;
        }
    }

    public string Version
    {
        get
        {
            return m_Version;
        }
    }

    public PlayerControllerPreset ControllerPreset
    {
        get
        {
            return m_ControllerPreset;
        }
    }

    public PlayerProgressionPreset ProgressionPreset
    {
        get
        {
            return m_ProgressionPreset;
        }
    }

    public PlayerCraftablePowerUpsPreset PowerUpsPreset
    {
        get
        {
            return m_PowerUpsPreset;
        }
    }

    public PlayerAnimationBindingsPreset AnimationBindingsPreset
    {
        get
        {
            return m_AnimationBindingsPreset;
        }
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(m_PresetId))
            m_PresetId = Guid.NewGuid().ToString("N");
    }
    #endregion
}
