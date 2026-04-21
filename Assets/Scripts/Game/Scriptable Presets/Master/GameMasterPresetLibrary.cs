using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Library asset that lists all GameMasterPreset assets visible in Game Management Tool.
/// /params None.
/// /returns None.
/// </summary>
[CreateAssetMenu(fileName = "GameMasterPresetLibrary", menuName = "Game/Master Preset Library", order = 21)]
public sealed class GameMasterPresetLibrary : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Presets")]
    [Tooltip("List of registered game master presets.")]
    [SerializeField] private List<GameMasterPreset> presets = new List<GameMasterPreset>();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<GameMasterPreset> Presets
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
    /// Adds one master preset reference if it is not already registered.
    /// /params preset Preset asset to register.
    /// /returns None.
    /// </summary>
    public void AddPreset(GameMasterPreset preset)
    {
        if (preset == null)
            return;

        if (presets.Contains(preset))
            return;

        presets.Add(preset);
    }

    /// <summary>
    /// Removes one master preset reference from this library.
    /// /params preset Preset asset to unregister.
    /// /returns None.
    /// </summary>
    public void RemovePreset(GameMasterPreset preset)
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
