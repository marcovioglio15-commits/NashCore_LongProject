using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class PlayerInputActionsAssetUtility
{
    #region Constants
    public const string DefaultInputAssetPath = "Assets/Input System/InputSystem_Actions.inputactions";
    public const string DefaultInputFolder = "Assets/Input System";
    #endregion

    #region Public Methods
    public static InputActionAsset LoadOrCreateAsset()
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultInputAssetPath);

        if (asset != null)
        {
            EnsureRequiredActions(asset);
            return asset;
        }

        InputActionAsset createdAsset = CreateDefaultAsset();
        EnsureFolder(DefaultInputFolder);
        AssetDatabase.CreateAsset(createdAsset, DefaultInputAssetPath);
        AssetDatabase.SaveAssets();

        return createdAsset;
    }
    #endregion

    #region Private Methods
    private static void EnsureRequiredActions(InputActionAsset asset)
    {
        if (asset == null)
            return;

        bool changed = false;
        InputActionMap map = asset.FindActionMap("Player", false);

        if (map == null)
        {
            map = new InputActionMap("Player");
            asset.AddActionMap(map);
            changed = true;
        }

        changed |= EnsureAction(map, "Move", AddDefaultMoveBindings);
        changed |= EnsureAction(map, "Look", AddDefaultLookBindings);

        if (changed)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }

    private static bool EnsureAction(InputActionMap map, string actionName, Action<InputAction> configureBindings)
    {
        if (map == null)
            return false;

        InputAction action = map.FindAction(actionName, false);

        if (action != null)
            return false;

        InputAction createdAction = map.AddAction(actionName, InputActionType.Value, null, null, null, "Vector2");
        configureBindings?.Invoke(createdAction);
        return true;
    }

    private static void AddDefaultMoveBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Gamepad>/leftStick");
        action.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        action.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
    }

    private static void AddDefaultLookBindings(InputAction action)
    {
        if (action == null)
            return;

        action.AddBinding("<Gamepad>/rightStick");
        action.AddBinding("<Mouse>/delta");
        action.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
    }

    private static InputActionAsset CreateDefaultAsset()
    {
        InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
        asset.name = "PlayerInputActions";

        InputActionMap map = new InputActionMap("Player");

        InputAction move = map.AddAction("Move", InputActionType.Value, null, null, null, "Vector2");
        AddDefaultMoveBindings(move);

        InputAction look = map.AddAction("Look", InputActionType.Value, null, null, null, "Vector2");
        AddDefaultLookBindings(look);

        asset.AddActionMap(map);

        return asset;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parentFolder = System.IO.Path.GetDirectoryName(folderPath);
        string folderName = System.IO.Path.GetFileName(folderPath);

        if (string.IsNullOrWhiteSpace(parentFolder) == false && AssetDatabase.IsValidFolder(parentFolder) == false)
            EnsureFolder(parentFolder);

        AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    #endregion
}
