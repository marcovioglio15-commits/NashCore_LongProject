using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the project library of enemy visual presets.
/// /returns None.
/// </summary>
[CreateAssetMenu(fileName = "EnemyVisualPresetLibrary", menuName = "Enemy/Visual Preset Library", order = 12)]
public sealed class EnemyVisualPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered enemy visual presets.")]
    [SerializeField] private List<EnemyVisualPreset> presets = new List<EnemyVisualPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<EnemyVisualPreset> Presets
    {
        get
        {
            return presets;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers a preset in the library when not already present.
    /// /params preset Preset asset to register.
    /// /returns None.
    /// </summary>
    public void AddPreset(EnemyVisualPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes a preset from the library when present.
    /// /params preset Preset asset to remove.
    /// /returns None.
    /// </summary>
    public void RemovePreset(EnemyVisualPreset preset)
    {
        if (preset == null)
            return;

        if (!presets.Contains(preset))
            return;

        presets.Remove(preset);
    }
    #endregion

    #endregion
}
