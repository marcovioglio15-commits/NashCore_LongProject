using Unity.Mathematics;

/// <summary>
/// Centralizes fallback-value sanitization for enemy authoring data.
/// </summary>
public static class EnemyAuthoringFallbackValidationUtility
{
    #region Methods

    #region Validation
    public static void ValidateFallbackValues(ref float moveSpeed,
                                              ref float maxSpeed,
                                              ref float acceleration,
                                              ref float deceleration,
                                              ref float rotationSpeedDegreesPerSecond,
                                              ref float separationRadius,
                                              ref float separationWeight,
                                              ref float bodyRadius,
                                              ref float contactRadius,
                                              ref float contactAmountPerTick,
                                              ref float contactTickInterval,
                                              ref float areaRadius,
                                              ref float areaAmountPerTickPercent,
                                              ref float areaTickInterval,
                                              ref float maxHealth,
                                              ref float maxShield,
                                              ref EnemyVisualMode visualMode,
                                              ref float visualAnimationSpeed,
                                              ref float gpuAnimationLoopDuration,
                                              ref float maxVisibleDistance,
                                              ref float visibleDistanceHysteresis,
                                              ref int priorityTier,
                                              ref float steeringAggressiveness,
                                              ref float hitVfxLifetimeSeconds,
                                              ref float hitVfxScaleMultiplier)
    {
        if (moveSpeed < 0f)
            moveSpeed = 0f;

        if (maxSpeed < 0f)
            maxSpeed = 0f;

        if (acceleration < 0f)
            acceleration = 0f;

        if (deceleration < 0f)
            deceleration = 0f;

        if (float.IsNaN(rotationSpeedDegreesPerSecond) || float.IsInfinity(rotationSpeedDegreesPerSecond))
            rotationSpeedDegreesPerSecond = 0f;

        if (separationRadius < 0.1f)
            separationRadius = 0.1f;

        if (separationWeight < 0f)
            separationWeight = 0f;

        if (bodyRadius < 0.05f)
            bodyRadius = 0.05f;

        if (contactRadius < 0f)
            contactRadius = 0f;

        if (contactAmountPerTick < 0f)
            contactAmountPerTick = 0f;

        if (contactTickInterval < 0.01f)
            contactTickInterval = 0.01f;

        if (areaRadius < 0f)
            areaRadius = 0f;

        if (areaAmountPerTickPercent < 0f)
            areaAmountPerTickPercent = 0f;

        if (areaTickInterval < 0.01f)
            areaTickInterval = 0.01f;

        if (maxHealth < 1f)
            maxHealth = 1f;

        if (maxShield < 0f)
            maxShield = 0f;

        switch (visualMode)
        {
            case EnemyVisualMode.CompanionAnimator:
            case EnemyVisualMode.GpuBaked:
                break;

            default:
                visualMode = EnemyVisualMode.GpuBaked;
                break;
        }

        if (visualAnimationSpeed < 0f)
            visualAnimationSpeed = 0f;

        if (gpuAnimationLoopDuration < 0.05f)
            gpuAnimationLoopDuration = 0.05f;

        if (maxVisibleDistance < 0f)
            maxVisibleDistance = 0f;

        if (visibleDistanceHysteresis < 0f)
            visibleDistanceHysteresis = 0f;

        priorityTier = math.clamp(priorityTier, -128, 128);

        if (float.IsNaN(steeringAggressiveness) || float.IsInfinity(steeringAggressiveness))
            steeringAggressiveness = 1f;
        else
            steeringAggressiveness = math.clamp(steeringAggressiveness, 0f, 2.5f);

        if (float.IsNaN(hitVfxLifetimeSeconds) || float.IsInfinity(hitVfxLifetimeSeconds) || hitVfxLifetimeSeconds < 0.05f)
            hitVfxLifetimeSeconds = 0.05f;

        if (float.IsNaN(hitVfxScaleMultiplier) || float.IsInfinity(hitVfxScaleMultiplier) || hitVfxScaleMultiplier < 0.01f)
            hitVfxScaleMultiplier = 0.01f;
    }
    #endregion

    #endregion
}
