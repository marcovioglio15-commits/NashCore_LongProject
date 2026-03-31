using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the one-time upgrade from legacy single-element shooting authoring to the new stacked multi-element model.
/// /params none.
/// /returns none.
/// </summary>
public static class PlayerControllerElementalShootingMigrationUtility
{
    #region Constants
    public const int DefaultAppliedElementSlotCount = 1;

    private const string LegacyAppliedElementKey = "shootingSettings.values.appliedElement";
    private const string LegacyElementBehaviourPrefix = "shootingSettings.values.elementBulletSettings.";
    private const string AppliedElementsPrefix = "shootingSettings.values.appliedElements.Array.data[";
    private const string ElementBehavioursPrefix = "shootingSettings.values.elementBehaviours.";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the authored applied-element array is initialized while preserving the authored slot count.
    /// /params sourceSlots Authored array that may come from old or malformed serialized data.
    /// /returns Initialized slot array used by the upgraded authoring model.
    /// </summary>
    public static PlayerProjectileAppliedElement[] EnsureAppliedElementSlots(PlayerProjectileAppliedElement[] sourceSlots)
    {
        if (sourceSlots != null)
            return sourceSlots;

        return new PlayerProjectileAppliedElement[DefaultAppliedElementSlotCount];
    }

    /// <summary>
    /// Resizes the authored applied-element array while preserving existing values up to the new slot count.
    /// /params sourceSlots Current authored slot array.
    /// /params targetSlotCount Requested slot count.
    /// /returns Resized authored slot array.
    /// </summary>
    public static PlayerProjectileAppliedElement[] ResizeAppliedElementSlots(PlayerProjectileAppliedElement[] sourceSlots, int targetSlotCount)
    {
        int resolvedTargetSlotCount = Mathf.Max(0, targetSlotCount);
        PlayerProjectileAppliedElement[] resolvedSourceSlots = EnsureAppliedElementSlots(sourceSlots);

        if (resolvedSourceSlots.Length == resolvedTargetSlotCount)
            return resolvedSourceSlots;

        PlayerProjectileAppliedElement[] resizedSlots = new PlayerProjectileAppliedElement[resolvedTargetSlotCount];
        int copyCount = Mathf.Min(resolvedSourceSlots.Length, resolvedTargetSlotCount);

        for (int index = 0; index < copyCount; index++)
            resizedSlots[index] = resolvedSourceSlots[index];

        return resizedSlots;
    }

    /// <summary>
    /// Migrates legacy single-element authoring data into the new stacked slot array and per-element behaviour container.
    /// /params values Shooting values block being validated.
    /// /returns void.
    /// </summary>
    public static void MigrateLegacyElementalPayloadAuthoring(ShootingValues values)
    {
        if (values == null)
            return;

        if (values.ElementalPayloadDataMigrated)
            return;

        PlayerProjectileAppliedElement[] appliedElementSlots = EnsureAppliedElementSlots(values.AppliedElementsMutable);
        values.AppliedElementsMutable = appliedElementSlots;

        if (values.ElementBehavioursMutable == null)
            values.ElementBehavioursMutable = new ElementBulletSettingsByElement();

        values.ElementBehavioursMutable.CopyAllFrom(values.LegacyElementBulletSettings);

        if (!HasAnyAssignedElement(appliedElementSlots) && values.LegacyAppliedElement != PlayerProjectileAppliedElement.None)
        {
            if (appliedElementSlots.Length <= 0)
            {
                appliedElementSlots = ResizeAppliedElementSlots(appliedElementSlots, DefaultAppliedElementSlotCount);
                values.AppliedElementsMutable = appliedElementSlots;
            }

            appliedElementSlots[0] = values.LegacyAppliedElement;
        }

        values.ElementalPayloadDataMigrated = true;
    }

    /// <summary>
    /// Migrates legacy Add Scaling stat keys that targeted the old single-element fields into the new multi-element paths.
    /// /params preset Controller preset whose scaling rules should be upgraded once.
    /// /returns void.
    /// </summary>
    public static void MigrateLegacyScalingRules(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        if (preset.ElementalPayloadScalingMigrated)
            return;

        List<PlayerStatScalingRule> sourceRules = preset.ScalingRulesMutable;

        if (sourceRules == null)
        {
            preset.ElementalPayloadScalingMigrated = true;
            return;
        }

        List<PlayerStatScalingRule> migratedRules = new List<PlayerStatScalingRule>(sourceRules.Count + 8);

        for (int index = 0; index < sourceRules.Count; index++)
        {
            PlayerStatScalingRule sourceRule = sourceRules[index];

            if (sourceRule == null)
            {
                migratedRules.Add(null);
                continue;
            }

            string normalizedStatKey = NormalizeLegacyStatKey(sourceRule.StatKey);

            if (normalizedStatKey == LegacyAppliedElementKey)
            {
                migratedRules.Add(CloneRuleWithNewStatKey(sourceRule, AppliedElementsPrefix + "0]"));
                continue;
            }

            if (!normalizedStatKey.StartsWith(LegacyElementBehaviourPrefix))
            {
                migratedRules.Add(sourceRule);
                continue;
            }

            string propertySuffix = normalizedStatKey.Substring(LegacyElementBehaviourPrefix.Length);
            migratedRules.Add(CloneRuleWithNewStatKey(sourceRule, BuildElementBehaviourStatKey("fire", propertySuffix)));
            migratedRules.Add(CloneRuleWithNewStatKey(sourceRule, BuildElementBehaviourStatKey("ice", propertySuffix)));
            migratedRules.Add(CloneRuleWithNewStatKey(sourceRule, BuildElementBehaviourStatKey("poison", propertySuffix)));
            migratedRules.Add(CloneRuleWithNewStatKey(sourceRule, BuildElementBehaviourStatKey("custom", propertySuffix)));
        }

        sourceRules.Clear();

        for (int index = 0; index < migratedRules.Count; index++)
            sourceRules.Add(migratedRules[index]);

        preset.ElementalPayloadScalingMigrated = true;
    }

