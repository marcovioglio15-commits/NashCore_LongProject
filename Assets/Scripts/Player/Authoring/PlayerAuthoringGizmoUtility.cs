using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws PlayerAuthoring scene gizmos without keeping all visualization logic inside the MonoBehaviour file.
/// </summary>
public static class PlayerAuthoringGizmoUtility
{
    #region Constants
    private static readonly Color MovementGizmoColor = new Color(0.2f, 0.8f, 0.4f, 0.9f);
    private static readonly Color LookGizmoColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    private static readonly Color CameraFollowGizmoColor = new Color(1f, 0.8f, 0.2f, 0.9f);
    private static readonly Color CameraRoomGizmoColor = new Color(1f, 0.5f, 0.2f, 0.9f);
    private static readonly Color ShootingGizmoColor = new Color(1f, 0.25f, 0.25f, 0.9f);
    private static readonly Color PowerUpBombGizmoColor = new Color(1f, 0.35f, 0.15f, 0.75f);
    private static readonly Color PowerUpDashGizmoColor = new Color(0.25f, 0.85f, 1f, 0.85f);
    private static readonly Color RuntimeVisualBridgeGizmoColor = new Color(1f, 0.35f, 0.85f, 0.9f);
    private const float LookRadiusScale = 0.9f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Draws all configured PlayerAuthoring scene gizmos.
    /// /params authoring: Player authoring component being visualized.
    /// /returns void.
    /// </summary>
    public static void DrawSelectedGizmos(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return;

        PlayerControllerPreset controllerPreset = authoring.GetControllerPreset();

        if (controllerPreset == null)
            return;

        DrawMovementGizmo(authoring, controllerPreset);
        DrawLookGizmo(authoring, controllerPreset);
        DrawCameraGizmo(authoring, controllerPreset);
        DrawShootingGizmo(authoring, controllerPreset);
        DrawAnimationDebugGizmo(authoring);
        DrawRuntimeVisualBridgeGizmo(authoring);
        DrawPowerUpsGizmos(authoring);
    }
    #endregion

    #region Private Methods
    private static void DrawMovementGizmo(PlayerAuthoring authoring, PlayerControllerPreset preset)
    {
        MovementSettings movementSettings = preset.MovementSettings;

        if (movementSettings == null)
            return;

        Transform authoringTransform = authoring.transform;
        Vector3 origin = authoringTransform.position;
        float radius = authoring.GizmoRadius;
        Gizmos.color = MovementGizmoColor;

        if (movementSettings.DirectionsMode == MovementDirectionsMode.AllDirections)
        {
            Gizmos.DrawWireSphere(origin, radius);
            return;
        }

        int directionCount = Mathf.Max(1, movementSettings.DiscreteDirectionCount);
        float step = 360f / directionCount;
        float offset = movementSettings.DirectionOffsetDegrees;

        for (int index = 0; index < directionCount; index++)
        {
            float angle = (index * step) + offset;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Gizmos.DrawLine(origin, origin + direction * radius);
        }

        Gizmos.DrawWireSphere(origin, radius);
    }

    private static void DrawLookGizmo(PlayerAuthoring authoring, PlayerControllerPreset preset)
    {
        LookSettings lookSettings = preset.LookSettings;

        if (lookSettings == null)
            return;

        Vector3 center = authoring.transform.position + Vector3.up * 0.05f;
        float radius = authoring.GizmoRadius * LookRadiusScale;
        Gizmos.color = LookGizmoColor;

        if (lookSettings.DirectionsMode == LookDirectionsMode.AllDirections)
        {
            Gizmos.DrawWireSphere(center, radius);
            return;
        }

        if (lookSettings.DirectionsMode == LookDirectionsMode.DiscreteCount)
        {
            int directionCount = Mathf.Max(1, lookSettings.DiscreteDirectionCount);
            float step = 360f / directionCount;
            float offset = lookSettings.DirectionOffsetDegrees;

            for (int index = 0; index < directionCount; index++)
            {
                float angle = (index * step) + offset;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Gizmos.DrawLine(center, center + direction * radius);
            }

            Gizmos.DrawWireSphere(center, radius);
            return;
        }

        DrawConeGizmo(authoring, center, lookSettings.FrontConeEnabled, 0f, lookSettings.FrontConeAngle);
        DrawConeGizmo(authoring, center, lookSettings.RightConeEnabled, 90f, lookSettings.RightConeAngle);
        DrawConeGizmo(authoring, center, lookSettings.BackConeEnabled, 180f, lookSettings.BackConeAngle);
        DrawConeGizmo(authoring, center, lookSettings.LeftConeEnabled, 270f, lookSettings.LeftConeAngle);
    }

