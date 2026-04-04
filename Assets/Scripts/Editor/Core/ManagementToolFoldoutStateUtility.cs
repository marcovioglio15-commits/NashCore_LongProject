using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides stable state keys and reusable foldout binding helpers shared by management-tool editor panels.
/// /params None.
/// /returns None.
/// </summary>
public static class ManagementToolFoldoutStateUtility
{
    #region Fields
    private static readonly Dictionary<string, bool> foldoutStateByKey = new Dictionary<string, bool>(StringComparer.Ordinal);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one foldout already bound to a persistent state key.
    /// /params title Visible title shown in the foldout header.
    /// /params stateKey Stable state key used to restore and store the expanded state.
    /// /params defaultValue Expanded state used when the key was never seen before.
    /// /returns Configured foldout instance.
    /// </summary>
    public static Foldout CreateFoldout(string title, string stateKey, bool defaultValue)
    {
        Foldout foldout = new Foldout();
        foldout.text = title;
        BindFoldoutState(foldout, stateKey, defaultValue);
        return foldout;
    }

    /// <summary>
    /// Creates one foldout whose state key is derived from a serialized property plus a local suffix.
    /// /params property Serialized property that identifies the owning data context.
    /// /params title Visible title shown in the foldout header.
    /// /params suffix Local suffix appended to the property key when multiple foldouts share the same property root.
    /// /params defaultValue Expanded state used when no persisted state exists yet.
    /// /returns Configured foldout instance.
    /// </summary>
    public static Foldout CreatePropertyFoldout(SerializedProperty property,
                                                string title,
                                                string suffix,
                                                bool defaultValue)
    {
        string stateKey = BuildPropertyStateKey(property, suffix);
        return CreateFoldout(title, stateKey, defaultValue);
    }

    /// <summary>
    /// Binds one foldout to the provided state key.
    /// /params foldout Foldout that must persist its expanded state.
    /// /params stateKey Stable state key used to restore and store the expanded state.
    /// /params defaultValue Expanded state used when no persisted state exists yet.
    /// /returns None.
    /// </summary>
    public static void BindFoldoutState(Foldout foldout, string stateKey, bool defaultValue)
    {
        if (foldout == null)
            return;

        if (string.IsNullOrWhiteSpace(stateKey))
        {
            foldout.value = defaultValue;
            return;
        }

        foldout.viewDataKey = stateKey;
        foldout.value = ResolveFoldoutState(stateKey, defaultValue);
        foldout.RegisterValueChangedCallback(evt =>
        {
            SetFoldoutState(stateKey, evt.newValue);
        });
    }

    /// <summary>
    /// Builds the stable key that identifies the serialized object owning one UI subtree.
    /// /params serializedObject Serialized object that owns the target stateful controls.
    /// /returns Stable serialized-object key, or an empty string when unavailable.
    /// </summary>
    public static string BuildSerializedObjectStateKey(SerializedObject serializedObject)
    {
        if (serializedObject == null)
            return string.Empty;

        UnityEngine.Object targetObject = serializedObject.targetObject;

        if (targetObject == null)
            return string.Empty;

        string assetPath = AssetDatabase.GetAssetPath(targetObject);

        if (!string.IsNullOrWhiteSpace(assetPath))
            return string.Format("{0}|{1}", targetObject.GetType().FullName, assetPath);

        GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(targetObject);
        string globalObjectIdText = globalObjectId.ToString();

        if (!string.IsNullOrWhiteSpace(globalObjectIdText))
            return string.Format("{0}|{1}", targetObject.GetType().FullName, globalObjectIdText);

        return string.Format("{0}|Instance:{1}",
                             targetObject.GetType().FullName,
                             targetObject.GetInstanceID());
    }

