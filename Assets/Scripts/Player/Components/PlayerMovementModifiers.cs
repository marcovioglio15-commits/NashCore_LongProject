using Unity.Entities;

/// <summary>
/// This component holds modifiers for player movement, 
/// such as maximum speed and acceleration multipliers.
/// </summary>
public struct PlayerMovementModifiers : IComponentData
{
    // Multiplier for the player's maximum movement speed (e.g., due to buffs, debuffs, or environmental effects).
    public float MaxSpeedMultiplier;
    // Multiplier for the player's acceleration (e.g., due to buffs, debuffs, or environmental effects).
    public float AccelerationMultiplier;
}
