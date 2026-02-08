using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Represents player input state including movement, looking direction, and action triggers.
/// </summary>
public struct PlayerInputState : IComponentData
{
    public float2 Move; // Movement input vector (e.g., from joystick or WASD keys).
    public float2 Look; // Look input vector (e.g., from mouse movement or right joystick).
    public float Shoot; // Shooting trigger value (0 = idle, 1 = pressed).
}
