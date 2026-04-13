using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides shared default authored values used by Laser Beam visual preset data and bake fallbacks.
/// /params None.
/// /returns None.
/// </summary>
public static class PlayerLaserBeamVisualDefaultsUtility
{
    #region Constants
    public const string DefaultBodyMaterialPath = "Assets/3D/Materials/M_LiquidAntibioticBeam.mat";
    public const string DefaultSourceEffectMaterialPath = "Assets/3D/Materials/M_LiquidAntibioticBeamBubbles.mat";
    public const string DefaultTerminalCapMaterialPath = "Assets/3D/Materials/M_LiquidAntibioticBeamSplash.mat";
    public const float DefaultVerticalLift = 0.06f;
    public const float DefaultMinimumSegmentLength = 0.02f;
    public const int DefaultVisualPresetId = 0;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Creates one authored visual preset entry initialized with a default name and the default colors for the requested ID.
    /// /params stableId Stable numeric ID to initialize.
    /// /returns New visual preset definition populated with default colors.
    /// </summary>
    public static PlayerLaserBeamVisualPresetDefinition CreateDefaultVisualPresetDefinition(int stableId = DefaultVisualPresetId)
    {
        ResolveDefaultPreset(stableId,
                             out string displayName,
                             out Color coreColor,
                             out Color flowColor,
                             out Color stormColor,
                             out Color contactColor);
        PlayerLaserBeamVisualPresetDefinition visualPresetDefinition = new PlayerLaserBeamVisualPresetDefinition();
        visualPresetDefinition.Assign(stableId,
                                      displayName,
                                      coreColor,
                                      flowColor,
                                      stormColor,
                                      contactColor);
        return visualPresetDefinition;
    }

    /// <summary>
    /// Validates authored Laser Beam visual preset entries without rewriting designer-authored IDs or names.
    /// /params visualPresetDefinitions Mutable preset list authored on the visual preset.
    /// /returns None.
    /// </summary>
    public static void ValidateVisualPresetDefinitions(List<PlayerLaserBeamVisualPresetDefinition> visualPresetDefinitions)
    {
        if (visualPresetDefinitions == null)
            return;

        for (int presetIndex = 0; presetIndex < visualPresetDefinitions.Count; presetIndex++)
        {
            PlayerLaserBeamVisualPresetDefinition visualPresetDefinition = visualPresetDefinitions[presetIndex];

            if (visualPresetDefinition == null)
                continue;

            visualPresetDefinition.Validate();
        }
    }

    /// <summary>
    /// Resolves the default label and colors used when no authored visual preset matches the requested stable ID.
    /// /params stableId Stable numeric preset ID.
    /// /params displayName Default designer-facing preset name.
    /// /params coreColor White-hot core color.
    /// /params flowColor Primary beam flow color.
    /// /params stormColor Electrical storm color.
    /// /params contactColor Terminal cap and contact highlight color.
    /// /returns None.
    /// </summary>
    public static void ResolveDefaultPreset(int stableId,
                                            out string displayName,
                                            out Color coreColor,
                                            out Color flowColor,
                                            out Color stormColor,
                                            out Color contactColor)
    {
        switch (stableId)
        {
            case 1:
                displayName = "Sterile Mint";
                coreColor = new Color(0.97f, 1f, 0.98f, 1f);
                flowColor = new Color(0.22f, 0.92f, 0.72f, 1f);
                stormColor = new Color(0.86f, 1f, 0.93f, 1f);
                contactColor = new Color(0.96f, 1f, 0.97f, 1f);
                return;
            case 2:
                displayName = "Deep Azure";
                coreColor = new Color(0.95f, 0.98f, 1f, 1f);
                flowColor = new Color(0.14509805f, 0.18431373f, 1f, 1f);
                stormColor = new Color(0.56f, 0.74f, 1f, 1f);
                contactColor = new Color(0.92f, 0.97f, 1f, 1f);
                return;
            case 3:
                displayName = "Solar Amber";
                coreColor = new Color(1f, 0.98f, 0.9f, 1f);
                flowColor = new Color(1f, 0.82f, 0.24f, 1f);
                stormColor = new Color(1f, 0.97f, 0.72f, 1f);
                contactColor = new Color(1f, 0.96f, 0.76f, 1f);
                return;
            default:
                displayName = "Electric Azure";
                coreColor = new Color(0.97f, 0.99f, 1f, 1f);
                flowColor = new Color(0.19f, 0.93f, 1f, 1f);
                stormColor = new Color(0.75f, 1f, 1f, 1f);
                contactColor = new Color(0.94f, 0.98f, 1f, 1f);
                return;
        }
    }
    #endregion

    #endregion
}