    private static void DrawConeGizmo(PlayerAuthoring authoring, Vector3 center, bool enabled, float centerAngle, float coneAngle)
    {
        if (!enabled)
            return;

        float halfAngle = coneAngle * 0.5f;
        Vector3 left = Quaternion.Euler(0f, centerAngle - halfAngle, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, centerAngle + halfAngle, 0f) * Vector3.forward;
        float radius = authoring.GizmoRadius;
        Gizmos.DrawLine(center, center + left * radius);
        Gizmos.DrawLine(center, center + right * radius);
    }

    private static void DrawCameraGizmo(PlayerAuthoring authoring, PlayerControllerPreset preset)
    {
        CameraSettings cameraSettings = preset.CameraSettings;

        if (cameraSettings == null)
            return;

        Transform authoringTransform = authoring.transform;

        switch (cameraSettings.Behavior)
        {
            case CameraBehavior.FollowWithOffset:
                Gizmos.color = CameraFollowGizmoColor;
                Vector3 offsetPosition = authoringTransform.position + cameraSettings.FollowOffset;
                Gizmos.DrawLine(authoringTransform.position, offsetPosition);
                Gizmos.DrawWireSphere(offsetPosition, 0.2f);
                return;
            case CameraBehavior.RoomFixed:
                if (cameraSettings.RoomAnchor == null)
                    return;

                Gizmos.color = CameraRoomGizmoColor;
                Vector3 anchorPosition = cameraSettings.RoomAnchor.position;
                Gizmos.DrawLine(authoringTransform.position, anchorPosition);
                Gizmos.DrawWireSphere(anchorPosition, 0.25f);
                return;
        }
    }

    private static void DrawShootingGizmo(PlayerAuthoring authoring, PlayerControllerPreset preset)
    {
        ShootingSettings shootingSettings = preset.ShootingSettings;

        if (shootingSettings == null)
            return;

        Transform reference = authoring.WeaponReference != null ? authoring.WeaponReference : authoring.transform;
        Vector3 origin = reference.position;
        Vector3 spawnPoint = origin + reference.rotation * shootingSettings.ShootOffset;
        Vector3 forward = reference.forward;
        Gizmos.color = ShootingGizmoColor;
        Gizmos.DrawLine(origin, spawnPoint);
        Gizmos.DrawWireSphere(spawnPoint, 0.12f);
        Gizmos.DrawLine(spawnPoint, spawnPoint + forward * 0.5f);
    }

    private static void DrawAnimationDebugGizmo(PlayerAuthoring authoring)
    {
        if (!authoring.DrawAnimationDebugGizmos)
            return;

        Transform authoringTransform = authoring.transform;
        float axisLength = Mathf.Max(0.1f, authoring.AnimationDebugAxisLength);
        Vector3 origin = authoringTransform.position + Vector3.up * 0.06f;
        Vector3 forward = authoringTransform.forward * axisLength;
        Vector3 right = authoringTransform.right * axisLength;
        Gizmos.color = new Color(0.15f, 0.95f, 0.85f, 0.95f);
        Gizmos.DrawLine(origin, origin + forward);
        Gizmos.color = new Color(0.95f, 0.7f, 0.2f, 0.95f);
        Gizmos.DrawLine(origin, origin + right);
    }

    private static void DrawRuntimeVisualBridgeGizmo(PlayerAuthoring authoring)
    {
        if (!authoring.DrawRuntimeVisualBridgeGizmo)
            return;

        Transform authoringTransform = authoring.transform;
        Vector3 origin = authoringTransform.position + Vector3.up * 0.03f;
        Vector3 offsetPoint = origin + authoringTransform.rotation * authoring.RuntimeVisualBridgeOffset;
        Gizmos.color = RuntimeVisualBridgeGizmoColor;
        Gizmos.DrawLine(origin, offsetPoint);
        Gizmos.DrawWireSphere(offsetPoint, 0.08f);
    }

