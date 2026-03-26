using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the look section UI for player controller preset panels and wires its live visualization callbacks.
/// </summary>
internal static class PlayerControllerPresetsPanelLookSectionUtility
{
    #region Constants
    private const float MultiplierRowSpacing = 2f;
    private const float SubSectionMarginLeft = 5f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the full look section inside the provided container and wires all property-driven refresh callbacks.
    /// </summary>
    /// <param name="panel">Owning panel that provides serialized context and helper utilities.</param>
    /// <param name="section">Pre-created section container that receives the look UI.</param>

    public static void BuildLookSection(PlayerControllerPresetsPanel panel, VisualElement section)
    {
        if (panel == null || section == null)
            return;

        SerializedProperty lookProperty = panel.PresetSerializedObject.FindProperty("lookSettings");

        if (lookProperty == null)
            return;

        SerializedProperty directionsModeProperty = lookProperty.FindPropertyRelative("m_DirectionsMode");
        SerializedProperty countProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionCount");
        SerializedProperty offsetProperty = lookProperty.FindPropertyRelative("m_DirectionOffsetDegrees");
        SerializedProperty rotationModeProperty = lookProperty.FindPropertyRelative("m_RotationMode");
        SerializedProperty rotationSpeedProperty = lookProperty.FindPropertyRelative("m_RotationSpeed");
        SerializedProperty samplingProperty = lookProperty.FindPropertyRelative("m_MultiplierSampling");
        SerializedProperty maxSpeedMultipliersProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionMaxSpeedMultipliers");
        SerializedProperty accelerationMultipliersProperty = lookProperty.FindPropertyRelative("m_DiscreteDirectionAccelerationMultipliers");
        SerializedProperty scalingRulesProperty = panel.PresetSerializedObject.FindProperty("scalingRules");

        SerializedProperty frontEnabledProperty = lookProperty.FindPropertyRelative("m_FrontConeEnabled");
        SerializedProperty frontAngleProperty = lookProperty.FindPropertyRelative("m_FrontConeAngle");
        SerializedProperty frontMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_FrontConeMaxSpeedMultiplier");
        SerializedProperty frontAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_FrontConeAccelerationMultiplier");
        SerializedProperty backEnabledProperty = lookProperty.FindPropertyRelative("m_BackConeEnabled");
        SerializedProperty backAngleProperty = lookProperty.FindPropertyRelative("m_BackConeAngle");
        SerializedProperty backMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_BackConeMaxSpeedMultiplier");
        SerializedProperty backAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_BackConeAccelerationMultiplier");
        SerializedProperty leftEnabledProperty = lookProperty.FindPropertyRelative("m_LeftConeEnabled");
        SerializedProperty leftAngleProperty = lookProperty.FindPropertyRelative("m_LeftConeAngle");
        SerializedProperty leftMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_LeftConeMaxSpeedMultiplier");
        SerializedProperty leftAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_LeftConeAccelerationMultiplier");
        SerializedProperty rightEnabledProperty = lookProperty.FindPropertyRelative("m_RightConeEnabled");
        SerializedProperty rightAngleProperty = lookProperty.FindPropertyRelative("m_RightConeAngle");
        SerializedProperty rightMaxSpeedMultiplierProperty = lookProperty.FindPropertyRelative("m_RightConeMaxSpeedMultiplier");
        SerializedProperty rightAccelerationMultiplierProperty = lookProperty.FindPropertyRelative("m_RightConeAccelerationMultiplier");

        EnumField directionsModeField = new EnumField("Allowed Directions");
        directionsModeField.BindProperty(directionsModeProperty);
        section.Add(directionsModeField);

        VisualElement discreteContainer = new VisualElement();
        discreteContainer.style.marginLeft = 8f;

        VisualElement countFieldElement = PlayerScalingFieldElementFactory.CreateField(countProperty, scalingRulesProperty, "Direction Count");
        discreteContainer.Add(countFieldElement);
        VisualElement offsetFieldElement = PlayerScalingFieldElementFactory.CreateField(offsetProperty, scalingRulesProperty, "Direction Offset");
        discreteContainer.Add(offsetFieldElement);

        VisualElement discreteWarningsRoot = new VisualElement();
        discreteWarningsRoot.style.marginTop = 4f;
        discreteContainer.Add(discreteWarningsRoot);

        VisualElement conesContainer = new VisualElement();
        conesContainer.style.marginLeft = 8f;
        conesContainer.style.marginTop = 4f;

        List<Toggle> coneToggles = new List<Toggle>();
        List<FloatField> coneAngleFields = new List<FloatField>();
        conesContainer.Add(BuildConeRow("Front", frontEnabledProperty, frontAngleProperty, frontMaxSpeedMultiplierProperty, frontAccelerationMultiplierProperty, coneToggles, coneAngleFields));
        conesContainer.Add(BuildConeRow("Back", backEnabledProperty, backAngleProperty, backMaxSpeedMultiplierProperty, backAccelerationMultiplierProperty, coneToggles, coneAngleFields));
        conesContainer.Add(BuildConeRow("Left", leftEnabledProperty, leftAngleProperty, leftMaxSpeedMultiplierProperty, leftAccelerationMultiplierProperty, coneToggles, coneAngleFields));
        conesContainer.Add(BuildConeRow("Right", rightEnabledProperty, rightAngleProperty, rightMaxSpeedMultiplierProperty, rightAccelerationMultiplierProperty, coneToggles, coneAngleFields));

        PieChartElement pieChart = new PieChartElement();
        Slider lookZoomSlider = PlayerControllerPresetsPanelFieldUtility.CreatePieZoomSlider(pieChart);
        VisualElement multipliersSection = PlayerControllerPresetsPanelVisualizationUtility.BuildDiscreteMultipliersSection(out VisualElement multipliersTableRoot, out Label multipliersHeader);

        section.Add(discreteContainer);
        section.Add(conesContainer);
        section.Add(pieChart);
        section.Add(lookZoomSlider);
        section.Add(multipliersSection);

        EnumField rotationModeField = new EnumField("Rotation Mode");
        rotationModeField.BindProperty(rotationModeProperty);
        section.Add(rotationModeField);

        VisualElement rotationSpeedField = PlayerScalingFieldElementFactory.CreateField(rotationSpeedProperty, scalingRulesProperty, "Rotation Speed");
        section.Add(rotationSpeedField);

        EnumField samplingField = new EnumField("Multiplier Sampling");
        samplingField.BindProperty(samplingProperty);
        section.Add(samplingField);

        SerializedProperty lookActionProperty = panel.PresetSerializedObject.FindProperty("lookActionId");
        PlayerControllerPresetsPanelFieldUtility.EnsureDefaultActionId(panel, lookActionProperty, "Look");

        Foldout bindingsFoldout = PlayerControllerPresetsPanelFieldUtility.BuildBindingsFoldout(panel.InputAsset,
                                                                                                panel.PresetSerializedObject,
                                                                                                lookActionProperty,
                                                                                                InputActionSelectionElement.SelectionMode.Look);
        section.Add(bindingsFoldout);

        SerializedProperty valuesProperty = lookProperty.FindPropertyRelative("m_Values");
        Foldout valuesFoldout = PlayerControllerPresetsPanelFieldUtility.BuildValuesFoldout(valuesProperty,
                                                                                            scalingRulesProperty,
                                                                                            new string[]
        {
            "m_RotationDamping",
            "m_RotationMaxSpeed",
            "m_RotationDeadZone",
            "m_DigitalReleaseGraceSeconds"
        });
        section.Add(valuesFoldout);

        System.Action updateView = () =>
        {
            LookDirectionsMode directionsMode = (LookDirectionsMode)directionsModeProperty.enumValueIndex;
            RotationMode rotationMode = (RotationMode)rotationModeProperty.enumValueIndex;
            LookMultiplierSampling samplingMode = samplingProperty != null ? (LookMultiplierSampling)samplingProperty.enumValueIndex : LookMultiplierSampling.DirectionalBlend;
            bool isDiscrete = directionsMode == LookDirectionsMode.DiscreteCount;
            bool isCones = directionsMode == LookDirectionsMode.Cones;
            bool followMovement = directionsMode == LookDirectionsMode.FollowMovementDirection;

            discreteContainer.style.display = isDiscrete && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            PlayerControllerDirectionWarningUtility.RefreshOffsetWarnings(discreteWarningsRoot,
                                                                         isDiscrete && !followMovement,
                                                                         countProperty.intValue,
                                                                         offsetProperty.floatValue);
            conesContainer.style.display = isCones && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            pieChart.style.display = directionsMode == LookDirectionsMode.AllDirections || followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            lookZoomSlider.style.display = directionsMode == LookDirectionsMode.AllDirections || followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            rotationModeField.style.display = followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            rotationSpeedField.style.display = rotationMode == RotationMode.Continuous && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            samplingField.style.display = isDiscrete && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            bindingsFoldout.style.display = followMovement ? DisplayStyle.None : DisplayStyle.Flex;
            valuesFoldout.style.display = followMovement ? DisplayStyle.None : DisplayStyle.Flex;

            PlayerControllerPresetsPanelVisualizationUtility.UpdateLookPieChart(pieChart,
                                                                                directionsMode,
                                                                                countProperty.intValue,
                                                                                offsetProperty.floatValue,
                                                                                frontEnabledProperty,
                                                                                frontAngleProperty,
                                                                                backEnabledProperty,
                                                                                backAngleProperty,
                                                                                leftEnabledProperty,
                                                                                leftAngleProperty,
                                                                                rightEnabledProperty,
                                                                                rightAngleProperty);
            PlayerControllerPresetsPanelVisualizationUtility.UpdateLookLabels(pieChart, directionsMode, samplingMode, countProperty.intValue, offsetProperty.floatValue);
            pieChart.SetOverlayFields(null);

            multipliersSection.style.display = isDiscrete && !followMovement ? DisplayStyle.Flex : DisplayStyle.None;
            multipliersSection.style.marginLeft = SubSectionMarginLeft;

            if (multipliersHeader != null)
                multipliersHeader.text = samplingMode == LookMultiplierSampling.ArcConstant ? "Arc Multipliers" : "Directional Multipliers";

            if (isDiscrete)
            {
                PlayerControllerPresetsPanelVisualizationUtility.UpdateDiscreteMultipliersTable(panel,
                                                                                                multipliersTableRoot,
                                                                                                samplingMode,
                                                                                                countProperty.intValue,
                                                                                                offsetProperty.floatValue,
                                                                                                maxSpeedMultipliersProperty,
                                                                                                accelerationMultipliersProperty);
            }
        };

        directionsModeField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        countFieldElement.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        countFieldElement.RegisterCallback<ChangeEvent<int>>(evt =>
        {
            ScheduleViewRefresh();
        });

        offsetFieldElement.RegisterCallback<ChangeEvent<float>>(evt =>
        {
            ScheduleViewRefresh();
        });

        offsetFieldElement.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            updateView();
        });

        rotationModeField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        samplingField.RegisterValueChangedCallback(evt =>
        {
            updateView();
        });

        for (int index = 0; index < coneToggles.Count; index++)
        {
            Toggle toggle = coneToggles[index];
            toggle.RegisterValueChangedCallback(evt =>
            {
                updateView();
            });
        }

        for (int index = 0; index < coneAngleFields.Count; index++)
        {
            FloatField angleField = coneAngleFields[index];
            angleField.RegisterValueChangedCallback(evt =>
            {
                updateView();
            });
        }

        updateView();

        void ScheduleViewRefresh()
        {
            section.schedule.Execute(() =>
            {
                if (panel.PresetSerializedObject != null)
                    panel.PresetSerializedObject.Update();

                updateView();
            }).ExecuteLater(0);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds one cone row with toggle, angle and multiplier fields and optionally registers the created controls.
    /// </summary>
    /// <param name="label">Displayed cone label.</param>
    /// <param name="enabledProperty">Serialized cone enabled property.</param>
    /// <param name="angleProperty">Serialized cone angle property.</param>
    /// <param name="maxSpeedProperty">Serialized max-speed multiplier property.</param>
    /// <param name="accelerationProperty">Serialized acceleration multiplier property.</param>
    /// <param name="toggles">Optional list that receives the created toggle.</param>
    /// <param name="angleFields">Optional list that receives the created angle field.</param>
    /// <returns>Returns the constructed cone row.<returns>
    private static VisualElement BuildConeRow(string label,
                                              SerializedProperty enabledProperty,
                                              SerializedProperty angleProperty,
                                              SerializedProperty maxSpeedProperty,
                                              SerializedProperty accelerationProperty,
                                              List<Toggle> toggles,
                                              List<FloatField> angleFields)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = MultiplierRowSpacing;

        Toggle enabledToggle = new Toggle(label);
        enabledToggle.style.minWidth = 120f;
        enabledToggle.BindProperty(enabledProperty);
        row.Add(enabledToggle);

        FloatField angleField = new FloatField("Angle");
        angleField.style.flexGrow = 0f;
        angleField.style.width = 110f;
        angleField.BindProperty(angleProperty);
        row.Add(angleField);

        row.Add(PlayerControllerPresetsPanelVisualizationUtility.CreatePercentField(maxSpeedProperty,
                                                                                    new Color(0.2f, 0.8f, 1f, 0.85f),
                                                                                    "Max speed multiplier for this cone.",
                                                                                    "Max %"));
        row.Add(PlayerControllerPresetsPanelVisualizationUtility.CreatePercentField(accelerationProperty,
                                                                                    new Color(1f, 0.65f, 0.2f, 0.85f),
                                                                                    "Acceleration multiplier for this cone.",
                                                                                    "Accel %"));

        if (toggles != null)
            toggles.Add(enabledToggle);

        if (angleFields != null)
            angleFields.Add(angleField);

        return row;
    }
    #endregion

    #endregion
}
