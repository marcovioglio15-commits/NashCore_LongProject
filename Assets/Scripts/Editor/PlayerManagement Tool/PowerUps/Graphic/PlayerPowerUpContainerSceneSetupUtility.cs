using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates the dropped power-up container prefab, prompt animation assets, and testing-scene HUD wiring.
/// /params none.
/// /returns none.
/// </summary>
public static class PlayerPowerUpContainerSceneSetupUtility
{
    #region Constants
    private const string GeneratedRootFolder = "Assets/Generated/Player/PowerUpContainers";
    private const string AnimationFolder = GeneratedRootFolder + "/Animation";
    private const string MaterialAssetPath = GeneratedRootFolder + "/MAT_PlayerPowerUpContainer.mat";
    private const string HiddenClipAssetPath = AnimationFolder + "/AC_PlayerPowerUpContainerPrompt_Hidden.anim";
    private const string VisibleClipAssetPath = AnimationFolder + "/AC_PlayerPowerUpContainerPrompt_Visible.anim";
    private const string AnimatorControllerAssetPath = AnimationFolder + "/AC_PlayerPowerUpContainerPrompt.controller";
    private const string PrefabAssetPath = "Assets/Prefabs/Player/PF_PlayerPowerUpContainer.prefab";
    private const string PlayerPrefabAssetPath = "Assets/Prefabs/Player/PF_Player.prefab";
    private const string ScenePath = "Assets/Scenes/Testing/Main Scenes/SCN_PlayerControllerTesting/SCN_PlayerControllerTesting.unity";
    private const string OverlayCanvasName = "Canvas_PlayerPowerUpContainerOverlay";
    private const string OverlayRootName = "PowerUpContainerOverlayPanel";
    private const string OverlayCardName = "Card";
    private const string OverlayTitleName = "Title";
    private const string OverlayDescriptionName = "Description";
    private const string OverlayIconName = "Icon";
    private const string OverlayButtonsRowName = "ButtonsRow";
    private const string OverlayPrimaryButtonName = "ReplacePrimaryButton";
    private const string OverlaySecondaryButtonName = "ReplaceSecondaryButton";
    #endregion

    #region Menu
    /// <summary>
    /// Builds or refreshes all generated dropped-container assets and wires the testing scene from a manual editor command.
    /// /params none.
    /// /returns void.
    /// </summary>
    //[MenuItem("Tools/Player/Setup Dropped Power-Up Container")]
    public static void SetupFromMenu()
    {
        ExecuteSetup(logToConsole : true);
    }

    /// <summary>
    /// Builds or refreshes all generated dropped-container assets and wires the testing scene from Unity batch mode.
    /// /params none.
    /// /returns void.
    /// </summary>
    public static void ExecuteBatchSetup()
    {
        ExecuteSetup(logToConsole : true);
    }
    #endregion

    #region Methods
    /// <summary>
    /// Runs the full setup pipeline for generated dropped-container assets and scene wiring.
    /// /params logToConsole: True to write a final status message into the Console.
    /// /returns void.
    /// </summary>
    private static void ExecuteSetup(bool logToConsole)
    {
        PlayerInputActionsAssetUtility.LoadOrCreateAsset();
        EnsureFolder(GeneratedRootFolder);
        EnsureFolder(AnimationFolder);

        Material containerMaterial = CreateOrUpdateContainerMaterial();
        AnimatorController promptAnimatorController = CreateOrUpdatePromptAnimatorController();
        GameObject containerPrefab = CreateOrUpdateContainerPrefab(containerMaterial, promptAnimatorController);
        SceneSetupResult sceneSetupResult = SetupTestingScene(containerPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!logToConsole)
            return;

        Debug.Log(string.Format("[PlayerPowerUpContainerSceneSetupUtility] Setup completed. Prefab: {0} | Scene HUD Wired: {1} | Progression Preset Wired: {2}",
                                PrefabAssetPath,
                                sceneSetupResult.HudWired ? "Yes" : "No",
                                sceneSetupResult.ProgressionPresetWired ? "Yes" : "No"));
    }

