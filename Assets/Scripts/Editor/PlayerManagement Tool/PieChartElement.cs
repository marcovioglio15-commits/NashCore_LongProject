using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Represents a visual element for displaying a pie chart with configurable slices, direction markers, and overlay
/// fields.
/// </summary>
public sealed class PieChartElement : VisualElement
{
    #region Nested Types
    /// <summary>
    /// Represents a slice of a pie chart, defined by its start angle, end angle, midpoint angle, and color.
    /// </summary>
    public struct PieSlice
    {
        public float StartAngle;
        public float EndAngle;
        public float MidAngle;
        public Color Color;
    }

    /// <summary>
    /// Represents a descriptor for an overlay, containing angle, property, and tooltip information.
    /// </summary>
    public sealed class OverlayDescriptor
    {
        public float Angle;
        public SerializedProperty Property;
        public string Tooltip;
        public float RadiusOffset;
        public Color FieldColor;
        public bool UseFieldColor;
        public bool DisplayAsPercent;
    }

    /// <summary>
    /// Represents a binding between a float angle, a FloatField, and a SerializedProperty for overlay operations.
    /// </summary>
    private sealed class OverlayBinding
    {
        public float Angle;
        public FloatField Field;
        public SerializedProperty Property;
        public float RadiusOffset;
        public bool DisplayAsPercent;
    }
    #endregion

    #region Constants
    private const float FieldWidth = 46f;
    private const float FieldHeight = 16f;
    private const float ForwardLabelSize = 14f;
    private const float ForwardLabelPadding = 2f;
    private const float DirectionLineWidth = 2f;
    private const float DirectionLineRadiusFactor = 0.9f;
    private const float PieRadiusFactor = 0.48f;
    private const float OverlayRadiusFactor = 0.34f;
    #endregion