    /// <summary>
    /// Builds the stable context key for one serialized property.
    /// /params property Serialized property that identifies the owning data context.
    /// /returns Stable property context key without any local suffix.
    /// </summary>
    public static string BuildPropertyContextKey(SerializedProperty property)
    {
        if (property == null)
            return string.Empty;

        SerializedObject serializedObject = property.serializedObject;

        if (serializedObject == null)
            return string.Empty;

        string objectKey = BuildSerializedObjectStateKey(serializedObject);
        string propertyKey = PlayerScalingStatKeyUtility.NormalizePropertyPath(serializedObject, property.propertyPath);

        if (string.IsNullOrWhiteSpace(propertyKey))
            propertyKey = property.propertyPath;

        if (string.IsNullOrWhiteSpace(objectKey))
            return propertyKey;

        return string.Format("{0}|{1}", objectKey, propertyKey);
    }

    /// <summary>
    /// Builds the stable state key for one serialized property and one local suffix.
    /// /params property Serialized property that identifies the owning data context.
    /// /params suffix Local suffix appended to distinguish multiple foldouts under the same property.
    /// /returns Stable state key, or the property context key when the suffix is empty.
    /// </summary>
    public static string BuildPropertyStateKey(SerializedProperty property, string suffix)
    {
        string propertyContextKey = BuildPropertyContextKey(property);

        if (string.IsNullOrWhiteSpace(propertyContextKey))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(suffix))
            return propertyContextKey;

        return string.Format("{0}|{1}", propertyContextKey, suffix.Trim());
    }

    /// <summary>
    /// Resolves the last persisted expanded state for one foldout key.
    /// /params stateKey Stable state key used by one foldout.
    /// /params defaultValue Expanded state returned when the key has not been stored yet.
    /// /returns Persisted expanded state or the provided default.
    /// </summary>
    public static bool ResolveFoldoutState(string stateKey, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return defaultValue;

        bool isExpanded;

        if (foldoutStateByKey.TryGetValue(stateKey, out isExpanded))
            return isExpanded;

        return defaultValue;
    }

    /// <summary>
    /// Stores or clears one foldout expanded state.
    /// /params stateKey Stable state key used by one foldout.
    /// /params expanded Expanded state that must be persisted.
    /// /returns None.
    /// </summary>
    public static void SetFoldoutState(string stateKey, bool expanded)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        foldoutStateByKey[stateKey] = expanded;
    }

    /// <summary>
    /// Removes stale foldout states whose keys are no longer part of one valid key set.
    /// /params keyPrefix Shared prefix used by the group that is currently being rebuilt.
    /// /params validStateKeys Current valid keys for the rebuilt group.
    /// /returns None.
    /// </summary>
    public static void PruneFoldoutStates(string keyPrefix, HashSet<string> validStateKeys)
    {
        if (string.IsNullOrWhiteSpace(keyPrefix))
            return;

        if (validStateKeys == null)
            return;

        List<string> keysToRemove = new List<string>();

        foreach (KeyValuePair<string, bool> entry in foldoutStateByKey)
        {
            if (!entry.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
                continue;

            if (validStateKeys.Contains(entry.Key))
                continue;

            keysToRemove.Add(entry.Key);
        }

        for (int index = 0; index < keysToRemove.Count; index++)
            foldoutStateByKey.Remove(keysToRemove[index]);
    }

    /// <summary>
    /// Captures the current expanded state of every foldout under the provided root that exposes a state key through viewDataKey.
    /// /params root Root visual element whose descendant foldouts must be persisted before a rebuild or clear.
    /// /returns None.
    /// </summary>
    public static void CaptureFoldoutStates(VisualElement root)
    {
        if (root == null)
            return;

        List<Foldout> foldouts = root.Query<Foldout>().ToList();

        for (int foldoutIndex = 0; foldoutIndex < foldouts.Count; foldoutIndex++)
        {
            Foldout foldout = foldouts[foldoutIndex];

            if (foldout == null)
                continue;

            if (string.IsNullOrWhiteSpace(foldout.viewDataKey))
                continue;

            SetFoldoutState(foldout.viewDataKey, foldout.value);
        }
    }
    #endregion

    #endregion
}
