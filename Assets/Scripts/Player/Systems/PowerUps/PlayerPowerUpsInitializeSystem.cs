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
    private EntityQuery missingPassiveExplosionStateQuery;
    private EntityQuery missingElementalTrailStateQuery;
    private EntityQuery missingElementalTrailAttachedVfxStateQuery;
    private EntityQuery missingBombRequestBufferQuery;
    private EntityQuery missingElementalTrailSegmentBufferQuery;
    private EntityQuery missingExplosionRequestBufferQuery;
    private EntityQuery missingPowerUpVfxRequestBufferQuery;
    private EntityQuery missingPowerUpVfxPoolBufferQuery;
    private EntityQuery missingPowerUpVfxCapConfigQuery;
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

        missingPassiveExplosionStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerPowerUpsConfig>()
            .WithNone<PlayerPassiveExplosionState>()
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
        bool hasMissingPassiveExplosionState = missingPassiveExplosionStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingElementalTrailState = missingElementalTrailStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingElementalTrailAttachedVfxState = missingElementalTrailAttachedVfxStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingBombRequestBuffer = missingBombRequestBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingElementalTrailSegmentBuffer = missingElementalTrailSegmentBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingExplosionRequestBuffer = missingExplosionRequestBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPowerUpVfxRequestBuffer = missingPowerUpVfxRequestBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPowerUpVfxPoolBuffer = missingPowerUpVfxPoolBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingPowerUpVfxCapConfig = missingPowerUpVfxCapConfigQuery.IsEmptyIgnoreFilter == false;

        if (hasMissingState == false &&
            hasMissingPassiveToolsState == false &&
            hasMissingDash == false &&
            hasMissingBulletTimeState == false &&
            hasMissingPassiveExplosionState == false &&
            hasMissingElementalTrailState == false &&
            hasMissingElementalTrailAttachedVfxState == false &&
            hasMissingBombRequestBuffer == false &&
            hasMissingElementalTrailSegmentBuffer == false &&
            hasMissingExplosionRequestBuffer == false &&
            hasMissingPowerUpVfxRequestBuffer == false &&
            hasMissingPowerUpVfxPoolBuffer == false &&
            hasMissingPowerUpVfxCapConfig == false)
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

        if (hasMissingPassiveExplosionState)
            AddMissingPassiveExplosionState(ref commandBuffer);

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
            ElementalTrail = default
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

        switch (passiveToolConfig.ToolKind)
        {
            case PassiveToolKind.ProjectileSize:
                passiveToolsState.ProjectileSizeMultiplier *= math.max(0.01f, passiveToolConfig.ProjectileSize.SizeMultiplier);
                passiveToolsState.ProjectileDamageMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.DamageMultiplier);
                passiveToolsState.ProjectileSpeedMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.SpeedMultiplier);
                passiveToolsState.ProjectileLifetimeSecondsMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.LifetimeSecondsMultiplier);
                passiveToolsState.ProjectileLifetimeRangeMultiplier *= math.max(0f, passiveToolConfig.ProjectileSize.LifetimeRangeMultiplier);
                return;
            case PassiveToolKind.ElementalProjectiles:
                passiveToolsState.HasElementalProjectiles = 1;
                passiveToolsState.ElementalProjectiles = passiveToolConfig.ElementalProjectiles;
                return;
            case PassiveToolKind.PerfectCircle:
                passiveToolsState.HasPerfectCircle = 1;
                passiveToolsState.PerfectCircle = passiveToolConfig.PerfectCircle;
                return;
            case PassiveToolKind.BouncingProjectiles:
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
                return;
            case PassiveToolKind.SplittingProjectiles:
                passiveToolsState.HasSplittingProjectiles = 1;
                passiveToolsState.SplittingProjectiles = passiveToolConfig.SplittingProjectiles;
                return;
            case PassiveToolKind.Explosion:
                passiveToolsState.HasExplosion = 1;

                if (passiveToolsState.Explosion.Radius <= 0f)
                {
                    passiveToolsState.Explosion = passiveToolConfig.Explosion;
                    return;
                }

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

                return;
            case PassiveToolKind.ElementalTrail:
                passiveToolsState.HasElementalTrail = 1;
                passiveToolsState.ElementalTrail = passiveToolConfig.ElementalTrail;
                return;
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
    #endregion

    #endregion
}
