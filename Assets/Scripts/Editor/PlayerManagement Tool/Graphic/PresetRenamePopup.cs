using System;
using UnityEditor;
using UnityEngine;

public sealed class PresetRenamePopup : PopupWindowContent
{
    #region Fields
    private readonly string m_Title;
    private readonly string m_InitialName;
    private readonly Action<string> m_OnConfirm;
    private string m_NewName;
    #endregion

    #region Constructors
    public PresetRenamePopup(string title, string initialName, Action<string> onConfirm)
    {
        m_Title = string.IsNullOrWhiteSpace(title) ? "Rename Preset" : title;
        m_InitialName = initialName ?? string.Empty;
        m_OnConfirm = onConfirm;
        m_NewName = m_InitialName;
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
        m_NewName = m_InitialName;
        EditorApplication.delayCall += () => EditorGUI.FocusTextInControl("PresetRenameField");
    }

    public override void OnGUI(Rect rect)
    {
        GUILayout.Label(m_Title, EditorStyles.boldLabel);

        GUI.SetNextControlName("PresetRenameField");
        m_NewName = EditorGUILayout.TextField("Name", m_NewName);

        GUILayout.Space(6f);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
            editorWindow.Close();

        if (GUILayout.Button("Rename", GUILayout.Width(80f)))
        {
            if (string.IsNullOrWhiteSpace(m_NewName) == false)
                m_OnConfirm?.Invoke(m_NewName.Trim());

            editorWindow.Close();
        }

        EditorGUILayout.EndHorizontal();
    }
    #endregion
}
