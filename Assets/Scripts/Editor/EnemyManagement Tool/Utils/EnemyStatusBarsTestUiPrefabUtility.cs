using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates and deletes test world-space status bars directly on enemy prefab assets.
/// </summary>
public static class EnemyStatusBarsTestUiPrefabUtility
{
    #region Constants
    public const string TestUiRootObjectName = "__EnemyStatusBarsTestUI";

    private const string HealthBackgroundObjectName = "HealthBackground";
    private const string HealthFillObjectName = "HealthFill";
    private const string ShieldBackgroundObjectName = "ShieldBackground";
    private const string ShieldFillObjectName = "ShieldFill";
    #endregion

    #region Methods

    #region Public Methods
    public static bool HasGeneratedTestUi(GameObject enemyPrefab)
    {
        EnemyAuthoring authoring = ResolveEnemyAuthoring(enemyPrefab);

        if (authoring == null)
            return false;

        EnemyWorldSpaceStatusBarsView statusBarsView = authoring.WorldSpaceStatusBarsView;
        return IsGeneratedTestUiView(statusBarsView);
    }

    public static bool IsGeneratedTestUiView(EnemyWorldSpaceStatusBarsView statusBarsView)
    {
        if (statusBarsView == null)
            return false;

        GameObject viewObject = statusBarsView.gameObject;

        if (viewObject == null)
            return false;

        return string.Equals(viewObject.name, TestUiRootObjectName, StringComparison.Ordinal);
    }

