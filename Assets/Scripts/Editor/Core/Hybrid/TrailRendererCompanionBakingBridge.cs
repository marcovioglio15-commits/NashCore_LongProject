#if UNITY_EDITOR
using System;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Registers TrailRenderer as additional companion type for DOTS baking.
/// </summary>
[InitializeOnLoad]
public static class TrailRendererCompanionBakingBridge
{
    #region Methods

    #region Lifecycle
    static TrailRendererCompanionBakingBridge()
    {
        RegisterTrailRendererCompanionType();
    }
    #endregion

    #region Helpers
    private static void RegisterTrailRendererCompanionType()
    {
        Type bakingUtilityType = Type.GetType("Unity.Entities.BakingUtility, Unity.Entities.Hybrid");

        if (bakingUtilityType == null)
            return;

        MethodInfo addCompanionComponentTypeMethod = bakingUtilityType.GetMethod("AddAdditionalCompanionComponentType",
                                                                                  BindingFlags.Static | BindingFlags.NonPublic);

        if (addCompanionComponentTypeMethod == null)
            return;

        ComponentType trailRendererComponentType = ComponentType.ReadWrite<TrailRenderer>();
        addCompanionComponentTypeMethod.Invoke(null, new object[] { trailRendererComponentType });
    }
    #endregion

    #endregion
}
#endif
