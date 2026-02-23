using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyMasterPresetLibrary", menuName = "Enemy/Master Preset Library", order = 15)]
public sealed class EnemyMasterPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered enemy master presets.")]
    [SerializeField] private List<EnemyMasterPreset> presets = new List<EnemyMasterPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<EnemyMasterPreset> Presets
    {
        get
        {
            return presets;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    public void AddPreset(EnemyMasterPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    public void RemovePreset(EnemyMasterPreset preset)
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
