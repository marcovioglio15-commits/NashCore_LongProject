using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Resolves supported interactive controls, hierarchy paths and persistence keys for management-tool color customization.
/// /params None.
/// /returns None.
/// </summary>
internal static class ManagementToolInteractiveElementColorHierarchyUtility
{
    #region Constants
    private const string ElementRegisteredClassName = "management-tool-interactive-colors-element";
    private const string BaseFieldLabelClassName = "unity-base-field__label";
    private const string BasePopupFieldClassName = "unity-base-popup-field";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Traverses the supplied hierarchy and registers every supported interactive control exactly once.
    /// /params hierarchyRoot Root used to build stable hierarchical state keys.
    /// /params currentElement Current node being inspected.
    /// /params stateKeyPrefix Stable state-key prefix used for all controls under the root.
    /// /returns None.
    /// </summary>
    public static void RegisterHierarchyElements(VisualElement hierarchyRoot,
                                                 VisualElement currentElement,
                                                 string stateKeyPrefix)
    {
        if (hierarchyRoot == null || currentElement == null)
            return;

        TryRegisterInteractiveElement(hierarchyRoot, currentElement, stateKeyPrefix);

        foreach (VisualElement child in currentElement.Children())
            RegisterHierarchyElements(hierarchyRoot, child, stateKeyPrefix);
    }

    /// <summary>
    /// Traverses the currently visible hierarchy and appends one browser entry for each supported interactive control.
    /// /params hierarchyRoot Root used to build stable hierarchical state keys.
    /// /params currentElement Current node being inspected.
    /// /params stateKeyPrefix Stable state-key prefix used for all controls under the root.
    /// /params results Target list that receives the collected entries.
    /// /params registeredStateKeys Deduplication set for already collected state keys.
    /// /returns None.
    /// </summary>
    public static void AppendBrowserEntries(VisualElement hierarchyRoot,
                                            VisualElement currentElement,
                                            string stateKeyPrefix,
                                            IList<ManagementToolColorBrowserEntry> results,
                                            ISet<string> registeredStateKeys)
    {
        if (hierarchyRoot == null || currentElement == null)
            return;

        if (results == null || registeredStateKeys == null)
            return;

        if (!ManagementToolVisualElementVisibilityUtility.IsBrowsable(currentElement))
            return;

        TryAppendBrowserEntry(hierarchyRoot, currentElement, stateKeyPrefix, results, registeredStateKeys);

        foreach (VisualElement child in currentElement.Children())
            AppendBrowserEntries(hierarchyRoot, child, stateKeyPrefix, results, registeredStateKeys);
    }

    /// <summary>
    /// Resolves the nearest supported interactive ancestor of the clicked element, ensures it is registered and opens the color inspector.
    /// /params hierarchyRoot Root used to build stable hierarchical state keys.
    /// /params startElement Clicked visual element where the ancestor walk begins.
    /// /params stateKeyPrefix Stable state-key prefix used for all controls under the root.
    /// /params evt Mouse event emitted by UI Toolkit.
    /// /returns True when one supported interactive control handled the right click.
    /// </summary>
    public static bool TryOpenRightClickFallback(VisualElement hierarchyRoot,
                                                 VisualElement startElement,
                                                 string stateKeyPrefix,
                                                 MouseDownEvent evt)
    {
        if (hierarchyRoot == null || startElement == null || evt == null)
            return false;

        if (string.IsNullOrWhiteSpace(stateKeyPrefix))
            return false;

        VisualElement currentElement = startElement;

        while (currentElement != null)
        {
            if (TryResolveInteractiveKind(currentElement, out ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind))
            {
                string stateKey = BuildElementStateKey(hierarchyRoot, currentElement, stateKeyPrefix, elementKind);

                if (!string.IsNullOrWhiteSpace(stateKey))
                {
                    EnsureInteractiveElementRegistered(currentElement, stateKey, elementKind);
                    ManagementToolColorTriggerUtility.HandleRightMouseDown(evt, () =>
                    {
                        ManagementToolInteractiveElementColorPopup.Show(currentElement, stateKey, elementKind);
                    });
                    return true;
                }
            }

            if (currentElement == hierarchyRoot)
                break;

            currentElement = currentElement.parent;
        }

        return false;
    }

    /// <summary>
    /// Resolves one readable display name for the supplied interactive control.
    /// /params targetElement Element whose user-facing title must be resolved.
    /// /returns One readable display name for inspector and debugging UI.
    /// </summary>
    public static string ResolveDisplayName(VisualElement targetElement)
    {
        if (targetElement == null)
            return "Unnamed Control";

        if (targetElement is Button button && !string.IsNullOrWhiteSpace(button.text))
            return button.text;

        if (targetElement is ToolbarToggle toolbarToggle && !string.IsNullOrWhiteSpace(toolbarToggle.text))
            return toolbarToggle.text;

        if (targetElement is Foldout foldout && !string.IsNullOrWhiteSpace(foldout.text))
            return foldout.text;

        if (IsPopupLikeElement(targetElement))
        {
            Label popupLabel = targetElement.Q<Label>(className: BaseFieldLabelClassName);

            if (popupLabel != null && !string.IsNullOrWhiteSpace(popupLabel.text))
                return popupLabel.text;
        }

        if (!string.IsNullOrWhiteSpace(targetElement.name))
            return targetElement.name;

        return targetElement.GetType().Name;
    }