    /// <summary>
    /// Creates or refreshes the semi-transparent sphere material used by the dropped container prefab.
    /// /params none.
    /// /returns Material asset assigned to the generated prefab.
    /// </summary>
    private static Material CreateOrUpdateContainerMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialAssetPath);
        }

        Color tintColor = new Color(0.21f, 0.86f, 0.97f, 0.28f);
        material.name = Path.GetFileNameWithoutExtension(MaterialAssetPath);

        if (material.shader != null && string.Equals(material.shader.name, "Universal Render Pipeline/Lit", System.StringComparison.Ordinal))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", 2f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", tintColor);
            material.SetColor("_EmissionColor", new Color(0.06f, 0.24f, 0.3f, 1f));
        }
        else
        {
            material.SetColor("_Color", tintColor);
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Creates or refreshes the shared pop-up animator controller used by dropped-container prompts.
    /// /params none.
    /// /returns Animator controller assigned to generated prompt roots.
    /// </summary>
    private static AnimatorController CreateOrUpdatePromptAnimatorController()
    {
        AnimationClip hiddenClip = CreateOrUpdateHiddenPromptClip();
        AnimationClip visibleClip = CreateOrUpdateVisiblePromptClip();
        AnimatorController animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerAssetPath);

        if (animatorController == null)
        {
            animatorController = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerAssetPath);
        }

        animatorController.parameters = new AnimatorControllerParameter[0];
        animatorController.AddParameter("Visible", AnimatorControllerParameterType.Bool);

        AnimatorControllerLayer[] layers = animatorController.layers;

        if (layers == null || layers.Length <= 0)
        {
            animatorController.AddLayer("Base Layer");
            layers = animatorController.layers;
        }

        AnimatorControllerLayer layer = layers[0];
        AnimatorStateMachine stateMachine = layer.stateMachine;
        ChildAnimatorState[] existingStates = stateMachine.states;

        for (int stateIndex = existingStates.Length - 1; stateIndex >= 0; stateIndex--)
            stateMachine.RemoveState(existingStates[stateIndex].state);

        AnimatorState hiddenState = stateMachine.AddState("Hidden");
        hiddenState.motion = hiddenClip;
        hiddenState.writeDefaultValues = true;

        AnimatorState visibleState = stateMachine.AddState("Visible");
        visibleState.motion = visibleClip;
        visibleState.writeDefaultValues = true;

        stateMachine.defaultState = hiddenState;
        ClearStateMachineTransitions(stateMachine);

        AnimatorStateTransition toVisibleTransition = hiddenState.AddTransition(visibleState);
        toVisibleTransition.hasExitTime = false;
        toVisibleTransition.duration = 0f;
        toVisibleTransition.AddCondition(AnimatorConditionMode.If, 0f, "Visible");

        AnimatorStateTransition toHiddenTransition = visibleState.AddTransition(hiddenState);
        toHiddenTransition.hasExitTime = false;
        toHiddenTransition.duration = 0.05f;
        toHiddenTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Visible");

        AssetDatabase.SaveAssets();
        return animatorController;
    }

    /// <summary>
    /// Creates or refreshes the constant hidden-state animation clip for prompt roots.
    /// /params none.
    /// /returns Hidden-state animation clip asset.
    /// </summary>
    private static AnimationClip CreateOrUpdateHiddenPromptClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HiddenClipAssetPath);

        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, HiddenClipAssetPath);
        }

        clip.name = Path.GetFileNameWithoutExtension(HiddenClipAssetPath);
        clip.ClearCurves();
        SetFloatCurve(clip, "", typeof(RectTransform), "m_LocalScale.x", CreateConstantCurve(0.84f));
        SetFloatCurve(clip, "", typeof(RectTransform), "m_LocalScale.y", CreateConstantCurve(0.84f));
        SetFloatCurve(clip, "", typeof(RectTransform), "m_LocalScale.z", CreateConstantCurve(0.84f));
        SetFloatCurve(clip, "", typeof(CanvasGroup), "m_Alpha", CreateConstantCurve(0f));
        EditorUtility.SetDirty(clip);
        return clip;
    }

    /// <summary>
    /// Creates or refreshes the pop-up animation clip used when prompts become visible.
    /// /params none.
    /// /returns Visible-state animation clip asset.
    /// </summary>
    private static AnimationClip CreateOrUpdateVisiblePromptClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(VisibleClipAssetPath);

        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, VisibleClipAssetPath);
        }

        clip.name = Path.GetFileNameWithoutExtension(VisibleClipAssetPath);
        clip.ClearCurves();

        Keyframe[] scaleKeys = new Keyframe[]
        {
            new Keyframe(0f, 0.84f),
            new Keyframe(0.11f, 1.05f),
            new Keyframe(0.18f, 1f)
        };
        AnimationCurve scaleCurve = new AnimationCurve(scaleKeys);
        AnimationCurve alphaCurve = new AnimationCurve(new Keyframe(0f, 0f),
                                                       new Keyframe(0.08f, 1f),
                                                       new Keyframe(0.18f, 1f));

        SetFloatCurve(clip, "", typeof(RectTransform), "m_LocalScale.x", scaleCurve);
        SetFloatCurve(clip, "", typeof(RectTransform), "m_LocalScale.y", scaleCurve);
        SetFloatCurve(clip, "", typeof(RectTransform), "m_LocalScale.z", scaleCurve);
        SetFloatCurve(clip, "", typeof(CanvasGroup), "m_Alpha", alphaCurve);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    /// <summary>
    /// Creates or refreshes the dropped power-up container prefab and returns the saved asset.
    /// /params containerMaterial: Generated semi-transparent sphere material.
    /// /params promptAnimatorController: Generated animator controller used by the prompt roots.
    /// /returns Prefab asset ready to be assigned to progression presets.
    /// </summary>
    private static GameObject CreateOrUpdateContainerPrefab(Material containerMaterial, RuntimeAnimatorController promptAnimatorController)
    {
        TMP_FontAsset fontAsset = ResolveFontAsset();
        Sprite uiSprite = ResolveDefaultUiSprite();
        GameObject root = new GameObject("PF_PlayerPowerUpContainer");
        PlayerDroppedPowerUpContainerView containerView = root.AddComponent<PlayerDroppedPowerUpContainerView>();

        GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualObject.name = "VisualSphere";
        visualObject.transform.SetParent(root.transform, false);
        visualObject.transform.localScale = Vector3.one * 0.25f;
        Object.DestroyImmediate(visualObject.GetComponent<Collider>());

        MeshRenderer sphereRenderer = visualObject.GetComponent<MeshRenderer>();

        if (sphereRenderer != null)
            sphereRenderer.sharedMaterial = containerMaterial;

        GameObject billboardRootObject = new GameObject("BillboardRoot", typeof(RectTransform));
        billboardRootObject.transform.SetParent(root.transform, false);
        billboardRootObject.transform.localPosition = new Vector3(0f, 0.04f, 0f);

        Canvas canvas = billboardRootObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 40;
        billboardRootObject.AddComponent<CanvasScaler>();
        billboardRootObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = billboardRootObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(360f, 300f);
        canvasRect.localScale = Vector3.one * 0.0054f;

        Image iconImage = CreateImage("IconImage",
                                      billboardRootObject.transform,
                                      uiSprite,
                                      new Color(1f, 1f, 1f, 1f),
                                      new Vector2(132f, 132f),
                                      new Vector2(0.5f, 0.58f));

        GameObject singlePromptRoot = CreatePromptRoot("SinglePromptRoot",
                                                       billboardRootObject.transform,
                                                       uiSprite,
                                                       promptAnimatorController,
                                                       new Vector2(248f, 60f),
                                                       new Vector2(0.5f, 0.18f));
        TMP_Text singlePromptText = CreatePromptLabel("SinglePromptText",
                                                      singlePromptRoot.transform,
                                                      fontAsset,
                                                      26f,
                                                      "Press [F] to swap",
                                                      TextAlignmentOptions.Center);

        GameObject swapPromptRoot = CreatePromptRoot("SwapPromptRoot",
                                                     billboardRootObject.transform,
                                                     uiSprite,
                                                     promptAnimatorController,
                                                     new Vector2(356f, 96f),
                                                     new Vector2(0.5f, 0.17f));
        VerticalLayoutGroup swapLayoutGroup = swapPromptRoot.AddComponent<VerticalLayoutGroup>();
        swapLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
        swapLayoutGroup.childControlHeight = true;
        swapLayoutGroup.childControlWidth = true;
        swapLayoutGroup.childForceExpandHeight = false;
        swapLayoutGroup.childForceExpandWidth = true;
        swapLayoutGroup.padding = new RectOffset(16, 16, 12, 12);
        swapLayoutGroup.spacing = 4f;
        swapPromptRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text swapPrimaryPromptText = CreatePromptLabel("SwapPrimaryPromptText",
                                                           swapPromptRoot.transform,
                                                           fontAsset,
                                                           28f,
                                                           "[1] Slot 1",
                                                           TextAlignmentOptions.Center);
        TMP_Text swapSecondaryPromptText = CreatePromptLabel("SwapSecondaryPromptText",
                                                             swapPromptRoot.transform,
                                                             fontAsset,
                                                             28f,
                                                             "[2] Slot 2",
                                                             TextAlignmentOptions.Center);

        ConfigurePromptLabelLayout(singlePromptText);
        ConfigurePromptLabelLayout(swapPrimaryPromptText);
        ConfigurePromptLabelLayout(swapSecondaryPromptText);
        AssignContainerViewReferences(containerView,
                                      billboardRootObject.transform,
                                      iconImage,
                                      singlePromptRoot,
                                      singlePromptText,
                                      singlePromptRoot.GetComponent<Animator>(),
                                      swapPromptRoot,
                                      swapPrimaryPromptText,
                                      swapSecondaryPromptText,
                                      swapPromptRoot.GetComponent<Animator>());

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabAssetPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefabAsset;
    }

    /// <summary>
    /// Opens the testing scene, builds the overlay panel, wires HUD references, and assigns the generated prefab to the active progression preset.
    /// /params containerPrefab: Generated dropped-container prefab asset.
    /// /returns Result flags describing which scene-side references were updated.
    /// </summary>
    private static SceneSetupResult SetupTestingScene(GameObject containerPrefab)
    {
        SceneSetupResult result = new SceneSetupResult();

        if (!File.Exists(ScenePath))
            return result;

        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

        if (sceneAsset == null)
            return result;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        HUDManager hudManager = Object.FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
        Canvas overlayCanvas = ResolveOverlayCanvas();

        if (hudManager != null && overlayCanvas != null)
        {
            OverlayPanelReferences overlayPanelReferences = CreateOrUpdateOverlayPanel(overlayCanvas.transform);
            WireHudSection(hudManager, overlayPanelReferences);
            result.HudWired = true;
        }

        PlayerProgressionPreset progressionPreset = ResolveTargetProgressionPreset();

        if (progressionPreset != null)
        {
            SerializedObject progressionSerializedObject = new SerializedObject(progressionPreset);
            SerializedProperty containerSettingsProperty = progressionSerializedObject.FindProperty("powerUpContainerSettings");

            if (containerSettingsProperty != null)
            {
                SerializedProperty containerPrefabProperty = containerSettingsProperty.FindPropertyRelative("containerPrefab");

                if (containerPrefabProperty != null)
                {
                    containerPrefabProperty.objectReferenceValue = containerPrefab;
                    progressionSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(progressionPreset);
                    result.ProgressionPresetWired = true;
                }
            }
        }

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        return result;
    }

    /// <summary>
    /// Resolves or creates the overlay canvas used to host the dropped-container full-screen panel.
    /// /params none.
    /// /returns Screen-space overlay canvas used by the generated panel.
    /// </summary>
    private static Canvas ResolveOverlayCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int canvasIndex = 0; canvasIndex < canvases.Length; canvasIndex++)
        {
            Canvas canvas = canvases[canvasIndex];

            if (canvas == null)
                continue;

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            return canvas;
        }

        GameObject canvasObject = new GameObject(OverlayCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.pixelPerfect = false;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;
        return createdCanvas;
    }

    /// <summary>
    /// Resolves the progression preset currently used by the testing workflow.
    /// It first checks the opened testing scene, then falls back to the PF_Player prefab authoring setup.
    /// /params none.
    /// /returns Progression preset currently used by the player workflow when it can be resolved.
    /// </summary>
    private static PlayerProgressionPreset ResolveTargetProgressionPreset()
    {
        PlayerAuthoring scenePlayerAuthoring = Object.FindFirstObjectByType<PlayerAuthoring>(FindObjectsInactive.Include);

        if (scenePlayerAuthoring != null)
        {
            PlayerProgressionPreset sceneProgressionPreset = scenePlayerAuthoring.GetProgressionPreset();

            if (sceneProgressionPreset != null)
                return sceneProgressionPreset;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabAssetPath);

        if (prefabRoot == null)
            return null;

        try
        {
            PlayerAuthoring prefabPlayerAuthoring = prefabRoot.GetComponent<PlayerAuthoring>();

            if (prefabPlayerAuthoring == null)
                return null;

            return prefabPlayerAuthoring.GetProgressionPreset();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>
    /// Creates or refreshes the overlay panel hierarchy under the provided overlay canvas.
    /// /params canvasTransform: Overlay canvas transform receiving the generated panel hierarchy.
    /// /returns References used to wire the HUD section serialized fields.
    /// </summary>
    private static OverlayPanelReferences CreateOrUpdateOverlayPanel(Transform canvasTransform)
    {
        TMP_FontAsset fontAsset = ResolveFontAsset();
        Sprite uiSprite = ResolveDefaultUiSprite();
        GameObject overlayRoot = GetOrCreateChild(canvasTransform, OverlayRootName, typeof(RectTransform));
        RectTransform overlayRootRect = overlayRoot.GetComponent<RectTransform>();
        ConfigureStretchRect(overlayRootRect);
        Image overlayRootImage = GetOrAddComponent<Image>(overlayRoot);
        overlayRootImage.sprite = uiSprite;
        overlayRootImage.type = Image.Type.Sliced;
        overlayRootImage.color = new Color(0.02f, 0.04f, 0.07f, 0.78f);

        GameObject cardObject = GetOrCreateChild(overlayRoot.transform, OverlayCardName, typeof(RectTransform));
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(640f, 360f);
        cardRect.anchoredPosition = Vector2.zero;
        Image cardImage = GetOrAddComponent<Image>(cardObject);
        cardImage.sprite = uiSprite;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = new Color(0.09f, 0.13f, 0.18f, 0.96f);

        TMP_Text titleText = CreateOrUpdateOverlayText(cardObject.transform,
                                                       OverlayTitleName,
                                                       fontAsset,
                                                       34f,
                                                       new Vector2(0.5f, 0.83f),
                                                       new Vector2(500f, 50f),
                                                       "Dropped Power-Up",
                                                       TextAlignmentOptions.Center);
        TMP_Text descriptionText = CreateOrUpdateOverlayText(cardObject.transform,
                                                             OverlayDescriptionName,
                                                             fontAsset,
                                                             22f,
                                                             new Vector2(0.5f, 0.62f),
                                                             new Vector2(520f, 80f),
                                                             "Choose which active slot to replace.",
                                                             TextAlignmentOptions.Center);
        Image iconImage = CreateOrUpdateOverlayImage(cardObject.transform,
                                                     OverlayIconName,
                                                     uiSprite,
                                                     new Vector2(0.5f, 0.41f),
                                                     new Vector2(110f, 110f));

        GameObject buttonsRowObject = GetOrCreateChild(cardObject.transform, OverlayButtonsRowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform buttonsRowRect = buttonsRowObject.GetComponent<RectTransform>();
        buttonsRowRect.anchorMin = new Vector2(0.5f, 0.15f);
        buttonsRowRect.anchorMax = new Vector2(0.5f, 0.15f);
        buttonsRowRect.pivot = new Vector2(0.5f, 0.5f);
        buttonsRowRect.sizeDelta = new Vector2(520f, 72f);
        buttonsRowRect.anchoredPosition = Vector2.zero;
        HorizontalLayoutGroup buttonsLayoutGroup = buttonsRowObject.GetComponent<HorizontalLayoutGroup>();
        buttonsLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
        buttonsLayoutGroup.childControlHeight = false;
        buttonsLayoutGroup.childControlWidth = false;
        buttonsLayoutGroup.childForceExpandHeight = false;
        buttonsLayoutGroup.childForceExpandWidth = false;
        buttonsLayoutGroup.spacing = 20f;

        Button primaryButton = CreateOrUpdateOverlayButton(buttonsRowObject.transform,
                                                           OverlayPrimaryButtonName,
                                                           fontAsset,
                                                           uiSprite,
                                                           "Replace Slot 1");
        Button secondaryButton = CreateOrUpdateOverlayButton(buttonsRowObject.transform,
                                                             OverlaySecondaryButtonName,
                                                             fontAsset,
                                                             uiSprite,
                                                             "Replace Slot 2");

        overlayRoot.SetActive(false);
        return new OverlayPanelReferences
        {
            OverlayRoot = overlayRoot,
            TitleText = titleText,
            DescriptionText = descriptionText,
            IconImage = iconImage,
            PrimaryButton = primaryButton,
            PrimaryButtonText = primaryButton.GetComponentInChildren<TMP_Text>(true),
            SecondaryButton = secondaryButton,
            SecondaryButtonText = secondaryButton.GetComponentInChildren<TMP_Text>(true)
        };
    }

    /// <summary>
    /// Writes generated overlay references into the HUDManager serialized dropped-container section.
    /// /params hudManager: HUD manager updated in the testing scene.
    /// /params overlayPanelReferences: Generated overlay hierarchy references assigned into the serialized section.
    /// /returns void.
    /// </summary>
    private static void WireHudSection(HUDManager hudManager, OverlayPanelReferences overlayPanelReferences)
    {
        if (hudManager == null)
            return;

        SerializedObject hudSerializedObject = new SerializedObject(hudManager);
        SerializedProperty sectionProperty = hudSerializedObject.FindProperty("powerUpContainerInteractionSection");

        if (sectionProperty == null)
            return;

        sectionProperty.FindPropertyRelative("overlayPanelRoot").objectReferenceValue = overlayPanelReferences.OverlayRoot;
        sectionProperty.FindPropertyRelative("overlayTitleText").objectReferenceValue = overlayPanelReferences.TitleText;
        sectionProperty.FindPropertyRelative("overlayDescriptionText").objectReferenceValue = overlayPanelReferences.DescriptionText;
        sectionProperty.FindPropertyRelative("overlayIconImage").objectReferenceValue = overlayPanelReferences.IconImage;
        sectionProperty.FindPropertyRelative("replacePrimaryButton").objectReferenceValue = overlayPanelReferences.PrimaryButton;
        sectionProperty.FindPropertyRelative("replacePrimaryButtonText").objectReferenceValue = overlayPanelReferences.PrimaryButtonText;
        sectionProperty.FindPropertyRelative("replaceSecondaryButton").objectReferenceValue = overlayPanelReferences.SecondaryButton;
        sectionProperty.FindPropertyRelative("replaceSecondaryButtonText").objectReferenceValue = overlayPanelReferences.SecondaryButtonText;
        hudSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudManager);
    }

    /// <summary>
    /// Assigns generated child references to the dropped-container view component through serialization.
    /// /params containerView: Generated view component written with its child references.
    /// /params billboardRoot: Billboard root transform.
    /// /params iconImage: Center icon image.
    /// /params singlePromptRoot: Overlay prompt root.
    /// /params singlePromptText: Overlay prompt text.
    /// /params singlePromptAnimator: Overlay prompt animator.
    /// /params swapPromptRoot: Direct-swap prompt root.
    /// /params swapPrimaryPromptText: Direct-swap primary binding label.
    /// /params swapSecondaryPromptText: Direct-swap secondary binding label.
    /// /params swapPromptAnimator: Direct-swap prompt animator.
    /// /returns void.
    /// </summary>
    private static void AssignContainerViewReferences(PlayerDroppedPowerUpContainerView containerView,
                                                      Transform billboardRoot,
                                                      Image iconImage,
                                                      GameObject singlePromptRoot,
                                                      TMP_Text singlePromptText,
                                                      Animator singlePromptAnimator,
                                                      GameObject swapPromptRoot,
                                                      TMP_Text swapPrimaryPromptText,
                                                      TMP_Text swapSecondaryPromptText,
                                                      Animator swapPromptAnimator)
    {
        SerializedObject serializedObject = new SerializedObject(containerView);
        serializedObject.FindProperty("billboardRoot").objectReferenceValue = billboardRoot;
        serializedObject.FindProperty("iconImage").objectReferenceValue = iconImage;
        serializedObject.FindProperty("singlePromptRoot").objectReferenceValue = singlePromptRoot;
        serializedObject.FindProperty("singlePromptText").objectReferenceValue = singlePromptText;
        serializedObject.FindProperty("singlePromptAnimator").objectReferenceValue = singlePromptAnimator;
        serializedObject.FindProperty("swapPromptRoot").objectReferenceValue = swapPromptRoot;
        serializedObject.FindProperty("swapPrimaryPromptText").objectReferenceValue = swapPrimaryPromptText;
        serializedObject.FindProperty("swapSecondaryPromptText").objectReferenceValue = swapSecondaryPromptText;
        serializedObject.FindProperty("swapPromptAnimator").objectReferenceValue = swapPromptAnimator;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(containerView);
    }

    /// <summary>
    /// Creates one reusable world-space prompt root with image background, canvas group, and animator.
    /// /params objectName: Name assigned to the generated prompt root.
    /// /params parent: Parent transform receiving the prompt root.
    /// /params uiSprite: Default sliced UI sprite used by the prompt background.
    /// /params animatorController: Shared prompt animator controller.
    /// /params sizeDelta: Prompt size in canvas units.
    /// /params anchorPosition: Normalized anchor position inside the world-space canvas.
    /// /returns Generated prompt root object.
    /// </summary>
    private static GameObject CreatePromptRoot(string objectName,
                                               Transform parent,
                                               Sprite uiSprite,
                                               RuntimeAnimatorController animatorController,
                                               Vector2 sizeDelta,
                                               Vector2 anchorPosition)
    {
        GameObject promptRoot = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Animator));
        promptRoot.transform.SetParent(parent, false);
        RectTransform rectTransform = promptRoot.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorPosition;
        rectTransform.anchorMax = anchorPosition;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchoredPosition = Vector2.zero;
        Image image = promptRoot.GetComponent<Image>();
        image.sprite = uiSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.07f, 0.12f, 0.18f, 0.92f);
        CanvasGroup canvasGroup = promptRoot.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        Animator animator = promptRoot.GetComponent<Animator>();
        animator.runtimeAnimatorController = animatorController;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        promptRoot.transform.localScale = Vector3.one * 0.84f;
        return promptRoot;
    }

    /// <summary>
    /// Creates one prompt label under the provided world-space prompt root.
    /// /params objectName: Name assigned to the generated label.
    /// /params parent: Parent transform receiving the label.
    /// /params fontAsset: TMP font asset used by the label.
    /// /params fontSize: Font size assigned to the label.
    /// /params textValue: Initial preview text.
    /// /params alignment: TMP alignment used by the label.
    /// /returns Generated TMP label.
    /// </summary>
    private static TMP_Text CreatePromptLabel(string objectName,
                                              Transform parent,
                                              TMP_FontAsset fontAsset,
                                              float fontSize,
                                              string textValue,
                                              TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.fontSize = fontSize;
        text.text = textValue;
        text.alignment = alignment;
        text.color = new Color(0.96f, 0.98f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// Creates one centered image under the provided parent transform.
    /// /params objectName: Name assigned to the generated image.
    /// /params parent: Parent transform receiving the image.
    /// /params sprite: Sprite assigned to the image.
    /// /params color: Tint color assigned to the image.
    /// /params sizeDelta: Image size in UI units.
    /// /params anchorPosition: Normalized anchor position inside the parent rect.
    /// /returns Generated image component.
    /// </summary>
    private static Image CreateImage(string objectName,
                                     Transform parent,
                                     Sprite sprite,
                                     Color color,
                                     Vector2 sizeDelta,
                                     Vector2 anchorPosition)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorPosition;
        rectTransform.anchorMax = anchorPosition;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchoredPosition = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Creates or refreshes one centered overlay image inside the overlay card.
    /// /params parent: Parent transform receiving the image.
    /// /params objectName: Name assigned to the image object.
    /// /params sprite: Default UI sprite assigned to the image.
    /// /params anchorPosition: Normalized anchor position inside the overlay card.
    /// /params sizeDelta: Image size in UI units.
    /// /returns Generated or updated image component.
    /// </summary>
    private static Image CreateOrUpdateOverlayImage(Transform parent,
                                                    string objectName,
                                                    Sprite sprite,
                                                    Vector2 anchorPosition,
                                                    Vector2 sizeDelta)
    {
        GameObject imageObject = GetOrCreateChild(parent, objectName, typeof(RectTransform), typeof(Image));
        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorPosition;
        rectTransform.anchorMax = anchorPosition;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchoredPosition = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Creates or refreshes one overlay text label inside the overlay card.
    /// /params parent: Parent transform receiving the text object.
    /// /params objectName: Name assigned to the text object.
    /// /params fontAsset: TMP font asset used by the label.
    /// /params fontSize: Font size assigned to the label.
    /// /params anchorPosition: Normalized anchor position inside the overlay card.
    /// /params sizeDelta: Label size in UI units.
    /// /params textValue: Preview text written into the label.
    /// /params alignment: TMP alignment used by the label.
    /// /returns Generated or updated TMP label.
    /// </summary>
    private static TMP_Text CreateOrUpdateOverlayText(Transform parent,
                                                      string objectName,
                                                      TMP_FontAsset fontAsset,
                                                      float fontSize,
                                                      Vector2 anchorPosition,
                                                      Vector2 sizeDelta,
                                                      string textValue,
                                                      TextAlignmentOptions alignment)
    {
        GameObject textObject = GetOrCreateChild(parent, objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorPosition;
        rectTransform.anchorMax = anchorPosition;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchoredPosition = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.fontSize = fontSize;
        text.text = textValue;
        text.alignment = alignment;
        text.color = new Color(0.96f, 0.98f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// Creates or refreshes one overlay action button with a nested TMP label.
    /// /params parent: Parent transform receiving the button.
    /// /params objectName: Name assigned to the button object.
    /// /params fontAsset: TMP font asset used by the nested label.
    /// /params sprite: Default UI sprite assigned to the button background.
    /// /params textValue: Initial label text.
    /// /returns Generated or updated button component.
    /// </summary>
    private static Button CreateOrUpdateOverlayButton(Transform parent,
                                                      string objectName,
                                                      TMP_FontAsset fontAsset,
                                                      Sprite sprite,
                                                      string textValue)
    {
        GameObject buttonObject = GetOrCreateChild(parent, objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(240f, 72f);
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.15f, 0.39f, 0.56f, 0.98f);
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.2f, 0.48f, 0.67f, 1f);
        colors.pressedColor = new Color(0.11f, 0.29f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.65f);
        button.colors = colors;

        GameObject textObject = GetOrCreateChild(buttonObject.transform, "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        ConfigureStretchRect(textRect);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.fontSize = 24f;
        text.text = textValue;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }

    /// <summary>
    /// Resolves one TMP font asset suitable for generated world-space and overlay UI.
    /// /params none.
    /// /returns TMP font asset used by generated labels.
    /// </summary>
    private static TMP_FontAsset ResolveFontAsset()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");

        for (int fontIndex = 0; fontIndex < fontGuids.Length; fontIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(fontGuids[fontIndex]);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

            if (fontAsset != null)
                return fontAsset;
        }

        return null;
    }

    /// <summary>
    /// Resolves the default built-in sliced sprite used by generated UI images and buttons.
    /// /params none.
    /// /returns Built-in UI sprite when available.
    /// </summary>
    private static Sprite ResolveDefaultUiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    /// <summary>
    /// Ensures one project folder exists, creating its missing parents recursively when needed.
    /// /params folderPath: Unity project-relative folder path.
    /// /returns void.
    /// </summary>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }

    /// <summary>
    /// Configures one rect transform to fully stretch across its parent.
    /// /params rectTransform: Rect transform updated in place.
    /// /returns void.
    /// </summary>
    private static void ConfigureStretchRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// Configures prompt labels to cooperate with layout groups used by the generated swap prompt.
    /// /params text: Prompt label updated in place.
    /// /returns void.
    /// </summary>
    private static void ConfigurePromptLabelLayout(TMP_Text text)
    {
        if (text == null)
            return;

        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(text.gameObject);
        layoutElement.minHeight = 30f;
        layoutElement.preferredHeight = 34f;
    }

    /// <summary>
    /// Creates one constant animation curve.
    /// /params value: Constant curve value.
    /// /returns Animation curve that keeps the same value for the whole clip duration.
    /// </summary>
    private static AnimationCurve CreateConstantCurve(float value)
    {
        return new AnimationCurve(new Keyframe(0f, value), new Keyframe(0.2f, value));
    }

    /// <summary>
    /// Writes one float curve into the provided animation clip.
    /// /params clip: Animation clip receiving the curve.
    /// /params relativePath: Relative hierarchy path targeted by the curve.
    /// /params componentType: Component type containing the animated property.
    /// /params propertyName: Serialized property path animated by the curve.
    /// /params curve: Animation curve assigned to the clip.
    /// /returns void.
    /// </summary>
    private static void SetFloatCurve(AnimationClip clip,
                                      string relativePath,
                                      System.Type componentType,
                                      string propertyName,
                                      AnimationCurve curve)
    {
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(relativePath, componentType, propertyName);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    /// <summary>
    /// Removes all state-machine transitions before the generated prompt states are rebuilt.
    /// /params stateMachine: Animator state machine cleared in place.
    /// /returns void.
    /// </summary>
    private static void ClearStateMachineTransitions(AnimatorStateMachine stateMachine)
    {
        ChildAnimatorState[] states = stateMachine.states;

        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            AnimatorState state = states[stateIndex].state;
            AnimatorStateTransition[] transitions = state.transitions;

            for (int transitionIndex = transitions.Length - 1; transitionIndex >= 0; transitionIndex--)
                state.RemoveTransition(transitions[transitionIndex]);
        }
    }

    /// <summary>
    /// Resolves one child by exact name or creates it with the requested components.
    /// /params parent: Parent transform receiving the child object.
    /// /params objectName: Exact child name.
    /// /params componentTypes: Optional component types ensured on the child object.
    /// /returns Existing or newly created child object.
    /// </summary>
    private static GameObject GetOrCreateChild(Transform parent, string objectName, params System.Type[] componentTypes)
    {
        Transform childTransform = parent.Find(objectName);
        GameObject childObject = childTransform != null ? childTransform.gameObject : null;

        if (childObject == null)
        {
            childObject = new GameObject(objectName, componentTypes);
            childObject.transform.SetParent(parent, false);
        }
        else
        {
            for (int componentIndex = 0; componentIndex < componentTypes.Length; componentIndex++)
            {
                System.Type componentType = componentTypes[componentIndex];

                if (childObject.GetComponent(componentType) != null)
                    continue;

                childObject.AddComponent(componentType);
            }
        }

        return childObject;
    }

    /// <summary>
    /// Returns one existing component from the target object or adds it when missing.
    /// /params targetObject: GameObject receiving the requested component.
    /// /params none.
    /// /returns Existing or newly added component.
    /// </summary>
    private static T GetOrAddComponent<T>(GameObject targetObject) where T : Component
    {
        T component = targetObject.GetComponent<T>();

        if (component != null)
            return component;

        return targetObject.AddComponent<T>();
    }
    #endregion

    #region Nested Types
    private struct SceneSetupResult
    {
        public bool HudWired;
        public bool ProgressionPresetWired;
    }

    private struct OverlayPanelReferences
    {
        public GameObject OverlayRoot;
        public TMP_Text TitleText;
        public TMP_Text DescriptionText;
        public Image IconImage;
        public Button PrimaryButton;
        public TMP_Text PrimaryButtonText;
        public Button SecondaryButton;
        public TMP_Text SecondaryButtonText;
    }
    #endregion
}
