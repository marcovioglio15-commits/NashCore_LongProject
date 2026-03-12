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
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the payload editor for the provided module kind.
    /// /params payloadContainer Container that will host the payload controls.
    /// /params payloadProperty Serialized payload property to edit.
    /// /params moduleKind Module kind that selects the UI variant.
    /// /params payloadLabel Optional label used by the generic payload fallback.
    /// /returns void
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
            case PowerUpModuleKind.ProjectilesTuning:
                BuildProjectileTuningPayloadUi(payloadContainer, payloadProperty);
                return;
            case PowerUpModuleKind.ProjectilesPatternCone:
                PowerUpModuleDefinitionVisualizationUtility.BuildProjectilePatternConePayloadUi(payloadContainer, payloadProperty);
                return;
        }

        BuildDefaultPayloadUi(payloadContainer, payloadProperty, payloadLabel);
    }

    /// <summary>
    /// Creates a serialized field using the shared scaling-aware element factory.
    /// /params parent Parent visual element that receives the field.
    /// /params property Serialized property to draw.
    /// /params label Visible label for the created field.
    /// /returns void
    /// </summary>
    public static void AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return;

        if (property == null)
            return;

        SerializedProperty scalingRulesProperty = property.serializedObject != null
            ? property.serializedObject.FindProperty("scalingRules")
            : null;
        VisualElement field = PlayerScalingFieldElementFactory.CreateField(property, scalingRulesProperty, label);
        parent.Add(field);
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
        Foldout payloadFoldout = new Foldout();
        payloadFoldout.text = resolvedLabel;
        payloadFoldout.value = true;
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

        Foldout spawnFoldout = new Foldout();
        spawnFoldout.text = "Spawn";
        spawnFoldout.value = true;
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

        Foldout damageFoldout = new Foldout();
        damageFoldout.text = "Damage (Optional)";
        damageFoldout.value = true;
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
    #endregion

    #region Visibility
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
    #endregion

    #endregion
}
