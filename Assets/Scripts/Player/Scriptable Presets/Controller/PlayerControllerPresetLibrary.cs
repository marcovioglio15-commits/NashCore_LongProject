using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerControllerPresetLibrary", menuName = "Player/Controller Preset Library", order = 11)]
public sealed class PlayerControllerPresetLibrary : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("List of registered player controller presets.")]
    [Header("Presets")]
    [FormerlySerializedAs("m_Presets")]
    [SerializeField] private List<PlayerControllerPreset> presets = new List<PlayerControllerPreset>();
    #endregion

    #region Properties
    public IReadOnlyList<PlayerControllerPreset> Presets
    {
        get
        {
            return presets;
        }
    }
    #endregion

    #region Public Methods
    public void AddPreset(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    public void RemovePreset(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset) == false)
            return;

        presets.Remove(preset);
    }
    #endregion
}
