using System;
using UnityEngine.Serialization;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerControllerPreset", menuName = "Player/Controller Preset", order = 10)]
public sealed class PlayerControllerPreset : ScriptableObject
{
    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this preset, used for stable references.")]
    [FormerlySerializedAs("m_PresetId")]
    [SerializeField] private string presetId;

    [Tooltip("Human-readable preset name for designers.")]
    [FormerlySerializedAs("m_PresetName")]
    [SerializeField] private string presetName = "New Player Preset";

    [Tooltip("Short description of the preset use case.")]
    [FormerlySerializedAs("m_Description")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this preset.")]
    [FormerlySerializedAs("m_Version")]
    [SerializeField] private string version = "1.0.0";

    [Header("Controller Settings")]
    [Tooltip("Movement configuration block.")]
    [FormerlySerializedAs("m_MovementSettings")]
    [SerializeField] private MovementSettings movementSettings = new MovementSettings();

    [Tooltip("Look configuration block.")]
    [FormerlySerializedAs("m_LookSettings")]
    [SerializeField] private LookSettings lookSettings = new LookSettings();

    [Tooltip("Camera configuration block.")]
    [FormerlySerializedAs("m_CameraSettings")]
    [SerializeField] private CameraSettings cameraSettings = new CameraSettings();

    [Tooltip("Shooting configuration block.")]
    [SerializeField] private ShootingSettings shootingSettings = new ShootingSettings();

    [Header("Input Actions")]
    [Tooltip("Selected action ID for movement input.")]
    [FormerlySerializedAs("m_MoveActionId")]
    [SerializeField] private string moveActionId;

    [Tooltip("Selected action ID for look input.")]
    [FormerlySerializedAs("m_LookActionId")]
    [SerializeField] private string lookActionId;

    [Tooltip("Selected action ID for shooting input.")]
    [SerializeField] private string shootActionId;

    #endregion

    #region Properties
    public string PresetId
    {
        get
        {
            return presetId;
        }
    }

    public string PresetName
    {
        get
        {
            return presetName;
        }
    }

    public string Description
    {
        get
        {
            return description;
        }
    }

    public string Version
    {
        get
        {
            return version;
        }
    }

    public MovementSettings MovementSettings
    {
        get
        {
            return movementSettings;
        }
    }

    public LookSettings LookSettings
    {
        get
        {
            return lookSettings;
        }
    }

    public CameraSettings CameraSettings
    {
        get
        {
            return cameraSettings;
        }
    }

    public ShootingSettings ShootingSettings
    {
        get
        {
            return shootingSettings;
        }
    }

    public string MoveActionId
    {
        get
        {
            return moveActionId;
        }
    }

    public string LookActionId
    {
        get
        {
            return lookActionId;
        }
    }

    public string ShootActionId
    {
        get
        {
            return shootActionId;
        }
    }

    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (movementSettings == null)
            movementSettings = new MovementSettings();

        if (lookSettings == null)
            lookSettings = new LookSettings();

        if (cameraSettings == null)
            cameraSettings = new CameraSettings();

        if (shootingSettings == null)
            shootingSettings = new ShootingSettings();

        movementSettings.Validate();
        lookSettings.Validate();
        cameraSettings.Validate();
        shootingSettings.Validate();
    }
    #endregion
}
