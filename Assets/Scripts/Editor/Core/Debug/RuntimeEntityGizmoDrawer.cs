using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

/// <summary>
/// Draws runtime ECS gizmos inside the Scene view while the game is playing.
/// none.
/// returns none.
/// </summary>
[InitializeOnLoad]
public static class RuntimeEntityGizmoDrawer
{
    #region Constants
    private const float LabelVerticalOffset = 0.32f;
    private const float ArrowHeadSize = 0.18f;
    #endregion

    #region Fields
    private static readonly SceneViewPrimitiveDrawer primitiveDrawer = new SceneViewPrimitiveDrawer();
    private static GUIStyle labelStyle;
    #endregion

    #region Constructor
    static RuntimeEntityGizmoDrawer()
    {
        SceneView.duringSceneGui += HandleSceneViewGui;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        RuntimeGizmoDebugState.StateChanged += HandleDebugStateChanged;
    }
    #endregion

    #region Methods

    #region Events
    private static void HandleSceneViewGui(SceneView sceneView)
    {
        if (!Application.isPlaying)
            return;

        if (!RuntimeEntityGizmoRenderUtility.AnyRuntimeGizmoEnabled)
            return;

        EnsureLabelStyle();
        Handles.zTest = CompareFunction.LessEqual;
        primitiveDrawer.BindLabelStyle(labelStyle);
        RuntimeEntityGizmoRenderUtility.TryRender(primitiveDrawer);
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode ||
            state == PlayModeStateChange.ExitingPlayMode)
        {
            RuntimeEntityGizmoRenderUtility.ResetCachedContext();
        }
    }

    private static void HandleDebugStateChanged()
    {
        SceneView.RepaintAll();
    }
    #endregion

    #region Helpers
    private static void EnsureLabelStyle()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(EditorStyles.boldLabel);
        labelStyle.normal.textColor = new Color(0.94f, 0.97f, 1f, 0.94f);
        labelStyle.fontSize = 11;
    }
    #endregion

    #endregion

    #region Types
    /// <summary>
    /// Handles the Scene view backend used by the shared runtime gizmo rendering utility.
    /// none.
    /// returns none.
    /// </summary>
    private sealed class SceneViewPrimitiveDrawer : IRuntimeGizmoPrimitiveDrawer
    {
        #region Fields
        private GUIStyle currentLabelStyle;
        #endregion

        #region Methods

        #region Public Methods
        /// <summary>
        /// Stores the label style reused by subsequent label draw calls.
        /// style: Scene view GUI style used for labels.
        /// returns void.
        /// </summary>
        public void BindLabelStyle(GUIStyle style)
        {
            currentLabelStyle = style;
        }

        /// <summary>
        /// Draws one planar Scene view disc using Handles.
        /// center: World-space center of the disc.
        /// radius: Radius expressed in gameplay world units.
        /// color: Final Handles color.
        /// returns void.
        /// </summary>
        public void DrawWireDisc(Vector3 center, float radius, Color color)
        {
            if (radius <= 0f)
                return;

            Handles.color = color;
            Handles.DrawWireDisc(center, Vector3.up, radius);
        }

        /// <summary>
        /// Draws one world-space directional indicator inside the Scene view.
        /// origin: Vector origin in world space.
        /// direction: Direction expected to be safely normalizable.
        /// length: Vector length expressed in gameplay world units.
        /// color: Final Handles color.
        /// returns void.
        /// </summary>
        public void DrawDirection(Vector3 origin, Vector3 direction, float length, Color color)
        {
            if (length <= 0f)
                return;

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Vector3 normalizedDirection = direction.normalized;
            Vector3 end = origin + normalizedDirection * length;
            Quaternion arrowRotation = Quaternion.LookRotation(normalizedDirection, Vector3.up);

            Handles.color = color;
            Handles.DrawLine(origin, end);
            Handles.ConeHandleCap(0, end, arrowRotation, ArrowHeadSize, EventType.Repaint);
        }

        /// <summary>
        /// Draws one straight Scene view link between two positions.
        /// start: Link starting point in world space.
        /// end: Link end point in world space.
        /// color: Final Handles color.
        /// returns void.
        /// </summary>
        public void DrawLink(Vector3 start, Vector3 end, Color color)
        {
            Handles.color = color;
            Handles.DrawLine(start, end);
        }

        /// <summary>
        /// Draws one compact marker in the Scene view.
        /// position: Marker position in world space.
        /// radius: Marker size expressed in gameplay world units.
        /// color: Final Handles color.
        /// returns void.
        /// </summary>
        public void DrawMarker(Vector3 position, float radius, Color color)
        {
            float resolvedRadius = Mathf.Max(0.03f, radius);
            Handles.color = color;
            Handles.DrawWireDisc(position, Vector3.up, resolvedRadius);
        }

        /// <summary>
        /// Draws one Scene view label slightly above the target world position.
        /// position: World-space label anchor.
        /// text: Text shown in the Scene view.
        /// returns void.
        /// </summary>
        public void DrawLabel(Vector3 position, string text)
        {
            if (currentLabelStyle == null)
                return;

            Vector3 labelPosition = position + Vector3.up * LabelVerticalOffset;
            Handles.Label(labelPosition, text, currentLabelStyle);
        }
        #endregion

        #endregion
    }
    #endregion
}
