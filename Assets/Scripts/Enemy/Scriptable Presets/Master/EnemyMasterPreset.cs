using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyMasterPreset", menuName = "Enemy/Master Preset", order = 9)]
public sealed class EnemyMasterPreset : ScriptableObject
{
    #region Fields

    #region Serialized Fields
    [Header("Metadata")]
    [Tooltip("Unique ID for this enemy master preset, used for stable references.")]
    [SerializeField] private string presetId;

    [Tooltip("Enemy master preset name.")]
    [SerializeField] private string presetName = "New Enemy Master Preset";

    [Tooltip("Short description of this enemy master preset.")]
    [SerializeField] private string description;

    [Tooltip("Optional semantic version string for this enemy master preset.")]
    [SerializeField] private string version = "1.0.0";

    [Header("Sub Presets")]
    [Tooltip("Brain preset reference used by this enemy master preset.")]
    [SerializeField] private EnemyBrainPreset brainPreset;

    [Tooltip("Advanced pattern preset reference used by this enemy master preset.")]
    [SerializeField] private EnemyAdvancedPatternPreset advancedPatternPreset;

    [Header("Test UI Settings")]
    [Tooltip("Editor-only settings used by Enemy Management Tool to generate world-space health and shield bars on enemy prefabs.")]
    [SerializeField] private EnemyTestUiSettings testUiSettings = new EnemyTestUiSettings();
    #endregion

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

    public EnemyBrainPreset BrainPreset
    {
        get
        {
            return brainPreset;
        }
    }

    public EnemyAdvancedPatternPreset AdvancedPatternPreset
    {
        get
        {
            return advancedPatternPreset;
        }
    }

    public EnemyTestUiSettings TestUiSettings
    {
        get
        {
            return testUiSettings;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    public void ValidateValues()
    {
        if (string.IsNullOrWhiteSpace(presetId))
            presetId = Guid.NewGuid().ToString("N");

        if (testUiSettings == null)
            testUiSettings = new EnemyTestUiSettings();

        testUiSettings.ValidateValues();

        if (brainPreset != null)
            brainPreset.ValidateValues();

        if (advancedPatternPreset != null)
            advancedPatternPreset.ValidateValues();
    }
    #endregion

    #region Unity Methods
    private void OnValidate()
    {
        ValidateValues();
    }
    #endregion

    #endregion
}

[Serializable]
public sealed class EnemyTestUiSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("World-space offset from enemy pivot where status bars are rendered.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);

    [Tooltip("Width in pixels of the root world-space canvas container.")]
    [SerializeField] private float rootWidthPixels = 120f;

    [Tooltip("Height in pixels of the root world-space canvas container.")]
    [SerializeField] private float rootHeightPixels = 26f;

    [Tooltip("Uniform world scale applied to the generated status bars root transform.")]
    [SerializeField] private float worldScale = 0.01f;

    [Tooltip("Canvas sorting order used for generated status bars.")]
    [SerializeField] private int canvasSortingOrder = 4000;

    [Tooltip("Width in pixels of the health bar background and fill.")]
    [SerializeField] private float healthBarWidthPixels = 108f;

    [Tooltip("Height in pixels of the health bar background and fill.")]
    [SerializeField] private float healthBarHeightPixels = 8f;

    [Tooltip("Y offset in pixels for the health bar relative to root center.")]
    [SerializeField] private float healthBarYOffsetPixels = 5f;

    [Tooltip("Width in pixels of the shield bar background and fill.")]
    [SerializeField] private float shieldBarWidthPixels = 108f;

    [Tooltip("Height in pixels of the shield bar background and fill.")]
    [SerializeField] private float shieldBarHeightPixels = 6f;

    [Tooltip("Y offset in pixels for the shield bar relative to root center.")]
    [SerializeField] private float shieldBarYOffsetPixels = -5f;

    [Tooltip("Fill color of the generated health bar.")]
    [SerializeField] private Color healthFillColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Tooltip("Background color behind the generated health bar fill.")]
    [SerializeField] private Color healthBackgroundColor = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("Fill color of the generated shield bar.")]
    [SerializeField] private Color shieldFillColor = new Color(0.2f, 0.85f, 1f, 1f);

    [Tooltip("Background color behind the generated shield bar fill.")]
    [SerializeField] private Color shieldBackgroundColor = new Color(0f, 0f, 0f, 0.45f);

