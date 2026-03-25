using Unity.Mathematics;

/// <summary>
/// Centralizes fallback-value sanitization for enemy authoring data.
/// /returns None.
/// </summary>
public static class EnemyAuthoringFallbackValidationUtility
{
    #region Methods

    #region Validation
    /// <summary>
    /// Sanitizes fallback values used only when no preset source is assigned on EnemyAuthoring.
    /// Called by EnemyAuthoring.OnValidate to keep hidden fallback data in a safe range.
    /// /params moveSpeed: Fallback movement speed value.
    /// /params maxSpeed: Fallback maximum movement speed value.
    /// /params acceleration: Fallback acceleration value.
    /// /params deceleration: Fallback deceleration value.
    /// /params rotationSpeedDegreesPerSecond: Fallback self-rotation speed.
    /// /params minimumWallDistance: Fallback extra distance kept from static walls.
    /// /params separationRadius: Fallback neighbor separation radius.
    /// /params separationWeight: Fallback neighbor separation weight.
    /// /params bodyRadius: Fallback body radius.
    /// /params contactRadius: Fallback contact damage radius.
    /// /params contactAmountPerTick: Fallback contact damage amount.
    /// /params contactTickInterval: Fallback contact damage interval.
    /// /params areaRadius: Fallback area damage radius.
    /// /params areaAmountPerTickPercent: Fallback area damage percentage amount.
    /// /params areaTickInterval: Fallback area damage interval.
    /// /params maxHealth: Fallback maximum health.
    /// /params maxShield: Fallback maximum shield.
    /// /params priorityTier: Fallback priority tier.
    /// /params steeringAggressiveness: Fallback steering aggressiveness.
    /// /returns None.
    /// </summary>
    public static void ValidateFallbackValues(ref float moveSpeed,
                                              ref float maxSpeed,
                                              ref float acceleration,
                                              ref float deceleration,
                                              ref float rotationSpeedDegreesPerSecond,
                                              ref float minimumWallDistance,
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
                                              ref int priorityTier,
                                              ref float steeringAggressiveness)
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

        if (minimumWallDistance < 0f)
            minimumWallDistance = 0f;

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

        priorityTier = math.clamp(priorityTier, -128, 128);

        if (float.IsNaN(steeringAggressiveness) || float.IsInfinity(steeringAggressiveness))
            steeringAggressiveness = 1f;
        else
            steeringAggressiveness = math.clamp(steeringAggressiveness, 0f, 2.5f);
    }
    #endregion

    #endregion
}
