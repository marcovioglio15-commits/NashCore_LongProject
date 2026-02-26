using System.Collections.Generic;

public static class PowerUpModuleEnumDescriptions
{
    #region Fields
    private static readonly List<PowerUpModuleStage> StageValues = new List<PowerUpModuleStage>
    {
        PowerUpModuleStage.Trigger,
        PowerUpModuleStage.PreGate,
        PowerUpModuleStage.Gate,
        PowerUpModuleStage.StateEnter,
        PowerUpModuleStage.Execute,
        PowerUpModuleStage.StateExit,
        PowerUpModuleStage.PostExecute,
        PowerUpModuleStage.Hook
    };

    private static readonly List<PowerUpModuleKind> ModuleKindValues = new List<PowerUpModuleKind>
    {
        PowerUpModuleKind.TriggerPress,
        PowerUpModuleKind.TriggerRelease,
        PowerUpModuleKind.TriggerHoldCharge,
        PowerUpModuleKind.GateResource,
        PowerUpModuleKind.GateCooldown,
        PowerUpModuleKind.StateSuppressShooting,
        PowerUpModuleKind.EffectProjectilePatternCone,
        PowerUpModuleKind.EffectProjectileScale,
        PowerUpModuleKind.EffectProjectilePenetration,
        PowerUpModuleKind.EffectSpawnBomb,
        PowerUpModuleKind.EffectExplosionAreaDamage,
        PowerUpModuleKind.EffectDash,
        PowerUpModuleKind.EffectHealMissingHealth,
        PowerUpModuleKind.EffectTimeDilationEnemies,
        PowerUpModuleKind.HookOnPlayerMovementStep,
        PowerUpModuleKind.HookOnEnemyDeath,
        PowerUpModuleKind.HookOnProjectileSpawned,
        PowerUpModuleKind.HookOnProjectileWallHit,
        PowerUpModuleKind.HookOnProjectileDeath,
        PowerUpModuleKind.PassiveSpawnTrailSegment,
        PowerUpModuleKind.PassiveAreaTickApplyElement,
        PowerUpModuleKind.PassiveDeathExplosion,
        PowerUpModuleKind.PassiveProjectileOrbitOverride,
        PowerUpModuleKind.PassiveProjectileBounceOnWalls,
        PowerUpModuleKind.PassiveProjectileSplitOnDeath
    };

    #endregion

    #region Properties
    public static IReadOnlyList<PowerUpModuleStage> StageOptions
    {
        get
        {
            return StageValues;
        }
    }

    public static IReadOnlyList<PowerUpModuleKind> ModuleKindOptions
    {
        get
        {
            return ModuleKindValues;
        }
    }

    #endregion

    #region Methods

    #region Public API
    public static string FormatStageOption(PowerUpModuleStage stage)
    {
        return string.Concat(stage.ToString(), " - ", GetStageDescription(stage));
    }

    public static string FormatModuleKindOption(PowerUpModuleKind moduleKind)
    {
        return string.Concat(moduleKind.ToString(), " - ", GetModuleKindDescription(moduleKind));
    }

    public static string GetStageDescription(PowerUpModuleStage stage)
    {
        switch (stage)
        {
            case PowerUpModuleStage.Trigger:
                return "Input/event detection point that starts the flow.";
            case PowerUpModuleStage.PreGate:
                return "Optional normalization step before resource/cooldown checks.";
            case PowerUpModuleStage.Gate:
                return "Validation and cost checks that can block activation.";
            case PowerUpModuleStage.StateEnter:
                return "State changes enabled before effects are executed.";
            case PowerUpModuleStage.Execute:
                return "Main gameplay effect execution stage.";
            case PowerUpModuleStage.StateExit:
                return "Cleanup stage when transient states end.";
            case PowerUpModuleStage.PostExecute:
                return "Post-processing stage after primary effects.";
            case PowerUpModuleStage.Hook:
                return "Event-driven passive hook evaluated by runtime listeners.";
            default:
                return "No description available.";
        }
    }

