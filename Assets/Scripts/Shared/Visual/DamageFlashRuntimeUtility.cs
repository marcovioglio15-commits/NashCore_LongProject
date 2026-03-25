using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Centralizes shared runtime math and state updates used by player and enemy hit-flash feedback.
/// </summary>
public static class DamageFlashRuntimeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Converts a Unity color into a linear float4 suitable for ECS material and presentation paths.
    /// /params color: Source color authored in inspector space.
    /// /returns Linear-space float4 color.
    /// </summary>
    public static float4 ToLinearFloat4(Color color)
    {
        Color linearColor = color.linear;
        return new float4(linearColor.r, linearColor.g, linearColor.b, linearColor.a);
    }

    /// <summary>
    /// Converts a float4 color produced by ECS runtime data into a Unity Color for managed renderer APIs.
    /// /params color: Linear-space float4 color.
    /// /returns Unity Color with matching component values.
    /// </summary>
    public static Color ToManagedColor(float4 color)
    {
        return new Color(color.x, color.y, color.z, color.w);
    }

    /// <summary>
    /// Refreshes the flash timer of one entity when it receives valid damage.
    /// /params entityManager: Entity manager used to access flash components.
    /// /params entity: Damaged entity that should restart the flash feedback.
    /// /returns None.
    /// </summary>
    public static void Trigger(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.Exists(entity))
            return;

        if (!entityManager.HasComponent<DamageFlashConfig>(entity))
            return;

        if (!entityManager.HasComponent<DamageFlashState>(entity))
            return;

        DamageFlashConfig config = entityManager.GetComponentData<DamageFlashConfig>(entity);

        if (config.DurationSeconds <= 0f)
            return;

        DamageFlashState state = entityManager.GetComponentData<DamageFlashState>(entity);
        state.RemainingSeconds = math.max(0f, config.DurationSeconds);
        entityManager.SetComponentData(entity, state);
    }

    /// <summary>
    /// Advances one flash state and returns the blend value that should be rendered this frame.
    /// /params state: Mutable runtime flash state to advance.
    /// /params config: Immutable flash config used to clamp duration and intensity.
    /// /params deltaTime: Current frame delta time in seconds.
    /// /returns Blend factor in the [0..1] range to render this frame.
    /// </summary>
    public static float Advance(ref DamageFlashState state,
                                in DamageFlashConfig config,
                                float deltaTime)
    {
        float durationSeconds = math.max(0f, config.DurationSeconds);
        float maximumBlend = math.saturate(config.MaximumBlend);

        if (durationSeconds <= 0f || maximumBlend <= 0f)
        {
            state.RemainingSeconds = 0f;
            return 0f;
        }

        float currentRemainingSeconds = math.max(0f, state.RemainingSeconds);
        float normalized = math.saturate(currentRemainingSeconds / durationSeconds);
        float targetBlend = normalized * maximumBlend;
        state.RemainingSeconds = math.max(0f, currentRemainingSeconds - math.max(0f, deltaTime));
        return targetBlend;
    }

    /// <summary>
    /// Resolves the per-instance material color used by standard URP materials during a hit flash.
    /// /params baseColor: Original material color stored for restoration.
    /// /params config: Immutable flash config providing the target flash tint.
    /// /params blend: Current flash blend in the [0..1] range.
    /// /returns Blended color to write into per-instance material overrides.
    /// </summary>
    public static float4 ResolveBaseColor(in DamageFlashBaseColor baseColor,
                                          in DamageFlashConfig config,
                                          float blend)
    {
        return math.lerp(baseColor.Value, config.FlashColor, math.saturate(blend));
    }
    #endregion

    #endregion
}
