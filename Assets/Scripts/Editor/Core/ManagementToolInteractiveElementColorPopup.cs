using UnityEngine.UIElements;

/// <summary>
/// Opens the dedicated color inspector for one interactive management-tool control.
/// /params None.
/// /returns None.
/// </summary>
public static class ManagementToolInteractiveElementColorPopup
{
    /// <summary>
    /// Opens the dedicated color inspector for the provided interactive control.
    /// /params targetElement Target control being edited.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /params elementKind Interactive control kind used to apply colors correctly.
    /// /returns None.
    /// </summary>
    public static void Show(VisualElement targetElement,
                            string stateKey,
                            ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        ManagementToolColorInspectorWindow.OpenForInteractive(targetElement, stateKey, elementKind);
    }
}
