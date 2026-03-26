using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the managed HUD overlay for equipped active power-up slots, including icons and conditional module bars.
///  none.
/// returns none.
/// </summary>
internal sealed class HUDPowerUpOverlaySection
{
    #region Fields

    #region Constants
    private const float EnergyModuleThreshold = 0.0001f;
    private const float ResourceComparisonEpsilon = 0.0001f;
    #endregion

    #region Private Fields
    private readonly HUDPowerUpSlotVisual primarySlot;
    private readonly HUDPowerUpSlotVisual secondarySlot;
    private readonly float energyBarSmoothingSeconds;
    private readonly bool hideEnergyBarsWhenPlayerMissing;
    private readonly bool hideEnergyBarsWhenModuleMissing;
    private readonly float chargeBarSmoothingSeconds;
    private readonly bool hideChargeBarsWhenPlayerMissing;
    private readonly bool hideChargeBarsWhenModuleMissing;
    #endregion

    #endregion

    #region Methods

    #region Initialization
    /// <summary>
    /// Creates one runtime overlay section from the HUD image references already bound on the manager.
    ///  primaryIconImage: Primary slot icon image.
    ///  secondaryIconImage: Secondary slot icon image.
    ///  primaryEnergyFillImage: Primary slot energy fill image.
    ///  secondaryEnergyFillImage: Secondary slot energy fill image.
    ///  primaryChargeFillImage: Primary slot charge fill image.
    ///  secondaryChargeFillImage: Secondary slot charge fill image.
    ///  energyBarSmoothingSecondsValue: Smoothing time applied to energy bars.
    ///  hideEnergyBarsWhenPlayerMissingValue: Hides energy bars when the player entity is unavailable.
    ///  hideEnergyBarsWhenModuleMissingValue: Hides energy bars when the slot has no energy module.
    ///  chargeBarSmoothingSecondsValue: Smoothing time applied to charge bars.
    ///  hideChargeBarsWhenPlayerMissingValue: Hides charge bars when the player entity is unavailable.
    ///  hideChargeBarsWhenModuleMissingValue: Hides charge bars when the slot has no charge module.
    /// returns A ready-to-use overlay section.
    /// </summary>
    public HUDPowerUpOverlaySection(Image primaryIconImage,
                                    Image secondaryIconImage,
                                    Image primaryEnergyFillImage,
                                    Image secondaryEnergyFillImage,
                                    Image primaryChargeFillImage,
                                    Image secondaryChargeFillImage,
                                    float energyBarSmoothingSecondsValue,
                                    bool hideEnergyBarsWhenPlayerMissingValue,
                                    bool hideEnergyBarsWhenModuleMissingValue,
                                    float chargeBarSmoothingSecondsValue,
                                    bool hideChargeBarsWhenPlayerMissingValue,
                                    bool hideChargeBarsWhenModuleMissingValue)
    {
        primarySlot = HUDPowerUpSlotVisual.Create(primaryIconImage, primaryEnergyFillImage, primaryChargeFillImage);
        secondarySlot = HUDPowerUpSlotVisual.Create(secondaryIconImage, secondaryEnergyFillImage, secondaryChargeFillImage);
        energyBarSmoothingSeconds = Mathf.Max(0f, energyBarSmoothingSecondsValue);
        hideEnergyBarsWhenPlayerMissing = hideEnergyBarsWhenPlayerMissingValue;
        hideEnergyBarsWhenModuleMissing = hideEnergyBarsWhenModuleMissingValue;
        chargeBarSmoothingSeconds = Mathf.Max(0f, chargeBarSmoothingSecondsValue);
        hideChargeBarsWhenPlayerMissing = hideChargeBarsWhenPlayerMissingValue;
        hideChargeBarsWhenModuleMissing = hideChargeBarsWhenModuleMissingValue;
    }

    /// <summary>
    /// Applies the initial visual state before ECS data is available.
    ///  none.
    /// returns void.
    /// </summary>
    public void ApplyInitialVisualState()
    {
        primarySlot.ApplyInitialVisualState();
        secondarySlot.ApplyInitialVisualState();
    }
    #endregion

    #region Update
    /// <summary>
    /// Returns whether at least one slot exposes an icon or module bar that can be driven by runtime data.
    ///  none.
    /// returns True when the overlay section has something to render.
    /// </summary>
    public bool HasAnyVisuals()
    {
        if (primarySlot.HasAnyVisuals)
            return true;

        return secondarySlot.HasAnyVisuals;
    }

