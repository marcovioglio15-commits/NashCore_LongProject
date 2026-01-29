using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInputOverridePreset", menuName = "Player/Input Override Preset", order = 12)]
public sealed class PlayerInputOverridePreset : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("Unique ID for this override preset.")]
    [Header("Metadata")]
    [SerializeField] private string m_PresetId;

    [Tooltip("Human-readable preset name for designers.")]
    [SerializeField] private string m_PresetName = "New Input Override Preset";

    [Tooltip("Description shown as tooltip in tools.")]
    [SerializeField] private string m_Description;

    [Tooltip("Input action ID this preset targets.")]
    [Header("Action")]
    [SerializeField] private string m_ActionId;

    [Tooltip("Cached action name for clarity in tools.")]
    [SerializeField] private string m_ActionName;

    [Tooltip("Binding overrides applied for this action.")]
    [Header("Overrides")]
    [SerializeField] private List<InputBindingOverride> m_Overrides = new List<InputBindingOverride>();
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

    public string ActionId
    {
        get
        {
            return m_ActionId;
        }
    }

    public string ActionName
    {
        get
        {
            return m_ActionName;
        }
    }

    public IReadOnlyList<InputBindingOverride> Overrides
    {
        get
        {
            return m_Overrides;
        }
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(m_PresetId))
            m_PresetId = Guid.NewGuid().ToString("N");

        if (m_Overrides == null)
            m_Overrides = new List<InputBindingOverride>();
    }
    #endregion
}
