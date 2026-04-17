using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stores one optional visual theme entry used by the combo HUD when a specific combo rank becomes active.
/// none.
/// returns none.
/// </summary>
[Serializable]
public sealed class HUDComboCounterRankVisualDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Combo rank identifier that activates this visual theme.")]
    [SerializeField] private string rankId = string.Empty;

    [Tooltip("Optional sprite shown inside the rank badge while this rank is active.")]
    [SerializeField] private Sprite badgeSprite;

    [Tooltip("Tint applied to the badge image while this rank is active.")]
    [SerializeField] private Color badgeTint = Color.white;

    [Tooltip("Text color applied to the rank label while this rank is active.")]
    [SerializeField] private Color rankTextColor = Color.white;

    [Tooltip("Text color applied to the combo numeric label while this rank is active.")]
    [SerializeField] private Color comboValueTextColor = Color.white;

    [Tooltip("Tint applied to the progress fill while this rank is active.")]
    [SerializeField] private Color progressFillColor = Color.white;

    [Tooltip("Tint applied to the progress background while this rank is active.")]
    [SerializeField] private Color progressBackgroundColor = Color.white;
    #endregion

    #endregion

    #region Properties
    public string RankId
    {
        get
        {
            return rankId;
        }
    }

    public Sprite BadgeSprite
    {
        get
        {
            return badgeSprite;
        }
    }

    public Color BadgeTint
    {
        get
        {
            return badgeTint;
        }
    }

    public Color RankTextColor
    {
        get
        {
            return rankTextColor;
        }
    }

    public Color ComboValueTextColor
    {
        get
        {
            return comboValueTextColor;
        }
    }

    public Color ProgressFillColor
    {
        get
        {
            return progressFillColor;
        }
    }

    public Color ProgressBackgroundColor
    {
        get
        {
            return progressBackgroundColor;
        }
    }
    #endregion
}

/// <summary>
/// Renders the runtime combo counter, current rank label, and progress toward the next combo rank from ECS data.
/// none.
/// returns none.
/// </summary>
[Serializable]
public sealed class HUDComboCounterSection
{
    #region Constants
    private const float ProgressComparisonEpsilon = 0.0001f;
    #endregion

    #region Fields

    #region Serialized Fields
    [Tooltip("Enables the combo HUD section and its ECS-driven presentation updates.")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Optional root GameObject shown or hidden as one block for the combo counter.")]
    [SerializeField] private GameObject rootObject;

    [Tooltip("Optional badge image used to display the current combo-rank sprite.")]
    [SerializeField] private Image rankBadgeImage;

    [Tooltip("TMP text used to render the current combo-rank label.")]
    [SerializeField] private TMP_Text rankText;

    [Tooltip("TMP text used to render the current combo numeric value.")]
    [SerializeField] private TMP_Text comboValueText;

    [Tooltip("Optional fill image used as progress bar toward the next combo rank.")]
    [SerializeField] private Image progressFillImage;

    [Tooltip("Optional background image shown behind the progress fill.")]
    [SerializeField] private Image progressBackgroundImage;

    [Tooltip("Optional default badge sprite used when the active rank does not define a specific sprite.")]
    [SerializeField] private Sprite defaultBadgeSprite;

    [Tooltip("Fallback tint applied to the badge image when no rank-specific theme matches.")]
    [SerializeField] private Color defaultBadgeTint = Color.white;

    [Tooltip("Fallback color applied to the rank label when no rank-specific theme matches.")]
    [SerializeField] private Color defaultRankTextColor = Color.white;

    [Tooltip("Fallback color applied to the combo numeric label when no rank-specific theme matches.")]
    [SerializeField] private Color defaultComboValueTextColor = Color.white;

    [Tooltip("Fallback color applied to the progress fill when no rank-specific theme matches.")]
    [SerializeField] private Color defaultProgressFillColor = Color.white;

    [Tooltip("Fallback color applied to the progress background when no rank-specific theme matches.")]
    [SerializeField] private Color defaultProgressBackgroundColor = new Color(1f, 1f, 1f, 0.25f);

    [Tooltip("When disabled, the badge image stays hidden even if it is assigned.")]
    [SerializeField] private bool showRankBadgeImage = true;

    [Tooltip("When disabled, the progress bar stays hidden even if the images are assigned.")]
    [SerializeField] private bool showProgressBar = true;

    [Tooltip("Hides the combo HUD while no valid player entity is available.")]
    [SerializeField] private bool hideWhenPlayerMissing = true;

    [Tooltip("Hides the combo HUD while the current combo value is 0.")]
    [SerializeField] private bool hideWhenZeroCombo = true;

    [Tooltip("Fallback label shown before the first combo rank is reached.")]
    [SerializeField] private string idleRankLabel = "COMBO";

    [Tooltip("Per-rank visual themes keyed by combo Rank ID.")]
    [SerializeField] private List<HUDComboCounterRankVisualDefinition> rankVisuals = new List<HUDComboCounterRankVisualDefinition>();
    #endregion

    private int displayedComboValue = int.MinValue;
    private float displayedProgressNormalized = -1f;
    private FixedString64Bytes displayedRankId;
    private string displayedRankLabel = string.Empty;
    private bool displayedVisibleState;
    private bool rankThemeInitialized;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies the initial authored visual state before runtime ECS data becomes available.
    /// none.
    /// returns void.
    /// </summary>
    public void Initialize()
    {
        ApplyInitialVisualState();
    }

    /// <summary>
    /// Applies the initial visual state used before a valid player entity is resolved.
    /// none.
    /// returns void.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        displayedComboValue = int.MinValue;
        displayedProgressNormalized = -1f;
        displayedRankId = default;
        displayedRankLabel = string.Empty;
        displayedVisibleState = false;
        rankThemeInitialized = false;
        HandleMissingPlayer();
    }

