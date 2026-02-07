using Unity.Entities;
using Unity.Mathematics;

#region Components
public struct PlayerShootingState : IComponentData
{
    public byte AutomaticEnabled;
    public byte PreviousShootPressed;
    public float NextShotTime;
}

public struct ShooterProjectilePrefab : IComponentData
{
    public Entity PrefabEntity;
}

public struct ShooterMuzzleAnchor : IComponentData
{
    public Entity AnchorEntity;
}

public struct ProjectilePoolState : IComponentData
{
    public int InitialCapacity;
    public int ExpandBatch;
    public byte Initialized;
}

public struct ShootRequest : IBufferElementData
{
    public float3 Position;
    public float3 Direction;
    public float Speed;
    public float Range;
    public float Lifetime;
    public float Damage;
    public byte InheritPlayerSpeed;
}

public struct ProjectilePoolElement : IBufferElementData
{
    public Entity ProjectileEntity;
}

public struct Projectile : IComponentData
{
    public float3 Velocity;
    public float Damage;
    public float MaxRange;
    public float MaxLifetime;
    public byte InheritPlayerSpeed;
}

public struct ProjectileRuntimeState : IComponentData
{
    public float TraveledDistance;
    public float ElapsedLifetime;
}

public struct ProjectileOwner : IComponentData
{
    public Entity ShooterEntity;
}

public struct ProjectileActive : IComponentData, IEnableableComponent
{
}
#endregion
