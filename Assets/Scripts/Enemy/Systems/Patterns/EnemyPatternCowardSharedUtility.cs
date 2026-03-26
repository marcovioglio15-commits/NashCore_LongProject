using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Centralizes deterministic sampling and direction helpers shared by Coward movement.
/// /params none.
/// /returns none.
/// </summary>
public static class EnemyPatternCowardSharedUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the preferred retreat direction opposite to the player, with a deterministic fallback when both overlap.
    /// /params enemyEntity Current enemy entity.
    /// /params enemyPosition Current enemy position.
    /// /params playerPosition Current player position.
    /// /params elapsedTime Elapsed world time.
    /// /returns Normalized planar retreat direction.
    /// </summary>
    public static float3 ResolveRetreatDirection(Entity enemyEntity,
                                                 float3 enemyPosition,
                                                 float3 playerPosition,
                                                 float elapsedTime)
    {
        float3 retreatDirection = enemyPosition - playerPosition;
        retreatDirection.y = 0f;

        if (math.lengthsq(retreatDirection) > DirectionEpsilon)
            return math.normalize(retreatDirection);

        return ResolveDeterministicPlanarDirection(enemyEntity, elapsedTime, 5);
    }

    /// <summary>
    /// Resolves a deterministic planar direction from entity identity and an optional time seed.
    /// /params enemyEntity Current enemy entity.
    /// /params elapsedTime Elapsed world time.
    /// /params extraSeed Extra hash salt used to decorrelate callers.
    /// /returns Normalized deterministic planar direction.
    /// </summary>
    public static float3 ResolveDeterministicPlanarDirection(Entity enemyEntity, float elapsedTime, int extraSeed)
    {
        uint angleSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, extraSeed, (int)(elapsedTime * 19f)));
        float angleRadians = EnemyPatternWandererMovementUtility.ResolveHash01(angleSeed) * math.PI * 2f;
        return new float3(math.sin(angleRadians), 0f, math.cos(angleRadians));
    }

    /// <summary>
    /// Resolves one stable orbit sign per entity so nearby Cowards separate into opposite circulation directions.
    /// /params enemyEntity Current enemy entity.
    /// /returns Signed scalar equal to -1 or +1.
    /// </summary>
    public static float ResolveOrbitSign(Entity enemyEntity)
    {
        uint orbitHash = math.hash(new int2(enemyEntity.Index, enemyEntity.Version));

        if ((orbitHash & 1u) == 0u)
            return -1f;

        return 1f;
    }

    /// <summary>
    /// Resolves the number of angular samples for the current picker mode.
    /// /params patternConfig Compiled pattern config.
    /// /params useInfiniteDirectionSampling Whether infinite angular sampling is enabled.
    /// /params minimumSampleCount Minimum sample count enforced by the caller.
    /// /returns Effective sample count.
    /// </summary>
    public static int ResolveSampleCount(in EnemyPatternConfig patternConfig,
                                         bool useInfiniteDirectionSampling,
                                         int minimumSampleCount)
    {
        int sampleCount = math.max(math.max(1, minimumSampleCount), patternConfig.BasicCandidateSampleCount);

        if (!useInfiniteDirectionSampling)
            return sampleCount;

        float directionStepDegrees = math.clamp(patternConfig.BasicInfiniteDirectionStepDegrees, 0.5f, 90f);
        return math.max(sampleCount, (int)math.ceil(360f / directionStepDegrees));
    }

    /// <summary>
    /// Resolves the phase angle used when a full 360° sweep is sampled.
    /// /params enemyEntity Current enemy entity.
    /// /params elapsedTime Elapsed world time.
    /// /params useInfiniteDirectionSampling Whether infinite angular sampling is enabled.
    /// /params seedOffset Extra hash salt used by the caller.
    /// /returns Starting phase angle in radians.
    /// </summary>
    public static float ResolvePhaseAngleRadians(Entity enemyEntity,
                                                 float elapsedTime,
                                                 bool useInfiniteDirectionSampling,
                                                 int seedOffset)
    {
        if (!useInfiniteDirectionSampling)
            return 0f;

        uint phaseSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, (int)(elapsedTime * 11f), seedOffset));
        return EnemyPatternWandererMovementUtility.ResolveHash01(phaseSeed) * math.PI * 2f;
    }

    /// <summary>
    /// Resolves one candidate angle either from the infinite sweep or from per-sample hash noise.
    /// /params enemyEntity Current enemy entity.
    /// /params sampleIndex Current sample index.
    /// /params elapsedTime Elapsed world time.
    /// /params useInfiniteDirectionSampling Whether infinite angular sampling is enabled.
    /// /params phaseAngleRadians Sweep phase angle in radians.
    /// /params stepRadians Angular step used by infinite sampling.
    /// /returns Candidate angle in radians.
    /// </summary>
    public static float ResolveSampleAngleRadians(Entity enemyEntity,
                                                  int sampleIndex,
                                                  float elapsedTime,
                                                  bool useInfiniteDirectionSampling,
                                                  float phaseAngleRadians,
                                                  float stepRadians)
    {
        if (useInfiniteDirectionSampling)
            return phaseAngleRadians + stepRadians * sampleIndex;

        uint directionSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, sampleIndex, (int)(elapsedTime * 17f)));
        return EnemyPatternWandererMovementUtility.ResolveHash01(directionSeed) * math.PI * 2f;
    }

    /// <summary>
    /// Resolves one sample distance inside the requested min/max travel band.
    /// /params enemyEntity Current enemy entity.
    /// /params sampleIndex Current sample index.
    /// /params elapsedTime Elapsed world time.
    /// /params minimumDistance Minimum allowed distance.
    /// /params maximumDistance Maximum allowed distance.
    /// /params searchRadius Search radius cap.
    /// /returns Candidate travel distance.
    /// </summary>
    public static float ResolveSampleDistance(Entity enemyEntity,
                                              int sampleIndex,
                                              float elapsedTime,
                                              float minimumDistance,
                                              float maximumDistance,
                                              float searchRadius)
    {
        uint distanceSeed = math.hash(new int4(enemyEntity.Index, enemyEntity.Version, sampleIndex, (int)(elapsedTime * 31f)));
        float distance01 = EnemyPatternWandererMovementUtility.ResolveHash01(distanceSeed);
        float targetDistance = math.lerp(minimumDistance, maximumDistance, distance01);
        return math.min(targetDistance, searchRadius);
    }

    /// <summary>
    /// Resolves a small stable decision cooldown used to stagger Coward repath requests across frames.
    /// /params enemyEntity Current enemy entity.
    /// /params minimumSeconds Minimum cooldown in seconds.
    /// /params maximumSeconds Maximum cooldown in seconds.
    /// /returns Stable cooldown value for this entity.
    /// </summary>
    public static float ResolveDecisionCooldown(Entity enemyEntity, float minimumSeconds, float maximumSeconds)
    {
        float clampedMinimumSeconds = math.max(0f, minimumSeconds);
        float clampedMaximumSeconds = math.max(clampedMinimumSeconds, maximumSeconds);
        uint cooldownSeed = math.hash(new int2(enemyEntity.Index ^ 1877, enemyEntity.Version ^ 6151));
        float cooldown01 = EnemyPatternWandererMovementUtility.ResolveHash01(cooldownSeed);
        return math.lerp(clampedMinimumSeconds, clampedMaximumSeconds, cooldown01);
    }

    /// <summary>
    /// Resolves a stable tangent direction along a wall normal so Cowards slide away from corners instead of pinning in place.
    /// /params enemyEntity Current enemy entity.
    /// /params wallNormal Surface normal of the nearby wall.
    /// /returns Normalized planar tangent direction.
    /// </summary>
    public static float3 ResolveWallTangentDirection(Entity enemyEntity, float3 wallNormal)
    {
        float3 planarWallNormal = math.normalizesafe(new float3(wallNormal.x, 0f, wallNormal.z), float3.zero);

        if (math.lengthsq(planarWallNormal) <= DirectionEpsilon)
            return ResolveDeterministicPlanarDirection(enemyEntity, 0f, 31);

        float3 tangentDirection = new float3(-planarWallNormal.z, 0f, planarWallNormal.x);

        if (ResolveOrbitSign(enemyEntity) < 0f)
            tangentDirection = -tangentDirection;

        return math.normalizesafe(tangentDirection, ResolveDeterministicPlanarDirection(enemyEntity, 0f, 29));
    }
    #endregion

    #endregion
}
