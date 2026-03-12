using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Handles milestone power-up card rendering, custom UI navigation, pointer selection, and ECS command submission.
/// </summary>
[System.Serializable]
public sealed class HUDMilestoneSelectionSection
{
    #region Fields

    #region Constants
    private const int MaxSelectableOffers = 6;
    #endregion

    #region Serialized Fields
    [Tooltip("Root panel shown while a milestone power-up selection is active.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Header text updated with the milestone level and current offer count.")]
    [SerializeField] private TMP_Text headerText;

    [Tooltip("Legacy button-based option widgets kept for backward compatibility with older HUD layouts.")]
    [SerializeField] private List<MilestonePowerUpSelectionOptionBinding> optionBindings = new List<MilestonePowerUpSelectionOptionBinding>();

    [Tooltip("Optional skip button that closes the milestone selection without taking an unlock.")]
    [SerializeField] private Button skipButton;

    [Tooltip("Automatically discovers card views under PowerUpsPanel/PowerUpList and uses them for image-style selection.")]
    [SerializeField] private bool autoDiscoverOptionViewsFromPanelRoot = true;

    [Tooltip("Minimum Navigate axis magnitude required before a custom card-navigation step is accepted.")]
    [SerializeField] private float navigationInputDeadzone = 0.5f;

    [Tooltip("Minimum unscaled time required between two accepted custom navigation steps.")]
    [SerializeField] private float navigationRepeatCooldownSeconds = 0.15f;

    [Tooltip("Loops the current selection from last card to first card and vice versa.")]
    [SerializeField] private bool wrapNavigation = true;

    [Tooltip("Moves the current keyboard or gamepad selection to the card under the mouse pointer.")]
    [SerializeField] private bool followPointerHoverSelection = true;

    [Tooltip("Disables default EventSystem navigation while the milestone panel is open to avoid duplicate Submit/Navigate processing.")]
    [SerializeField] private bool suspendEventSystemNavigationWhileSelectionActive = true;

    [Tooltip("Automatically queues the first rolled offer when no selection UI and no skip button are configured.")]
    [SerializeField] private bool autoSelectFirstOfferWhenUiMissing = true;

    [Tooltip("Blocks further card, button, and skip interactions immediately after a command is queued.")]
    [SerializeField] private bool lockButtonsAfterSelectionClick = true;
    #endregion

    private readonly List<Button> registeredButtons = new List<Button>(MaxSelectableOffers);
    private readonly List<UnityAction> registeredActions = new List<UnityAction>(MaxSelectableOffers);
    private readonly List<MilestonePowerUpSelectionOptionView> discoveredOptionViews = new List<MilestonePowerUpSelectionOptionView>(MaxSelectableOffers);
    private Button registeredSkipButton;
    private UnityAction registeredSkipAction;
    private GameObject discoveredPanelRoot;
    private InputAction navigateAction;
    private InputAction submitAction;
    private InputAction cancelAction;
    private EntityManager entityManager;
    private Entity playerEntity;
    private EventSystem suppressedEventSystem;
    private bool cachedSendNavigationEvents;
    private bool hasRuntimeContext;
    private bool isPanelVisible;
    private bool interactionLocked;
    private bool navigationInputReleased = true;
    private int activeOfferCount;
    private int selectedOfferIndex = -1;
    private float nextAllowedNavigateUnscaledTime;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers UI listeners, resolves option-card views, and applies the initial hidden state.
    /// </summary>
    /// <returns>Void.</returns>
    public void Initialize()
    {
        RegisterOptionButtons();
        RefreshDiscoveredOptionViews();
        RefreshInputActions();
        HidePanel();
    }

    /// <summary>
    /// Unregisters listeners and restores EventSystem navigation when the owning HUD is destroyed.
    /// </summary>
    /// <returns>Void.</returns>
    public void Dispose()
    {
        RestoreEventSystemNavigationIfNeeded();
        UnregisterInputActions();
        UnregisterOptionViewCallbacks();
        UnregisterOptionButtons();
    }

    /// <summary>
    /// Clears runtime references and hides the milestone panel when the player entity is unavailable.
    /// </summary>
    /// <returns>Void.</returns>
    public void HandleMissingPlayer()
    {
        hasRuntimeContext = false;
        playerEntity = Entity.Null;
        HidePanel();
    }

