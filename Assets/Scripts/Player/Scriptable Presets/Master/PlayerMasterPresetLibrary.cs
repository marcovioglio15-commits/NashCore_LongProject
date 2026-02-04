using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMasterPresetLibrary", menuName = "Player/Master Preset Library", order = 15)]
public sealed class PlayerMasterPresetLibrary : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("List of registered player master presets.")]
    [Header("Presets")]
    [SerializeField] private List<PlayerMasterPreset> m_Presets = new List<PlayerMasterPreset>();
    #endregion

    #region Properties
    public IReadOnlyList<PlayerMasterPreset> Presets
    {
        get
        {
            return m_Presets;
        }
    }
    #endregion

    #region Public Methods
    public void AddPreset(PlayerMasterPreset preset)
    {
        if (preset == null)
            return;

        if (m_Presets.Contains(preset))
            return;

        m_Presets.Add(preset);
    }

    public void RemovePreset(PlayerMasterPreset preset)
    {
        if (preset == null)
            return;

        if (m_Presets.Contains(preset) == false)
            return;

        m_Presets.Remove(preset);
    }
    #endregion
}
