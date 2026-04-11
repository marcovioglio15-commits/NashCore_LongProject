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
    public const string DefaultSourceBubbleMaterialPath = "Assets/3D/Materials/M_LiquidAntibioticBeamBubbles.mat";
    public const string DefaultImpactSplashMaterialPath = "Assets/3D/Materials/M_LiquidAntibioticBeamSplash.mat";
    public const float DefaultVerticalLift = 0.06f;
    public const float DefaultMinimumSegmentLength = 0.02f;
    #endregion

    #region Fields
    private static readonly LaserBeamVisualPalette[] supportedPalettes =
    {
        LaserBeamVisualPalette.AntibioticBlue,
        LaserBeamVisualPalette.SterileMint,
        LaserBeamVisualPalette.ToxicLime,
        LaserBeamVisualPalette.PlasmaAmber
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Returns the supported palette list used by Laser Beam visual authoring.
    /// /params None.
    /// /returns Stable ordered palette array.
    /// </summary>
    public static IReadOnlyList<LaserBeamVisualPalette> SupportedPalettes
    {
        get
        {
            return supportedPalettes;
        }
    }

    /// <summary>
    /// Ensures the authored palette list contains one valid entry for every supported Laser Beam palette.
    /// /params paletteSettings Mutable palette list authored on the visual preset.
    /// /returns None.
    /// </summary>
    public static void EnsurePaletteCoverage(List<PlayerLaserBeamPaletteSettings> paletteSettings)
    {
        if (paletteSettings == null)
            return;

        HashSet<LaserBeamVisualPalette> coveredPalettes = new HashSet<LaserBeamVisualPalette>();

        for (int paletteIndex = paletteSettings.Count - 1; paletteIndex >= 0; paletteIndex--)
        {
            PlayerLaserBeamPaletteSettings paletteSetting = paletteSettings[paletteIndex];

            if (paletteSetting == null)
            {
                paletteSettings.RemoveAt(paletteIndex);
                continue;
            }

            LaserBeamVisualPalette visualPalette = paletteSetting.VisualPalette;

            if (coveredPalettes.Add(visualPalette))
            {
                paletteSetting.Validate();
                continue;
            }

            paletteSettings.RemoveAt(paletteIndex);
        }

        for (int paletteIndex = 0; paletteIndex < supportedPalettes.Length; paletteIndex++)
        {
            LaserBeamVisualPalette visualPalette = supportedPalettes[paletteIndex];

            if (coveredPalettes.Contains(visualPalette))
                continue;

            paletteSettings.Add(CreateDefaultPaletteSettings(visualPalette));
        }
    }

    /// <summary>
    /// Creates one authored palette entry initialized with the default colors for the requested enum.
    /// /params visualPalette Palette enum to initialize.
    /// /returns New palette settings instance populated with default colors.
    /// </summary>
    public static PlayerLaserBeamPaletteSettings CreateDefaultPaletteSettings(LaserBeamVisualPalette visualPalette)
    {
        ResolveDefaultColors(visualPalette,
                             out Color bodyColorA,
                             out Color bodyColorB,
                             out Color coreColor,
                             out Color rimColor);
        PlayerLaserBeamPaletteSettings paletteSettings = new PlayerLaserBeamPaletteSettings();
        paletteSettings.Assign(visualPalette,
                               bodyColorA,
                               bodyColorB,
                               coreColor,
                               rimColor);
        return paletteSettings;
    }

    /// <summary>
    /// Resolves the default editable colors used by one Laser Beam palette enum.
    /// /params visualPalette Requested palette enum.
    /// /params bodyColorA Primary liquid gradient color.
    /// /params bodyColorB Secondary liquid gradient color.
    /// /params coreColor Bright central streak color.
    /// /params rimColor Outer contour color.
    /// /returns None.
    /// </summary>
    public static void ResolveDefaultColors(LaserBeamVisualPalette visualPalette,
                                            out Color bodyColorA,
                                            out Color bodyColorB,
                                            out Color coreColor,
                                            out Color rimColor)
    {
        switch (visualPalette)
        {
            case LaserBeamVisualPalette.SterileMint:
                bodyColorA = new Color(0.12f, 0.74f, 0.58f, 1f);
                bodyColorB = new Color(0.39f, 0.95f, 0.73f, 1f);
                coreColor = new Color(0.78f, 1f, 0.92f, 1f);
                rimColor = new Color(0.03f, 0.29f, 0.18f, 1f);
                return;
            case LaserBeamVisualPalette.ToxicLime:
                bodyColorA = new Color(0.26f, 0.83f, 0.1f, 1f);
                bodyColorB = new Color(0.63f, 0.98f, 0.24f, 1f);
                coreColor = new Color(0.9f, 1f, 0.68f, 1f);
                rimColor = new Color(0.15f, 0.31f, 0.05f, 1f);
                return;
            case LaserBeamVisualPalette.PlasmaAmber:
                bodyColorA = new Color(0.85f, 0.58f, 0.14f, 1f);
                bodyColorB = new Color(1f, 0.84f, 0.26f, 1f);
                coreColor = new Color(1f, 0.97f, 0.72f, 1f);
                rimColor = new Color(0.4f, 0.24f, 0.05f, 1f);
                return;
            default:
                bodyColorA = new Color(0.04f, 0.28f, 0.77f, 1f);
                bodyColorB = new Color(0.12f, 0.64f, 0.98f, 1f);
                coreColor = new Color(0.68f, 0.94f, 1f, 1f);
                rimColor = new Color(0.01f, 0.11f, 0.42f, 1f);
                return;
        }
    }
    #endregion

    #endregion
}