    /// <summary>
    /// Refreshes milestone HUD visibility, option content, and fallback auto-pick behavior from ECS state.
    /// </summary>
    /// <param name="runtimeEntityManager">Entity manager used to read and write milestone selection data.</param>
    /// <param name="runtimePlayerEntity">Player entity currently driving the HUD.</param>
    /// <returns>Void.</returns>
    public void Update(EntityManager runtimeEntityManager, Entity runtimePlayerEntity)
    {
        RefreshDiscoveredOptionViews();
        RefreshInputActions();

        if (!HUDMilestoneSelectionOptionUtility.HasUiConfigured(panelRoot, skipButton, discoveredOptionViews, optionBindings))
            return;

        entityManager = runtimeEntityManager;
        playerEntity = runtimePlayerEntity;
        hasRuntimeContext = true;

        if (!TryGetActiveSelectionOffers(out PlayerMilestonePowerUpSelectionState selectionState, out DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers))
        {
            HidePanel();
            return;
        }

        if (!HasOfferSelectionUi() && skipButton == null && autoSelectFirstOfferWhenUiMissing)
        {
            TryQueueSelectionCommand(0);
            return;
        }

        ShowPanel(selectionState, selectionOffers);
    }
    #endregion

    #region Setup
    /// <summary>
    /// Registers legacy button listeners and the optional skip button kept for compatibility with existing HUD layouts.
    /// </summary>
    /// <returns>Void.</returns>
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
                UnityAction clickAction = () => HandleOptionSelected(capturedOptionIndex);
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

    /// <summary>
    /// Removes all legacy button listeners registered by Initialize.
    /// </summary>
    /// <returns>Void.</returns>
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

    /// <summary>
    /// Rebuilds the auto-discovered option-card list when the configured panel root changes.
    /// </summary>
    /// <returns>Void.</returns>
    private void RefreshDiscoveredOptionViews()
    {
        if (!autoDiscoverOptionViewsFromPanelRoot)
            return;

        if (panelRoot == null)
        {
            discoveredPanelRoot = null;
            UnregisterOptionViewCallbacks();
            discoveredOptionViews.Clear();
            return;
        }

        if (ReferenceEquals(discoveredPanelRoot, panelRoot) && discoveredOptionViews.Count > 0)
            return;

        UnregisterOptionViewCallbacks();
        HUDMilestoneSelectionOptionUtility.DiscoverOptionViews(panelRoot, discoveredOptionViews, MaxSelectableOffers);

        for (int optionIndex = 0; optionIndex < discoveredOptionViews.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionView optionView = discoveredOptionViews[optionIndex];

            if (optionView == null)
                continue;

            optionView.RegisterCallbacks(HandleOptionViewClicked, HandleOptionViewHovered);
        }

        discoveredPanelRoot = panelRoot;
    }

    /// <summary>
    /// Clears registered pointer callbacks from all discovered card views.
    /// </summary>
    /// <returns>Void.</returns>
    private void UnregisterOptionViewCallbacks()
    {
        for (int optionIndex = 0; optionIndex < discoveredOptionViews.Count; optionIndex++)
        {
            MilestonePowerUpSelectionOptionView optionView = discoveredOptionViews[optionIndex];

            if (optionView == null)
                continue;

            optionView.ClearCallbacks();
        }
    }

    /// <summary>
    /// Rebinds custom UI actions whenever the runtime input asset is recreated by InputAuthoring.
    /// </summary>
    /// <returns>Void.</returns>
    private void RefreshInputActions()
    {
        InputAction runtimeNavigateAction = PlayerInputRuntime.UINavigateAction;
        InputAction runtimeSubmitAction = PlayerInputRuntime.UISubmitAction;
        InputAction runtimeCancelAction = PlayerInputRuntime.UICancelAction;

        if (ReferenceEquals(navigateAction, runtimeNavigateAction) &&
            ReferenceEquals(submitAction, runtimeSubmitAction) &&
            ReferenceEquals(cancelAction, runtimeCancelAction))
            return;

        UnregisterInputActions();
        navigateAction = runtimeNavigateAction;
        submitAction = runtimeSubmitAction;
        cancelAction = runtimeCancelAction;

        if (navigateAction != null)
        {
            navigateAction.performed += HandleNavigatePerformed;
            navigateAction.canceled += HandleNavigateCanceled;
        }

        if (submitAction != null)
            submitAction.performed += HandleSubmitPerformed;

        if (cancelAction != null)
            cancelAction.performed += HandleCancelPerformed;
    }

