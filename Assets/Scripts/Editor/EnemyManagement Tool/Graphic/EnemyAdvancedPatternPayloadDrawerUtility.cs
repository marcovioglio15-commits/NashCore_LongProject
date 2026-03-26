using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>
/// Builds module payload editors for enemy advanced pattern drawers.
/// </summary>
internal static class EnemyAdvancedPatternPayloadDrawerUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds payload editor for Stationary modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
    public static bool BuildStationaryPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty stationaryProperty = payloadDataProperty.FindPropertyRelative("stationary");

        if (stationaryProperty == null)
        {
            HelpBox missingBox = new HelpBox("Stationary payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(payloadContainer, stationaryProperty.FindPropertyRelative("freezeRotation"), "Freeze Rotation");
        return true;
    }

    /// <summary>
    /// Builds payload editor for DropItems modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
    public static bool BuildDropItemsPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty dropItemsProperty = payloadDataProperty.FindPropertyRelative("dropItems");

        if (dropItemsProperty == null)
        {
            HelpBox missingBox = new HelpBox("DropItems payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        SerializedProperty dropPayloadKindProperty = dropItemsProperty.FindPropertyRelative("dropPayloadKind");
        SerializedProperty experienceProperty = dropItemsProperty.FindPropertyRelative("experience");

        if (dropPayloadKindProperty == null || experienceProperty == null)
        {
            HelpBox missingFieldsBox = new HelpBox("DropItems payload fields are missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingFieldsBox);
            return false;
        }

        EnemyAdvancedPatternDrawerUtility.AddField(payloadContainer, dropPayloadKindProperty, "Drop Kind");

        Foldout experienceFoldout = new Foldout();
        experienceFoldout.text = "Experience";
        experienceFoldout.value = true;
        payloadContainer.Add(experienceFoldout);

        SerializedProperty dropDefinitionsProperty = experienceProperty.FindPropertyRelative("dropDefinitions");
        SerializedProperty complessiveExperienceDropMinimumProperty = experienceProperty.FindPropertyRelative("complessiveExperienceDropMinimum");
        SerializedProperty complessiveExperienceDropMaximumProperty = experienceProperty.FindPropertyRelative("complessiveExperienceDropMaximum");
        SerializedProperty dropsDistributionProperty = experienceProperty.FindPropertyRelative("dropsDistribution");
        SerializedProperty dropRadiusProperty = experienceProperty.FindPropertyRelative("dropRadius");
        SerializedProperty collectionMovementProperty = experienceProperty.FindPropertyRelative("collectionMovement");

        if (dropDefinitionsProperty == null ||
            complessiveExperienceDropMinimumProperty == null ||
            complessiveExperienceDropMaximumProperty == null ||
            dropsDistributionProperty == null ||
            dropRadiusProperty == null ||
            collectionMovementProperty == null)
        {
            HelpBox missingExperienceFieldsBox = new HelpBox("Experience drop settings are missing.", HelpBoxMessageType.Warning);
            experienceFoldout.Add(missingExperienceFieldsBox);
            return false;
        }

        Foldout dropDefinitionFoldout = new Foldout();
        dropDefinitionFoldout.text = "Drop Definition";
        dropDefinitionFoldout.value = true;
        experienceFoldout.Add(dropDefinitionFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(dropDefinitionFoldout, dropDefinitionsProperty, "Definitions");

        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, complessiveExperienceDropMinimumProperty, "Complessive Experience Drop Min");
        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, complessiveExperienceDropMaximumProperty, "Complessive Experience Drop Max");
        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, dropsDistributionProperty, "Drops Distribution");
        EnemyAdvancedPatternDrawerUtility.AddField(experienceFoldout, dropRadiusProperty, "Drop Radius");

        Foldout collectionMovementFoldout = new Foldout();
        collectionMovementFoldout.text = "Collection Movement";
        collectionMovementFoldout.value = true;
        experienceFoldout.Add(collectionMovementFoldout);
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("moveSpeed"), "Move Speed");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("collectDistance"), "Collect Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("collectDistancePerPlayerSpeed"), "Collect Distance Per Player Speed");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("spawnAnimationMinDuration"), "Spawn Animation Min Duration");
        EnemyAdvancedPatternDrawerUtility.AddField(collectionMovementFoldout, collectionMovementProperty.FindPropertyRelative("spawnAnimationMaxDuration"), "Spawn Animation Max Duration");

        HelpBox distributionWarningBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        distributionWarningBox.style.marginTop = 4f;
        experienceFoldout.Add(distributionWarningBox);

        bool isUpdatingDropItemsWarning = false;
        RefreshDropItemsRangeWarning();

        experienceFoldout.RegisterCallback<SerializedPropertyChangeEvent>(changedEvent =>
        {
            RefreshDropItemsRangeWarning();
        });

        payloadContainer.TrackPropertyValue(complessiveExperienceDropMinimumProperty, changedProperty =>
        {
            RefreshDropItemsRangeWarning();
        });
        payloadContainer.TrackPropertyValue(complessiveExperienceDropMaximumProperty, changedProperty =>
        {
            RefreshDropItemsRangeWarning();
        });
        payloadContainer.TrackPropertyValue(dropsDistributionProperty, changedProperty =>
        {
            RefreshDropItemsRangeWarning();
        });

        if (payloadDataProperty.serializedObject != null)
        {
            payloadContainer.TrackSerializedObjectValue(payloadDataProperty.serializedObject, changedObject =>
            {
                RefreshDropItemsRangeWarning();
            });
        }

        UpdateDropPayloadVisibility(dropPayloadKindProperty, experienceFoldout);
        payloadContainer.TrackPropertyValue(dropPayloadKindProperty, changedProperty =>
        {
            UpdateDropPayloadVisibility(changedProperty, experienceFoldout);
        });

        return true;

        void RefreshDropItemsRangeWarning()
        {
            if (isUpdatingDropItemsWarning)
                return;

            isUpdatingDropItemsWarning = true;
            EnemyAdvancedPatternDropDistributionWarningUtility.RefreshDropItemsDistributionWarning(dropDefinitionsProperty,
                                                                                                  complessiveExperienceDropMinimumProperty,
                                                                                                  complessiveExperienceDropMaximumProperty,
                                                                                                  dropsDistributionProperty,
                                                                                                  distributionWarningBox);
            isUpdatingDropItemsWarning = false;
        }
    }

    /// <summary>
    /// Builds payload editor for Wanderer modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
    public static bool BuildWandererPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
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

        EnemyAdvancedPatternDrawerUtility.AddField(payloadContainer, modeProperty, "Mode");

        Foldout basicFoldout = new Foldout();
        basicFoldout.text = "Basic";
        basicFoldout.value = true;
        payloadContainer.Add(basicFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("searchRadius"), "Search Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("minimumTravelDistance"), "Minimum Travel Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("maximumTravelDistance"), "Maximum Travel Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("arrivalTolerance"), "Arrival Tolerance");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("waitCooldownSeconds"), "Wait Cooldown Seconds");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("candidateSampleCount"), "Candidate Sample Count");
        SerializedProperty useInfiniteDirectionSamplingProperty = basicProperty.FindPropertyRelative("useInfiniteDirectionSampling");
        SerializedProperty infiniteDirectionStepDegreesProperty = basicProperty.FindPropertyRelative("infiniteDirectionStepDegrees");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, useInfiniteDirectionSamplingProperty, "Use Infinite Direction Sampling");

        VisualElement infiniteDirectionContainer = new VisualElement();
        infiniteDirectionContainer.style.marginLeft = 12f;
        basicFoldout.Add(infiniteDirectionContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(infiniteDirectionContainer, infiniteDirectionStepDegreesProperty, "Infinite Direction Step Degrees");

        UpdateToggleContainerVisibility(useInfiniteDirectionSamplingProperty, infiniteDirectionContainer);
        basicFoldout.TrackPropertyValue(useInfiniteDirectionSamplingProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, infiniteDirectionContainer);
        });

        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("unexploredDirectionPreference"), "Unexplored Direction Preference");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("towardPlayerPreference"), "Toward Player Preference");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("minimumEnemyClearance"), "Minimum Enemy Clearance");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("trajectoryPredictionTime"), "Trajectory Prediction Time");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("freeTrajectoryPreference"), "Free Trajectory Preference");
        EnemyAdvancedPatternDrawerUtility.AddField(basicFoldout, basicProperty.FindPropertyRelative("blockedPathRetryDelay"), "Blocked Path Retry Delay");

        Foldout dvdFoldout = new Foldout();
        dvdFoldout.text = "DVD";
        dvdFoldout.value = true;
        payloadContainer.Add(dvdFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("speedMultiplier"), "Speed Multiplier");
        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("bounceDamping"), "Bounce Damping");
        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("randomizeInitialDirection"), "Randomize Initial Direction");
        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("fixedInitialDirectionDegrees"), "Fixed Initial Direction Degrees");
        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("cornerNudgeDistance"), "Corner Nudge Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(dvdFoldout, dvdProperty.FindPropertyRelative("ignoreSteeringAndPriority"), "Ignore Steering And Priority");

        UpdateWandererModeVisibility(modeProperty, basicFoldout, dvdFoldout);
        payloadContainer.TrackPropertyValue(modeProperty, changedProperty =>
        {
            UpdateWandererModeVisibility(changedProperty, basicFoldout, dvdFoldout);
        });

        return true;
    }

    /// <summary>
    /// Builds payload editor for Coward modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
    public static bool BuildCowardPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
    {
        SerializedProperty cowardProperty = payloadDataProperty.FindPropertyRelative("coward");

        if (cowardProperty == null)
        {
            HelpBox missingBox = new HelpBox("Coward payload data is missing.", HelpBoxMessageType.Warning);
            payloadContainer.Add(missingBox);
            return false;
        }

        Foldout detectionFoldout = new Foldout();
        detectionFoldout.text = "1. Detection";
        detectionFoldout.value = true;
        payloadContainer.Add(detectionFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(detectionFoldout, cowardProperty.FindPropertyRelative("detectionRadius"), "Detection Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(detectionFoldout, cowardProperty.FindPropertyRelative("releaseDistanceBuffer"), "Release Buffer");

        Foldout retreatDistancesFoldout = new Foldout();
        retreatDistancesFoldout.text = "2. Retreat Distances";
        retreatDistancesFoldout.value = true;
        payloadContainer.Add(retreatDistancesFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("searchRadius"), "Search Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("minimumRetreatDistance"), "Minimum Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("maximumRetreatDistance"), "Maximum Distance");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("arrivalTolerance"), "Arrival Tolerance");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, cowardProperty.FindPropertyRelative("candidateSampleCount"), "Candidate Samples");
        SerializedProperty useInfiniteDirectionSamplingProperty = cowardProperty.FindPropertyRelative("useInfiniteDirectionSampling");
        SerializedProperty infiniteDirectionStepDegreesProperty = cowardProperty.FindPropertyRelative("infiniteDirectionStepDegrees");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatDistancesFoldout, useInfiniteDirectionSamplingProperty, "Use Infinite Sampling");

        VisualElement infiniteDirectionContainer = new VisualElement();
        infiniteDirectionContainer.style.marginLeft = 12f;
        retreatDistancesFoldout.Add(infiniteDirectionContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(infiniteDirectionContainer, infiniteDirectionStepDegreesProperty, "Infinite Step Degrees");

        UpdateToggleContainerVisibility(useInfiniteDirectionSamplingProperty, infiniteDirectionContainer);
        retreatDistancesFoldout.TrackPropertyValue(useInfiniteDirectionSamplingProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, infiniteDirectionContainer);
        });

        Foldout retreatSteeringFoldout = new Foldout();
        retreatSteeringFoldout.text = "3. Retreat Steering";
        retreatSteeringFoldout.value = true;
        payloadContainer.Add(retreatSteeringFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("minimumEnemyClearance"), "Enemy Clearance");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("trajectoryPredictionTime"), "Prediction Time");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("freeTrajectoryPreference"), "Trajectory Safety");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("retreatDirectionPreference"), "Retreat Directness");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("openSpacePreference"), "Open Space Bias");
        EnemyAdvancedPatternDrawerUtility.AddField(retreatSteeringFoldout, cowardProperty.FindPropertyRelative("navigationRetreatPreference"), "Pathfinding Bias");

        Foldout patrolFoldout = new Foldout();
        patrolFoldout.text = "4. Patrol";
        patrolFoldout.value = true;
        payloadContainer.Add(patrolFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(patrolFoldout, cowardProperty.FindPropertyRelative("patrolRadius"), "Patrol Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(patrolFoldout, cowardProperty.FindPropertyRelative("patrolWaitSeconds"), "Patrol Pause");
        EnemyAdvancedPatternDrawerUtility.AddField(patrolFoldout, cowardProperty.FindPropertyRelative("patrolSpeedMultiplier"), "Patrol Speed");

        Foldout speedFoldout = new Foldout();
        speedFoldout.text = "5. Speed";
        speedFoldout.value = true;
        payloadContainer.Add(speedFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(speedFoldout, cowardProperty.FindPropertyRelative("retreatSpeedMultiplierFar"), "Retreat Speed Far");
        EnemyAdvancedPatternDrawerUtility.AddField(speedFoldout, cowardProperty.FindPropertyRelative("retreatSpeedMultiplierNear"), "Retreat Speed Near");

        Foldout recoveryFoldout = new Foldout();
        recoveryFoldout.text = "6. Recovery";
        recoveryFoldout.value = true;
        payloadContainer.Add(recoveryFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(recoveryFoldout, cowardProperty.FindPropertyRelative("blockedPathRetryDelay"), "Retry Delay");
        return true;
    }

    /// <summary>
    /// Builds payload editor for Shooter modules.
    /// </summary>
    /// <param name="payloadDataProperty">Payload data root.</param>
    /// <param name="payloadContainer">Target UI container.</param>
    /// <returns>Returns true when UI is built.</returns>
    public static bool BuildShooterPayloadEditor(SerializedProperty payloadDataProperty, VisualElement payloadContainer)
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

        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, aimPolicyProperty, "Aim Policy");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, movementPolicyProperty, "Movement Policy");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, fireIntervalProperty, "Fire Interval");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, burstCountProperty, "Burst Count");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, intraBurstDelayProperty, "Intra Burst Delay");
        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, useMinimumRangeProperty, "Use Minimum Range");

        VisualElement minimumRangeContainer = new VisualElement();
        minimumRangeContainer.style.marginLeft = 12f;
        firingFoldout.Add(minimumRangeContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(minimumRangeContainer, minimumRangeProperty, "Minimum Range");

        EnemyAdvancedPatternDrawerUtility.AddField(firingFoldout, useMaximumRangeProperty, "Use Maximum Range");

        VisualElement maximumRangeContainer = new VisualElement();
        maximumRangeContainer.style.marginLeft = 12f;
        firingFoldout.Add(maximumRangeContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(maximumRangeContainer, maximumRangeProperty, "Maximum Range");

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

        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectilesPerShot"), "Projectiles Per Shot");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("spreadAngleDegrees"), "Spread Angle Degrees");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileSpeed"), "Projectile Speed");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileDamage"), "Projectile Damage");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileRange"), "Projectile Range");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileLifetime"), "Projectile Lifetime");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileExplosionRadius"), "Projectile Explosion Radius");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("projectileScaleMultiplier"), "Projectile Scale Multiplier");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("penetrationMode"), "Penetration Mode");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("maxPenetrations"), "Max Penetrations");
        EnemyAdvancedPatternDrawerUtility.AddField(projectileFoldout, projectileProperty.FindPropertyRelative("inheritShooterSpeed"), "Inherit Shooter Speed");

        Foldout runtimeProjectileFoldout = new Foldout();
        runtimeProjectileFoldout.text = "Runtime Projectile";
        runtimeProjectileFoldout.value = true;
        payloadContainer.Add(runtimeProjectileFoldout);

        EnemyAdvancedPatternDrawerUtility.AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("projectilePrefab"), "Projectile Prefab");
        EnemyAdvancedPatternDrawerUtility.AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("poolInitialCapacity"), "Pool Initial Capacity");
        EnemyAdvancedPatternDrawerUtility.AddField(runtimeProjectileFoldout, runtimeProjectileProperty.FindPropertyRelative("poolExpandBatch"), "Pool Expand Batch");

        Foldout elementalFoldout = new Foldout();
        elementalFoldout.text = "Elemental";
        elementalFoldout.value = true;
        payloadContainer.Add(elementalFoldout);

        SerializedProperty enableElementalDamageProperty = elementalProperty.FindPropertyRelative("enableElementalDamage");
        SerializedProperty effectDataProperty = elementalProperty.FindPropertyRelative("effectData");
        SerializedProperty stacksPerHitProperty = elementalProperty.FindPropertyRelative("stacksPerHit");

        EnemyAdvancedPatternDrawerUtility.AddField(elementalFoldout, enableElementalDamageProperty, "Enable Elemental Damage");

        VisualElement elementalPayloadContainer = new VisualElement();
        elementalPayloadContainer.style.marginLeft = 12f;
        elementalFoldout.Add(elementalPayloadContainer);
        EnemyAdvancedPatternDrawerUtility.AddField(elementalPayloadContainer, effectDataProperty, "Effect Data");
        EnemyAdvancedPatternDrawerUtility.AddField(elementalPayloadContainer, stacksPerHitProperty, "Stacks Per Hit");

        UpdateToggleContainerVisibility(enableElementalDamageProperty, elementalPayloadContainer);
        elementalFoldout.TrackPropertyValue(enableElementalDamageProperty, changedProperty =>
        {
            UpdateToggleContainerVisibility(changedProperty, elementalPayloadContainer);
        });

        return true;
    }
    #endregion

    #region Private Methods
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
    /// Updates DropItems payload visibility from the selected drop payload kind.
    /// </summary>
    /// <param name="dropPayloadKindProperty">Drop payload kind property.</param>
    /// <param name="experienceFoldout">Experience settings foldout.</param>

    private static void UpdateDropPayloadVisibility(SerializedProperty dropPayloadKindProperty, VisualElement experienceFoldout)
    {
        EnemyDropItemsPayloadKind payloadKind = EnemyDropItemsPayloadKind.Experience;

        if (dropPayloadKindProperty != null && dropPayloadKindProperty.propertyType == SerializedPropertyType.Enum)
            payloadKind = (EnemyDropItemsPayloadKind)dropPayloadKindProperty.enumValueIndex;

        if (experienceFoldout != null)
            experienceFoldout.style.display = payloadKind == EnemyDropItemsPayloadKind.Experience ? DisplayStyle.Flex : DisplayStyle.None;
    }
    #endregion

    #endregion
}
