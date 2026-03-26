using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles the simple front-end scene flow for the authored main menu.
///  None.
/// returns None.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Buttons")]
    [Tooltip("Button that starts the gameplay scene.")]
    [SerializeField] private Button playButton;

    [Tooltip("Button that closes the application.")]
    [SerializeField] private Button quitButton;

    [Header("Scene Flow")]
    [Tooltip("Gameplay scene loaded when Play is selected.")]
    [SerializeField] private string gameplaySceneName = "SCN_PlayerControllerTesting";

    [Header("Navigation")]
    [Tooltip("Optional EventSystem override used to select the default menu button.")]
    [SerializeField] private EventSystem eventSystemOverride;
    #endregion

    #region Runtime
    private MenuSelectionController selectionController;
    #endregion

    #endregion

    #region Methods

    #region Unity Methods
    private void Awake()
    {
        selectionController = GetComponent<MenuSelectionController>();
    }

    private void OnEnable()
    {
        RegisterButtons();
        SelectDefaultButton();
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDisable()
    {
        UnregisterButtons();
    }
    #endregion

    #region Wiring
    /// <summary>
    /// Registers click handlers for the authored menu buttons.
    ///  None.
    /// returns None.
    /// </summary>
    private void RegisterButtons()
    {
        if (playButton != null)
            playButton.onClick.AddListener(HandlePlayPressed);

        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuitPressed);
    }

    /// <summary>
    /// Removes click handlers from the authored menu buttons.
    ///  None.
    /// returns None.
    /// </summary>
    private void UnregisterButtons()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(HandlePlayPressed);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(HandleQuitPressed);
    }
    #endregion

    #region Callbacks
    /// <summary>
    /// Loads the configured gameplay scene.
    ///  None.
    /// returns None.
    /// </summary>
    private void HandlePlayPressed()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
            return;

        SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// Requests application shutdown through the shared helper.
    ///  None.
    /// returns None.
    /// </summary>
    private void HandleQuitPressed()
    {
        AppUtils.QuitGame();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Selects the first non-null authored menu button so keyboard and controller navigation work immediately.
    ///  None.
    /// returns None.
    /// </summary>
    private void SelectDefaultButton()
    {
        if (selectionController != null && playButton != null)
        {
            selectionController.SelectSelectable(playButton, rememberAsDefault : true);
            return;
        }

        EventSystem resolvedEventSystem = eventSystemOverride != null
            ? eventSystemOverride
            : EventSystem.current;

        if (resolvedEventSystem == null)
            return;

        if (playButton != null)
        {
            Canvas.ForceUpdateCanvases();
            resolvedEventSystem.SetSelectedGameObject(null);
            playButton.Select();
            resolvedEventSystem.SetSelectedGameObject(playButton.gameObject);
            return;
        }

        if (quitButton != null)
        {
            Canvas.ForceUpdateCanvases();
            resolvedEventSystem.SetSelectedGameObject(null);
            quitButton.Select();
            resolvedEventSystem.SetSelectedGameObject(quitButton.gameObject);
        }
    }
    #endregion

    #endregion
}
