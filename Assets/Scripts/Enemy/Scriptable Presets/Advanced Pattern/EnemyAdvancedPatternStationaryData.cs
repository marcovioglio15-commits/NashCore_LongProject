using System;
using UnityEngine;

/// <summary>
/// Contains payload values for Stationary movement modules.
/// </summary>
[Serializable]
public sealed class EnemyStationaryModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("When enabled, Stationary also blocks self-rotation updates. Disable to freeze translation only.")]
    [SerializeField] private bool freezeRotation = true;
    #endregion

    #endregion

    #region Properties
    public bool FreezeRotation
    {
        get
        {
            return freezeRotation;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Validates Stationary payload values.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