    public static string GetModuleKindDescription(PowerUpModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerPress:
                return "Fires once when input crosses press threshold.";
            case PowerUpModuleKind.TriggerRelease:
                return "Fires once when pressed input is released.";
            case PowerUpModuleKind.TriggerHoldCharge:
                return "Accumulates charge while input is held.";
            case PowerUpModuleKind.GateResource:
                return "Consumes/validates energy or health resource.";
            case PowerUpModuleKind.GateCooldown:
                return "Prevents activation until cooldown reaches zero.";
            case PowerUpModuleKind.StateSuppressShooting:
                return "Temporarily blocks standard shooting logic.";
            case PowerUpModuleKind.EffectProjectilePatternCone:
                return "Emits multiple projectiles in a cone spread.";
            case PowerUpModuleKind.EffectProjectileScale:
                return "Applies projectile size/damage/speed/range multipliers.";
            case PowerUpModuleKind.EffectProjectilePenetration:
                return "Configures projectile pass-through behavior.";
            case PowerUpModuleKind.EffectSpawnBomb:
                return "Spawns and handles bomb projectile behavior.";
            case PowerUpModuleKind.EffectExplosionAreaDamage:
                return "Applies radial damage in an explosion area.";
            case PowerUpModuleKind.EffectDash:
                return "Performs a movement dash with optional i-frames.";
            case PowerUpModuleKind.EffectHealMissingHealth:
                return "Heals missing HP by a flat amount.";
            case PowerUpModuleKind.EffectTimeDilationEnemies:
                return "Slows enemy simulation for a duration.";
            case PowerUpModuleKind.HookOnPlayerMovementStep:
                return "Passive trigger executed on movement step updates.";
            case PowerUpModuleKind.HookOnEnemyDeath:
                return "Passive trigger executed when an enemy dies.";
            case PowerUpModuleKind.HookOnProjectileSpawned:
                return "Passive trigger executed when a projectile spawns.";
            case PowerUpModuleKind.HookOnProjectileWallHit:
                return "Passive trigger executed on projectile wall collision.";
            case PowerUpModuleKind.HookOnProjectileDeath:
                return "Passive trigger executed when a projectile despawns.";
            case PowerUpModuleKind.PassiveSpawnTrailSegment:
                return "Spawns persistent trail segments around movement path.";
            case PowerUpModuleKind.PassiveAreaTickApplyElement:
                return "Applies elemental stacks periodically in an area.";
            case PowerUpModuleKind.PassiveDeathExplosion:
                return "Triggers explosions from eligible death events.";
            case PowerUpModuleKind.PassiveProjectileOrbitOverride:
                return "Overrides projectile movement to orbital pattern.";
            case PowerUpModuleKind.PassiveProjectileBounceOnWalls:
                return "Adds wall bounce logic to projectiles.";
            case PowerUpModuleKind.PassiveProjectileSplitOnDeath:
                return "Splits projectile into children on death/despawn.";
            default:
                return "No description available.";
        }
    }

    public static PowerUpModuleStage GetRecommendedStage(PowerUpModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerPress:
            case PowerUpModuleKind.TriggerRelease:
            case PowerUpModuleKind.TriggerHoldCharge:
                return PowerUpModuleStage.Trigger;
            case PowerUpModuleKind.GateResource:
            case PowerUpModuleKind.GateCooldown:
                return PowerUpModuleStage.Gate;
            case PowerUpModuleKind.StateSuppressShooting:
                return PowerUpModuleStage.StateEnter;
            case PowerUpModuleKind.EffectProjectilePatternCone:
            case PowerUpModuleKind.EffectProjectileScale:
            case PowerUpModuleKind.EffectProjectilePenetration:
            case PowerUpModuleKind.EffectSpawnBomb:
            case PowerUpModuleKind.EffectExplosionAreaDamage:
            case PowerUpModuleKind.EffectDash:
            case PowerUpModuleKind.EffectHealMissingHealth:
            case PowerUpModuleKind.EffectTimeDilationEnemies:
                return PowerUpModuleStage.Execute;
            case PowerUpModuleKind.HookOnPlayerMovementStep:
            case PowerUpModuleKind.HookOnEnemyDeath:
            case PowerUpModuleKind.HookOnProjectileSpawned:
            case PowerUpModuleKind.HookOnProjectileWallHit:
            case PowerUpModuleKind.HookOnProjectileDeath:
            case PowerUpModuleKind.PassiveSpawnTrailSegment:
            case PowerUpModuleKind.PassiveAreaTickApplyElement:
            case PowerUpModuleKind.PassiveDeathExplosion:
            case PowerUpModuleKind.PassiveProjectileOrbitOverride:
            case PowerUpModuleKind.PassiveProjectileBounceOnWalls:
            case PowerUpModuleKind.PassiveProjectileSplitOnDeath:
                return PowerUpModuleStage.Hook;
            default:
                return PowerUpModuleStage.Execute;
        }
    }

    public static bool IsStageCoherent(PowerUpModuleKind moduleKind, PowerUpModuleStage stage)
    {
        return stage == GetRecommendedStage(moduleKind);
    }

    public static bool TryGetPayloadProperty(PowerUpModuleKind moduleKind, out string relativePropertyPath, out string payloadLabel)
    {
        switch (moduleKind)
        {
            case PowerUpModuleKind.TriggerHoldCharge:
                relativePropertyPath = "holdCharge";
                payloadLabel = "Hold Charge Payload";
                return true;
            case PowerUpModuleKind.GateResource:
                relativePropertyPath = "resourceGate";
                payloadLabel = "Resource Gate Payload";
                return true;
            case PowerUpModuleKind.GateCooldown:
                relativePropertyPath = "cooldownGate";
                payloadLabel = "Cooldown Gate Payload";
                return true;
            case PowerUpModuleKind.StateSuppressShooting:
                relativePropertyPath = "suppressShooting";
                payloadLabel = "Suppress Shooting Payload";
                return true;
            case PowerUpModuleKind.EffectProjectilePatternCone:
                relativePropertyPath = "projectilePatternCone";
                payloadLabel = "Projectile Pattern Payload";
                return true;
            case PowerUpModuleKind.EffectProjectileScale:
                relativePropertyPath = "projectileScale";
                payloadLabel = "Projectile Scale Payload";
                return true;
            case PowerUpModuleKind.EffectProjectilePenetration:
                relativePropertyPath = "projectilePenetration";
                payloadLabel = "Projectile Penetration Payload";
                return true;
            case PowerUpModuleKind.EffectSpawnBomb:
                relativePropertyPath = "bomb";
                payloadLabel = "Bomb Payload";
                return true;
            case PowerUpModuleKind.EffectExplosionAreaDamage:
            case PowerUpModuleKind.PassiveDeathExplosion:
                relativePropertyPath = "deathExplosion";
                payloadLabel = "Explosion Payload";
                return true;
            case PowerUpModuleKind.EffectDash:
                relativePropertyPath = "dash";
                payloadLabel = "Dash Payload";
                return true;
            case PowerUpModuleKind.EffectHealMissingHealth:
                relativePropertyPath = "healMissingHealth";
                payloadLabel = "Heal Payload";
                return true;
            case PowerUpModuleKind.EffectTimeDilationEnemies:
                relativePropertyPath = "bulletTime";
                payloadLabel = "Time Dilation Payload";
                return true;
            case PowerUpModuleKind.PassiveSpawnTrailSegment:
                relativePropertyPath = "trailSpawn";
                payloadLabel = "Trail Spawn Payload";
                return true;
            case PowerUpModuleKind.PassiveAreaTickApplyElement:
                relativePropertyPath = "elementalAreaTick";
                payloadLabel = "Elemental Area Tick Payload";
                return true;
            case PowerUpModuleKind.PassiveProjectileOrbitOverride:
                relativePropertyPath = "projectileOrbitOverride";
                payloadLabel = "Orbit Override Payload";
                return true;
            case PowerUpModuleKind.PassiveProjectileBounceOnWalls:
                relativePropertyPath = "projectileBounceOnWalls";
                payloadLabel = "Bounce Payload";
                return true;
            case PowerUpModuleKind.PassiveProjectileSplitOnDeath:
                relativePropertyPath = "projectileSplitOnDeath";
                payloadLabel = "Split Payload";
                return true;
            default:
                relativePropertyPath = string.Empty;
                payloadLabel = string.Empty;
                return false;
        }
    }
    #endregion

    #endregion
}
