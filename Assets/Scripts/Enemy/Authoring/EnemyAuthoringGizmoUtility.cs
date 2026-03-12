using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Draws selected-scene gizmos for enemy authoring instances.
/// </summary>
public static class EnemyAuthoringGizmoUtility
{
    #region Methods

    #region Drawing
    public static void DrawSelectedGizmos(Vector3 center,
                                          Transform elementalVfxAnchor,
                                          EnemyWorldSpaceStatusBarsView worldSpaceStatusBarsView,
                                          bool drawContactRadiusGizmo,
                                          bool contactDamageEnabled,
                                          float contactRadius,
                                          Color contactGizmoColor,
                                          bool drawAreaRadiusGizmo,
                                          bool areaDamageEnabled,
                                          float areaRadius,
                                          Color areaGizmoColor,
                                          bool drawSeparationRadiusGizmo,
                                          float separationRadius,
                                          Color separationGizmoColor,
                                          bool drawBodyRadiusGizmo,
                                          float bodyRadius,
                                          Color bodyGizmoColor,
                                          bool drawAggressivenessRadiusGizmo,
                                          float steeringAggressiveness,
                                          Color aggressivenessGizmoColor,
                                          bool drawVisualDistanceGizmo,
                                          bool enableDistanceCulling,
                                          float maxVisibleDistance,
                                          Color visualDistanceGizmoColor,
                                          Color elementalAnchorGizmoColor,
                                          bool drawWorldSpaceBarsGizmo,
                                          Color worldSpaceBarsGizmoColor)
    {
        if (drawContactRadiusGizmo && contactDamageEnabled)
            DrawWireRadius(center, math.max(0f, contactRadius), contactGizmoColor);

        if (drawAreaRadiusGizmo && areaDamageEnabled)
            DrawWireRadius(center, math.max(0f, areaRadius), areaGizmoColor);

        if (drawSeparationRadiusGizmo)
            DrawWireRadius(center, math.max(0f, separationRadius), separationGizmoColor);

        if (drawBodyRadiusGizmo)
            DrawWireRadius(center, math.max(0f, bodyRadius), bodyGizmoColor);

        if (drawAggressivenessRadiusGizmo)
        {
            float effectiveClearanceRadius = math.max(0f, separationRadius) * math.max(0f, steeringAggressiveness);
            DrawWireRadius(center, effectiveClearanceRadius, aggressivenessGizmoColor);
        }

        if (drawVisualDistanceGizmo && enableDistanceCulling)
            DrawWireRadius(center, math.max(0f, maxVisibleDistance), visualDistanceGizmoColor);

        if (elementalVfxAnchor != null)
        {
            Gizmos.color = elementalAnchorGizmoColor;
            Vector3 anchorPosition = elementalVfxAnchor.position;
            Gizmos.DrawLine(center, anchorPosition);
            Gizmos.DrawWireSphere(anchorPosition, 0.14f);
        }

        DrawWorldSpaceBarsGizmo(center, worldSpaceStatusBarsView, drawWorldSpaceBarsGizmo, worldSpaceBarsGizmoColor);
    }

    private static void DrawWireRadius(Vector3 center, float radius, Color color)
    {
        if (radius <= 0f)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(center, radius);
    }

    private static void DrawWorldSpaceBarsGizmo(Vector3 center,
                                                EnemyWorldSpaceStatusBarsView worldSpaceStatusBarsView,
                                                bool drawWorldSpaceBarsGizmo,
                                                Color worldSpaceBarsGizmoColor)
    {
        if (!drawWorldSpaceBarsGizmo)
            return;

        if (worldSpaceStatusBarsView == null)
            return;

        Transform barsTransform = worldSpaceStatusBarsView.transform;

        if (barsTransform == null)
            return;

        Gizmos.color = worldSpaceBarsGizmoColor;
        Vector3 barsPosition = barsTransform.position;
        Gizmos.DrawLine(center, barsPosition);
        Gizmos.DrawWireCube(barsPosition, new Vector3(0.2f, 0.08f, 0.02f));
    }
    #endregion

    #endregion
}
