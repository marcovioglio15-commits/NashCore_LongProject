using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds module payload forms that are primarily field-driven and delegates chart-heavy payloads to visualization utilities.
/// </summary>
public static class PowerUpModuleDefinitionPayloadDrawerUtility
{
    #region Constants
    private const float AvailableVariablesBoxHeight = 46f;
    #endregion

    #region Fields
    private static readonly Dictionary<string, Action> characterTuningRefreshByKey = new Dictionary<string, Action>(StringComparer.Ordinal);
    private static string activeCharacterTuningFormulaKey = string.Empty;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the payload editor for the provided module kind.
    /// payloadContainer Container that will host the payload controls.
    /// payloadProperty Serialized payload property to edit.
    /// moduleKind Module kind that selects the UI variant.
    /// payloadLabel Optional label used by the generic payload fallback.
    /// returns void
    /// </summary>
    public static void BuildPayloadEditor(VisualElement payloadContainer,
                                          SerializedProperty payloadProperty,
                                          PowerUpModuleKind moduleKind,
                                          string payloadLabel)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerHoldCharge:
                BuildHoldChargePayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.GateResource:
                BuildResourceGatePayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.ProjectileSplit:
                PowerUpModuleDefinitionVisualizationUtility.BuildProjectileSplitPayloadUi(payloadContainer, payloadProperty);
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
            case PowerUpModuleKind.CharacterTuning:
                BuildCharacterTuningPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.ProjectilesPatternCone:
                PowerUpModuleDefinitionVisualizationUtility.BuildProjectilePatternConePayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.OrbitalProjectiles:
                BuildOrbitalProjectilesPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.LaserBeam:
                BuildLaserBeamPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.Stackable:
                BuildStackablePayloadUi(payloadContainer, payloadProperty);
                return;
        }

        BuildDefaultPayloadUi(payloadContainer, payloadProperty, payloadLabel);
    }

    /// <summary>
    /// Creates a serialized field using the shared scaling-aware element factory.
    /// parent Parent visual element that receives the field.
    /// property Serialized property to draw.
    /// label Visible label for the created field.
    /// returns Created field root, or null when the input is invalid.
    /// </summary>
    public static VisualElement AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return null;

        if (property == null)
            return null;

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);
        parent.Add(field);
        return field;
    }
    #endregion

    #region Generic Payload
    private static void BuildDefaultPayloadUi(VisualElement payloadContainer,
                                              SerializedProperty payloadProperty,
                                              string payloadLabel)
    {
        if (payloadContainer == null || payloadProperty == null)
            return;

        string resolvedLabel = string.IsNullOrWhiteSpace(payloadLabel) ? payloadProperty.displayName : payloadLabel;
        Foldout payloadFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(payloadProperty,
                                                                                           resolvedLabel,
                                                                                           string.Format("Payload:{0}", resolvedLabel),
                                                                                           true);
        payloadContainer.Add(payloadFoldout);

        if (!payloadProperty.hasVisibleChildren)
        {
            AddField(payloadFoldout, payloadProperty, resolvedLabel);
            return;
        }

        SerializedProperty iterator = payloadProperty.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();
        int parentDepth = payloadProperty.depth;
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            if (SerializedProperty.EqualContents(iterator, endProperty))
                break;

            enterChildren = false;

            if (iterator.depth != parentDepth + 1)
                continue;

            SerializedProperty childProperty = iterator.Copy();
            AddField(payloadFoldout, childProperty, childProperty.displayName);
        }
    }
    #endregion

    #region Specialized Payloads
    private static void BuildHoldChargePayloadUi(VisualElement payloadContainer, SerializedProperty holdChargePayloadProperty)
    {
        if (payloadContainer == null || holdChargePayloadProperty == null)
            return;

        SerializedProperty requiredChargeProperty = holdChargePayloadProperty.FindPropertyRelative("requiredCharge");
        SerializedProperty maximumChargeProperty = holdChargePayloadProperty.FindPropertyRelative("maximumCharge");
        SerializedProperty chargeRatePerSecondProperty = holdChargePayloadProperty.FindPropertyRelative("chargeRatePerSecond");
        SerializedProperty decayAfterReleaseProperty = holdChargePayloadProperty.FindPropertyRelative("decayAfterRelease");
        SerializedProperty decayAfterReleasePercentPerSecondProperty = holdChargePayloadProperty.FindPropertyRelative("decayAfterReleasePercentPerSecond");
        SerializedProperty passiveChargeGainWhileReleasedProperty = holdChargePayloadProperty.FindPropertyRelative("passiveChargeGainWhileReleased");
        SerializedProperty passiveChargeGainPercentPerSecondProperty = holdChargePayloadProperty.FindPropertyRelative("passiveChargeGainPercentPerSecond");

        if (requiredChargeProperty == null ||
            maximumChargeProperty == null ||
            chargeRatePerSecondProperty == null ||
            decayAfterReleaseProperty == null ||
            decayAfterReleasePercentPerSecondProperty == null ||
            passiveChargeGainWhileReleasedProperty == null ||
            passiveChargeGainPercentPerSecondProperty == null)
        {
            HelpBox errorBox = new HelpBox("Hold charge payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, requiredChargeProperty, "Required Charge");
        AddField(payloadContainer, maximumChargeProperty, "Maximum Charge");
        AddField(payloadContainer, chargeRatePerSecondProperty, "Charge Rate Per Second");
        AddField(payloadContainer, decayAfterReleaseProperty, "Decay After Release");

        VisualElement decayContainer = new VisualElement();
        decayContainer.style.marginLeft = 12f;
        payloadContainer.Add(decayContainer);
        AddField(decayContainer, decayAfterReleasePercentPerSecondProperty, "Decay Percent Per Second");

        AddField(payloadContainer, passiveChargeGainWhileReleasedProperty, "Passive Gain While Released");

        VisualElement passiveGainContainer = new VisualElement();
        passiveGainContainer.style.marginLeft = 12f;
        payloadContainer.Add(passiveGainContainer);
        AddField(passiveGainContainer, passiveChargeGainPercentPerSecondProperty, "Passive Gain Percent Per Second");

        UpdateBooleanContainerVisibility(decayAfterReleaseProperty, decayContainer);
        UpdateBooleanContainerVisibility(passiveChargeGainWhileReleasedProperty, passiveGainContainer);

        payloadContainer.TrackPropertyValue(decayAfterReleaseProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, decayContainer);
        });
        payloadContainer.TrackPropertyValue(passiveChargeGainWhileReleasedProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, passiveGainContainer);
        });
    }

    private static void BuildResourceGatePayloadUi(VisualElement payloadContainer, SerializedProperty resourceGatePayloadProperty)
    {
        if (payloadContainer == null || resourceGatePayloadProperty == null)
            return;

        SerializedProperty activationResourceProperty = resourceGatePayloadProperty.FindPropertyRelative("activationResource");
        SerializedProperty maintenanceResourceProperty = resourceGatePayloadProperty.FindPropertyRelative("maintenanceResource");
        SerializedProperty maximumEnergyProperty = resourceGatePayloadProperty.FindPropertyRelative("maximumEnergy");
        SerializedProperty activationCostProperty = resourceGatePayloadProperty.FindPropertyRelative("activationCost");
        SerializedProperty maintenanceCostPerSecondProperty = resourceGatePayloadProperty.FindPropertyRelative("maintenanceCostPerSecond");
        SerializedProperty isToggleableProperty = resourceGatePayloadProperty.FindPropertyRelative("isToggleable");
        SerializedProperty maintenanceTicksPerSecondProperty = resourceGatePayloadProperty.FindPropertyRelative("maintenanceTicksPerSecond");
        SerializedProperty minimumActivationEnergyPercentProperty = resourceGatePayloadProperty.FindPropertyRelative("minimumActivationEnergyPercent");
        SerializedProperty chargeTypeProperty = resourceGatePayloadProperty.FindPropertyRelative("chargeType");
        SerializedProperty chargePerTriggerProperty = resourceGatePayloadProperty.FindPropertyRelative("chargePerTrigger");
        SerializedProperty cooldownSecondsProperty = resourceGatePayloadProperty.FindPropertyRelative("cooldownSeconds");
        SerializedProperty allowRechargeDuringToggleStartupLockProperty = resourceGatePayloadProperty.FindPropertyRelative("allowRechargeDuringToggleStartupLock");

        if (activationResourceProperty == null ||
            maintenanceResourceProperty == null ||
            maximumEnergyProperty == null ||
            activationCostProperty == null ||
            maintenanceCostPerSecondProperty == null ||
            isToggleableProperty == null ||
            maintenanceTicksPerSecondProperty == null ||
            minimumActivationEnergyPercentProperty == null ||
            chargeTypeProperty == null ||
            chargePerTriggerProperty == null ||
            cooldownSecondsProperty == null ||
            allowRechargeDuringToggleStartupLockProperty == null)
        {
            HelpBox errorBox = new HelpBox("Resource gate payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, activationResourceProperty, "Activation Resource");
        AddField(payloadContainer, maintenanceResourceProperty, "Maintenance Resource");
        AddField(payloadContainer, maximumEnergyProperty, "Maximum Energy");
        AddField(payloadContainer, activationCostProperty, "Activation Cost");
        AddField(payloadContainer, maintenanceCostPerSecondProperty, "Maintenance Cost Per Second");
        AddField(payloadContainer, minimumActivationEnergyPercentProperty, "Minimum Energy Activation Percent");
        AddField(payloadContainer, chargeTypeProperty, "Charge Type");
        AddField(payloadContainer, chargePerTriggerProperty, "Charge Per Trigger");
        AddField(payloadContainer, cooldownSecondsProperty, "Cooldown Seconds");
        AddField(payloadContainer, isToggleableProperty, "Is Toggleable");

        VisualElement toggleableContainer = new VisualElement();
        toggleableContainer.style.marginLeft = 12f;
        payloadContainer.Add(toggleableContainer);

        HelpBox toggleableHelpBox = new HelpBox("When toggleable is enabled, Cooldown Seconds becomes the startup lock interval: maintenance is not paid and the power-up cannot be disabled during that time.", HelpBoxMessageType.Info);
        toggleableContainer.Add(toggleableHelpBox);
        AddField(toggleableContainer, maintenanceTicksPerSecondProperty, "Maintenance Ticks Per Second");
        AddField(toggleableContainer, allowRechargeDuringToggleStartupLockProperty, "Allow Recharge During Startup Lock");

        UpdateBooleanContainerVisibility(isToggleableProperty, toggleableContainer);
        payloadContainer.TrackPropertyValue(isToggleableProperty, changedProperty =>
        {
            UpdateBooleanContainerVisibility(changedProperty, toggleableContainer);
        });
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
        SerializedProperty explosionVfxPrefabProperty = spawnPayloadProperty.FindPropertyRelative("explosionVfxPrefab");
        SerializedProperty scaleVfxToRadiusProperty = spawnPayloadProperty.FindPropertyRelative("scaleVfxToRadius");
        SerializedProperty vfxScaleMultiplierProperty = spawnPayloadProperty.FindPropertyRelative("vfxScaleMultiplier");

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
            affectAllEnemiesInRadiusProperty == null ||
            explosionVfxPrefabProperty == null ||
            scaleVfxToRadiusProperty == null ||
            vfxScaleMultiplierProperty == null)
        {
            HelpBox errorBox = new HelpBox("Spawn object payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        Foldout spawnFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(spawnPayloadProperty,
                                                                                         "Spawn",
                                                                                         "SpawnPayload",
                                                                                         true);
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

        Foldout damageFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(spawnPayloadProperty,
                                                                                          "Damage (Optional)",
                                                                                          "SpawnPayloadDamage",
                                                                                          true);
        spawnFoldout.Add(damageFoldout);
        AddField(damageFoldout, enableDamagePayloadProperty, "Enable Damage Payload");

        VisualElement damageContainer = new VisualElement();
        damageContainer.style.marginLeft = 12f;
        damageFoldout.Add(damageContainer);
        AddField(damageContainer, radiusProperty, "Radius");
        AddField(damageContainer, damageProperty, "Damage");
        AddField(damageContainer, affectAllEnemiesInRadiusProperty, "Affect All Enemies In Radius");
        AddField(damageContainer, explosionVfxPrefabProperty, "Explosion VFX Prefab");
        AddField(damageContainer, scaleVfxToRadiusProperty, "Scale VFX To Radius");
        AddField(damageContainer, vfxScaleMultiplierProperty, "VFX Scale Multiplier");

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

    private static void BuildCharacterTuningPayloadUi(VisualElement payloadContainer, SerializedProperty characterTuningPayloadProperty)
    {
        if (payloadContainer == null || characterTuningPayloadProperty == null)
            return;

        SerializedObject serializedObject = characterTuningPayloadProperty.serializedObject;
        SerializedProperty formulasProperty = characterTuningPayloadProperty.FindPropertyRelative("formulas");

        if (formulasProperty == null)
        {
            HelpBox errorBox = new HelpBox("Character Tuning payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        HelpBox infoBox = new HelpBox("Each entry uses [TargetStat] = expression syntax. The right-hand expression supports the same operators and functions available in Add Scaling formulas, including switch(condition, case:value, ..., fallback).", HelpBoxMessageType.Info);
        payloadContainer.Add(infoBox);
        VisualElement formulasField = AddField(payloadContainer, formulasProperty, "Acquisition Formulas");
        ScrollView availableVariablesScrollView = new ScrollView(ScrollViewMode.Vertical);
        availableVariablesScrollView.style.marginTop = 2f;
        availableVariablesScrollView.style.height = AvailableVariablesBoxHeight;
        availableVariablesScrollView.style.maxHeight = AvailableVariablesBoxHeight;
        availableVariablesScrollView.style.flexShrink = 0f;
        payloadContainer.Add(availableVariablesScrollView);

        Label availableVariablesLabel = new Label(string.Empty);
        availableVariablesLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        availableVariablesLabel.style.whiteSpace = WhiteSpace.Normal;
        availableVariablesLabel.style.flexShrink = 0f;
        availableVariablesScrollView.Add(availableVariablesLabel);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(warningBox);
        string formulasPropertyPath = formulasProperty.propertyPath;
        string formulaKey = BuildCharacterTuningFormulaKey(serializedObject, formulasPropertyPath);
        RegisterCharacterTuningRefresher(formulaKey, RefreshCharacterTuningUi);
        payloadContainer.RegisterCallback<DetachFromPanelEvent>(evt => UnregisterCharacterTuningRefresher(formulaKey));
        payloadContainer.RegisterCallback<MouseDownEvent>(evt =>
        {
            SetActiveCharacterTuningFormula(formulaKey);
        });
        payloadContainer.RegisterCallback<FocusOutEvent>(evt =>
        {
            if (evt.relatedTarget is VisualElement nextFocusedElement && payloadContainer.Contains(nextFocusedElement))
                return;

            ClearActiveCharacterTuningFormula(formulaKey);
        });

        if (formulasField != null)
        {
            formulasField.RegisterCallback<FocusInEvent>(evt =>
            {
                SetActiveCharacterTuningFormula(formulaKey);
            });
            formulasField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                SetActiveCharacterTuningFormula(formulaKey);
            });
        }

        RefreshCharacterTuningUi();
        RegisterCharacterTuningFormulaRefresh(payloadContainer,
                                              serializedObject,
                                              formulasPropertyPath,
                                              RefreshCharacterTuningUi);

        void RefreshCharacterTuningUi()
        {
            SerializedProperty reboundFormulasProperty = serializedObject != null
                ? serializedObject.FindProperty(formulasPropertyPath)
                : null;
            RefreshCharacterTuningAvailableVariables(serializedObject, availableVariablesLabel);
            RefreshCharacterTuningWarnings(serializedObject, reboundFormulasProperty, warningBox);
            availableVariablesScrollView.style.display = IsActiveCharacterTuningFormula(formulaKey)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }

    private static void BuildStackablePayloadUi(VisualElement payloadContainer, SerializedProperty stackablePayloadProperty)
    {
        if (payloadContainer == null || stackablePayloadProperty == null)
            return;

        SerializedProperty maxAcquisitionsProperty = stackablePayloadProperty.FindPropertyRelative("maxAcquisitions");

        if (maxAcquisitionsProperty == null)
        {
            HelpBox errorBox = new HelpBox("Stackable payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        HelpBox infoBox = new HelpBox("Stackable controls how many times the same power-up can be acquired from milestones. Pair it with Character Tuning so repeated pickups have a meaningful acquisition effect.", HelpBoxMessageType.Info);
        payloadContainer.Add(infoBox);
        AddField(payloadContainer, maxAcquisitionsProperty, "Max Acquisitions");
    }

    private static void BuildOrbitalProjectilesPayloadUi(VisualElement payloadContainer, SerializedProperty orbitalPayloadProperty)
    {
        if (payloadContainer == null || orbitalPayloadProperty == null)
            return;

        SerializedProperty pathModeProperty = orbitalPayloadProperty.FindPropertyRelative("pathMode");
        SerializedProperty radialEntrySpeedProperty = orbitalPayloadProperty.FindPropertyRelative("radialEntrySpeed");
        SerializedProperty heightOffsetProperty = orbitalPayloadProperty.FindPropertyRelative("heightOffset");
        SerializedProperty goldenAngleDegreesProperty = orbitalPayloadProperty.FindPropertyRelative("goldenAngleDegrees");
        SerializedProperty orbitalSpeedProperty = orbitalPayloadProperty.FindPropertyRelative("orbitalSpeed");
        SerializedProperty orbitRadiusMinProperty = orbitalPayloadProperty.FindPropertyRelative("orbitRadiusMin");
        SerializedProperty orbitRadiusMaxProperty = orbitalPayloadProperty.FindPropertyRelative("orbitRadiusMax");
        SerializedProperty orbitPulseFrequencyProperty = orbitalPayloadProperty.FindPropertyRelative("orbitPulseFrequency");
        SerializedProperty orbitEntryRatioProperty = orbitalPayloadProperty.FindPropertyRelative("orbitEntryRatio");
        SerializedProperty orbitBlendDurationProperty = orbitalPayloadProperty.FindPropertyRelative("orbitBlendDuration");
        SerializedProperty spiralStartRadiusProperty = orbitalPayloadProperty.FindPropertyRelative("spiralStartRadius");
        SerializedProperty spiralMaximumRadiusProperty = orbitalPayloadProperty.FindPropertyRelative("spiralMaximumRadius");
        SerializedProperty spiralAngularSpeedDegreesPerSecondProperty = orbitalPayloadProperty.FindPropertyRelative("spiralAngularSpeedDegreesPerSecond");
        SerializedProperty spiralGrowthMultiplierProperty = orbitalPayloadProperty.FindPropertyRelative("spiralGrowthMultiplier");
        SerializedProperty spiralTurnsBeforeDespawnProperty = orbitalPayloadProperty.FindPropertyRelative("spiralTurnsBeforeDespawn");
        SerializedProperty spiralClockwiseProperty = orbitalPayloadProperty.FindPropertyRelative("spiralClockwise");

        if (pathModeProperty == null ||
            radialEntrySpeedProperty == null ||
            heightOffsetProperty == null ||
            goldenAngleDegreesProperty == null ||
            orbitalSpeedProperty == null ||
            orbitRadiusMinProperty == null ||
            orbitRadiusMaxProperty == null ||
            orbitPulseFrequencyProperty == null ||
            orbitEntryRatioProperty == null ||
            orbitBlendDurationProperty == null ||
            spiralStartRadiusProperty == null ||
            spiralMaximumRadiusProperty == null ||
            spiralAngularSpeedDegreesPerSecondProperty == null ||
            spiralGrowthMultiplierProperty == null ||
            spiralTurnsBeforeDespawnProperty == null ||
            spiralClockwiseProperty == null)
        {
            HelpBox errorBox = new HelpBox("Orbital projectiles payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        AddField(payloadContainer, pathModeProperty, "Path Mode");
        AddField(payloadContainer, radialEntrySpeedProperty, "Radial Entry Speed");
        AddField(payloadContainer, heightOffsetProperty, "Height Offset");
        AddField(payloadContainer, goldenAngleDegreesProperty, "Golden Angle Degrees");

        VisualElement circleContainer = new VisualElement();
        circleContainer.style.marginLeft = 12f;
        payloadContainer.Add(circleContainer);
        AddField(circleContainer, orbitalSpeedProperty, "Orbital Speed");
        AddField(circleContainer, orbitRadiusMinProperty, "Orbit Radius Min");
        AddField(circleContainer, orbitRadiusMaxProperty, "Orbit Radius Max");
        AddField(circleContainer, orbitPulseFrequencyProperty, "Orbit Pulse Frequency");
        AddField(circleContainer, orbitEntryRatioProperty, "Orbit Entry Ratio");
        AddField(circleContainer, orbitBlendDurationProperty, "Orbit Blend Duration");

        VisualElement spiralContainer = new VisualElement();
        spiralContainer.style.marginLeft = 12f;
        payloadContainer.Add(spiralContainer);
        AddField(spiralContainer, spiralStartRadiusProperty, "Spiral Start Radius");
        AddField(spiralContainer, spiralMaximumRadiusProperty, "Spiral Maximum Radius");
        AddField(spiralContainer, spiralAngularSpeedDegreesPerSecondProperty, "Spiral Angular Speed Degrees Per Second");
        AddField(spiralContainer, spiralGrowthMultiplierProperty, "Spiral Growth Multiplier");
        AddField(spiralContainer, spiralTurnsBeforeDespawnProperty, "Spiral Turns Before Despawn");
        AddField(spiralContainer, spiralClockwiseProperty, "Spiral Clockwise");

        UpdateOrbitPathModeContainers(pathModeProperty, circleContainer, spiralContainer);
        payloadContainer.TrackPropertyValue(pathModeProperty, changedProperty =>
        {
            UpdateOrbitPathModeContainers(changedProperty, circleContainer, spiralContainer);
        });
    }

    private static void BuildLaserBeamPayloadUi(VisualElement payloadContainer, SerializedProperty laserBeamPayloadProperty)
    {
        if (payloadContainer == null || laserBeamPayloadProperty == null)
            return;

        SerializedProperty damageMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("damageMultiplier");
        SerializedProperty virtualProjectileSpeedMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("virtualProjectileSpeedMultiplier");
        SerializedProperty damageTickIntervalSecondsProperty = laserBeamPayloadProperty.FindPropertyRelative("damageTickIntervalSeconds");
        SerializedProperty maximumContinuousActiveSecondsProperty = laserBeamPayloadProperty.FindPropertyRelative("maximumContinuousActiveSeconds");
        SerializedProperty cooldownSecondsProperty = laserBeamPayloadProperty.FindPropertyRelative("cooldownSeconds");
        SerializedProperty maximumBounceSegmentsProperty = laserBeamPayloadProperty.FindPropertyRelative("maximumBounceSegments");
        SerializedProperty visualPaletteProperty = laserBeamPayloadProperty.FindPropertyRelative("visualPalette");
        SerializedProperty bodyProfileProperty = laserBeamPayloadProperty.FindPropertyRelative("bodyProfile");
        SerializedProperty sourceShapeProperty = laserBeamPayloadProperty.FindPropertyRelative("sourceShape");
        SerializedProperty impactShapeProperty = laserBeamPayloadProperty.FindPropertyRelative("impactShape");
        SerializedProperty bodyWidthMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("bodyWidthMultiplier");
        SerializedProperty collisionWidthMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("collisionWidthMultiplier");
        SerializedProperty sourceScaleMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("sourceScaleMultiplier");
        SerializedProperty impactScaleMultiplierProperty = laserBeamPayloadProperty.FindPropertyRelative("impactScaleMultiplier");
        SerializedProperty bodyOpacityProperty = laserBeamPayloadProperty.FindPropertyRelative("bodyOpacity");
        SerializedProperty coreBrightnessProperty = laserBeamPayloadProperty.FindPropertyRelative("coreBrightness");
        SerializedProperty rimBrightnessProperty = laserBeamPayloadProperty.FindPropertyRelative("rimBrightness");
        SerializedProperty flowScrollSpeedProperty = laserBeamPayloadProperty.FindPropertyRelative("flowScrollSpeed");
        SerializedProperty flowPulseFrequencyProperty = laserBeamPayloadProperty.FindPropertyRelative("flowPulseFrequency");
        SerializedProperty wobbleAmplitudeProperty = laserBeamPayloadProperty.FindPropertyRelative("wobbleAmplitude");
        SerializedProperty bubbleDriftSpeedProperty = laserBeamPayloadProperty.FindPropertyRelative("bubbleDriftSpeed");

        if (damageMultiplierProperty == null ||
            virtualProjectileSpeedMultiplierProperty == null ||
            damageTickIntervalSecondsProperty == null ||
            maximumContinuousActiveSecondsProperty == null ||
            cooldownSecondsProperty == null ||
            maximumBounceSegmentsProperty == null ||
            visualPaletteProperty == null ||
            bodyProfileProperty == null ||
            sourceShapeProperty == null ||
            impactShapeProperty == null ||
            bodyWidthMultiplierProperty == null ||
            collisionWidthMultiplierProperty == null ||
            sourceScaleMultiplierProperty == null ||
            impactScaleMultiplierProperty == null ||
            bodyOpacityProperty == null ||
            coreBrightnessProperty == null ||
            rimBrightnessProperty == null ||
            flowScrollSpeedProperty == null ||
            flowPulseFrequencyProperty == null ||
            wobbleAmplitudeProperty == null ||
            bubbleDriftSpeedProperty == null)
        {
            HelpBox errorBox = new HelpBox("Laser Beam payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(errorBox);
            return;
        }

        HelpBox infoBox = new HelpBox("Laser Beam overrides base projectile spawning while the Shoot input is held. It always behaves as hold-to-fire, even if the current controller shooting trigger mode uses single-shot or toggle semantics.", HelpBoxMessageType.Info);
        payloadContainer.Add(infoBox);

        HelpBox warningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        payloadContainer.Add(warningBox);

        Foldout gameplayFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                            "Gameplay",
                                                                                            "LaserBeamPayloadGameplay",
                                                                                            true);
        payloadContainer.Add(gameplayFoldout);
        AddField(gameplayFoldout, damageMultiplierProperty, "Damage Multiplier");
        AddField(gameplayFoldout, virtualProjectileSpeedMultiplierProperty, "Virtual Projectile Speed Multiplier");
        AddField(gameplayFoldout, damageTickIntervalSecondsProperty, "Damage Tick Interval Seconds");
        AddField(gameplayFoldout, cooldownSecondsProperty, "Cooldown Seconds");

        VisualElement cooldownContainer = new VisualElement();
        cooldownContainer.style.marginLeft = 12f;
        gameplayFoldout.Add(cooldownContainer);
        AddField(cooldownContainer, maximumContinuousActiveSecondsProperty, "Maximum Continuous Active Seconds");

        AddField(gameplayFoldout, maximumBounceSegmentsProperty, "Maximum Bounce Segments");

        Foldout visualsFoldout = PlayerManagementFoldoutStateUtility.CreatePropertyFoldout(laserBeamPayloadProperty,
                                                                                           "Presentation",
                                                                                           "LaserBeamPayloadPresentation",
                                                                                           true);
        payloadContainer.Add(visualsFoldout);
        AddField(visualsFoldout, visualPaletteProperty, "Visual Palette");
        AddField(visualsFoldout, bodyProfileProperty, "Body Profile");
        AddField(visualsFoldout, sourceShapeProperty, "Source Shape");
        AddField(visualsFoldout, impactShapeProperty, "Impact Shape");
        AddField(visualsFoldout, bodyWidthMultiplierProperty, "Body Width Multiplier");
        AddField(visualsFoldout, collisionWidthMultiplierProperty, "Collision Width Multiplier");
        AddField(visualsFoldout, sourceScaleMultiplierProperty, "Source Scale Multiplier");
        AddField(visualsFoldout, impactScaleMultiplierProperty, "Impact Scale Multiplier");
        AddField(visualsFoldout, bodyOpacityProperty, "Body Opacity");
        AddField(visualsFoldout, coreBrightnessProperty, "Core Brightness");
        AddField(visualsFoldout, rimBrightnessProperty, "Rim Brightness");
        AddField(visualsFoldout, flowScrollSpeedProperty, "Flow Scroll Speed");
        AddField(visualsFoldout, flowPulseFrequencyProperty, "Flow Pulse Frequency");
        AddField(visualsFoldout, wobbleAmplitudeProperty, "Wobble Amplitude");
        AddField(visualsFoldout, bubbleDriftSpeedProperty, "Bubble Drift Speed");

        UpdateLaserBeamCooldownVisibility(cooldownSecondsProperty, cooldownContainer);
        RefreshLaserBeamWarnings(cooldownSecondsProperty,
                                 damageTickIntervalSecondsProperty,
                                 maximumContinuousActiveSecondsProperty,
                                 bodyWidthMultiplierProperty,
                                 collisionWidthMultiplierProperty,
                                 sourceScaleMultiplierProperty,
                                 impactScaleMultiplierProperty,
                                 bodyOpacityProperty,
                                 warningBox);

        payloadContainer.TrackPropertyValue(cooldownSecondsProperty, changedProperty =>
        {
            UpdateLaserBeamCooldownVisibility(changedProperty, cooldownContainer);
            RefreshLaserBeamWarnings(changedProperty,
                                     damageTickIntervalSecondsProperty,
                                     maximumContinuousActiveSecondsProperty,
                                     bodyWidthMultiplierProperty,
                                     collisionWidthMultiplierProperty,
                                     sourceScaleMultiplierProperty,
                                     impactScaleMultiplierProperty,
                                     bodyOpacityProperty,
                                     warningBox);
        });
        payloadContainer.TrackPropertyValue(damageTickIntervalSecondsProperty, changedProperty =>
        {
            RefreshLaserBeamWarnings(cooldownSecondsProperty,
                                     changedProperty,
                                     maximumContinuousActiveSecondsProperty,
                                     bodyWidthMultiplierProperty,
                                     collisionWidthMultiplierProperty,
                                     sourceScaleMultiplierProperty,
                                     impactScaleMultiplierProperty,
                                     bodyOpacityProperty,
                                     warningBox);
        });
        payloadContainer.TrackPropertyValue(maximumContinuousActiveSecondsProperty, changedProperty =>
        {
            RefreshLaserBeamWarnings(cooldownSecondsProperty,
                                     damageTickIntervalSecondsProperty,
                                     changedProperty,
                                     bodyWidthMultiplierProperty,
                                     collisionWidthMultiplierProperty,
                                     sourceScaleMultiplierProperty,
                                     impactScaleMultiplierProperty,
                                     bodyOpacityProperty,
                                     warningBox);
        });
        payloadContainer.TrackPropertyValue(bodyWidthMultiplierProperty, changedProperty =>
        {
            RefreshLaserBeamWarnings(cooldownSecondsProperty,
                                     damageTickIntervalSecondsProperty,
                                     maximumContinuousActiveSecondsProperty,
                                     changedProperty,
                                     collisionWidthMultiplierProperty,
                                     sourceScaleMultiplierProperty,
                                     impactScaleMultiplierProperty,
                                     bodyOpacityProperty,
                                     warningBox);
        });
        payloadContainer.TrackPropertyValue(collisionWidthMultiplierProperty, changedProperty =>
        {
            RefreshLaserBeamWarnings(cooldownSecondsProperty,
                                     damageTickIntervalSecondsProperty,
                                     maximumContinuousActiveSecondsProperty,
                                     bodyWidthMultiplierProperty,
                                     changedProperty,
                                     sourceScaleMultiplierProperty,
                                     impactScaleMultiplierProperty,
                                     bodyOpacityProperty,
                                     warningBox);
        });
        payloadContainer.TrackPropertyValue(sourceScaleMultiplierProperty, changedProperty =>
        {
            RefreshLaserBeamWarnings(cooldownSecondsProperty,
                                     damageTickIntervalSecondsProperty,
                                     maximumContinuousActiveSecondsProperty,
                                     bodyWidthMultiplierProperty,
                                     collisionWidthMultiplierProperty,
                                     changedProperty,
                                     impactScaleMultiplierProperty,
                                     bodyOpacityProperty,
                                     warningBox);
        });
        payloadContainer.TrackPropertyValue(impactScaleMultiplierProperty, changedProperty =>
        {
            RefreshLaserBeamWarnings(cooldownSecondsProperty,
                                     damageTickIntervalSecondsProperty,
                                     maximumContinuousActiveSecondsProperty,
                                     bodyWidthMultiplierProperty,
                                     collisionWidthMultiplierProperty,
                                     sourceScaleMultiplierProperty,
                                     changedProperty,
                                     bodyOpacityProperty,
                                     warningBox);
        });
        payloadContainer.TrackPropertyValue(bodyOpacityProperty, changedProperty =>
        {
            RefreshLaserBeamWarnings(cooldownSecondsProperty,
                                     damageTickIntervalSecondsProperty,
                                     maximumContinuousActiveSecondsProperty,
                                     bodyWidthMultiplierProperty,
                                     collisionWidthMultiplierProperty,
                                     sourceScaleMultiplierProperty,
                                     impactScaleMultiplierProperty,
                                     changedProperty,
                                     warningBox);
        });
    }
    #endregion

    #region Visibility
    private static void UpdateBooleanContainerVisibility(SerializedProperty toggleProperty, VisualElement container)
    {
        if (container == null)
            return;

        if (toggleProperty == null)
        {
            container.style.display = DisplayStyle.None;
            return;
        }

        container.style.display = toggleProperty.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
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

    private static void UpdateLaserBeamCooldownVisibility(SerializedProperty cooldownSecondsProperty, VisualElement cooldownContainer)
    {
        if (cooldownContainer == null)
            return;

        if (cooldownSecondsProperty == null)
        {
            cooldownContainer.style.display = DisplayStyle.None;
            return;
        }

        cooldownContainer.style.display = cooldownSecondsProperty.floatValue > 0f ? DisplayStyle.Flex : DisplayStyle.None;
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

    private static void RefreshCharacterTuningWarnings(SerializedObject serializedObject,
                                                       SerializedProperty formulasProperty,
                                                       HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        if (serializedObject == null || formulasProperty == null || !formulasProperty.isArray)
        {
            warningBox.text = "Character Tuning formulas are not available.";
            warningBox.style.display = DisplayStyle.Flex;
            return;
        }

        List<string> warningLines = new List<string>();
        HashSet<string> allowedVariables = PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject);
        Dictionary<string, PlayerFormulaValueType> variableTypes = PlayerScalingFormulaValidationUtility.BuildScopedVariableTypeMap(serializedObject);

        if (formulasProperty.arraySize <= 0)
            warningLines.Add("No acquisition formula configured. Character Tuning currently has no effect.");

        for (int formulaIndex = 0; formulaIndex < formulasProperty.arraySize; formulaIndex++)
        {
            SerializedProperty formulaEntryProperty = formulasProperty.GetArrayElementAtIndex(formulaIndex);

            if (formulaEntryProperty == null)
            {
                warningLines.Add(string.Format("Formula #{0} is missing.", formulaIndex + 1));
                continue;
            }

            SerializedProperty formulaProperty = formulaEntryProperty.FindPropertyRelative("formula");

            if (formulaProperty == null || formulaProperty.propertyType != SerializedPropertyType.String)
            {
                warningLines.Add(string.Format("Formula #{0} payload is invalid.", formulaIndex + 1));
                continue;
            }

            string formulaValue = formulaProperty.stringValue;

            if (string.IsNullOrWhiteSpace(formulaValue))
            {
                warningLines.Add(string.Format("Formula #{0} is empty.", formulaIndex + 1));
                continue;
            }

            if (PlayerCharacterTuningFormulaValidationUtility.TryValidateAssignmentFormula(formulaValue,
                                                                                          allowedVariables,
                                                                                          variableTypes,
                                                                                          out string warningMessage))
            {
                continue;
            }

            warningLines.Add(string.Format("Formula #{0}: {1}", formulaIndex + 1, warningMessage));
        }

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    private static void RefreshLaserBeamWarnings(SerializedProperty cooldownSecondsProperty,
                                                 SerializedProperty damageTickIntervalSecondsProperty,
                                                 SerializedProperty maximumContinuousActiveSecondsProperty,
                                                 SerializedProperty bodyWidthMultiplierProperty,
                                                 SerializedProperty collisionWidthMultiplierProperty,
                                                 SerializedProperty sourceScaleMultiplierProperty,
                                                 SerializedProperty impactScaleMultiplierProperty,
                                                 SerializedProperty bodyOpacityProperty,
                                                 HelpBox warningBox)
    {
        if (warningBox == null)
            return;

        List<string> warningLines = new List<string>();

        if (damageTickIntervalSecondsProperty != null && damageTickIntervalSecondsProperty.floatValue <= 0f)
            warningLines.Add("Damage Tick Interval Seconds should be > 0.");

        if (cooldownSecondsProperty != null && cooldownSecondsProperty.floatValue <= 0f)
        {
            if (maximumContinuousActiveSecondsProperty != null && maximumContinuousActiveSecondsProperty.floatValue > 0f)
                warningLines.Add("Maximum Continuous Active Seconds is ignored while Cooldown Seconds is 0.");
        }
        else if (maximumContinuousActiveSecondsProperty != null && maximumContinuousActiveSecondsProperty.floatValue <= 0f)
        {
            warningLines.Add("Maximum Continuous Active Seconds should be > 0 when Cooldown Seconds is enabled.");
        }

        if (bodyWidthMultiplierProperty != null && bodyWidthMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Body Width Multiplier should be > 0.");

        if (collisionWidthMultiplierProperty != null && collisionWidthMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Collision Width Multiplier should be > 0.");

        if (sourceScaleMultiplierProperty != null && sourceScaleMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Source Scale Multiplier should be > 0.");

        if (impactScaleMultiplierProperty != null && impactScaleMultiplierProperty.floatValue <= 0f)
            warningLines.Add("Impact Scale Multiplier should be > 0.");

        if (bodyOpacityProperty != null && bodyOpacityProperty.floatValue <= 0f)
            warningLines.Add("Body Opacity should be > 0.");

        if (warningLines.Count <= 0)
        {
            warningBox.text = string.Empty;
            warningBox.style.display = DisplayStyle.None;
            return;
        }

        warningBox.text = string.Join("\n", warningLines);
        warningBox.style.display = DisplayStyle.Flex;
    }

    private static void RefreshCharacterTuningAvailableVariables(SerializedObject serializedObject, Label availableVariablesLabel)
    {
        if (availableVariablesLabel == null)
            return;

        HashSet<string> allowedVariables = serializedObject != null
            ? PlayerScalingFormulaValidationUtility.BuildScopedVariableSet(serializedObject)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, PlayerScalableStatType> variableTypes = serializedObject != null
            ? PlayerScalingFormulaValidationUtility.BuildScopedScalableStatTypeMap(serializedObject)
            : new Dictionary<string, PlayerScalableStatType>(StringComparer.OrdinalIgnoreCase);

        availableVariablesLabel.text = PlayerScalingFormulaValidationUtility.BuildAvailableVariablesLabelText(allowedVariables, variableTypes);
    }

    /// <summary>
    /// Refreshes Character Tuning helper UI only when the local formulas payload changes, avoiding global serialized-object watchers on reorderable cards.
    /// payloadContainer Parent element that receives bubbled serialized change events.
    /// serializedObject Serialized object that owns the formulas payload.
    /// formulasPropertyPath Property path of the formulas array to re-resolve after local edits.
    /// refreshUi Callback that rebinds helper text and warnings after a local formulas edit.
    /// returns void
    /// </summary>
    private static void RegisterCharacterTuningFormulaRefresh(VisualElement payloadContainer,
                                                              SerializedObject serializedObject,
                                                              string formulasPropertyPath,
                                                              Action refreshUi)
    {
        if (payloadContainer == null)
            return;

        if (serializedObject == null)
            return;

        if (string.IsNullOrWhiteSpace(formulasPropertyPath))
            return;

        payloadContainer.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
        {
            if (evt == null || evt.changedProperty == null)
                return;

            string changedPath = evt.changedProperty.propertyPath;

            if (string.IsNullOrWhiteSpace(changedPath))
                return;

            if (!string.Equals(changedPath, formulasPropertyPath, StringComparison.Ordinal) &&
                !changedPath.StartsWith(formulasPropertyPath + ".Array.data[", StringComparison.Ordinal))
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            refreshUi?.Invoke();
        });
    }

    private static string BuildCharacterTuningFormulaKey(SerializedObject serializedObject, string formulasPropertyPath)
    {
        if (serializedObject == null || serializedObject.targetObject == null)
            return formulasPropertyPath ?? string.Empty;

        return string.Format("{0}:{1}",
                             serializedObject.targetObject.GetInstanceID(),
                             formulasPropertyPath ?? string.Empty);
    }

    private static bool IsActiveCharacterTuningFormula(string formulaKey)
    {
        return !string.IsNullOrWhiteSpace(formulaKey) &&
               string.Equals(activeCharacterTuningFormulaKey, formulaKey, StringComparison.Ordinal);
    }

    private static void SetActiveCharacterTuningFormula(string formulaKey)
    {
        if (string.IsNullOrWhiteSpace(formulaKey))
            return;

        activeCharacterTuningFormulaKey = formulaKey;
        RefreshRegisteredCharacterTuningFormulas();
    }

    private static void ClearActiveCharacterTuningFormula(string formulaKey)
    {
        if (string.IsNullOrWhiteSpace(formulaKey))
            return;

        if (!string.Equals(activeCharacterTuningFormulaKey, formulaKey, StringComparison.Ordinal))
            return;

        activeCharacterTuningFormulaKey = string.Empty;
        RefreshRegisteredCharacterTuningFormulas();
    }

    private static void RegisterCharacterTuningRefresher(string formulaKey, Action refreshUi)
    {
        if (string.IsNullOrWhiteSpace(formulaKey) || refreshUi == null)
            return;

        characterTuningRefreshByKey[formulaKey] = refreshUi;
    }

    private static void UnregisterCharacterTuningRefresher(string formulaKey)
    {
        if (string.IsNullOrWhiteSpace(formulaKey))
            return;

        characterTuningRefreshByKey.Remove(formulaKey);
        ClearActiveCharacterTuningFormula(formulaKey);
    }

    private static void RefreshRegisteredCharacterTuningFormulas()
    {
        foreach (Action refreshUi in characterTuningRefreshByKey.Values)
            refreshUi?.Invoke();
    }

    private static void UpdateOrbitPathModeContainers(SerializedProperty pathModeProperty,
                                                      VisualElement circleContainer,
                                                      VisualElement spiralContainer)
    {
        ProjectileOrbitPathMode pathMode = ProjectileOrbitPathMode.Circle;

        if (pathModeProperty != null)
            pathMode = (ProjectileOrbitPathMode)pathModeProperty.enumValueIndex;

        if (circleContainer != null)
            circleContainer.style.display = pathMode == ProjectileOrbitPathMode.Circle ? DisplayStyle.Flex : DisplayStyle.None;

        if (spiralContainer != null)
            spiralContainer.style.display = pathMode == ProjectileOrbitPathMode.GoldenSpiral ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
