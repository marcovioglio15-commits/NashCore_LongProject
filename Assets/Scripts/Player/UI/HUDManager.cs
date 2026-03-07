using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages player HUD widgets and updates health/active-power-up bars from ECS runtime data.
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

    [Header("Power Ups - Energy")]
    [Tooltip("Primary slot energy fill image. Displayed only when the primary slot has an energy module.")]
    [SerializeField] private Image primaryEnergyFillImage;

    [Tooltip("Secondary slot energy fill image. Displayed only when the secondary slot has an energy module.")]
    [SerializeField] private Image secondaryEnergyFillImage;

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
    #endregion

    private World defaultWorld;
    private EntityManager entityManager;
    private EntityQuery playerQuery;
    private bool playerQueryInitialized;
    private Entity cachedPlayerEntity;
    private float displayedHealthNormalized = 1f;
    private float displayedShieldNormalized;
    private float displayedPrimaryEnergyNormalized = 1f;
    private float displayedSecondaryEnergyNormalized = 1f;
    private float displayedPrimaryChargeNormalized;
    private float displayedSecondaryChargeNormalized;
    #endregion

    #region Methods

    #region Unity Methods
    private void Awake()
    {
        ClampSettings();
        TryInitializeEcsBindings();
        ApplyInitialVisualState();
    }

    private void Update()
    {
        if (TryInitializeEcsBindings() == false)
        {
            HandleMissingPlayer();
            return;
        }

        if (TryResolvePlayerEntity(out Entity playerEntity) == false)
        {
            HandleMissingPlayer();
            return;
        }

        UpdateHealthBar(playerEntity);
        UpdateShieldBar(playerEntity);
        UpdatePowerUpBars(playerEntity);
    }
    #endregion

    #region ECS
    private bool TryInitializeEcsBindings()
    {
        World currentWorld = World.DefaultGameObjectInjectionWorld;

        if (currentWorld == null || currentWorld.IsCreated == false)
        {
            defaultWorld = null;
            playerQueryInitialized = false;
            cachedPlayerEntity = Entity.Null;
            return false;
        }

        if (ReferenceEquals(defaultWorld, currentWorld) == false)
        {
            defaultWorld = currentWorld;
            playerQueryInitialized = false;
            cachedPlayerEntity = Entity.Null;
        }

        entityManager = defaultWorld.EntityManager;

        if (playerQueryInitialized == false)
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

        if (entityManager.Exists(resolvedPlayerEntity) == false)
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
    private void UpdateHealthBar(Entity playerEntity)
    {
        if (playerHealthFillImage == null)
            return;

        if (entityManager.HasComponent<PlayerHealth>(playerEntity) == false)
        {
            HandleMissingHealthBar();
            return;
        }

        PlayerHealth playerHealth = entityManager.GetComponentData<PlayerHealth>(playerEntity);
        float targetNormalizedValue = 0f;

        if (playerHealth.Max > 0f)
            targetNormalizedValue = Mathf.Clamp01(playerHealth.Current / playerHealth.Max);

        displayedHealthNormalized = SmoothNormalized(displayedHealthNormalized,
                                                     targetNormalizedValue,
                                                     healthBarSmoothingSeconds);
        ApplyFill(playerHealthFillImage, displayedHealthNormalized);
    }

    private void UpdateShieldBar(Entity playerEntity)
    {
        if (playerShieldFillImage == null)
            return;

        if (entityManager.HasComponent<PlayerShield>(playerEntity) == false)
        {
            HandleMissingShieldBar();
            return;
        }

        PlayerShield playerShield = entityManager.GetComponentData<PlayerShield>(playerEntity);
        float targetNormalizedValue = 0f;

        if (playerShield.Max > 0f)
            targetNormalizedValue = Mathf.Clamp01(playerShield.Current / playerShield.Max);

        displayedShieldNormalized = SmoothNormalized(displayedShieldNormalized,
                                                     targetNormalizedValue,
                                                     shieldBarSmoothingSeconds);
        ApplyFill(playerShieldFillImage, displayedShieldNormalized);
    }

    private void UpdatePowerUpBars(Entity playerEntity)
    {
        if (HasAnyPowerUpBars() == false)
            return;

        if (entityManager.HasComponent<PlayerPowerUpsConfig>(playerEntity) == false ||
            entityManager.HasComponent<PlayerPowerUpsState>(playerEntity) == false)
        {
            HandleMissingPowerUpBars();
            return;
        }

        PlayerPowerUpsConfig powerUpsConfig = entityManager.GetComponentData<PlayerPowerUpsConfig>(playerEntity);
        PlayerPowerUpsState powerUpsState = entityManager.GetComponentData<PlayerPowerUpsState>(playerEntity);

        UpdateEnergyBar(primaryEnergyFillImage,
                        in powerUpsConfig.PrimarySlot,
                        powerUpsState.PrimaryEnergy,
                        ref displayedPrimaryEnergyNormalized);
        UpdateEnergyBar(secondaryEnergyFillImage,
                        in powerUpsConfig.SecondarySlot,
                        powerUpsState.SecondaryEnergy,
                        ref displayedSecondaryEnergyNormalized);
        UpdateChargeBar(primaryChargeFillImage,
                        in powerUpsConfig.PrimarySlot,
                        powerUpsState.PrimaryEnergy,
                        powerUpsState.PrimaryCharge,
                        ref displayedPrimaryChargeNormalized);
        UpdateChargeBar(secondaryChargeFillImage,
                        in powerUpsConfig.SecondarySlot,
                        powerUpsState.SecondaryEnergy,
                        powerUpsState.SecondaryCharge,
                        ref displayedSecondaryChargeNormalized);
    }

    private void UpdateEnergyBar(Image fillImage,
                                 in PlayerPowerUpSlotConfig slotConfig,
                                 float currentEnergy,
                                 ref float displayedNormalized)
    {
        if (fillImage == null)
            return;

        if (HasEnergyModule(in slotConfig) == false)
        {
            displayedNormalized = 0f;

            if (hideEnergyBarsWhenModuleMissing)
            {
                fillImage.enabled = false;
                return;
            }

            ApplyFill(fillImage, 0f);
            return;
        }

        float maximumEnergy = Mathf.Max(0f, slotConfig.MaximumEnergy);
        float targetNormalized = 0f;

        if (maximumEnergy > 0f)
            targetNormalized = Mathf.Clamp01(currentEnergy / maximumEnergy);

        displayedNormalized = SmoothNormalized(displayedNormalized,
                                               targetNormalized,
                                               energyBarSmoothingSeconds);
        ApplyFill(fillImage, displayedNormalized);
    }

    private void UpdateChargeBar(Image fillImage,
                                 in PlayerPowerUpSlotConfig slotConfig,
                                 float currentEnergy,
                                 float currentCharge,
                                 ref float displayedNormalized)
    {
        if (fillImage == null)
            return;

        if (HasChargeModule(in slotConfig) == false)
        {
            displayedNormalized = 0f;

            if (hideChargeBarsWhenModuleMissing)
            {
                fillImage.enabled = false;
                return;
            }

            ApplyFill(fillImage, 0f);
            return;
        }

        if (CanDisplayChargeProgress(in slotConfig, currentEnergy) == false)
        {
            displayedNormalized = SmoothNormalized(displayedNormalized,
                                                   0f,
                                                   chargeBarSmoothingSeconds);
            ApplyFill(fillImage, displayedNormalized);
            return;
        }

        float maximumCharge = Mathf.Max(slotConfig.ChargeShot.RequiredCharge, slotConfig.ChargeShot.MaximumCharge);
        float targetNormalized = 0f;

        if (maximumCharge > 0f)
            targetNormalized = Mathf.Clamp01(currentCharge / maximumCharge);

        displayedNormalized = SmoothNormalized(displayedNormalized,
                                               targetNormalized,
                                               chargeBarSmoothingSeconds);
        ApplyFill(fillImage, displayedNormalized);
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
    }

    private void ApplyInitialVisualState()
    {
        if (playerHealthFillImage != null)
            ApplyFill(playerHealthFillImage, displayedHealthNormalized);

        if (playerShieldFillImage != null)
            ApplyFill(playerShieldFillImage, displayedShieldNormalized);

        if (primaryEnergyFillImage != null)
            ApplyFill(primaryEnergyFillImage, displayedPrimaryEnergyNormalized);

        if (secondaryEnergyFillImage != null)
            ApplyFill(secondaryEnergyFillImage, displayedSecondaryEnergyNormalized);

        if (primaryChargeFillImage != null)
            ApplyFill(primaryChargeFillImage, displayedPrimaryChargeNormalized);

        if (secondaryChargeFillImage != null)
            ApplyFill(secondaryChargeFillImage, displayedSecondaryChargeNormalized);
    }

    private void HandleMissingPlayer()
    {
        HandleMissingHealthBar();
        HandleMissingShieldBar();
        HandleMissingPowerUpBars();
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
        if (playerShieldFillImage == null)
            return;

        if (hideShieldBarWhenPlayerMissing)
        {
            playerShieldFillImage.enabled = false;
            return;
        }

        ApplyFill(playerShieldFillImage, displayedShieldNormalized);
    }

    private void HandleMissingPowerUpBars()
    {
        HandleMissingImage(primaryEnergyFillImage, hideEnergyBarsWhenPlayerMissing, displayedPrimaryEnergyNormalized);
        HandleMissingImage(secondaryEnergyFillImage, hideEnergyBarsWhenPlayerMissing, displayedSecondaryEnergyNormalized);
        HandleMissingImage(primaryChargeFillImage, hideChargeBarsWhenPlayerMissing, displayedPrimaryChargeNormalized);
        HandleMissingImage(secondaryChargeFillImage, hideChargeBarsWhenPlayerMissing, displayedSecondaryChargeNormalized);
    }

    private static void HandleMissingImage(Image fillImage, bool hideWhenMissing, float displayedValue)
    {
        if (fillImage == null)
            return;

        if (hideWhenMissing)
        {
            fillImage.enabled = false;
            return;
        }

        fillImage.enabled = true;
        fillImage.fillAmount = Mathf.Clamp01(displayedValue);
    }

    private static bool HasEnergyModule(in PlayerPowerUpSlotConfig slotConfig)
    {
        if (slotConfig.IsDefined == 0)
            return false;

        return slotConfig.MaximumEnergy > 0.0001f;
    }

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

            if (currentEnergy + 0.0001f < minimumEnergyRequired)
                return false;
        }

        float activationCost = Mathf.Max(0f, slotConfig.ActivationCost);

        if (activationCost > 0f && currentEnergy + 0.0001f < activationCost)
            return false;

        return true;
    }

    private bool HasAnyPowerUpBars()
    {
        if (primaryEnergyFillImage != null)
            return true;

        if (secondaryEnergyFillImage != null)
            return true;

        if (primaryChargeFillImage != null)
            return true;

        return secondaryChargeFillImage != null;
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
