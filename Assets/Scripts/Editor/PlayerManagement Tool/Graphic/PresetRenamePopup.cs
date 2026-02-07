using System;
using UnityEditor;
using UnityEngine;

public sealed class PresetRenamePopup : PopupWindowContent
{
    #region Fields
    private readonly string title;
    private readonly string initialName;
    private readonly Action<string> onConfirm;
    private string newName;
    #endregion

    #region Constructors
    public PresetRenamePopup(string title, string initialName, Action<string> onConfirm)
    {
        this.title = string.IsNullOrWhiteSpace(title) ? "Rename Preset" : title;
        this.initialName = initialName ?? string.Empty;
        this.onConfirm = onConfirm;
        newName = this.initialName;
    }
    #endregion

    #region API
    public static void Show(Rect anchorRect, string title, string initialName, Action<string> onConfirm)
    {
        PresetRenamePopup popup = new PresetRenamePopup(title, initialName, onConfirm);
        PopupWindow.Show(anchorRect, popup);
    }
    #endregion

    #region PopupWindowContent
    public override Vector2 GetWindowSize()
    {
        return new Vector2(300f, 96f);
    }

    public override void OnOpen()
    {
        newName = initialName;
        EditorApplication.delayCall += () => EditorGUI.FocusTextInControl("PresetRenameField");
    }

    public override void OnGUI(Rect rect)
    {
        GUILayout.Label(title, EditorStyles.boldLabel);

        GUI.SetNextControlName("PresetRenameField");
        newName = EditorGUILayout.TextField("Name", newName);

        GUILayout.Space(6f);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
            editorWindow.Close();

        if (GUILayout.Button("Rename", GUILayout.Width(80f)))
        {
            if (string.IsNullOrWhiteSpace(newName) == false)
                onConfirm?.Invoke(newName.Trim());

            editorWindow.Close();
        }

        EditorGUILayout.EndHorizontal();
    }
    #endregion
}
