using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Adds persistent color customization to interactive management-tool controls such as buttons, foldouts and popup fields.
/// /params None.
/// /returns None.
/// </summary>
public static class ManagementToolInteractiveElementColorUtility
{
    #region Nested Types
    /// <summary>
    /// Declares the supported interactive control kinds.
    /// /params None.
    /// /returns None.
    /// </summary>
    public enum InteractiveElementKind
    {
        ButtonLike = 0,
        PopupLike = 1,
        FoldoutLike = 2
    }

    /// <summary>
    /// Stores one live interactive control registered under one persisted state key.
    /// /params None.
    /// /returns None.
    /// </summary>
    private sealed class InteractiveElementRegistration
    {
        #region Fields
        public readonly VisualElement TargetElement;
        public readonly string StateKey;
        public readonly InteractiveElementKind ElementKind;
        #endregion

        #region Methods

        #region Constructors
        /// <summary>
        /// Creates one live interactive-control registration.
        /// /params targetElement Control currently attached to a management-tool window.
        /// /params stateKey Stable persistence key used by EditorPrefs.
        /// /params elementKind Interactive control kind used to apply colors correctly.
        /// /returns None.
        /// </summary>
        public InteractiveElementRegistration(VisualElement targetElement,
                                              string stateKey,
                                              InteractiveElementKind elementKind)
        {
            TargetElement = targetElement;
            StateKey = stateKey;
            ElementKind = elementKind;
        }
        #endregion

        #endregion
    }
    #endregion

    #region Constants
    private const string RootRegisteredClassName = "management-tool-interactive-colors-root";
    private const string InteractiveRightClickRegisteredClassName = "management-tool-interactive-colors-right-click";
    #endregion

    #region Fields
    private static readonly Dictionary<string, List<InteractiveElementRegistration>> registrationsByStateKey =
        new Dictionary<string, List<InteractiveElementRegistration>>();
    private static readonly Dictionary<VisualElement, string> stateKeyPrefixesByRoot =
        new Dictionary<VisualElement, string>();
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Registers the provided root so current and future interactive descendants expose persistent color customization.
    /// /params root Root visual element that owns the management-tool hierarchy.
    /// /params stateKeyPrefix Stable state-key prefix used for all controls under the root.
    /// /returns None.
    /// </summary>
    public static void RegisterHierarchy(VisualElement root, string stateKeyPrefix)
    {
        if (root == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKeyPrefix))
            return;

        stateKeyPrefixesByRoot[root] = stateKeyPrefix;

