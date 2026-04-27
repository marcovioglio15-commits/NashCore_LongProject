using System;
using UnityEngine;

/// <summary>
/// Stores boss-specific screen-space UI presentation settings.
/// /params None.
/// /returns None.
/// </summary>
[Serializable]
public sealed class EnemyBossVisualUiSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the dedicated boss HUD for enemies using a Boss Pattern Preset.")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Optional boss display name shown above the bottom health bar. Empty falls back to the visual preset name.")]
    [SerializeField] private string bossDisplayName;

    [Tooltip("Screen-space health fill color used by the bottom boss bar.")]
    [SerializeField] private Color healthFillColor = new Color(0.9f, 0.12f, 0.08f, 1f);

    [Tooltip("Screen-space background color used behind the bottom boss bar.")]
    [SerializeField] private Color healthBackgroundColor = new Color(0f, 0f, 0f, 0.7f);

    [Tooltip("Sprite used by the off-screen indicator that slides along screen edges.")]
    [SerializeField] private Sprite offscreenIndicatorSprite;

    [Tooltip("Tint color applied to the off-screen boss indicator.")]
    [SerializeField] private Color offscreenIndicatorColor = new Color(1f, 0.2f, 0.1f, 0.95f);

    [Tooltip("Square size in pixels used by the off-screen boss indicator image.")]
    [Range(16f, 192f)]
    [SerializeField] private float offscreenIndicatorSizePixels = 56f;

    [Tooltip("Bottom offset in pixels for the boss health bar root.")]
    [Range(0f, 220f)]
    [SerializeField] private float bottomOffsetPixels = 42f;

    [Tooltip("Target width in pixels for the boss health bar.")]
    [Range(180f, 1200f)]
    [SerializeField] private float widthPixels = 560f;

    [Tooltip("Target height in pixels for the boss health bar fill.")]
    [Range(8f, 72f)]
    [SerializeField] private float heightPixels = 22f;

    [Tooltip("Extra screen-edge margin in pixels kept outside the off-screen indicator half size.")]
    [Range(0f, 160f)]
    [SerializeField] private float edgePaddingPixels = 30f;
    #endregion

    #endregion

    #region Properties
    public bool Enabled
    {
        get
        {
            return enabled;
        }
    }

    public string BossDisplayName
    {
        get
        {
            return bossDisplayName;
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

    public Sprite OffscreenIndicatorSprite
    {
        get
        {
            return offscreenIndicatorSprite;
        }
    }

    public Color OffscreenIndicatorColor
    {
        get
        {
            return offscreenIndicatorColor;
        }
    }

    public float BottomOffsetPixels
    {
        get
        {
            return bottomOffsetPixels;
        }
    }

    public float OffscreenIndicatorSizePixels
    {
        get
        {
            return offscreenIndicatorSizePixels;
        }
    }

    public float WidthPixels
    {
        get
        {
            return widthPixels;
        }
    }

    public float HeightPixels
    {
        get
        {
            return heightPixels;
        }
    }

    public float EdgePaddingPixels
    {
        get
        {
            return edgePaddingPixels;
        }
    }
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Validates color alpha channels while leaving authored layout values untouched for tool warnings.
    /// /params None.
    /// /returns None.
    /// </summary>
    public void Validate()
    {
        healthFillColor.a = Mathf.Clamp01(healthFillColor.a);
        healthBackgroundColor.a = Mathf.Clamp01(healthBackgroundColor.a);
        offscreenIndicatorColor.a = Mathf.Clamp01(offscreenIndicatorColor.a);
    }
    #endregion

    #endregion
}
