using Unity.Mathematics;

/// <summary>
/// Provides shared spawn-warning config helpers for spawner bake and runtime paths.
/// /params None.
/// /returns None.
/// </summary>
internal static class EnemySpawnWarningConfigUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the warning config for one baked spawn event.
    /// /params waveEvent Baked spawn event that may carry an override.
    /// /params fallbackConfig Spawner-level fallback warning config.
    /// /returns Event-specific warning config.
    /// </summary>
    public static EnemySpawnWarningConfig ResolveEventWarningConfig(in EnemySpawnerWaveEventElement waveEvent,
                                                                    in EnemySpawnWarningConfig fallbackConfig)
    {
        if (waveEvent.HasSpawnWarningOverride != 0)
            return waveEvent.SpawnWarningOverride;

        return fallbackConfig;
    }

    /// <summary>
    /// Resolves the effective warning lead time used by scheduling and warning presentation.
    /// /params warningConfig Warning config to inspect.
    /// /returns Effective lead time in seconds, or zero when warnings are disabled.
    /// </summary>
    public static float ResolveEffectiveLeadTimeSeconds(in EnemySpawnWarningConfig warningConfig)
    {
        if (warningConfig.Enabled == 0)
            return 0f;

        return math.max(0f, warningConfig.LeadTimeSeconds);
    }
    #endregion

    #endregion
}
