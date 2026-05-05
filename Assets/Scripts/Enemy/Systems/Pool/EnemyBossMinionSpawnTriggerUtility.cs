using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Evaluates boss minion spawn trigger state and shared runtime bookkeeping for spawn rules.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyBossMinionSpawnTriggerUtility
{
    #region Methods

    #region Trigger Evaluation
    /// <summary>
    /// Evaluates the configured trigger and alive cap for one minion rule.
    /// /params aliveMinionCount Current active minions for the source boss rule.
    /// /params rule Rule runtime data, mutated to consume non-spawnable damage-hit triggers.
    /// /params bossHealth Boss health state.
    /// /params bossRuntime Boss runtime state.
    /// /params elapsedTime Current world elapsed time. Damage-trigger rules use boss lifetime for cooldown timestamps.
    /// /returns True when the rule should spawn minions now.
    /// </summary>
    public static bool ShouldTriggerRule(int aliveMinionCount,
                                         ref EnemyBossMinionSpawnElement rule,
                                         in EnemyHealth bossHealth,
                                         in EnemyRuntimeState bossRuntime,
                                         float elapsedTime)
    {
        // Consume damage hits that cannot spawn because the rule has no valid output.
        if (rule.PoolEntity == Entity.Null || rule.SpawnCount <= 0)
        {
            ConsumeBlockedBossDamageTrigger(ref rule, in bossRuntime);
            return false;
        }

        // Consume damage hits that arrive while the alive cap keeps the rule blocked.
        if (rule.MaxAliveMinions > 0 && aliveMinionCount >= rule.MaxAliveMinions)
        {
            ConsumeBlockedBossDamageTrigger(ref rule, in bossRuntime);
            return false;
        }

        // Dispatch by trigger type after shared gates have been evaluated.
        switch (rule.Trigger)
        {
            case EnemyBossMinionSpawnTrigger.BossDamaged:
                return ShouldTriggerBossDamagedRule(ref rule, in bossRuntime);

            case EnemyBossMinionSpawnTrigger.HealthBelowPercent:
                return ShouldTriggerHealthBelowPercentRule(in rule, in bossHealth);

            default:
                return elapsedTime >= rule.NextSpawnTime;
        }
    }

    #endregion

    #region Trigger State
    /// <summary>
    /// Updates trigger bookkeeping after a rule spawns.
    /// /params rule Mutable rule state.
    /// /params bossRuntime Boss runtime state.
    /// /params elapsedTime Current world elapsed time. Damage-trigger cooldowns are resolved from boss lifetime instead.
    /// /returns None.
    /// </summary>
    public static void MarkRuleTriggered(ref EnemyBossMinionSpawnElement rule,
                                         in EnemyRuntimeState bossRuntime,
                                         float elapsedTime)
    {
        rule.NextSpawnTime = ResolveNextSpawnTime(in rule, in bossRuntime, elapsedTime);
        rule.LastObservedDamageLifetimeSeconds = bossRuntime.LastDamageLifetimeSeconds;

        if (rule.Trigger == EnemyBossMinionSpawnTrigger.HealthBelowPercent)
            rule.Triggered = 1;
    }

    /// <summary>
    /// Resolves the first allowed spawn time for one freshly initialized rule.
    /// /params rule Rule being initialized.
    /// /params bossRuntime Boss runtime state used by damage-trigger rules.
    /// /params elapsedTime Current world elapsed time.
    /// /returns Initial spawn-ready timestamp.
    /// </summary>
    public static float ResolveInitialNextSpawnTime(in EnemyBossMinionSpawnElement rule,
                                                    in EnemyRuntimeState bossRuntime,
                                                    float elapsedTime)
    {
        switch (rule.Trigger)
        {
            case EnemyBossMinionSpawnTrigger.Interval:
                return elapsedTime + math.max(0.01f, rule.IntervalSeconds);

            case EnemyBossMinionSpawnTrigger.BossDamaged:
                return math.max(0f, bossRuntime.LifetimeSeconds);

            default:
                return elapsedTime;
        }
    }

    /// <summary>
    /// Resolves the next allowed spawn time after one trigger activation.
    /// /params rule Rule that just spawned minions.
    /// /params bossRuntime Boss runtime state used by damage-trigger rules.
    /// /params elapsedTime Current world elapsed time.
    /// /returns Next spawn-ready timestamp.
    /// </summary>
    private static float ResolveNextSpawnTime(in EnemyBossMinionSpawnElement rule,
                                              in EnemyRuntimeState bossRuntime,
                                              float elapsedTime)
    {
        switch (rule.Trigger)
        {
            case EnemyBossMinionSpawnTrigger.BossDamaged:
                return math.max(0f, bossRuntime.LifetimeSeconds) + math.max(0f, rule.BossHitCooldownSeconds);

            default:
                return elapsedTime + math.max(0.01f, rule.IntervalSeconds);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Evaluates the boss-damaged trigger and consumes hits that happen while the rule is blocked by cooldown.
    /// /params rule Mutable minion rule runtime state.
    /// /params bossRuntime Boss runtime state carrying hit timestamps.
    /// /returns True when an unobserved hit is allowed to spawn immediately.
    /// </summary>
    private static bool ShouldTriggerBossDamagedRule(ref EnemyBossMinionSpawnElement rule,
                                                     in EnemyRuntimeState bossRuntime)
    {
        if (!TryResolveUnobservedBossDamage(in rule, in bossRuntime, out float damageLifetimeSeconds))
            return false;

        if (damageLifetimeSeconds >= rule.NextSpawnTime &&
            math.max(0f, bossRuntime.LifetimeSeconds) >= rule.NextSpawnTime)
            return true;

        rule.LastObservedDamageLifetimeSeconds = damageLifetimeSeconds;
        return false;
    }

    /// <summary>
    /// Evaluates the one-shot health threshold trigger for rules that spawn below a boss health percentage.
    /// /params rule Minion rule runtime state.
    /// /params bossHealth Boss health state.
    /// /returns True when the boss crossed the configured health threshold and the rule has not fired yet.
    /// </summary>
    private static bool ShouldTriggerHealthBelowPercentRule(in EnemyBossMinionSpawnElement rule,
                                                            in EnemyHealth bossHealth)
    {
        if (rule.Triggered != 0)
            return false;

        if (bossHealth.Max <= 0f)
            return false;

        return math.saturate(bossHealth.Current / bossHealth.Max) <= math.saturate(rule.HealthThresholdPercent);
    }

    /// <summary>
    /// Consumes a boss-damaged trigger when another gate prevents the rule from spawning this frame.
    /// /params rule Mutable minion rule runtime state.
    /// /params bossRuntime Boss runtime state carrying hit timestamps.
    /// /returns None.
    /// </summary>
    private static void ConsumeBlockedBossDamageTrigger(ref EnemyBossMinionSpawnElement rule, in EnemyRuntimeState bossRuntime)
    {
        if (rule.Trigger != EnemyBossMinionSpawnTrigger.BossDamaged)
            return;

        if (!TryResolveUnobservedBossDamage(in rule, in bossRuntime, out float damageLifetimeSeconds))
            return;

        rule.LastObservedDamageLifetimeSeconds = damageLifetimeSeconds;
    }

    /// <summary>
    /// Resolves whether the boss has taken damage that this rule has not observed yet.
    /// /params rule Minion rule runtime state.
    /// /params bossRuntime Boss runtime state carrying hit timestamps.
    /// /params damageLifetimeSeconds Output unobserved damage timestamp.
    /// /returns True when the latest boss hit is newer than the rule observation cursor.
    /// </summary>
    private static bool TryResolveUnobservedBossDamage(in EnemyBossMinionSpawnElement rule,
                                                       in EnemyRuntimeState bossRuntime,
                                                       out float damageLifetimeSeconds)
    {
        damageLifetimeSeconds = bossRuntime.LastDamageLifetimeSeconds;

        if (bossRuntime.HasTakenDamage == 0)
            return false;

        return damageLifetimeSeconds > rule.LastObservedDamageLifetimeSeconds;
    }
    #endregion

    #endregion
}
