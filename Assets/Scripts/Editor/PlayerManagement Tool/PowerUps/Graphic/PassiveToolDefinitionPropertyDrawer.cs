using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(PassiveToolDefinition))]
public sealed class PassiveToolDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private static readonly Color SplitUniformColorA = new Color(0.20f, 0.62f, 0.88f, 0.85f);
    private static readonly Color SplitUniformColorB = new Color(0.12f, 0.42f, 0.72f, 0.85f);
    private static readonly Color SplitCustomColor = new Color(0.96f, 0.56f, 0.18f, 0.88f);
    private static readonly Color SplitBackgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.55f);
    #endregion

    #region Fields
    private static readonly List<PassiveToolKind> SupportedKinds = new List<PassiveToolKind>
    {
        PassiveToolKind.ProjectileSize,
        PassiveToolKind.ElementalProjectiles,
        PassiveToolKind.PerfectCircle,
        PassiveToolKind.BouncingProjectiles,
        PassiveToolKind.SplittingProjectiles,
        PassiveToolKind.Explosion,
        PassiveToolKind.ElementalTrail
    };
    #endregion

    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty commonDataProperty = property.FindPropertyRelative("commonData");
        SerializedProperty toolKindProperty = property.FindPropertyRelative("toolKind");

        if (commonDataProperty == null ||
            toolKindProperty == null)
        {
            Label errorLabel = new Label("Passive tool data is missing serialized fields.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        PassiveToolKind initialKind = SanitizeKind(toolKindProperty);
        PopupField<PassiveToolKind> toolKindField = new PopupField<PassiveToolKind>("Tool Type", SupportedKinds, initialKind);
        toolKindField.formatListItemCallback = FormatToolKind;
        toolKindField.formatSelectedValueCallback = FormatToolKind;

        VisualElement toolSpecificContainer = new VisualElement();
        toolSpecificContainer.style.marginTop = 2f;

        AddField(root, commonDataProperty, "Common Data");
        root.Add(toolKindField);

        Label toolSpecificLabel = new Label("Tool Specific");
        toolSpecificLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toolSpecificLabel.style.marginTop = 4f;
        root.Add(toolSpecificLabel);
        root.Add(toolSpecificContainer);

        SerializedObject serializedObject = property.serializedObject;
        string rootPropertyPath = property.propertyPath;

        toolKindField.RegisterValueChangedCallback(evt =>
        {
            SerializedProperty resolvedRootProperty = TryResolveRootProperty(serializedObject, rootPropertyPath);

            if (resolvedRootProperty == null)
                return;

            SerializedProperty resolvedToolKindProperty = resolvedRootProperty.FindPropertyRelative("toolKind");
            SetToolKind(resolvedToolKindProperty, evt.newValue);
            RefreshToolSpecific(toolSpecificContainer, resolvedRootProperty);
        });

        root.TrackPropertyValue(toolKindProperty, changedProperty =>
        {
            SerializedProperty resolvedRootProperty = TryResolveRootProperty(changedProperty.serializedObject, rootPropertyPath);

            if (resolvedRootProperty == null)
                return;

            SerializedProperty resolvedToolKindProperty = resolvedRootProperty.FindPropertyRelative("toolKind");

            if (resolvedToolKindProperty == null)
                return;

            PassiveToolKind selectedKind = SanitizeKind(resolvedToolKindProperty);

            if (toolKindField.value != selectedKind)
                toolKindField.SetValueWithoutNotify(selectedKind);

            RefreshToolSpecific(toolSpecificContainer, resolvedRootProperty);
        });

        RefreshToolSpecific(toolSpecificContainer, property);
        return root;
    }

    private static void AddField(VisualElement parent, SerializedProperty property, string labelOverride = null)
    {
        if (parent == null || property == null)
            return;

        PropertyField field = string.IsNullOrWhiteSpace(labelOverride)
            ? new PropertyField(property)
            : new PropertyField(property, labelOverride);
        field.BindProperty(property);
        parent.Add(field);
    }

    private static string FormatToolKind(PassiveToolKind toolKind)
    {
        switch (toolKind)
        {
            case PassiveToolKind.ProjectileSize:
                return "Projectile Size";
            case PassiveToolKind.ElementalProjectiles:
                return "Elemental Projectiles";
            case PassiveToolKind.PerfectCircle:
                return "Perfect Circle";
            case PassiveToolKind.BouncingProjectiles:
                return "Bouncing Projectiles";
            case PassiveToolKind.SplittingProjectiles:
                return "Splitting Projectiles";
            case PassiveToolKind.Explosion:
                return "Explosion";
            case PassiveToolKind.ElementalTrail:
                return "Elemental Trail";
            default:
                return "Projectile Size";
        }
    }

    private static PassiveToolKind SanitizeKind(SerializedProperty toolKindProperty)
    {
        if (toolKindProperty == null)
            return PassiveToolKind.ProjectileSize;

        PassiveToolKind currentKind = (PassiveToolKind)toolKindProperty.enumValueIndex;

        if (SupportedKinds.Contains(currentKind))
            return currentKind;

        toolKindProperty.serializedObject.Update();
        toolKindProperty.enumValueIndex = (int)PassiveToolKind.ProjectileSize;
        toolKindProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return PassiveToolKind.ProjectileSize;
    }

    private static void SetToolKind(SerializedProperty toolKindProperty, PassiveToolKind selectedKind)
    {
        if (toolKindProperty == null)
            return;

        PassiveToolKind sanitizedKind = selectedKind;

        if (SupportedKinds.Contains(sanitizedKind) == false)
            sanitizedKind = PassiveToolKind.ProjectileSize;

        if (toolKindProperty.enumValueIndex == (int)sanitizedKind)
            return;

        toolKindProperty.serializedObject.Update();
        toolKindProperty.enumValueIndex = (int)sanitizedKind;
        toolKindProperty.serializedObject.ApplyModifiedProperties();
    }

    private static SerializedProperty TryResolveRootProperty(SerializedObject serializedObject, string propertyPath)
    {
        if (serializedObject == null)
            return null;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return null;

        return serializedObject.FindProperty(propertyPath);
    }

    private static void RefreshToolSpecific(VisualElement container, SerializedProperty rootProperty)
    {
        if (container == null || rootProperty == null)
            return;

        container.Clear();
        SerializedProperty toolKindProperty = rootProperty.FindPropertyRelative("toolKind");
        SerializedProperty projectileSizeDataProperty = rootProperty.FindPropertyRelative("projectileSizeData");
        SerializedProperty elementalProjectilesDataProperty = rootProperty.FindPropertyRelative("elementalProjectilesData");
        SerializedProperty perfectCircleDataProperty = rootProperty.FindPropertyRelative("perfectCircleData");
        SerializedProperty bouncingProjectilesDataProperty = rootProperty.FindPropertyRelative("bouncingProjectilesData");
        SerializedProperty splittingProjectilesDataProperty = rootProperty.FindPropertyRelative("splittingProjectilesData");
        SerializedProperty explosionDataProperty = rootProperty.FindPropertyRelative("explosionData");
        SerializedProperty elementalTrailDataProperty = rootProperty.FindPropertyRelative("elementalTrailData");

        if (toolKindProperty == null ||
            projectileSizeDataProperty == null ||
            elementalProjectilesDataProperty == null ||
            perfectCircleDataProperty == null ||
            bouncingProjectilesDataProperty == null ||
            splittingProjectilesDataProperty == null ||
            explosionDataProperty == null ||
            elementalTrailDataProperty == null)
        {
            Label errorLabel = new Label("Tool specific data is missing serialized fields.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            container.Add(errorLabel);
            return;
        }

        PassiveToolKind selectedKind = SanitizeKind(toolKindProperty);

        switch (selectedKind)
        {
            case PassiveToolKind.ProjectileSize:
                AddField(container, projectileSizeDataProperty, "Projectile Size Settings");
                return;
            case PassiveToolKind.ElementalProjectiles:
                AddField(container, elementalProjectilesDataProperty, "Elemental Projectiles Settings");
                return;
            case PassiveToolKind.PerfectCircle:
                AddField(container, perfectCircleDataProperty, "Perfect Circle Settings");
                return;
            case PassiveToolKind.BouncingProjectiles:
                AddField(container, bouncingProjectilesDataProperty, "Bouncing Projectiles Settings");
                return;
            case PassiveToolKind.SplittingProjectiles:
                BuildSplittingToolUI(container, splittingProjectilesDataProperty);
                return;
            case PassiveToolKind.Explosion:
                AddField(container, explosionDataProperty, "Explosion Settings");
                return;
            case PassiveToolKind.ElementalTrail:
                AddField(container, elementalTrailDataProperty, "Elemental Trail Settings");
                return;
        }
    }

    private static void BuildSplittingToolUI(VisualElement container, SerializedProperty splittingProjectilesDataProperty)
    {
        if (container == null || splittingProjectilesDataProperty == null)
            return;

        PropertyField splitField = new PropertyField(splittingProjectilesDataProperty, "Splitting Projectiles Settings");
        splitField.BindProperty(splittingProjectilesDataProperty);
        container.Add(splitField);

        SerializedProperty directionModeProperty = splittingProjectilesDataProperty.FindPropertyRelative("directionMode");
        SerializedProperty splitProjectileCountProperty = splittingProjectilesDataProperty.FindPropertyRelative("splitProjectileCount");
        SerializedProperty splitOffsetDegreesProperty = splittingProjectilesDataProperty.FindPropertyRelative("splitOffsetDegrees");
        SerializedProperty customAnglesDegreesProperty = splittingProjectilesDataProperty.FindPropertyRelative("customAnglesDegrees");

        if (directionModeProperty == null ||
            splitProjectileCountProperty == null ||
            splitOffsetDegreesProperty == null ||
            customAnglesDegreesProperty == null)
            return;

        Label chartLabel = new Label("Split Direction Pie");
        chartLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        chartLabel.style.marginTop = 4f;
        container.Add(chartLabel);

        PieChartElement pieChart = new PieChartElement();
        pieChart.style.minHeight = 240f;
        pieChart.style.marginTop = 4f;
        pieChart.style.marginBottom = 8f;
        pieChart.SetZoom(0.95f);
        container.Add(pieChart);

        splitField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            UpdateSplitPieChart(pieChart,
                                directionModeProperty,
                                splitProjectileCountProperty,
                                splitOffsetDegreesProperty,
                                customAnglesDegreesProperty);
        });

        container.TrackPropertyValue(directionModeProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                changedProperty,
                                splitProjectileCountProperty,
                                splitOffsetDegreesProperty,
                                customAnglesDegreesProperty);
        });

        container.TrackPropertyValue(splitProjectileCountProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                directionModeProperty,
                                changedProperty,
                                splitOffsetDegreesProperty,
                                customAnglesDegreesProperty);
        });

        container.TrackPropertyValue(splitOffsetDegreesProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                directionModeProperty,
                                splitProjectileCountProperty,
                                changedProperty,
                                customAnglesDegreesProperty);
        });

        container.TrackPropertyValue(customAnglesDegreesProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                directionModeProperty,
                                splitProjectileCountProperty,
                                splitOffsetDegreesProperty,
                                changedProperty);
        });

        UpdateSplitPieChart(pieChart,
                            directionModeProperty,
                            splitProjectileCountProperty,
                            splitOffsetDegreesProperty,
                            customAnglesDegreesProperty);
    }

    private static void UpdateSplitPieChart(PieChartElement pieChart,
                                            SerializedProperty directionModeProperty,
                                            SerializedProperty splitProjectileCountProperty,
                                            SerializedProperty splitOffsetDegreesProperty,
                                            SerializedProperty customAnglesDegreesProperty)
    {
        if (pieChart == null ||
            directionModeProperty == null ||
            splitProjectileCountProperty == null ||
            splitOffsetDegreesProperty == null ||
            customAnglesDegreesProperty == null)
            return;

        ProjectileSplitDirectionMode directionMode = (ProjectileSplitDirectionMode)directionModeProperty.enumValueIndex;
        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();
        List<float> directionMarkers = new List<float>();
        List<PieChartElement.LabelDescriptor> labels = new List<PieChartElement.LabelDescriptor>();

        switch (directionMode)
        {
            case ProjectileSplitDirectionMode.Uniform:
                BuildUniformSplitSlices(ref slices,
                                        ref directionMarkers,
                                        ref labels,
                                        splitProjectileCountProperty.intValue,
                                        splitOffsetDegreesProperty.floatValue);
                break;
            case ProjectileSplitDirectionMode.CustomAngles:
                BuildCustomSplitSlices(ref slices,
                                       ref directionMarkers,
                                       ref labels,
                                       customAnglesDegreesProperty,
                                       splitOffsetDegreesProperty.floatValue);
                break;
        }

        if (slices.Count == 0)
        {
            slices.Add(new PieChartElement.PieSlice
            {
                StartAngle = 0f,
                EndAngle = 360f,
                MidAngle = 180f,
                Color = SplitBackgroundColor
            });
        }

        pieChart.SetSlices(slices);
        pieChart.SetDirectionMarkers(directionMarkers,
                                     new Color(0.95f, 0.95f, 0.95f, 0.85f),
                                     new Color(1f, 0.9f, 0.3f, 1f),
                                     0f,
                                     true);
        pieChart.SetSegmentLabels(labels);
        pieChart.SetOverlayFields(null);
    }

    private static void BuildUniformSplitSlices(ref List<PieChartElement.PieSlice> slices,
                                                ref List<float> directionMarkers,
                                                ref List<PieChartElement.LabelDescriptor> labels,
                                                int splitCountInput,
                                                float splitOffsetDegrees)
    {
        int splitCount = Mathf.Max(1, splitCountInput);
        float step = 360f / splitCount;

        for (int splitIndex = 0; splitIndex < splitCount; splitIndex++)
        {
            float start = NormalizeAngle(splitOffsetDegrees + step * splitIndex);
            float end = NormalizeAngle(start + step);
            float mid = NormalizeAngle(start + step * 0.5f);
            AddNormalizedSlice(ref slices,
                               start,
                               end,
                               splitIndex % 2 == 0 ? SplitUniformColorA : SplitUniformColorB);
            directionMarkers.Add(mid);

            PieChartElement.LabelDescriptor label = new PieChartElement.LabelDescriptor
            {
                Angle = mid,
                Text = (splitIndex + 1).ToString(),
                RadiusOffset = -12f,
                TextColor = Color.white,
                UseTextColor = true
            };
            labels.Add(label);
        }
    }

    private static void BuildCustomSplitSlices(ref List<PieChartElement.PieSlice> slices,
                                               ref List<float> directionMarkers,
                                               ref List<PieChartElement.LabelDescriptor> labels,
                                               SerializedProperty customAnglesDegreesProperty,
                                               float splitOffsetDegrees)
    {
        int angleCount = customAnglesDegreesProperty.arraySize;

        if (angleCount <= 0)
            return;

        for (int angleIndex = 0; angleIndex < angleCount; angleIndex++)
        {
            SerializedProperty angleProperty = customAnglesDegreesProperty.GetArrayElementAtIndex(angleIndex);

            if (angleProperty == null)
                continue;

            float angle = NormalizeAngle(angleProperty.floatValue + splitOffsetDegrees);
            float halfWidth = 8f;
            float start = NormalizeAngle(angle - halfWidth);
            float end = NormalizeAngle(angle + halfWidth);
            AddNormalizedSlice(ref slices, start, end, SplitCustomColor);
            directionMarkers.Add(angle);

            PieChartElement.LabelDescriptor label = new PieChartElement.LabelDescriptor
            {
                Angle = angle,
                Text = (angleIndex + 1).ToString(),
                RadiusOffset = -8f,
                TextColor = Color.white,
                UseTextColor = true
            };
            labels.Add(label);
        }
    }

    private static void AddNormalizedSlice(ref List<PieChartElement.PieSlice> slices,
                                           float startAngle,
                                           float endAngle,
                                           Color color)
    {
        float normalizedStart = NormalizeAngle(startAngle);
        float normalizedEnd = NormalizeAngle(endAngle);

        if (Mathf.Approximately(normalizedStart, normalizedEnd))
        {
            slices.Add(new PieChartElement.PieSlice
            {
                StartAngle = 0f,
                EndAngle = 360f,
                MidAngle = 180f,
                Color = color
            });
            return;
        }

        if (normalizedEnd > normalizedStart)
        {
            slices.Add(new PieChartElement.PieSlice
            {
                StartAngle = normalizedStart,
                EndAngle = normalizedEnd,
                MidAngle = NormalizeAngle((normalizedStart + normalizedEnd) * 0.5f),
                Color = color
            });
            return;
        }

        slices.Add(new PieChartElement.PieSlice
        {
            StartAngle = normalizedStart,
            EndAngle = 360f,
            MidAngle = NormalizeAngle((normalizedStart + 360f) * 0.5f),
            Color = color
        });

        slices.Add(new PieChartElement.PieSlice
        {
            StartAngle = 0f,
            EndAngle = normalizedEnd,
            MidAngle = NormalizeAngle(normalizedEnd * 0.5f),
            Color = color
        });
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle, 360f);
    }
    #endregion
}
