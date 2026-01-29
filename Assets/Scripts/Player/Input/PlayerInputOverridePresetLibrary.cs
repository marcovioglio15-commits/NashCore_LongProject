using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInputOverridePresetLibrary", menuName = "Player/Input Override Preset Library", order = 13)]
public sealed class PlayerInputOverridePresetLibrary : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("List of registered input override presets.")]
    [Header("Presets")]
    [SerializeField] private List<PlayerInputOverridePreset> m_Presets = new List<PlayerInputOverridePreset>();
    #endregion

    #region Properties
    public IReadOnlyList<PlayerInputOverridePreset> Presets
    {
        get
        {
            return m_Presets;
        }
    }
    #endregion

    #region Public Methods
    public void AddPreset(PlayerInputOverridePreset preset)
    {
        if (preset == null)
            return;

        if (m_Presets.Contains(preset))
            return;

        m_Presets.Add(preset);
    }

    public void RemovePreset(PlayerInputOverridePreset preset)
    {
        if (preset == null)
            return;

        if (m_Presets.Contains(preset) == false)
            return;

        m_Presets.Remove(preset);
    }
    #endregion
}
