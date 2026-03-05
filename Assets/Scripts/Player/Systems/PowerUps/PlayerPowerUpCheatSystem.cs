using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Consumes runtime cheat commands and replaces the player's whole power-up loadout with a baked preset snapshot.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateBefore(typeof(PlayerPowerUpRechargeSystem))]
public partial struct PlayerPowerUpCheatSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers all component requirements needed by runtime cheat preset application.
    /// </summary>
    /// <param name="state">System state used to declare update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpCheatCommand>();
        state.RequireForUpdate<PlayerPowerUpCheatPresetEntry>();
        state.RequireForUpdate<PlayerPowerUpCheatPresetPassiveElement>();
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
    }

    /// <summary>
    /// Applies queued cheat commands for each player, replacing runtime config and passives when a preset swap is requested.
    /// </summary>
    /// <param name="state">Current ECS system state.</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach ((DynamicBuffer<PlayerPowerUpCheatCommand> cheatCommands,
                  DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries,
                  DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                  RefRW<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                  RefRW<PlayerPassiveToolsState> passiveToolsState) in SystemAPI.Query<DynamicBuffer<PlayerPowerUpCheatCommand>,
                                                                                       DynamicBuffer<PlayerPowerUpCheatPresetEntry>,
                                                                                       DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement>,
                                                                                       RefRW<PlayerPowerUpsConfig>,
                                                                                       RefRW<PlayerPowerUpsState>,
                                                                                       DynamicBuffer<EquippedPassiveToolElement>,
                                                                                       RefRW<PlayerPassiveToolsState>>())
        {
            int commandCount = cheatCommands.Length;

            if (commandCount <= 0)
                continue;

            bool passivesChanged = false;

            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                PlayerPowerUpCheatCommand cheatCommand = cheatCommands[commandIndex];
                bool changed = ProcessCheatCommand(in cheatCommand,
                                                   cheatPresetEntries,
                                                   cheatPresetPassives,
                                                   ref powerUpsConfig.ValueRW,
                                                   ref powerUpsState.ValueRW,
                                                   equippedPassiveTools);

                if (changed)
                    passivesChanged = true;
            }

            if (passivesChanged)
                passiveToolsState.ValueRW = BuildPassiveToolsState(equippedPassiveTools);

            cheatCommands.Clear();
        }
    }
    #endregion

    #region Commands
    /// <summary>
    /// Routes one command to the matching cheat action.
    /// </summary>
    /// <param name="cheatCommand">Command payload to process.</param>
    /// <param name="cheatPresetEntries">Baked preset metadata buffer.</param>
    /// <param name="cheatPresetPassives">Flattened baked passives buffer.</param>
    /// <param name="powerUpsConfig">Runtime power-up config to mutate.</param>
    /// <param name="powerUpsState">Runtime power-up state to reset.</param>
    /// <param name="equippedPassiveTools">Runtime equipped passives buffer to replace.</param>
    /// <returns>True when runtime loadout was changed, otherwise false.</returns>
    private static bool ProcessCheatCommand(in PlayerPowerUpCheatCommand cheatCommand,
                                            DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries,
                                            DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                                            ref PlayerPowerUpsConfig powerUpsConfig,
                                            ref PlayerPowerUpsState powerUpsState,
                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        switch (cheatCommand.CommandType)
        {
            case PlayerPowerUpCheatCommandType.ApplyPresetByIndex:
                return TryApplyPresetByIndex(cheatCommand.PresetIndex,
                                             cheatPresetEntries,
                                             cheatPresetPassives,
                                             ref powerUpsConfig,
                                             ref powerUpsState,
                                             equippedPassiveTools);
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies a full preset snapshot by index when the entry exists.
    /// </summary>
    /// <param name="presetIndex">Requested snapshot index.</param>
    /// <param name="cheatPresetEntries">Baked preset metadata buffer.</param>
    /// <param name="cheatPresetPassives">Flattened baked passives buffer.</param>
    /// <param name="powerUpsConfig">Runtime power-up config to mutate.</param>
    /// <param name="powerUpsState">Runtime state to reset after replacement.</param>
    /// <param name="equippedPassiveTools">Runtime equipped passives buffer to replace.</param>
    /// <returns>True when the preset was found and applied, otherwise false.</returns>
    private static bool TryApplyPresetByIndex(int presetIndex,
                                              DynamicBuffer<PlayerPowerUpCheatPresetEntry> cheatPresetEntries,
                                              DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                                              ref PlayerPowerUpsConfig powerUpsConfig,
                                              ref PlayerPowerUpsState powerUpsState,
                                              DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        if (presetIndex < 0)
            return false;

        if (presetIndex >= cheatPresetEntries.Length)
            return false;

        PlayerPowerUpCheatPresetEntry cheatPresetEntry = cheatPresetEntries[presetIndex];

        if (cheatPresetEntry.IsDefined == 0)
            return false;

        powerUpsConfig = cheatPresetEntry.PowerUpsConfig;
        ReplaceEquippedPassivesFromSnapshot(cheatPresetEntry, cheatPresetPassives, equippedPassiveTools);
        ResetRuntimeStateAfterPresetSwap(ref powerUpsState, in powerUpsConfig);
        return true;
    }

    /// <summary>
    /// Replaces equipped passive tools with the passive range referenced by one baked preset entry.
    /// </summary>
    /// <param name="cheatPresetEntry">Preset metadata containing passive range indices.</param>
    /// <param name="cheatPresetPassives">Flattened source passive payloads.</param>
    /// <param name="equippedPassiveTools">Runtime destination buffer to overwrite.</param>
    private static void ReplaceEquippedPassivesFromSnapshot(in PlayerPowerUpCheatPresetEntry cheatPresetEntry,
                                                            DynamicBuffer<PlayerPowerUpCheatPresetPassiveElement> cheatPresetPassives,
                                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        equippedPassiveTools.Clear();

        if (cheatPresetPassives.Length <= 0)
            return;

        int safeStartIndex = math.clamp(cheatPresetEntry.PassiveStartIndex, 0, cheatPresetPassives.Length);
        int availableCount = cheatPresetPassives.Length - safeStartIndex;
        int safeCount = math.clamp(cheatPresetEntry.PassiveCount, 0, availableCount);

        for (int passiveOffset = 0; passiveOffset < safeCount; passiveOffset++)
        {
            PlayerPassiveToolConfig passiveToolConfig = cheatPresetPassives[safeStartIndex + passiveOffset].Tool;

            if (passiveToolConfig.IsDefined == 0)
                continue;

            equippedPassiveTools.Add(new EquippedPassiveToolElement
            {
                Tool = passiveToolConfig
            });
        }
    }

    /// <summary>
    /// Resets transient runtime power-up state after a full preset swap.
    /// </summary>
    /// <param name="powerUpsState">Runtime state to reset.</param>
    /// <param name="powerUpsConfig">Runtime config used to initialize slot energy.</param>
    private static void ResetRuntimeStateAfterPresetSwap(ref PlayerPowerUpsState powerUpsState, in PlayerPowerUpsConfig powerUpsConfig)
    {
        float primaryMaximumEnergy = math.max(0f, powerUpsConfig.PrimarySlot.MaximumEnergy);
        float secondaryMaximumEnergy = math.max(0f, powerUpsConfig.SecondarySlot.MaximumEnergy);
        powerUpsState.PrimaryEnergy = primaryMaximumEnergy;
        powerUpsState.SecondaryEnergy = secondaryMaximumEnergy;
        powerUpsState.PrimaryCooldownRemaining = 0f;
        powerUpsState.SecondaryCooldownRemaining = 0f;
        powerUpsState.PrimaryCharge = 0f;
        powerUpsState.SecondaryCharge = 0f;
        powerUpsState.PrimaryIsCharging = 0;
        powerUpsState.SecondaryIsCharging = 0;
        powerUpsState.IsShootingSuppressed = 0;
    }
    #endregion

    #region Passive Tools
    /// <summary>
    /// Rebuilds aggregated passive multipliers and feature flags from equipped passive entries.
    /// </summary>
    /// <param name="equippedPassiveTools">Runtime equipped passive-tool buffer.</param>
    /// <returns>Aggregated passive state used by power-up systems.</returns>
    private static PlayerPassiveToolsState BuildPassiveToolsState(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools)
    {
        PlayerPassiveToolsState passiveToolsState = new PlayerPassiveToolsState
        {
            ProjectileSizeMultiplier = 1f,
            ProjectileDamageMultiplier = 1f,
            ProjectileSpeedMultiplier = 1f,
            ProjectileLifetimeSecondsMultiplier = 1f,
            ProjectileLifetimeRangeMultiplier = 1f,
            HasShotgun = 0,
            Shotgun = default,
            HasElementalProjectiles = 0,
            ElementalProjectiles = default,
            HasPerfectCircle = 0,
            PerfectCircle = default,
            HasBouncingProjectiles = 0,
            BouncingProjectiles = default,
            HasSplittingProjectiles = 0,
            SplittingProjectiles = default,
            HasExplosion = 0,
            Explosion = default,
            HasElementalTrail = 0,
            ElementalTrail = default,
            HasHeal = 0,
            Heal = default
        };

        for (int passiveToolIndex = 0; passiveToolIndex < equippedPassiveTools.Length; passiveToolIndex++)
        {
            EquippedPassiveToolElement equippedPassiveTool = equippedPassiveTools[passiveToolIndex];
            AccumulatePassiveTool(ref passiveToolsState, in equippedPassiveTool.Tool);
        }

        return passiveToolsState;
    }

    /// <summary>
    /// Merges one passive-tool payload into the aggregated passive runtime state.
    /// </summary>
    /// <param name="passiveToolsState">Accumulated runtime passive state.</param>
    /// <param name="passiveToolConfig">Passive-tool payload to merge.</param>
    private static void AccumulatePassiveTool(ref PlayerPassiveToolsState passiveToolsState, in PlayerPassiveToolConfig passiveToolConfig)
    {
        if (passiveToolConfig.IsDefined == 0)
            return;

        if (passiveToolConfig.HasProjectileSize != 0)
        {
            passiveToolsState.ProjectileSizeMultiplier *= math.max(0.01f, passiveToolConfig.ProjectileSize.SizeMultiplier);
            passiveToolsState.ProjectileDamageMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.DamageMultiplier);
            passiveToolsState.ProjectileSpeedMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.SpeedMultiplier);
            passiveToolsState.ProjectileLifetimeSecondsMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.LifetimeSecondsMultiplier);
            passiveToolsState.ProjectileLifetimeRangeMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.LifetimeRangeMultiplier);
        }

        if (passiveToolConfig.HasShotgun != 0)
        {
            passiveToolsState.HasShotgun = 1;
            passiveToolsState.Shotgun.ProjectileCount += math.max(0, passiveToolConfig.Shotgun.ProjectileCount);
            passiveToolsState.Shotgun.ConeAngleDegrees = math.max(passiveToolsState.Shotgun.ConeAngleDegrees,
                                                                  math.max(0f, passiveToolConfig.Shotgun.ConeAngleDegrees));
            passiveToolsState.Shotgun.PenetrationMode = (ProjectilePenetrationMode)math.max((int)passiveToolsState.Shotgun.PenetrationMode,
                                                                                             (int)passiveToolConfig.Shotgun.PenetrationMode);
            passiveToolsState.Shotgun.MaxPenetrations += math.max(0, passiveToolConfig.Shotgun.MaxPenetrations);
        }

        if (passiveToolConfig.HasElementalProjectiles != 0 && passiveToolConfig.ElementalProjectiles.StacksPerHit > 0f)
        {
            float candidateStacksPerHit = math.max(0f, passiveToolConfig.ElementalProjectiles.StacksPerHit);

            if (candidateStacksPerHit > 0f)
            {
                if (passiveToolsState.HasElementalProjectiles == 0)
                {
                    passiveToolsState.HasElementalProjectiles = 1;
                    passiveToolsState.ElementalProjectiles = passiveToolConfig.ElementalProjectiles;
                }
                else
                {
                    passiveToolsState.ElementalProjectiles.Effect = passiveToolConfig.ElementalProjectiles.Effect;
                    passiveToolsState.ElementalProjectiles.StacksPerHit += candidateStacksPerHit;
                }
            }
        }

        if (passiveToolConfig.HasPerfectCircle != 0)
        {
            passiveToolsState.HasPerfectCircle = 1;

            if (passiveToolsState.PerfectCircle.OrbitRadiusMax <= 0f)
            {
                passiveToolsState.PerfectCircle = passiveToolConfig.PerfectCircle;
            }
            else
            {
                passiveToolsState.PerfectCircle.RadialEntrySpeed = math.max(passiveToolsState.PerfectCircle.RadialEntrySpeed, passiveToolConfig.PerfectCircle.RadialEntrySpeed);
                passiveToolsState.PerfectCircle.OrbitalSpeed = math.max(passiveToolsState.PerfectCircle.OrbitalSpeed, passiveToolConfig.PerfectCircle.OrbitalSpeed);
                passiveToolsState.PerfectCircle.OrbitRadiusMin = math.max(passiveToolsState.PerfectCircle.OrbitRadiusMin, passiveToolConfig.PerfectCircle.OrbitRadiusMin);
                passiveToolsState.PerfectCircle.OrbitRadiusMax = math.max(passiveToolsState.PerfectCircle.OrbitRadiusMax, passiveToolConfig.PerfectCircle.OrbitRadiusMax);
                passiveToolsState.PerfectCircle.OrbitPulseFrequency = math.max(passiveToolsState.PerfectCircle.OrbitPulseFrequency, passiveToolConfig.PerfectCircle.OrbitPulseFrequency);
                passiveToolsState.PerfectCircle.OrbitEntryRatio = math.max(passiveToolsState.PerfectCircle.OrbitEntryRatio, passiveToolConfig.PerfectCircle.OrbitEntryRatio);
                passiveToolsState.PerfectCircle.OrbitBlendDuration = math.max(passiveToolsState.PerfectCircle.OrbitBlendDuration, passiveToolConfig.PerfectCircle.OrbitBlendDuration);
                passiveToolsState.PerfectCircle.HeightOffset = math.max(passiveToolsState.PerfectCircle.HeightOffset, passiveToolConfig.PerfectCircle.HeightOffset);
                passiveToolsState.PerfectCircle.GoldenAngleDegrees = math.max(passiveToolsState.PerfectCircle.GoldenAngleDegrees, passiveToolConfig.PerfectCircle.GoldenAngleDegrees);
            }
        }

        if (passiveToolConfig.HasBouncingProjectiles != 0)
        {
            passiveToolsState.HasBouncingProjectiles = 1;
            passiveToolsState.BouncingProjectiles.MaxBounces += math.max(0, passiveToolConfig.BouncingProjectiles.MaxBounces);
            passiveToolsState.BouncingProjectiles.SpeedPercentChangePerBounce += passiveToolConfig.BouncingProjectiles.SpeedPercentChangePerBounce;

            if (passiveToolsState.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce <= 0f)
                passiveToolsState.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce = math.max(0f, passiveToolConfig.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce);
            else
                passiveToolsState.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce = math.min(passiveToolsState.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce,
                                                                                                    math.max(0f, passiveToolConfig.BouncingProjectiles.MinimumSpeedMultiplierAfterBounce));

            passiveToolsState.BouncingProjectiles.MaximumSpeedMultiplierAfterBounce = math.max(passiveToolsState.BouncingProjectiles.MaximumSpeedMultiplierAfterBounce,
                                                                                                math.max(0f, passiveToolConfig.BouncingProjectiles.MaximumSpeedMultiplierAfterBounce));
        }

        if (passiveToolConfig.HasSplittingProjectiles != 0)
        {
            passiveToolsState.HasSplittingProjectiles = 1;

            if (passiveToolsState.SplittingProjectiles.SplitProjectileCount <= 0)
            {
                passiveToolsState.SplittingProjectiles = passiveToolConfig.SplittingProjectiles;
            }
            else
            {
                passiveToolsState.SplittingProjectiles.SplitProjectileCount = math.max(passiveToolsState.SplittingProjectiles.SplitProjectileCount,
                                                                                       passiveToolConfig.SplittingProjectiles.SplitProjectileCount);
                passiveToolsState.SplittingProjectiles.SplitOffsetDegrees = math.max(passiveToolsState.SplittingProjectiles.SplitOffsetDegrees,
                                                                                     passiveToolConfig.SplittingProjectiles.SplitOffsetDegrees);
                passiveToolsState.SplittingProjectiles.SplitDamageMultiplier = math.max(passiveToolsState.SplittingProjectiles.SplitDamageMultiplier,
                                                                                        passiveToolConfig.SplittingProjectiles.SplitDamageMultiplier);
                passiveToolsState.SplittingProjectiles.SplitSizeMultiplier = math.max(passiveToolsState.SplittingProjectiles.SplitSizeMultiplier,
                                                                                      passiveToolConfig.SplittingProjectiles.SplitSizeMultiplier);
                passiveToolsState.SplittingProjectiles.SplitSpeedMultiplier = math.max(passiveToolsState.SplittingProjectiles.SplitSpeedMultiplier,
                                                                                       passiveToolConfig.SplittingProjectiles.SplitSpeedMultiplier);
                passiveToolsState.SplittingProjectiles.SplitLifetimeMultiplier = math.max(passiveToolsState.SplittingProjectiles.SplitLifetimeMultiplier,
                                                                                          passiveToolConfig.SplittingProjectiles.SplitLifetimeMultiplier);

                if (passiveToolsState.SplittingProjectiles.CustomAnglesDegrees.Length <= 0 &&
                    passiveToolConfig.SplittingProjectiles.CustomAnglesDegrees.Length > 0)
                {
                    passiveToolsState.SplittingProjectiles.CustomAnglesDegrees = passiveToolConfig.SplittingProjectiles.CustomAnglesDegrees;
                }

                passiveToolsState.SplittingProjectiles.TriggerMode = passiveToolConfig.SplittingProjectiles.TriggerMode;
                passiveToolsState.SplittingProjectiles.DirectionMode = passiveToolConfig.SplittingProjectiles.DirectionMode;
            }
        }

        if (passiveToolConfig.HasExplosion != 0)
        {
            passiveToolsState.HasExplosion = 1;

            if (passiveToolsState.Explosion.Radius <= 0f)
            {
                passiveToolsState.Explosion = passiveToolConfig.Explosion;
            }
            else
            {
                passiveToolsState.Explosion.CooldownSeconds = math.min(passiveToolsState.Explosion.CooldownSeconds, passiveToolConfig.Explosion.CooldownSeconds);
                passiveToolsState.Explosion.Radius = math.max(passiveToolsState.Explosion.Radius, passiveToolConfig.Explosion.Radius);
                passiveToolsState.Explosion.Damage += passiveToolConfig.Explosion.Damage;
                passiveToolsState.Explosion.AffectAllEnemiesInRadius = passiveToolsState.Explosion.AffectAllEnemiesInRadius != 0 || passiveToolConfig.Explosion.AffectAllEnemiesInRadius != 0 ? (byte)1 : (byte)0;

                if (passiveToolsState.Explosion.ExplosionVfxPrefabEntity == Entity.Null && passiveToolConfig.Explosion.ExplosionVfxPrefabEntity != Entity.Null)
                {
                    passiveToolsState.Explosion.ExplosionVfxPrefabEntity = passiveToolConfig.Explosion.ExplosionVfxPrefabEntity;
                    passiveToolsState.Explosion.ScaleVfxToRadius = passiveToolConfig.Explosion.ScaleVfxToRadius;
                    passiveToolsState.Explosion.VfxScaleMultiplier = passiveToolConfig.Explosion.VfxScaleMultiplier;
                }
            }
        }

        if (passiveToolConfig.HasElementalTrail != 0)
        {
            passiveToolsState.HasElementalTrail = 1;

            if (passiveToolsState.ElementalTrail.TrailSegmentLifetimeSeconds <= 0f)
            {
                passiveToolsState.ElementalTrail = passiveToolConfig.ElementalTrail;
            }
            else
            {
                passiveToolsState.ElementalTrail.Effect = passiveToolConfig.ElementalTrail.Effect;
                passiveToolsState.ElementalTrail.TrailSegmentLifetimeSeconds = math.max(passiveToolsState.ElementalTrail.TrailSegmentLifetimeSeconds,
                                                                                        passiveToolConfig.ElementalTrail.TrailSegmentLifetimeSeconds);
                passiveToolsState.ElementalTrail.TrailSpawnDistance = math.max(passiveToolsState.ElementalTrail.TrailSpawnDistance,
                                                                               passiveToolConfig.ElementalTrail.TrailSpawnDistance);
                passiveToolsState.ElementalTrail.TrailSpawnIntervalSeconds = math.min(passiveToolsState.ElementalTrail.TrailSpawnIntervalSeconds,
                                                                                      passiveToolConfig.ElementalTrail.TrailSpawnIntervalSeconds);
                passiveToolsState.ElementalTrail.TrailRadius = math.max(passiveToolsState.ElementalTrail.TrailRadius,
                                                                        passiveToolConfig.ElementalTrail.TrailRadius);
                passiveToolsState.ElementalTrail.MaxActiveSegmentsPerPlayer = math.max(passiveToolsState.ElementalTrail.MaxActiveSegmentsPerPlayer,
                                                                                        passiveToolConfig.ElementalTrail.MaxActiveSegmentsPerPlayer);
                passiveToolsState.ElementalTrail.StacksPerTick += math.max(0f, passiveToolConfig.ElementalTrail.StacksPerTick);
                passiveToolsState.ElementalTrail.ApplyIntervalSeconds = math.min(passiveToolsState.ElementalTrail.ApplyIntervalSeconds,
                                                                                 passiveToolConfig.ElementalTrail.ApplyIntervalSeconds);

                if (passiveToolsState.ElementalTrail.TrailAttachedVfxPrefabEntity == Entity.Null &&
                    passiveToolConfig.ElementalTrail.TrailAttachedVfxPrefabEntity != Entity.Null)
                {
                    passiveToolsState.ElementalTrail.TrailAttachedVfxPrefabEntity = passiveToolConfig.ElementalTrail.TrailAttachedVfxPrefabEntity;
                    passiveToolsState.ElementalTrail.TrailAttachedVfxScaleMultiplier = passiveToolConfig.ElementalTrail.TrailAttachedVfxScaleMultiplier;
                    passiveToolsState.ElementalTrail.TrailAttachedVfxOffset = passiveToolConfig.ElementalTrail.TrailAttachedVfxOffset;
                }
            }
        }

        if (passiveToolConfig.HasHeal != 0)
        {
            passiveToolsState.HasHeal = 1;

            if (passiveToolsState.Heal.HealAmount <= 0f)
            {
                passiveToolsState.Heal = passiveToolConfig.Heal;
            }
            else
            {
                passiveToolsState.Heal.HealAmount += math.max(0f, passiveToolConfig.Heal.HealAmount);
                passiveToolsState.Heal.CooldownSeconds = math.min(passiveToolsState.Heal.CooldownSeconds, passiveToolConfig.Heal.CooldownSeconds);
                passiveToolsState.Heal.DurationSeconds = math.max(passiveToolsState.Heal.DurationSeconds, passiveToolConfig.Heal.DurationSeconds);
                passiveToolsState.Heal.TickIntervalSeconds = math.min(passiveToolsState.Heal.TickIntervalSeconds, passiveToolConfig.Heal.TickIntervalSeconds);
                passiveToolsState.Heal.StackPolicy = passiveToolConfig.Heal.StackPolicy;
                passiveToolsState.Heal.TriggerMode = passiveToolConfig.Heal.TriggerMode;
            }
        }
    }
    #endregion

    #endregion
}
