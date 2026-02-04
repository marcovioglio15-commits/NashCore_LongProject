using Unity.Entities;
using Unity.Mathematics;

#region Components
public struct PlayerMovementState : IComponentData
{
    public float3 DesiredDirection;
    public float3 Velocity;
}
#endregion
