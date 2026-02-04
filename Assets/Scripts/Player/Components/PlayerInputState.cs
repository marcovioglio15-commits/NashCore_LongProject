using Unity.Entities;
using Unity.Mathematics;

#region Components
public struct PlayerInputState : IComponentData
{
    public float2 Move;
    public float2 Look;
    public float PrimaryAction;
    public float SecondaryAction;
}
#endregion
