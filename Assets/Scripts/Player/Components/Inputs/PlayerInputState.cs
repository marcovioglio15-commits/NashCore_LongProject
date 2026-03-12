using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Represents player input state including movement, looking direction, and action triggers.
/// </summary>
public struct PlayerInputState : IComponentData
{
    public float2 Move; // Movement input vector (e.g., from joystick or WASD keys).
    public float2 Look; // Look input vector (e.g., right joystick). Mouse-pointer look is resolved separately at runtime.
    public float Shoot; // Shooting trigger value (0 = idle, 1 = pressed).
    public float PowerUpPrimary; // Primary active-tool trigger value.
    public float PowerUpSecondary; // Secondary active-tool trigger value.
    public float SwapPowerUpSlots; // Active-slot swap trigger value.
}
