using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Maintains boss-owned minion pools and activates normal enemy minions from boss spawn rules.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyBossPatternRuntimeSystem))]
[UpdateBefore(typeof(EnemyShooterRequestSystem))]
public partial struct EnemyBossMinionSpawnSystem : ISystem
{
    #region Nested Types
    /// <summary>
    /// Stores one pool initialization request collected while iterating boss entities.
    /// /params None.
    /// /returns None.
    /// </summary>
    private struct RuleInitializationRequest
    {
        public Entity BossEntity;
        public int RuleIndex;
        public EnemyBossMinionSpawnElement Rule;
    }

    /// <summary>
    /// Stores one minion spawn request collected while iterating boss entities.
    /// /params None.
    /// /returns None.
    /// </summary>
    private struct MinionSpawnRequest
    {
        public Entity BossEntity;
        public int RuleIndex;
        public float3 BossPosition;
        public EnemyBossMinionSpawnElement Rule;
    }

    private struct BossMinionRuleKey : System.IEquatable<BossMinionRuleKey>
    {
        public Entity BossEntity;
        public int RuleIndex;

        public bool Equals(BossMinionRuleKey other)
        {
            return BossEntity == other.BossEntity && RuleIndex == other.RuleIndex;
        }

        public override int GetHashCode()
        {
            return (int)math.hash(new int3(BossEntity.Index, BossEntity.Version, RuleIndex));
        }
    }
    #endregion

