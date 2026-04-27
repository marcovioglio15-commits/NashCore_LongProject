using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
    #region Constants
    private const float TwoPi = 6.283185307179586f;
    #endregion

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
        NativeList<RuleInitializationRequest> initializationRequests = new NativeList<RuleInitializationRequest>(Allocator.Temp);
        NativeList<MinionSpawnRequest> spawnRequests = new NativeList<MinionSpawnRequest>(Allocator.Temp);

        try
        {
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
                        QueueRuleInitialization(initializationRequests, bossEntity, ruleIndex, ref rule, elapsedTime);
                        minionRules[ruleIndex] = rule;
                        continue;
                    }

                    if (ShouldTriggerRule(entityManager,
                                          bossEntity,
                                          ruleIndex,
                                          in rule,
                                          in bossHealth.ValueRO,
                                          in bossRuntime.ValueRO,
                                          elapsedTime))
                    {
                        MarkRuleTriggered(ref rule, in bossRuntime.ValueRO, elapsedTime);
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
            ProcessMinionSpawnRequests(entityManager, spawnRequests);
        }
        finally
        {
            if (initializationRequests.IsCreated)
                initializationRequests.Dispose();

            if (spawnRequests.IsCreated)
                spawnRequests.Dispose();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Queues pool initialization for one boss minion rule without performing structural changes during query iteration.
    /// /params initializationRequests Request list filled by the current update.
    /// /params bossEntity Boss that owns the rule.
    /// /params ruleIndex Rule index on the boss buffer.
    /// /params rule Mutable rule state.
    /// /params elapsedTime Current world elapsed time.
    /// /returns None.
    /// </summary>
    private static void QueueRuleInitialization(NativeList<RuleInitializationRequest> initializationRequests,
                                                Entity bossEntity,
                                                int ruleIndex,
                                                ref EnemyBossMinionSpawnElement rule,
                                                float elapsedTime)
    {
        rule.Initialized = 1;
        rule.NextSpawnTime = ResolveInitialNextSpawnTime(in rule, elapsedTime);
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
    /// /returns None.
    /// </summary>
    private static void ProcessMinionSpawnRequests(EntityManager entityManager,
                                                   NativeList<MinionSpawnRequest> spawnRequests)
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

            SpawnMinions(entityManager,
                         request.BossEntity,
                         request.RuleIndex,
                         request.BossPosition,
                         ref rule);

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
    /// Evaluates the configured trigger and alive cap for one minion rule.
    /// /params entityManager Entity manager used to count active minions.
    /// /params bossEntity Boss that owns the rule.
    /// /params ruleIndex Rule index being evaluated.
    /// /params rule Rule runtime data.
    /// /params bossHealth Boss health state.
    /// /params bossRuntime Boss runtime state.
    /// /params elapsedTime Current world elapsed time.
    /// /returns True when the rule should spawn minions now.
    /// </summary>
    private static bool ShouldTriggerRule(EntityManager entityManager,
                                          Entity bossEntity,
                                          int ruleIndex,
                                          in EnemyBossMinionSpawnElement rule,
                                          in EnemyHealth bossHealth,
                                          in EnemyRuntimeState bossRuntime,
                                          float elapsedTime)
    {
        if (rule.PoolEntity == Entity.Null || rule.SpawnCount <= 0)
        {
            return false;
        }

        if (rule.MaxAliveMinions > 0 &&
            CountAliveMinions(entityManager, bossEntity, ruleIndex) >= rule.MaxAliveMinions)
        {
            return false;
        }

        switch (rule.Trigger)
        {
            case EnemyBossMinionSpawnTrigger.BossDamaged:
                if (elapsedTime < rule.NextSpawnTime)
                {
                    return false;
                }

                return bossRuntime.HasTakenDamage != 0 &&
                       bossRuntime.LastDamageLifetimeSeconds > rule.LastObservedDamageLifetimeSeconds;

            case EnemyBossMinionSpawnTrigger.HealthBelowPercent:
                if (rule.Triggered != 0)
                {
                    return false;
                }

                if (bossHealth.Max <= 0f)
                {
                    return false;
                }

                return math.saturate(bossHealth.Current / bossHealth.Max) <= math.saturate(rule.HealthThresholdPercent);

            default:
                return elapsedTime >= rule.NextSpawnTime;
        }
    }

    /// <summary>
    /// Counts active minions currently owned by one boss rule.
    /// /params entityManager Entity manager used to inspect component-enabled state.
    /// /params bossEntity Boss that owns the minions.
    /// /params ruleIndex Rule index to count.
    /// /returns Active minion count.
    /// </summary>
    private static int CountAliveMinions(EntityManager entityManager, Entity bossEntity, int ruleIndex)
    {
        int count = 0;
        EntityQuery query = entityManager.CreateEntityQuery(typeof(EnemyBossMinionOwner), typeof(EnemyActive));
        Unity.Collections.NativeArray<Entity> minionEntities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        try
        {
            for (int index = 0; index < minionEntities.Length; index++)
            {
                Entity minionEntity = minionEntities[index];

                if (!entityManager.Exists(minionEntity))
                    continue;

                if (!entityManager.IsComponentEnabled<EnemyActive>(minionEntity))
                    continue;

                EnemyBossMinionOwner owner = entityManager.GetComponentData<EnemyBossMinionOwner>(minionEntity);

                if (owner.BossEntity == bossEntity && owner.RuleIndex == ruleIndex)
                    count++;
            }
        }
        finally
        {
            if (minionEntities.IsCreated)
                minionEntities.Dispose();

            query.Dispose();
        }

        return count;
    }

    /// <summary>
    /// Activates up to the configured spawn count from the rule pool.
    /// /params entityManager Entity manager used to mutate pooled minions.
    /// /params bossEntity Boss that owns the minions.
    /// /params ruleIndex Rule index being spawned.
    /// /params bossPosition Current boss position.
    /// /params rule Mutable rule runtime data.
    /// /returns None.
    /// </summary>
    private static void SpawnMinions(EntityManager entityManager,
                                     Entity bossEntity,
                                     int ruleIndex,
                                     float3 bossPosition,
                                     ref EnemyBossMinionSpawnElement rule)
    {
        int availableSlots = rule.MaxAliveMinions > 0
            ? math.max(0, rule.MaxAliveMinions - CountAliveMinions(entityManager, bossEntity, ruleIndex))
            : rule.SpawnCount;
        int spawnCount = math.min(math.max(0, rule.SpawnCount), availableSlots);

        for (int spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
        {
            if (!TryAcquireMinion(entityManager, rule.PoolEntity, rule.PrefabEntity, out Entity minionEntity))
                return;

            float3 spawnPosition = ResolveSpawnPosition(bossPosition, rule.SpawnRadius, bossEntity, ruleIndex, spawnIndex);
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
            RuleIndex = ruleIndex
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

    /// <summary>
    /// Updates trigger bookkeeping after a rule spawns.
    /// /params rule Mutable rule state.
    /// /params bossRuntime Boss runtime state.
    /// /params elapsedTime Current world elapsed time.
    /// /returns None.
    /// </summary>
    private static void MarkRuleTriggered(ref EnemyBossMinionSpawnElement rule,
                                          in EnemyRuntimeState bossRuntime,
                                          float elapsedTime)
    {
        rule.NextSpawnTime = ResolveNextSpawnTime(in rule, elapsedTime);
        rule.LastObservedDamageLifetimeSeconds = bossRuntime.LastDamageLifetimeSeconds;

        if (rule.Trigger == EnemyBossMinionSpawnTrigger.HealthBelowPercent)
        {
            rule.Triggered = 1;
        }
    }

    /// <summary>
    /// Resolves the first allowed spawn time for one freshly initialized rule.
    /// /params rule Rule being initialized.
    /// /params elapsedTime Current world elapsed time.
    /// /returns Initial spawn-ready timestamp.
    /// </summary>
    private static float ResolveInitialNextSpawnTime(in EnemyBossMinionSpawnElement rule, float elapsedTime)
    {
        switch (rule.Trigger)
        {
            case EnemyBossMinionSpawnTrigger.Interval:
                return elapsedTime + math.max(0.01f, rule.IntervalSeconds);

            default:
                return elapsedTime;
        }
    }

    /// <summary>
    /// Resolves the next allowed spawn time after one trigger activation.
    /// /params rule Rule that just spawned minions.
    /// /params elapsedTime Current world elapsed time.
    /// /returns Next spawn-ready timestamp.
    /// </summary>
    private static float ResolveNextSpawnTime(in EnemyBossMinionSpawnElement rule, float elapsedTime)
    {
        switch (rule.Trigger)
        {
            case EnemyBossMinionSpawnTrigger.BossDamaged:
                return elapsedTime + math.max(0f, rule.BossHitCooldownSeconds);

            default:
                return elapsedTime + math.max(0.01f, rule.IntervalSeconds);
        }
    }

    /// <summary>
    /// Resolves a deterministic spawn point around the boss.
    /// /params bossPosition Boss world position.
    /// /params radius Spawn radius.
    /// /params bossEntity Boss entity used for deterministic variation.
    /// /params ruleIndex Source rule index.
    /// /params spawnIndex Current spawn index.
    /// /returns World spawn position.
    /// </summary>
    private static float3 ResolveSpawnPosition(float3 bossPosition,
                                               float radius,
                                               Entity bossEntity,
                                               int ruleIndex,
                                               int spawnIndex)
    {
        uint seed = math.hash(new int4(bossEntity.Index,
                                       bossEntity.Version,
                                       ruleIndex + 17,
                                       spawnIndex + 113));
        float normalized = (seed & 0x00FFFFFFu) / 16777215f;
        float angle = normalized * TwoPi;
        float resolvedRadius = math.max(0f, radius);
        float3 position = bossPosition;
        position.x += math.cos(angle) * resolvedRadius;
        position.z += math.sin(angle) * resolvedRadius;
        return position;
    }
    #endregion

    #endregion
}
