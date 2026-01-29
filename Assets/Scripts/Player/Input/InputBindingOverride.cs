using System;
using UnityEngine;

[Serializable]
public struct InputBindingOverride
{
    #region Serialized Fields
    [Tooltip("GUID of the action this override targets.")]
    [Header("Binding Override")]
    [SerializeField] private string m_ActionId;

    [Tooltip("GUID of the binding this override targets.")]
    [SerializeField] private string m_BindingId;

    [Tooltip("Override path applied to the binding.")]
    [SerializeField] private string m_OverridePath;

    [Tooltip("Override interactions applied to the binding.")]
    [SerializeField] private string m_OverrideInteractions;

    [Tooltip("Override processors applied to the binding.")]
    [SerializeField] private string m_OverrideProcessors;
    #endregion

    #region Properties
    public string ActionId
    {
        get
        {
            return m_ActionId;
        }
    }

    public string BindingId
    {
        get
        {
            return m_BindingId;
        }
    }

    public string OverridePath
    {
        get
        {
            return m_OverridePath;
        }
    }

    public string OverrideInteractions
    {
        get
        {
            return m_OverrideInteractions;
        }
    }

    public string OverrideProcessors
    {
        get
        {
            return m_OverrideProcessors;
        }
    }
    #endregion
}