    #region Fields
    private EntityQuery activeMinionQuery;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares boss minion spawn buffers as runtime dependencies.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        activeMinionQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyBossMinionOwner, EnemyActive>()
            .Build();
        state.RequireForUpdate<EnemyBossMinionSpawnElement>();
    }

    /// <summary>
    /// Initializes missing pools and evaluates minion spawn triggers.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        Allocator frameAllocator = state.WorldUpdateAllocator;
        NativeList<RuleInitializationRequest> initializationRequests = new NativeList<RuleInitializationRequest>(frameAllocator);
        NativeList<MinionSpawnRequest> spawnRequests = new NativeList<MinionSpawnRequest>(frameAllocator);
        NativeParallelHashMap<BossMinionRuleKey, int> aliveMinionCounts = new NativeParallelHashMap<BossMinionRuleKey, int>(math.max(1, activeMinionQuery.CalculateEntityCount()), frameAllocator);
        NativeArray<EnemyNavigationCellElement> navigationCellSnapshot = default;
        PhysicsWorldSingleton physicsWorldSingleton = default;
        bool hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out physicsWorldSingleton);
        int wallsLayerMask = WorldWallCollisionUtility.ResolveWallsLayerMask();
        EnemyNavigationGridState navigationGridState = default;
        bool navigationReady = false;

        if (SystemAPI.TryGetSingleton<PlayerWorldLayersConfig>(out PlayerWorldLayersConfig worldLayersConfig) &&
            worldLayersConfig.WallsLayerMask != 0)
        {
            wallsLayerMask = worldLayersConfig.WallsLayerMask;
        }

        BuildAliveMinionCounts(ref state, ref aliveMinionCounts);

        foreach ((RefRO<EnemyHealth> bossHealth,
                  RefRO<EnemyRuntimeState> bossRuntime,
                  RefRO<LocalTransform> bossTransform,
                  Entity bossEntity)
                 in SystemAPI.Query<RefRO<EnemyHealth>,
                                    RefRO<EnemyRuntimeState>,
                                    RefRO<LocalTransform>>()
                             .WithAll<EnemyBossTag, EnemyActive>()
                             .WithNone<EnemyDespawnRequest>()
                             .WithEntityAccess())
        {
            DynamicBuffer<EnemyBossMinionSpawnElement> minionRules = entityManager.GetBuffer<EnemyBossMinionSpawnElement>(bossEntity);

            for (int ruleIndex = 0; ruleIndex < minionRules.Length; ruleIndex++)
            {
                EnemyBossMinionSpawnElement rule = minionRules[ruleIndex];

                if (rule.Initialized == 0)
                {
                    QueueRuleInitialization(initializationRequests,
                                            bossEntity,
                                            ruleIndex,
                                            ref rule,
                                            in bossRuntime.ValueRO,
                                            elapsedTime);
                    minionRules[ruleIndex] = rule;
                    continue;
                }

                int aliveMinionCount = ResolveAliveMinionCount(in aliveMinionCounts, bossEntity, ruleIndex);

                if (EnemyBossMinionSpawnTriggerUtility.ShouldTriggerRule(aliveMinionCount,
                                                                         ref rule,
                                                                         in bossHealth.ValueRO,
                                                                         in bossRuntime.ValueRO,
                                                                         elapsedTime))
                {
                    EnemyBossMinionSpawnTriggerUtility.MarkRuleTriggered(ref rule, in bossRuntime.ValueRO, elapsedTime);
                    QueueMinionSpawn(spawnRequests,
                                     bossEntity,
                                     ruleIndex,
                                     bossTransform.ValueRO.Position,
                                     in rule);
                }

                minionRules[ruleIndex] = rule;
            }
        }

        ProcessRuleInitializationRequests(entityManager, initializationRequests);

        if (spawnRequests.Length > 0 &&
            SystemAPI.TryGetSingleton<EnemyNavigationGridState>(out navigationGridState) &&
            SystemAPI.TryGetSingletonBuffer<EnemyNavigationCellElement>(out DynamicBuffer<EnemyNavigationCellElement> navigationCells) &&
            navigationGridState.Initialized != 0 &&
            navigationGridState.FlowReady != 0 &&
            navigationCells.Length > 0)
        {
            navigationCellSnapshot = CollectionHelper.CreateNativeArray(navigationCells.AsNativeArray(), frameAllocator);
            navigationReady = navigationCellSnapshot.IsCreated && navigationCellSnapshot.Length > 0;
        }

        ProcessMinionSpawnRequests(entityManager,
                                   spawnRequests,
                                   in aliveMinionCounts,
                                   hasPhysicsWorld,
                                   in physicsWorldSingleton,
                                   wallsLayerMask,
                                   navigationReady,
                                   in navigationGridState,
                                   navigationCellSnapshot);
    }
    #endregion

    #region Private Methods
    private void BuildAliveMinionCounts(ref SystemState state,
                                        ref NativeParallelHashMap<BossMinionRuleKey, int> aliveMinionCounts)
    {
        foreach ((RefRO<EnemyBossMinionOwner> owner,
                  EnabledRefRO<EnemyActive> enemyActive)
                 in SystemAPI.Query<RefRO<EnemyBossMinionOwner>, EnabledRefRO<EnemyActive>>()
                             .WithAll<EnemyActive>())
        {
            if (!enemyActive.ValueRO)
                continue;

            BossMinionRuleKey key = new BossMinionRuleKey
            {
                BossEntity = owner.ValueRO.BossEntity,
                RuleIndex = owner.ValueRO.RuleIndex
            };

            if (aliveMinionCounts.TryGetValue(key, out int count))
            {
                aliveMinionCounts[key] = count + 1;
                continue;
            }

            aliveMinionCounts.Add(key, 1);
        }
    }

    private static int ResolveAliveMinionCount(in NativeParallelHashMap<BossMinionRuleKey, int> aliveMinionCounts,
                                               Entity bossEntity,
                                               int ruleIndex)
    {
        BossMinionRuleKey key = new BossMinionRuleKey
        {
            BossEntity = bossEntity,
            RuleIndex = ruleIndex
        };

        return aliveMinionCounts.TryGetValue(key, out int count) ? count : 0;
    }

    /// <summary>
    /// Queues pool initialization for one boss minion rule without performing structural changes during query iteration.
    /// /params initializationRequests Request list filled by the current update.
    /// /params bossEntity Boss that owns the rule.
    /// /params ruleIndex Rule index on the boss buffer.
    /// /params rule Mutable rule state.
    /// /params bossRuntime Boss runtime state used by damage-trigger cooldowns.
    /// /params elapsedTime Current world elapsed time.
    /// /returns None.
    /// </summary>
    private static void QueueRuleInitialization(NativeList<RuleInitializationRequest> initializationRequests,
                                                Entity bossEntity,
                                                int ruleIndex,
                                                ref EnemyBossMinionSpawnElement rule,
                                                in EnemyRuntimeState bossRuntime,
                                                float elapsedTime)
    {
        rule.Initialized = 1;
        rule.NextSpawnTime = EnemyBossMinionSpawnTriggerUtility.ResolveInitialNextSpawnTime(in rule, in bossRuntime, elapsedTime);
        rule.LastObservedDamageLifetimeSeconds = 0f;

        initializationRequests.Add(new RuleInitializationRequest
        {
            BossEntity = bossEntity,
            RuleIndex = ruleIndex,
            Rule = rule
        });
    }

    /// <summary>
    /// Queues one spawn request so pooled minions are activated after query iteration has completed.
    /// /params spawnRequests Request list filled by the current update.
    /// /params bossEntity Boss that owns the minions.
    /// /params ruleIndex Rule index on the boss buffer.
    /// /params bossPosition Current boss world position.
    /// /params rule Rule data used for spawning.
    /// /returns None.
    /// </summary>
    private static void QueueMinionSpawn(NativeList<MinionSpawnRequest> spawnRequests,
                                         Entity bossEntity,
                                         int ruleIndex,
                                         float3 bossPosition,
                                         in EnemyBossMinionSpawnElement rule)
    {
        spawnRequests.Add(new MinionSpawnRequest
        {
            BossEntity = bossEntity,
            RuleIndex = ruleIndex,
            BossPosition = bossPosition,
            Rule = rule
        });
    }

    /// <summary>
    /// Performs queued pool creation and prewarming after boss entity iteration has completed.
    /// /params entityManager Entity manager used for structural changes.
    /// /params initializationRequests Requests collected during the simulation pass.
    /// /returns None.
    /// </summary>
    private static void ProcessRuleInitializationRequests(EntityManager entityManager,
                                                         NativeList<RuleInitializationRequest> initializationRequests)
    {
        for (int requestIndex = 0; requestIndex < initializationRequests.Length; requestIndex++)
        {
            RuleInitializationRequest request = initializationRequests[requestIndex];

            if (!entityManager.Exists(request.BossEntity))
                continue;

            if (!entityManager.HasBuffer<EnemyBossMinionSpawnElement>(request.BossEntity))
                continue;

            EnemyBossMinionSpawnElement rule = request.Rule;

            if (rule.PrefabEntity != Entity.Null && entityManager.Exists(rule.PrefabEntity))
                rule.PoolEntity = CreateAndPrewarmRulePool(entityManager, in rule);

            DynamicBuffer<EnemyBossMinionSpawnElement> minionRules = entityManager.GetBuffer<EnemyBossMinionSpawnElement>(request.BossEntity);

            if (request.RuleIndex < 0 || request.RuleIndex >= minionRules.Length)
                continue;

            minionRules[request.RuleIndex] = rule;
        }
    }

    /// <summary>
    /// Creates and prewarms the pool entity required by one boss minion rule.
    /// /params entityManager Entity manager used for structural changes.
    /// /params rule Rule that owns pool sizing and prefab data.
    /// /returns Created pool entity, or Entity.Null when no valid prefab exists.
    /// </summary>
    private static Entity CreateAndPrewarmRulePool(EntityManager entityManager,
                                                   in EnemyBossMinionSpawnElement rule)
    {
        if (rule.PrefabEntity == Entity.Null || !entityManager.Exists(rule.PrefabEntity))
            return Entity.Null;

        Entity poolEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(poolEntity, new EnemyPoolState
        {
            PrefabEntity = rule.PrefabEntity,
            InitialCapacity = math.max(0, rule.AutomaticPoolSize),
            ExpandBatch = math.max(1, rule.PoolExpandBatch),
            Initialized = 1
        });
        entityManager.AddComponentData(poolEntity, new EnemySpawner
        {
            InitialPoolCapacityPerPrefab = math.max(0, rule.AutomaticPoolSize),
            ExpandBatchPerPrefab = math.max(1, rule.PoolExpandBatch),
            DespawnDistance = math.max(0f, rule.DespawnDistance),
            MaximumSpawnDistanceFromCenter = 0f,
            TotalPlannedEnemyCount = 0
        });
        entityManager.AddBuffer<EnemyPoolElement>(poolEntity);
        EnemyPoolUtility.ExpandPool(entityManager,
                                    poolEntity,
                                    poolEntity,
                                    rule.PrefabEntity,
                                    math.max(0, rule.AutomaticPoolSize));
        return poolEntity;
    }

    /// <summary>
    /// Performs queued minion activations after boss entity iteration has completed.
    /// /params entityManager Entity manager used to mutate pooled minions.
    /// /params spawnRequests Requests collected during the simulation pass.
    /// /params hasPhysicsWorld True when wall queries can be evaluated.
    /// /params physicsWorldSingleton Physics world used for spawn safety checks.
    /// /params wallsLayerMask Wall layer mask used by spawn safety checks.
    /// /params navigationReady True when the shared navigation grid can project spawn positions.
    /// /params navigationGridState Shared navigation grid state.
    /// /params navigationCells Stable navigation cell snapshot safe across structural changes.
    /// /returns None.
    /// </summary>
    private static void ProcessMinionSpawnRequests(EntityManager entityManager,
                                                   NativeList<MinionSpawnRequest> spawnRequests,
                                                   in NativeParallelHashMap<BossMinionRuleKey, int> aliveMinionCounts,
                                                   bool hasPhysicsWorld,
                                                   in PhysicsWorldSingleton physicsWorldSingleton,
                                                   int wallsLayerMask,
                                                   bool navigationReady,
                                                   in EnemyNavigationGridState navigationGridState,
                                                   NativeArray<EnemyNavigationCellElement> navigationCells)
    {
        for (int requestIndex = 0; requestIndex < spawnRequests.Length; requestIndex++)
        {
            MinionSpawnRequest request = spawnRequests[requestIndex];

            if (!entityManager.Exists(request.BossEntity))
                continue;

            if (!entityManager.HasBuffer<EnemyBossMinionSpawnElement>(request.BossEntity))
                continue;

            EnemyBossMinionSpawnElement rule = ResolveCurrentRule(entityManager,
                                                                  request.BossEntity,
                                                                  request.RuleIndex,
                                                                  in request.Rule);

            if (rule.PoolEntity == Entity.Null || !entityManager.Exists(rule.PoolEntity))
                continue;

            int aliveMinionCount = ResolveAliveMinionCount(in aliveMinionCounts, request.BossEntity, request.RuleIndex);

            SpawnMinions(entityManager,
                         request.BossEntity,
                         request.RuleIndex,
                         request.BossPosition,
                         ref rule,
                         aliveMinionCount,
                         hasPhysicsWorld,
                         in physicsWorldSingleton,
                         wallsLayerMask,
                         navigationReady,
                         in navigationGridState,
                         navigationCells);

            WriteCurrentRule(entityManager, request.BossEntity, request.RuleIndex, in rule);
        }
    }

    /// <summary>
    /// Reads the current rule from the boss buffer without keeping the buffer alive across structural changes.
    /// /params entityManager Entity manager used to access the boss buffer.
    /// /params bossEntity Boss that owns the rule.
    /// /params ruleIndex Rule index inside the boss buffer.
    /// /params fallbackRule Request-time rule used when the buffer index is no longer valid.
    /// /returns Current rule data.
    /// </summary>
    private static EnemyBossMinionSpawnElement ResolveCurrentRule(EntityManager entityManager,
                                                                  Entity bossEntity,
                                                                  int ruleIndex,
                                                                  in EnemyBossMinionSpawnElement fallbackRule)
    {
        DynamicBuffer<EnemyBossMinionSpawnElement> minionRules = entityManager.GetBuffer<EnemyBossMinionSpawnElement>(bossEntity);

        if (ruleIndex < 0 || ruleIndex >= minionRules.Length)
            return fallbackRule;

        return minionRules[ruleIndex];
    }

    /// <summary>
    /// Writes an updated rule back after structural changes have completed, reacquiring the buffer handle.
    /// /params entityManager Entity manager used to access the boss buffer.
    /// /params bossEntity Boss that owns the rule.
    /// /params ruleIndex Rule index inside the boss buffer.
    /// /params rule Updated rule data.
    /// /returns None.
    /// </summary>
    private static void WriteCurrentRule(EntityManager entityManager,
                                         Entity bossEntity,
                                         int ruleIndex,
                                         in EnemyBossMinionSpawnElement rule)
    {
        if (!entityManager.Exists(bossEntity))
            return;

        if (!entityManager.HasBuffer<EnemyBossMinionSpawnElement>(bossEntity))
            return;

        DynamicBuffer<EnemyBossMinionSpawnElement> minionRules = entityManager.GetBuffer<EnemyBossMinionSpawnElement>(bossEntity);

        if (ruleIndex < 0 || ruleIndex >= minionRules.Length)
            return;

        minionRules[ruleIndex] = rule;
    }

    /// <summary>
    /// Activates up to the configured spawn count from the rule pool.
    /// /params entityManager Entity manager used to mutate pooled minions.
    /// /params bossEntity Boss that owns the minions.
    /// /params ruleIndex Rule index being spawned.
    /// /params bossPosition Current boss position.
    /// /params rule Mutable rule runtime data.
    /// /params hasPhysicsWorld True when wall queries can be evaluated.
    /// /params physicsWorldSingleton Physics world used for spawn safety checks.
    /// /params wallsLayerMask Wall layer mask used by spawn safety checks.
    /// /params navigationReady True when the shared navigation grid can project spawn positions.
    /// /params navigationGridState Shared navigation grid state.
    /// /params navigationCells Stable navigation cell snapshot safe across structural changes.
    /// /returns None.
    /// </summary>
    private static void SpawnMinions(EntityManager entityManager,
                                     Entity bossEntity,
                                     int ruleIndex,
                                     float3 bossPosition,
                                     ref EnemyBossMinionSpawnElement rule,
                                     int aliveMinionCount,
                                     bool hasPhysicsWorld,
                                     in PhysicsWorldSingleton physicsWorldSingleton,
                                     int wallsLayerMask,
                                     bool navigationReady,
                                     in EnemyNavigationGridState navigationGridState,
                                     NativeArray<EnemyNavigationCellElement> navigationCells)
    {
        int availableSlots = rule.MaxAliveMinions > 0
            ? math.max(0, rule.MaxAliveMinions - aliveMinionCount)
            : rule.SpawnCount;
        int spawnCount = math.min(math.max(0, rule.SpawnCount), availableSlots);

        for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
        {
            if (!TryAcquireMinion(entityManager, rule.PoolEntity, rule.PrefabEntity, out Entity minionEntity))
                return;

            EnemyData minionData = entityManager.HasComponent<EnemyData>(minionEntity)
                ? entityManager.GetComponentData<EnemyData>(minionEntity)
                : default;
            float3 spawnPosition = EnemyBossMinionSpawnPositionUtility.ResolveSpawnPosition(bossPosition,
                                                                                            rule.SpawnRadius,
                                                                                            bossEntity,
                                                                                            ruleIndex,
                                                                                            spawnIndex,
                                                                                            in minionData,
                                                                                            hasPhysicsWorld,
                                                                                            in physicsWorldSingleton,
                                                                                            wallsLayerMask,
                                                                                            navigationReady,
                                                                                            in navigationGridState,
                                                                                            navigationCells);
            EnemyPoolUtility.ActivateEnemy(entityManager,
                                           minionEntity,
                                           rule.PoolEntity,
                                           rule.PoolEntity,
                                           -1,
                                           spawnPosition);
            ApplyMinionMetadata(entityManager, minionEntity, bossEntity, ruleIndex, in rule);
        }
    }

    /// <summary>
    /// Acquires one inactive minion from a rule pool, expanding the pool when empty.
    /// /params entityManager Entity manager used to access the pool.
    /// /params poolEntity Pool entity.
    /// /params prefabEntity Enemy prefab entity.
    /// /params minionEntity Output acquired minion.
    /// /returns True when a minion was acquired.
    /// </summary>
    private static bool TryAcquireMinion(EntityManager entityManager,
                                         Entity poolEntity,
                                         Entity prefabEntity,
                                         out Entity minionEntity)
    {
        minionEntity = Entity.Null;

        if (poolEntity == Entity.Null || !entityManager.Exists(poolEntity))
            return false;

        if (!entityManager.HasBuffer<EnemyPoolElement>(poolEntity))
            return false;

        DynamicBuffer<EnemyPoolElement> poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);

        if (poolBuffer.Length <= 0 && entityManager.HasComponent<EnemyPoolState>(poolEntity))
        {
            EnemyPoolState poolState = entityManager.GetComponentData<EnemyPoolState>(poolEntity);
            EnemyPoolUtility.ExpandPool(entityManager,
                                        poolEntity,
                                        poolEntity,
                                        prefabEntity,
                                        math.max(1, poolState.ExpandBatch));
            poolBuffer = entityManager.GetBuffer<EnemyPoolElement>(poolEntity);
        }

        while (poolBuffer.Length > 0)
        {
            int lastIndex = poolBuffer.Length - 1;
            minionEntity = poolBuffer[lastIndex].EnemyEntity;
            poolBuffer.RemoveAt(lastIndex);

            if (entityManager.Exists(minionEntity))
                return true;
        }

        minionEntity = Entity.Null;
        return false;
    }

    /// <summary>
    /// Writes boss ownership and reward multipliers onto one activated minion.
    /// /params entityManager Entity manager used to mutate the minion.
    /// /params minionEntity Activated minion.
    /// /params bossEntity Boss that owns the minion.
    /// /params ruleIndex Source rule index.
    /// /params rule Rule data supplying reward multipliers.
    /// /returns None.
    /// </summary>
    private static void ApplyMinionMetadata(EntityManager entityManager,
                                            Entity minionEntity,
                                            Entity bossEntity,
                                            int ruleIndex,
                                            in EnemyBossMinionSpawnElement rule)
    {
        EnemyBossMinionOwner owner = new EnemyBossMinionOwner
        {
            BossEntity = bossEntity,
            RuleIndex = ruleIndex,
            KillOnBossDeath = rule.KillMinionsOnBossDeath,
            BlocksRunCompletion = rule.RequireMinionsKilledForRunCompletion
        };

        if (entityManager.HasComponent<EnemyBossMinionOwner>(minionEntity))
            entityManager.SetComponentData(minionEntity, owner);
        else
            entityManager.AddComponentData(minionEntity, owner);

        EnemyDropRewardMultiplier rewardMultiplier = new EnemyDropRewardMultiplier
        {
            ExperienceMultiplier = math.max(0f, rule.ExperienceDropMultiplier),
            ExtraComboPointsMultiplier = math.max(0f, rule.ExtraComboPointsMultiplier),
            FutureDropsMultiplier = math.max(0f, rule.FutureDropsMultiplier)
        };

        if (entityManager.HasComponent<EnemyDropRewardMultiplier>(minionEntity))
            entityManager.SetComponentData(minionEntity, rewardMultiplier);
        else
            entityManager.AddComponentData(minionEntity, rewardMultiplier);
    }

    #endregion

    #endregion
}
