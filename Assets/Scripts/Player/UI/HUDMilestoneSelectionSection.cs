using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Handles HUD rendering and ECS command submission for milestone power-up selection and skip actions.
/// </summary>
[System.Serializable]
public sealed class HUDMilestoneSelectionSection
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Root panel shown when milestone power-up offers are available.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Header text updated with milestone level and selection prompt.")]
    [SerializeField] private TMP_Text headerText;

    [Tooltip("Ordered option widgets mapped to rolled offers by index.")]
    [SerializeField] private List<MilestonePowerUpSelectionOptionBinding> optionBindings = new List<MilestonePowerUpSelectionOptionBinding>();

    [Tooltip("Optional button that skips the current milestone selection without choosing a power-up.")]
    [SerializeField] private Button skipButton;

    [Tooltip("Automatically queues the first offer when selection is active but no option buttons are configured.")]
    [SerializeField] private bool autoSelectFirstOfferWhenUiMissing = true;

    [Tooltip("Disables all option buttons immediately after one click to avoid duplicate commands.")]
    [SerializeField] private bool lockButtonsAfterSelectionClick = true;
    #endregion

    private readonly List<Button> registeredButtons = new List<Button>(4);
    private readonly List<UnityAction> registeredActions = new List<UnityAction>(4);
    private Button registeredSkipButton;
    private UnityAction registeredSkipAction;
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

        if (!HasOfferSelectionButton() && !HasSkipButton() && autoSelectFirstOfferWhenUiMissing)
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

        if (optionBindings != null && optionBindings.Count > 0)
        {
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

        if (skipButton == null)
            return;

        registeredSkipButton = skipButton;
        registeredSkipAction = HandleSkipButtonPressed;
        registeredSkipButton.onClick.AddListener(registeredSkipAction);
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

        if (registeredSkipButton != null && registeredSkipAction != null)
            registeredSkipButton.onClick.RemoveListener(registeredSkipAction);

        registeredSkipButton = null;
        registeredSkipAction = null;
    }

    private bool HasUiConfigured()
    {
        if (panelRoot != null)
            return true;

        if (skipButton != null)
            return true;

        return optionBindings != null && optionBindings.Count > 0;
    }

    /// <summary>
    /// Returns whether at least one configured option button can submit a power-up selection command.
    /// </summary>
    /// <returns>True when an offer-selection button exists; otherwise false.</returns>
    private bool HasOfferSelectionButton()
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

    /// <summary>
    /// Returns whether a skip button is configured for the milestone selection panel.
    /// </summary>
    /// <returns>True when the skip button reference is assigned; otherwise false.</returns>
    private bool HasSkipButton()
    {
        return skipButton != null;
    }


    private void ShowPanel(PlayerMilestonePowerUpSelectionState selectionState,
                           DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers)
    {
        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        if (headerText != null)
            headerText.text = string.Format("Milestone Lv {0} - Choose 1 of {1} Power-Ups", selectionState.MilestoneLevel, selectionOffers.Length);

        SetSkipButtonVisible(true);

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

        SetSkipButtonVisible(false);

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
        {
            SetSkipButtonInteractable(interactable);
            return;
        }

        for (int optionIndex = 0; optionIndex < optionBindings.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionBinding optionBinding = optionBindings[optionIndex];

            if (optionBinding == null || optionBinding.SelectButton == null)
                continue;

            optionBinding.SelectButton.interactable = interactable;
        }

        SetSkipButtonInteractable(interactable);
    }

    /// <summary>
    /// Shows or hides the optional skip button depending on the milestone selection state.
    /// </summary>
    /// <param name="isVisible">True to show the skip button; false to hide it.</param>
    /// <returns>Void.</returns>
    private void SetSkipButtonVisible(bool isVisible)
    {
        if (skipButton == null)
            return;

        GameObject skipButtonObject = skipButton.gameObject;

        if (skipButtonObject.activeSelf == isVisible)
        {
            skipButton.interactable = isVisible;
            return;
        }

        skipButtonObject.SetActive(isVisible);
        skipButton.interactable = isVisible;
    }

    /// <summary>
    /// Enables or disables interaction on the optional skip button.
    /// </summary>
    /// <param name="interactable">True to allow clicks; false to block them.</param>
    /// <returns>Void.</returns>
    private void SetSkipButtonInteractable(bool interactable)
    {
        if (skipButton == null)
            return;

        skipButton.interactable = interactable;
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

    /// <summary>
    /// Handles the optional skip button click and submits a skip command to ECS.
    /// </summary>
    /// <returns>Void.</returns>
    private void HandleSkipButtonPressed()
    {
        if (!hasRuntimeContext)
            return;

        if (!TryQueueSkipCommand())
            return;

        if (!lockButtonsAfterSelectionClick)
            return;

        SetOptionButtonsInteractable(false);
    }

    /// <summary>
    /// Queues a power-up selection command for the specified offer index.
    /// </summary>
    /// <param name="offerIndex">Offer index selected by the player.</param>
    /// <returns>True when the command is queued; otherwise false.</returns>
    private bool TryQueueSelectionCommand(int offerIndex)
    {
        return TryQueueCommand(PlayerMilestoneSelectionCommandType.SelectOffer, offerIndex);
    }

    /// <summary>
    /// Queues a skip command for the currently active milestone selection.
    /// </summary>
    /// <returns>True when the command is queued; otherwise false.</returns>
    private bool TryQueueSkipCommand()
    {
        return TryQueueCommand(PlayerMilestoneSelectionCommandType.Skip, -1);
    }

    /// <summary>
    /// Queues one generic milestone-selection command after validating the current runtime state.
    /// </summary>
    /// <param name="commandType">Command kind requested by the HUD.</param>
    /// <param name="offerIndex">Offer index used by selection commands, or -1 for skip.</param>
    /// <returns>True when the command is queued; otherwise false.</returns>
    private bool TryQueueCommand(PlayerMilestoneSelectionCommandType commandType, int offerIndex)
    {
        if (playerEntity == Entity.Null)
            return false;

        if (!entityManager.HasComponent<PlayerMilestonePowerUpSelectionState>(playerEntity))
            return false;

        PlayerMilestonePowerUpSelectionState selectionState = entityManager.GetComponentData<PlayerMilestonePowerUpSelectionState>(playerEntity);

        if (selectionState.IsSelectionActive == 0)
            return false;

        if (commandType == PlayerMilestoneSelectionCommandType.SelectOffer)
        {
            if (!entityManager.HasBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity))
                return false;

            DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> offersBuffer = entityManager.GetBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity);

            if (offerIndex < 0 || offerIndex >= offersBuffer.Length)
                return false;
        }

        if (!entityManager.HasBuffer<PlayerMilestonePowerUpSelectionCommand>(playerEntity))
            return false;

        DynamicBuffer<PlayerMilestonePowerUpSelectionCommand> selectionCommandsBuffer = entityManager.GetBuffer<PlayerMilestonePowerUpSelectionCommand>(playerEntity);
        selectionCommandsBuffer.Clear();
        selectionCommandsBuffer.Add(new PlayerMilestonePowerUpSelectionCommand
        {
            CommandType = commandType,
            OfferIndex = offerIndex
        });
        return true;
    }
    #endregion

    #endregion
}
