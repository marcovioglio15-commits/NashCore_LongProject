using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Initializes runtime components required by power-up systems.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerPowerUpsInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingStateQuery;
    private EntityQuery missingPassiveToolsStateQuery;
    private EntityQuery missingDashQuery;
    private EntityQuery missingBulletTimeStateQuery;
    private EntityQuery missingHealOverTimeStateQuery;
    private EntityQuery missingPassiveExplosionStateQuery;
    private EntityQuery missingPassiveHealStateQuery;
    private EntityQuery missingElementalTrailStateQuery;
    private EntityQuery missingElementalTrailAttachedVfxStateQuery;
    private EntityQuery missingBombRequestBufferQuery;
    private EntityQuery missingElementalTrailSegmentBufferQuery;
    private EntityQuery missingExplosionRequestBufferQuery;
    private EntityQuery missingPowerUpVfxRequestBufferQuery;
    private EntityQuery missingPowerUpVfxPoolBufferQuery;
    private EntityQuery missingPowerUpVfxCapConfigQuery;
    private EntityQuery missingPowerUpCheatBufferQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpsConfig>();

        missingStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpsState>()
            .Build();

        missingPassiveToolsStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPassiveToolsState>()
            .Build();

        missingDashQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerDashState>()
            .Build();

        missingBulletTimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerBulletTimeState>()
            .Build();

        missingHealOverTimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerHealOverTimeState>()
            .Build();

        missingPassiveExplosionStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPassiveExplosionState>()
            .Build();

        missingPassiveHealStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPassiveHealState>()
            .Build();

        missingElementalTrailStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerElementalTrailState>()
            .Build();

        missingElementalTrailAttachedVfxStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerElementalTrailAttachedVfxState>()
            .Build();

        missingBombRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerBombSpawnRequest>()
            .Build();

        missingElementalTrailSegmentBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerElementalTrailSegmentElement>()
            .Build();

        missingExplosionRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerExplosionRequest>()
            .Build();

        missingPowerUpVfxRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpVfxSpawnRequest>()
            .Build();

        missingPowerUpVfxPoolBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpVfxPoolElement>()
            .Build();

        missingPowerUpVfxCapConfigQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpVfxCapConfig>()
            .Build();

        missingPowerUpCheatBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPowerUpCheatCommand>()
            .Build();
    }

    /// <summary>
    /// Updates the system, adding missing power-up runtime states and buffers
    /// to entities with a PlayerPowerUpsConfig.
    /// </summary>
    /// <param name="state"></param>
    public void OnUpdate(ref SystemState state)
    {
        bool hasMissingState = missingStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPassiveToolsState = missingPassiveToolsStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingDash = missingDashQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingBulletTimeState = missingBulletTimeStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingHealOverTimeState = missingHealOverTimeStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPassiveExplosionState = missingPassiveExplosionStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPassiveHealState = missingPassiveHealStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingElementalTrailState = missingElementalTrailStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingElementalTrailAttachedVfxState = missingElementalTrailAttachedVfxStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingBombRequestBuffer = missingBombRequestBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingElementalTrailSegmentBuffer = missingElementalTrailSegmentBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingExplosionRequestBuffer = missingExplosionRequestBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPowerUpVfxRequestBuffer = missingPowerUpVfxRequestBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPowerUpVfxPoolBuffer = missingPowerUpVfxPoolBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPowerUpVfxCapConfig = missingPowerUpVfxCapConfigQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPowerUpCheatBuffer = missingPowerUpCheatBufferQuery.IsEmptyIgnoreFilter == false;

        if (hasMissingState == false &&
            hasMissingPassiveToolsState == false &&
            hasMissingDash == false &&
            hasMissingBulletTimeState == false &&
            hasMissingHealOverTimeState == false &&
            hasMissingPassiveExplosionState == false &&
            hasMissingPassiveHealState == false &&
            hasMissingElementalTrailState == false &&
            hasMissingElementalTrailAttachedVfxState == false &&
            hasMissingBombRequestBuffer == false &&
            hasMissingElementalTrailSegmentBuffer == false &&
            hasMissingExplosionRequestBuffer == false &&
            hasMissingPowerUpVfxRequestBuffer == false &&
            hasMissingPowerUpVfxPoolBuffer == false &&
            hasMissingPowerUpVfxCapConfig == false &&
            hasMissingPowerUpCheatBuffer == false)
            return;

        uint currentKillCount = 0u;

        // if the total kill count is available (meaning the GlobalEnemyKillCounter singleton exists),
        // use it to initialize the LastObservedGlobalKillCount in PlayerPowerUpsState,
        if (SystemAPI.TryGetSingleton<GlobalEnemyKillCounter>(out GlobalEnemyKillCounter killCounter))
            currentKillCount = killCounter.TotalKilled;

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup = SystemAPI.GetBufferLookup<EquippedPassiveToolElement>(true);

        if (hasMissingState)
            AddMissingState(ref commandBuffer, currentKillCount);

        if (hasMissingPassiveToolsState)
            AddMissingPassiveToolsState(ref commandBuffer, in equippedPassiveToolsLookup);

        if (hasMissingDash)
            AddMissingDashState(ref commandBuffer);

        if (hasMissingBulletTimeState)
            AddMissingBulletTimeState(ref commandBuffer);

        if (hasMissingHealOverTimeState)
            AddMissingHealOverTimeState(ref commandBuffer);

        if (hasMissingPassiveExplosionState)
            AddMissingPassiveExplosionState(ref commandBuffer);

        if (hasMissingPassiveHealState)
            AddMissingPassiveHealState(ref commandBuffer);

        if (hasMissingElementalTrailState)
            AddMissingElementalTrailState(ref commandBuffer);

        if (hasMissingElementalTrailAttachedVfxState)
            AddMissingElementalTrailAttachedVfxState(ref commandBuffer);

        if (hasMissingBombRequestBuffer)
            AddMissingBombRequestBuffers(ref commandBuffer);

        if (hasMissingElementalTrailSegmentBuffer)
            AddMissingElementalTrailSegmentBuffers(ref commandBuffer);

        if (hasMissingExplosionRequestBuffer)
            AddMissingExplosionRequestBuffers(ref commandBuffer);

        if (hasMissingPowerUpVfxRequestBuffer)
            AddMissingPowerUpVfxRequestBuffers(ref commandBuffer);

        if (hasMissingPowerUpVfxPoolBuffer)
            AddMissingPowerUpVfxPoolBuffers(ref commandBuffer);

        if (hasMissingPowerUpVfxCapConfig)
            AddMissingPowerUpVfxCapConfig(ref commandBuffer);

        if (hasMissingPowerUpCheatBuffer)
            AddMissingPowerUpCheatBuffers(ref commandBuffer);

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private void AddMissingState(ref EntityCommandBuffer commandBuffer, uint currentKillCount)
    {
        NativeArray<Entity> entities = missingStateQuery.ToEntityArray(Allocator.Temp);
        NativeArray<PlayerPowerUpsConfig> configs = missingStateQuery.ToComponentDataArray<PlayerPowerUpsConfig>(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            PlayerPowerUpsConfig config = configs[index];
            float primaryMaximumEnergy = math.max(0f, config.PrimarySlot.MaximumEnergy);
            float secondaryMaximumEnergy = math.max(0f, config.SecondarySlot.MaximumEnergy);

            commandBuffer.AddComponent(entities[index], new PlayerPowerUpsState
            {
                PrimaryEnergy = primaryMaximumEnergy,
                SecondaryEnergy = secondaryMaximumEnergy,
                PrimaryCooldownRemaining = 0f,
                SecondaryCooldownRemaining = 0f,
                PrimaryCharge = 0f,
                SecondaryCharge = 0f,
                PrimaryIsCharging = 0,
                SecondaryIsCharging = 0,
                IsShootingSuppressed = 0,
                PreviousPrimaryPressed = 0,
                PreviousSecondaryPressed = 0,
                LastObservedGlobalKillCount = currentKillCount,
                LastValidMovementDirection = float3.zero
            });
        }

        entities.Dispose();
        configs.Dispose();
    }

    private void AddMissingPassiveToolsState(ref EntityCommandBuffer commandBuffer, in BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup)
    {
        NativeArray<Entity> entities = missingPassiveToolsStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            PlayerPassiveToolsState passiveToolsState = BuildPassiveToolsState(entity, in equippedPassiveToolsLookup);
            commandBuffer.AddComponent(entity, passiveToolsState);
        }

        entities.Dispose();
    }

    private static PlayerPassiveToolsState BuildPassiveToolsState(Entity entity, in BufferLookup<EquippedPassiveToolElement> equippedPassiveToolsLookup)
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

        if (equippedPassiveToolsLookup.HasBuffer(entity) == false)
            return passiveToolsState;

        DynamicBuffer<EquippedPassiveToolElement> equippedPassiveToolsBuffer = equippedPassiveToolsLookup[entity];

        for (int passiveToolIndex = 0; passiveToolIndex < equippedPassiveToolsBuffer.Length; passiveToolIndex++)
        {
            EquippedPassiveToolElement equippedPassiveTool = equippedPassiveToolsBuffer[passiveToolIndex];
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

    private void AddMissingDashState(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingDashQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerDashState
            {
                IsDashing = 0,
                Phase = 0,
                PhaseRemaining = 0f,
                HoldDuration = 0f,
                RemainingInvulnerability = 0f,
                Direction = float3.zero,
                EntryVelocity = float3.zero,
                Speed = 0f,
                TransitionInDuration = 0f,
                TransitionOutDuration = 0f
            });
        }

        entities.Dispose();
    }

    private void AddMissingBulletTimeState(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingBulletTimeStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerBulletTimeState
            {
                RemainingDuration = 0f,
                SlowPercent = 0f
            });
        }

        entities.Dispose();
    }

    private void AddMissingHealOverTimeState(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingHealOverTimeStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerHealOverTimeState
            {
                IsActive = 0,
                HealPerSecond = 0f,
                RemainingTotalHeal = 0f,
                RemainingDuration = 0f,
                TickIntervalSeconds = 0.2f,
                TickTimer = 0f
            });
        }

        entities.Dispose();
    }

    private void AddMissingPassiveExplosionState(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingPassiveExplosionStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerPassiveExplosionState
            {
                CooldownRemaining = 0f,
                PreviousObservedHealth = -1f
            });
        }

        entities.Dispose();
    }

    private void AddMissingPassiveHealState(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingPassiveHealStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerPassiveHealState
            {
                CooldownRemaining = 0f,
                PreviousObservedHealth = -1f
            });
        }

        entities.Dispose();
    }

    private void AddMissingElementalTrailState(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingElementalTrailStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerElementalTrailState
            {
                LastSpawnPosition = float3.zero,
                SpawnTimer = 0f,
                ActiveSegments = 0,
                Initialized = 0
            });
        }

        entities.Dispose();
    }

    private void AddMissingElementalTrailAttachedVfxState(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingElementalTrailAttachedVfxStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerElementalTrailAttachedVfxState
            {
                VfxEntity = Entity.Null,
                PrefabEntity = Entity.Null
            });
        }

        entities.Dispose();
    }

    private void AddMissingBombRequestBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingBombRequestBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<PlayerBombSpawnRequest>(entities[index]);

        entities.Dispose();
    }

    private void AddMissingElementalTrailSegmentBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingElementalTrailSegmentBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<PlayerElementalTrailSegmentElement>(entities[index]);

        entities.Dispose();
    }

    private void AddMissingExplosionRequestBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingExplosionRequestBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<PlayerExplosionRequest>(entities[index]);

        entities.Dispose();
    }

    private void AddMissingPowerUpVfxRequestBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingPowerUpVfxRequestBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<PlayerPowerUpVfxSpawnRequest>(entities[index]);

        entities.Dispose();
    }

    private void AddMissingPowerUpVfxPoolBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingPowerUpVfxPoolBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<PlayerPowerUpVfxPoolElement>(entities[index]);

        entities.Dispose();
    }

    private void AddMissingPowerUpVfxCapConfig(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingPowerUpVfxCapConfigQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new PlayerPowerUpVfxCapConfig
            {
                MaxSamePrefabPerCell = 6,
                CellSize = 2.5f,
                MaxAttachedSamePrefabPerTarget = 1,
                MaxActiveOneShotVfx = 400,
                RefreshAttachedLifetimeOnCapHit = 1
            });
        }

        entities.Dispose();
    }

    private void AddMissingPowerUpCheatBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingPowerUpCheatBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<PlayerPowerUpCheatCommand>(entities[index]);

        entities.Dispose();
    }
    #endregion

    #endregion
}