        if (!root.ClassListContains(RootRegisteredClassName))
        {
            root.AddToClassList(RootRegisteredClassName);
            bool refreshScheduled = false;
            HashSet<VisualElement> pendingRefreshRoots = new HashSet<VisualElement>();

            void ScheduleRefresh(VisualElement refreshRoot)
            {
                if (refreshRoot != null)
                    pendingRefreshRoots.Add(refreshRoot);

                if (refreshScheduled)
                    return;

                refreshScheduled = true;
                root.schedule.Execute(() =>
                {
                    refreshScheduled = false;

                    // Re-scan the entire root when no scoped refresh root is pending.
                    if (pendingRefreshRoots.Count <= 0)
                    {
                        ManagementToolInteractiveElementColorHierarchyUtility.RegisterHierarchyElements(root, root, stateKeyPrefix);
                        return;
                    }

                    // Re-scan only the affected subtrees after attach/geometry changes.
                    List<VisualElement> refreshRoots = new List<VisualElement>(pendingRefreshRoots);
                    pendingRefreshRoots.Clear();

                    for (int refreshIndex = 0; refreshIndex < refreshRoots.Count; refreshIndex++)
                    {
                        VisualElement currentRefreshRoot = refreshRoots[refreshIndex];

                        if (currentRefreshRoot == null)
                            continue;

                        ManagementToolInteractiveElementColorHierarchyUtility.RegisterHierarchyElements(root,
                                                                                                        currentRefreshRoot,
                                                                                                        stateKeyPrefix);
                    }
                });
            }

            root.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                ScheduleRefresh(root);
            });
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                ScheduleRefresh(root);
            });
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                VisualElement targetElement = evt.target as VisualElement;

                if (targetElement == null)
                    return;

                if (targetElement == root)
                    return;

                ScheduleRefresh(targetElement);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                VisualElement targetElement = evt.target as VisualElement;

                if (targetElement == null)
                    return;

                if (targetElement == root)
                    return;

                ScheduleRefresh(targetElement);
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<MouseDownEvent>(evt =>
            {
                HandleRootRightMouseDownFallback(root, evt);
            });
        }

        ManagementToolInteractiveElementColorHierarchyUtility.RegisterHierarchyElements(root, root, stateKeyPrefix);
    }

    /// <summary>
    /// Forces one immediate rescan of a subtree already living under a registered management-tool root.
    /// /params refreshRoot Subtree root that should be re-scanned for recolorable controls.
    /// /returns None.
    /// </summary>
    public static void RefreshRegisteredSubtree(VisualElement refreshRoot)
    {
        if (refreshRoot == null)
            return;

        VisualElement registeredRoot = ResolveRegisteredRoot(refreshRoot);

        if (registeredRoot == null)
            return;

        string stateKeyPrefix;

        if (!stateKeyPrefixesByRoot.TryGetValue(registeredRoot, out stateKeyPrefix))
            return;

        if (string.IsNullOrWhiteSpace(stateKeyPrefix))
            return;

        ManagementToolInteractiveElementColorHierarchyUtility.RegisterHierarchyElements(registeredRoot,
                                                                                        refreshRoot,
                                                                                        stateKeyPrefix);
    }

    /// <summary>
    /// Registers one interactive control for persistent recoloring and connects its direct right-click opening.
    /// /params targetElement Target control that should expose color editing.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /params elementKind Interactive control kind used to apply colors correctly.
    /// /returns None.
    /// </summary>
    public static void RegisterInteractiveElement(VisualElement targetElement,
                                                  string stateKey,
                                                  InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        RegisterInteractiveInstance(targetElement, stateKey, elementKind);
        ApplySavedColors(targetElement, stateKey, elementKind);

        if (targetElement.ClassListContains(InteractiveRightClickRegisteredClassName))
            return;

        targetElement.AddToClassList(InteractiveRightClickRegisteredClassName);

        targetElement.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            RegisterInteractiveInstance(targetElement, stateKey, elementKind);
            ApplySavedColors(targetElement, stateKey, elementKind);
        });
        targetElement.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            UnregisterInteractiveInstance(targetElement, stateKey);
        });
        targetElement.RegisterCallback<MouseDownEvent>(evt =>
        {
            HandleInteractiveRightMouseDown(targetElement, stateKey, elementKind, evt);
        }, TrickleDown.TrickleDown);
    }

    /// <summary>
    /// Saves and immediately applies one text/background color pair to every live interactive control bound to the provided state key.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /params textColor Persisted text color.
    /// /params backgroundColor Persisted background color.
    /// /returns None.
    /// </summary>
    public static void SaveAndApplyColors(string stateKey, Color textColor, Color backgroundColor)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        ManagementToolStateUtility.SaveColorPair(stateKey, textColor, backgroundColor);
        ApplySavedColorsToRegisteredElements(stateKey);
        ManagementToolColorRefreshUtility.RepaintOpenManagementToolWindows();
    }

    /// <summary>
    /// Saves and immediately applies one text/background color pair to the provided interactive control and every live control that shares its state key.
    /// /params targetElement Target control that should be updated.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /params elementKind Interactive control kind used to apply colors correctly.
    /// /params textColor Persisted text color.
    /// /params backgroundColor Persisted background color.
    /// /returns None.
    /// </summary>
    public static void SaveAndApplyColors(VisualElement targetElement,
                                          string stateKey,
                                          InteractiveElementKind elementKind,
                                          Color textColor,
                                          Color backgroundColor)
    {
        if (targetElement != null)
            RegisterInteractiveInstance(targetElement, stateKey, elementKind);

        SaveAndApplyColors(stateKey, textColor, backgroundColor);
    }

    /// <summary>
    /// Restores the default styling for every live interactive control bound to the provided state key and removes its persisted custom colors.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /returns None.
    /// </summary>
    public static void ResetColors(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        ManagementToolStateUtility.DeleteState(stateKey);
        ApplySavedColorsToRegisteredElements(stateKey);
        ManagementToolColorRefreshUtility.RepaintOpenManagementToolWindows();
    }

    /// <summary>
    /// Restores the default styling for the provided interactive control and every live control that shares its state key.
    /// /params targetElement Target control that should be reset.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /params elementKind Interactive control kind used to clear colors correctly.
    /// /returns None.
    /// </summary>
    public static void ResetColors(VisualElement targetElement, string stateKey, InteractiveElementKind elementKind)
    {
        if (targetElement != null)
            RegisterInteractiveInstance(targetElement, stateKey, elementKind);

        ResetColors(stateKey);
    }

    /// <summary>
    /// Applies the saved text/background colors to one interactive control when such state exists.
    /// /params targetElement Target control that should receive its saved colors.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /params elementKind Interactive control kind used to apply colors correctly.
    /// /returns None.
    /// </summary>
    public static void ApplySavedColors(VisualElement targetElement, string stateKey, InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        if (ManagementToolStateUtility.TryLoadColorPair(stateKey, out Color textColor, out Color backgroundColor))
        {
            ManagementToolInteractiveElementColorStyleUtility.ApplyColors(targetElement, elementKind, textColor, backgroundColor);
            return;
        }

        ManagementToolInteractiveElementColorStyleUtility.ClearColors(targetElement, elementKind);
    }

    /// <summary>
    /// Resolves the current visible text color of the provided interactive control.
    /// /params targetElement Target control being inspected.
    /// /params elementKind Interactive control kind used to read the correct visual node.
    /// /returns The currently resolved text color.
    /// </summary>
    public static Color ResolveCurrentTextColor(VisualElement targetElement, InteractiveElementKind elementKind)
    {
        return ManagementToolInteractiveElementColorStyleUtility.ResolveCurrentTextColor(targetElement, elementKind);
    }

    /// <summary>
    /// Resolves the current visible background color of the provided interactive control.
    /// /params targetElement Target control being inspected.
    /// /params elementKind Interactive control kind used to read the correct visual node.
    /// /returns The currently resolved background color.
    /// </summary>
    public static Color ResolveCurrentBackgroundColor(VisualElement targetElement, InteractiveElementKind elementKind)
    {
        return ManagementToolInteractiveElementColorStyleUtility.ResolveCurrentBackgroundColor(targetElement, elementKind);
    }

    /// <summary>
    /// Returns whether the provided interactive control kind supports visible background recoloring.
    /// /params elementKind Interactive control kind being inspected.
    /// /returns True when background recoloring should be exposed to the user.
    /// </summary>
    public static bool CanCustomizeBackground(InteractiveElementKind elementKind)
    {
        switch (elementKind)
        {
            case InteractiveElementKind.ButtonLike:
            case InteractiveElementKind.PopupLike:
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Appends one browser entry for each currently visible interactive recolor target under the supplied root.
    /// /params root Root visual element whose live recolorable controls should be collected.
    /// /params results Target list that receives the collected entries.
    /// /returns None.
    /// </summary>
    internal static void AppendBrowserEntries(VisualElement root, IList<ManagementToolColorBrowserEntry> results)
    {
        if (root == null)
            return;

        if (results == null)
            return;

        string stateKeyPrefix;

        if (!TryGetStateKeyPrefix(root, out stateKeyPrefix))
            return;

        HashSet<string> registeredStateKeys = new HashSet<string>();
        ManagementToolInteractiveElementColorHierarchyUtility.AppendBrowserEntries(root,
                                                                                   root,
                                                                                   stateKeyPrefix,
                                                                                   results,
                                                                                   registeredStateKeys);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Registers one live interactive-control instance under its persisted state key.
    /// /params targetElement Target control instance.
    /// /params stateKey Stable persistence key used by the control.
    /// /params elementKind Interactive control kind used to apply colors correctly.
    /// /returns None.
    /// </summary>
    private static void RegisterInteractiveInstance(VisualElement targetElement,
                                                    string stateKey,
                                                    InteractiveElementKind elementKind)
    {
        if (targetElement == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        List<InteractiveElementRegistration> registeredElements;

        if (!registrationsByStateKey.TryGetValue(stateKey, out registeredElements))
        {
            registeredElements = new List<InteractiveElementRegistration>();
            registrationsByStateKey[stateKey] = registeredElements;
        }

        for (int registrationIndex = 0; registrationIndex < registeredElements.Count; registrationIndex++)
        {
            InteractiveElementRegistration registration = registeredElements[registrationIndex];

            if (registration == null)
                continue;

            if (registration.TargetElement == targetElement)
                return;
        }

        registeredElements.Add(new InteractiveElementRegistration(targetElement, stateKey, elementKind));
    }

    /// <summary>
    /// Unregisters one live interactive-control instance from its persisted state key.
    /// /params targetElement Target control instance.
    /// /params stateKey Stable persistence key used by the control.
    /// /returns None.
    /// </summary>
    private static void UnregisterInteractiveInstance(VisualElement targetElement, string stateKey)
    {
        if (targetElement == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        List<InteractiveElementRegistration> registeredElements;

        if (!registrationsByStateKey.TryGetValue(stateKey, out registeredElements))
            return;

        for (int registrationIndex = registeredElements.Count - 1; registrationIndex >= 0; registrationIndex--)
        {
            InteractiveElementRegistration registration = registeredElements[registrationIndex];

            if (registration == null)
            {
                registeredElements.RemoveAt(registrationIndex);
                continue;
            }

            if (registration.TargetElement != targetElement)
                continue;

            registeredElements.RemoveAt(registrationIndex);
        }

        if (registeredElements.Count <= 0)
            registrationsByStateKey.Remove(stateKey);
    }

    /// <summary>
    /// Applies the persisted state to every currently live interactive control that shares the provided state key.
    /// /params stateKey Stable persistence key used by the controls.
    /// /returns None.
    /// </summary>
    private static void ApplySavedColorsToRegisteredElements(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        List<InteractiveElementRegistration> registeredElements;

        if (!registrationsByStateKey.TryGetValue(stateKey, out registeredElements))
            return;

        for (int registrationIndex = registeredElements.Count - 1; registrationIndex >= 0; registrationIndex--)
        {
            InteractiveElementRegistration registration = registeredElements[registrationIndex];

            if (registration == null)
            {
                registeredElements.RemoveAt(registrationIndex);
                continue;
            }

            if (registration.TargetElement == null || registration.TargetElement.panel == null)
            {
                registeredElements.RemoveAt(registrationIndex);
                continue;
            }

            ApplySavedColors(registration.TargetElement, registration.StateKey, registration.ElementKind);
            ManagementToolColorRefreshUtility.MarkElementHierarchyDirty(registration.TargetElement);
        }

        if (registeredElements.Count <= 0)
            registrationsByStateKey.Remove(stateKey);
    }

    /// <summary>
    /// Opens the dedicated color inspector when the user right-clicks one supported interactive control.
    /// /params targetElement Target control being edited.
    /// /params stateKey Stable persistence key used by EditorPrefs.
    /// /params elementKind Interactive control kind used to apply colors correctly.
    /// /params evt Mouse event emitted by UI Toolkit.
    /// /returns None.
    /// </summary>
    private static void HandleInteractiveRightMouseDown(VisualElement targetElement,
                                                        string stateKey,
                                                        InteractiveElementKind elementKind,
                                                        MouseDownEvent evt)
    {
        if (targetElement == null || evt == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        ManagementToolColorTriggerUtility.HandleRightMouseDown(evt, () =>
        {
            ManagementToolInteractiveElementColorPopup.Show(targetElement, stateKey, elementKind);
        });
    }

    /// <summary>
    /// Resolves the nearest registered management-tool root for one descendant element.
    /// /params startElement Descendant element whose owning root must be resolved.
    /// /returns The nearest registered root, or null when none is found.
    /// </summary>
    private static VisualElement ResolveRegisteredRoot(VisualElement startElement)
    {
        VisualElement currentElement = startElement;

        while (currentElement != null)
        {
            if (currentElement.ClassListContains(RootRegisteredClassName))
                return currentElement;

            currentElement = currentElement.parent;
        }

        return null;
    }

    /// <summary>
    /// Resolves the state-key prefix associated with one registered management-tool root.
    /// /params root Registered management-tool root being inspected.
    /// /params stateKeyPrefix Resolved stable prefix when present.
    /// /returns True when one state-key prefix is available for the supplied root.
    /// </summary>
    private static bool TryGetStateKeyPrefix(VisualElement root, out string stateKeyPrefix)
    {
        stateKeyPrefix = string.Empty;

        if (root == null)
            return false;

        if (!stateKeyPrefixesByRoot.TryGetValue(root, out stateKeyPrefix))
            return false;

        if (string.IsNullOrWhiteSpace(stateKeyPrefix))
            return false;

        return true;
    }

    /// <summary>
    /// Handles right-click fallback routing on the tool root for targets that were not yet directly registered.
    /// /params root Registered management-tool root receiving the bubbled mouse event.
    /// /params evt Mouse event emitted by UI Toolkit.
    /// /returns None.
    /// </summary>
    private static void HandleRootRightMouseDownFallback(VisualElement root, MouseDownEvent evt)
    {
        if (root == null || evt == null)
            return;

        if (evt.button != 1)
            return;

        VisualElement clickedElement = evt.target as VisualElement;

        if (clickedElement == null)
            return;

        if (ManagementToolCategoryLabelUtility.TryOpenFallbackFromExactTarget(clickedElement, evt))
            return;

        string stateKeyPrefix;

        if (!TryGetStateKeyPrefix(root, out stateKeyPrefix))
            return;

        if (ManagementToolInteractiveElementColorHierarchyUtility.TryOpenRightClickFallback(root,
                                                                                            clickedElement,
                                                                                            stateKeyPrefix,
                                                                                            evt))
            return;

        ManagementToolCategoryLabelUtility.TryOpenFallbackFromAncestors(root, clickedElement, evt);
    }

    #endregion

    #endregion
}
