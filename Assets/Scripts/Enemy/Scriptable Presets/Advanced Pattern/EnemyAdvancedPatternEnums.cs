/// <summary>
/// Declares supported enemy pattern module categories.
/// </summary>
public enum EnemyPatternModuleKind
{
    Stationary = 0,
    Grunt = 1,
    Wanderer = 2,
    Shooter = 3,
    DropItems = 4
}

/// <summary>
/// Declares movement variants available for Wanderer modules.
/// </summary>
public enum EnemyWandererMode
{
    Basic = 0,
    Dvd = 1
}

/// <summary>
/// Declares how Shooter modules resolve aim direction.
/// </summary>
public enum EnemyShooterAimPolicy
{
    LockOnFireStart = 0,
    ContinuousTracking = 1
}

/// <summary>
/// Declares how Shooter modules interact with movement while firing.
/// </summary>
public enum EnemyShooterMovementPolicy
{
    KeepMoving = 0,
    StopWhileAiming = 1
}

/// <summary>
/// Declares runtime-resolved movement pattern kind used by ECS systems.
/// </summary>
public enum EnemyCompiledMovementPatternKind : byte
{
    Grunt = 0,
    Stationary = 1,
    WandererBasic = 2,
    WandererDvd = 3
}

/// <summary>
/// Declares drop-items payload categories supported by DropItems modules.
/// </summary>
public enum EnemyDropItemsPayloadKind
{
    Experience = 0
}