    #endregion

    #region Private Methods
    /// <summary>
    /// Registers one interactive control when it is supported and was not registered before.
    /// /params hierarchyRoot Root used to build stable hierarchical state keys.
    /// /params targetElement Candidate control to register.
    /// /params stateKeyPrefix Stable state-key prefix used for all controls under the root.
    /// /returns None.
    /// </summary>
    private static void TryRegisterInteractiveElement(VisualElement hierarchyRoot,
                                                      VisualElement targetElement,
                                                      string stateKeyPrefix)
    {
        if (targetElement == null)
            return;

        if (!TryResolveInteractiveKind(targetElement, out ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind))
            return;

        string stateKey = BuildElementStateKey(hierarchyRoot, targetElement, stateKeyPrefix, elementKind);

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        EnsureInteractiveElementRegistered(targetElement, stateKey, elementKind);
    }

    /// <summary>
    /// Appends one browser entry for the provided visible control when it is supported and not yet collected.
    /// /params hierarchyRoot Root used to build stable hierarchical state keys.
    /// /params targetElement Candidate control being inspected.
    /// /params stateKeyPrefix Stable state-key prefix used for all controls under the root.
    /// /params results Target list that receives the collected entries.
    /// /params registeredStateKeys Deduplication set for already collected state keys.
    /// /returns None.
    /// </summary>
    private static void TryAppendBrowserEntry(VisualElement hierarchyRoot,
                                              VisualElement targetElement,
                                              string stateKeyPrefix,
                                              IList<ManagementToolColorBrowserEntry> results,
                                              ISet<string> registeredStateKeys)
    {
        if (!TryResolveInteractiveKind(targetElement, out ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind))
            return;

        string stateKey = BuildElementStateKey(hierarchyRoot, targetElement, stateKeyPrefix, elementKind);

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        EnsureInteractiveElementRegistered(targetElement, stateKey, elementKind);

        if (!registeredStateKeys.Add(stateKey))
            return;

        ManagementToolColorBrowserEntry browserEntry = new ManagementToolColorBrowserEntry();
        browserEntry.DisplayName = ResolveDisplayName(targetElement);
        browserEntry.StateKey = stateKey;
        browserEntry.IsLabel = false;
        browserEntry.SupportsBackground = ManagementToolInteractiveElementColorUtility.CanCustomizeBackground(elementKind);
        browserEntry.InteractiveTarget = targetElement;
        browserEntry.InteractiveElementKind = elementKind;
        results.Add(browserEntry);
    }

    /// <summary>
    /// Registers one supported control when needed and reapplies any saved colors to the live instance.
    /// /params targetElement Supported interactive control being synchronized.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /params elementKind Interactive control kind used to apply colors correctly.
    /// /returns None.
    /// </summary>
    private static void EnsureInteractiveElementRegistered(VisualElement targetElement,
                                                           string stateKey,
                                                           ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return;

        if (!targetElement.ClassListContains(ElementRegisteredClassName))
            targetElement.AddToClassList(ElementRegisteredClassName);

        ManagementToolInteractiveElementColorUtility.RegisterInteractiveElement(targetElement, stateKey, elementKind);
    }

