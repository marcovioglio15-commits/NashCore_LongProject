using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the authored gameplay-menu prefab, injects it into the gameplay scene, creates the main-menu scene, and refreshes build settings.
/// /params None.
/// /returns None.
/// </summary>
public static class PlayerGameplayMenuSetupUtility
{
    #region Constants
    private const string GameplayMenusPrefabPath = "Assets/Prefabs/UI/PF_GameplayMenus.prefab";
    private const string MainMenuScenePath = "Assets/Scenes/Testing/Main Scenes/UI/SCN_MainMenu.unity";
    private const string GameplayScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_PlayerControllerTesting/SCN_PlayerControllerTesting.unity";
    private const string MainMenuSceneName = "SCN_MainMenu";
    private const string GameplaySceneName = "SCN_PlayerControllerTesting";
    private static readonly Color BackgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f);
    private static readonly Color OverlayColor = new Color(0.03f, 0.05f, 0.07f, 0.78f);
    private static readonly Color PanelColor = new Color(0.1f, 0.14f, 0.18f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.2f, 0.28f, 0.35f, 1f);
    private static readonly Color ButtonHighlightColor = new Color(0.28f, 0.38f, 0.47f, 1f);
    private static readonly Color ButtonPressedColor = new Color(0.16f, 0.22f, 0.27f, 1f);
    private static readonly Color TextColor = new Color(0.95f, 0.97f, 1f, 1f);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Runs the full authored UI setup for menus and gameplay-scene integration.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void ExecuteSetup()
    {
        GameObject gameplayMenusPrefab = EnsureGameplayMenusPrefab();
        EnsureGameplayScene(gameplayMenusPrefab);
        EnsureMainMenuScene();
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    #endregion

    #region Prefab Setup
    /// <summary>
    /// Creates or refreshes the authored gameplay-menu prefab used directly inside the gameplay scene.
    /// /params None.
    /// /returns Gameplay menu prefab asset.
    /// </summary>
    private static GameObject EnsureGameplayMenusPrefab()
    {
        EnsureFolder(Path.GetDirectoryName(GameplayMenusPrefabPath));
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayMenusPrefabPath);
        GameObject prefabContentsRoot = existingPrefab != null
            ? PrefabUtility.LoadPrefabContents(GameplayMenusPrefabPath)
            : new GameObject("PF_GameplayMenus", typeof(RectTransform), typeof(GameplayMenuController));

        try
        {
            prefabContentsRoot.name = "PF_GameplayMenus";
            RectTransform rootRect = EnsureRectTransform(prefabContentsRoot);
            StretchToParent(rootRect);
            DestroyAllChildren(prefabContentsRoot.transform);

            GameObject pauseOverlay = CreateOverlayRoot("PauseMenu", prefabContentsRoot.transform);
            GameObject pausePanel = CreatePanel("PausePanel", pauseOverlay.transform, new Vector2(420f, 430f));
            TMP_Text pauseTitle = CreateTitleText("PauseTitle", "Paused", pausePanel.transform);
            Button resumeButton = CreateMenuButton("ResumeButton", "Resume", pausePanel.transform);
            Button restartButton = CreateMenuButton("RestartButton", "Restart", pausePanel.transform);
            Button mainMenuButton = CreateMenuButton("MainMenuButton", "Main Menu", pausePanel.transform);
            Button quitButton = CreateMenuButton("QuitButton", "Quit", pausePanel.transform);

            GameObject endingOverlay = CreateOverlayRoot("EndingMenu", prefabContentsRoot.transform);
            GameObject endingPanel = CreatePanel("EndingPanel", endingOverlay.transform, new Vector2(460f, 450f));
            TMP_Text endingTitle = CreateTitleText("EndingTitle", "Run Complete", endingPanel.transform);
            TMP_Text endingMessage = CreateBodyText("EndingMessage", "Victory", endingPanel.transform, 30f);
            Button playAgainButton = CreateMenuButton("PlayAgainButton", "Play Again", endingPanel.transform);
            Button endingMainMenuButton = CreateMenuButton("EndingMainMenuButton", "Main Menu", endingPanel.transform);
            Button endingQuitButton = CreateMenuButton("EndingQuitButton", "Quit", endingPanel.transform);

            pauseOverlay.SetActive(false);
            endingOverlay.SetActive(false);
            EnsureLayout(pausePanel.transform);
            EnsureLayout(endingPanel.transform);
            EnsureLayoutElement(pauseTitle.rectTransform.gameObject, 56f);
            EnsureLayoutElement(endingTitle.rectTransform.gameObject, 56f);
            EnsureLayoutElement(endingMessage.rectTransform.gameObject, 64f);
            ConfigureVerticalNavigation(resumeButton, restartButton, mainMenuButton, quitButton);
            ConfigureVerticalNavigation(playAgainButton, endingMainMenuButton, endingQuitButton);

            GameplayMenuController gameplayMenuController = GetOrAddComponent<GameplayMenuController>(prefabContentsRoot);
            MenuSelectionController selectionController = GetOrAddComponent<MenuSelectionController>(prefabContentsRoot);
            SerializedObject serializedController = new SerializedObject(gameplayMenuController);
            serializedController.Update();
            serializedController.FindProperty("pauseMenuRoot").objectReferenceValue = pauseOverlay;
            serializedController.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            serializedController.FindProperty("pauseRestartButton").objectReferenceValue = restartButton;
            serializedController.FindProperty("pauseMainMenuButton").objectReferenceValue = mainMenuButton;
            serializedController.FindProperty("pauseQuitButton").objectReferenceValue = quitButton;
            serializedController.FindProperty("endingMenuRoot").objectReferenceValue = endingOverlay;
            serializedController.FindProperty("endingMessageText").objectReferenceValue = endingMessage;
            serializedController.FindProperty("endingPlayAgainButton").objectReferenceValue = playAgainButton;
            serializedController.FindProperty("endingMainMenuButton").objectReferenceValue = endingMainMenuButton;
            serializedController.FindProperty("endingQuitButton").objectReferenceValue = endingQuitButton;
            serializedController.FindProperty("mainMenuSceneName").stringValue = MainMenuSceneName;
            serializedController.FindProperty("victoryMessage").stringValue = "Victory";
            serializedController.FindProperty("defeatMessage").stringValue = "Defeat";
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedSelectionController = new SerializedObject(selectionController);
            serializedSelectionController.Update();
            serializedSelectionController.FindProperty("defaultSelectable").objectReferenceValue = resumeButton;
            serializedSelectionController.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabContentsRoot, GameplayMenusPrefabPath);
        }
        finally
        {
            if (existingPrefab != null)
                PrefabUtility.UnloadPrefabContents(prefabContentsRoot);
            else
                UnityEngine.Object.DestroyImmediate(prefabContentsRoot);
        }

        GameObject generatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayMenusPrefabPath);

        if (generatedPrefab == null)
            throw new InvalidOperationException(string.Format("Failed to create gameplay menus prefab at '{0}'.", GameplayMenusPrefabPath));

        return generatedPrefab;
    }
    #endregion

    #region Scene Setup
    /// <summary>
    /// Injects the authored gameplay-menu prefab into the gameplay root scene and ensures UI input is ready there.
    /// /params gameplayMenusPrefab: Prefab asset instantiated under the gameplay canvas.
    /// /returns None.
    /// </summary>
    private static void EnsureGameplayScene(GameObject gameplayMenusPrefab)
    {
        Scene gameplayScene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        HUDManager hudManager = FindComponentInScene<HUDManager>(gameplayScene);

        if (hudManager == null)
            throw new InvalidOperationException("HUDManager not found in gameplay scene. Unable to resolve the gameplay canvas.");

        Canvas targetCanvas = hudManager.GetComponentInParent<Canvas>();

        if (targetCanvas == null)
            targetCanvas = ResolveGameplayCanvas(gameplayScene);

        if (targetCanvas == null)
            throw new InvalidOperationException("Gameplay canvas not found in gameplay scene.");

        EnsureSceneEventSystem(gameplayScene);
        RemoveExistingGameplayMenuControllers(gameplayScene);

        GameObject instantiatedMenus = PrefabUtility.InstantiatePrefab(gameplayMenusPrefab, gameplayScene) as GameObject;

        if (instantiatedMenus == null)
            throw new InvalidOperationException("Unable to instantiate gameplay menus prefab into gameplay scene.");

        RectTransform instantiatedRect = instantiatedMenus.GetComponent<RectTransform>();
        instantiatedRect.SetParent(targetCanvas.transform, false);
        StretchToParent(instantiatedRect);
        instantiatedRect.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(gameplayScene);
        EditorSceneManager.SaveScene(gameplayScene);
    }

    /// <summary>
    /// Creates or refreshes the authored standalone main-menu scene.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void EnsureMainMenuScene()
    {
        EnsureFolder(Path.GetDirectoryName(MainMenuScenePath));
        Scene mainMenuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateMainMenuCamera(mainMenuScene);
        Canvas canvas = CreateCanvasRoot("Canvas_MainMenu");
        EventSystem eventSystem = CreateEventSystem("EventSystem");
        GameObject menuRoot = new GameObject("MainMenuRoot", typeof(RectTransform), typeof(MainMenuController));
        RectTransform menuRootRect = menuRoot.GetComponent<RectTransform>();
        menuRootRect.SetParent(canvas.transform, false);
        StretchToParent(menuRootRect);

        GameObject backgroundObject = CreateImageObject("Background", menuRoot.transform, BackgroundColor);
        StretchToParent(backgroundObject.GetComponent<RectTransform>());

        GameObject panel = CreatePanel("MainMenuPanel", menuRoot.transform, new Vector2(440f, 340f));
        TMP_Text titleText = CreateTitleText("Title", "NashCore", panel.transform);
        TMP_Text subtitleText = CreateBodyText("Subtitle", "Bullet Heaven / Roguelite", panel.transform, 22f);
        Button playButton = CreateMenuButton("PlayButton", "Play", panel.transform);
        Button quitButton = CreateMenuButton("QuitButton", "Quit", panel.transform);

        EnsureLayout(panel.transform);
        EnsureLayoutElement(titleText.rectTransform.gameObject, 68f);
        EnsureLayoutElement(subtitleText.rectTransform.gameObject, 42f);
        ConfigureVerticalNavigation(playButton, quitButton);

        MainMenuController mainMenuController = menuRoot.GetComponent<MainMenuController>();
        MenuSelectionController selectionController = GetOrAddComponent<MenuSelectionController>(menuRoot);
        SerializedObject serializedController = new SerializedObject(mainMenuController);
        serializedController.Update();
        serializedController.FindProperty("playButton").objectReferenceValue = playButton;
        serializedController.FindProperty("quitButton").objectReferenceValue = quitButton;
        serializedController.FindProperty("gameplaySceneName").stringValue = GameplaySceneName;
        serializedController.FindProperty("eventSystemOverride").objectReferenceValue = eventSystem;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedSelectionController = new SerializedObject(selectionController);
        serializedSelectionController.Update();
        serializedSelectionController.FindProperty("eventSystemOverride").objectReferenceValue = eventSystem;
        serializedSelectionController.FindProperty("defaultSelectable").objectReferenceValue = playButton;
        serializedSelectionController.ApplyModifiedPropertiesWithoutUndo();
        eventSystem.firstSelectedGameObject = playButton.gameObject;

        EditorSceneManager.MarkSceneDirty(mainMenuScene);
        EditorSceneManager.SaveScene(mainMenuScene, MainMenuScenePath);
    }

    /// <summary>
    /// Ensures build settings start from the main menu, keep gameplay available, and preserve any extra existing scenes afterward.
    /// /params None.
    /// /returns None.
    /// </summary>
    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> updatedScenes = new List<EditorBuildSettingsScene>();
        AddSceneIfMissing(updatedScenes, MainMenuScenePath);
        AddSceneIfMissing(updatedScenes, GameplayScenePath);
        EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;

        for (int sceneIndex = 0; sceneIndex < existingScenes.Length; sceneIndex++)
        {
            string scenePath = existingScenes[sceneIndex].path;

            if (string.Equals(scenePath, MainMenuScenePath, StringComparison.Ordinal))
                continue;

            if (string.Equals(scenePath, GameplayScenePath, StringComparison.Ordinal))
                continue;

            AddSceneIfMissing(updatedScenes, scenePath);
        }

        EditorBuildSettings.scenes = updatedScenes.ToArray();
    }
    #endregion

    #region UI Creation
    /// <summary>
    /// Creates one full-screen overlay root used by authored gameplay menus.
    /// /params objectName: Name assigned to the created overlay object.
    /// /params parent: Parent transform that receives the overlay root.
    /// /returns Created overlay GameObject.
    /// </summary>
    private static GameObject CreateOverlayRoot(string objectName, Transform parent)
    {
        GameObject overlayObject = CreateImageObject(objectName, parent, OverlayColor);
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        StretchToParent(overlayRect);
        return overlayObject;
    }

    /// <summary>
    /// Creates one centered panel object used by the main menu and gameplay overlays.
    /// /params objectName: Name assigned to the created panel object.
    /// /params parent: Parent transform that receives the panel.
    /// /params size: Fixed panel size in pixels.
    /// /returns Created panel GameObject.
    /// </summary>
    private static GameObject CreatePanel(string objectName, Transform parent, Vector2 size)
    {
        GameObject panelObject = CreateImageObject(objectName, parent, PanelColor);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = Vector2.zero;
        return panelObject;
    }

    /// <summary>
    /// Creates one title text element under the given parent using the shared menu style.
    /// /params objectName: Name assigned to the created text object.
    /// /params text: Displayed label.
    /// /params parent: Parent transform that receives the text.
    /// /returns Created TMP text component.
    /// </summary>
    private static TMP_Text CreateTitleText(string objectName, string text, Transform parent)
    {
        return CreateText(objectName, text, parent, 42f, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    /// <summary>
    /// Creates one body text element under the given parent using the shared menu style.
    /// /params objectName: Name assigned to the created text object.
    /// /params text: Displayed label.
    /// /params parent: Parent transform that receives the text.
    /// /params fontSize: Point size used for the created label.
    /// /returns Created TMP text component.
    /// </summary>
    private static TMP_Text CreateBodyText(string objectName, string text, Transform parent, float fontSize)
    {
        return CreateText(objectName, text, parent, fontSize, FontStyles.Normal, TextAlignmentOptions.Center);
    }

    /// <summary>
    /// Creates one TMP text element with the requested visual settings.
    /// /params objectName: Name assigned to the created text object.
    /// /params text: Displayed label.
    /// /params parent: Parent transform that receives the text.
    /// /params fontSize: Point size used for the created label.
    /// /params fontStyle: Font style used by the created label.
    /// /params alignment: Alignment used by the created label.
    /// /returns Created TMP text component.
    /// </summary>
    private static TMP_Text CreateText(string objectName,
                                       string text,
                                       Transform parent,
                                       float fontSize,
                                       FontStyles fontStyle,
                                       TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(320f, fontSize + 24f);

        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.font = ResolveFontAsset();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = alignment;
        textComponent.color = TextColor;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        return textComponent;
    }

    /// <summary>
    /// Creates one gameplay-menu button with a centered TMP label and shared navigation styling.
    /// /params objectName: Name assigned to the created button object.
    /// /params label: Displayed button label.
    /// /params parent: Parent transform that receives the button.
    /// /returns Created Button component.
    /// </summary>
    private static Button CreateMenuButton(string objectName, string label, Transform parent)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(MenuSelectableHoverRelay));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(260f, 54f);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 260f;
        layoutElement.preferredHeight = 54f;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = ButtonColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHighlightColor;
        colors.selectedColor = ButtonHighlightColor;
        colors.pressedColor = ButtonPressedColor;
        colors.disabledColor = new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.45f);
        button.colors = colors;

        TMP_Text labelText = CreateBodyText("Label", label, buttonObject.transform, 24f);
        RectTransform labelRect = labelText.rectTransform;
        StretchToParent(labelRect);
        labelText.margin = new Vector4(8f, 4f, 8f, 4f);
        return button;
    }

    /// <summary>
    /// Creates one image object with RectTransform and Image already configured.
    /// /params objectName: Name assigned to the created object.
    /// /params parent: Parent transform that receives the object.
    /// /params color: Image color assigned to the new object.
    /// /returns Created GameObject.
    /// </summary>
    private static GameObject CreateImageObject(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(parent, false);
        imageRect.localScale = Vector3.one;
        imageRect.localPosition = Vector3.zero;
        imageObject.GetComponent<Image>().color = color;
        return imageObject;
    }
    #endregion

    #region Scene Helpers
    /// <summary>
    /// Creates the main-menu camera used by the standalone front-end scene.
    /// /params scene: Scene that receives the created camera object.
    /// /returns None.
    /// </summary>
    private static void CreateMainMenuCamera(Scene scene)
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera cameraComponent = cameraObject.GetComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = BackgroundColor;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
    }

    /// <summary>
    /// Creates one screen-space overlay canvas with scaler and raycaster configured for authored UI.
    /// /params objectName: Name assigned to the canvas root.
    /// /returns Created Canvas component.
    /// </summary>
    private static Canvas CreateCanvasRoot(string objectName)
    {
        GameObject canvasObject = new GameObject(objectName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>
    /// Creates one EventSystem configured for Input System UI navigation.
    /// /params objectName: Name assigned to the EventSystem object.
    /// /returns Created EventSystem component.
    /// </summary>
    private static EventSystem CreateEventSystem(string objectName)
    {
        GameObject eventSystemObject = new GameObject(objectName, typeof(EventSystem), typeof(InputSystemUIInputModule));
        InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
        return eventSystemObject.GetComponent<EventSystem>();
    }

    /// <summary>
    /// Ensures the gameplay scene has one EventSystem with Input System UI module ready for authored menus.
    /// /params scene: Gameplay scene whose EventSystem should be validated.
    /// /returns None.
    /// </summary>
    private static void EnsureSceneEventSystem(Scene scene)
    {
        EventSystem eventSystem = FindComponentInScene<EventSystem>(scene);

        if (eventSystem == null)
            throw new InvalidOperationException("EventSystem not found in gameplay scene.");

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();

        if (inputModule == null)
            throw new InvalidOperationException("Gameplay EventSystem is missing InputSystemUIInputModule.");

        if (inputModule.actionsAsset != null)
            return;

        inputModule.AssignDefaultActions();
        EditorUtility.SetDirty(inputModule);
    }

    /// <summary>
    /// Resolves the most appropriate gameplay canvas when the HUD manager is not parented directly under it.
    /// /params scene: Gameplay scene searched for a valid screen-space UI canvas.
    /// /returns Resolved gameplay canvas or null when none is available.
    /// </summary>
    private static Canvas ResolveGameplayCanvas(Scene scene)
    {
        List<Canvas> canvases = FindComponentsInScene<Canvas>(scene);

        for (int canvasIndex = 0; canvasIndex < canvases.Count; canvasIndex++)
        {
            Canvas candidateCanvas = canvases[canvasIndex];

            if (candidateCanvas == null)
                continue;

            if (candidateCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            if (candidateCanvas.GetComponent<GraphicRaycaster>() == null)
                continue;

            return candidateCanvas;
        }

        return null;
    }

    /// <summary>
    /// Removes any pre-existing GameplayMenuController roots from the gameplay scene before the generated prefab is re-instantiated.
    /// /params scene: Gameplay scene being refreshed.
    /// /returns None.
    /// </summary>
    private static void RemoveExistingGameplayMenuControllers(Scene scene)
    {
        List<GameplayMenuController> controllers = FindComponentsInScene<GameplayMenuController>(scene);

        for (int controllerIndex = 0; controllerIndex < controllers.Count; controllerIndex++)
        {
            GameplayMenuController controller = controllers[controllerIndex];

            if (controller == null)
                continue;

            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }
    }

    /// <summary>
    /// Configures one vertical loop of explicit menu navigation for the provided buttons.
    /// /params buttons: Ordered button list that should navigate up and down consistently.
    /// /returns None.
    /// </summary>
    private static void ConfigureVerticalNavigation(params Button[] buttons)
    {
        for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
        {
            Button button = buttons[buttonIndex];

            if (button == null)
                continue;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = ResolvePreviousButton(buttons, buttonIndex);
            navigation.selectOnDown = ResolveNextButton(buttons, buttonIndex);
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            button.navigation = navigation;
        }
    }
    #endregion

    #region Generic Helpers
    /// <summary>
    /// Recursively creates a folder chain inside the Unity project when one or more path segments are missing.
    /// /params folderPath: Project-relative folder path that must exist.
    /// /returns None.
    /// </summary>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string normalizedFolderPath = folderPath.Replace("\\", "/");
        string[] segments = normalizedFolderPath.Split('/');
        string currentPath = segments[0];

        for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
        {
            string nextPath = currentPath + "/" + segments[segmentIndex];

            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, segments[segmentIndex]);

            currentPath = nextPath;
        }
    }

    /// <summary>
    /// Adds one enabled scene entry only when it is not already present in the target list.
    /// /params scenes: Mutable build-settings scene list.
    /// /params scenePath: Scene path that should be present.
    /// /returns None.
    /// </summary>
    private static void AddSceneIfMissing(List<EditorBuildSettingsScene> scenes, string scenePath)
    {
        for (int sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++)
        {
            if (string.Equals(scenes[sceneIndex].path, scenePath, StringComparison.Ordinal))
                return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
    }

    /// <summary>
    /// Returns the existing component on one GameObject or adds it when missing.
    /// /params targetObject: GameObject receiving the requested component.
    /// /returns Existing or newly added component instance.
    /// </summary>
    private static TComponent GetOrAddComponent<TComponent>(GameObject targetObject) where TComponent : Component
    {
        TComponent component = targetObject.GetComponent<TComponent>();

        if (component != null)
            return component;

        return targetObject.AddComponent<TComponent>();
    }

    /// <summary>
    /// Resolves the previous valid button in one cyclic navigation list.
    /// /params buttons: Ordered button list used for navigation.
    /// /params startIndex: Current button index.
    /// /returns Previous valid button or null when none is available.
    /// </summary>
    private static Selectable ResolvePreviousButton(Button[] buttons, int startIndex)
    {
        if (buttons == null || buttons.Length <= 1)
            return null;

        for (int offsetIndex = 1; offsetIndex < buttons.Length; offsetIndex++)
        {
            int candidateIndex = (startIndex - offsetIndex + buttons.Length) % buttons.Length;
            Button candidateButton = buttons[candidateIndex];

            if (candidateButton != null)
                return candidateButton;
        }

        return null;
    }

    /// <summary>
    /// Resolves the next valid button in one cyclic navigation list.
    /// /params buttons: Ordered button list used for navigation.
    /// /params startIndex: Current button index.
    /// /returns Next valid button or null when none is available.
    /// </summary>
    private static Selectable ResolveNextButton(Button[] buttons, int startIndex)
    {
        if (buttons == null || buttons.Length <= 1)
            return null;

        for (int offsetIndex = 1; offsetIndex < buttons.Length; offsetIndex++)
        {
            int candidateIndex = (startIndex + offsetIndex) % buttons.Length;
            Button candidateButton = buttons[candidateIndex];

            if (candidateButton != null)
                return candidateButton;
        }

        return null;
    }

    /// <summary>
    /// Finds the first component of the requested type inside one opened scene.
    /// /params scene: Scene searched for the requested component.
    /// /returns First matching component or null when not found.
    /// </summary>
    private static TComponent FindComponentInScene<TComponent>(Scene scene) where TComponent : Component
    {
        List<TComponent> components = FindComponentsInScene<TComponent>(scene);
        return components.Count > 0 ? components[0] : null;
    }

    /// <summary>
    /// Finds all components of the requested type inside one opened scene.
    /// /params scene: Scene searched for the requested component type.
    /// /returns List of matching components.
    /// </summary>
    private static List<TComponent> FindComponentsInScene<TComponent>(Scene scene) where TComponent : Component
    {
        List<TComponent> resolvedComponents = new List<TComponent>();
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            TComponent[] components = rootObjects[rootIndex].GetComponentsInChildren<TComponent>(true);

            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                resolvedComponents.Add(components[componentIndex]);
        }

        return resolvedComponents;
    }

    /// <summary>
    /// Resolves the TMP font asset used by generated menu text elements.
    /// /params None.
    /// /returns TMP font asset or null when no font asset exists in the project.
    /// </summary>
    private static TMP_FontAsset ResolveFontAsset()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");

        for (int guidIndex = 0; guidIndex < fontGuids.Length; guidIndex++)
        {
            string fontPath = AssetDatabase.GUIDToAssetPath(fontGuids[guidIndex]);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);

            if (fontAsset != null)
                return fontAsset;
        }

        return null;
    }

    /// <summary>
    /// Ensures one transform uses a centered vertical-layout stack for menu content.
    /// /params parent: Menu panel transform that should host vertically stacked children.
    /// /returns None.
    /// </summary>
    private static void EnsureLayout(Transform parent)
    {
        VerticalLayoutGroup layoutGroup = GetOrAddComponent<VerticalLayoutGroup>(parent.gameObject);
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = 14f;
        layoutGroup.padding = new RectOffset(28, 28, 30, 30);

        ContentSizeFitter contentSizeFitter = GetOrAddComponent<ContentSizeFitter>(parent.gameObject);
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    /// <summary>
    /// Ensures one UI object exposes a preferred-height layout element used by the menu vertical layout.
    /// /params targetObject: UI object that should receive the preferred height.
    /// /params preferredHeight: Preferred layout height for the object.
    /// /returns None.
    /// </summary>
    private static void EnsureLayoutElement(GameObject targetObject, float preferredHeight)
    {
        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(targetObject);
        layoutElement.preferredHeight = preferredHeight;
    }

    /// <summary>
    /// Ensures one GameObject has a RectTransform and returns it.
    /// /params targetObject: GameObject that should expose a RectTransform.
    /// /returns Existing or newly added RectTransform.
    /// </summary>
    private static RectTransform EnsureRectTransform(GameObject targetObject)
    {
        RectTransform rectTransform = targetObject.GetComponent<RectTransform>();

        if (rectTransform != null)
            return rectTransform;

        return targetObject.AddComponent<RectTransform>();
    }

    /// <summary>
    /// Stretches one RectTransform to the full extent of its parent.
    /// /params rectTransform: RectTransform that should occupy the full parent area.
    /// /returns None.
    /// </summary>
    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Removes all direct children under one transform.
    /// /params parent: Parent transform whose full child list should be cleared.
    /// /returns None.
    /// </summary>
    private static void DestroyAllChildren(Transform parent)
    {
        for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(childIndex).gameObject);
    }
    #endregion

    #endregion
}
