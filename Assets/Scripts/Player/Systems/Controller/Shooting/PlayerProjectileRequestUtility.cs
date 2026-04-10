using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Centralizes projectile-request math shared by base shooting and active power-up shooting paths.
/// /params None.
/// /returns None.
/// </summary>
public static class PlayerProjectileRequestUtility
{
    #region Constants
    private const float DirectionLengthEpsilon = 1e-6f;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the planar shoot direction from look state and falls back to the current transform forward.
    /// /params lookState Current player look state.
    /// /params localTransform Current player transform used for fallback orientation.
    /// /returns The normalized planar shoot direction.
    /// </summary>
    public static float3 ResolveShootDirection(in PlayerLookState lookState,
                                               in LocalTransform localTransform)
    {
        float3 lookDirection = lookState.DesiredDirection;
        lookDirection.y = 0f;

        if (math.lengthsq(lookDirection) > DirectionLengthEpsilon)
            return math.normalizesafe(lookDirection, new float3(0f, 0f, 1f));

        float3 fallbackDirection = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.Rotation), new float3(0f, 0f, 1f));
        return math.normalizesafe(fallbackDirection, new float3(0f, 0f, 1f));
    }

    /// <summary>
    /// Resolves the spawn position used by projectile-like emissions from the current muzzle configuration.
    /// /params playerEntity Current player entity owning the emission.
    /// /params localTransform Current player transform.
    /// /params runtimeShootingConfig Runtime shooting config used to resolve the shoot offset.
    /// /params muzzleLookup Read-only muzzle anchor lookup.
    /// /params transformLookup Read-only transform lookup.
    /// /params localToWorldLookup Read-only LocalToWorld lookup.
    /// /returns The resolved world-space spawn position.
    /// </summary>
    public static float3 ResolveShootSpawnPosition(Entity playerEntity,
                                                   in LocalTransform localTransform,
                                                   in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                   in ComponentLookup<ShooterMuzzleAnchor> muzzleLookup,
                                                   in ComponentLookup<LocalTransform> transformLookup,
                                                   in ComponentLookup<LocalToWorld> localToWorldLookup)
    {
        float3 shootOffset = runtimeShootingConfig.ShootOffset;
        return PlayerShootOriginUtility.ResolveSpawnPosition(playerEntity,
                                                             in localTransform,
                                                             in shootOffset,
                                                             in muzzleLookup,
                                                             in transformLookup,
                                                             in localToWorldLookup);
    }

    /// <summary>
    /// Builds one projectile request template from runtime shooting data plus optional local multipliers and elemental override.
    /// /params runtimeShootingConfig Current runtime shooting config.
    /// /params appliedElementSlots Runtime default elemental-slot buffer.
    /// /params passiveToolsState Current aggregated passive-tool state.
    /// /params sizeMultiplier Local size multiplier applied on top of base and passive values.
    /// /params damageMultiplier Local damage multiplier applied on top of base and passive values.
    /// /params speedMultiplier Local speed multiplier applied on top of base and passive values.
    /// /params rangeMultiplier Local range multiplier applied on top of base and passive values.
    /// /params lifetimeMultiplier Local lifetime multiplier applied on top of base and passive values.
    /// /params hasElementalPayloadOverride True when the override elemental payload should replace the default one.
    /// /params elementalEffectOverride Override elemental effect configuration.
    /// /params elementalStacksPerHitOverride Override elemental stacks per hit.
    /// /returns The resolved request template ready to be emitted as one or more ShootRequest entries.
    /// </summary>
    public static PlayerProjectileRequestTemplate BuildProjectileTemplate(in PlayerRuntimeShootingConfig runtimeShootingConfig,
                                                                          DynamicBuffer<PlayerRuntimeShootingAppliedElementSlot> appliedElementSlots,
                                                                          in PlayerPassiveToolsState passiveToolsState,
                                                                          float sizeMultiplier,
                                                                          float damageMultiplier,
                                                                          float speedMultiplier,
                                                                          float rangeMultiplier,
                                                                          float lifetimeMultiplier,
                                                                          bool hasElementalPayloadOverride,
                                                                          in ElementalEffectConfig elementalEffectOverride,
                                                                          float elementalStacksPerHitOverride)
    {
        ShootingValuesBlob values = runtimeShootingConfig.Values;
        ProjectileElementalPayload resolvedElementalPayloadOverride = default;
        float scale = math.max(0.01f,
                               math.max(0.01f, values.ProjectileSizeMultiplier) *
                               math.max(0.01f, passiveToolsState.ProjectileSizeMultiplier) *
                               math.max(0.01f, sizeMultiplier));
        float damage = math.max(0f, values.Damage * math.max(0f, passiveToolsState.ProjectileDamageMultiplier) * math.max(0f, damageMultiplier));
        float speed = math.max(0f, values.ShootSpeed * math.max(0f, passiveToolsState.ProjectileSpeedMultiplier) * math.max(0f, speedMultiplier));
        float range = ApplyLifetimeMultiplier(values.Range,
                                              math.max(0f, passiveToolsState.ProjectileLifetimeRangeMultiplier) * math.max(0f, rangeMultiplier));
        float lifetime = ApplyLifetimeMultiplier(values.Lifetime,
                                                 math.max(0f, passiveToolsState.ProjectileLifetimeSecondsMultiplier) * math.max(0f, lifetimeMultiplier));

        if (hasElementalPayloadOverride)
        {
            resolvedElementalPayloadOverride = ProjectileElementalPayloadUtility.BuildSingle(in elementalEffectOverride,
                                                                                            math.max(0f, elementalStacksPerHitOverride));
        }
        else
        {
            PlayerProjectileElementUtility.TryBuildDefaultPayload(appliedElementSlots,
                                                                  in values,
                                                                  out resolvedElementalPayloadOverride);
        }

        return new PlayerProjectileRequestTemplate
        {
            Speed = speed,
            Damage = damage,
            ExplosionRadius = math.max(0f, values.ExplosionRadius),
            Range = range,
            Lifetime = lifetime,
            ScaleMultiplier = scale,
            Knockback = values.Knockback,
            InheritPlayerSpeed = runtimeShootingConfig.ProjectilesInheritPlayerSpeed,
            ElementalPayloadOverride = resolvedElementalPayloadOverride
        };
    }

    /// <summary>
    /// Resolves final penetration settings by merging base shooting values with an optional override.
    /// /params baseShootingValues Base controller shooting values.
    /// /params overrideMode Optional override penetration mode.
    /// /params overrideMaxPenetrations Optional override penetration count.
    /// /params resolvedMode Final resolved penetration mode.
    /// /params resolvedMaxPenetrations Final resolved maximum penetration count.
    /// /returns None.
    /// </summary>
    public static void ResolvePenetrationSettings(in ShootingValuesBlob baseShootingValues,
                                                  ProjectilePenetrationMode overrideMode,
                                                  int overrideMaxPenetrations,
                                                  out ProjectilePenetrationMode resolvedMode,
                                                  out int resolvedMaxPenetrations)
    {
        resolvedMode = baseShootingValues.PenetrationMode;
        resolvedMaxPenetrations = math.max(0, baseShootingValues.MaxPenetrations);

        if (overrideMode != ProjectilePenetrationMode.None)
            resolvedMode = (ProjectilePenetrationMode)math.max((int)resolvedMode, (int)overrideMode);

        resolvedMaxPenetrations = math.max(resolvedMaxPenetrations, math.max(0, overrideMaxPenetrations));
    }

    /// <summary>
    /// Emits one single request or one evenly spread burst, depending on the projectile count.
    /// /params shootRequests Mutable ShootRequest buffer receiving the generated entries.
    /// /params projectileCount Number of projectile lanes to emit.
    /// /params coneAngleDegrees Total spread angle in degrees.
    /// /params spawnPosition World-space emission origin.
    /// /params shootDirection Base forward direction.
    /// /params template Resolved template copied into each ShootRequest.
    /// /params penetrationMode Penetration mode assigned to emitted requests.
    /// /params maxPenetrations Maximum penetrations assigned to emitted requests.
    /// /params isSplitChild Flag propagated to emitted requests.
    /// /returns None.
    /// </summary>
    public static void AddSpreadRequests(ref DynamicBuffer<ShootRequest> shootRequests,
                                         int projectileCount,
                                         float coneAngleDegrees,
                                         float3 spawnPosition,
                                         float3 shootDirection,
                                         in PlayerProjectileRequestTemplate template,
                                         ProjectilePenetrationMode penetrationMode,
                                         int maxPenetrations,
                                         byte isSplitChild)
    {
        if (projectileCount <= 1)
        {
            AddShootRequest(ref shootRequests,
                            spawnPosition,
                            shootDirection,
                            in template,
                            penetrationMode,
                            maxPenetrations,
                            isSplitChild);
            return;
        }

        float halfCone = coneAngleDegrees * 0.5f;
        float step = coneAngleDegrees / (projectileCount - 1);

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            float angle = -halfCone + step * projectileIndex;
            quaternion rotationOffset = quaternion.AxisAngle(new float3(0f, 1f, 0f), math.radians(angle));
            float3 spreadDirection = math.rotate(rotationOffset, shootDirection);

            if (math.lengthsq(spreadDirection) <= DirectionLengthEpsilon)
                spreadDirection = shootDirection;

            AddShootRequest(ref shootRequests,
                            spawnPosition,
                            spreadDirection,
                            in template,
                            penetrationMode,
                            maxPenetrations,
                            isSplitChild);
        }
    }

    /// <summary>
    /// Adds one fully initialized ShootRequest entry to the provided buffer.
    /// /params shootRequests Mutable ShootRequest buffer receiving the entry.
    /// /params position World-space emission origin.
    /// /params direction Desired projectile forward direction.
    /// /params template Resolved projectile template.
    /// /params penetrationMode Penetration mode assigned to the entry.
    /// /params maxPenetrations Maximum penetrations assigned to the entry.
    /// /params isSplitChild Flag propagated to the entry.
    /// /returns None.
    /// </summary>
    public static void AddShootRequest(ref DynamicBuffer<ShootRequest> shootRequests,
                                       float3 position,
                                       float3 direction,
                                       in PlayerProjectileRequestTemplate template,
                                       ProjectilePenetrationMode penetrationMode,
                                       int maxPenetrations,
                                       byte isSplitChild)
    {
        shootRequests.Add(new ShootRequest
        {
            Position = position,
            Direction = math.normalizesafe(direction, new float3(0f, 0f, 1f)),
            Speed = math.max(0f, template.Speed),
            ExplosionRadius = math.max(0f, template.ExplosionRadius),
            Range = template.Range,
            Lifetime = template.Lifetime,
            Damage = math.max(0f, template.Damage),
            ProjectileScaleMultiplier = math.max(0.01f, template.ScaleMultiplier),
            PenetrationMode = penetrationMode,
            MaxPenetrations = math.max(0, maxPenetrations),
            KnockbackEnabled = template.Knockback.Enabled,
            KnockbackStrength = math.max(0f, template.Knockback.Strength),
            KnockbackDurationSeconds = math.max(0f, template.Knockback.DurationSeconds),
            KnockbackDirectionMode = template.Knockback.DirectionMode,
            KnockbackStackingMode = template.Knockback.StackingMode,
            InheritPlayerSpeed = template.InheritPlayerSpeed,
            IsSplitChild = isSplitChild,
            ElementalPayloadOverride = template.ElementalPayloadOverride
        });
    }

    /// <summary>
    /// Applies a safe multiplier to one range or lifetime limit while preserving non-positive disabled values.
    /// /params baseLifetimeValue Base lifetime or range value.
    /// /params lifetimeMultiplier Multiplier applied when the base value is positive.
    /// /returns The scaled value, or the untouched disabled base value when the input is non-positive.
    /// </summary>
    public static float ApplyLifetimeMultiplier(float baseLifetimeValue,
                                                float lifetimeMultiplier)
    {
        if (baseLifetimeValue <= 0f)
            return baseLifetimeValue;

        return math.max(0f, baseLifetimeValue * math.max(0f, lifetimeMultiplier));
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores one reusable projectile request payload before emission into ShootRequest buffers.
/// /params None.
/// /returns None.
/// </summary>
public struct PlayerProjectileRequestTemplate
{
    #region Fields
    public float Speed;
    public float Damage;
    public float ExplosionRadius;
    public float Range;
    public float Lifetime;
    public float ScaleMultiplier;
    public ProjectileKnockbackSettingsBlob Knockback;
    public byte InheritPlayerSpeed;
    public ProjectileElementalPayload ElementalPayloadOverride;
    #endregion
}
