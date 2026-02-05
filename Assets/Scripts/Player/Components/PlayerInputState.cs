using Unity.Entities;
using Unity.Mathematics;

#region Components
/// <summary>
/// Represents player input state including movement, looking direction, and action triggers.
/// </summary>
public struct PlayerInputState : IComponentData
{
    public float2 Move; // Movement input vector (e.g., from joystick or WASD keys).
    public float2 Look; // Look input vector (e.g., from mouse movement or right joystick).
    public float PrimaryAction; // Primary action trigger (e.g., left mouse button or gamepad button).
    public float SecondaryAction; // Secondary action trigger (e.g., right mouse button or gamepad button).
}
#endregion
