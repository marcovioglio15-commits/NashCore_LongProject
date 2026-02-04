using Unity.Entities;
using Unity.Mathematics;

#region Components
public struct PlayerLookState : IComponentData
{
    public float3 DesiredDirection;
    public float3 CurrentDirection;
    public float AngularSpeed;
}
#endregion
