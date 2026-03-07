using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

#region Components
/// <summary>
/// Stores one debug snapshot for a scaled stat, including input [this] value and evaluated final value.
/// </summary>
public struct PlayerScalingDebugRuleElement : IBufferElementData
{
    public FixedString64Bytes PresetTypeLabel;
    public FixedString64Bytes TargetDisplayName;
    public FixedString128Bytes StatKey;
    public FixedString512Bytes Formula;
    public float ThisValue;
    public float FinalValue;
    public float4 DebugColor;
}
#endregion
