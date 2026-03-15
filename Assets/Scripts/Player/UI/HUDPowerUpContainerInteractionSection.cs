using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Handles world-space prompts and overlay interactions for dropped active power-up containers.
/// /params none.
/// /returns none.
/// </summary>
[System.Serializable]
public sealed class HUDPowerUpContainerInteractionSection
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Full-screen overlay root shown when Overlay Panel mode is opened from a dropped power-up container.")]
    [SerializeField] private GameObject overlayPanelRoot;

    [Tooltip("Optional title text updated with the dropped power-up display name inside the overlay panel.")]
    [SerializeField] private TMP_Text overlayTitleText;

    [Tooltip("Optional description text updated with the dropped power-up description inside the overlay panel.")]
    [SerializeField] private TMP_Text overlayDescriptionText;

    [Tooltip("Optional icon image updated with the dropped power-up sprite inside the overlay panel.")]
    [SerializeField] private Image overlayIconImage;

    [Tooltip("Button that swaps the dropped power-up into the primary active slot.")]
    [SerializeField] private Button replacePrimaryButton;

    [Tooltip("Optional label used to customize the primary-slot button text.")]
    [SerializeField] private TMP_Text replacePrimaryButtonText;

    [Tooltip("Button that swaps the dropped power-up into the secondary active slot.")]
    [SerializeField] private Button replaceSecondaryButton;

    [Tooltip("Optional label used to customize the secondary-slot button text.")]
    [SerializeField] private TMP_Text replaceSecondaryButtonText;
    #endregion

    private Button registeredPrimaryButton;
    private Button registeredSecondaryButton;
    private UnityEngine.Events.UnityAction registeredPrimaryButtonAction;
    private UnityEngine.Events.UnityAction registeredSecondaryButtonAction;
    private EntityManager entityManager;
    private Entity currentPlayerEntity;
    private Entity promptContainerEntity;
    private PlayerDroppedPowerUpContainerView promptContainerView;
    private Entity overlayContainerEntity;
    private PlayerDroppedPowerUpContainerView overlayContainerView;
    private bool overlayOpen;
    private bool isTimeScaleResuming;
    private float resumeStartTimeScale;
    private float resumeTargetTimeScale = 1f;
    private float resumeDurationSeconds;
    private float resumeElapsedSeconds;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers button listeners and applies the initial hidden state.
    /// /params none.
    /// /returns void.
    /// </summary>
    public void Initialize()
    {
        CacheButtonTexts();
        RegisterButtons();
        HideOverlayImmediate();
    }

    /// <summary>
    /// Unregisters button listeners and restores a safe default Time.timeScale.
    /// /params none.
    /// /returns void.
    /// </summary>
    public void Dispose()
    {
        UnregisterButtons();
        currentPlayerEntity = Entity.Null;
        HideTrackedPromptView();
        promptContainerEntity = Entity.Null;
        promptContainerView = null;
        overlayContainerEntity = Entity.Null;
        overlayContainerView = null;
        overlayOpen = false;
        StopTimeScaleResume();
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Clears presentation state when no valid player entity is available.
    /// /params none.
    /// /returns void.
    /// </summary>
    public void HandleMissingPlayer()
    {
        currentPlayerEntity = Entity.Null;
        HideTrackedPromptView();
        promptContainerEntity = Entity.Null;
        promptContainerView = null;
        HideOverlayImmediate();
        StopTimeScaleResume();
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Updates dropped-container prompts, overlay visibility, and swap command submission for the current player entity.
    /// /params runtimeEntityManager: Entity manager used to read and write runtime ECS state.
    /// /params playerEntity: Current local player entity driving the HUD.
    /// /returns void.
    /// </summary>
    public void Update(EntityManager runtimeEntityManager, Entity playerEntity)
    {
        entityManager = runtimeEntityManager;
        currentPlayerEntity = playerEntity;
        CacheButtonTexts();
        RegisterButtons();

        bool milestoneSelectionActive = IsMilestoneSelectionActive(playerEntity);
        UpdateTimeScaleResume(milestoneSelectionActive);

        if (!entityManager.Exists(playerEntity) ||
            !entityManager.HasComponent<PlayerPowerUpContainerInteractionConfig>(playerEntity) ||
            !entityManager.HasComponent<PlayerPowerUpContainerProximityState>(playerEntity))
        {
            HideTrackedPromptView();
            promptContainerEntity = Entity.Null;
            promptContainerView = null;

            if (overlayOpen)
                CloseOverlay(true);

            return;
        }

        if (overlayOpen)
        {
            HandleOverlayUpdate(milestoneSelectionActive);
            return;
        }

        if (milestoneSelectionActive)
        {
            HideTrackedPromptView();
            promptContainerEntity = Entity.Null;
            promptContainerView = null;
            return;
        }

        PlayerPowerUpContainerInteractionConfig interactionConfig = entityManager.GetComponentData<PlayerPowerUpContainerInteractionConfig>(playerEntity);
        PlayerPowerUpContainerProximityState proximityState = entityManager.GetComponentData<PlayerPowerUpContainerProximityState>(playerEntity);

        if (proximityState.HasContainerInRange == 0 ||
            !IsContainerUsable(proximityState.NearestContainerEntity) ||
            !TryResolveContainerView(proximityState.NearestContainerEntity, out PlayerDroppedPowerUpContainerView containerView))
        {
            HideTrackedPromptView();
            promptContainerEntity = Entity.Null;
            promptContainerView = null;
            return;
        }

        if (promptContainerEntity != proximityState.NearestContainerEntity)
        {
            HideTrackedPromptView();
            promptContainerEntity = proximityState.NearestContainerEntity;
            promptContainerView = containerView;
        }
        else
        {
            promptContainerView = containerView;
        }

        switch (interactionConfig.InteractionMode)
        {
            case PlayerPowerUpContainerInteractionMode.OverlayPanel:
                UpdateOverlayPrompt(promptContainerEntity, containerView);
                return;
            case PlayerPowerUpContainerInteractionMode.Prompt3D:
                UpdateDirectSwapPrompt(playerEntity, promptContainerEntity, containerView);
                return;
            default:
                containerView.HidePrompts();
                return;
        }
    }
    #endregion

    #region Setup
    /// <summary>
    /// Registers the two overlay buttons used to pick the active slot replacement target.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void RegisterButtons()
    {
        if (!ReferenceEquals(registeredPrimaryButton, replacePrimaryButton))
        {
            if (registeredPrimaryButton != null && registeredPrimaryButtonAction != null)
                registeredPrimaryButton.onClick.RemoveListener(registeredPrimaryButtonAction);

            registeredPrimaryButton = replacePrimaryButton;
            registeredPrimaryButtonAction = HandleReplacePrimaryButtonPressed;

            if (registeredPrimaryButton != null)
                registeredPrimaryButton.onClick.AddListener(registeredPrimaryButtonAction);
        }

        if (!ReferenceEquals(registeredSecondaryButton, replaceSecondaryButton))
        {
            if (registeredSecondaryButton != null && registeredSecondaryButtonAction != null)
                registeredSecondaryButton.onClick.RemoveListener(registeredSecondaryButtonAction);

            registeredSecondaryButton = replaceSecondaryButton;
            registeredSecondaryButtonAction = HandleReplaceSecondaryButtonPressed;

            if (registeredSecondaryButton != null)
                registeredSecondaryButton.onClick.AddListener(registeredSecondaryButtonAction);
        }
    }

    /// <summary>
    /// Removes the listeners registered on the overlay action buttons.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void UnregisterButtons()
    {
        if (registeredPrimaryButton != null && registeredPrimaryButtonAction != null)
            registeredPrimaryButton.onClick.RemoveListener(registeredPrimaryButtonAction);

        if (registeredSecondaryButton != null && registeredSecondaryButtonAction != null)
            registeredSecondaryButton.onClick.RemoveListener(registeredSecondaryButtonAction);

        registeredPrimaryButton = null;
        registeredSecondaryButton = null;
        registeredPrimaryButtonAction = null;
        registeredSecondaryButtonAction = null;
    }

    /// <summary>
    /// Auto-resolves button labels from the assigned button hierarchy when explicit references are missing.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void CacheButtonTexts()
    {
        if (replacePrimaryButtonText == null && replacePrimaryButton != null)
            replacePrimaryButtonText = replacePrimaryButton.GetComponentInChildren<TMP_Text>(true);

        if (replaceSecondaryButtonText == null && replaceSecondaryButton != null)
            replaceSecondaryButtonText = replaceSecondaryButton.GetComponentInChildren<TMP_Text>(true);
    }
    #endregion

    #region Update
    /// <summary>
    /// Updates overlay-mode prompt text and opens the full-screen panel on a fresh interaction press.
    /// /params playerEntity: Current player entity.
    /// /params containerEntity: Nearest dropped container currently in range.
    /// /params containerView: Companion view used to display the world-space prompt.
    /// /returns void.
    /// </summary>
    private void UpdateOverlayPrompt(Entity containerEntity, PlayerDroppedPowerUpContainerView containerView)
    {
        InputAction interactAction = PlayerInputRuntime.PowerUpContainerInteractAction;
        string bindingDisplayString = ResolveBindingDisplayString(interactAction, "F");
        containerView.ShowSinglePrompt(string.Format("Press [{0}] to swap", bindingDisplayString));

        if (interactAction == null)
            return;

        if (!interactAction.WasPressedThisFrame())
            return;

        OpenOverlay(containerEntity, containerView);
    }

    /// <summary>
    /// Updates 3D Prompt mode and queues an authoritative swap command when one direct-replacement action is pressed.
    /// /params playerEntity: Current player entity.
    /// /params containerEntity: Nearest dropped container currently in range.
    /// /params containerView: Companion view used to display the world-space prompt.
    /// /returns void.
    /// </summary>
    private void UpdateDirectSwapPrompt(Entity playerEntity, Entity containerEntity, PlayerDroppedPowerUpContainerView containerView)
    {
        InputAction replacePrimaryAction = PlayerInputRuntime.PowerUpContainerReplacePrimaryAction;
        InputAction replaceSecondaryAction = PlayerInputRuntime.PowerUpContainerReplaceSecondaryAction;
        string primaryBindingDisplayString = ResolveBindingDisplayString(replacePrimaryAction, "1");
        string secondaryBindingDisplayString = ResolveBindingDisplayString(replaceSecondaryAction, "2");

        containerView.ShowSwapPrompt(string.Format("[{0}] Slot 1", primaryBindingDisplayString),
                                     string.Format("[{0}] Slot 2", secondaryBindingDisplayString));

        if (replacePrimaryAction != null && replacePrimaryAction.WasPressedThisFrame())
        {
            TryQueueSwapCommand(playerEntity, containerEntity, 0);
            return;
        }

        if (replaceSecondaryAction != null && replaceSecondaryAction.WasPressedThisFrame())
            TryQueueSwapCommand(playerEntity, containerEntity, 1);
    }

    /// <summary>
    /// Updates the overlay state while it is open and closes it when canceled or invalidated.
    /// /params playerEntity: Current player entity.
    /// /params milestoneSelectionActive: True when a milestone selection is currently open and must keep gameplay paused.
    /// /returns void.
    /// </summary>
    private void HandleOverlayUpdate(bool milestoneSelectionActive)
    {
        HideTrackedPromptView();

        if (!IsContainerUsable(overlayContainerEntity))
        {
            CloseOverlay(true);
            return;
        }

        UpdateOverlayContent(overlayContainerEntity);

        if (milestoneSelectionActive)
            return;

        InputAction cancelAction = PlayerInputRuntime.UICancelAction;

        if (cancelAction != null && cancelAction.WasPressedThisFrame())
            CloseOverlay(true);
    }
    #endregion

    #region Overlay
    /// <summary>
    /// Opens the full-screen overlay for the specified dropped container and pauses gameplay immediately.
    /// /params containerEntity: Dropped container selected by the player.
    /// /returns void.
    /// </summary>
    private void OpenOverlay(Entity containerEntity, PlayerDroppedPowerUpContainerView containerView)
    {
        if (overlayPanelRoot == null)
            return;

        overlayContainerEntity = containerEntity;
        overlayContainerView = containerView;
        overlayOpen = true;
        StopTimeScaleResume();
        CancelMilestoneTimeScaleResume();
        UpdateOverlayContent(containerEntity);

        if (!overlayPanelRoot.activeSelf)
            overlayPanelRoot.SetActive(true);

        Time.timeScale = 0f;
        HideTrackedPromptView();
        promptContainerEntity = containerEntity;
        promptContainerView = containerView;

        if (replacePrimaryButton != null)
            replacePrimaryButton.interactable = true;

        if (replaceSecondaryButton != null)
            replaceSecondaryButton.interactable = true;
    }

    /// <summary>
    /// Updates overlay labels with the current dropped power-up metadata.
    /// /params containerEntity: Dropped container currently shown by the overlay.
    /// /returns void.
    /// </summary>
    private void UpdateOverlayContent(Entity containerEntity)
    {
        if (!entityManager.Exists(containerEntity) || !entityManager.HasComponent<PlayerDroppedPowerUpContainerContent>(containerEntity))
            return;

        PlayerDroppedPowerUpContainerContent containerContent = entityManager.GetComponentData<PlayerDroppedPowerUpContainerContent>(containerEntity);
        string powerUpId = containerContent.StoredPowerUp.SlotConfig.PowerUpId.ToString();
        string title = PlayerPowerUpPresentationRuntime.ResolveDisplayName(powerUpId, powerUpId);
        string description = string.Empty;

        if (PlayerPowerUpPresentationRuntime.TryResolveEntry(powerUpId, out PlayerPowerUpPresentationRuntime.PowerUpPresentationEntry presentationEntry))
            description = presentationEntry.Description;

        if (overlayTitleText != null)
            overlayTitleText.text = title;

        if (overlayDescriptionText != null)
            overlayDescriptionText.text = string.IsNullOrWhiteSpace(description) ? "Choose which active slot to replace." : description;

        if (overlayIconImage != null)
        {
            if (PlayerPowerUpPresentationRuntime.TryResolveIcon(powerUpId, out Sprite icon))
            {
                overlayIconImage.sprite = icon;
                overlayIconImage.enabled = true;
            }
            else
            {
                overlayIconImage.sprite = null;
                overlayIconImage.enabled = false;
            }
        }

        if (replacePrimaryButtonText != null)
            replacePrimaryButtonText.text = "Replace Slot 1";

        if (replaceSecondaryButtonText != null)
            replaceSecondaryButtonText.text = "Replace Slot 2";
    }

    /// <summary>
    /// Closes the overlay and starts the configured Time.timeScale resume.
    /// /params resumeTimeScale: True to restore Time.timeScale using the configured duration; false to restore it immediately.
    /// /returns void.
    /// </summary>
    private void CloseOverlay(bool resumeTimeScale)
    {
        if (!overlayOpen)
            return;

        Entity containerEntity = overlayContainerEntity;
        PlayerDroppedPowerUpContainerView closedOverlayContainerView = overlayContainerView;
        overlayOpen = false;
        overlayContainerEntity = Entity.Null;
        overlayContainerView = null;

        if (overlayPanelRoot != null && overlayPanelRoot.activeSelf)
            overlayPanelRoot.SetActive(false);

        if (resumeTimeScale)
            BeginTimeScaleResume();
        else
            Time.timeScale = 1f;

        if (closedOverlayContainerView != null)
            closedOverlayContainerView.HidePrompts();

        if (containerEntity == promptContainerEntity)
            HideTrackedPromptView();
    }

    /// <summary>
    /// Immediately hides the overlay panel without creating a Time.timeScale resume.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void HideOverlayImmediate()
    {
        overlayOpen = false;
        overlayContainerEntity = Entity.Null;

        if (overlayContainerView != null)
            overlayContainerView.HidePrompts();

        overlayContainerView = null;

        if (overlayPanelRoot != null && overlayPanelRoot.activeSelf)
            overlayPanelRoot.SetActive(false);
    }

    #endregion

    #region Commands
    /// <summary>
    /// Handles the overlay primary-slot button press by queuing one authoritative swap command.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void HandleReplacePrimaryButtonPressed()
    {
        TryQueueOverlaySwapCommand(0);
    }

    /// <summary>
    /// Handles the overlay secondary-slot button press by queuing one authoritative swap command.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void HandleReplaceSecondaryButtonPressed()
    {
        TryQueueOverlaySwapCommand(1);
    }

    /// <summary>
    /// Queues one authoritative swap command from the currently open overlay and closes it afterward.
    /// /params targetSlotIndex: Selected active-slot index. 0 is primary and 1 is secondary.
    /// /returns void.
    /// </summary>
    private void TryQueueOverlaySwapCommand(int targetSlotIndex)
    {
        if (!overlayOpen)
            return;

        if (currentPlayerEntity == Entity.Null)
            return;

        if (!TryQueueSwapCommand(currentPlayerEntity, overlayContainerEntity, targetSlotIndex))
            return;

        CloseOverlay(true);
    }

    /// <summary>
    /// Queues one authoritative dropped-container swap command on the player entity buffer.
    /// /params playerEntity: Player entity receiving the command.
    /// /params containerEntity: Dropped container targeted by the swap.
    /// /params targetSlotIndex: Selected active-slot index. 0 is primary and 1 is secondary.
    /// /returns True when the command was queued; otherwise false.
    /// </summary>
    private bool TryQueueSwapCommand(Entity playerEntity, Entity containerEntity, int targetSlotIndex)
    {
        if (!entityManager.Exists(playerEntity) ||
            !entityManager.HasBuffer<PlayerPowerUpContainerSwapCommand>(playerEntity) ||
            !IsContainerUsable(containerEntity))
        {
            return false;
        }

        DynamicBuffer<PlayerPowerUpContainerSwapCommand> swapCommands = entityManager.GetBuffer<PlayerPowerUpContainerSwapCommand>(playerEntity);
        swapCommands.Add(new PlayerPowerUpContainerSwapCommand
        {
            ContainerEntity = containerEntity,
            TargetSlotIndex = targetSlotIndex
        });
        HideTrackedPromptView();
        return true;
    }

    #endregion

    #region Time Scale
    /// <summary>
    /// Starts the unscaled Time.timeScale resume configured on the current player interaction settings.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void BeginTimeScaleResume()
    {
        if (currentPlayerEntity == Entity.Null ||
            !entityManager.Exists(currentPlayerEntity) ||
            !entityManager.HasComponent<PlayerPowerUpContainerInteractionConfig>(currentPlayerEntity))
        {
            Time.timeScale = 1f;
            StopTimeScaleResume();
            return;
        }

        PlayerPowerUpContainerInteractionConfig interactionConfig = entityManager.GetComponentData<PlayerPowerUpContainerInteractionConfig>(currentPlayerEntity);
        resumeDurationSeconds = Mathf.Max(0f, interactionConfig.OverlayPanelTimeScaleResumeDurationSeconds);

        if (resumeDurationSeconds <= 0f)
        {
            Time.timeScale = 1f;
            StopTimeScaleResume();
            return;
        }

        resumeStartTimeScale = Mathf.Clamp01(Time.timeScale);
        resumeTargetTimeScale = 1f;
        resumeElapsedSeconds = 0f;
        isTimeScaleResuming = true;
    }

    /// <summary>
    /// Advances the unscaled Time.timeScale resume when no milestone selection currently needs a hard pause.
    /// /params milestoneSelectionActive: True when milestone selection is currently forcing Time.timeScale to 0.
    /// /returns void.
    /// </summary>
    private void UpdateTimeScaleResume(bool milestoneSelectionActive)
    {
        if (!isTimeScaleResuming)
            return;

        if (milestoneSelectionActive)
            return;

        if (resumeDurationSeconds <= 0f)
        {
            Time.timeScale = resumeTargetTimeScale;
            StopTimeScaleResume();
            return;
        }

        resumeElapsedSeconds += Time.unscaledDeltaTime;
        float normalizedProgress = Mathf.Clamp01(resumeElapsedSeconds / resumeDurationSeconds);
        Time.timeScale = Mathf.Lerp(resumeStartTimeScale, resumeTargetTimeScale, normalizedProgress);

        if (normalizedProgress < 1f)
            return;

        Time.timeScale = resumeTargetTimeScale;
        StopTimeScaleResume();
    }

    /// <summary>
    /// Clears the in-progress Time.timeScale resume state.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void StopTimeScaleResume()
    {
        isTimeScaleResuming = false;
        resumeStartTimeScale = 0f;
        resumeTargetTimeScale = 1f;
        resumeDurationSeconds = 0f;
        resumeElapsedSeconds = 0f;
    }

    /// <summary>
    /// Cancels the milestone-driven Time.timeScale resume so the container overlay can keep gameplay paused until the player confirms or cancels the swap.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void CancelMilestoneTimeScaleResume()
    {
        if (currentPlayerEntity == Entity.Null)
            return;

        if (!entityManager.Exists(currentPlayerEntity))
            return;

        if (!entityManager.HasComponent<PlayerMilestoneTimeScaleResumeState>(currentPlayerEntity))
            return;

        entityManager.SetComponentData(currentPlayerEntity,
                                       PlayerMilestoneSelectionOutcomeUtility.CreateInactiveResumeState());
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Returns whether milestone selection is currently active on the player entity.
    /// /params playerEntity: Player entity inspected for milestone selection state.
    /// /returns True when milestone selection is active; otherwise false.
    /// </summary>
    private bool IsMilestoneSelectionActive(Entity playerEntity)
    {
        if (!entityManager.Exists(playerEntity) || !entityManager.HasComponent<PlayerMilestonePowerUpSelectionState>(playerEntity))
            return false;

        return entityManager.GetComponentData<PlayerMilestonePowerUpSelectionState>(playerEntity).IsSelectionActive != 0;
    }

    /// <summary>
    /// Returns whether the target dropped container entity still exists and stores one valid power-up payload.
    /// /params containerEntity: Dropped container entity inspected for usability.
    /// /returns True when the container can still be interacted with; otherwise false.
    /// </summary>
    private bool IsContainerUsable(Entity containerEntity)
    {
        if (containerEntity == Entity.Null || !entityManager.Exists(containerEntity))
            return false;

        if (!entityManager.HasComponent<PlayerDroppedPowerUpContainerContent>(containerEntity))
            return false;

        PlayerDroppedPowerUpContainerContent containerContent = entityManager.GetComponentData<PlayerDroppedPowerUpContainerContent>(containerEntity);
        return containerContent.StoredPowerUp.SlotConfig.IsDefined != 0;
    }

    /// <summary>
    /// Resolves the companion view attached to one dropped container entity.
    /// /params containerEntity: Dropped container entity inspected for a companion view.
    /// /params containerView: Resolved companion view when available.
    /// /returns True when the view exists; otherwise false.
    /// </summary>
    private bool TryResolveContainerView(Entity containerEntity, out PlayerDroppedPowerUpContainerView containerView)
    {
        containerView = null;

        if (containerEntity == Entity.Null)
            return false;

        return PlayerDroppedPowerUpContainerViewRuntimeUtility.TryResolveRuntimeView(entityManager,
                                                                                     containerEntity,
                                                                                     out containerView);
    }

    /// <summary>
    /// Hides the world-space prompt on one dropped container when its companion view is available.
    /// /params containerEntity: Dropped container whose prompt must be hidden.
    /// /returns void.
    /// </summary>
    private void HidePromptForEntity(Entity containerEntity)
    {
        if (!TryResolveContainerView(containerEntity, out PlayerDroppedPowerUpContainerView containerView))
            return;

        containerView.HidePrompts();
    }

    /// <summary>
    /// Hides the currently tracked prompt view without touching ECS state, allowing safe teardown after the world is destroyed.
    /// /params none.
    /// /returns void.
    /// </summary>
    private void HideTrackedPromptView()
    {
        if (promptContainerView != null)
            promptContainerView.HidePrompts();
    }

    /// <summary>
    /// Resolves one short display string for the first binding associated with the provided action.
    /// /params action: Input action shown to the player.
    /// /params fallback: Fallback string used when no action or binding is available.
    /// /returns Display string rendered inside prompts and overlay labels.
    /// </summary>
    private static string ResolveBindingDisplayString(InputAction action, string fallback)
    {
        return PlayerInputRuntime.ResolveBindingDisplayString(action, fallback);
    }
    #endregion

    #endregion
}
