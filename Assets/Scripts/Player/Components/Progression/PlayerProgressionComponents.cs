using Unity.Entities;
using Unity.Collections;

#region Components
/// <summary>
/// Stores runtime player health values.
/// </summary>
public struct PlayerHealth : IComponentData
{
    public float Current;
    public float Max;
}

/// <summary>
/// Stores runtime player shield values.
/// </summary>
public struct PlayerShield : IComponentData
{
    public float Current;
    public float Max;
}

/// <summary>
/// Stores runtime player experience value.
/// </summary>
public struct PlayerExperience : IComponentData
{
    public float Current;
}

/// <summary>
/// Stores one runtime scalable stat value keyed by name.
/// </summary>
public struct PlayerScalableStatElement : IBufferElementData
{
    public FixedString64Bytes Name;
    public byte Type;
    public float Value;
}
#endregion
