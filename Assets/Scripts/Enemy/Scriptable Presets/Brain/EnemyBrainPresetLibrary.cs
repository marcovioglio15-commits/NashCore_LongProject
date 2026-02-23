using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyBrainPresetLibrary", menuName = "Enemy/Brain Preset Library", order = 11)]
public sealed class EnemyBrainPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered enemy brain presets.")]
    [SerializeField] private List<EnemyBrainPreset> presets = new List<EnemyBrainPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<EnemyBrainPreset> Presets
    {
        get
        {
            return presets;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    public void AddPreset(EnemyBrainPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    public void RemovePreset(EnemyBrainPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset) == false)
            return;

        presets.Remove(preset);
    }
    #endregion

    #endregion
}
