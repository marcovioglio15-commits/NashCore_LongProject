using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerProgressionPresetLibrary", menuName = "Player/Progression Preset Library", order = 16)]
public sealed class PlayerProgressionPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered player progression presets.")]
    [SerializeField] private List<PlayerProgressionPreset> presets = new List<PlayerProgressionPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<PlayerProgressionPreset> Presets
    {
        get
        {
            return presets;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    public void AddPreset(PlayerProgressionPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    public void RemovePreset(PlayerProgressionPreset preset)
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