    /// <summary>
    /// Removes Add Scaling rules that target applied-element slots no longer present in the authored array.
    /// /params preset Controller preset whose scaling rules should be pruned.
    /// /returns void.
    /// </summary>
    public static void PruneAppliedElementScalingRules(PlayerControllerPreset preset)
    {
        if (preset == null)
            return;

        List<PlayerStatScalingRule> scalingRules = preset.ScalingRulesMutable;

        if (scalingRules == null || scalingRules.Count <= 0)
            return;

        int slotCount = ResolveAppliedElementSlotCount(preset.ShootingSettings != null ? preset.ShootingSettings.Values : null);

        for (int index = scalingRules.Count - 1; index >= 0; index--)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];

            if (scalingRule == null)
                continue;

            if (!TryExtractAppliedElementSlotIndex(NormalizeLegacyStatKey(scalingRule.StatKey), out int slotIndex))
                continue;

            if (slotIndex >= 0 && slotIndex < slotCount)
                continue;

            scalingRules.RemoveAt(index);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Reports whether at least one authored slot contains a valid gameplay element.
    /// /params appliedElementSlots Fixed-size authored slot array.
    /// /returns True when the array contains at least one element other than None.
    /// </summary>
    private static bool HasAnyAssignedElement(PlayerProjectileAppliedElement[] appliedElementSlots)
    {
        if (appliedElementSlots == null)
            return false;

        for (int index = 0; index < appliedElementSlots.Length; index++)
        {
            if (appliedElementSlots[index] != PlayerProjectileAppliedElement.None)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts one applied-element slot index from a normalized scaling stat key.
    /// /params statKey Normalized scaling stat key.
    /// /params slotIndex Resolved slot index when present.
    /// /returns True when the key targets one applied-element slot entry.
    /// </summary>
    private static bool TryExtractAppliedElementSlotIndex(string statKey, out int slotIndex)
    {
        slotIndex = -1;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        if (!statKey.StartsWith(AppliedElementsPrefix))
            return false;

        int closingBracketIndex = statKey.IndexOf(']', AppliedElementsPrefix.Length);

        if (closingBracketIndex < 0)
            return false;

        string slotIndexText = statKey.Substring(AppliedElementsPrefix.Length, closingBracketIndex - AppliedElementsPrefix.Length);
        return int.TryParse(slotIndexText, out slotIndex);
    }

    /// <summary>
    /// Resolves the current authored applied-element slot count from one shooting values block.
    /// /params values Shooting values container whose slot count should be inspected.
    /// /returns Current authored slot count.
    /// </summary>
    private static int ResolveAppliedElementSlotCount(ShootingValues values)
    {
        if (values == null || values.AppliedElementsMutable == null)
            return 0;

        return values.AppliedElementsMutable.Length;
    }

    /// <summary>
    /// Builds the upgraded stat key for one per-element behaviour property.
    /// /params elementKey Lowercase serialized element field name.
    /// /params propertySuffix Serialized behaviour property suffix.
    /// /returns Upgraded stat key string.
    /// </summary>
    private static string BuildElementBehaviourStatKey(string elementKey, string propertySuffix)
    {
        return ElementBehavioursPrefix + elementKey + "." + propertySuffix;
    }

    /// <summary>
    /// Normalizes one stored stat key so older private backing-field segments do not block migration.
    /// /params statKey Serialized stat key stored by the legacy rule.
    /// /returns Normalized key string used by migration comparisons.
    /// </summary>
    private static string NormalizeLegacyStatKey(string statKey)
    {
        if (string.IsNullOrWhiteSpace(statKey))
            return string.Empty;

        return statKey.Replace("m_", string.Empty);
    }

    /// <summary>
    /// Clones a scaling rule while replacing only the destination stat key.
    /// /params sourceRule Existing rule to clone.
    /// /params statKey New stat key written into the cloned rule.
    /// /returns Cloned scaling rule.
    /// </summary>
    private static PlayerStatScalingRule CloneRuleWithNewStatKey(PlayerStatScalingRule sourceRule, string statKey)
    {
        PlayerStatScalingRule clonedRule = new PlayerStatScalingRule();
        clonedRule.Configure(statKey,
                             sourceRule.AddScaling,
                             sourceRule.Formula,
                             sourceRule.DebugInConsole,
                             sourceRule.DebugColor);
        return clonedRule;
    }
    #endregion

    #endregion
}