    /// <summary>
    /// Resolves the interactive-control kind for one candidate element.
    /// /params targetElement Candidate control being inspected.
    /// /params elementKind Resolved interactive-control kind.
    /// /returns True when the element is supported.
    /// </summary>
    private static bool TryResolveInteractiveKind(VisualElement targetElement,
                                                  out ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        elementKind = ManagementToolInteractiveElementColorUtility.InteractiveElementKind.ButtonLike;

        if (targetElement is Button)
            return true;

        if (targetElement is ToolbarToggle)
            return true;

        if (targetElement is Foldout)
        {
            elementKind = ManagementToolInteractiveElementColorUtility.InteractiveElementKind.FoldoutLike;
            return true;
        }

        if (IsPopupLikeElement(targetElement))
        {
            elementKind = ManagementToolInteractiveElementColorUtility.InteractiveElementKind.PopupLike;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether one element should be treated as a popup-like control.
    /// /params targetElement Candidate control being inspected.
    /// /returns True when the element is a popup-like control.
    /// </summary>
    private static bool IsPopupLikeElement(VisualElement targetElement)
    {
        if (targetElement == null)
            return false;

        if (targetElement.ClassListContains(BasePopupFieldClassName))
            return true;

        string typeName = targetElement.GetType().Name;
        return typeName.Contains("PopupField", StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the persisted state key used by one interactive control.
    /// /params hierarchyRoot Root used to build stable hierarchical state keys.
    /// /params targetElement Target control whose key is being built.
    /// /params stateKeyPrefix Stable state-key prefix used for all controls under the root.
    /// /params elementKind Interactive control kind used to build the terminal segment.
    /// /returns One persisted state key for the target control.
    /// </summary>
    private static string BuildElementStateKey(VisualElement hierarchyRoot,
                                               VisualElement targetElement,
                                               string stateKeyPrefix,
                                               ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        List<string> pathSegments = new List<string>();
        VisualElement currentElement = targetElement;

        while (currentElement != null)
        {
            if (currentElement == hierarchyRoot)
                break;

            string currentToken = ResolvePathToken(currentElement);

            if (!string.IsNullOrWhiteSpace(currentToken))
            {
                int siblingOrdinal = ResolveSiblingOrdinal(currentElement, currentToken);
                pathSegments.Insert(0, currentToken + "_" + siblingOrdinal);
            }

            currentElement = currentElement.parent;
        }

        string leafPrefix = ResolveLeafPrefix(elementKind);
        string leafToken = ResolveDisplayToken(targetElement);

        if (string.IsNullOrWhiteSpace(leafToken))
            leafToken = SanitizeToken(targetElement.GetType().Name);

        pathSegments.Add(leafPrefix + "_" + leafToken);
        return stateKeyPrefix + "." + string.Join(".", pathSegments);
    }

    /// <summary>
    /// Resolves the leaf-prefix segment used to separate persisted keys by interactive kind.
    /// /params elementKind Interactive control kind used by the target element.
    /// /returns Stable persisted-key prefix for the leaf segment.
    /// </summary>
    private static string ResolveLeafPrefix(ManagementToolInteractiveElementColorUtility.InteractiveElementKind elementKind)
    {
        switch (elementKind)
        {
            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.PopupLike:
                return "Popup";

            case ManagementToolInteractiveElementColorUtility.InteractiveElementKind.FoldoutLike:
                return "Foldout";

            default:
                return "Control";
        }
    }

    /// <summary>
    /// Resolves one stable path token for the supplied element.
    /// /params targetElement Element whose token must be resolved.
    /// /returns One sanitized path token.
    /// </summary>
    private static string ResolvePathToken(VisualElement targetElement)
    {
        if (targetElement == null)
            return string.Empty;

        string displayToken = ResolveDisplayToken(targetElement);

        if (!string.IsNullOrWhiteSpace(displayToken))
            return displayToken;

        if (!string.IsNullOrWhiteSpace(targetElement.name))
            return SanitizeToken(targetElement.name);

        return SanitizeToken(targetElement.GetType().Name);
    }

    /// <summary>
    /// Resolves one user-facing token for the supplied element whenever possible.
    /// /params targetElement Element whose user-facing text must be resolved.
    /// /returns One sanitized display token, or an empty string when unavailable.
    /// </summary>
    private static string ResolveDisplayToken(VisualElement targetElement)
    {
        if (targetElement == null)
            return string.Empty;

        if (targetElement is Button button && !string.IsNullOrWhiteSpace(button.text))
            return SanitizeToken(button.text);

        if (targetElement is ToolbarToggle toolbarToggle && !string.IsNullOrWhiteSpace(toolbarToggle.text))
            return SanitizeToken(toolbarToggle.text);

        if (targetElement is Foldout foldout && !string.IsNullOrWhiteSpace(foldout.text))
            return SanitizeToken(foldout.text);

        if (IsPopupLikeElement(targetElement))
        {
            Label popupLabel = targetElement.Q<Label>(className: BaseFieldLabelClassName);

            if (popupLabel != null && !string.IsNullOrWhiteSpace(popupLabel.text))
                return SanitizeToken(popupLabel.text);
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves the ordinal of one element among siblings that share the same path token.
    /// /params targetElement Target element whose ordinal must be resolved.
    /// /params pathToken Token used to compare siblings.
    /// /returns One zero-based ordinal for the target element.
    /// </summary>
    private static int ResolveSiblingOrdinal(VisualElement targetElement, string pathToken)
    {
        VisualElement parentElement = targetElement.parent;

        if (parentElement == null)
            return 0;

        int ordinal = 0;

        foreach (VisualElement sibling in parentElement.Children())
        {
            if (sibling == targetElement)
                return ordinal;

            if (!string.Equals(ResolvePathToken(sibling), pathToken, StringComparison.Ordinal))
                continue;

            ordinal++;
        }

        return ordinal;
    }

    /// <summary>
    /// Sanitizes one free-form token so it is stable and EditorPrefs-safe.
    /// /params rawToken Raw token text to sanitize.
    /// /returns One sanitized token.
    /// </summary>
    private static string SanitizeToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return "Unnamed";

        char[] characters = rawToken.Trim().ToCharArray();

        for (int index = 0; index < characters.Length; index++)
        {
            if (char.IsLetterOrDigit(characters[index]))
                continue;

            characters[index] = '_';
        }

        string sanitizedToken = new string(characters);

        while (sanitizedToken.Contains("__", StringComparison.Ordinal))
            sanitizedToken = sanitizedToken.Replace("__", "_", StringComparison.Ordinal);

        sanitizedToken = sanitizedToken.Trim('_');
        return string.IsNullOrWhiteSpace(sanitizedToken) ? "Unnamed" : sanitizedToken;
    }
    #endregion

    #endregion
}
