using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerControllerPresetLibrary", menuName = "Player/Controller Preset Library", order = 11)]
public sealed class PlayerControllerPresetLibrary : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("List of registered player controller presets.")]
    [Header("Presets")]
    [SerializeField] private List<PlayerControllerPreset> m_Presets = new List<PlayerControllerPreset>();
    #endregion

    #region Properties
    public IReadOnlyList<PlayerControllerPreset> Presets
    {
        get
        {
            return m_Presets;
        }
    }
    #endregion

    #region Public Methods
    public void AddPreset(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        if (m_Presets.Contains(preset))
            return;

        m_Presets.Add(preset);
    }

    public void RemovePreset(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        if (m_Presets.Contains(preset) == false)
            return;

        m_Presets.Remove(preset);
    }
    #endregion
}
