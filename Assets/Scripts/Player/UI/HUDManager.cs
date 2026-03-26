using Unity.Entities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages player HUD widgets and updates health, shield, level, experience, and active-power-up bars from ECS runtime data.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDManager : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Health")]
    [Tooltip("UI Image used as fillable health bar. Fill method should be Horizontal or Radial.")]
    [SerializeField] private Image playerHealthFillImage;

    [Tooltip("Seconds used to smooth visual fill transitions. Set 0 for immediate updates.")]
    [SerializeField] private float healthBarSmoothingSeconds = 0.08f;

    [Tooltip("Hide health bar image when no player entity with PlayerHealth is available.")]
    [SerializeField] private bool hideHealthBarWhenPlayerMissing = true;

    [Header("Shield")]
    [Tooltip("UI Image used as fillable shield bar. Fill method should be Horizontal or Radial.")]
    [SerializeField] private Image playerShieldFillImage;

    [Tooltip("Seconds used to smooth visual shield fill transitions. Set 0 for immediate updates.")]
    [SerializeField] private float shieldBarSmoothingSeconds = 0.08f;

    [Tooltip("Hide shield bar image when no player entity with PlayerShield is available.")]
    [SerializeField] private bool hideShieldBarWhenPlayerMissing = true;

    [Header("Level & Experience")]
    [Tooltip("UI Text used to display the current player level.")]
    [SerializeField] private TMP_Text playerLevelText;

    [Tooltip("Hide player level text when no player entity with PlayerLevel is available.")]
    [SerializeField] private bool hideLevelTextWhenPlayerMissing = true;

    [Tooltip("UI Image used as fillable experience bar toward the next player level.")]
    [SerializeField] private Image playerExperienceFillImage;

    [Tooltip("Seconds used to smooth visual experience fill transitions. Set 0 for immediate updates.")]
    [SerializeField] private float experienceBarSmoothingSeconds = 0.08f;

    [Tooltip("Hide experience bar image when no player entity with progression runtime data is available.")]
    [SerializeField] private bool hideExperienceBarWhenPlayerMissing = true;

    [Header("Power Ups - Energy")]
    [Tooltip("Primary slot energy fill image. Displayed only when the primary slot has an energy module.")]
    [SerializeField] private Image primaryEnergyFillImage;

    [Tooltip("Secondary slot energy fill image. Displayed only when the secondary slot has an energy module.")]
    [SerializeField] private Image secondaryEnergyFillImage;

    [Header("Power Ups - Icons")]
    [Tooltip("Primary slot icon image. Shows the sprite assigned to the currently equipped primary active power up.")]
    [SerializeField] private Image primaryPowerUpIconImage;

    [Tooltip("Secondary slot icon image. Shows the sprite assigned to the currently equipped secondary active power up.")]
    [SerializeField] private Image secondaryPowerUpIconImage;

    [Tooltip("Seconds used to smooth energy fill transitions. Set 0 for immediate updates.")]
    [SerializeField] private float energyBarSmoothingSeconds = 0.08f;

    [Tooltip("Hide energy bars when no player entity is available.")]
    [SerializeField] private bool hideEnergyBarsWhenPlayerMissing = true;

    [Tooltip("Hide energy bars when the corresponding slot has no energy module.")]
    [SerializeField] private bool hideEnergyBarsWhenModuleMissing = true;

    [Header("Power Ups - Charge")]
    [Tooltip("Primary slot charge fill image. Displayed only when the primary slot has a charge module.")]
    [SerializeField] private Image primaryChargeFillImage;

    [Tooltip("Secondary slot charge fill image. Displayed only when the secondary slot has a charge module.")]
    [SerializeField] private Image secondaryChargeFillImage;

    [Tooltip("Seconds used to smooth charge fill transitions. Set 0 for immediate updates.")]
    [SerializeField] private float chargeBarSmoothingSeconds = 0.05f;

    [Tooltip("Hide charge bars when no player entity is available.")]
    [SerializeField] private bool hideChargeBarsWhenPlayerMissing = true;

    [Tooltip("Hide charge bars when the corresponding slot has no charge module.")]
    [SerializeField] private bool hideChargeBarsWhenModuleMissing = true;

    [Header("Run Timer")]
    [Tooltip("Serialized HUD section that configures and renders the authoritative run timer.")]
    [SerializeField] private HUDRunTimerSection runTimerSection = new HUDRunTimerSection();

    [Header("Milestone Power-Up Selection")]
    [Tooltip("Serialized HUD section that renders milestone choices and sends ECS selection commands.")]
    [SerializeField] private HUDMilestoneSelectionSection milestoneSelectionSection = new HUDMilestoneSelectionSection();

    [Header("Dropped Power-Up Containers")]
    [Tooltip("Serialized HUD section that handles dropped active power-up prompts and overlay swaps.")]
    [SerializeField] private HUDPowerUpContainerInteractionSection powerUpContainerInteractionSection = new HUDPowerUpContainerInteractionSection();
    #endregion

    private World defaultWorld;
    private EntityManager entityManager;
    private EntityQuery playerQuery;
    private bool playerQueryInitialized;
    private Entity cachedPlayerEntity;
    private bool shieldCanvasWarningIssued;
    private int displayedPlayerLevel = -1;
    private float displayedHealthNormalized = 1f;
    private float displayedShieldNormalized;
    private float displayedExperienceNormalized;
    private HUDPowerUpOverlaySection powerUpOverlaySection;
    #endregion

    #region Methods

    #region Unity Methods
    private void Awake()
    {
        ClampSettings();
        ValidateShieldOverlayBinding();
        powerUpOverlaySection = new HUDPowerUpOverlaySection(primaryPowerUpIconImage,
                                                             secondaryPowerUpIconImage,
                                                             primaryEnergyFillImage,
                                                             secondaryEnergyFillImage,
                                                             primaryChargeFillImage,
                                                             secondaryChargeFillImage,
                                                             energyBarSmoothingSeconds,
                                                             hideEnergyBarsWhenPlayerMissing,
                                                             hideEnergyBarsWhenModuleMissing,
                                                             chargeBarSmoothingSeconds,
                                                             hideChargeBarsWhenPlayerMissing,
                                                             hideChargeBarsWhenModuleMissing);
        runTimerSection.Initialize();
        milestoneSelectionSection.Initialize();
        powerUpContainerInteractionSection.Initialize();
        TryInitializeEcsBindings();
        ApplyInitialVisualState();
    }

    private void OnDestroy()
    {
        milestoneSelectionSection.Dispose();
        powerUpContainerInteractionSection.Dispose();
    }

    private void Update()
    {
        if (!TryInitializeEcsBindings())
        {
            HandleMissingPlayer();
            return;
        }

        if (!TryResolvePlayerEntity(out Entity playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        bool snapCoreBars = ShouldSnapCoreBars(playerEntity);
        UpdateHealthBar(playerEntity, snapCoreBars);
        UpdateShieldBar(playerEntity, snapCoreBars);
        UpdateLevelAndExperience(playerEntity);
        powerUpOverlaySection.Update(entityManager, playerEntity);
        runTimerSection.Update(entityManager, playerEntity);
        milestoneSelectionSection.Update(entityManager, playerEntity);
        powerUpContainerInteractionSection.Update(entityManager, playerEntity);
    }
    #endregion

    #region ECS
    private bool TryInitializeEcsBindings()
    {
        World currentWorld = World.DefaultGameObjectInjectionWorld;

        if (currentWorld == null || !currentWorld.IsCreated)
        {
            defaultWorld = null;
            playerQueryInitialized = false;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        if (!ReferenceEquals(defaultWorld, currentWorld))
        {
            defaultWorld = currentWorld;
            playerQueryInitialized = false;
            cachedPlayerEntity = Entity.Null;
        }

        entityManager = defaultWorld.EntityManager;

        if (!playerQueryInitialized)
        {
            EntityQueryDesc queryDescription = new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PlayerControllerConfig>()
                }
            };

            playerQuery = entityManager.CreateEntityQuery(queryDescription);
            playerQueryInitialized = true;
        }

        return playerQueryInitialized;
    }

    private bool TryResolvePlayerEntity(out Entity playerEntity)
    {
        if (cachedPlayerEntity != Entity.Null &&
            entityManager.Exists(cachedPlayerEntity) &&
            entityManager.HasComponent<PlayerControllerConfig>(cachedPlayerEntity))
        {
            playerEntity = cachedPlayerEntity;
            return true;
        }

        if (playerQuery.IsEmptyIgnoreFilter)
        {
            playerEntity = Entity.Null;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        int playerCount = playerQuery.CalculateEntityCount();

        if (playerCount != 1)
        {
            playerEntity = Entity.Null;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        Entity resolvedPlayerEntity = playerQuery.GetSingletonEntity();

        if (!entityManager.Exists(resolvedPlayerEntity))
        {
            playerEntity = Entity.Null;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        cachedPlayerEntity = resolvedPlayerEntity;
        playerEntity = resolvedPlayerEntity;
        return true;
    }
    #endregion

    #region Bars
    private void UpdateHealthBar(Entity playerEntity, bool snapImmediately)
    {
        if (playerHealthFillImage == null)
            return;

        if (!entityManager.HasComponent<PlayerHealth>(playerEntity))
        {
            HandleMissingHealthBar();
            return;
        }

        PlayerHealth playerHealth = entityManager.GetComponentData<PlayerHealth>(playerEntity);
        float targetNormalizedValue = 0f;

        if (playerHealth.Max > 0f)
            targetNormalizedValue = Mathf.Clamp01(playerHealth.Current / playerHealth.Max);

        if (snapImmediately)
        {
            displayedHealthNormalized = targetNormalizedValue;
        }
        else
        {
            displayedHealthNormalized = SmoothNormalized(displayedHealthNormalized,
                                                         targetNormalizedValue,
                                                         healthBarSmoothingSeconds);
        }

        ApplyFill(playerHealthFillImage, displayedHealthNormalized);
    }

    private void UpdateShieldBar(Entity playerEntity, bool snapImmediately)
    {
        if (!TryResolveShieldFillImage(out Image shieldFillImage))
            return;

        if (!entityManager.HasComponent<PlayerShield>(playerEntity))
        {
            HandleMissingShieldBar();
            return;
        }

        PlayerShield playerShield = entityManager.GetComponentData<PlayerShield>(playerEntity);
        float targetNormalizedValue = 0f;

        if (playerShield.Max > 0f)
            targetNormalizedValue = Mathf.Clamp01(playerShield.Current / playerShield.Max);

        if (snapImmediately)
        {
            displayedShieldNormalized = targetNormalizedValue;
        }
        else
        {
            displayedShieldNormalized = SmoothNormalized(displayedShieldNormalized,
                                                         targetNormalizedValue,
                                                         shieldBarSmoothingSeconds);
        }

        ApplyFill(shieldFillImage, displayedShieldNormalized);
    }

    /// <summary>
    /// Updates the player level text and experience progress bar from ECS progression data.
    /// </summary>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    private void UpdateLevelAndExperience(Entity playerEntity)
    {
        bool hasPlayerLevel = entityManager.HasComponent<PlayerLevel>(playerEntity);
        bool hasPlayerExperience = entityManager.HasComponent<PlayerExperience>(playerEntity);

        if (!hasPlayerLevel)
            HandleMissingLevelText();

        if (!hasPlayerLevel || !hasPlayerExperience)
        {
            HandleMissingExperienceBar();
            return;
        }

        PlayerLevel playerLevel = entityManager.GetComponentData<PlayerLevel>(playerEntity);
        PlayerExperience playerExperience = entityManager.GetComponentData<PlayerExperience>(playerEntity);
        UpdateLevelText(in playerLevel);
        UpdateExperienceBar(in playerExperience, in playerLevel);
    }

    /// <summary>
    /// Updates the player level text label using the current runtime level.
    /// </summary>
    /// <param name="playerLevel">Current player level state.</param>
    private void UpdateLevelText(in PlayerLevel playerLevel)
    {
        if (playerLevelText == null)
            return;

        int currentPlayerLevel = Mathf.Max(0, playerLevel.Current);

        if (!playerLevelText.enabled)
            playerLevelText.enabled = true;

        if (displayedPlayerLevel == currentPlayerLevel)
            return;

        displayedPlayerLevel = currentPlayerLevel;
        playerLevelText.text = string.Format("Lv {0}", currentPlayerLevel);
    }

    /// <summary>
    /// Updates the experience progress bar using the current experience value and next-level threshold.
    /// </summary>
    /// <param name="playerExperience">Current runtime experience state.</param>
    /// <param name="playerLevel">Current player level state used to resolve the next threshold.</param>
    private void UpdateExperienceBar(in PlayerExperience playerExperience, in PlayerLevel playerLevel)
    {
        if (playerExperienceFillImage == null)
            return;

        float targetNormalizedValue = 0f;
        float requiredExperienceForNextLevel = Mathf.Max(0f, playerLevel.RequiredExperienceForNextLevel);

        if (requiredExperienceForNextLevel > 0f)
            targetNormalizedValue = Mathf.Clamp01(playerExperience.Current / requiredExperienceForNextLevel);

        displayedExperienceNormalized = SmoothNormalized(displayedExperienceNormalized,
                                                         targetNormalizedValue,
                                                         experienceBarSmoothingSeconds);
        ApplyFill(playerExperienceFillImage, displayedExperienceNormalized);
    }

    #endregion

    #region Helpers
    private void ClampSettings()
    {
        if (healthBarSmoothingSeconds < 0f)
            healthBarSmoothingSeconds = 0f;

        if (energyBarSmoothingSeconds < 0f)
            energyBarSmoothingSeconds = 0f;

        if (chargeBarSmoothingSeconds < 0f)
            chargeBarSmoothingSeconds = 0f;

        if (shieldBarSmoothingSeconds < 0f)
            shieldBarSmoothingSeconds = 0f;

        if (experienceBarSmoothingSeconds < 0f)
            experienceBarSmoothingSeconds = 0f;
    }

    private void ApplyInitialVisualState()
    {
        if (playerHealthFillImage != null)
            ApplyFill(playerHealthFillImage, displayedHealthNormalized);

        if (TryResolveShieldFillImage(out Image shieldFillImage))
            ApplyFill(shieldFillImage, displayedShieldNormalized);

        if (playerExperienceFillImage != null)
            ApplyFill(playerExperienceFillImage, displayedExperienceNormalized);
        powerUpOverlaySection.ApplyInitialVisualState();
        runTimerSection.ApplyInitialVisualState();

        HandleMissingLevelText();
        runTimerSection.HandleMissingPlayer();
        milestoneSelectionSection.HandleMissingPlayer();
        powerUpContainerInteractionSection.HandleMissingPlayer();
    }

    private void HandleMissingPlayer()
    {
        HandleMissingHealthBar();
        HandleMissingShieldBar();
        HandleMissingLevelText();
        HandleMissingExperienceBar();
        powerUpOverlaySection.HandleMissingPlayer();
        runTimerSection.HandleMissingPlayer();
        milestoneSelectionSection.HandleMissingPlayer();
        powerUpContainerInteractionSection.HandleMissingPlayer();
    }

    private void HandleMissingHealthBar()
    {
        if (playerHealthFillImage == null)
            return;

        if (hideHealthBarWhenPlayerMissing)
        {
            playerHealthFillImage.enabled = false;
            return;
        }

        ApplyFill(playerHealthFillImage, displayedHealthNormalized);
    }

    private void HandleMissingShieldBar()
    {
        if (!TryResolveShieldFillImage(out Image shieldFillImage))
            return;

        if (hideShieldBarWhenPlayerMissing)
        {
            shieldFillImage.enabled = false;
            return;
        }

        ApplyFill(shieldFillImage, displayedShieldNormalized);
    }

    /// <summary>
    /// Applies the missing-player state to the player level label.
    /// </summary>
    private void HandleMissingLevelText()
    {
        if (playerLevelText == null)
            return;

        if (hideLevelTextWhenPlayerMissing)
        {
            playerLevelText.enabled = false;
            displayedPlayerLevel = -1;
            return;
        }

        playerLevelText.enabled = true;
        playerLevelText.text = string.Empty;
        displayedPlayerLevel = -1;
    }

    /// <summary>
    /// Applies the missing-player state to the experience progress bar.
    /// </summary>
    private void HandleMissingExperienceBar()
    {
        if (playerExperienceFillImage == null)
            return;

        if (hideExperienceBarWhenPlayerMissing)
        {
            playerExperienceFillImage.enabled = false;
            return;
        }

        ApplyFill(playerExperienceFillImage, displayedExperienceNormalized);
    }

    /// <summary>
    /// Returns the configured shield fill image only when it belongs to a screen-space HUD canvas.
    /// </summary>
    /// <param name="shieldFillImage">Resolved shield image safe to update from HUDManager.</param>
    /// <returns>True when the shield image is available and not bound to a world-space canvas; otherwise false.</returns>
    private bool TryResolveShieldFillImage(out Image shieldFillImage)
    {
        shieldFillImage = playerShieldFillImage;

        if (shieldFillImage == null)
            return false;

        Canvas owningCanvas = shieldFillImage.canvas;

        if (owningCanvas == null || owningCanvas.renderMode != RenderMode.WorldSpace)
            return true;

        ValidateShieldOverlayBinding();
        return false;
    }

    /// <summary>
    /// Returns whether health and shield bars should snap to their exact runtime values.
    /// </summary>
    /// <param name="playerEntity">Player entity currently driving the HUD.</param>
    /// <returns>True when the run outcome is finalized and the ending screen should bypass smoothing.</returns>
    private bool ShouldSnapCoreBars(Entity playerEntity)
    {
        if (!entityManager.HasComponent<PlayerRunOutcomeState>(playerEntity))
            return false;

        PlayerRunOutcomeState runOutcomeState = entityManager.GetComponentData<PlayerRunOutcomeState>(playerEntity);
        return runOutcomeState.IsFinalized != 0;
    }

    /// <summary>
    /// Warns once when the configured shield image is still assigned to a world-space canvas.
    /// </summary>
    private void ValidateShieldOverlayBinding()
    {
        if (playerShieldFillImage == null)
            return;

        Canvas owningCanvas = playerShieldFillImage.canvas;

        if (owningCanvas == null || owningCanvas.renderMode != RenderMode.WorldSpace)
            return;

        if (shieldCanvasWarningIssued)
            return;

        shieldCanvasWarningIssued = true;
        Debug.LogWarning("[HUDManager] Player Shield Fill Image is bound to a World Space canvas. Assign a Screen Space Overlay HUD image instead so the shield is managed by HUDManager as screen UI.",
                         playerShieldFillImage);
    }

    private static float SmoothNormalized(float displayedValue, float targetValue, float smoothingSeconds)
    {
        if (smoothingSeconds <= 0f)
            return Mathf.Clamp01(targetValue);

        float step = Time.deltaTime / smoothingSeconds;
        return Mathf.MoveTowards(displayedValue, Mathf.Clamp01(targetValue), step);
    }

    private static void ApplyFill(Image fillImage, float normalizedValue)
    {
        fillImage.enabled = true;
        fillImage.fillAmount = Mathf.Clamp01(normalizedValue);
    }
    #endregion

    #endregion
}
