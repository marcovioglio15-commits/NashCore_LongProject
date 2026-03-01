using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(PowerUpModuleDefinition))]
public sealed class PowerUpModuleDefinitionPropertyDrawer : PropertyDrawer
{
    #region Constants
    private const float InfoIndent = 126f;
    private static readonly Color SplitUniformColorA = new Color(0.20f, 0.62f, 0.88f, 0.85f);
    private static readonly Color SplitUniformColorB = new Color(0.12f, 0.42f, 0.72f, 0.85f);
    private static readonly Color SplitCustomColor = new Color(0.96f, 0.56f, 0.18f, 0.88f);
    private static readonly Color SplitBackgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.55f);
    private static readonly Color ConeBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.42f);
    private static readonly Color ConeFillColor = new Color(0.18f, 0.72f, 0.92f, 0.82f);
    private static readonly Color ConeDirectionColor = new Color(0.94f, 0.94f, 0.94f, 0.92f);
    private static readonly Color ConeForwardColor = new Color(1f, 0.84f, 0.22f, 1f);
    #endregion

    #region Methods
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = new VisualElement();
        SerializedProperty moduleIdProperty = property.FindPropertyRelative("moduleId");
        SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
        SerializedProperty moduleKindProperty = property.FindPropertyRelative("moduleKind");
        SerializedProperty defaultStageProperty = property.FindPropertyRelative("defaultStage");
        SerializedProperty notesProperty = property.FindPropertyRelative("notes");
        SerializedProperty dataProperty = property.FindPropertyRelative("data");

        if (moduleIdProperty == null ||
            displayNameProperty == null ||
            moduleKindProperty == null ||
            defaultStageProperty == null ||
            notesProperty == null ||
            dataProperty == null)
        {
            Label errorLabel = new Label("PowerUpModuleDefinition serialized fields are missing.");
            errorLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            root.Add(errorLabel);
            return root;
        }

        AddField(root, moduleIdProperty, "Module ID");
        AddField(root, displayNameProperty, "Display Name");

        List<PowerUpModuleKind> moduleKindOptions = BuildModuleKindOptions();
        PowerUpModuleKind currentModuleKind = ResolveModuleKind(moduleKindProperty);
        PopupField<PowerUpModuleKind> moduleKindPopup = new PopupField<PowerUpModuleKind>("Module Kind", moduleKindOptions, currentModuleKind);
        moduleKindPopup.formatListItemCallback = PowerUpModuleEnumDescriptions.FormatModuleKindOption;
        moduleKindPopup.formatSelectedValueCallback = moduleKind =>
        {
            return moduleKind.ToString();
        };
        moduleKindPopup.tooltip = "Determines runtime behavior and payload schema. Changing this value also changes which payload fields are used by bindings.";
        root.Add(moduleKindPopup);

        HelpBox moduleKindInfoBox = new HelpBox(PowerUpModuleEnumDescriptions.GetModuleKindDescription(currentModuleKind), HelpBoxMessageType.Info);
        moduleKindInfoBox.style.marginTop = 2f;
        moduleKindInfoBox.style.marginLeft = InfoIndent;
        root.Add(moduleKindInfoBox);

        AddField(root, notesProperty, "Notes");

        Label payloadHeader = new Label("Module Payload");
        payloadHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        payloadHeader.style.marginTop = 4f;
        payloadHeader.style.marginLeft = InfoIndent;
        root.Add(payloadHeader);

        VisualElement payloadContainer = new VisualElement();
        payloadContainer.style.marginLeft = InfoIndent;
        root.Add(payloadContainer);

        RefreshModuleUi(moduleKindProperty,
                        defaultStageProperty,
                        dataProperty,
                        moduleKindPopup,
                        moduleKindInfoBox,
                        payloadContainer);

        moduleKindPopup.RegisterValueChangedCallback(evt =>
        {
            if ((int)evt.newValue == moduleKindProperty.enumValueIndex)
            {
                return;
            }

            moduleKindProperty.serializedObject.Update();
            moduleKindProperty.enumValueIndex = (int)evt.newValue;
            moduleKindProperty.serializedObject.ApplyModifiedProperties();
            RefreshModuleUi(moduleKindProperty,
                            defaultStageProperty,
                            dataProperty,
                            moduleKindPopup,
                            moduleKindInfoBox,
                            payloadContainer);
        });

        root.TrackPropertyValue(moduleKindProperty, changedProperty =>
        {
            RefreshModuleUi(changedProperty,
                            defaultStageProperty,
                            dataProperty,
                            moduleKindPopup,
                            moduleKindInfoBox,
                            payloadContainer);
        });

        return root;
    }

    private static void RefreshModuleUi(SerializedProperty moduleKindProperty,
                                        SerializedProperty stageProperty,
                                        SerializedProperty dataProperty,
                                        PopupField<PowerUpModuleKind> moduleKindPopup,
                                        HelpBox moduleKindInfoBox,
                                        VisualElement payloadContainer)
    {
        PowerUpModuleKind moduleKind = ResolveModuleKind(moduleKindProperty);
        PowerUpModuleStage stage = PowerUpModuleKindUtility.ResolveStageFromKind(moduleKind);

        if (stageProperty != null && stageProperty.propertyType == SerializedPropertyType.Enum && stageProperty.enumValueIndex != (int)stage)
        {
            stageProperty.serializedObject.Update();
            stageProperty.enumValueIndex = (int)stage;
            stageProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        if (EqualityComparer<PowerUpModuleKind>.Default.Equals(moduleKindPopup.value, moduleKind) == false)
        {
            moduleKindPopup.SetValueWithoutNotify(moduleKind);
        }

        moduleKindInfoBox.text = PowerUpModuleEnumDescriptions.GetModuleKindDescription(moduleKind);
        RebuildPayloadContainer(payloadContainer, dataProperty, moduleKind);
    }

    private static void RebuildPayloadContainer(VisualElement payloadContainer, SerializedProperty dataProperty, PowerUpModuleKind moduleKind)
    {
        if (payloadContainer == null)
        {
            return;
        }

        payloadContainer.Clear();

        if (dataProperty == null)
        {
            return;
        }

        string relativePath;
        string payloadLabel;
        bool hasPayload = PowerUpModuleEnumDescriptions.TryGetPayloadProperty(moduleKind, out relativePath, out payloadLabel);

        if (hasPayload == false)
        {
            HelpBox infoBox = new HelpBox("No payload is required for this module kind.", HelpBoxMessageType.Info);
            payloadContainer.Add(infoBox);
            return;
        }

        SerializedProperty payloadProperty = dataProperty.FindPropertyRelative(relativePath);

        if (payloadProperty == null)
        {
            HelpBox warningBox = new HelpBox("Payload property is missing for the selected module kind.", HelpBoxMessageType.Warning);
            payloadContainer.Add(warningBox);
            return;
        }

        BuildPayloadEditor(payloadContainer, payloadProperty, moduleKind, payloadLabel);
    }

    public static void BuildPayloadEditor(VisualElement payloadContainer,
                                          SerializedProperty payloadProperty,
                                          PowerUpModuleKind moduleKind,
                                          string payloadLabel)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        switch (moduleKind)
        {
            case PowerUpModuleKind.ProjectileSplit:
                BuildProjectileSplitPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.SpawnObject:
                BuildSpawnObjectPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.Heal:
                BuildHealPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.StateSuppressShooting:
                BuildSuppressShootingPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.ProjectilesTuning:
                BuildProjectileTuningPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.ProjectilesPatternCone:
                BuildProjectilePatternConePayloadUi(payloadContainer, payloadProperty);
                return;
        }

        PropertyField payloadField = new PropertyField(payloadProperty, payloadLabel);
        payloadField.BindProperty(payloadProperty);
        payloadContainer.Add(payloadField);
    }

    private static void BuildSpawnObjectPayloadUi(VisualElement payloadContainer, SerializedProperty spawnPayloadProperty)
    {
        if (payloadContainer == null || spawnPayloadProperty == null)
            return;

        SerializedProperty prefabProperty = spawnPayloadProperty.FindPropertyRelative("bombPrefab");
        SerializedProperty spawnOffsetProperty = spawnPayloadProperty.FindPropertyRelative("spawnOffset");
        SerializedProperty spawnOffsetOrientationProperty = spawnPayloadProperty.FindPropertyRelative("spawnOffsetOrientation");
        SerializedProperty deploySpeedProperty = spawnPayloadProperty.FindPropertyRelative("deploySpeed");
        SerializedProperty collisionRadiusProperty = spawnPayloadProperty.FindPropertyRelative("collisionRadius");
        SerializedProperty bounceOnWallsProperty = spawnPayloadProperty.FindPropertyRelative("bounceOnWalls");
        SerializedProperty bounceDampingProperty = spawnPayloadProperty.FindPropertyRelative("bounceDamping");
        SerializedProperty linearDampingPerSecondProperty = spawnPayloadProperty.FindPropertyRelative("linearDampingPerSecond");
        SerializedProperty fuseSecondsProperty = spawnPayloadProperty.FindPropertyRelative("fuseSeconds");
        SerializedProperty enableDamagePayloadProperty = spawnPayloadProperty.FindPropertyRelative("enableDamagePayload");
        SerializedProperty radiusProperty = spawnPayloadProperty.FindPropertyRelative("radius");
        SerializedProperty damageProperty = spawnPayloadProperty.FindPropertyRelative("damage");
        SerializedProperty affectAllEnemiesInRadiusProperty = spawnPayloadProperty.FindPropertyRelative("affectAllEnemiesInRadius");

        if (prefabProperty == null ||
            spawnOffsetProperty == null ||
            spawnOffsetOrientationProperty == null ||
            deploySpeedProperty == null ||
            collisionRadiusProperty == null ||
            bounceOnWallsProperty == null ||
            bounceDampingProperty == null ||
            linearDampingPerSecondProperty == null ||
            fuseSecondsProperty == null ||
            enableDamagePayloadProperty == null ||
            radiusProperty == null ||
            damageProperty == null ||
            affectAllEnemiesInRadiusProperty == null)
        {
            HelpBox errorBox = new HelpBox("Spawn object payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        Foldout spawnFoldout = new Foldout
        {
            text = "Spawn",
            value = true
        };
        payloadContainer.Add(spawnFoldout);
        AddField(spawnFoldout, prefabProperty, "Spawn Prefab");
        AddField(spawnFoldout, spawnOffsetProperty, "Spawn Offset");
        AddField(spawnFoldout, spawnOffsetOrientationProperty, "Spawn Offset Orientation");
        AddField(spawnFoldout, deploySpeedProperty, "Deploy Speed");
        AddField(spawnFoldout, collisionRadiusProperty, "Collision Radius");
        AddField(spawnFoldout, bounceOnWallsProperty, "Bounce On Walls");
        AddField(spawnFoldout, bounceDampingProperty, "Bounce Damping");
        AddField(spawnFoldout, linearDampingPerSecondProperty, "Linear Damping Per Second");
        AddField(spawnFoldout, fuseSecondsProperty, "Fuse Seconds");

        Foldout damageFoldout = new Foldout
        {
            text = "Damage (Optional)",
            value = true
        };
        spawnFoldout.Add(damageFoldout);
        AddField(damageFoldout, enableDamagePayloadProperty, "Enable Damage Payload");

        VisualElement damageContainer = new VisualElement();
        damageContainer.style.marginLeft = 12f;
        damageFoldout.Add(damageContainer);
        AddField(damageContainer, radiusProperty, "Radius");
        AddField(damageContainer, damageProperty, "Damage");
        AddField(damageContainer, affectAllEnemiesInRadiusProperty, "Affect All Enemies In Radius");

        UpdateDamageContainerVisibility(enableDamagePayloadProperty, damageContainer);
        payloadContainer.TrackPropertyValue(enableDamagePayloadProperty, changedProperty =>
        {
            UpdateDamageContainerVisibility(changedProperty, damageContainer);
        });
    }

    private static void BuildHealPayloadUi(VisualElement payloadContainer, SerializedProperty healPayloadProperty)
    {
        if (payloadContainer == null || healPayloadProperty == null)
            return;

        SerializedProperty applyModeProperty = healPayloadProperty.FindPropertyRelative("applyMode");
        SerializedProperty healAmountProperty = healPayloadProperty.FindPropertyRelative("healAmount");
        SerializedProperty durationSecondsProperty = healPayloadProperty.FindPropertyRelative("durationSeconds");
        SerializedProperty tickIntervalSecondsProperty = healPayloadProperty.FindPropertyRelative("tickIntervalSeconds");
        SerializedProperty stackPolicyProperty = healPayloadProperty.FindPropertyRelative("stackPolicy");

        if (applyModeProperty == null ||
            healAmountProperty == null ||
            durationSecondsProperty == null ||
            tickIntervalSecondsProperty == null ||
            stackPolicyProperty == null)
        {
            HelpBox errorBox = new HelpBox("Heal payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, applyModeProperty, "Apply Mode");
        AddField(payloadContainer, healAmountProperty, "Heal Amount");

        VisualElement overTimeContainer = new VisualElement();
        overTimeContainer.style.marginLeft = 12f;
        payloadContainer.Add(overTimeContainer);
        AddField(overTimeContainer, durationSecondsProperty, "Duration Seconds");
        AddField(overTimeContainer, tickIntervalSecondsProperty, "Tick Interval Seconds");
        AddField(overTimeContainer, stackPolicyProperty, "Stack Policy");

        UpdateHealOverTimeContainerVisibility(applyModeProperty, overTimeContainer);
        payloadContainer.TrackPropertyValue(applyModeProperty, changedProperty =>
        {
            UpdateHealOverTimeContainerVisibility(changedProperty, overTimeContainer);
        });
    }

    private static void UpdateHealOverTimeContainerVisibility(SerializedProperty applyModeProperty, VisualElement overTimeContainer)
    {
        if (overTimeContainer == null)
            return;

        if (applyModeProperty == null)
        {
            overTimeContainer.style.display = DisplayStyle.None;
            return;
        }

        PowerUpHealApplicationMode applyMode = (PowerUpHealApplicationMode)applyModeProperty.enumValueIndex;
        overTimeContainer.style.display = applyMode == PowerUpHealApplicationMode.OverTime ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void BuildSuppressShootingPayloadUi(VisualElement payloadContainer, SerializedProperty suppressPayloadProperty)
    {
        if (payloadContainer == null || suppressPayloadProperty == null)
            return;

        SerializedProperty suppressBaseShootingProperty = suppressPayloadProperty.FindPropertyRelative("suppressBaseShootingWhileActive");
        SerializedProperty interruptOtherSlotOnEnterProperty = suppressPayloadProperty.FindPropertyRelative("interruptOtherSlotOnEnter");
        SerializedProperty interruptOtherSlotChargingOnlyProperty = suppressPayloadProperty.FindPropertyRelative("interruptOtherSlotChargingOnly");

        if (suppressBaseShootingProperty == null ||
            interruptOtherSlotOnEnterProperty == null ||
            interruptOtherSlotChargingOnlyProperty == null)
        {
            HelpBox errorBox = new HelpBox("Suppress Shooting payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, suppressBaseShootingProperty, "Suppress Base Shooting While Active");
        AddField(payloadContainer, interruptOtherSlotOnEnterProperty, "Interrupt Other Slot On Enter");

        VisualElement interruptOptionsContainer = new VisualElement();
        interruptOptionsContainer.style.marginLeft = 12f;
        payloadContainer.Add(interruptOptionsContainer);
        AddField(interruptOptionsContainer, interruptOtherSlotChargingOnlyProperty, "Interrupt Other Slot Charging Only");

        UpdateInterruptOptionsVisibility(interruptOtherSlotOnEnterProperty, interruptOptionsContainer);
        payloadContainer.TrackPropertyValue(interruptOtherSlotOnEnterProperty, changedProperty =>
        {
            UpdateInterruptOptionsVisibility(changedProperty, interruptOptionsContainer);
        });
    }

    private static void UpdateInterruptOptionsVisibility(SerializedProperty interruptOtherSlotOnEnterProperty, VisualElement interruptOptionsContainer)
    {
        if (interruptOptionsContainer == null)
            return;

        if (interruptOtherSlotOnEnterProperty == null)
        {
            interruptOptionsContainer.style.display = DisplayStyle.None;
            return;
        }

        interruptOptionsContainer.style.display = interruptOtherSlotOnEnterProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void BuildProjectileTuningPayloadUi(VisualElement payloadContainer, SerializedProperty projectileTuningPayloadProperty)
    {
        if (payloadContainer == null || projectileTuningPayloadProperty == null)
            return;

        SerializedProperty sizeMultiplierProperty = projectileTuningPayloadProperty.FindPropertyRelative("sizeMultiplier");
        SerializedProperty damageMultiplierProperty = projectileTuningPayloadProperty.FindPropertyRelative("damageMultiplier");
        SerializedProperty speedMultiplierProperty = projectileTuningPayloadProperty.FindPropertyRelative("speedMultiplier");
        SerializedProperty rangeMultiplierProperty = projectileTuningPayloadProperty.FindPropertyRelative("rangeMultiplier");
        SerializedProperty lifetimeMultiplierProperty = projectileTuningPayloadProperty.FindPropertyRelative("lifetimeMultiplier");
        SerializedProperty penetrationModeProperty = projectileTuningPayloadProperty.FindPropertyRelative("penetrationMode");
        SerializedProperty maxPenetrationsProperty = projectileTuningPayloadProperty.FindPropertyRelative("maxPenetrations");
        SerializedProperty applyElementalOnHitProperty = projectileTuningPayloadProperty.FindPropertyRelative("applyElementalOnHit");
        SerializedProperty elementalEffectDataProperty = projectileTuningPayloadProperty.FindPropertyRelative("elementalEffectData");
        SerializedProperty elementalStacksPerHitProperty = projectileTuningPayloadProperty.FindPropertyRelative("elementalStacksPerHit");

        if (sizeMultiplierProperty == null ||
            damageMultiplierProperty == null ||
            speedMultiplierProperty == null ||
            rangeMultiplierProperty == null ||
            lifetimeMultiplierProperty == null ||
            penetrationModeProperty == null ||
            maxPenetrationsProperty == null ||
            applyElementalOnHitProperty == null ||
            elementalEffectDataProperty == null ||
            elementalStacksPerHitProperty == null)
        {
            HelpBox errorBox = new HelpBox("Projectile tuning payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, sizeMultiplierProperty, "Size Multiplier");
        AddField(payloadContainer, damageMultiplierProperty, "Damage Multiplier");
        AddField(payloadContainer, speedMultiplierProperty, "Speed Multiplier");
        AddField(payloadContainer, rangeMultiplierProperty, "Range Multiplier");
        AddField(payloadContainer, lifetimeMultiplierProperty, "Lifetime Multiplier");
        AddField(payloadContainer, penetrationModeProperty, "Penetration Mode");

        VisualElement penetrationContainer = new VisualElement();
        penetrationContainer.style.marginLeft = 12f;
        payloadContainer.Add(penetrationContainer);
        AddField(penetrationContainer, maxPenetrationsProperty, "Max Penetrations");

        UpdatePenetrationOptionsVisibility(penetrationModeProperty, penetrationContainer);
        payloadContainer.TrackPropertyValue(penetrationModeProperty, changedProperty =>
        {
            UpdatePenetrationOptionsVisibility(changedProperty, penetrationContainer);
        });

        AddField(payloadContainer, applyElementalOnHitProperty, "Apply Elemental On Hit");

        VisualElement elementalPayloadContainer = new VisualElement();
        elementalPayloadContainer.style.marginLeft = 12f;
        payloadContainer.Add(elementalPayloadContainer);
        AddField(elementalPayloadContainer, elementalEffectDataProperty, "Elemental Effect");
        AddField(elementalPayloadContainer, elementalStacksPerHitProperty, "Elemental Stacks Per Hit");

        UpdateElementalPayloadOptionsVisibility(applyElementalOnHitProperty, elementalPayloadContainer);
        payloadContainer.TrackPropertyValue(applyElementalOnHitProperty, changedProperty =>
        {
            UpdateElementalPayloadOptionsVisibility(changedProperty, elementalPayloadContainer);
        });
    }

    private static void BuildProjectilePatternConePayloadUi(VisualElement payloadContainer, SerializedProperty conePayloadProperty)
    {
        if (payloadContainer == null || conePayloadProperty == null)
            return;

        SerializedProperty projectileCountProperty = conePayloadProperty.FindPropertyRelative("projectileCount");
        SerializedProperty coneAngleDegreesProperty = conePayloadProperty.FindPropertyRelative("coneAngleDegrees");

        if (projectileCountProperty == null || coneAngleDegreesProperty == null)
        {
            HelpBox errorBox = new HelpBox("Projectile cone payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, projectileCountProperty, "Projectile Count");
        AddField(payloadContainer, coneAngleDegreesProperty, "Cone Angle Degrees");

        Label chartLabel = new Label("Cone Pattern Preview");
        chartLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        chartLabel.style.marginTop = 4f;
        payloadContainer.Add(chartLabel);

        PieChartElement pieChart = new PieChartElement();
        pieChart.style.minHeight = 220f;
        pieChart.style.marginTop = 4f;
        pieChart.style.marginBottom = 6f;
        pieChart.SetZoom(0.95f);
        payloadContainer.Add(pieChart);

        payloadContainer.TrackPropertyValue(projectileCountProperty, changedProperty =>
        {
            UpdateProjectilePatternConePieChart(pieChart, changedProperty, coneAngleDegreesProperty);
        });

        payloadContainer.TrackPropertyValue(coneAngleDegreesProperty, changedProperty =>
        {
            UpdateProjectilePatternConePieChart(pieChart, projectileCountProperty, changedProperty);
        });

        UpdateProjectilePatternConePieChart(pieChart, projectileCountProperty, coneAngleDegreesProperty);
    }

    private static void BuildProjectilePenetrationPayloadUi(VisualElement payloadContainer, SerializedProperty projectilePenetrationPayloadProperty)
    {
        if (payloadContainer == null || projectilePenetrationPayloadProperty == null)
            return;

        SerializedProperty penetrationModeProperty = projectilePenetrationPayloadProperty.FindPropertyRelative("mode");
        SerializedProperty maxPenetrationsProperty = projectilePenetrationPayloadProperty.FindPropertyRelative("maxPenetrations");

        if (penetrationModeProperty == null || maxPenetrationsProperty == null)
        {
            HelpBox errorBox = new HelpBox("Projectile penetration payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, penetrationModeProperty, "Penetration Mode");

        VisualElement penetrationContainer = new VisualElement();
        penetrationContainer.style.marginLeft = 12f;
        payloadContainer.Add(penetrationContainer);
        AddField(penetrationContainer, maxPenetrationsProperty, "Max Penetrations");

        UpdatePenetrationOptionsVisibility(penetrationModeProperty, penetrationContainer);
        payloadContainer.TrackPropertyValue(penetrationModeProperty, changedProperty =>
        {
            UpdatePenetrationOptionsVisibility(changedProperty, penetrationContainer);
        });
    }

    private static void UpdatePenetrationOptionsVisibility(SerializedProperty penetrationModeProperty, VisualElement penetrationContainer)
    {
        if (penetrationContainer == null)
            return;

        if (penetrationModeProperty == null)
        {
            penetrationContainer.style.display = DisplayStyle.None;
            return;
        }

        ProjectilePenetrationMode penetrationMode = (ProjectilePenetrationMode)penetrationModeProperty.enumValueIndex;
        penetrationContainer.style.display = penetrationMode == ProjectilePenetrationMode.FixedHits ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void UpdateElementalPayloadOptionsVisibility(SerializedProperty applyElementalOnHitProperty, VisualElement elementalPayloadContainer)
    {
        if (elementalPayloadContainer == null)
            return;

        if (applyElementalOnHitProperty == null)
        {
            elementalPayloadContainer.style.display = DisplayStyle.None;
            return;
        }

        elementalPayloadContainer.style.display = applyElementalOnHitProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void UpdateProjectilePatternConePieChart(PieChartElement pieChart,
                                                            SerializedProperty projectileCountProperty,
                                                            SerializedProperty coneAngleDegreesProperty)
    {
        if (pieChart == null || projectileCountProperty == null || coneAngleDegreesProperty == null)
            return;

        int projectileCount = Mathf.Max(1, projectileCountProperty.intValue);
        float coneAngleDegrees = Mathf.Max(0f, coneAngleDegreesProperty.floatValue);
        float halfCone = coneAngleDegrees * 0.5f;
        List<PieChartElement.PieSlice> slices = new List<PieChartElement.PieSlice>();
        List<float> directionMarkers = new List<float>();
        List<PieChartElement.LabelDescriptor> labels = new List<PieChartElement.LabelDescriptor>();

        slices.Add(new PieChartElement.PieSlice
        {
            StartAngle = 0f,
            EndAngle = 360f,
            MidAngle = 180f,
            Color = ConeBackgroundColor
        });

        if (coneAngleDegrees > 0f)
            AddNormalizedSlice(ref slices, NormalizeAngle(-halfCone), NormalizeAngle(halfCone), ConeFillColor);

        if (projectileCount <= 1)
        {
            directionMarkers.Add(0f);
            labels.Add(new PieChartElement.LabelDescriptor
            {
                Angle = 0f,
                Text = "1",
                RadiusOffset = -10f,
                TextColor = Color.white,
                UseTextColor = true
            });
        }
        else
        {
            float stepDegrees = projectileCount > 1 ? coneAngleDegrees / (projectileCount - 1) : 0f;

            for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            {
                float angle = -halfCone + stepDegrees * projectileIndex;
                float normalizedAngle = NormalizeAngle(angle);
                directionMarkers.Add(normalizedAngle);
                labels.Add(new PieChartElement.LabelDescriptor
                {
                    Angle = normalizedAngle,
                    Text = (projectileIndex + 1).ToString(),
                    RadiusOffset = -10f,
                    TextColor = Color.white,
                    UseTextColor = true
                });
            }
        }

        pieChart.SetSlices(slices);
        pieChart.SetDirectionMarkers(directionMarkers, ConeDirectionColor, ConeForwardColor, 0f, true);
        pieChart.SetSegmentLabels(labels);
        pieChart.SetOverlayFields(null);
    }

    private static void UpdateDamageContainerVisibility(SerializedProperty enableDamagePayloadProperty, VisualElement damageContainer)
    {
        if (damageContainer == null)
            return;

        if (enableDamagePayloadProperty == null)
        {
            damageContainer.style.display = DisplayStyle.None;
            return;
        }

        damageContainer.style.display = enableDamagePayloadProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static void BuildProjectileSplitPayloadUi(VisualElement payloadContainer, SerializedProperty splitPayloadProperty)
    {
        if (payloadContainer == null || splitPayloadProperty == null)
        {
            return;
        }

        SerializedProperty triggerModeProperty = splitPayloadProperty.FindPropertyRelative("triggerMode");
        SerializedProperty directionModeProperty = splitPayloadProperty.FindPropertyRelative("directionMode");
        SerializedProperty splitProjectileCountProperty = splitPayloadProperty.FindPropertyRelative("splitProjectileCount");
        SerializedProperty splitOffsetDegreesProperty = splitPayloadProperty.FindPropertyRelative("splitOffsetDegrees");
        SerializedProperty customAnglesDegreesProperty = splitPayloadProperty.FindPropertyRelative("customAnglesDegrees");
        SerializedProperty splitDamagePercentFromOriginalProperty = splitPayloadProperty.FindPropertyRelative("splitDamagePercentFromOriginal");
        SerializedProperty splitSizePercentFromOriginalProperty = splitPayloadProperty.FindPropertyRelative("splitSizePercentFromOriginal");
        SerializedProperty splitSpeedPercentFromOriginalProperty = splitPayloadProperty.FindPropertyRelative("splitSpeedPercentFromOriginal");
        SerializedProperty splitLifetimePercentFromOriginalProperty = splitPayloadProperty.FindPropertyRelative("splitLifetimePercentFromOriginal");

        if (triggerModeProperty == null ||
            directionModeProperty == null ||
            splitProjectileCountProperty == null ||
            splitOffsetDegreesProperty == null ||
            customAnglesDegreesProperty == null ||
            splitDamagePercentFromOriginalProperty == null ||
            splitSizePercentFromOriginalProperty == null ||
            splitSpeedPercentFromOriginalProperty == null ||
            splitLifetimePercentFromOriginalProperty == null)
        {
            HelpBox errorBox = new HelpBox("Projectile split payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, triggerModeProperty, "Split Trigger Mode");
        AddField(payloadContainer, directionModeProperty, "Direction Mode");

        VisualElement uniformContainer = new VisualElement();
        uniformContainer.style.marginLeft = 12f;
        AddField(uniformContainer, splitProjectileCountProperty, "Split Projectile Count");
        payloadContainer.Add(uniformContainer);

        VisualElement customAnglesContainer = new VisualElement();
        customAnglesContainer.style.marginLeft = 12f;
        AddField(customAnglesContainer, customAnglesDegreesProperty, "Custom Angles Degrees");
        payloadContainer.Add(customAnglesContainer);

        AddField(payloadContainer, splitOffsetDegreesProperty, "Split Offset Degrees");
        AddField(payloadContainer, splitDamagePercentFromOriginalProperty, "Split Damage % From Original");
        AddField(payloadContainer, splitSizePercentFromOriginalProperty, "Split Size % From Original");
        AddField(payloadContainer, splitSpeedPercentFromOriginalProperty, "Split Speed % From Original");
        AddField(payloadContainer, splitLifetimePercentFromOriginalProperty, "Split Lifetime % From Original");

        UpdateDirectionModeContainers(directionModeProperty, uniformContainer, customAnglesContainer);
        payloadContainer.TrackPropertyValue(directionModeProperty, changedProperty =>
        {
            UpdateDirectionModeContainers(changedProperty, uniformContainer, customAnglesContainer);
        });

        Label chartLabel = new Label("Split Direction Pie");
        chartLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        chartLabel.style.marginTop = 4f;
        payloadContainer.Add(chartLabel);

        PieChartElement pieChart = new PieChartElement();
        pieChart.style.minHeight = 220f;
        pieChart.style.marginTop = 4f;
        pieChart.style.marginBottom = 6f;
        pieChart.SetZoom(0.95f);
        payloadContainer.Add(pieChart);

        payloadContainer.TrackPropertyValue(directionModeProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                changedProperty,
                                splitProjectileCountProperty,
                                splitOffsetDegreesProperty,
                                customAnglesDegreesProperty);
        });

        payloadContainer.TrackPropertyValue(splitProjectileCountProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                directionModeProperty,
                                changedProperty,
                                splitOffsetDegreesProperty,
                                customAnglesDegreesProperty);
        });

        payloadContainer.TrackPropertyValue(splitOffsetDegreesProperty, changedProperty =>
        {
            UpdateSplitPieChart(pieChart,
                                directionModeProperty,
                                splitProjectileCountProperty,
                                changedProperty,
                                customAnglesDegreesProperty);
        });

        payloadContainer.TrackPropertyValue(customAnglesDegreesProperty, changedProperty =>
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

    private static void UpdateDirectionModeContainers(SerializedProperty directionModeProperty,
                                                      VisualElement uniformContainer,
                                                      VisualElement customAnglesContainer)
    {
        if (directionModeProperty == null || uniformContainer == null || customAnglesContainer == null)
        {
            return;
        }

        ProjectileSplitDirectionMode directionMode = (ProjectileSplitDirectionMode)directionModeProperty.enumValueIndex;
        bool showUniform = directionMode == ProjectileSplitDirectionMode.Uniform;
        uniformContainer.style.display = showUniform ? DisplayStyle.Flex : DisplayStyle.None;
        customAnglesContainer.style.display = showUniform ? DisplayStyle.None : DisplayStyle.Flex;
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
        {
            return;
        }

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
            PieChartElement.PieSlice emptySlice = new PieChartElement.PieSlice
            {
                StartAngle = 0f,
                EndAngle = 360f,
                MidAngle = 180f,
                Color = SplitBackgroundColor
            };
            slices.Add(emptySlice);
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
            directionMarkers.Add(start);

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
        {
            return;
        }

        for (int angleIndex = 0; angleIndex < angleCount; angleIndex++)
        {
            SerializedProperty angleProperty = customAnglesDegreesProperty.GetArrayElementAtIndex(angleIndex);

            if (angleProperty == null)
            {
                continue;
            }

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
            PieChartElement.PieSlice fullSlice = new PieChartElement.PieSlice
            {
                StartAngle = 0f,
                EndAngle = 360f,
                MidAngle = 180f,
                Color = color
            };
            slices.Add(fullSlice);
            return;
        }

        if (normalizedEnd > normalizedStart)
        {
            PieChartElement.PieSlice simpleSlice = new PieChartElement.PieSlice
            {
                StartAngle = normalizedStart,
                EndAngle = normalizedEnd,
                MidAngle = NormalizeAngle((normalizedStart + normalizedEnd) * 0.5f),
                Color = color
            };
            slices.Add(simpleSlice);
            return;
        }

        PieChartElement.PieSlice wrapSliceA = new PieChartElement.PieSlice
        {
            StartAngle = normalizedStart,
            EndAngle = 360f,
            MidAngle = NormalizeAngle((normalizedStart + 360f) * 0.5f),
            Color = color
        };
        slices.Add(wrapSliceA);

        PieChartElement.PieSlice wrapSliceB = new PieChartElement.PieSlice
        {
            StartAngle = 0f,
            EndAngle = normalizedEnd,
            MidAngle = NormalizeAngle(normalizedEnd * 0.5f),
            Color = color
        };
        slices.Add(wrapSliceB);
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle, 360f);
    }

    private static void UpdateStageCoherenceBox(HelpBox coherenceBox, PowerUpModuleKind moduleKind, PowerUpModuleStage stage)
    {
        if (coherenceBox == null)
        {
            return;
        }

        bool isCoherent = PowerUpModuleEnumDescriptions.IsStageCoherent(moduleKind, stage);

        if (isCoherent)
        {
            coherenceBox.style.display = DisplayStyle.None;
            coherenceBox.text = string.Empty;
            return;
        }

        PowerUpModuleStage recommendedStage = PowerUpModuleEnumDescriptions.GetRecommendedStage(moduleKind);
        coherenceBox.text = string.Format("Default Stage '{0}' is not coherent with Module Kind '{1}'. Recommended stage: '{2}'.",
                                          stage,
                                          moduleKind,
                                          recommendedStage);
        coherenceBox.style.display = DisplayStyle.Flex;
    }

    private static void AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
        {
            return;
        }

        if (property == null)
        {
            return;
        }

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        parent.Add(field);
    }

    private static List<PowerUpModuleKind> BuildModuleKindOptions()
    {
        List<PowerUpModuleKind> options = new List<PowerUpModuleKind>();
        IReadOnlyList<PowerUpModuleKind> moduleKindOptions = PowerUpModuleEnumDescriptions.ModuleKindOptions;

        for (int index = 0; index < moduleKindOptions.Count; index++)
        {
            options.Add(moduleKindOptions[index]);
        }

        return options;
    }

    private static List<PowerUpModuleStage> BuildStageOptions()
    {
        List<PowerUpModuleStage> options = new List<PowerUpModuleStage>();
        IReadOnlyList<PowerUpModuleStage> stageOptions = PowerUpModuleEnumDescriptions.StageOptions;

        for (int index = 0; index < stageOptions.Count; index++)
        {
            options.Add(stageOptions[index]);
        }

        return options;
    }

    private static PowerUpModuleKind ResolveModuleKind(SerializedProperty moduleKindProperty)
    {
        IReadOnlyList<PowerUpModuleKind> options = PowerUpModuleEnumDescriptions.ModuleKindOptions;

        if (moduleKindProperty == null || moduleKindProperty.propertyType != SerializedPropertyType.Enum)
        {
            return options.Count > 0 ? options[0] : default;
        }

        int enumValue = moduleKindProperty.enumValueIndex;

        for (int index = 0; index < options.Count; index++)
        {
            if ((int)options[index] != enumValue)
            {
                continue;
            }

            return options[index];
        }

        return options.Count > 0 ? options[0] : default;
    }

    private static PowerUpModuleStage ResolveStage(SerializedProperty stageProperty)
    {
        IReadOnlyList<PowerUpModuleStage> options = PowerUpModuleEnumDescriptions.StageOptions;

        if (stageProperty == null || stageProperty.propertyType != SerializedPropertyType.Enum)
        {
            return options.Count > 0 ? options[0] : default;
        }

        int enumValue = stageProperty.enumValueIndex;

        for (int index = 0; index < options.Count; index++)
        {
            if ((int)options[index] != enumValue)
            {
                continue;
            }

            return options[index];
        }

        return options.Count > 0 ? options[0] : default;
    }
    #endregion
}
