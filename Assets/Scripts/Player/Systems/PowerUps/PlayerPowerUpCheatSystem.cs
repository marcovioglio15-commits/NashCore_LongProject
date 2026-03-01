using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Consumes runtime power-up cheat commands and mutates active/passive loadouts at runtime.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateBefore(typeof(PlayerPowerUpRechargeSystem))]
public partial struct PlayerPowerUpCheatSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpCheatCommand>();
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<EquippedPassiveToolElement>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach ((DynamicBuffer<PlayerPowerUpCheatCommand> cheatCommands,
                  RefRW<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                  RefRW<PlayerPassiveToolsState> passiveToolsState) in SystemAPI.Query<DynamicBuffer<PlayerPowerUpCheatCommand>,
                                                                                       RefRW<PlayerPowerUpsConfig>,
                                                                                       RefRW<PlayerPowerUpsState>,
                                                                                       DynamicBuffer<EquippedPassiveToolElement>,
                                                                                       RefRW<PlayerPassiveToolsState>>())
        {
            int commandCount = cheatCommands.Length;

            if (commandCount <= 0)
                continue;

            bool slotConfigChanged = false;
            bool passiveToolsChanged = false;

            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                PlayerPowerUpCheatCommand cheatCommand = cheatCommands[commandIndex];
                ProcessCheatCommand(in cheatCommand,
                                    ref powerUpsConfig.ValueRW,
                                    ref powerUpsState.ValueRW,
                                    equippedPassiveTools,
                                    ref slotConfigChanged,
                                    ref passiveToolsChanged);
            }

            if (slotConfigChanged)
                SanitizeRuntimeStateAfterSlotChanges(ref powerUpsConfig.ValueRW, ref powerUpsState.ValueRW);

            if (passiveToolsChanged)
                passiveToolsState.ValueRW = BuildPassiveToolsState(equippedPassiveTools);

            cheatCommands.Clear();
        }
    }
    #endregion

    #region Commands
    private static void ProcessCheatCommand(in PlayerPowerUpCheatCommand cheatCommand,
                                            ref PlayerPowerUpsConfig powerUpsConfig,
                                            ref PlayerPowerUpsState powerUpsState,
                                            DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools,
                                            ref bool slotConfigChanged,
                                            ref bool passiveToolsChanged)
    {
        switch (cheatCommand.CommandType)
        {
            case PlayerPowerUpCheatCommandType.RefillEnergy:
                powerUpsState.PrimaryEnergy = math.max(0f, powerUpsConfig.PrimarySlot.MaximumEnergy);
                powerUpsState.SecondaryEnergy = math.max(0f, powerUpsConfig.SecondarySlot.MaximumEnergy);
                return;
            case PlayerPowerUpCheatCommandType.ResetCooldowns:
                powerUpsState.PrimaryCooldownRemaining = 0f;
                powerUpsState.SecondaryCooldownRemaining = 0f;
                return;
            case PlayerPowerUpCheatCommandType.SwapActiveSlots:
                SwapActiveSlots(ref powerUpsConfig, ref powerUpsState);
                slotConfigChanged = true;
                return;
            case PlayerPowerUpCheatCommandType.SetPrimaryActiveKind:
                if (ApplyActiveKindToSlot(ref powerUpsConfig.PrimarySlot, cheatCommand.ActiveKind))
                {
                    powerUpsState.PrimaryCharge = 0f;
                    powerUpsState.PrimaryIsCharging = 0;
                    slotConfigChanged = true;
                }

                return;
            case PlayerPowerUpCheatCommandType.SetSecondaryActiveKind:
                if (ApplyActiveKindToSlot(ref powerUpsConfig.SecondarySlot, cheatCommand.ActiveKind))
                {
                    powerUpsState.SecondaryCharge = 0f;
                    powerUpsState.SecondaryIsCharging = 0;
                    slotConfigChanged = true;
                }

                return;
            case PlayerPowerUpCheatCommandType.AddPassiveByKind:
                if (TryAddPassiveByKind(equippedPassiveTools, cheatCommand.PassiveKind))
                    passiveToolsChanged = true;

                return;
            case PlayerPowerUpCheatCommandType.RemovePassiveByKind:
                if (TryRemovePassiveByKind(equippedPassiveTools, cheatCommand.PassiveKind))
                    passiveToolsChanged = true;

                return;
            case PlayerPowerUpCheatCommandType.ClearPassives:
                if (equippedPassiveTools.Length > 0)
                {
                    equippedPassiveTools.Clear();
                    passiveToolsChanged = true;
                }

                return;
        }
    }

    private static void SwapActiveSlots(ref PlayerPowerUpsConfig powerUpsConfig, ref PlayerPowerUpsState powerUpsState)
    {
        PlayerPowerUpSlotConfig primarySlot = powerUpsConfig.PrimarySlot;
        powerUpsConfig.PrimarySlot = powerUpsConfig.SecondarySlot;
        powerUpsConfig.SecondarySlot = primarySlot;

        float primaryEnergy = powerUpsState.PrimaryEnergy;
        powerUpsState.PrimaryEnergy = powerUpsState.SecondaryEnergy;
        powerUpsState.SecondaryEnergy = primaryEnergy;

        float primaryCooldownRemaining = powerUpsState.PrimaryCooldownRemaining;
        powerUpsState.PrimaryCooldownRemaining = powerUpsState.SecondaryCooldownRemaining;
        powerUpsState.SecondaryCooldownRemaining = primaryCooldownRemaining;

        float primaryCharge = powerUpsState.PrimaryCharge;
        powerUpsState.PrimaryCharge = powerUpsState.SecondaryCharge;
        powerUpsState.SecondaryCharge = primaryCharge;

        byte primaryIsCharging = powerUpsState.PrimaryIsCharging;
        powerUpsState.PrimaryIsCharging = powerUpsState.SecondaryIsCharging;
        powerUpsState.SecondaryIsCharging = primaryIsCharging;
    }

    private static bool ApplyActiveKindToSlot(ref PlayerPowerUpSlotConfig slotConfig, ActiveToolKind activeKind)
    {
        if (activeKind == ActiveToolKind.Custom)
            return false;

        if (slotConfig.IsDefined != 0 && slotConfig.ToolKind == activeKind)
            return false;

        slotConfig.IsDefined = 1;
        slotConfig.ToolKind = activeKind;
        slotConfig.ActivationInputMode = PowerUpActivationInputMode.OnPress;

        if (slotConfig.CooldownSeconds < 0f)
            slotConfig.CooldownSeconds = 0f;

        slotConfig.MinimumActivationEnergyPercent = math.clamp(slotConfig.MinimumActivationEnergyPercent, 0f, 100f);

        if (slotConfig.ActivationResource == PowerUpResourceType.Energy && slotConfig.MaximumEnergy <= 0f)
            slotConfig.MaximumEnergy = 100f;

        if (slotConfig.MaximumEnergy > 0f && slotConfig.ChargePerTrigger <= 0f)
            slotConfig.ChargePerTrigger = 25f;

        switch (activeKind)
        {
            case ActiveToolKind.Bomb:
                slotConfig.Bomb.CollisionRadius = math.max(0.01f, slotConfig.Bomb.CollisionRadius);
                slotConfig.Bomb.FuseSeconds = math.max(0.05f, slotConfig.Bomb.FuseSeconds);
                slotConfig.Bomb.Radius = math.max(0.1f, slotConfig.Bomb.Radius);
                slotConfig.Bomb.Damage = math.max(0f, slotConfig.Bomb.Damage);

                if (slotConfig.Bomb.EnableDamagePayload == 0)
                {
                    slotConfig.Bomb.Radius = 0f;
                    slotConfig.Bomb.Damage = 0f;
                    slotConfig.Bomb.AffectAllEnemiesInRadius = 0;
                }

                break;
            case ActiveToolKind.Dash:
                slotConfig.Dash.Distance = math.max(0.1f, slotConfig.Dash.Distance);
                slotConfig.Dash.Duration = math.max(0.01f, slotConfig.Dash.Duration);
                slotConfig.Dash.SpeedTransitionInSeconds = math.max(0f, slotConfig.Dash.SpeedTransitionInSeconds);
                slotConfig.Dash.SpeedTransitionOutSeconds = math.max(0f, slotConfig.Dash.SpeedTransitionOutSeconds);
                slotConfig.Dash.InvulnerabilityExtraTime = math.max(0f, slotConfig.Dash.InvulnerabilityExtraTime);
                break;
            case ActiveToolKind.BulletTime:
                slotConfig.BulletTime.Duration = math.max(0.05f, slotConfig.BulletTime.Duration);
                slotConfig.BulletTime.EnemySlowPercent = math.clamp(slotConfig.BulletTime.EnemySlowPercent, 0f, 100f);

                if (slotConfig.BulletTime.EnemySlowPercent <= 0f)
                    slotConfig.BulletTime.EnemySlowPercent = 40f;

                break;
            case ActiveToolKind.Shotgun:
                slotConfig.Shotgun.ProjectileCount = math.max(1, slotConfig.Shotgun.ProjectileCount);
                slotConfig.Shotgun.ConeAngleDegrees = math.max(0f, slotConfig.Shotgun.ConeAngleDegrees);
                slotConfig.Shotgun.SizeMultiplier = math.max(0.01f, slotConfig.Shotgun.SizeMultiplier);
                slotConfig.Shotgun.DamageMultiplier = math.max(0f, slotConfig.Shotgun.DamageMultiplier);
                slotConfig.Shotgun.SpeedMultiplier = math.max(0f, slotConfig.Shotgun.SpeedMultiplier);
                slotConfig.Shotgun.RangeMultiplier = math.max(0f, slotConfig.Shotgun.RangeMultiplier);
                slotConfig.Shotgun.LifetimeMultiplier = math.max(0f, slotConfig.Shotgun.LifetimeMultiplier);
                slotConfig.Shotgun.MaxPenetrations = math.max(0, slotConfig.Shotgun.MaxPenetrations);
                slotConfig.Shotgun.ElementalStacksPerHit = math.max(0f, slotConfig.Shotgun.ElementalStacksPerHit);
                break;
            case ActiveToolKind.ChargeShot:
                slotConfig.ChargeShot.RequiredCharge = math.max(1f, slotConfig.ChargeShot.RequiredCharge);
                slotConfig.ChargeShot.MaximumCharge = math.max(slotConfig.ChargeShot.RequiredCharge, slotConfig.ChargeShot.MaximumCharge);
                slotConfig.ChargeShot.ChargeRatePerSecond = math.max(1f, slotConfig.ChargeShot.ChargeRatePerSecond);
                slotConfig.ChargeShot.SizeMultiplier = math.max(0.01f, slotConfig.ChargeShot.SizeMultiplier);
                slotConfig.ChargeShot.DamageMultiplier = math.max(0f, slotConfig.ChargeShot.DamageMultiplier);
                slotConfig.ChargeShot.SpeedMultiplier = math.max(0f, slotConfig.ChargeShot.SpeedMultiplier);
                slotConfig.ChargeShot.RangeMultiplier = math.max(0f, slotConfig.ChargeShot.RangeMultiplier);
                slotConfig.ChargeShot.LifetimeMultiplier = math.max(0f, slotConfig.ChargeShot.LifetimeMultiplier);
                slotConfig.ChargeShot.MaxPenetrations = math.max(0, slotConfig.ChargeShot.MaxPenetrations);
                slotConfig.ChargeShot.ElementalStacksPerHit = math.max(0f, slotConfig.ChargeShot.ElementalStacksPerHit);
                break;
            case ActiveToolKind.PortableHealthPack:
                slotConfig.PortableHealthPack.HealAmount = math.max(1f, slotConfig.PortableHealthPack.HealAmount);
                slotConfig.PortableHealthPack.DurationSeconds = math.max(0f, slotConfig.PortableHealthPack.DurationSeconds);
                slotConfig.PortableHealthPack.TickIntervalSeconds = math.max(0.01f, slotConfig.PortableHealthPack.TickIntervalSeconds);

                if (slotConfig.PortableHealthPack.ApplyMode == PowerUpHealApplicationMode.OverTime &&
                    slotConfig.PortableHealthPack.DurationSeconds <= 0f)
                    slotConfig.PortableHealthPack.DurationSeconds = 2f;

                break;
        }

        return true;
    }

    private static void SanitizeRuntimeStateAfterSlotChanges(ref PlayerPowerUpsConfig powerUpsConfig, ref PlayerPowerUpsState powerUpsState)
    {
        float primaryMaximumEnergy = math.max(0f, powerUpsConfig.PrimarySlot.MaximumEnergy);
        float secondaryMaximumEnergy = math.max(0f, powerUpsConfig.SecondarySlot.MaximumEnergy);
        powerUpsState.PrimaryEnergy = math.clamp(powerUpsState.PrimaryEnergy, 0f, primaryMaximumEnergy);
        powerUpsState.SecondaryEnergy = math.clamp(powerUpsState.SecondaryEnergy, 0f, secondaryMaximumEnergy);
        powerUpsState.PrimaryCooldownRemaining = math.max(0f, powerUpsState.PrimaryCooldownRemaining);
        powerUpsState.SecondaryCooldownRemaining = math.max(0f, powerUpsState.SecondaryCooldownRemaining);

        if (powerUpsConfig.PrimarySlot.ToolKind == ActiveToolKind.ChargeShot)
        {
            float primaryMaximumCharge = math.max(powerUpsConfig.PrimarySlot.ChargeShot.RequiredCharge,
                                                  powerUpsConfig.PrimarySlot.ChargeShot.MaximumCharge);
            powerUpsState.PrimaryCharge = math.clamp(powerUpsState.PrimaryCharge, 0f, primaryMaximumCharge);
        }
        else
        {
            powerUpsState.PrimaryCharge = 0f;
            powerUpsState.PrimaryIsCharging = 0;
        }

        if (powerUpsConfig.SecondarySlot.ToolKind == ActiveToolKind.ChargeShot)
        {
            float secondaryMaximumCharge = math.max(powerUpsConfig.SecondarySlot.ChargeShot.RequiredCharge,
                                                    powerUpsConfig.SecondarySlot.ChargeShot.MaximumCharge);
            powerUpsState.SecondaryCharge = math.clamp(powerUpsState.SecondaryCharge, 0f, secondaryMaximumCharge);
        }
        else
        {
            powerUpsState.SecondaryCharge = 0f;
            powerUpsState.SecondaryIsCharging = 0;
        }

        powerUpsState.IsShootingSuppressed = 0;
    }
    #endregion

    #region Passive Tools
    private static bool TryAddPassiveByKind(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools, PassiveToolKind passiveKind)
    {
        if (passiveKind == PassiveToolKind.Custom)
            return false;

        if (ContainsPassiveKind(equippedPassiveTools, passiveKind))
            return false;

        PlayerPassiveToolConfig passiveToolConfig = CreateDefaultPassiveToolConfig(passiveKind);

        if (passiveToolConfig.IsDefined == 0)
            return false;

        equippedPassiveTools.Add(new EquippedPassiveToolElement
        {
            Tool = passiveToolConfig
        });
        return true;
    }

    private static bool TryRemovePassiveByKind(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools, PassiveToolKind passiveKind)
    {
        if (passiveKind == PassiveToolKind.Custom)
            return false;

        bool removedAny = false;

        for (int index = equippedPassiveTools.Length - 1; index >= 0; index--)
        {
            EquippedPassiveToolElement equippedPassiveTool = equippedPassiveTools[index];

            if (equippedPassiveTool.Tool.IsDefined == 0)
                continue;

            if (equippedPassiveTool.Tool.ToolKind != passiveKind)
                continue;

            equippedPassiveTools.RemoveAt(index);
            removedAny = true;
        }

        return removedAny;
    }

    private static bool ContainsPassiveKind(DynamicBuffer<EquippedPassiveToolElement> equippedPassiveTools, PassiveToolKind passiveKind)
    {
        for (int index = 0; index < equippedPassiveTools.Length; index++)
        {
            PlayerPassiveToolConfig passiveTool = equippedPassiveTools[index].Tool;

            if (passiveTool.IsDefined == 0)
                continue;

            if (passiveTool.ToolKind == passiveKind)
                return true;
        }

        return false;
    }

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

    private static PlayerPassiveToolConfig CreateDefaultPassiveToolConfig(PassiveToolKind passiveKind)
    {
        switch (passiveKind)
        {
            case PassiveToolKind.ProjectileSize:
                return new PlayerPassiveToolConfig
                {
                    IsDefined = 1,
                    ToolKind = PassiveToolKind.ProjectileSize,
                    HasProjectileSize = 1,
                    ProjectileSize = new ProjectileSizePassiveConfig
                    {
                        SizeMultiplier = 1.3f,
                        DamageMultiplier = 1.15f,
                        SpeedMultiplier = 1f,
                        LifetimeSecondsMultiplier = 1f,
                        LifetimeRangeMultiplier = 1f
                    }
                };
            case PassiveToolKind.ElementalProjectiles:
                return new PlayerPassiveToolConfig
                {
                    IsDefined = 1,
                    ToolKind = PassiveToolKind.ElementalProjectiles,
                    HasElementalProjectiles = 1,
                    ElementalProjectiles = new ElementalProjectilesPassiveConfig
                    {
                        Effect = CreateDefaultElementalEffectConfig(ElementType.Fire),
                        StacksPerHit = 1f
                    }
                };
            case PassiveToolKind.PerfectCircle:
                return new PlayerPassiveToolConfig
                {
                    IsDefined = 1,
                    ToolKind = PassiveToolKind.PerfectCircle,
                    HasPerfectCircle = 1,
                    PerfectCircle = new PerfectCirclePassiveConfig
                    {
                        RadialEntrySpeed = 14f,
                        OrbitalSpeed = 220f,
                        OrbitRadiusMin = 1.25f,
                        OrbitRadiusMax = 2.5f,
                        OrbitPulseFrequency = 1.2f,
                        OrbitEntryRatio = 0.35f,
                        OrbitBlendDuration = 0.3f,
                        HeightOffset = 0f,
                        GoldenAngleDegrees = 137.5f
                    }
                };
            case PassiveToolKind.BouncingProjectiles:
                return new PlayerPassiveToolConfig
                {
                    IsDefined = 1,
                    ToolKind = PassiveToolKind.BouncingProjectiles,
                    HasBouncingProjectiles = 1,
                    BouncingProjectiles = new BouncingProjectilesPassiveConfig
                    {
                        MaxBounces = 2,
                        SpeedPercentChangePerBounce = -10f,
                        MinimumSpeedMultiplierAfterBounce = 0.6f,
                        MaximumSpeedMultiplierAfterBounce = 1f
                    }
                };
            case PassiveToolKind.SplittingProjectiles:
                FixedList128Bytes<float> customAngles = default;

                return new PlayerPassiveToolConfig
                {
                    IsDefined = 1,
                    ToolKind = PassiveToolKind.SplittingProjectiles,
                    HasSplittingProjectiles = 1,
                    SplittingProjectiles = new SplittingProjectilesPassiveConfig
                    {
                        TriggerMode = ProjectileSplitTriggerMode.OnEnemyKilled,
                        DirectionMode = ProjectileSplitDirectionMode.Uniform,
                        SplitProjectileCount = 3,
                        SplitOffsetDegrees = 0f,
                        CustomAnglesDegrees = customAngles,
                        SplitDamageMultiplier = 0.45f,
                        SplitSizeMultiplier = 0.9f,
                        SplitSpeedMultiplier = 0.85f,
                        SplitLifetimeMultiplier = 0.75f
                    }
                };
            case PassiveToolKind.Explosion:
                return new PlayerPassiveToolConfig
                {
                    IsDefined = 1,
                    ToolKind = PassiveToolKind.Explosion,
                    HasExplosion = 1,
                    Explosion = new ExplosionPassiveConfig
                    {
                        TriggerMode = PassiveExplosionTriggerMode.OnEnemyKilled,
                        CooldownSeconds = 0.7f,
                        Radius = 2.25f,
                        Damage = 35f,
                        AffectAllEnemiesInRadius = 1,
                        TriggerOffset = float3.zero,
                        ExplosionVfxPrefabEntity = Entity.Null,
                        ScaleVfxToRadius = 0,
                        VfxScaleMultiplier = 1f
                    }
                };
            case PassiveToolKind.ElementalTrail:
                return new PlayerPassiveToolConfig
                {
                    IsDefined = 1,
                    ToolKind = PassiveToolKind.ElementalTrail,
                    HasElementalTrail = 1,
                    ElementalTrail = new ElementalTrailPassiveConfig
                    {
                        Effect = CreateDefaultElementalEffectConfig(ElementType.Poison),
                        TrailSegmentLifetimeSeconds = 3f,
                        TrailSpawnDistance = 1.25f,
                        TrailSpawnIntervalSeconds = 0.15f,
                        TrailRadius = 1.5f,
                        MaxActiveSegmentsPerPlayer = 18,
                        StacksPerTick = 1f,
                        ApplyIntervalSeconds = 0.2f,
                        TrailAttachedVfxPrefabEntity = Entity.Null,
                        TrailAttachedVfxScaleMultiplier = 1f,
                        TrailAttachedVfxOffset = float3.zero
                    }
                };
            default:
                return default;
        }
    }

    private static ElementalEffectConfig CreateDefaultElementalEffectConfig(ElementType elementType)
    {
        return new ElementalEffectConfig
        {
            ElementType = elementType,
            EffectKind = ElementalEffectKind.Dots,
            ProcMode = ElementalProcMode.ProgressiveUntilThreshold,
            ReapplyMode = ElementalProcReapplyMode.RefreshActiveProc,
            ProcThresholdStacks = 5f,
            MaximumStacks = 30f,
            StackDecayPerSecond = 1f,
            ConsumeStacksOnProc = 0,
            DotDamagePerTick = 6f,
            DotTickInterval = 0.25f,
            DotDurationSeconds = 2f,
            ImpedimentSlowPercentPerStack = 8f,
            ImpedimentProcSlowPercent = 30f,
            ImpedimentMaxSlowPercent = 80f,
            ImpedimentDurationSeconds = 1.5f
        };
    }
    #endregion

    #endregion
}