    /// <summary>
    /// Unregisters custom UI input callbacks from the currently cached runtime actions.
    /// </summary>
    /// <returns>Void.</returns>
    private void UnregisterInputActions()
    {
        if (navigateAction != null)
        {
            navigateAction.performed -= HandleNavigatePerformed;
            navigateAction.canceled -= HandleNavigateCanceled;
        }

        if (submitAction != null)
            submitAction.performed -= HandleSubmitPerformed;

        if (cancelAction != null)
            cancelAction.performed -= HandleCancelPerformed;

        navigateAction = null;
        submitAction = null;
        cancelAction = null;
    }
    #endregion

    #region ECS
    /// <summary>
    /// Resolves the active milestone selection state and offer buffer from the current player entity.
    /// </summary>
    /// <param name="selectionState">Resolved milestone selection state when active.</param>
    /// <param name="selectionOffers">Resolved milestone offer buffer when active.</param>
    /// <returns>True when the player currently owns an active milestone selection; otherwise false.</returns>
    private bool TryGetActiveSelectionOffers(out PlayerMilestonePowerUpSelectionState selectionState,
                                             out DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers)
    {
        selectionState = default;
        selectionOffers = default;

        if (!entityManager.HasComponent<PlayerMilestonePowerUpSelectionState>(playerEntity))
            return false;

        if (!entityManager.HasBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity))
            return false;

        selectionState = entityManager.GetComponentData<PlayerMilestonePowerUpSelectionState>(playerEntity);

        if (selectionState.IsSelectionActive == 0)
            return false;

        selectionOffers = entityManager.GetBuffer<PlayerMilestonePowerUpSelectionOfferElement>(playerEntity);

        if (selectionOffers.Length <= 0)
            return false;

