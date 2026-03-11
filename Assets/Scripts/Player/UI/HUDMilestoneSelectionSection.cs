using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Handles HUD rendering and ECS command submission for milestone power-up selection.
/// </summary>
[System.Serializable]
public sealed class HUDMilestoneSelectionSection
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Root panel shown when milestone power-up offers are available.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Header text updated with milestone level and selection prompt.")]
    [SerializeField] private Text headerText;

    [Tooltip("Ordered option widgets mapped to rolled offers by index.")]
    [SerializeField] private List<MilestonePowerUpSelectionOptionBinding> optionBindings = new List<MilestonePowerUpSelectionOptionBinding>();

    [Tooltip("Automatically queues the first offer when selection is active but no option buttons are configured.")]
    [SerializeField] private bool autoSelectFirstOfferWhenUiMissing = true;

    [Tooltip("Disables all option buttons immediately after one click to avoid duplicate commands.")]
    [SerializeField] private bool lockButtonsAfterSelectionClick = true;
    #endregion

    private readonly List<Button> registeredButtons = new List<Button>(4);
    private readonly List<UnityAction> registeredActions = new List<UnityAction>(4);
    private EntityManager entityManager;
    private Entity playerEntity;
    private bool hasRuntimeContext;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers option button listeners and applies hidden initial state.
    /// </summary>
    /// <returns>Void.</returns>
    public void Initialize()
    {
        RegisterOptionButtons();
        HidePanel();
    }

    /// <summary>
    /// Unregisters option button listeners.
    /// </summary>
    /// <returns>Void.</returns>
    public void Dispose()
    {
        UnregisterOptionButtons();
    }

    /// <summary>
    /// Hides the milestone panel and clears runtime context when no player is available.
    /// </summary>
    /// <returns>Void.</returns>
    public void HandleMissingPlayer()
    {
        hasRuntimeContext = false;
        playerEntity = Entity.Null;
        HidePanel();
    }

    /// <summary>
    /// Refreshes panel visibility/content and submits auto-pick commands when required.
    /// </summary>
    /// <param name="runtimeEntityManager">Active entity manager used for ECS reads/writes.</param>
    /// <param name="runtimePlayerEntity">Resolved player entity.</param>
    /// <returns>Void.</returns>
    public void Update(EntityManager runtimeEntityManager, Entity runtimePlayerEntity)
    {
        if (!HasUiConfigured())
            return;

        entityManager = runtimeEntityManager;
        playerEntity = runtimePlayerEntity;
        hasRuntimeContext = true;

        if (!entityManager.HasComponent<PlayerMilestonePowerUpSelectionState>(playerEntity) ||
            !entityManager.HasBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity))
        {
            HidePanel();
            return;
        }

        PlayerMilestonePowerUpSelectionState selectionState = entityManager.GetComponentData<PlayerMilestonePowerUpSelectionState>(playerEntity);

        if (selectionState.IsSelectionActive == 0)
        {
            HidePanel();
            return;
        }

        DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers = entityManager.GetBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity);

        if (selectionOffers.Length <= 0)
        {
            HidePanel();
            return;
        }

        if (!HasSelectableButton() && autoSelectFirstOfferWhenUiMissing)
        {
            TryQueueSelectionCommand(0);
            return;
        }

        ShowPanel(selectionState, selectionOffers);
    }
    #endregion

    #region UI
    private void RegisterOptionButtons()
    {
        UnregisterOptionButtons();

        if (optionBindings == null || optionBindings.Count <= 0)
            return;

        for (int optionIndex = 0; optionIndex < optionBindings.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionBinding optionBinding = optionBindings[optionIndex];

            if (optionBinding == null || optionBinding.SelectButton == null)
                continue;

            int capturedOptionIndex = optionIndex;
            UnityAction clickAction = () => HandleOptionButtonPressed(capturedOptionIndex);
            optionBinding.SelectButton.onClick.AddListener(clickAction);
            registeredButtons.Add(optionBinding.SelectButton);
            registeredActions.Add(clickAction);
        }
    }

    private void UnregisterOptionButtons()
    {
        int registeredCount = Mathf.Min(registeredButtons.Count, registeredActions.Count);

        for (int registeredIndex = 0; registeredIndex < registeredCount; registeredIndex++)
        {
            Button registeredButton = registeredButtons[registeredIndex];
            UnityAction registeredAction = registeredActions[registeredIndex];

            if (registeredButton == null || registeredAction == null)
                continue;

            registeredButton.onClick.RemoveListener(registeredAction);
        }

        registeredButtons.Clear();
        registeredActions.Clear();
    }

    private bool HasUiConfigured()
    {
        if (panelRoot != null)
            return true;

        return optionBindings != null && optionBindings.Count > 0;
    }

    private bool HasSelectableButton()
    {
        if (optionBindings == null || optionBindings.Count <= 0)
            return false;

        for (int optionIndex = 0; optionIndex < optionBindings.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionBinding optionBinding = optionBindings[optionIndex];

            if (optionBinding == null || optionBinding.SelectButton == null)
                continue;

            return true;
        }

        return false;
    }

    private void ShowPanel(PlayerMilestonePowerUpSelectionState selectionState,
                           DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers)
    {
        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        if (headerText != null)
            headerText.text = string.Format("Milestone Lv {0} - Choose One Power-Up", selectionState.MilestoneLevel);

        if (optionBindings == null || optionBindings.Count <= 0)
            return;

        for (int optionIndex = 0; optionIndex < optionBindings.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionBinding optionBinding = optionBindings[optionIndex];

            if (optionBinding == null)
                continue;

            bool hasOffer = optionIndex < selectionOffers.Length;

            if (!hasOffer)
            {
                SetOptionUnused(optionBinding);
                continue;
            }

            PlayerMilestonePowerUpSelectionOfferElement offer = selectionOffers[optionIndex];
            SetOptionOffer(optionBinding, optionIndex, in offer);
        }
    }

    private void HidePanel()
    {
        if (panelRoot != null && panelRoot.activeSelf)
            panelRoot.SetActive(false);

        if (optionBindings == null || optionBindings.Count <= 0)
            return;

        for (int optionIndex = 0; optionIndex < optionBindings.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionBinding optionBinding = optionBindings[optionIndex];

            if (optionBinding == null)
                continue;

            SetOptionUnused(optionBinding);
        }
    }

    private static void SetOptionOffer(MilestonePowerUpSelectionOptionBinding optionBinding,
                                       int optionIndex,
                                       in PlayerMilestonePowerUpSelectionOfferElement offer)
    {
        if (optionBinding.SelectButton != null)
        {
            if (!optionBinding.SelectButton.gameObject.activeSelf)
                optionBinding.SelectButton.gameObject.SetActive(true);

            optionBinding.SelectButton.interactable = true;
        }

        if (optionBinding.NameText != null)
        {
            string displayName = string.IsNullOrWhiteSpace(offer.DisplayName.ToString())
                ? offer.PowerUpId.ToString()
                : offer.DisplayName.ToString();
            optionBinding.NameText.text = string.Format("{0}. {1}", optionIndex + 1, displayName);
        }

        if (optionBinding.DescriptionText != null)
        {
            string description = offer.Description.ToString();
            optionBinding.DescriptionText.text = string.IsNullOrWhiteSpace(description)
                ? "No description available."
                : description;
        }

        if (optionBinding.TypeText != null)
            optionBinding.TypeText.text = offer.UnlockKind == PlayerPowerUpUnlockKind.Active ? "Active" : "Passive";
    }

    private static void SetOptionUnused(MilestonePowerUpSelectionOptionBinding optionBinding)
    {
        if (optionBinding.SelectButton != null)
        {
            if (optionBinding.HideWhenUnused)
            {
                if (optionBinding.SelectButton.gameObject.activeSelf)
                    optionBinding.SelectButton.gameObject.SetActive(false);
            }
            else
            {
                if (!optionBinding.SelectButton.gameObject.activeSelf)
                    optionBinding.SelectButton.gameObject.SetActive(true);

                optionBinding.SelectButton.interactable = false;
            }
        }

        if (optionBinding.NameText != null)
            optionBinding.NameText.text = string.Empty;

        if (optionBinding.DescriptionText != null)
            optionBinding.DescriptionText.text = string.Empty;

        if (optionBinding.TypeText != null)
            optionBinding.TypeText.text = string.Empty;
    }

    private void SetOptionButtonsInteractable(bool interactable)
    {
        if (optionBindings == null || optionBindings.Count <= 0)
            return;

        for (int optionIndex = 0; optionIndex < optionBindings.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionBinding optionBinding = optionBindings[optionIndex];

            if (optionBinding == null || optionBinding.SelectButton == null)
                continue;

            optionBinding.SelectButton.interactable = interactable;
        }
    }
    #endregion

    #region Commands
    private void HandleOptionButtonPressed(int optionIndex)
    {
        if (!hasRuntimeContext)
            return;

        if (!TryQueueSelectionCommand(optionIndex))
            return;

        if (!lockButtonsAfterSelectionClick)
            return;

        SetOptionButtonsInteractable(false);
    }

    private bool TryQueueSelectionCommand(int offerIndex)
    {
        if (playerEntity == Entity.Null)
            return false;

        if (!entityManager.HasComponent<PlayerMilestonePowerUpSelectionState>(playerEntity))
            return false;

        PlayerMilestonePowerUpSelectionState selectionState = entityManager.GetComponentData<PlayerMilestonePowerUpSelectionState>(playerEntity);

        if (selectionState.IsSelectionActive == 0)
            return false;

        if (!entityManager.HasBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity))
            return false;

        DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> offersBuffer = entityManager.GetBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity);

        if (offerIndex < 0 || offerIndex >= offersBuffer.Length)
            return false;

        if (!entityManager.HasBuffer<PlayerMilestonePowerUpSelectionCommand>(playerEntity))
            return false;

        DynamicBuffer<PlayerMilestonePowerUpSelectionCommand> selectionCommandsBuffer = entityManager.GetBuffer<PlayerMilestonePowerUpSelectionCommand>(playerEntity);
        selectionCommandsBuffer.Clear();
        selectionCommandsBuffer.Add(new PlayerMilestonePowerUpSelectionCommand
        {
            OfferIndex = offerIndex
        });
        return true;
    }
    #endregion

    #endregion
}
