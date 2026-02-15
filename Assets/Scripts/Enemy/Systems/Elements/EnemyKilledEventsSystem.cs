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
                  RefRO<LocalTransform> enemyTransform,
                  Entity enemyEntity) in SystemAPI.Query<RefRO<EnemyDespawnRequest>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (despawnRequest.ValueRO.Reason != EnemyDespawnReason.Killed)
                continue;

            killedEventsBuffer.Add(new EnemyKilledEventElement
            {
                EnemyEntity = enemyEntity,
                Position = enemyTransform.ValueRO.Position
            });
        }
    }
    #endregion

    #endregion
}
