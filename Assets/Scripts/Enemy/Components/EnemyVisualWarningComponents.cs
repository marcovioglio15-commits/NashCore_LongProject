using Unity.Entities;
using Unity.Mathematics;

#region Enemy Visual Warning Components
/// <summary>
/// Stores immutable visual settings used while shooter enemies charge the first shot of one burst.
/// </summary>
public struct EnemyShooterAimPulseVisualConfig : IComponentData
{
    public byte Enabled;
    public float4 Color;
    public float LeadTimeSeconds;
    public float FadeOutSeconds;
    public float MaximumBlend;
}

/// <summary>
/// Stores the last composed flash values applied to enemy renderers.
/// </summary>
public struct EnemyVisualFlashPresentationState : IComponentData
{
    public float AppliedBlend;
    public float4 AppliedColor;
    public float ShooterPulseBlend;
}
#endregion
