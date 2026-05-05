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
        return GetEntryCount(in payload) > 0;
    }

    /// <summary>
    /// Returns the sanitized number of elemental entries stored in a compact projectile payload.
    /// /params payload Payload to inspect.
    /// /returns Entry count clamped to the supported inline capacity.
    /// </summary>
    public static int GetEntryCount(in ProjectileElementalPayload payload)
    {
        return math.min(payload.EntryCount, MaximumEntryCount);
    }

    /// <summary>
    /// Reads one compact payload entry.
    /// /params payload Payload to read.
    /// /params index Entry index in the compact payload.
    /// /returns The stored entry, or default when the index is outside the active range.
    /// </summary>
    public static ProjectileElementalPayloadEntry GetEntry(in ProjectileElementalPayload payload, int index)
    {
        if (index < 0 || index >= GetEntryCount(in payload))
            return default;

        switch (index)
        {
            case 0:
                return payload.Entry0;
            case 1:
                return payload.Entry1;
            case 2:
                return payload.Entry2;
            case 3:
                return payload.Entry3;
            default:
                return default;
        }
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

        int entryCount = GetEntryCount(in payload);

        for (int index = 0; index < entryCount; index++)
        {
            ProjectileElementalPayloadEntry existingEntry = GetEntry(in payload, index);

            if (existingEntry.ElementTypeId != (byte)effect.ElementType)
                continue;

            existingEntry.StacksPerHit += sanitizedStacksPerHit;
            SetEntry(ref payload, index, existingEntry);
            return true;
        }

        if (entryCount >= MaximumEntryCount)
            return false;

        SetEntry(ref payload,
                 entryCount,
                 new ProjectileElementalPayloadEntry
        {
            Effect = effect,
            StacksPerHit = sanitizedStacksPerHit
        });

        payload.EntryCount = (byte)(entryCount + 1);
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
        int sourceEntryCount = GetEntryCount(in source);

        for (int index = 0; index < sourceEntryCount; index++)
        {
            ProjectileElementalPayloadEntry sourceEntry = GetEntry(in source, index);
            ElementalEffectConfig sourceEffect = sourceEntry.Effect;
            TryAddOrMerge(ref destination, in sourceEffect, sourceEntry.StacksPerHit);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes one compact payload entry without growing beyond the fixed inline capacity.
    /// /params payload Payload to mutate.
    /// /params index Entry index to write.
    /// /params entry Entry data to store.
    /// /returns None.
    /// </summary>
    private static void SetEntry(ref ProjectileElementalPayload payload,
                                 int index,
                                 in ProjectileElementalPayloadEntry entry)
    {
        switch (index)
        {
            case 0:
                payload.Entry0 = entry;
                break;
            case 1:
                payload.Entry1 = entry;
                break;
            case 2:
                payload.Entry2 = entry;
                break;
            case 3:
                payload.Entry3 = entry;
                break;
        }
    }
    #endregion

    #endregion
}
