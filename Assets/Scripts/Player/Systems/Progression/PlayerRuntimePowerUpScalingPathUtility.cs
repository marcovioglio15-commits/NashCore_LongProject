using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Applies runtime power-up scaling values to active and passive configs using modular payload paths.
/// </summary>
internal static class PlayerRuntimePowerUpScalingPathUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies one resolved Add Scaling value to the runtime config field represented by the provided payload path.
    /// /params payloadPath: Modular payload path extracted from the scaling rule stat key.
    /// /params unlockKind: Active or passive target kind owning the runtime config.
    /// /params resolvedValue: Formula result already evaluated against scalable-stat runtime values.
    /// /params activeSlotConfig: Mutable active slot config rebuilt from immutable baselines.
    /// /params passiveToolConfig: Mutable passive tool config rebuilt from immutable baselines.
    /// /returns void.
    /// </summary>
    public static void ApplyValue(string payloadPath,
                                  PlayerPowerUpUnlockKind unlockKind,
                                  float resolvedValue,
                                  ref PlayerPowerUpSlotConfig activeSlotConfig,
                                  ref PlayerPassiveToolConfig passiveToolConfig)
    {
        if (string.IsNullOrWhiteSpace(payloadPath))
            return;

        switch (unlockKind)
        {
            case PlayerPowerUpUnlockKind.Active:
                ApplyActiveValue(payloadPath, resolvedValue, ref activeSlotConfig);
                ApplyEmbeddedTogglePassiveValue(payloadPath, resolvedValue, ref activeSlotConfig);
                return;
            case PlayerPowerUpUnlockKind.Passive:
                ApplyPassiveValue(payloadPath, resolvedValue, ref passiveToolConfig);
                return;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Applies one resolved Add Scaling value to an active-slot runtime config field.
    /// /params payloadPath: Modular payload path extracted from the scaling rule stat key.
    /// /params resolvedValue: Formula result already evaluated against scalable-stat runtime values.
    /// /params activeSlotConfig: Mutable active slot config rebuilt from immutable baselines.
    /// /returns void.
    /// </summary>
    private static void ApplyActiveValue(string payloadPath,
                                         float resolvedValue,
                                         ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        switch (payloadPath)
        {
            case "resourceGate.maximumEnergy":
                activeSlotConfig.MaximumEnergy = math.max(0f, resolvedValue);
                return;
            case "resourceGate.activationCost":
                activeSlotConfig.ActivationCost = math.max(0f, resolvedValue);
                return;
            case "resourceGate.maintenanceCostPerSecond":
                activeSlotConfig.MaintenanceCostPerSecond = math.max(0f, resolvedValue);
                return;
            case "resourceGate.maintenanceTicksPerSecond":
                activeSlotConfig.MaintenanceTicksPerSecond = math.max(0f, resolvedValue);
                return;
            case "resourceGate.chargePerTrigger":
                activeSlotConfig.ChargePerTrigger = math.max(0f, resolvedValue);
                return;
            case "resourceGate.cooldownSeconds":
                activeSlotConfig.CooldownSeconds = math.max(0f, resolvedValue);
                return;
            case "resourceGate.minimumActivationEnergyPercent":
                activeSlotConfig.MinimumActivationEnergyPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case "holdCharge.requiredCharge":
                activeSlotConfig.ChargeShot.RequiredCharge = math.max(0f, resolvedValue);

                if (activeSlotConfig.ChargeShot.MaximumCharge < activeSlotConfig.ChargeShot.RequiredCharge)
                    activeSlotConfig.ChargeShot.MaximumCharge = activeSlotConfig.ChargeShot.RequiredCharge;

                return;
            case "holdCharge.maximumCharge":
                activeSlotConfig.ChargeShot.MaximumCharge = math.max(activeSlotConfig.ChargeShot.RequiredCharge, resolvedValue);
                return;
            case "holdCharge.chargeRatePerSecond":
                activeSlotConfig.ChargeShot.ChargeRatePerSecond = math.max(0f, resolvedValue);
                return;
            case "holdCharge.decayAfterReleasePercentPerSecond":
                activeSlotConfig.ChargeShot.DecayAfterReleasePercentPerSecond = math.max(0f, resolvedValue);
                return;
            case "holdCharge.passiveChargeGainPercentPerSecond":
                activeSlotConfig.ChargeShot.PassiveChargeGainPercentPerSecond = math.max(0f, resolvedValue);
                return;
            case "projectilePatternCone.projectileCount":
                activeSlotConfig.Shotgun.ProjectileCount = math.max(1, (int)resolvedValue);
                return;
            case "projectilePatternCone.coneAngleDegrees":
                activeSlotConfig.Shotgun.ConeAngleDegrees = math.max(0f, resolvedValue);
                return;
            case "bomb.spawnOffset.x":
                activeSlotConfig.Bomb.SpawnOffset.x = resolvedValue;
                return;
            case "bomb.spawnOffset.y":
                activeSlotConfig.Bomb.SpawnOffset.y = resolvedValue;
                return;
            case "bomb.spawnOffset.z":
                activeSlotConfig.Bomb.SpawnOffset.z = resolvedValue;
                return;
            case "bomb.deploySpeed":
                activeSlotConfig.Bomb.DeploySpeed = math.max(0f, resolvedValue);
                return;
            case "bomb.collisionRadius":
                activeSlotConfig.Bomb.CollisionRadius = math.max(0.01f, resolvedValue);
                return;
            case "bomb.bounceDamping":
                activeSlotConfig.Bomb.BounceDamping = math.clamp(resolvedValue, 0f, 1f);
                return;
            case "bomb.linearDampingPerSecond":
                activeSlotConfig.Bomb.LinearDampingPerSecond = math.max(0f, resolvedValue);
                return;
            case "bomb.fuseSeconds":
                activeSlotConfig.Bomb.FuseSeconds = math.max(0.05f, resolvedValue);
                return;
            case "bomb.radius":
                activeSlotConfig.Bomb.Radius = math.max(0.1f, resolvedValue);
                activeSlotConfig.Bomb.EnableDamagePayload = activeSlotConfig.Bomb.Radius > 0f || activeSlotConfig.Bomb.Damage > 0f ? (byte)1 : (byte)0;
                return;
            case "bomb.damage":
                activeSlotConfig.Bomb.Damage = math.max(0f, resolvedValue);
                activeSlotConfig.Bomb.EnableDamagePayload = activeSlotConfig.Bomb.Radius > 0f || activeSlotConfig.Bomb.Damage > 0f ? (byte)1 : (byte)0;
                return;
            case "bomb.vfxScaleMultiplier":
                activeSlotConfig.Bomb.VfxScaleMultiplier = math.max(0.01f, resolvedValue);
                return;
            case "dash.distance":
                activeSlotConfig.Dash.Distance = math.max(0f, resolvedValue);
                return;
            case "dash.duration":
                activeSlotConfig.Dash.Duration = math.max(0.01f, resolvedValue);
                return;
            case "dash.speedTransitionInSeconds":
                activeSlotConfig.Dash.SpeedTransitionInSeconds = math.max(0f, resolvedValue);
                return;
            case "dash.speedTransitionOutSeconds":
                activeSlotConfig.Dash.SpeedTransitionOutSeconds = math.max(0f, resolvedValue);
                return;
            case "dash.invulnerabilityExtraTime":
                activeSlotConfig.Dash.InvulnerabilityExtraTime = math.max(0f, resolvedValue);
                return;
            case "bulletTime.duration":
                activeSlotConfig.BulletTime.Duration = math.max(0.05f, resolvedValue);
                return;
            case "bulletTime.enemySlowPercent":
                activeSlotConfig.BulletTime.EnemySlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case "bulletTime.transitionTimeSeconds":
                activeSlotConfig.BulletTime.TransitionTimeSeconds = math.max(0f, resolvedValue);
                return;
            case "healMissingHealth.healAmount":
                activeSlotConfig.PortableHealthPack.HealAmount = math.max(0f, resolvedValue);
                return;
            case "healMissingHealth.durationSeconds":
                activeSlotConfig.PortableHealthPack.DurationSeconds = math.max(0f, resolvedValue);
                return;
            case "healMissingHealth.tickIntervalSeconds":
                activeSlotConfig.PortableHealthPack.TickIntervalSeconds = math.max(0.01f, resolvedValue);
                return;
        }
    }

    /// <summary>
    /// Applies one resolved Add Scaling value to the embedded passive payload owned by an active toggleable power-up.
    /// This keeps runtime scaling generic for active power-ups that expose passive module payloads through TogglePassiveTool.
    /// /params payloadPath: Modular payload path extracted from the scaling rule stat key.
    /// /params resolvedValue: Formula result already evaluated against scalable-stat runtime values.
    /// /params activeSlotConfig: Mutable active slot config rebuilt from immutable baselines.
    /// /returns void.
    /// </summary>
    private static void ApplyEmbeddedTogglePassiveValue(string payloadPath,
                                                        float resolvedValue,
                                                        ref PlayerPowerUpSlotConfig activeSlotConfig)
    {
        if (activeSlotConfig.ToolKind != ActiveToolKind.PassiveToggle)
            return;

        if (activeSlotConfig.TogglePassiveTool.IsDefined == 0)
            return;

        PlayerPassiveToolConfig togglePassiveTool = activeSlotConfig.TogglePassiveTool;
        ApplyPassiveValue(payloadPath, resolvedValue, ref togglePassiveTool);
        activeSlotConfig.TogglePassiveTool = togglePassiveTool;
    }

    /// <summary>
    /// Applies one resolved Add Scaling value to a passive-tool runtime config field.
    /// /params payloadPath: Modular payload path extracted from the scaling rule stat key.
    /// /params resolvedValue: Formula result already evaluated against scalable-stat runtime values.
    /// /params passiveToolConfig: Mutable passive tool config rebuilt from immutable baselines.
    /// /returns void.
    /// </summary>
    private static void ApplyPassiveValue(string payloadPath,
                                          float resolvedValue,
                                          ref PlayerPassiveToolConfig passiveToolConfig)
    {
        switch (payloadPath)
        {
            case "resourceGate.cooldownSeconds":
                passiveToolConfig.Heal.CooldownSeconds = math.max(0f, resolvedValue);
                passiveToolConfig.BulletTime.CooldownSeconds = math.max(0f, resolvedValue);
                return;
            case "projectilePatternCone.projectileCount":
                passiveToolConfig.Shotgun.ProjectileCount = math.max(1, (int)resolvedValue);
                return;
            case "projectilePatternCone.coneAngleDegrees":
                passiveToolConfig.Shotgun.ConeAngleDegrees = math.max(0f, resolvedValue);
                return;
            case "trailSpawn.trailSegmentLifetimeSeconds":
                passiveToolConfig.ElementalTrail.TrailSegmentLifetimeSeconds = math.max(0.05f, resolvedValue);
                return;
            case "trailSpawn.trailSpawnDistance":
                passiveToolConfig.ElementalTrail.TrailSpawnDistance = math.max(0f, resolvedValue);
                return;
            case "trailSpawn.trailSpawnIntervalSeconds":
                passiveToolConfig.ElementalTrail.TrailSpawnIntervalSeconds = math.max(0.01f, resolvedValue);
                return;
            case "trailSpawn.trailRadius":
                passiveToolConfig.ElementalTrail.TrailRadius = math.max(0f, resolvedValue);
                return;
            case "trailSpawn.maxActiveSegmentsPerPlayer":
                passiveToolConfig.ElementalTrail.MaxActiveSegmentsPerPlayer = math.max(1, (int)resolvedValue);
                return;
            case "trailSpawn.trailAttachedVfxOffset.x":
                passiveToolConfig.ElementalTrail.TrailAttachedVfxOffset.x = resolvedValue;
                return;
            case "trailSpawn.trailAttachedVfxOffset.y":
                passiveToolConfig.ElementalTrail.TrailAttachedVfxOffset.y = resolvedValue;
                return;
            case "trailSpawn.trailAttachedVfxOffset.z":
                passiveToolConfig.ElementalTrail.TrailAttachedVfxOffset.z = resolvedValue;
                return;
            case "elementalAreaTick.stacksPerTick":
                passiveToolConfig.ElementalTrail.StacksPerTick = math.max(0f, resolvedValue);
                return;
            case "elementalAreaTick.applyIntervalSeconds":
                passiveToolConfig.ElementalTrail.ApplyIntervalSeconds = math.max(0.01f, resolvedValue);
                return;
            case "deathExplosion.cooldownSeconds":
                passiveToolConfig.Explosion.CooldownSeconds = math.max(0f, resolvedValue);
                return;
            case "deathExplosion.radius":
                passiveToolConfig.Explosion.Radius = math.max(0f, resolvedValue);
                return;
            case "deathExplosion.damage":
                passiveToolConfig.Explosion.Damage = math.max(0f, resolvedValue);
                return;
            case "deathExplosion.triggerOffset.x":
                passiveToolConfig.Explosion.TriggerOffset.x = resolvedValue;
                return;
            case "deathExplosion.triggerOffset.y":
                passiveToolConfig.Explosion.TriggerOffset.y = resolvedValue;
                return;
            case "deathExplosion.triggerOffset.z":
                passiveToolConfig.Explosion.TriggerOffset.z = resolvedValue;
                return;
            case "deathExplosion.vfxScaleMultiplier":
                passiveToolConfig.Explosion.VfxScaleMultiplier = math.max(0.01f, resolvedValue);
                return;
            case "projectileOrbitOverride.radialEntrySpeed":
                passiveToolConfig.PerfectCircle.RadialEntrySpeed = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitalSpeed":
                passiveToolConfig.PerfectCircle.OrbitalSpeed = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitRadiusMin":
                passiveToolConfig.PerfectCircle.OrbitRadiusMin = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitRadiusMax":
                passiveToolConfig.PerfectCircle.OrbitRadiusMax = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitPulseFrequency":
                passiveToolConfig.PerfectCircle.OrbitPulseFrequency = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.orbitEntryRatio":
                passiveToolConfig.PerfectCircle.OrbitEntryRatio = math.clamp(resolvedValue, 0f, 1f);
                return;
            case "projectileOrbitOverride.orbitBlendDuration":
                passiveToolConfig.PerfectCircle.OrbitBlendDuration = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.heightOffset":
                passiveToolConfig.PerfectCircle.HeightOffset = resolvedValue;
                return;
            case "projectileOrbitOverride.goldenAngleDegrees":
                passiveToolConfig.PerfectCircle.GoldenAngleDegrees = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralStartRadius":
                passiveToolConfig.PerfectCircle.SpiralStartRadius = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralMaximumRadius":
                passiveToolConfig.PerfectCircle.SpiralMaximumRadius = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralAngularSpeedDegreesPerSecond":
                passiveToolConfig.PerfectCircle.SpiralAngularSpeedDegreesPerSecond = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralGrowthMultiplier":
                passiveToolConfig.PerfectCircle.SpiralGrowthMultiplier = math.max(0f, resolvedValue);
                return;
            case "projectileOrbitOverride.spiralTurnsBeforeDespawn":
                passiveToolConfig.PerfectCircle.SpiralTurnsBeforeDespawn = math.max(0.1f, resolvedValue);
                return;
            case "projectileBounceOnWalls.maxBounces":
                passiveToolConfig.BouncingProjectiles.MaxBounces = math.max(0, (int)resolvedValue);
                return;
            case "projectileBounceOnWalls.speedPercentChangePerBounce":
                passiveToolConfig.BouncingProjectiles.SpeedPercentChangePerBounce = resolvedValue;
                return;
            case "projectileBounceOnWalls.minimumSpeedMultiplierAfterBounce":
                passiveToolConfig.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce = math.max(0f, resolvedValue);
                return;
            case "projectileBounceOnWalls.maximumSpeedMultiplierAfterBounce":
                passiveToolConfig.BouncingProjectiles.MaximumSpeedMultiplierAfterBounce = math.max(0f, resolvedValue);
                return;
            case "projectileSplit.splitProjectileCount":
                passiveToolConfig.SplittingProjectiles.SplitProjectileCount = math.max(1, (int)resolvedValue);
                return;
            case "projectileSplit.splitOffsetDegrees":
                passiveToolConfig.SplittingProjectiles.SplitOffsetDegrees = resolvedValue;
                return;
            case "projectileSplit.splitDamagePercentFromOriginal":
                passiveToolConfig.SplittingProjectiles.SplitDamageMultiplier = math.max(0f, resolvedValue * 0.01f);
                return;
            case "projectileSplit.splitSizePercentFromOriginal":
                passiveToolConfig.SplittingProjectiles.SplitSizeMultiplier = math.max(0f, resolvedValue * 0.01f);
                return;
            case "projectileSplit.splitSpeedPercentFromOriginal":
                passiveToolConfig.SplittingProjectiles.SplitSpeedMultiplier = math.max(0f, resolvedValue * 0.01f);
                return;
            case "projectileSplit.splitLifetimePercentFromOriginal":
                passiveToolConfig.SplittingProjectiles.SplitLifetimeMultiplier = math.max(0f, resolvedValue * 0.01f);
                return;
            case "healMissingHealth.healAmount":
                passiveToolConfig.Heal.HealAmount = math.max(0f, resolvedValue);
                return;
            case "healMissingHealth.durationSeconds":
                passiveToolConfig.Heal.DurationSeconds = math.max(0.05f, resolvedValue);
                return;
            case "healMissingHealth.tickIntervalSeconds":
                passiveToolConfig.Heal.TickIntervalSeconds = math.max(0.01f, resolvedValue);
                return;
            case "bulletTime.duration":
                passiveToolConfig.BulletTime.DurationSeconds = math.max(0.05f, resolvedValue);
                return;
            case "bulletTime.enemySlowPercent":
                passiveToolConfig.BulletTime.EnemySlowPercent = math.clamp(resolvedValue, 0f, 100f);
                return;
            case "bulletTime.transitionTimeSeconds":
                passiveToolConfig.BulletTime.TransitionTimeSeconds = math.max(0f, resolvedValue);
                return;
        }
    }
    #endregion

    #endregion
}
