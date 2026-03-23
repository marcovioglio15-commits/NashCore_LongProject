using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Computes stable hashes for runtime scalable-stat buffers so expensive scaling sync can early-out.
/// </summary>
internal static class PlayerScalableStatHashUtility
{
    #region Constants
    private const uint FnvOffsetBasis = 2166136261u;
    private const uint FnvPrime = 16777619u;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Computes one FNV-1a hash from all current scalable-stat names and values.
    /// /params scalableStats: Runtime scalable-stat buffer to hash.
    /// /returns Stable hash representing the current variable context.
    /// </summary>
    public static uint ComputeHash(DynamicBuffer<PlayerScalableStatElement> scalableStats)
    {
        uint rollingHash = FnvOffsetBasis;

        if (!scalableStats.IsCreated)
            return rollingHash;

        for (int statIndex = 0; statIndex < scalableStats.Length; statIndex++)
        {
            PlayerScalableStatElement scalableStat = scalableStats[statIndex];

            if (scalableStat.Name.Length == 0)
                continue;

            rollingHash = (rollingHash ^ (uint)scalableStat.Name.GetHashCode()) * FnvPrime;
            rollingHash = (rollingHash ^ math.asuint(scalableStat.Value)) * FnvPrime;
        }

        rollingHash = (rollingHash ^ (uint)scalableStats.Length) * FnvPrime;
        return rollingHash;
    }
    #endregion

    #endregion
}
