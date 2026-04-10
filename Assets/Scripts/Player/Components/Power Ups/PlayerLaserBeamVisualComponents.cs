using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores shared asset references and geometric presentation settings used by the Laser Beam managed renderer path.
/// </summary>
public struct PlayerLaserBeamVisualConfig : IComponentData
{
    #region Fields
    public UnityObjectRef<Material> BodyMaterial;
    public UnityObjectRef<Material> SourceBubbleMaterial;
    public UnityObjectRef<Material> ImpactSplashMaterial;
    public float VerticalLift;
    public float MinimumSegmentLength;
    public float MaximumVisualSegmentLength;
    public float BodyBlobSpacingMultiplier;
    public float BodyBlobLengthMultiplier;
    public float BodyBlobWidthMultiplier;
    public float SourceForwardOffset;
    public float ImpactForwardOffset;
    #endregion
}

/// <summary>
/// Stores one authored 3D body prefab mapped to a Laser Beam body profile.
/// </summary>
public struct PlayerLaserBeamBodyVariantElement : IBufferElementData
{
    #region Fields
    public LaserBeamBodyProfile BodyProfile;
    public UnityObjectRef<GameObject> Prefab;
    #endregion
}

/// <summary>
/// Stores one authored source particle prefab mapped to a Laser Beam cap shape.
/// </summary>
public struct PlayerLaserBeamSourceVariantElement : IBufferElementData
{
    #region Fields
    public LaserBeamCapShape Shape;
    public UnityObjectRef<GameObject> Prefab;
    #endregion
}

/// <summary>
/// Stores one authored impact particle prefab mapped to a Laser Beam cap shape.
/// </summary>
public struct PlayerLaserBeamImpactVariantElement : IBufferElementData
{
    #region Fields
    public LaserBeamCapShape Shape;
    public UnityObjectRef<GameObject> Prefab;
    #endregion
}

/// <summary>
/// Stores one editable palette entry used by the Laser Beam managed renderer path.
/// </summary>
public struct PlayerLaserBeamPaletteElement : IBufferElementData
{
    #region Fields
    public LaserBeamVisualPalette VisualPalette;
    public float4 BodyColorA;
    public float4 BodyColorB;
    public float4 CoreColor;
    public float4 RimColor;
    #endregion
}