    /// <summary>
    /// Updates both active-slot overlays from the current ECS power-up config and state.
    ///  entityManager: Entity manager used to read runtime power-up components.
    ///  playerEntity: Player entity currently driving the overlay.
    /// returns void.
    /// </summary>
    public void Update(EntityManager entityManager, Entity playerEntity)
    {
        if (!HasAnyVisuals())
            return;

        if (!entityManager.Exists(playerEntity) ||
            !entityManager.HasComponent<PlayerPowerUpsConfig>(playerEntity) ||
            !entityManager.HasComponent<PlayerPowerUpsState>(playerEntity))
        {
            HandleMissingPlayer();
            return;
        }

        PlayerPowerUpsConfig powerUpsConfig = entityManager.GetComponentData<PlayerPowerUpsConfig>(playerEntity);
        PlayerPowerUpsState powerUpsState = entityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);

        primarySlot.Update(in powerUpsConfig.PrimarySlot,
                           powerUpsState.PrimaryEnergy,
                           powerUpsState.PrimaryCharge,
                           energyBarSmoothingSeconds,
                           hideEnergyBarsWhenModuleMissing,
                           chargeBarSmoothingSeconds,
                           hideChargeBarsWhenModuleMissing);
        secondarySlot.Update(in powerUpsConfig.SecondarySlot,
                             powerUpsState.SecondaryEnergy,
                             powerUpsState.SecondaryCharge,
                             energyBarSmoothingSeconds,
                             hideEnergyBarsWhenModuleMissing,
                             chargeBarSmoothingSeconds,
                             hideChargeBarsWhenModuleMissing);
    }

    /// <summary>
    /// Applies the missing-player state to icons and module bars.
    ///  none.
    /// returns void.
    /// </summary>
    public void HandleMissingPlayer()
    {
        primarySlot.HandleMissingPlayer(hideEnergyBarsWhenPlayerMissing, hideChargeBarsWhenPlayerMissing);
        secondarySlot.HandleMissingPlayer(hideEnergyBarsWhenPlayerMissing, hideChargeBarsWhenPlayerMissing);
    }
    #endregion

    #region Shared Helpers
    /// <summary>
    /// Returns whether the current slot exposes one energy module that should drive the energy bar.
    ///  slotConfig: Slot configuration currently bound to the HUD slot.
    /// returns True when an energy module is present.
    /// </summary>
    private static bool HasEnergyModule(in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        return slotConfig.MaximumEnergy > EnergyModuleThreshold;
    }

    /// <summary>
    /// Returns whether the current slot exposes one charge module that should drive the charge bar.
    ///  slotConfig: Slot configuration currently bound to the HUD slot.
    /// returns True when a charge module is present.
    /// </summary>
    private static bool HasChargeModule(in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        if (slotConfig.ToolKind != ActiveToolKind.ChargeShot)
            return false;

        if (slotConfig.ChargeShot.RequiredCharge <= 0f)
            return false;

        if (slotConfig.ChargeShot.MaximumCharge <= 0f)
            return false;

        return slotConfig.ChargeShot.ChargeRatePerSecond > 0f;
    }

    /// <summary>
    /// Returns whether charge progress is meaningful for the current slot and energy state.
    ///  slotConfig: Slot configuration currently bound to the HUD slot.
    ///  currentEnergy: Current slot energy value.
    /// returns True when the charge bar can show progress.
    /// </summary>
    private static bool CanDisplayChargeProgress(in PlayerPowerUpSlotConfig slotConfig, float currentEnergy)
    {
        if (slotConfig.ActivationResource != PowerUpResourceType.Energy)
            return true;

        float maximumEnergy = Mathf.Max(0f, slotConfig.MaximumEnergy);

        if (maximumEnergy <= 0f)
            return false;

        float minimumActivationEnergyPercent = Mathf.Clamp(slotConfig.MinimumActivationEnergyPercent, 0f, 100f);

        if (minimumActivationEnergyPercent > 0f)
        {
            float minimumEnergyRequired = maximumEnergy * (minimumActivationEnergyPercent * 0.01f);

            if (currentEnergy + ResourceComparisonEpsilon < minimumEnergyRequired)
                return false;
        }

        float activationCost = Mathf.Max(0f, slotConfig.ActivationCost);

        if (activationCost > 0f && currentEnergy + ResourceComparisonEpsilon < activationCost)
            return false;

        return true;
    }

    /// <summary>
    /// Smoothly approaches one normalized target value used by energy and charge bars.
    ///  displayedValue: Current displayed normalized value.
    ///  targetValue: New normalized target value.
    ///  smoothingSeconds: Time used to interpolate the value.
    /// returns Smoothed normalized value.
    /// </summary>
    private static float SmoothNormalized(float displayedValue, float targetValue, float smoothingSeconds)
    {
        if (smoothingSeconds <= 0f)
            return Mathf.Clamp01(targetValue);

        float step = Time.deltaTime / smoothingSeconds;
        return Mathf.MoveTowards(displayedValue, Mathf.Clamp01(targetValue), step);
    }
    #endregion

    #endregion

    #region Nested Types

    #region Slot Visual
    /// <summary>
    /// Stores and updates the managed visuals for one active power-up slot.
    ///  none.
    /// returns none.
    /// </summary>
    private sealed class HUDPowerUpSlotVisual
    {
        #region Fields

        #region Private Fields
        private readonly Image iconImage;
        private readonly HUDPowerUpBarVisual energyBar;
        private readonly HUDPowerUpBarVisual chargeBar;
        private float displayedEnergyNormalized = 1f;
        private float displayedChargeNormalized;
        #endregion

        #endregion

        #region Properties
        public bool HasAnyVisuals
        {
            get
            {
                if (iconImage != null)
                    return true;

                if (energyBar.HasVisual)
                    return true;

                return chargeBar.HasVisual;
            }
        }
        #endregion

        #region Methods

        #region Factory
        /// <summary>
        /// Builds one slot-visual descriptor from the bar fill images already bound in the HUD.
        ///  iconImage: Direct icon image reference serialized on the HUD manager.
        ///  energyFillImage: Energy fill image for the slot.
        ///  chargeFillImage: Charge fill image for the slot.
        /// returns A slot-visual descriptor ready for runtime updates.
        /// </summary>
        public static HUDPowerUpSlotVisual Create(Image iconImage, Image energyFillImage, Image chargeFillImage)
        {
            HUDPowerUpBarVisual resolvedEnergyBar = HUDPowerUpBarVisual.Create(energyFillImage);
            HUDPowerUpBarVisual resolvedChargeBar = HUDPowerUpBarVisual.Create(chargeFillImage);
            return new HUDPowerUpSlotVisual(iconImage, in resolvedEnergyBar, in resolvedChargeBar);
        }

        /// <summary>
        /// Creates one slot visual descriptor.
        ///  iconImageValue: Optional icon image shown above the module bars.
        ///  energyBarValue: Energy bar visuals owned by the slot.
        ///  chargeBarValue: Charge bar visuals owned by the slot.
        /// returns A fully initialized slot visual descriptor.
        /// </summary>
        private HUDPowerUpSlotVisual(Image iconImageValue,
                                     in HUDPowerUpBarVisual energyBarValue,
                                     in HUDPowerUpBarVisual chargeBarValue)
        {
            iconImage = iconImageValue;
            energyBar = energyBarValue;
            chargeBar = chargeBarValue;
        }
        #endregion

        #region Lifecycle
        /// <summary>
        /// Applies the initial fill amounts and icon visibility before ECS data arrives.
        ///  none.
        /// returns void.
        /// </summary>
        public void ApplyInitialVisualState()
        {
            energyBar.ApplyFill(displayedEnergyNormalized);
            chargeBar.ApplyFill(displayedChargeNormalized);
            ApplyMissingIcon();
        }

        /// <summary>
        /// Updates the slot icon plus its energy and charge bars from the current slot runtime data.
        ///  slotConfig: Active slot configuration currently bound to the player.
        ///  currentEnergy: Current energy value stored for the slot.
        ///  currentCharge: Current charge value stored for the slot.
        ///  energySmoothingSeconds: Smoothing time applied to energy visuals.
        ///  hideEnergyWhenModuleMissing: Hides the energy bar root when no module is present.
        ///  chargeSmoothingSeconds: Smoothing time applied to charge visuals.
        ///  hideChargeWhenModuleMissing: Hides the charge bar root when no module is present.
        /// returns void.
        /// </summary>
        public void Update(in PlayerPowerUpSlotConfig slotConfig,
                           float currentEnergy,
                           float currentCharge,
                           float energySmoothingSeconds,
                           bool hideEnergyWhenModuleMissing,
                           float chargeSmoothingSeconds,
                           bool hideChargeWhenModuleMissing)
        {
            UpdateIcon(in slotConfig);
            UpdateEnergyBar(in slotConfig, currentEnergy, energySmoothingSeconds, hideEnergyWhenModuleMissing);
            UpdateChargeBar(in slotConfig, currentEnergy, currentCharge, chargeSmoothingSeconds, hideChargeWhenModuleMissing);
        }

        /// <summary>
        /// Applies the missing-player state to the slot visuals.
        ///  hideEnergyBar: Hides the energy bar when the player is unavailable.
        ///  hideChargeBar: Hides the charge bar when the player is unavailable.
        /// returns void.
        /// </summary>
        public void HandleMissingPlayer(bool hideEnergyBar, bool hideChargeBar)
        {
            ApplyMissingIcon();
            energyBar.HandleMissing(displayedEnergyNormalized, hideEnergyBar);
            chargeBar.HandleMissing(displayedChargeNormalized, hideChargeBar);
        }
        #endregion

        #region Update Helpers
        /// <summary>
        /// Updates the slot icon from the cached presentation runtime.
        ///  slotConfig: Active slot configuration currently bound to the player.
        /// returns void.
        /// </summary>
        private void UpdateIcon(in PlayerPowerUpSlotConfig slotConfig)
        {
            if (iconImage == null)
                return;

            if (slotConfig.IsDefined == 0)
            {
                ApplyMissingIcon();
                return;
            }

            string powerUpId = slotConfig.PowerUpId.ToString();

            if (!PlayerPowerUpPresentationRuntime.TryResolveIcon(powerUpId, out Sprite icon))
            {
                ApplyMissingIcon();
                return;
            }

            if (iconImage.sprite != icon)
                iconImage.sprite = icon;

            if (!iconImage.enabled)
                iconImage.enabled = true;
        }

        /// <summary>
        /// Updates the energy bar for the slot.
        ///  slotConfig: Active slot configuration currently bound to the player.
        ///  currentEnergy: Current slot energy value.
        ///  smoothingSeconds: Smoothing time applied to the displayed fill.
        ///  hideWhenModuleMissing: Hides the energy bar root when no module is present.
        /// returns void.
        /// </summary>
        private void UpdateEnergyBar(in PlayerPowerUpSlotConfig slotConfig,
                                     float currentEnergy,
                                     float smoothingSeconds,
                                     bool hideWhenModuleMissing)
        {
            if (!energyBar.HasVisual)
                return;

            if (!HasEnergyModule(in slotConfig))
            {
                displayedEnergyNormalized = 0f;
                energyBar.ApplyMissing(displayedEnergyNormalized, hideWhenModuleMissing);
                return;
            }

            float maximumEnergy = Mathf.Max(0f, slotConfig.MaximumEnergy);
            float targetNormalized = 0f;

            if (maximumEnergy > 0f)
                targetNormalized = Mathf.Clamp01(currentEnergy / maximumEnergy);

            displayedEnergyNormalized = SmoothNormalized(displayedEnergyNormalized, targetNormalized, smoothingSeconds);
            energyBar.ApplyFill(displayedEnergyNormalized);
        }

        /// <summary>
        /// Updates the charge bar for the slot.
        ///  slotConfig: Active slot configuration currently bound to the player.
        ///  currentEnergy: Current slot energy value.
        ///  currentCharge: Current slot charge value.
        ///  smoothingSeconds: Smoothing time applied to the displayed fill.
        ///  hideWhenModuleMissing: Hides the charge bar root when no module is present.
        /// returns void.
        /// </summary>
        private void UpdateChargeBar(in PlayerPowerUpSlotConfig slotConfig,
                                     float currentEnergy,
                                     float currentCharge,
                                     float smoothingSeconds,
                                     bool hideWhenModuleMissing)
        {
            if (!chargeBar.HasVisual)
                return;

            if (!HasChargeModule(in slotConfig))
            {
                displayedChargeNormalized = 0f;
                chargeBar.ApplyMissing(displayedChargeNormalized, hideWhenModuleMissing);
                return;
            }

            if (!CanDisplayChargeProgress(in slotConfig, currentEnergy))
            {
                displayedChargeNormalized = SmoothNormalized(displayedChargeNormalized, 0f, smoothingSeconds);
                chargeBar.ApplyFill(displayedChargeNormalized);
                return;
            }

            float maximumCharge = Mathf.Max(slotConfig.ChargeShot.RequiredCharge, slotConfig.ChargeShot.MaximumCharge);
            float targetNormalized = 0f;

            if (maximumCharge > 0f)
                targetNormalized = Mathf.Clamp01(currentCharge / maximumCharge);

            displayedChargeNormalized = SmoothNormalized(displayedChargeNormalized, targetNormalized, smoothingSeconds);
            chargeBar.ApplyFill(displayedChargeNormalized);
        }

        /// <summary>
        /// Applies the empty icon state when no power-up or runtime icon is available.
        ///  none.
        /// returns void.
        /// </summary>
        private void ApplyMissingIcon()
        {
            if (iconImage == null)
                return;

            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        #endregion

        #endregion
    }
    #endregion

    #region Bar Visual
    /// <summary>
    /// Stores the visual references used by one HUD bar, including the fill image and its background root.
    ///  none.
    /// returns none.
    /// </summary>
    private readonly struct HUDPowerUpBarVisual
    {
        #region Fields
        public readonly Image FillImage;
        public readonly GameObject RootObject;
        #endregion

        #region Properties
        public bool HasVisual
        {
            get
            {
                if (FillImage != null)
                    return true;

                return RootObject != null;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Creates one bar-visual descriptor from a fill image and its parent background object.
        ///  fillImage: Fill image bound in the HUD manager.
        /// returns A bar-visual descriptor ready for updates.
        /// </summary>
        public static HUDPowerUpBarVisual Create(Image fillImage)
        {
            GameObject rootObject = null;

            if (fillImage != null)
            {
                Transform parentTransform = fillImage.transform.parent;
                rootObject = parentTransform != null ? parentTransform.gameObject : fillImage.gameObject;
            }

            return new HUDPowerUpBarVisual(fillImage, rootObject);
        }

        /// <summary>
        /// Creates one bar-visual descriptor.
        ///  fillImageValue: Fill image driven by runtime values.
        ///  rootObjectValue: Root object that contains the bar background and fill.
        /// returns A fully initialized bar-visual descriptor.
        /// </summary>
        private HUDPowerUpBarVisual(Image fillImageValue, GameObject rootObjectValue)
        {
            FillImage = fillImageValue;
            RootObject = rootObjectValue;
        }

        /// <summary>
        /// Applies one normalized fill value while keeping the full bar hierarchy visible.
        ///  normalizedValue: Normalized fill amount written into the fill image.
        /// returns void.
        /// </summary>
        public void ApplyFill(float normalizedValue)
        {
            if (!HasVisual)
                return;

            SetRootVisible(true);

            if (FillImage == null)
                return;

            if (!FillImage.enabled)
                FillImage.enabled = true;

            FillImage.fillAmount = Mathf.Clamp01(normalizedValue);
        }

        /// <summary>
        /// Applies the missing-data state to the bar.
        ///  displayedValue: Last displayed normalized value used when the bar remains visible.
        ///  hideWhenMissing: Hides the entire bar hierarchy when true.
        /// returns void.
        /// </summary>
        public void HandleMissing(float displayedValue, bool hideWhenMissing)
        {
            if (!HasVisual)
                return;

            if (hideWhenMissing)
            {
                SetRootVisible(false);
                return;
            }

            ApplyFill(displayedValue);
        }

        /// <summary>
        /// Applies the missing-module state to the bar.
        ///  displayedValue: Last displayed normalized value used when the bar remains visible.
        ///  hideWhenMissing: Hides the entire bar hierarchy when true.
        /// returns void.
        /// </summary>
        public void ApplyMissing(float displayedValue, bool hideWhenMissing)
        {
            HandleMissing(displayedValue, hideWhenMissing);
        }

        /// <summary>
        /// Shows or hides the full bar hierarchy only when a state change is required.
        ///  isVisible: Target active state for the bar root.
        /// returns void.
        /// </summary>
        private void SetRootVisible(bool isVisible)
        {
            if (RootObject != null)
            {
                if (RootObject.activeSelf != isVisible)
                    RootObject.SetActive(isVisible);

                return;
            }

            if (FillImage != null && FillImage.enabled != isVisible)
                FillImage.enabled = isVisible;
        }
        #endregion
    }
    #endregion

    #endregion
}
