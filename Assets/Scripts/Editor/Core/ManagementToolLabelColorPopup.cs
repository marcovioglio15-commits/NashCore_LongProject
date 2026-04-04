using UnityEngine.UIElements;

/// <summary>
/// Opens the dedicated color inspector for one management-tool label.
/// /params None.
/// /returns None.
/// </summary>
public static class ManagementToolLabelColorPopup
{
    /// <summary>
    /// Opens the dedicated color inspector for the provided label.
    /// /params label Target label being edited.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /returns None.
    /// </summary>
    public static void Show(Label label, string stateKey)
    {
        ManagementToolColorInspectorWindow.OpenForLabel(label, stateKey);
    }
}