    public static bool TryGenerateTestUi(GameObject enemyPrefab, EnemyTestUiSettings settings, out string message)
    {
        message = string.Empty;

        if (!TryResolvePrefabPath(enemyPrefab, out string prefabPath, out message))
            return false;

        EnemyTestUiSettings resolvedSettings = settings;

        if (resolvedSettings == null)
            resolvedSettings = new EnemyTestUiSettings();

        resolvedSettings.ValidateValues();

        Sprite defaultSprite = ResolveDefaultUiSprite();

        if (defaultSprite == null)
        {
            message = "Unable to resolve Unity built-in UI sprite for generated status bars.";
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
        {
            message = "Unable to load prefab contents for test UI generation.";
            return false;
        }

        try
        {
            EnemyAuthoring authoring = ResolveEnemyAuthoring(prefabRoot);

            if (authoring == null)
            {
                message = "EnemyAuthoring component not found on selected prefab.";
                return false;
            }

            RemoveGeneratedTestUiRoots(prefabRoot.transform);
            RemoveNonGeneratedStatusBarsViewComponents(prefabRoot.transform);

            GameObject testUiRoot = CreateStatusBarsRoot(prefabRoot.transform,
                                                         resolvedSettings,
                                                         defaultSprite,
                                                         out Image healthFillImage,
                                                         out Image shieldFillImage);

            EnemyWorldSpaceStatusBarsView statusBarsView = testUiRoot.GetComponent<EnemyWorldSpaceStatusBarsView>();

            if (statusBarsView == null)
            {
                message = "Generated test UI root is missing EnemyWorldSpaceStatusBarsView.";
                return false;
            }

            ApplyViewSettings(statusBarsView, healthFillImage, shieldFillImage, testUiRoot, resolvedSettings);

            SerializedObject serializedAuthoring = new SerializedObject(authoring);
            SerializedProperty worldSpaceStatusBarsViewProperty = serializedAuthoring.FindProperty("worldSpaceStatusBarsView");

            if (worldSpaceStatusBarsViewProperty == null)
            {
                message = "Property 'worldSpaceStatusBarsView' not found on EnemyAuthoring.";
                return false;
            }

            serializedAuthoring.Update();
            worldSpaceStatusBarsViewProperty.objectReferenceValue = statusBarsView;
            serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(authoring);
            EditorUtility.SetDirty(statusBarsView);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            message = "Test UI generated and assigned on selected enemy prefab.";
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    public static bool TryDeleteTestUi(GameObject enemyPrefab, out string message)
    {
        message = string.Empty;

        if (!TryResolvePrefabPath(enemyPrefab, out string prefabPath, out message))
            return false;

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
        {
            message = "Unable to load prefab contents for test UI deletion.";
            return false;
        }

        try
        {
            EnemyAuthoring authoring = ResolveEnemyAuthoring(prefabRoot);

            if (authoring == null)
            {
                message = "EnemyAuthoring component not found on selected prefab.";
                return false;
            }

            SerializedObject serializedAuthoring = new SerializedObject(authoring);
            SerializedProperty worldSpaceStatusBarsViewProperty = serializedAuthoring.FindProperty("worldSpaceStatusBarsView");
            bool clearAssignedView = false;

            if (worldSpaceStatusBarsViewProperty != null)
            {
                serializedAuthoring.Update();
                EnemyWorldSpaceStatusBarsView assignedView = worldSpaceStatusBarsViewProperty.objectReferenceValue as EnemyWorldSpaceStatusBarsView;
                clearAssignedView = IsGeneratedTestUiView(assignedView);
            }

            Transform generatedRoot = FindGeneratedTestUiTransform(prefabRoot.transform);
            bool hasGeneratedRoot = generatedRoot != null;

            if (hasGeneratedRoot)
                UnityEngine.Object.DestroyImmediate(generatedRoot.gameObject);

            bool clearedAssignedView = false;

            if (worldSpaceStatusBarsViewProperty != null)
            {
                if (clearAssignedView || hasGeneratedRoot)
                {
                    worldSpaceStatusBarsViewProperty.objectReferenceValue = null;
                    clearedAssignedView = true;
                }

                serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!hasGeneratedRoot && !clearedAssignedView)
            {
                message = "No generated test UI found on selected enemy prefab.";
                return false;
            }

            EditorUtility.SetDirty(authoring);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            message = "Generated test UI deleted from selected enemy prefab.";
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
    #endregion

    #region UI Building
    private static GameObject CreateStatusBarsRoot(Transform parent,
                                                   EnemyTestUiSettings settings,
                                                   Sprite defaultSprite,
                                                   out Image healthFillImage,
                                                   out Image shieldFillImage)
    {
        GameObject rootObject = new GameObject(TestUiRootObjectName,
                                               typeof(RectTransform),
                                               typeof(Canvas),
                                               typeof(CanvasScaler),
                                               typeof(EnemyWorldSpaceStatusBarsView));
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        Canvas rootCanvas = rootObject.GetComponent<Canvas>();
        CanvasScaler rootCanvasScaler = rootObject.GetComponent<CanvasScaler>();

        rootRect.SetParent(parent, false);
        rootRect.localPosition = settings.WorldOffset;
        rootRect.localRotation = Quaternion.identity;
        rootRect.localScale = new Vector3(settings.WorldScale, settings.WorldScale, settings.WorldScale);
        rootRect.sizeDelta = new Vector2(settings.RootWidthPixels, settings.RootHeightPixels);

        rootCanvas.renderMode = RenderMode.WorldSpace;
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = settings.CanvasSortingOrder;

        rootCanvasScaler.dynamicPixelsPerUnit = 10f;
        rootCanvasScaler.referencePixelsPerUnit = 100f;

        CreateBarImage(rootRect,
                       HealthBackgroundObjectName,
                       settings.HealthBarWidthPixels,
                       settings.HealthBarHeightPixels,
                       settings.HealthBarYOffsetPixels,
                       settings.HealthBackgroundColor,
                       false,
                       defaultSprite);
        CreateBarImage(rootRect,
                       ShieldBackgroundObjectName,
                       settings.ShieldBarWidthPixels,
                       settings.ShieldBarHeightPixels,
                       settings.ShieldBarYOffsetPixels,
                       settings.ShieldBackgroundColor,
                       false,
                       defaultSprite);
        healthFillImage = CreateBarImage(rootRect,
                                         HealthFillObjectName,
                                         settings.HealthBarWidthPixels,
                                         settings.HealthBarHeightPixels,
                                         settings.HealthBarYOffsetPixels,
                                         settings.HealthFillColor,
                                         true,
                                         defaultSprite);
        shieldFillImage = CreateBarImage(rootRect,
                                         ShieldFillObjectName,
                                         settings.ShieldBarWidthPixels,
                                         settings.ShieldBarHeightPixels,
                                         settings.ShieldBarYOffsetPixels,
                                         settings.ShieldFillColor,
                                         true,
                                         defaultSprite);
        return rootObject;
    }

    private static Image CreateBarImage(Transform parent,
                                        string objectName,
                                        float width,
                                        float height,
                                        float yOffset,
                                        Color color,
                                        bool isFill,
                                        Sprite sprite)
    {
        GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        Image barImage = barObject.GetComponent<Image>();

        barRect.SetParent(parent, false);
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = new Vector2(0f, yOffset);
        barRect.sizeDelta = new Vector2(width, height);

        barImage.sprite = sprite;
        barImage.raycastTarget = false;
        barImage.color = color;

        if (isFill)
        {
            barImage.type = Image.Type.Filled;
            barImage.fillMethod = Image.FillMethod.Horizontal;
            barImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            barImage.fillAmount = 1f;
            return barImage;
        }

        barImage.type = Image.Type.Simple;
        return barImage;
    }
    #endregion

    #region Helpers
    private static bool TryResolvePrefabPath(GameObject enemyPrefab, out string prefabPath, out string message)
    {
        prefabPath = string.Empty;
        message = string.Empty;

        if (enemyPrefab == null)
        {
            message = "Select an enemy prefab first.";
            return false;
        }

        prefabPath = AssetDatabase.GetAssetPath(enemyPrefab);

        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            message = "Selected object is not a valid prefab asset.";
            return false;
        }

        if (PrefabUtility.GetPrefabAssetType(enemyPrefab) == PrefabAssetType.NotAPrefab)
        {
            message = "Selected object is not a prefab asset.";
            return false;
        }

        return true;
    }

    private static EnemyAuthoring ResolveEnemyAuthoring(GameObject enemyPrefab)
    {
        if (enemyPrefab == null)
            return null;

        EnemyAuthoring directAuthoring = enemyPrefab.GetComponent<EnemyAuthoring>();

        if (directAuthoring != null)
            return directAuthoring;

        return enemyPrefab.GetComponentInChildren<EnemyAuthoring>(true);
    }

    private static Transform FindGeneratedTestUiTransform(Transform rootTransform)
    {
        if (rootTransform == null)
            return null;

        Transform[] childTransforms = rootTransform.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < childTransforms.Length; index++)
        {
            Transform childTransform = childTransforms[index];

            if (childTransform == null)
                continue;

            if (string.Equals(childTransform.name, TestUiRootObjectName, StringComparison.Ordinal))
                return childTransform;
        }

        return null;
    }

    private static void RemoveGeneratedTestUiRoots(Transform rootTransform)
    {
        if (rootTransform == null)
            return;

        Transform[] childTransforms = rootTransform.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < childTransforms.Length; index++)
        {
            Transform childTransform = childTransforms[index];

            if (childTransform == null)
                continue;

            if (!string.Equals(childTransform.name, TestUiRootObjectName, StringComparison.Ordinal))
                continue;

            UnityEngine.Object.DestroyImmediate(childTransform.gameObject);
        }
    }

    private static void RemoveNonGeneratedStatusBarsViewComponents(Transform rootTransform)
    {
        if (rootTransform == null)
            return;

        EnemyWorldSpaceStatusBarsView[] statusBarsViews = rootTransform.GetComponentsInChildren<EnemyWorldSpaceStatusBarsView>(true);

        for (int index = 0; index < statusBarsViews.Length; index++)
        {
            EnemyWorldSpaceStatusBarsView statusBarsView = statusBarsViews[index];

            if (statusBarsView == null)
                continue;

            GameObject ownerObject = statusBarsView.gameObject;

            if (ownerObject == null)
                continue;

            if (string.Equals(ownerObject.name, TestUiRootObjectName, StringComparison.Ordinal))
                continue;

            UnityEngine.Object.DestroyImmediate(statusBarsView);
        }
    }

    private static Sprite ResolveDefaultUiSprite()
    {
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        if (uiSprite != null)
            return uiSprite;

        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
    }

    private static void ApplyViewSettings(EnemyWorldSpaceStatusBarsView statusBarsView,
                                          Image healthFillImage,
                                          Image shieldFillImage,
                                          GameObject visibilityRoot,
                                          EnemyTestUiSettings settings)
    {
        if (statusBarsView == null)
            return;

        SerializedObject viewSerializedObject = new SerializedObject(statusBarsView);
        SerializedProperty healthFillImageProperty = viewSerializedObject.FindProperty("healthFillImage");
        SerializedProperty shieldFillImageProperty = viewSerializedObject.FindProperty("shieldFillImage");
        SerializedProperty visibilityRootProperty = viewSerializedObject.FindProperty("visibilityRoot");
        SerializedProperty hideShieldWhenEmptyProperty = viewSerializedObject.FindProperty("hideShieldWhenEmpty");
        SerializedProperty hideWhenEnemyInactiveProperty = viewSerializedObject.FindProperty("hideWhenEnemyInactive");
        SerializedProperty hideWhenEnemyCulledProperty = viewSerializedObject.FindProperty("hideWhenEnemyCulled");
        SerializedProperty smoothingSecondsProperty = viewSerializedObject.FindProperty("smoothingSeconds");
        SerializedProperty shieldSmoothingSecondsProperty = viewSerializedObject.FindProperty("shieldSmoothingSeconds");
        SerializedProperty worldOffsetProperty = viewSerializedObject.FindProperty("worldOffset");
        SerializedProperty billboardToCameraProperty = viewSerializedObject.FindProperty("billboardToCamera");
        SerializedProperty billboardYawOnlyProperty = viewSerializedObject.FindProperty("billboardYawOnly");

        viewSerializedObject.Update();

        if (healthFillImageProperty != null)
            healthFillImageProperty.objectReferenceValue = healthFillImage;

        if (shieldFillImageProperty != null)
            shieldFillImageProperty.objectReferenceValue = shieldFillImage;

        if (visibilityRootProperty != null)
            visibilityRootProperty.objectReferenceValue = visibilityRoot;

        if (hideShieldWhenEmptyProperty != null)
            hideShieldWhenEmptyProperty.boolValue = settings.HideShieldWhenEmpty;

        if (hideWhenEnemyInactiveProperty != null)
            hideWhenEnemyInactiveProperty.boolValue = settings.HideWhenEnemyInactive;

        if (hideWhenEnemyCulledProperty != null)
            hideWhenEnemyCulledProperty.boolValue = settings.HideWhenEnemyCulled;

        if (smoothingSecondsProperty != null)
            smoothingSecondsProperty.floatValue = settings.SmoothingSeconds;

        if (shieldSmoothingSecondsProperty != null)
            shieldSmoothingSecondsProperty.floatValue = settings.ShieldSmoothingSeconds;

        if (worldOffsetProperty != null)
            worldOffsetProperty.vector3Value = settings.WorldOffset;

        if (billboardToCameraProperty != null)
            billboardToCameraProperty.boolValue = settings.BillboardToCamera;

        if (billboardYawOnlyProperty != null)
            billboardYawOnlyProperty.boolValue = settings.BillboardYawOnly;

        viewSerializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion

    #endregion
}
