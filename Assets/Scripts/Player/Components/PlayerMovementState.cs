using Unity.Entities;
using Unity.Mathematics;

#region Components
public struct PlayerMovementState : IComponentData
{
    public float3 DesiredDirection;
    public float3 Velocity;
    public byte PrevMoveMask; // Previous digital move mask.
    public byte CurrMoveMask; // Current digital move mask.
    public float4 MovePressTimes; // Timestamp per digital direction (Up, Down, Left, Right).
    public byte ReleaseHoldMask; // Last diagonal mask held during release stabilization.
    public float ReleaseHoldUntilTime; // Time (seconds) until the release stabilization ends.
}
#endregion
