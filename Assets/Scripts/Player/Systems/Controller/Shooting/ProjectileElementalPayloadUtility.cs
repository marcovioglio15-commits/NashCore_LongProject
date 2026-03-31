using Unity.Mathematics;

/// <summary>
/// Provides allocation-free helpers to build, merge and inspect multi-element projectile payloads.
/// /params none.
/// /returns none.
/// </summary>
public static class ProjectileElementalPayloadUtility
{
    #region Constants
    public const int MaximumEntryCount = 4;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Reports whether a projectile payload contains at least one valid entry.
    /// /params payload Payload to inspect.
    /// /returns True when at least one entry is present.
    /// </summary>
    public static bool HasAnyPayload(in ProjectileElementalPayload payload)
    {
        return payload.Entries.Length > 0;
    }

    /// <summary>
    /// Builds a payload containing exactly one elemental entry when the authored stack count is valid.
    /// /params effect Effect definition captured at projectile creation.
    /// /params stacksPerHit Stacks applied by the entry on each valid hit.
    /// /returns Payload containing the requested entry, or an empty payload when stacks are not valid.
    /// </summary>
    public static ProjectileElementalPayload BuildSingle(in ElementalEffectConfig effect, float stacksPerHit)
    {
        ProjectileElementalPayload payload = default;
        TryAddOrMerge(ref payload, in effect, stacksPerHit);
        return payload;
    }

    /// <summary>
    /// Appends or merges one elemental entry into the payload, combining duplicate element types by stack count.
    /// /params payload Mutable payload to extend.
    /// /params effect Effect definition captured at projectile creation.
    /// /params stacksPerHit Stacks applied by the entry on each valid hit.
    /// /returns True when a new or merged entry was written into the payload.
    /// </summary>
    public static bool TryAddOrMerge(ref ProjectileElementalPayload payload,
                                     in ElementalEffectConfig effect,
                                     float stacksPerHit)
    {
        float sanitizedStacksPerHit = math.max(0f, stacksPerHit);

        if (sanitizedStacksPerHit <= 0f)
            return false;

        for (int index = 0; index < payload.Entries.Length; index++)
        {
            ProjectileElementalPayloadEntry existingEntry = payload.Entries[index];

            if (existingEntry.Effect.ElementType != effect.ElementType)
                continue;

            existingEntry.StacksPerHit += sanitizedStacksPerHit;
            payload.Entries[index] = existingEntry;
            return true;
        }

        if (payload.Entries.Length >= MaximumEntryCount)
            return false;

        payload.Entries.Add(new ProjectileElementalPayloadEntry
        {
            Effect = effect,
            StacksPerHit = sanitizedStacksPerHit
        });
        return true;
    }

    /// <summary>
    /// Merges all entries from another payload into the destination while preserving first-writer effect settings on duplicates.
    /// /params destination Payload that receives the merged entries.
    /// /params source Source payload whose entries should be appended.
    /// /returns void.
    /// </summary>
    public static void MergePayload(ref ProjectileElementalPayload destination, in ProjectileElementalPayload source)
    {
        for (int index = 0; index < source.Entries.Length; index++)
        {
            ProjectileElementalPayloadEntry sourceEntry = source.Entries[index];
            TryAddOrMerge(ref destination, in sourceEntry.Effect, sourceEntry.StacksPerHit);
        }
    }
    #endregion

    #endregion
}