        return true;
    }
    #endregion

    #region UI
    /// <summary>
    /// Returns whether the current milestone panel exposes at least one control that can select a rolled offer.
    /// </summary>
    /// <returns>True when cards or legacy buttons can select an offer; otherwise false.</returns>
    private bool HasOfferSelectionUi()
    {
        if (HUDMilestoneSelectionOptionUtility.HasDiscoveredOptionView(discoveredOptionViews))
            return true;

        return HUDMilestoneSelectionOptionUtility.HasOfferSelectionButton(optionBindings);
    }

    /// <summary>
    /// Populates the milestone panel with current ECS offer data and keeps the selected card index valid.
    /// </summary>
    /// <param name="selectionState">Current milestone selection state component.</param>
    /// <param name="selectionOffers">Current buffer of rolled milestone offers.</param>
    /// <returns>Void.</returns>
    private void ShowPanel(PlayerMilestonePowerUpSelectionState selectionState,
                           DynamicBuffer<PlayerMilestonePowerUpSelectionOfferElement> selectionOffers)
    {
        activeOfferCount = Mathf.Min(selectionOffers.Length, MaxSelectableOffers);
        selectedOfferIndex = HUDMilestoneSelectionNavigationUtility.NormalizeSelectedOfferIndex(selectedOfferIndex, activeOfferCount);

        if (panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);

        if (headerText != null)
            headerText.text = HUDMilestoneSelectionOptionUtility.BuildHeaderText(selectionState.MilestoneLevel, activeOfferCount);

        ApplyPanelVisibleState(true);
        HUDMilestoneSelectionOptionUtility.SetSkipButtonVisible(skipButton, true, !interactionLocked);
        HUDMilestoneSelectionOptionUtility.RenderOptionViews(discoveredOptionViews, selectionOffers, activeOfferCount);
        HUDMilestoneSelectionOptionUtility.RenderOptionBindings(optionBindings, selectionOffers, activeOfferCount);
        HUDMilestoneSelectionOptionUtility.SetOptionInputsInteractable(discoveredOptionViews, optionBindings, skipButton, !interactionLocked);
        HUDMilestoneSelectionOptionUtility.ApplySelectionVisuals(discoveredOptionViews, optionBindings, selectedOfferIndex, activeOfferCount);
    }

    /// <summary>
    /// Hides the milestone panel and resets its transient navigation and interaction state.
    /// </summary>
    /// <returns>Void.</returns>
    private void HidePanel()
    {
        if (panelRoot != null && panelRoot.activeSelf)
            panelRoot.SetActive(false);

        ApplyPanelVisibleState(false);
        HUDMilestoneSelectionOptionUtility.SetSkipButtonVisible(skipButton, false, false);
        HUDMilestoneSelectionOptionUtility.ResetOptionViews(discoveredOptionViews);
        HUDMilestoneSelectionOptionUtility.ResetOptionBindings(optionBindings);
        interactionLocked = false;
        activeOfferCount = 0;
        selectedOfferIndex = -1;
        navigationInputReleased = true;
        nextAllowedNavigateUnscaledTime = 0f;
    }

    /// <summary>
    /// Applies one-time side effects that must run when the panel visibility changes.
    /// </summary>
    /// <param name="isVisible">True when the panel is now visible; false when it is now hidden.</param>
    /// <returns>Void.</returns>
    private void ApplyPanelVisibleState(bool isVisible)
    {
        if (isPanelVisible == isVisible)
            return;

        isPanelVisible = isVisible;

        if (isVisible)
        {
            SuppressEventSystemNavigationIfNeeded();
            return;
        }

        RestoreEventSystemNavigationIfNeeded();
    }

    /// <summary>
    /// Disables default EventSystem navigation while the milestone panel uses custom input handling.
    /// </summary>
    /// <returns>Void.</returns>
    private void SuppressEventSystemNavigationIfNeeded()
    {
        if (!suspendEventSystemNavigationWhileSelectionActive)
            return;

        EventSystem currentEventSystem = EventSystem.current;

        if (currentEventSystem == null)
            return;

        if (ReferenceEquals(suppressedEventSystem, currentEventSystem))
            return;

        RestoreEventSystemNavigationIfNeeded();
        suppressedEventSystem = currentEventSystem;
        cachedSendNavigationEvents = currentEventSystem.sendNavigationEvents;
        currentEventSystem.sendNavigationEvents = false;
        currentEventSystem.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Restores the EventSystem navigation flag cached when the milestone panel became visible.
    /// </summary>
    /// <returns>Void.</returns>
    private void RestoreEventSystemNavigationIfNeeded()
    {
        if (suppressedEventSystem == null)
            return;

        suppressedEventSystem.sendNavigationEvents = cachedSendNavigationEvents;
        suppressedEventSystem = null;
    }
    #endregion

    #region Input
    /// <summary>
    /// Handles one UI Navigate performed event and converts it into a custom card-selection step.
    /// </summary>
    /// <param name="context">Input callback context raised by the Navigate action.</param>
    /// <returns>Void.</returns>
    private void HandleNavigatePerformed(InputAction.CallbackContext context)
    {
        if (!HUDMilestoneSelectionNavigationUtility.CanHandleSelectionInput(hasRuntimeContext, isPanelVisible, interactionLocked, activeOfferCount))
            return;

        if (!navigationInputReleased && Time.unscaledTime < nextAllowedNavigateUnscaledTime)
            return;

        Vector2 navigateValue = context.ReadValue<Vector2>();
        int navigationStep = HUDMilestoneSelectionNavigationUtility.ResolveNavigationStep(navigateValue, navigationInputDeadzone);

        if (navigationStep == 0)
            return;

        int nextOptionIndex = HUDMilestoneSelectionNavigationUtility.MoveSelection(selectedOfferIndex, activeOfferCount, navigationStep, wrapNavigation);

        if (nextOptionIndex == selectedOfferIndex)
            return;

        selectedOfferIndex = nextOptionIndex;
        navigationInputReleased = false;
        nextAllowedNavigateUnscaledTime = Time.unscaledTime + navigationRepeatCooldownSeconds;
        HUDMilestoneSelectionOptionUtility.ApplySelectionVisuals(discoveredOptionViews, optionBindings, selectedOfferIndex, activeOfferCount);
    }

    /// <summary>
    /// Re-arms custom navigation when the Navigate action returns to its neutral value.
    /// </summary>
    /// <param name="context">Input callback context raised by the Navigate action.</param>
    /// <returns>Void.</returns>
    private void HandleNavigateCanceled(InputAction.CallbackContext context)
    {
        navigationInputReleased = true;
    }

    /// <summary>
    /// Resolves the current highlighted offer when the Submit action is pressed.
    /// </summary>
    /// <param name="context">Input callback context raised by the Submit action.</param>
    /// <returns>Void.</returns>
    private void HandleSubmitPerformed(InputAction.CallbackContext context)
    {
        if (!HUDMilestoneSelectionNavigationUtility.CanHandleSelectionInput(hasRuntimeContext, isPanelVisible, interactionLocked, activeOfferCount))
            return;

        HandleOptionSelected(selectedOfferIndex);
    }

    /// <summary>
    /// Maps the Cancel action to the milestone skip flow when the skip button is configured.
    /// </summary>
    /// <param name="context">Input callback context raised by the Cancel action.</param>
    /// <returns>Void.</returns>
    private void HandleCancelPerformed(InputAction.CallbackContext context)
    {
        if (!HUDMilestoneSelectionNavigationUtility.CanHandleSelectionInput(hasRuntimeContext, isPanelVisible, interactionLocked, activeOfferCount))
            return;

        if (skipButton == null)
            return;

        HandleSkipButtonPressed();
    }
    #endregion

    #region Pointer
    /// <summary>
    /// Resolves the clicked card to its offer index and queues the corresponding ECS selection command.
    /// </summary>
    /// <param name="optionView">Card view clicked by the player.</param>
    /// <returns>Void.</returns>
    private void HandleOptionViewClicked(MilestonePowerUpSelectionOptionView optionView)
    {
        if (!HUDMilestoneSelectionNavigationUtility.TryGetOptionViewIndex(discoveredOptionViews, optionView, activeOfferCount, out int optionIndex))
            return;

        HandleOptionSelected(optionIndex);
    }

    /// <summary>
    /// Syncs the current highlighted offer to the card under the mouse pointer.
    /// </summary>
    /// <param name="optionView">Card view currently hovered by the pointer.</param>
    /// <returns>Void.</returns>
    private void HandleOptionViewHovered(MilestonePowerUpSelectionOptionView optionView)
    {
        if (!followPointerHoverSelection)
            return;

        if (!HUDMilestoneSelectionNavigationUtility.CanHandleSelectionInput(hasRuntimeContext, isPanelVisible, interactionLocked, activeOfferCount))
            return;

        if (!HUDMilestoneSelectionNavigationUtility.TryGetOptionViewIndex(discoveredOptionViews, optionView, activeOfferCount, out int optionIndex))
            return;

        if (optionIndex == selectedOfferIndex)
            return;

        selectedOfferIndex = optionIndex;
        HUDMilestoneSelectionOptionUtility.ApplySelectionVisuals(discoveredOptionViews, optionBindings, selectedOfferIndex, activeOfferCount);
    }
    #endregion

    #region Commands
    /// <summary>
    /// Handles one offer selection request coming from cards, buttons, or the Submit action.
    /// </summary>
    /// <param name="optionIndex">Offer index requested by the current UI source.</param>
    /// <returns>Void.</returns>
    private void HandleOptionSelected(int optionIndex)
    {
        if (!hasRuntimeContext)
            return;

        if (optionIndex < 0 || optionIndex >= activeOfferCount)
            return;

        selectedOfferIndex = optionIndex;

        if (!TryQueueSelectionCommand(optionIndex))
            return;

        ApplyCommandLockIfNeeded();
    }

    /// <summary>
    /// Handles the optional skip button click and maps Cancel input to the same ECS command path.
    /// </summary>
    /// <returns>Void.</returns>
    private void HandleSkipButtonPressed()
    {
        if (!hasRuntimeContext)
            return;

        if (!TryQueueSkipCommand())
            return;

        ApplyCommandLockIfNeeded();
    }

    /// <summary>
    /// Applies the post-command interaction lock requested by the current HUD settings.
    /// </summary>
    /// <returns>Void.</returns>
    private void ApplyCommandLockIfNeeded()
    {
        if (!lockButtonsAfterSelectionClick)
            return;

        interactionLocked = true;
        HUDMilestoneSelectionOptionUtility.SetOptionInputsInteractable(discoveredOptionViews, optionBindings, skipButton, false);
    }

    /// <summary>
    /// Queues a power-up selection command for the specified offer index.
    /// </summary>
    /// <param name="offerIndex">Offer index selected by the player.</param>
    /// <returns>True when the command is queued; otherwise false.</returns>
    private bool TryQueueSelectionCommand(int offerIndex)
    {
        return HUDMilestoneSelectionCommandUtility.TryQueueCommand(entityManager,
                                                                   playerEntity,
                                                                   PlayerMilestoneSelectionCommandType.SelectOffer,
                                                                   offerIndex);
    }

    /// <summary>
    /// Queues a skip command for the currently active milestone selection.
    /// </summary>
    /// <returns>True when the command is queued; otherwise false.</returns>
    private bool TryQueueSkipCommand()
    {
        return HUDMilestoneSelectionCommandUtility.TryQueueCommand(entityManager,
                                                                   playerEntity,
                                                                   PlayerMilestoneSelectionCommandType.Skip,
                                                                   -1);
    }
    #endregion

    #endregion
}
