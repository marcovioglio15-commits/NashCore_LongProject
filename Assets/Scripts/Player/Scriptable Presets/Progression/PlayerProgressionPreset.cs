using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerProgressionPreset", menuName = "Player/Progression Preset", order = 12)]
public sealed class PlayerProgressionPreset : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("Unique ID for this progression preset, used for stable references.")]
    [Header("Metadata")]
    [SerializeField] private string m_PresetId;

    [Tooltip("Human-readable progression preset name for designers.")]
    [SerializeField] private string m_PresetName = "New Progression Preset";

    [Tooltip("Short description of the progression preset use case.")]
    [SerializeField] private string m_Description;

    [Tooltip("Optional semantic version string for this progression preset.")]
    [SerializeField] private string m_Version = "1.0.0";
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
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(m_PresetId))
            m_PresetId = Guid.NewGuid().ToString("N");
    }
    #endregion
}