    [Tooltip("Hide shield fill image when shield percentage is zero.")]
    [SerializeField] private bool hideShieldWhenEmpty = true;

    [Tooltip("Hide status bars when enemy is inactive in pooling runtime.")]
    [SerializeField] private bool hideWhenEnemyInactive = true;

    [Tooltip("Hide status bars when enemy visuals are culled by distance.")]
    [SerializeField] private bool hideWhenEnemyCulled;

    [Tooltip("Fill smoothing duration in seconds. Set to zero for immediate updates.")]
    [SerializeField] private float smoothingSeconds;

    [Tooltip("Optional smoothing duration in seconds for shield fill transitions. Set to zero to reuse generic fill smoothing.")]
    [SerializeField] private float shieldSmoothingSeconds = 0.08f;

    [Tooltip("Rotate generated status bars to face the active camera every frame.")]
    [SerializeField] private bool billboardToCamera = true;

    [Tooltip("When billboarding is enabled, constrain rotation to Y axis only.")]
    [SerializeField] private bool billboardYawOnly;
    #endregion

    #endregion

    #region Properties
    public Vector3 WorldOffset
    {
        get
        {
            return worldOffset;
        }
    }

    public float RootWidthPixels
    {
        get
        {
            return rootWidthPixels;
        }
    }

    public float RootHeightPixels
    {
        get
        {
            return rootHeightPixels;
        }
    }

    public float WorldScale
    {
        get
        {
            return worldScale;
        }
    }

    public int CanvasSortingOrder
    {
        get
        {
            return canvasSortingOrder;
        }
    }

    public float HealthBarWidthPixels
    {
        get
        {
            return healthBarWidthPixels;
        }
    }

    public float HealthBarHeightPixels
    {
        get
        {
            return healthBarHeightPixels;
        }
    }

    public float HealthBarYOffsetPixels
    {
        get
        {
            return healthBarYOffsetPixels;
        }
    }

    public float ShieldBarWidthPixels
    {
        get
        {
            return shieldBarWidthPixels;
        }
    }

    public float ShieldBarHeightPixels
    {
        get
        {
            return shieldBarHeightPixels;
        }
    }

    public float ShieldBarYOffsetPixels
    {
        get
        {
            return shieldBarYOffsetPixels;
        }
    }

    public Color HealthFillColor
    {
        get
        {
            return healthFillColor;
        }
    }

    public Color HealthBackgroundColor
    {
        get
        {
            return healthBackgroundColor;
        }
    }

    public Color ShieldFillColor
    {
        get
        {
            return shieldFillColor;
        }
    }

    public Color ShieldBackgroundColor
    {
        get
        {
            return shieldBackgroundColor;
        }
    }

    public bool HideShieldWhenEmpty
    {
        get
        {
            return hideShieldWhenEmpty;
        }
    }

    public bool HideWhenEnemyInactive
    {
        get
        {
            return hideWhenEnemyInactive;
        }
    }

    public bool HideWhenEnemyCulled
    {
        get
        {
            return hideWhenEnemyCulled;
        }
    }

    public float SmoothingSeconds
    {
        get
        {
            return smoothingSeconds;
        }
    }

    public float ShieldSmoothingSeconds
    {
        get
        {
            return shieldSmoothingSeconds;
        }
    }

    public bool BillboardToCamera
    {
        get
        {
            return billboardToCamera;
        }
    }

    public bool BillboardYawOnly
    {
        get
        {
            return billboardYawOnly;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    public void ValidateValues()
    {
        if (rootWidthPixels < 1f)
            rootWidthPixels = 1f;

        if (rootHeightPixels < 1f)
            rootHeightPixels = 1f;

        if (worldScale < 0.0001f)
            worldScale = 0.0001f;

        if (healthBarWidthPixels < 1f)
            healthBarWidthPixels = 1f;

        if (healthBarHeightPixels < 1f)
            healthBarHeightPixels = 1f;

        if (shieldBarWidthPixels < 1f)
            shieldBarWidthPixels = 1f;

        if (shieldBarHeightPixels < 1f)
            shieldBarHeightPixels = 1f;

        if (smoothingSeconds < 0f)
            smoothingSeconds = 0f;

        if (shieldSmoothingSeconds < 0f)
            shieldSmoothingSeconds = 0f;
    }
    #endregion

    #endregion
}
