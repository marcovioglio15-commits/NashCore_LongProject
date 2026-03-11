#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards DOTS scene streaming initialization on editor domain/play transitions.
/// This avoids machine-specific initialization races that can surface as
/// NullReferenceExceptions in AsyncLoadSceneOperation.ScheduleSceneRead.
/// </summary>
[InitializeOnLoad]
public static class EntitiesSceneStreamingInitGuard
{
    private const string AsyncLoadSceneOperationTypeName = "Unity.Scenes.AsyncLoadSceneOperation, Unity.Scenes";
    private const string InitMethodName = "EditorInitializeOnLoadMethod";
    private const string UnityObjectRefsFieldName = "s_UnityObjectsRefs";
    private const bool LogForcedInitialization = false;

    private static Type s_AsyncLoadSceneOperationType;
    private static MethodInfo s_InitMethod;
    private static FieldInfo s_UnityObjectRefsField;
    private static bool s_HasLoggedFallbackInit;

    static EntitiesSceneStreamingInitGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            EnsureInitialized("before-play");
    }

    private static void EnsureInitialized(string context)
    {
        if (!TryResolveReflectionMembers())
            return;

        if (IsUnityObjectRefsCreated())
            return;

        try
        {
            s_InitMethod.Invoke(null, null);

            if (LogForcedInitialization && !s_HasLoggedFallbackInit)
            {
                s_HasLoggedFallbackInit = true;
                Debug.Log(
                    string.Format(
                        "[EntitiesSceneStreamingInitGuard] Forced DOTS scene-streaming init ({0}) to prevent AsyncLoadSceneOperation null refs.",
                        context));
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                string.Format(
                    "[EntitiesSceneStreamingInitGuard] Failed to force DOTS scene-streaming init ({0}): {1}",
                    context,
                    exception.Message));
        }
    }

    private static bool IsUnityObjectRefsCreated()
    {
        object value = s_UnityObjectRefsField.GetValue(null);

        if (value == null)
            return false;

        PropertyInfo isCreatedProperty = value.GetType().GetProperty("IsCreated",
                                                                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (isCreatedProperty == null || isCreatedProperty.PropertyType != typeof(bool))
            return false;

        object result = isCreatedProperty.GetValue(value, null);
        return result is bool isCreated && isCreated;
    }

    private static bool TryResolveReflectionMembers()
    {
        if (s_AsyncLoadSceneOperationType == null)
            s_AsyncLoadSceneOperationType = Type.GetType(AsyncLoadSceneOperationTypeName);

        if (s_AsyncLoadSceneOperationType == null)
            return false;

        if (s_InitMethod == null)
        {
            s_InitMethod = s_AsyncLoadSceneOperationType.GetMethod(InitMethodName,
                                                                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        }

        if (s_UnityObjectRefsField == null)
        {
            s_UnityObjectRefsField = s_AsyncLoadSceneOperationType.GetField(UnityObjectRefsFieldName,
                                                                             BindingFlags.NonPublic | BindingFlags.Static);
        }

        return s_InitMethod != null && s_UnityObjectRefsField != null;
    }
}
#endif
