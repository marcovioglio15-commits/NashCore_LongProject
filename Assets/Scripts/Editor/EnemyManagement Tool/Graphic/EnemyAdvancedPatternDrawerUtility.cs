using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Provides shared editor helpers for Enemy Advanced Pattern property drawers.
/// </summary>
public static class EnemyAdvancedPatternDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds a bound property field to a parent container.
    /// </summary>
    /// <param name="parent">Parent visual element that receives the field.</param>
    /// <param name="property">Serialized property to bind.</param>
    /// <param name="label">UI label text.</param>
    /// <returns>Returns true when the field is successfully added.</returns>
    public static bool AddField(VisualElement parent, SerializedProperty property, string label)
    {
        if (parent == null)
            return false;

        if (property == null)
            return false;

        PropertyField field = new PropertyField(property, label);
        field.BindProperty(property);
        parent.Add(field);
        return true;
    }

    /// <summary>
    /// Resolves module kind enum from serialized property with fallback.
    /// </summary>
    /// <param name="moduleKindProperty">Serialized enum property.</param>
    /// <returns>Returns a valid module kind value.</returns>
    public static EnemyPatternModuleKind ResolveModuleKind(SerializedProperty moduleKindProperty)
    {
        if (moduleKindProperty == null)
            return EnemyPatternModuleKind.Grunt;

        if (moduleKindProperty.propertyType != SerializedPropertyType.Enum)
            return EnemyPatternModuleKind.Grunt;

        int enumValue = moduleKindProperty.enumValueIndex;

        if (Enum.IsDefined(typeof(EnemyPatternModuleKind), enumValue) == false)
            return EnemyPatternModuleKind.Grunt;

        return (EnemyPatternModuleKind)enumValue;
    }

    /// <summary>
    /// Builds and refreshes payload UI for a specific module kind.
    /// </summary>
    /// <param name="payloadDataProperty">Serialized payload data root.</param>
    /// <param name="moduleKind">Target module kind.</param>
    /// <param name="payloadContainer">Container to rebuild.</param>
    /// <returns>Returns true when payload UI is built.</returns>
    public static bool RefreshPayloadEditor(SerializedProperty payloadDataProperty,
                                            EnemyPatternModuleKind moduleKind,
                                            VisualElement payloadContainer)
    {
        if (payloadContainer == null)
            return false;

        payloadContainer.Clear();

        if (payloadDataProperty == null)
        {
            HelpBox missingPayloadBox = new HelpBox("Payload data property is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingPayloadBox);
            return false;
        }

        switch (moduleKind)
        {
            case EnemyPatternModuleKind.Stationary:
                return BuildStationaryPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Wanderer:
                return BuildWandererPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Shooter:
                return BuildShooterPayloadEditor(payloadDataProperty, payloadContainer);

            case EnemyPatternModuleKind.Grunt:
                HelpBox noPayloadBox = new HelpBox("No payload is required for this module kind.", HelpBoxMessageType.Info);
                payloadContainer.Add(noPayloadBox);
                return true;

            default:
                HelpBox unsupportedBox = new HelpBox("Unsupported module kind.", HelpBoxMessageType.Warning);
                payloadContainer.Add(unsupportedBox);
                return false;
        }
    }

    /// <summary>
    /// Builds module ID options from the current preset module catalog.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing moduleDefinitions.</param>
    /// <returns>Returns distinct module IDs preserving list order.</returns>
    public static List<string> BuildModuleIdOptions(SerializedObject serializedObject)
    {
        List<string> options = new List<string>();

        if (serializedObject == null)
            return options;

        SerializedProperty moduleDefinitionsProperty = serializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
            return options;

        for (int index = 0; index < moduleDefinitionsProperty.arraySize; index++)
        {
            SerializedProperty moduleDefinitionProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(index);

            if (moduleDefinitionProperty == null)
                continue;

            SerializedProperty moduleIdProperty = moduleDefinitionProperty.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            string moduleId = moduleIdProperty.stringValue;

            if (string.IsNullOrWhiteSpace(moduleId))
                continue;

            if (ContainsOption(options, moduleId))
                continue;

            options.Add(moduleId);
        }

        return options;
    }

    /// <summary>
    /// Resolves current module ID to a valid option value.
    /// </summary>
    /// <param name="currentModuleId">Current module ID string value.</param>
    /// <param name="options">Available module options.</param>
    /// <returns>Returns selected module ID guaranteed to be in options when options are present.</returns>
    public static string ResolveInitialModuleId(string currentModuleId, List<string> options)
    {
        if (options == null || options.Count == 0)
            return string.Empty;

        for (int index = 0; index < options.Count; index++)
        {
            string option = options[index];

            if (string.Equals(option, currentModuleId, StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return options[0];
    }

    /// <summary>
    /// Resolves module metadata for a specific module ID.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing moduleDefinitions.</param>
    /// <param name="moduleId">Module ID to resolve.</param>
    /// <param name="moduleKind">Resolved module kind.</param>
    /// <param name="displayName">Resolved module display name.</param>
    /// <returns>Returns true when module definition is found.</returns>
    public static bool TryResolveModuleInfo(SerializedObject serializedObject,
                                            string moduleId,
                                            out EnemyPatternModuleKind moduleKind,
                                            out string displayName)
    {
        moduleKind = EnemyPatternModuleKind.Grunt;
        displayName = string.Empty;

        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        SerializedProperty moduleDefinitionsProperty = serializedObject.FindProperty("moduleDefinitions");

        if (moduleDefinitionsProperty == null)
            return false;

        for (int index = 0; index < moduleDefinitionsProperty.arraySize; index++)
        {
            SerializedProperty moduleDefinitionProperty = moduleDefinitionsProperty.GetArrayElementAtIndex(index);

            if (moduleDefinitionProperty == null)
                continue;

            SerializedProperty moduleIdProperty = moduleDefinitionProperty.FindPropertyRelative("moduleId");

            if (moduleIdProperty == null)
                continue;

            if (string.Equals(moduleIdProperty.stringValue, moduleId, StringComparison.OrdinalIgnoreCase) == false)
                continue;

            SerializedProperty moduleKindProperty = moduleDefinitionProperty.FindPropertyRelative("moduleKind");
            SerializedProperty displayNameProperty = moduleDefinitionProperty.FindPropertyRelative("displayName");
            moduleKind = ResolveModuleKind(moduleKindProperty);
            displayName = displayNameProperty != null && string.IsNullOrWhiteSpace(displayNameProperty.stringValue) == false
                ? displayNameProperty.stringValue
                : moduleId;
            return true;
        }

        return false;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Builds payload editor for Stationary modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
    private static bool BuildStationaryPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty stationaryProperty = payloadDataProperty.FindPropertyRelative("stationary");

        if (stationaryProperty == null)
        {
            HelpBox missingBox = new HelpBox("Stationary payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        AddField(payloadContainer, stationaryProperty.FindPropertyRelative("freezeRotation"), "Freeze Rotation");
        return true;
    }

    /// <summary>
    /// Builds payload editor for Wanderer modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
    private static bool BuildWandererPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty wandererProperty = payloadDataProperty.FindPropertyRelative("wanderer");

        if (wandererProperty == null)
        {
            HelpBox missingBox = new HelpBox("Wanderer payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        SerializedProperty modeProperty = wandererProperty.FindPropertyRelative("mode");
        SerializedProperty basicProperty = wandererProperty.FindPropertyRelative("basic");
        SerializedProperty dvdProperty = wandererProperty.FindPropertyRelative("dvd");

        if (modeProperty == null || basicProperty == null || dvdProperty == null)
        {
            HelpBox missingFieldsBox = new HelpBox("Wanderer payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingFieldsBox);
            return false;
        }

        AddField(payloadContainer, modeProperty, "Mode");

        Foldout basicFoldout = new Foldout();
        basicFoldout.text = "Basic";
        basicFoldout.value = true;
        payloadContainer.Add(basicFoldout);

        AddField(basicFoldout, basicProperty.FindPropertyRelative("searchRadius"), "Search Radius");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("minimumTravelDistance"), "Minimum Travel Distance");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("maximumTravelDistance"), "Maximum Travel Distance");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("arrivalTolerance"), "Arrival Tolerance");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("waitCooldownSeconds"), "Wait Cooldown Seconds");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("candidateSampleCount"), "Candidate Sample Count");
        SerializedProperty useInfiniteDirectionSamplingProperty = basicProperty.FindPropertyRelative("useInfiniteDirectionSampling");
        SerializedProperty infiniteDirectionStepDegreesProperty = basicProperty.FindPropertyRelative("infiniteDirectionStepDegrees");
        AddField(basicFoldout, useInfiniteDirectionSamplingProperty, "Use Infinite Direction Sampling");

        VisualElement infiniteDirectionContainer = new VisualElement();
        infiniteDirectionContainer.style.marginLeft = 12f;
        basicFoldout.Add(infiniteDirectionContainer);
        AddField(infiniteDirectionContainer, infiniteDirectionStepDegreesProperty, "Infinite Direction Step Degrees");

        UpdateToggleContainerVisibility(useInfiniteDirectionSamplingProperty, infiniteDirectionContainer);
        basicFoldout.TrackPropertyValue(useInfiniteDirectionSamplingProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, infiniteDirectionContainer);
        });

        AddField(basicFoldout, basicProperty.FindPropertyRelative("unexploredDirectionPreference"), "Unexplored Direction Preference");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("towardPlayerPreference"), "Toward Player Preference");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("minimumWallDistance"), "Minimum Wall Distance");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("minimumEnemyClearance"), "Minimum Enemy Clearance");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("trajectoryPredictionTime"), "Trajectory Prediction Time");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("freeTrajectoryPreference"), "Free Trajectory Preference");
        AddField(basicFoldout, basicProperty.FindPropertyRelative("blockedPathRetryDelay"), "Blocked Path Retry Delay");

        Foldout dvdFoldout = new Foldout();
        dvdFoldout.text = "DVD";
        dvdFoldout.value = true;
        payloadContainer.Add(dvdFoldout);

        AddField(dvdFoldout, dvdProperty.FindPropertyRelative("speedMultiplier"), "Speed Multiplier");
        AddField(dvdFoldout, dvdProperty.FindPropertyRelative("bounceDamping"), "Bounce Damping");
        AddField(dvdFoldout, dvdProperty.FindPropertyRelative("randomizeInitialDirection"), "Randomize Initial Direction");
        AddField(dvdFoldout, dvdProperty.FindPropertyRelative("fixedInitialDirectionDegrees"), "Fixed Initial Direction Degrees");
        AddField(dvdFoldout, dvdProperty.FindPropertyRelative("cornerNudgeDistance"), "Corner Nudge Distance");
        AddField(dvdFoldout, dvdProperty.FindPropertyRelative("ignoreSteeringAndPriority"), "Ignore Steering And Priority");

        UpdateWandererModeVisibility(modeProperty, basicFoldout, dvdFoldout);
        payloadContainer.TrackPropertyValue(modeProperty, changedProperty =>
        {
            UpdateWandererModeVisibility(changedProperty, basicFoldout, dvdFoldout);
        });

        return true;
    }

    /// <summary>
    /// Builds payload editor for Shooter modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
    private static bool BuildShooterPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty shooterProperty = payloadDataProperty.FindPropertyRelative("shooter");

        if (shooterProperty == null)
        {
            HelpBox missingBox = new HelpBox("Shooter payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        SerializedProperty aimPolicyProperty = shooterProperty.FindPropertyRelative("aimPolicy");
        SerializedProperty movementPolicyProperty = shooterProperty.FindPropertyRelative("movementPolicy");
        SerializedProperty fireIntervalProperty = shooterProperty.FindPropertyRelative("fireInterval");
        SerializedProperty burstCountProperty = shooterProperty.FindPropertyRelative("burstCount");
        SerializedProperty intraBurstDelayProperty = shooterProperty.FindPropertyRelative("intraBurstDelay");
        SerializedProperty useMinimumRangeProperty = shooterProperty.FindPropertyRelative("useMinimumRange");
        SerializedProperty minimumRangeProperty = shooterProperty.FindPropertyRelative("minimumRange");
        SerializedProperty useMaximumRangeProperty = shooterProperty.FindPropertyRelative("useMaximumRange");
        SerializedProperty maximumRangeProperty = shooterProperty.FindPropertyRelative("maximumRange");
        SerializedProperty projectileProperty = shooterProperty.FindPropertyRelative("projectile");
        SerializedProperty runtimeProjectileProperty = shooterProperty.FindPropertyRelative("runtimeProjectile");
        SerializedProperty elementalProperty = shooterProperty.FindPropertyRelative("elemental");

        if (aimPolicyProperty == null ||
            movementPolicyProperty == null ||
            fireIntervalProperty == null ||
            burstCountProperty == null ||
            intraBurstDelayProperty == null ||
            useMinimumRangeProperty == null ||
            minimumRangeProperty == null ||
            useMaximumRangeProperty == null ||
            maximumRangeProperty == null ||
            projectileProperty == null ||
            runtimeProjectileProperty == null ||
            elementalProperty == null)
        {
            HelpBox missingFieldsBox = new HelpBox("Shooter payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingFieldsBox);
            return false;
        }

        Foldout firingFoldout = new Foldout();
        firingFoldout.text = "Firing";
        firingFoldout.value = true;
        payloadContainer.Add(firingFoldout);

        AddField(firingFoldout, aimPolicyProperty, "Aim Policy");
        AddField(firingFoldout, movementPolicyProperty, "Movement Policy");
        AddField(firingFoldout, fireIntervalProperty, "Fire Interval");
        AddField(firingFoldout, burstCountProperty, "Burst Count");
        AddField(firingFoldout, intraBurstDelayProperty, "Intra Burst Delay");
        AddField(firingFoldout, useMinimumRangeProperty, "Use Minimum Range");

        VisualElement minimumRangeContainer = new VisualElement();
        minimumRangeContainer.style.marginLeft = 12f;
        firingFoldout.Add(minimumRangeContainer);
        AddField(minimumRangeContainer, minimumRangeProperty, "Minimum Range");

        AddField(firingFoldout, useMaximumRangeProperty, "Use Maximum Range");

        VisualElement maximumRangeContainer = new VisualElement();
        maximumRangeContainer.style.marginLeft = 12f;
        firingFoldout.Add(maximumRangeContainer);
        AddField(maximumRangeContainer, maximumRangeProperty, "Maximum Range");

        UpdateToggleContainerVisibility(useMinimumRangeProperty, minimumRangeContainer);
        UpdateToggleContainerVisibility(useMaximumRangeProperty, maximumRangeContainer);
        firingFoldout.TrackPropertyValue(useMinimumRangeProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, minimumRangeContainer);
        });
        firingFoldout.TrackPropertyValue(useMaximumRangeProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, maximumRangeContainer);
        });

        Foldout projectileFoldout = new Foldout();
        projectileFoldout.text = "Projectile";
        projectileFoldout.value = true;
        payloadContainer.Add(projectileFoldout);

        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectilesPerShot"), "Projectiles Per Shot");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("spreadAngleDegrees"), "Spread Angle Degrees");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileSpeed"), "Projectile Speed");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileDamage"), "Projectile Damage");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileRange"), "Projectile Range");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileLifetime"), "Projectile Lifetime");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileExplosionRadius"), "Projectile Explosion Radius");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileScaleMultiplier"), "Projectile Scale Multiplier");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("penetrationMode"), "Penetration Mode");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("maxPenetrations"), "Max Penetrations");
        AddField(projectileFoldout, projectileProperty.FindPropertyRelative("inheritShooterSpeed"), "Inherit Shooter Speed");

        Foldout runtimeProjectileFoldout = new Foldout();
        runtimeProjectileFoldout.text = "Runtime Projectile";
        runtimeProjectileFoldout.value = true;
        payloadContainer.Add(runtimeProjectileFoldout);

        AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("projectilePrefab"), "Projectile Prefab");
        AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("poolInitialCapacity"), "Pool Initial Capacity");
        AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("poolExpandBatch"), "Pool Expand Batch");

        Foldout elementalFoldout = new Foldout();
        elementalFoldout.text = "Elemental";
        elementalFoldout.value = true;
        payloadContainer.Add(elementalFoldout);

        SerializedProperty enableElementalDamageProperty = elementalProperty.FindPropertyRelative("enableElementalDamage");
        SerializedProperty effectDataProperty = elementalProperty.FindPropertyRelative("effectData");
        SerializedProperty stacksPerHitProperty = elementalProperty.FindPropertyRelative("stacksPerHit");

        AddField(elementalFoldout, enableElementalDamageProperty, "Enable Elemental Damage");

        VisualElement elementalPayloadContainer = new VisualElement();
        elementalPayloadContainer.style.marginLeft = 12f;
        elementalFoldout.Add(elementalPayloadContainer);
        AddField(elementalPayloadContainer, effectDataProperty, "Effect Data");
        AddField(elementalPayloadContainer, stacksPerHitProperty, "Stacks Per Hit");

        UpdateToggleContainerVisibility(enableElementalDamageProperty, elementalPayloadContainer);
        elementalFoldout.TrackPropertyValue(enableElementalDamageProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, elementalPayloadContainer);
        });

        return true;
    }

    /// <summary>
    /// Updates Wanderer payload foldout visibility from selected mode.
    /// </summary>
    /// <param name="modeProperty">Serialized mode property.</param>
    /// <param name="basicFoldout">Basic foldout element.</param>
    /// <param name="dvdFoldout">DVD foldout element.</param>
    private static void UpdateWandererModeVisibility(SerializedProperty modeProperty, VisualElement basicFoldout, VisualElement dvdFoldout)
    {
        EnemyWandererMode mode = EnemyWandererMode.Basic;

        if (modeProperty != null && modeProperty.propertyType == SerializedPropertyType.Enum)
            mode = (EnemyWandererMode)modeProperty.enumValueIndex;

        if (basicFoldout != null)
            basicFoldout.style.display = mode == EnemyWandererMode.Basic ? DisplayStyle.Flex : DisplayStyle.None;

        if (dvdFoldout != null)
            dvdFoldout.style.display = mode == EnemyWandererMode.Dvd ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Updates child container visibility from a boolean toggle property.
    /// </summary>
    /// <param name="toggleProperty">Boolean serialized property.</param>
    /// <param name="container">Container to show or hide.</param>
    private static void UpdateToggleContainerVisibility(SerializedProperty toggleProperty, VisualElement container)
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

    /// <summary>
    /// Checks if a list already contains an option value using case-insensitive comparison.
    /// </summary>
    /// <param name="options">Current options list.</param>
    /// <param name="value">Value to test.</param>
    /// <returns>Returns true when the value exists in the list.</returns>
    private static bool ContainsOption(List<string> options, string value)
    {
        if (options == null)
            return false;

        for (int index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion

    #endregion
}
