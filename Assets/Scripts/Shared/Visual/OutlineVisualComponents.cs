using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

/// <summary>
/// Stores immutable outline presentation settings used by managed and Entities Graphics render paths.
/// /params None.
/// /returns None.
/// </summary>
public struct OutlineVisualConfig : IComponentData
{
    #region Fields
    public byte Enabled;
    public float Thickness;
    public float4 Color;
    #endregion
}

/// <summary>
/// Entities Graphics material override for outline color.
/// /params None.
/// /returns None.
/// </summary>
[MaterialProperty("_OutlineColor")]
public struct MaterialOutlineColor : IComponentData
{
    #region Fields
    public float4 Value;
    #endregion
}

/// <summary>
/// Entities Graphics material override for outline thickness.
/// /params None.
/// /returns None.
/// </summary>
[MaterialProperty("_OutlineThickness")]
public struct MaterialOutlineThickness : IComponentData
{
    #region Fields
    public float Value;
    #endregion
}
