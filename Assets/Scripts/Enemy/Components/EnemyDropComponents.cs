using Unity.Entities;
using Unity.Mathematics;

#region Drop Components
/// <summary>
/// Stores baked enemy drop module settings used at runtime.
/// </summary>
public struct EnemyDropItemsConfig : IComponentData
{
    public EnemyDropItemsPayloadKind PayloadKind;
    public float MinimumTotalExperienceDrop;
    public float MaximumTotalExperienceDrop;
    public float Distribution;
    public float DropRadius;
    public float AttractionSpeed;
    public float CollectDistance;
    public float CollectDistancePerPlayerSpeed;
    public float SpawnAnimationMinDuration;
    public float SpawnAnimationMaxDuration;
    public int EstimatedDropsPerDeath;
}

/// <summary>
/// Stores one baked experience drop-definition entry.
/// </summary>
public struct EnemyExperienceDropDefinitionElement : IBufferElementData
{
    public Entity PrefabEntity;
    public float ExperienceAmount;
}

/// <summary>
/// Stores runtime data for one spawned experience drop entity.
/// </summary>
public struct EnemyExperienceDrop : IComponentData
{
    public float ExperienceAmount;
    public float AttractionSpeed;
    public float CollectDistance;
    public float CollectDistancePerPlayerSpeed;
    public float3 SpawnStartPosition;
    public float3 SpawnTargetPosition;
    public float SpawnAnimationDuration;
    public float SpawnAnimationElapsed;
    public Entity PoolEntity;
    public byte IsAttracting;
}

/// <summary>
/// Marks active pooled experience drop entities that must be simulated.
/// </summary>
public struct EnemyExperienceDropActive : IComponentData, IEnableableComponent
{
}

/// <summary>
/// Singleton registry state for experience drop pools.
/// </summary>
public struct EnemyExperienceDropPoolRegistry : IComponentData
{
    public byte Initialized;
}

/// <summary>
/// Maps one drop prefab entity to its dedicated pool entity.
/// </summary>
public struct EnemyExperienceDropPoolMapElement : IBufferElementData
{
    public Entity PrefabEntity;
    public Entity PoolEntity;
}

/// <summary>
/// Stores one experience drop pool configuration and initialization state.
/// </summary>
public struct EnemyExperienceDropPoolState : IComponentData
{
    public Entity PrefabEntity;
    public int InitialCapacity;
    public int ExpandBatch;
    public byte Initialized;
}

/// <summary>
/// Stores pooled experience drop entity references for one pool.
/// </summary>
public struct EnemyExperienceDropPoolElement : IBufferElementData
{
    public Entity DropEntity;
}
#endregion
