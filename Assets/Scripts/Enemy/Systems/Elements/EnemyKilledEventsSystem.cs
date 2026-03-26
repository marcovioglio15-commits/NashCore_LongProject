using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

/// <summary>
/// Collects enemy killed events into a singleton buffer for cross-system gameplay triggers.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(EnemyDespawnSystem))]
[UpdateBefore(typeof(EnemyKillCounterSystem))]
[UpdateBefore(typeof(EnemyFinalizeDespawnSystem))]
public partial struct EnemyKilledEventsSystem : ISystem
{
    #region Fields
    private Entity killedEventsEntity;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        EntityQuery eventsQuery = state.GetEntityQuery(ComponentType.ReadOnly<EnemyKilledEventElement>());

        if (eventsQuery.IsEmptyIgnoreFilter)
        {
            killedEventsEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<EnemyKilledEventElement>(killedEventsEntity);
            return;
        }

        NativeArray<Entity> entities = eventsQuery.ToEntityArray(Allocator.Temp);
        killedEventsEntity = entities.Length > 0 ? entities[0] : Entity.Null;
        entities.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (killedEventsEntity == Entity.Null || state.EntityManager.Exists(killedEventsEntity) == false)
        {
            killedEventsEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<EnemyKilledEventElement>(killedEventsEntity);
        }

        DynamicBuffer<EnemyKilledEventElement> killedEventsBuffer = state.EntityManager.GetBuffer<EnemyKilledEventElement>(killedEventsEntity);
        killedEventsBuffer.Clear();

        foreach ((RefRO<EnemyDespawnRequest> despawnRequest,
                  RefRO<EnemyData> enemyData,
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity) in SystemAPI.Query<RefRO<EnemyDespawnRequest>,
                                                         RefRO<EnemyData>,
                                                         RefRO<LocalTransform>>()
                                                 .WithEntityAccess())
        {
            if (despawnRequest.ValueRO.Reason != EnemyDespawnReason.Killed)
                continue;

            float3 killedEventPosition = ResolveKilledEventPosition(enemyTransform.ValueRO.Position, enemyData.ValueRO.BodyRadius);

            killedEventsBuffer.Add(new EnemyKilledEventElement
            {
                EnemyEntity = enemyEntity,
                Position = killedEventPosition
            });
        }
    }

    /// <summary>
    /// Resolves a stable world position used by death-triggered gameplay VFX spawned from killed events.
    /// </summary>
    /// <param name="enemyPosition">Enemy transform position sampled before pooled finalize-despawn.</param>
    /// <param name="bodyRadius">Enemy body radius used to add a small upward safety offset.</param>
    /// <returns>Adjusted kill-event world position with vertical lift to avoid floor clipping.<returns>
    private static float3 ResolveKilledEventPosition(float3 enemyPosition, float bodyRadius)
    {
        float3 resolvedPosition = enemyPosition;
        float verticalOffset = math.max(0.05f, math.max(0f, bodyRadius) * 0.35f);
        resolvedPosition.y += verticalOffset;
        return resolvedPosition;
    }
    #endregion

    #endregion
}
