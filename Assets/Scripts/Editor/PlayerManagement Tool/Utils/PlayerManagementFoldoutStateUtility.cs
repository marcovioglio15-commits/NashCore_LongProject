using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides stable state keys and reusable foldout binding helpers for Player Management Tool UI Toolkit rebuilds.
/// This keeps foldout open/closed state aligned with the same serialized object and property even after redraws or array reorders.
/// </summary>
public static class PlayerManagementFoldoutStateUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one foldout already bound to a persistent state key.
    /// title Visible title shown in the foldout header.
    /// stateKey Stable state key used to restore and store the expanded state.
    /// defaultValue Expanded state used when the key was never seen before.
    /// returns Configured foldout instance.
    /// </summary>
    public static Foldout CreateFoldout(string title, string stateKey, bool defaultValue)
    {
        return ManagementToolFoldoutStateUtility.CreateFoldout(title, stateKey, defaultValue);
    }

    /// <summary>
    /// Creates one foldout whose state key is derived from a serialized property plus a local suffix.
    /// property Serialized property that identifies the owning data context.
    /// title Visible title shown in the foldout header.
    /// suffix Local suffix appended to the property key when multiple foldouts share the same property root.
    /// defaultValue Expanded state used when no persisted state exists yet.
    /// returns Configured foldout instance.
    /// </summary>
    public static Foldout CreatePropertyFoldout(SerializedProperty property,
                                                string title,
                                                string suffix,
                                                bool defaultValue)
    {
        return ManagementToolFoldoutStateUtility.CreatePropertyFoldout(property, title, suffix, defaultValue);
    }

    /// <summary>
    /// Binds one foldout to the provided state key.
    /// foldout Foldout that must persist its expanded state.
    /// stateKey Stable state key used to restore and store the expanded state.
    /// defaultValue Expanded state used when no persisted state exists yet.
    /// returns void
    /// </summary>
    public static void BindFoldoutState(Foldout foldout, string stateKey, bool defaultValue)
    {
        ManagementToolFoldoutStateUtility.BindFoldoutState(foldout, stateKey, defaultValue);
    }

    /// <summary>
    /// Builds the stable key that identifies the serialized object owning one UI subtree.
    /// serializedObject Serialized object that owns the target stateful controls.
    /// returns Stable serialized-object key, or an empty string when unavailable.
    /// </summary>
    public static string BuildSerializedObjectStateKey(SerializedObject serializedObject)
    {
        return ManagementToolFoldoutStateUtility.BuildSerializedObjectStateKey(serializedObject);
    }

    /// <summary>
    /// Builds the stable context key for one serialized property.
    /// property Serialized property that identifies the owning data context.
    /// returns Stable property context key without any local suffix.
    /// </summary>
    public static string BuildPropertyContextKey(SerializedProperty property)
    {
        return ManagementToolFoldoutStateUtility.BuildPropertyContextKey(property);
    }

    /// <summary>
    /// Builds the stable state key for one serialized property and one local suffix.
    /// property Serialized property that identifies the owning data context.
    /// suffix Local suffix appended to distinguish multiple foldouts under the same property.
    /// returns Stable state key, or the property context key when the suffix is empty.
    /// </summary>
    public static string BuildPropertyStateKey(SerializedProperty property, string suffix)
    {
        return ManagementToolFoldoutStateUtility.BuildPropertyStateKey(property, suffix);
    }

    /// <summary>
    /// Resolves the last persisted expanded state for one foldout key.
    /// stateKey Stable state key used by one foldout.
    /// defaultValue Expanded state returned when the key has not been stored yet.
    /// returns Persisted expanded state or the provided default.
    /// </summary>
    public static bool ResolveFoldoutState(string stateKey, bool defaultValue)
    {
        return ManagementToolFoldoutStateUtility.ResolveFoldoutState(stateKey, defaultValue);
    }

    /// <summary>
    /// Stores or clears one foldout expanded state.
    /// stateKey Stable state key used by one foldout.
    /// expanded Expanded state that must be persisted.
    /// returns void
    /// </summary>
    public static void SetFoldoutState(string stateKey, bool expanded)
    {
        ManagementToolFoldoutStateUtility.SetFoldoutState(stateKey, expanded);
    }

    /// <summary>
    /// Removes stale foldout states whose keys are no longer part of one valid key set.
    /// keyPrefix Shared prefix used by the group that is currently being rebuilt.
    /// validStateKeys Current valid keys for the rebuilt group.
    /// returns void
    /// </summary>
    public static void PruneFoldoutStates(string keyPrefix, HashSet<string> validStateKeys)
    {
        ManagementToolFoldoutStateUtility.PruneFoldoutStates(keyPrefix, validStateKeys);
    }

    /// <summary>
    /// Captures the current expanded state of every foldout under the provided root that exposes a state key through viewDataKey.
    /// root Root visual element whose descendant foldouts must be persisted before a rebuild/clear.
    /// returns void
    /// </summary>
    public static void CaptureFoldoutStates(VisualElement root)
    {
        ManagementToolFoldoutStateUtility.CaptureFoldoutStates(root);
    }
    #endregion

    #endregion
}
