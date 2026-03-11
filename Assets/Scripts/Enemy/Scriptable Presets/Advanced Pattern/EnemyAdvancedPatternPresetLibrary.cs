using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores registered EnemyAdvancedPatternPreset assets used by Enemy Management Tool.
/// </summary>
[CreateAssetMenu(fileName = "EnemyAdvancedPatternPresetLibrary", menuName = "Enemy/Advanced Pattern Preset Library", order = 16)]
public sealed class EnemyAdvancedPatternPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered advanced pattern presets.")]
    [SerializeField] private List<EnemyAdvancedPatternPreset> presets = new List<EnemyAdvancedPatternPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<EnemyAdvancedPatternPreset> Presets
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
    /// Adds a preset to the library if not already present.
    /// </summary>
    /// <param name="preset">Preset to register.</param>
    public void AddPreset(EnemyAdvancedPatternPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes a preset from the library when present.
    /// </summary>
    /// <param name="preset">Preset to unregister.</param>
    public void RemovePreset(EnemyAdvancedPatternPreset preset)
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
