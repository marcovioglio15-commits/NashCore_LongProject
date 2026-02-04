using Unity.Entities;

#region Components
public struct PlayerMovementModifiers : IComponentData
{
    public float MaxSpeedMultiplier;
    public float AccelerationMultiplier;
}
#endregion
