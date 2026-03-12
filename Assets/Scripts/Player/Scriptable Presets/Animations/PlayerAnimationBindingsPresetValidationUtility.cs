using System.Collections.Generic;

/// <summary>
/// Provides shared validation helpers for animation bindings presets.
/// </summary>
public static class PlayerAnimationBindingsPresetValidationUtility
{
    public static string TrimParameter(string parameterName, string fallbackValue)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return fallbackValue;

        return parameterName.Trim();
    }

    public static void ValidateScalingRules(List<PlayerStatScalingRule> scalingRules)
    {
        if (scalingRules == null)
            return;

        for (int index = 0; index < scalingRules.Count; index++)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];

            if (scalingRule != null)
                continue;

            scalingRule = new PlayerStatScalingRule();
            scalingRule.Configure(string.Empty, false, string.Empty);
            scalingRules[index] = scalingRule;
        }

        for (int index = scalingRules.Count - 1; index >= 0; index--)
        {
            PlayerStatScalingRule scalingRule = scalingRules[index];
            scalingRule.Validate();

            if (string.IsNullOrWhiteSpace(scalingRule.StatKey))
                scalingRules.RemoveAt(index);
        }
    }
}
