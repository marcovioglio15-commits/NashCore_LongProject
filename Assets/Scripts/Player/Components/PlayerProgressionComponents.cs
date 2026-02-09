using Unity.Entities;

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
/// Stores runtime player experience value.
/// </summary>
public struct PlayerExperience : IComponentData
{
    public float Current;
}
#endregion