    /// <summary>
    /// Applies the missing-player visual state and clears cached values.
    /// none.
    /// returns void.
    /// </summary>
    public void HandleMissingPlayer()
    {
        displayedComboValue = int.MinValue;
        displayedProgressNormalized = -1f;
        displayedRankId = default;
        displayedRankLabel = string.Empty;
        rankThemeInitialized = false;

        if (!isEnabled)
        {
            SetVisible(false);
            return;
        }

        if (hideWhenPlayerMissing)
        {
            SetVisible(false);
            return;
        }

        ApplyVisibleState(0,
                          default,
                          0f);
    }

    /// <summary>
    /// Updates the combo HUD from ECS combo components owned by the current player entity.
    /// runtimeEntityManager Entity manager used to read combo runtime components.
    /// playerEntity Player entity currently driving the HUD.
    /// returns void.
    /// </summary>
    public void Update(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        if (!isEnabled)
        {
            SetVisible(false);
            return;
        }

        if (!runtimeEntityManager.Exists(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerComboCounterState>(playerEntity) ||
            !runtimeEntityManager.HasComponent<PlayerRuntimeComboCounterConfig>(playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        PlayerRuntimeComboCounterConfig runtimeComboConfig = runtimeEntityManager.GetComponentData<PlayerRuntimeComboCounterConfig>(playerEntity);
        PlayerComboCounterState comboCounterState = runtimeEntityManager.GetComponentData<PlayerComboCounterState>(playerEntity);
        bool shouldBeVisible = runtimeComboConfig.Enabled != 0;

        if (hideWhenZeroCombo && comboCounterState.CurrentValue <= 0)
        {
            shouldBeVisible = false;
        }

        if (!shouldBeVisible)
        {
            SetVisible(false);
            return;
        }

        ApplyVisibleState(comboCounterState.CurrentValue,
                          comboCounterState.CurrentRankId,
                          comboCounterState.ProgressNormalized);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies the current combo state to all authored UI bindings.
    /// comboValue Current combo numeric value.
    /// currentRankId Current combo-rank identifier.
    /// progressNormalized Current normalized progress toward the next rank.
    /// returns void.
    /// </summary>
    private void ApplyVisibleState(int comboValue,
                                   FixedString64Bytes currentRankId,
                                   float progressNormalized)
    {
        SetVisible(true);
        ApplyRankTheme(currentRankId);
        ApplyRankLabel(currentRankId);
        ApplyComboValue(comboValue);
        ApplyProgress(progressNormalized);
    }

    /// <summary>
    /// Shows or hides the combo HUD root.
    /// visible True when the section should be visible.
    /// returns void.
    /// </summary>
    private void SetVisible(bool visible)
    {
        if (displayedVisibleState == visible)
        {
            ApplyStandaloneElementVisibility(visible);
            return;
        }

        displayedVisibleState = visible;

        if (rootObject != null)
        {
            rootObject.SetActive(visible);
            ApplyStandaloneElementVisibility(visible);
            return;
        }

        ApplyStandaloneElementVisibility(visible);
    }

    /// <summary>
    /// Applies standalone element visibility for scenes that do not assign a dedicated root object.
    /// visible True when the section should be visible.
    /// returns void.
    /// </summary>
    private void ApplyStandaloneElementVisibility(bool visible)
    {
        if (rankBadgeImage != null)
        {
            rankBadgeImage.enabled = visible && showRankBadgeImage;
        }

        if (rankText != null)
        {
            rankText.enabled = visible;
        }

        if (comboValueText != null)
        {
            comboValueText.enabled = visible;
        }

        bool showProgress = visible && showProgressBar;

        if (progressFillImage != null)
        {
            progressFillImage.enabled = showProgress;
        }

        if (progressBackgroundImage != null)
        {
            progressBackgroundImage.enabled = showProgress;
        }
    }

    /// <summary>
    /// Applies the current combo-rank visual theme when the active rank changes.
    /// currentRankId Current combo-rank identifier.
    /// returns void.
    /// </summary>
    private void ApplyRankTheme(FixedString64Bytes currentRankId)
    {
        if (rankThemeInitialized && displayedRankId == currentRankId)
        {
            return;
        }

        displayedRankId = currentRankId;
        rankThemeInitialized = true;
        HUDComboCounterRankVisualDefinition rankVisual = FindRankVisual(currentRankId);
        Sprite resolvedBadgeSprite = rankVisual != null && rankVisual.BadgeSprite != null ? rankVisual.BadgeSprite : defaultBadgeSprite;
        Color resolvedBadgeTint = rankVisual != null ? rankVisual.BadgeTint : defaultBadgeTint;
        Color resolvedRankTextColor = rankVisual != null ? rankVisual.RankTextColor : defaultRankTextColor;
        Color resolvedComboValueTextColor = rankVisual != null ? rankVisual.ComboValueTextColor : defaultComboValueTextColor;
        Color resolvedProgressFillColor = rankVisual != null ? rankVisual.ProgressFillColor : defaultProgressFillColor;
        Color resolvedProgressBackgroundColor = rankVisual != null ? rankVisual.ProgressBackgroundColor : defaultProgressBackgroundColor;

        if (rankBadgeImage != null)
        {
            rankBadgeImage.sprite = resolvedBadgeSprite;
            rankBadgeImage.color = resolvedBadgeTint;
        }

        if (rankText != null)
        {
            rankText.color = resolvedRankTextColor;
        }

        if (comboValueText != null)
        {
            comboValueText.color = resolvedComboValueTextColor;
        }

        if (progressFillImage != null)
        {
            progressFillImage.color = resolvedProgressFillColor;
        }

        if (progressBackgroundImage != null)
        {
            progressBackgroundImage.color = resolvedProgressBackgroundColor;
        }
    }

    /// <summary>
    /// Applies the displayed rank label only when it actually changed.
    /// currentRankId Current combo-rank identifier.
    /// returns void.
    /// </summary>
    private void ApplyRankLabel(FixedString64Bytes currentRankId)
    {
        if (rankText == null)
        {
            return;
        }

        string resolvedLabel = currentRankId.Length > 0 ? currentRankId.ToString() : idleRankLabel;

        if (string.Equals(displayedRankLabel, resolvedLabel, StringComparison.Ordinal))
        {
            return;
        }

        displayedRankLabel = resolvedLabel;
        rankText.text = resolvedLabel;
    }

    /// <summary>
    /// Applies the displayed combo numeric label only when it actually changed.
    /// comboValue Current combo numeric value.
    /// returns void.
    /// </summary>
    private void ApplyComboValue(int comboValue)
    {
        if (comboValueText == null)
        {
            return;
        }

        int sanitizedComboValue = Mathf.Max(0, comboValue);

        if (displayedComboValue == sanitizedComboValue)
        {
            return;
        }

        displayedComboValue = sanitizedComboValue;
        comboValueText.text = string.Format("x{0}", sanitizedComboValue);
    }

    /// <summary>
    /// Applies the displayed progress fill only when it actually changed.
    /// progressNormalized Current normalized progress toward the next rank.
    /// returns void.
    /// </summary>
    private void ApplyProgress(float progressNormalized)
    {
        if (progressFillImage == null)
        {
            return;
        }

        float sanitizedProgress = Mathf.Clamp01(progressNormalized);

        if (Mathf.Abs(displayedProgressNormalized - sanitizedProgress) <= ProgressComparisonEpsilon)
        {
            return;
        }

        displayedProgressNormalized = sanitizedProgress;
        progressFillImage.fillAmount = sanitizedProgress;
    }

    /// <summary>
    /// Resolves the visual theme entry matching the provided rank identifier.
    /// currentRankId Current combo-rank identifier.
    /// returns Matching visual theme, or null when no theme matches.
    /// </summary>
    private HUDComboCounterRankVisualDefinition FindRankVisual(FixedString64Bytes currentRankId)
    {
        if (currentRankId.Length <= 0 || rankVisuals == null || rankVisuals.Count <= 0)
        {
            return null;
        }

        string currentRankIdText = currentRankId.ToString();

        for (int visualIndex = 0; visualIndex < rankVisuals.Count; visualIndex++)
        {
            HUDComboCounterRankVisualDefinition rankVisual = rankVisuals[visualIndex];

            if (rankVisual == null || string.IsNullOrWhiteSpace(rankVisual.RankId))
            {
                continue;
            }

            if (!string.Equals(rankVisual.RankId, currentRankIdText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return rankVisual;
        }

        return null;
    }
    #endregion

    #endregion
}
