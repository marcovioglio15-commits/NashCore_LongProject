using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMasterPresetLibrary", menuName = "Player/Master Preset Library", order = 15)]
public sealed class PlayerMasterPresetLibrary : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("List of registered player master presets.")]
    [Header("Presets")]
    [FormerlySerializedAs("m_Presets")]
    [SerializeField] private List<PlayerMasterPreset> presets = new List<PlayerMasterPreset>();
    #endregion

    #region Properties
    public IReadOnlyList<PlayerMasterPreset> Presets
    {
        get
        {
            return presets;
        }
    }
    #endregion

    #region Public Methods
    public void AddPreset(PlayerMasterPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    public void RemovePreset(PlayerMasterPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset) == false)
            return;

        presets.Remove(preset);
    }
    #endregion
}
