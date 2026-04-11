using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds authored Laser Beam visual runtime data from the active player visual preset.
/// /params None.
/// /returns None.
/// </summary>
public static class PlayerLaserBeamVisualBakeUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds the shared Laser Beam visual config baked on the player entity.
    /// /params authoring Player authoring used to resolve the active visual preset.
    /// /returns Baked Laser Beam visual config.
    /// </summary>
    public static PlayerLaserBeamVisualConfig BuildConfig(PlayerAuthoring authoring)
    {
        PlayerLaserBeamVisualSettings visualSettings = ResolveVisualSettings(authoring);
        PlayerLaserBeamVisualRigAuthoring rigAuthoring = ResolveRigAuthoring(authoring);

        if (visualSettings == null)
        {
            return new PlayerLaserBeamVisualConfig
            {
                BodyMaterial = default,
                SourceBubbleMaterial = default,
                ImpactSplashMaterial = default,
                VerticalLift = PlayerLaserBeamVisualDefaultsUtility.DefaultVerticalLift,
                MinimumSegmentLength = PlayerLaserBeamVisualDefaultsUtility.DefaultMinimumSegmentLength,
                MaximumRibbonSegmentLength = 0.18f,
                TerminalSplashLengthMultiplier = 1.2f,
                TerminalSplashWidthMultiplier = 1.7f,
                SourceForwardOffset = 0.02f,
                ImpactForwardOffset = 0f
            };
        }

        return new PlayerLaserBeamVisualConfig
        {
            BodyMaterial = visualSettings.BeamMaterial,
            SourceBubbleMaterial = visualSettings.SourceBubbleMaterial,
            ImpactSplashMaterial = visualSettings.ImpactSplashMaterial,
            VerticalLift = math.max(0f, visualSettings.VerticalLift),
            MinimumSegmentLength = math.max(0.001f, visualSettings.MinimumSegmentLength),
            MaximumRibbonSegmentLength = math.max(0.01f, rigAuthoring != null ? rigAuthoring.MaximumRibbonSegmentLength : 0.18f),
            TerminalSplashLengthMultiplier = math.max(0.01f, rigAuthoring != null ? rigAuthoring.TerminalSplashLengthMultiplier : 1.2f),
            TerminalSplashWidthMultiplier = math.max(0.01f, rigAuthoring != null ? rigAuthoring.TerminalSplashWidthMultiplier : 1.7f),
            SourceForwardOffset = rigAuthoring != null ? rigAuthoring.SourceForwardOffset : 0.02f,
            ImpactForwardOffset = rigAuthoring != null ? rigAuthoring.ImpactForwardOffset : 0f
        };
    }

    /// <summary>
    /// Populates the baked source prefab variants resolved from the active visual rig authoring component.
    /// /params authoring Player authoring used to resolve the active visual rig.
    /// /params variantBuffer Destination buffer written in-place.
    /// /returns None.
    /// </summary>
    public static void PopulateSourceVariantBuffer(PlayerAuthoring authoring, DynamicBuffer<PlayerLaserBeamSourceVariantElement> variantBuffer)
    {
        variantBuffer.Clear();
        PlayerLaserBeamVisualRigAuthoring rigAuthoring = ResolveRigAuthoring(authoring);

        AddSourceVariant(variantBuffer,
                         LaserBeamCapShape.BubbleBurst,
                         rigAuthoring != null ? rigAuthoring.BubbleBurstSourcePrefab : null);
        AddSourceVariant(variantBuffer,
                         LaserBeamCapShape.StarBloom,
                         rigAuthoring != null ? rigAuthoring.StarBloomSourcePrefab : null);
        AddSourceVariant(variantBuffer,
                         LaserBeamCapShape.SoftDisc,
                         rigAuthoring != null ? rigAuthoring.SoftDiscSourcePrefab : null);
    }

    /// <summary>
    /// Populates the baked impact prefab variants resolved from the active visual rig authoring component.
    /// /params authoring Player authoring used to resolve the active visual rig.
    /// /params variantBuffer Destination buffer written in-place.
    /// /returns None.
    /// </summary>
    public static void PopulateImpactVariantBuffer(PlayerAuthoring authoring, DynamicBuffer<PlayerLaserBeamImpactVariantElement> variantBuffer)
    {
        variantBuffer.Clear();
        PlayerLaserBeamVisualRigAuthoring rigAuthoring = ResolveRigAuthoring(authoring);

        AddImpactVariant(variantBuffer,
                         LaserBeamCapShape.BubbleBurst,
                         rigAuthoring != null ? rigAuthoring.BubbleBurstImpactPrefab : null);
        AddImpactVariant(variantBuffer,
                         LaserBeamCapShape.StarBloom,
                         rigAuthoring != null ? rigAuthoring.StarBloomImpactPrefab : null);
        AddImpactVariant(variantBuffer,
                         LaserBeamCapShape.SoftDisc,
                         rigAuthoring != null ? rigAuthoring.SoftDiscImpactPrefab : null);
    }

    /// <summary>
    /// Populates the baked palette buffer used by the Laser Beam managed renderer path.
    /// /params authoring Player authoring used to resolve the active visual preset.
    /// /params paletteBuffer Destination buffer written in-place.
    /// /returns None.
    /// </summary>
    public static void PopulatePaletteBuffer(PlayerAuthoring authoring, DynamicBuffer<PlayerLaserBeamPaletteElement> paletteBuffer)
    {
        paletteBuffer.Clear();

        if (paletteBuffer.Capacity < PlayerLaserBeamVisualDefaultsUtility.SupportedPalettes.Count)
            paletteBuffer.Capacity = PlayerLaserBeamVisualDefaultsUtility.SupportedPalettes.Count;

        PlayerLaserBeamVisualSettings visualSettings = ResolveVisualSettings(authoring);
        IReadOnlyList<PlayerLaserBeamPaletteSettings> authoredPalettes = visualSettings != null
            ? visualSettings.Palettes
            : null;
        HashSet<LaserBeamVisualPalette> coveredPalettes = new HashSet<LaserBeamVisualPalette>();

        if (authoredPalettes != null)
        {
            for (int paletteIndex = 0; paletteIndex < authoredPalettes.Count; paletteIndex++)
            {
                PlayerLaserBeamPaletteSettings paletteSettings = authoredPalettes[paletteIndex];

                if (paletteSettings == null)
                    continue;

                if (!coveredPalettes.Add(paletteSettings.VisualPalette))
                    continue;

                paletteBuffer.Add(new PlayerLaserBeamPaletteElement
                {
                    VisualPalette = paletteSettings.VisualPalette,
                    BodyColorA = DamageFlashRuntimeUtility.ToLinearFloat4(paletteSettings.BodyColorA),
                    BodyColorB = DamageFlashRuntimeUtility.ToLinearFloat4(paletteSettings.BodyColorB),
                    CoreColor = DamageFlashRuntimeUtility.ToLinearFloat4(paletteSettings.CoreColor),
                    RimColor = DamageFlashRuntimeUtility.ToLinearFloat4(paletteSettings.RimColor)
                });
            }
        }

        IReadOnlyList<LaserBeamVisualPalette> supportedPalettes = PlayerLaserBeamVisualDefaultsUtility.SupportedPalettes;

        for (int paletteIndex = 0; paletteIndex < supportedPalettes.Count; paletteIndex++)
        {
            LaserBeamVisualPalette visualPalette = supportedPalettes[paletteIndex];

            if (coveredPalettes.Contains(visualPalette))
                continue;

            PlayerLaserBeamVisualDefaultsUtility.ResolveDefaultColors(visualPalette,
                                                                     out Color bodyColorA,
                                                                     out Color bodyColorB,
                                                                     out Color coreColor,
                                                                     out Color rimColor);
            paletteBuffer.Add(new PlayerLaserBeamPaletteElement
            {
                VisualPalette = visualPalette,
                BodyColorA = DamageFlashRuntimeUtility.ToLinearFloat4(bodyColorA),
                BodyColorB = DamageFlashRuntimeUtility.ToLinearFloat4(bodyColorB),
                CoreColor = DamageFlashRuntimeUtility.ToLinearFloat4(coreColor),
                RimColor = DamageFlashRuntimeUtility.ToLinearFloat4(rimColor)
            });
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the active authored Laser Beam visual settings from the current player master preset.
    /// /params authoring Player authoring used to resolve the current master preset.
    /// /returns Resolved visual settings asset, or null when unavailable.
    /// </summary>
    private static PlayerLaserBeamVisualSettings ResolveVisualSettings(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return null;

        return PlayerAuthoringVisualPresetResolverUtility.ResolveLaserBeamVisualSettings(authoring.MasterPreset);
    }

    /// <summary>
    /// Resolves the active runtime visual rig authoring component from the player visual bridge prefab.
    /// /params authoring Player authoring used to resolve the current runtime visual bridge prefab.
    /// /returns Resolved rig authoring component, or null when unavailable.
    /// </summary>
    private static PlayerLaserBeamVisualRigAuthoring ResolveRigAuthoring(PlayerAuthoring authoring)
    {
        if (authoring == null)
            return null;

        GameObject runtimeVisualBridgePrefab = PlayerAuthoringVisualPresetResolverUtility.ResolveRuntimeVisualBridgePrefab(authoring.MasterPreset,
                                                                                                                            authoring.RuntimeVisualBridgePrefab);

        if (runtimeVisualBridgePrefab == null)
            return null;

        return runtimeVisualBridgePrefab.GetComponent<PlayerLaserBeamVisualRigAuthoring>();
    }

    /// <summary>
    /// Adds one baked source variant entry to the destination buffer.
    /// /params variantBuffer Destination buffer written in-place.
    /// /params shape Runtime cap-shape selector stored by the entry.
    /// /params prefab Authored prefab reference stored by the entry.
    /// /returns None.
    /// </summary>
    private static void AddSourceVariant(DynamicBuffer<PlayerLaserBeamSourceVariantElement> variantBuffer,
                                         LaserBeamCapShape shape,
                                         GameObject prefab)
    {
        variantBuffer.Add(new PlayerLaserBeamSourceVariantElement
        {
            Shape = shape,
            Prefab = prefab
        });
    }

    /// <summary>
    /// Adds one baked impact variant entry to the destination buffer.
    /// /params variantBuffer Destination buffer written in-place.
    /// /params shape Runtime cap-shape selector stored by the entry.
    /// /params prefab Authored prefab reference stored by the entry.
    /// /returns None.
    /// </summary>
    private static void AddImpactVariant(DynamicBuffer<PlayerLaserBeamImpactVariantElement> variantBuffer,
                                         LaserBeamCapShape shape,
                                         GameObject prefab)
    {
        variantBuffer.Add(new PlayerLaserBeamImpactVariantElement
        {
            Shape = shape,
            Prefab = prefab
        });
    }
    #endregion

    #endregion
}