    #region Fields
    // Internal collections and state for managing pie slices, overlay bindings, and direction markers.
    private readonly List<PieSlice> m_Slices = new List<PieSlice>();
    private readonly List<OverlayBinding> m_OverlayBindings = new List<OverlayBinding>();
    private readonly List<float> m_DirectionAngles = new List<float>();
    private readonly VisualElement m_OverlayRoot;
    private readonly Label m_ForwardLabel;
    private Color m_DirectionColor = new Color(1f, 1f, 1f, 0.9f);
    private Color m_ForwardColor = new Color(1f, 0.9f, 0.2f, 1f);
    private float m_ForwardAngle;
    private bool m_ShowForwardLabel;
    private float m_Zoom = 1f;
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the PieChartElement class, setting up layout, overlay elements, and event
    /// handlers.
    /// </summary>
    public PieChartElement()
    {
        style.flexGrow = 1f;
        style.minHeight = 220f;
        style.minWidth = 220f;
        style.marginTop = 6f;
        style.marginBottom = 6f;

        m_OverlayRoot = new VisualElement();
        m_OverlayRoot.style.position = Position.Absolute;
        m_OverlayRoot.style.left = 0f;
        m_OverlayRoot.style.top = 0f;
        m_OverlayRoot.style.right = 0f;
        m_OverlayRoot.style.bottom = 0f;

        m_ForwardLabel = new Label("F");
        m_ForwardLabel.style.position = Position.Absolute;
        m_ForwardLabel.style.width = ForwardLabelSize;
        m_ForwardLabel.style.height = ForwardLabelSize;
        m_ForwardLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        m_ForwardLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        m_ForwardLabel.style.fontSize = 10f;
        m_ForwardLabel.style.display = DisplayStyle.None;
        m_ForwardLabel.pickingMode = PickingMode.Ignore;
        m_OverlayRoot.Add(m_ForwardLabel);

        Add(m_OverlayRoot);

        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Replaces the current collection of pie slices with the specified list and updates the layout.
    /// </summary>
    /// <param name="slices">The new list of PieSlice objects to set.</param>
    public void SetSlices(List<PieSlice> slices)
    {
        m_Slices.Clear();

        if (slices != null)
            m_Slices.AddRange(slices);

        MarkDirtyRepaint();
        UpdateOverlayLayout();
    }

    /// <summary>
    /// Configures overlay fields based on the provided list of overlay descriptors.
    /// </summary>
    /// <param name="descriptors">A list of OverlayDescriptor objects used to create and bind overlay fields.</param>
    public void SetOverlayFields(List<OverlayDescriptor> descriptors)
    {
        m_OverlayRoot.Clear();
        m_OverlayBindings.Clear();
        m_OverlayRoot.Add(m_ForwardLabel);

        if (descriptors == null)
            return;

        for (int i = 0; i < descriptors.Count; i++)
        {
            OverlayDescriptor descriptor = descriptors[i];

            if (descriptor == null)
                continue;

            FloatField field = new FloatField();
            field.label = string.Empty;
            field.style.position = Position.Absolute;
            field.style.width = FieldWidth;
            field.style.height = FieldHeight;
            field.style.fontSize = 10f;
            field.tooltip = descriptor.Tooltip;
            field.isDelayed = true;
            field.style.unityTextAlign = TextAnchor.MiddleCenter;

            if (descriptor.UseFieldColor)
                field.style.color = descriptor.FieldColor;

            if (descriptor.Property != null)
            {
                float value = descriptor.Property.floatValue;
                field.value = descriptor.DisplayAsPercent ? value * 100f : value;

                field.RegisterValueChangedCallback(evt =>
                {
                    if (descriptor.Property == null)
                        return;

                    float inputValue = evt.newValue;
                    float storedValue = descriptor.DisplayAsPercent ? Mathf.Clamp(inputValue / 100f, 0f, 1f) : inputValue;
                    descriptor.Property.floatValue = storedValue;
                    descriptor.Property.serializedObject.ApplyModifiedProperties();

                    if (descriptor.DisplayAsPercent)
                        field.SetValueWithoutNotify(storedValue * 100f);
                });
            }

            OverlayBinding binding = new OverlayBinding
            {
                Angle = descriptor.Angle,
                Field = field,
                Property = descriptor.Property,
                RadiusOffset = descriptor.RadiusOffset,
                DisplayAsPercent = descriptor.DisplayAsPercent
            };

            m_OverlayBindings.Add(binding);
            m_OverlayRoot.Add(field);
        }

        UpdateOverlayLayout();
    }

    /// <summary>
    /// Configures the direction markers with specified angles, colors, forward angle, and label visibility.
    /// </summary>
    /// <param name="directionAngles">A list of angles representing the directions to display.</param>
    /// <param name="directionColor">The color used for the direction markers.</param>
    /// <param name="forwardColor">The color used for the forward direction marker and label.</param>
    /// <param name="forwardAngle">The angle representing the forward direction.</param>
    /// <param name="showForwardLabel">Indicates whether the forward label should be shown.</param>
    public void SetDirectionMarkers(List<float> directionAngles, Color directionColor, Color forwardColor, float forwardAngle, bool showForwardLabel)
    {
        m_DirectionAngles.Clear();

        if (directionAngles != null)
            m_DirectionAngles.AddRange(directionAngles);

        m_DirectionColor = directionColor;
        m_ForwardColor = forwardColor;
        m_ForwardAngle = forwardAngle;
        m_ShowForwardLabel = showForwardLabel;

        if (m_ForwardLabel != null)
            m_ForwardLabel.style.color = m_ForwardColor;

        UpdateForwardLabelVisibility();
        MarkDirtyRepaint();
        UpdateOverlayLayout();
    }

    /// <summary>
    /// Sets the zoom factor for the pie chart and overlay layout.
    /// </summary>
    /// <param name="zoom">Zoom multiplier applied to the pie and overlay radii.</param>
    public void SetZoom(float zoom)
    {
        m_Zoom = Mathf.Max(0.1f, zoom);
        MarkDirtyRepaint();
        UpdateOverlayLayout();
    }
    #endregion

    #region VisualElement Rendering
    /// <summary>
    /// Renders pie slices and directional lines within the visual element using the provided mesh generation context.
    /// </summary>
    /// <param name="mgc">The mesh generation context used for drawing visual content.</param>
    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        Rect rect = contentRect;

        if (rect.width <= 0f || rect.height <= 0f)
            return;

        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * PieRadiusFactor * m_Zoom;

        Painter2D painter = mgc.painter2D;
        painter.lineWidth = 1f;

        for (int i = 0; i < m_Slices.Count; i++)
        {
            PieSlice slice = m_Slices[i];
            painter.fillColor = slice.Color;
            painter.strokeColor = new Color(0f, 0f, 0f, 0.35f);

            painter.BeginPath();
            painter.MoveTo(center);
            painter.Arc(center, radius, slice.StartAngle, slice.EndAngle);
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        painter.strokeColor = new Color(0f, 0f, 0f, 0.45f);
        painter.BeginPath();
        painter.Arc(center, radius, 0f, 360f);
        painter.ClosePath();
        painter.Stroke();

        if (m_DirectionAngles.Count > 0)
        {
            painter.lineWidth = DirectionLineWidth;

            for (int i = 0; i < m_DirectionAngles.Count; i++)
            {
                float angle = m_DirectionAngles[i];
                float radians = (angle - 90f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 end = center + (dir * radius * DirectionLineRadiusFactor);

                painter.strokeColor = IsForwardAngle(angle) ? m_ForwardColor : m_DirectionColor;
                painter.BeginPath();
                painter.MoveTo(center);
                painter.LineTo(end);
                painter.Stroke();
            }
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Handles geometry change events by updating the overlay layout.
    /// </summary>
    /// <param name="evt">The geometry changed event data.</param>
    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        UpdateOverlayLayout();
    }

    /// <summary>
    /// Arranges overlay fields in a circular layout around the content rectangle and updates their positions and
    /// values.
    /// </summary>
    private void UpdateOverlayLayout()
    {
        Rect rect = contentRect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * OverlayRadiusFactor * m_Zoom;
        float pieRadius = Mathf.Min(rect.width, rect.height) * PieRadiusFactor * m_Zoom;

        if (m_OverlayBindings.Count > 0)
        {
            for (int i = 0; i < m_OverlayBindings.Count; i++)
            {
                OverlayBinding binding = m_OverlayBindings[i];
                FloatField field = binding.Field;

                if (field == null)
                    continue;

                float radians = (binding.Angle - 90f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                float adjustedRadius = radius + binding.RadiusOffset;
                Vector2 position = center + (dir * adjustedRadius);

                field.style.left = position.x - (FieldWidth * 0.5f);
                field.style.top = position.y - (FieldHeight * 0.5f);

                if (binding.Property != null)
                {
                    float value = binding.Property.floatValue;
                    field.SetValueWithoutNotify(binding.DisplayAsPercent ? value * 100f : value);
                }
            }
        }

        UpdateForwardLabelPosition(center, pieRadius);
    }

    /// <summary>
    /// Updates the visibility of the forward label based on the m_ShowForwardLabel flag.
    /// </summary>
    private void UpdateForwardLabelVisibility()
    {
        if (m_ForwardLabel == null)
            return;

        m_ForwardLabel.style.display = m_ShowForwardLabel ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Positions the forward label at a calculated point around a center based on a specified radius and angle.
    /// </summary>
    /// <param name="center">The center point from which to position the forward label.</param>
    /// <param name="radius">The radius distance from the center to place the forward label.</param>
    private void UpdateForwardLabelPosition(Vector2 center, float radius)
    {
        if (m_ShowForwardLabel == false || m_ForwardLabel == null)
            return;

        float radians = (m_ForwardAngle - 90f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        float labelRadius = radius + (ForwardLabelSize * 0.5f) + ForwardLabelPadding;
        Vector2 position = center + (dir * labelRadius);

        m_ForwardLabel.style.left = position.x - (ForwardLabelSize * 0.5f);
        m_ForwardLabel.style.top = position.y - (ForwardLabelSize * 0.5f);
    }

    /// <summary>
    /// Determines whether the specified angle is within 0.5 degrees of the forward angle.
    /// </summary>
    /// <param name="angle">The angle to compare, in degrees.</param>
    /// <returns>True if the angle is within 0.5 degrees of the forward angle; otherwise, false.</returns>
    private bool IsForwardAngle(float angle)
    {
        return Mathf.Abs(Mathf.DeltaAngle(angle, m_ForwardAngle)) < 0.5f;
    }
    #endregion
}