    private static void DrawPowerUpsGizmos(PlayerAuthoring authoring)
    {
        PlayerPowerUpsPreset preset = authoring.GetPowerUpsPreset();

        if (preset == null)
            return;

        ActiveToolDefinition primaryTool = ResolveActiveToolById(preset, preset.PrimaryActiveToolId);
        ActiveToolDefinition secondaryTool = ResolveActiveToolById(preset, preset.SecondaryActiveToolId);
        ActiveToolDefinition bombTool = ResolveToolByKind(primaryTool, secondaryTool, ActiveToolKind.Bomb);
        ActiveToolDefinition dashTool = ResolveToolByKind(primaryTool, secondaryTool, ActiveToolKind.Dash);

        if (bombTool != null)
            DrawBombGizmo(authoring, bombTool);

        if (dashTool != null)
            DrawDashGizmo(authoring, dashTool);
    }

    private static void DrawBombGizmo(PlayerAuthoring authoring, ActiveToolDefinition bombTool)
    {
        BombToolData bombData = bombTool.BombData;

        if (bombData == null)
            return;

        Transform authoringTransform = authoring.transform;
        Vector3 origin = authoringTransform.position;
        Vector3 spawnPoint = authoringTransform.TransformPoint(bombData.SpawnOffset);
        float deploySpeed = Mathf.Max(0f, bombData.DeploySpeed);
        Vector3 throwEnd = spawnPoint + authoringTransform.forward * Mathf.Max(0.25f, deploySpeed * 0.2f);
        float radius = Mathf.Max(0.1f, bombData.Radius);
        Gizmos.color = PowerUpBombGizmoColor;
        Gizmos.DrawLine(origin, spawnPoint);
        Gizmos.DrawWireSphere(spawnPoint, 0.15f);
        Gizmos.DrawWireSphere(spawnPoint, radius);
        Gizmos.DrawLine(spawnPoint, throwEnd);
    }

    private static void DrawDashGizmo(PlayerAuthoring authoring, ActiveToolDefinition dashTool)
    {
        DashToolData dashData = dashTool.DashData;

        if (dashData == null)
            return;

        float distance = Mathf.Max(0f, dashData.Distance);

        if (distance <= 0f)
            return;

        Vector3 origin = authoring.transform.position + Vector3.up * 0.05f;
        Vector3 endPoint = origin + authoring.transform.forward * distance;
        Gizmos.color = PowerUpDashGizmoColor;
        Gizmos.DrawLine(origin, endPoint);
        Gizmos.DrawWireSphere(endPoint, 0.12f);
    }

    private static ActiveToolDefinition ResolveActiveToolById(PlayerPowerUpsPreset preset, string powerUpId)
    {
        if (preset == null || string.IsNullOrWhiteSpace(powerUpId))
            return null;

        IReadOnlyList<ActiveToolDefinition> activeTools = preset.ActiveTools;

        if (activeTools == null || activeTools.Count == 0)
            return null;

        for (int index = 0; index < activeTools.Count; index++)
        {
            ActiveToolDefinition activeTool = activeTools[index];

            if (activeTool == null)
                continue;

            PowerUpCommonData commonData = activeTool.CommonData;

            if (commonData == null)
                continue;

            if (!string.Equals(commonData.PowerUpId, powerUpId, StringComparison.OrdinalIgnoreCase))
                continue;

            return activeTool;
        }

        return null;
    }

    private static ActiveToolDefinition ResolveToolByKind(ActiveToolDefinition firstCandidate, ActiveToolDefinition secondCandidate, ActiveToolKind requestedKind)
    {
        if (firstCandidate != null && firstCandidate.ToolKind == requestedKind)
            return firstCandidate;

        if (secondCandidate != null && secondCandidate.ToolKind == requestedKind)
            return secondCandidate;

        return null;
    }
    #endregion

    #endregion
}
