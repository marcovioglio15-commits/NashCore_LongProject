using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerControllerPreset", menuName = "Player/Controller Preset", order = 10)]
public sealed class PlayerControllerPreset : ScriptableObject
{
    #region Serialized Fields
    [Tooltip("Unique ID for this preset, used for stable references.")]
    [Header("Metadata")]
    [SerializeField] private string m_PresetId;

    [Tooltip("Human-readable preset name for designers.")]
    [SerializeField] private string m_PresetName = "New Player Preset";

    [Tooltip("Short description of the preset use case.")]
    [SerializeField] private string m_Description;

    [Tooltip("Optional semantic version string for this preset.")]
    [SerializeField] private string m_Version = "1.0.0";

    [Tooltip("Movement configuration block.")]
    [Header("Controller Settings")]
    [SerializeField] private MovementSettings m_MovementSettings = new MovementSettings();

    [Tooltip("Look configuration block.")]
    [SerializeField] private LookSettings m_LookSettings = new LookSettings();

    [Tooltip("Camera configuration block.")]
    [SerializeField] private CameraSettings m_CameraSettings = new CameraSettings();

    [Tooltip("Selected action ID for movement input.")]
    [Header("Input Actions")]
    [SerializeField] private string m_MoveActionId;

    [Tooltip("Selected action ID for look input.")]
    [SerializeField] private string m_LookActionId;

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return m_PresetId;
        }
    }

    public string PresetName
    {
        get
        {
            return m_PresetName;
        }
    }

    public string Description
    {
        get
        {
            return m_Description;
        }
    }

    public string Version
    {
        get
        {
            return m_Version;
        }
    }

    public MovementSettings MovementSettings
    {
        get
        {
            return m_MovementSettings;
        }
    }

    public LookSettings LookSettings
    {
        get
        {
            return m_LookSettings;
        }
    }

    public CameraSettings CameraSettings
    {
        get
        {
            return m_CameraSettings;
        }
    }

    public string MoveActionId
    {
        get
        {
            return m_MoveActionId;
        }
    }

    public string LookActionId
    {
        get
        {
            return m_LookActionId;
        }
    }

    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(m_PresetId))
            m_PresetId = Guid.NewGuid().ToString("N");

        if (m_MovementSettings == null)
            m_MovementSettings = new MovementSettings();

        if (m_LookSettings == null)
            m_LookSettings = new LookSettings();

        if (m_CameraSettings == null)
            m_CameraSettings = new CameraSettings();

        m_MovementSettings.Validate();
        m_LookSettings.Validate();
        m_CameraSettings.Validate();
    }
    #endregion
}
