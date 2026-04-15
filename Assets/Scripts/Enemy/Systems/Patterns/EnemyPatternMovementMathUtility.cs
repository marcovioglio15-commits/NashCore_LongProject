using Unity.Mathematics;

/// <summary>
/// Centralizes math helpers and tuning scalars used by enemy pattern movement runtime.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemyPatternMovementMathUtility
{
    #region Constants
    private const float DirectionEpsilon = 1e-6f;
    private const float PriorityYieldMaxSpeedBoost = 0.65f;
    private const float PriorityYieldMaxAccelerationBoost = 1.9f;
    private const float PriorityYieldGapSpeedScaleMin = 0.62f;
    private const float PriorityYieldGapSpeedScaleMax = 1.4f;
    private const float PriorityYieldGapAccelerationScaleMin = 0.72f;
    private const float PriorityYieldGapAccelerationScaleMax = 1.58f;
    private const float ShortRangePriorityAccelerationMultiplier = 2.25f;
    private const float ShortRangeTakeoverAccelerationMultiplier = 3.4f;
    private const float MinimumSteeringAggressiveness = 0f;
    private const float MaximumSteeringAggressiveness = 2.5f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves whether current movement pattern ignores steering and priority interactions.
    /// /params patternConfig Current compiled pattern configuration.
    /// /returns True when the active movement explicitly requests steering and priority bypass.
    /// </summary>
    public static bool ShouldIgnoreSteeringAndPriority(in EnemyPatternConfig patternConfig)
    {
        if (patternConfig.MovementKind == EnemyCompiledMovementPatternKind.ShortRangeDash)
            return true;

        if (patternConfig.MovementKind != EnemyCompiledMovementPatternKind.WandererDvd)
            return false;

        return patternConfig.DvdIgnoreSteeringAndPriority != 0;
    }

    /// <summary>
    /// Blends clearance with the current desired velocity while preserving stable forward speed.
    /// /params baseVelocity Current desired planar velocity before clearance.
    /// /params clearanceVelocity Planar clearance contribution.
    /// /params clearanceBlend Clearance blend scalar.
    /// /params minimumForwardSpeedRatio Minimum retained forward speed ratio in [0..1].
    /// /returns Blended desired velocity.
    /// </summary>
    public static float3 ComposeDesiredVelocityWithClearance(float3 baseVelocity,
                                                            float3 clearanceVelocity,
                                                            float clearanceBlend,
                                                            float minimumForwardSpeedRatio)
    {
        float blend = math.max(0f, clearanceBlend);

        if (blend <= 0f)
            return baseVelocity;

        // Blend the lateral avoidance while keeping enough forward momentum to avoid stalls.
        float3 blendedClearance = clearanceVelocity * blend;
        float baseSpeed = math.length(baseVelocity);

        if (baseSpeed <= DirectionEpsilon)
            return baseVelocity + blendedClearance;

        float3 forwardDirection = baseVelocity / math.max(baseSpeed, DirectionEpsilon);
        float forwardDelta = math.dot(blendedClearance, forwardDirection);
        float minimumForwardSpeed = baseSpeed * math.clamp(minimumForwardSpeedRatio, 0f, 1f);
        float maximumForwardSpeed = baseSpeed * 1.15f;
        float forwardSpeed = math.clamp(baseSpeed + forwardDelta, minimumForwardSpeed, maximumForwardSpeed);
        float3 lateralClearance = blendedClearance - forwardDirection * forwardDelta;
        return forwardDirection * forwardSpeed + lateralClearance;
    }

    /// <summary>
    /// Resolves per-frame velocity change rate using acceleration for speed-up and deceleration for slow-down.
    /// /params currentVelocity Current planar velocity.
    /// /params desiredVelocity Target planar velocity.
    /// /params acceleration Configured acceleration.
    /// /params deceleration Configured deceleration.
    /// /returns Velocity delta rate in units per second.
    /// </summary>
    public static float ResolveVelocityChangeRate(float3 currentVelocity,
                                                  float3 desiredVelocity,
                                                  float acceleration,
                                                  float deceleration)
    {
        float currentSpeed = math.length(currentVelocity);
        float desiredSpeed = math.length(desiredVelocity);

        if (desiredSpeed + DirectionEpsilon >= currentSpeed)
            return math.max(0f, acceleration);

        if (deceleration > 0f)
            return deceleration;

        return math.max(0f, acceleration);
    }

    /// <summary>
    /// Resolves one steering aggressiveness value with safe defaults and clamps.
    /// /params rawAggressiveness Serialized aggressiveness value.
    /// /returns Resolved aggressiveness value ready for runtime use.
    /// </summary>
    public static float ResolveSteeringAggressiveness(float rawAggressiveness)
    {
        if (rawAggressiveness < 0f)
            return MinimumSteeringAggressiveness;

        return math.clamp(rawAggressiveness, MinimumSteeringAggressiveness, MaximumSteeringAggressiveness);
    }

    /// <summary>
    /// Maps steering aggressiveness to a configurable scalar range.
    /// /params aggressiveness Resolved aggressiveness value.
    /// /params minimumScale Output scale at minimum aggressiveness.
    /// /params maximumScale Output scale at maximum aggressiveness.
    /// /returns Interpolated scalar in the requested range.
    /// </summary>
    public static float ResolveAggressivenessScale(float aggressiveness, float minimumScale, float maximumScale)
    {
        float normalizedAggressiveness = math.saturate((aggressiveness - MinimumSteeringAggressiveness) /
                                                       math.max(0.0001f, MaximumSteeringAggressiveness - MinimumSteeringAggressiveness));
        return math.lerp(minimumScale, maximumScale, normalizedAggressiveness);
    }

    /// <summary>
    /// Resolves temporary max-speed boost applied while yielding to higher-priority neighbors.
    /// /params yieldUrgency Yield urgency in [0..1].
    /// /params priorityGapNormalized Normalized priority-tier gap in [0..1].
    /// /params aggressiveness Resolved steering aggressiveness.
    /// /returns Additional speed ratio in [0..+].
    /// </summary>
    public static float ResolvePriorityYieldSpeedBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.85f, 1.22f);
        float gapScale = math.lerp(PriorityYieldGapSpeedScaleMin,
                                   PriorityYieldGapSpeedScaleMax,
                                   math.saturate(priorityGapNormalized));
        return normalizedUrgency * PriorityYieldMaxSpeedBoost * aggressivenessScale * gapScale;
    }

    /// <summary>
    /// Resolves temporary acceleration boost applied while yielding to higher-priority neighbors.
    /// /params yieldUrgency Yield urgency in [0..1].
    /// /params priorityGapNormalized Normalized priority-tier gap in [0..1].
    /// /params aggressiveness Resolved steering aggressiveness.
    /// /returns Additional acceleration ratio in [0..+].
    /// </summary>
    public static float ResolvePriorityYieldAccelerationBoost(float yieldUrgency, float priorityGapNormalized, float aggressiveness)
    {
        float normalizedUrgency = math.saturate(yieldUrgency);

        if (normalizedUrgency <= 0f)
            return 0f;

        float aggressivenessScale = ResolveAggressivenessScale(aggressiveness, 0.9f, 1.3f);
        float gapScale = math.lerp(PriorityYieldGapAccelerationScaleMin,
                                   PriorityYieldGapAccelerationScaleMax,
                                   math.saturate(priorityGapNormalized));
        return normalizedUrgency * PriorityYieldMaxAccelerationBoost * aggressivenessScale * gapScale;
    }

    /// <summary>
    /// Resolves the acceleration multiplier used when a short-range interaction is currently driving movement.
    /// /params shortRangeTakeoverThisFrame True when the short-range interaction took over on the current frame.
    /// /returns Acceleration multiplier applied to the pattern movement update.
    /// </summary>
    public static float ResolveShortRangePriorityAccelerationMultiplier(bool shortRangeTakeoverThisFrame)
    {
        if (shortRangeTakeoverThisFrame)
            return ShortRangeTakeoverAccelerationMultiplier;

        return ShortRangePriorityAccelerationMultiplier;
    }
    #endregion

    #endregion
}
