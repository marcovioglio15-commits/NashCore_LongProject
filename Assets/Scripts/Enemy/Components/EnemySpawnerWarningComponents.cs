using Unity.Entities;
using Unity.Mathematics;

#region Enemy Spawner Warning Components
/// <summary>
/// Stores immutable visual tuning used by one enemy spawner to preview upcoming spawn events.
/// </summary>
public struct EnemySpawnWarningConfig : IComponentData
{
    public byte Enabled;
    public float LeadTimeSeconds;
    public float FadeOutSeconds;
    public float RadiusScale;
    public float RingWidth;
    public float HeightOffset;
    public float MaximumAlpha;
    public float4 Color;
    public float CellSize;
}

/// <summary>
/// Stores one transient spawn-warning request emitted shortly before a spawn event becomes active.
/// </summary>
public struct EnemySpawnWarningRequestElement : IBufferElementData
{
    public float3 WorldPosition;
    public float DurationSeconds;
    public float FadeOutSeconds;
    public float Radius;
    public float RingWidth;
    public float MaximumAlpha;
    public float4 Color;
}
#endregion
