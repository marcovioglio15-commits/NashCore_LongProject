using Unity.Entities;
using Unity.Mathematics;

#region Components
/// <summary>
/// Represents the look direction and angular speed state for a player entity.
/// </summary>
public struct PlayerLookState : IComponentData
{
    public float3 DesiredDirection; // The desired look direction based on player input.
    public float3 CurrentDirection; // The current look direction of the player.
    public float AngularSpeed; // The angular speed at which the player is turning.
    public byte PrevLookMask; // Previous digital look mask.
    public byte CurrLookMask; // Current digital look mask.
    public float4 LookPressTimes; // Timestamp per digital direction (Up, Down, Left, Right).
    public byte ReleaseHoldMask; // Last diagonal mask held during release stabilization.
    public float ReleaseHoldUntilTime; // Time (seconds) until the release stabilization ends.
}
#endregion
