using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Adds elemental runtime components and buffers to enemy entities when missing.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup), OrderFirst = true)]
public partial struct EnemyElementalInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingRuntimeStateQuery;
    private EntityQuery missingStacksBufferQuery;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyData>();

        missingRuntimeStateQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData>()
            .WithNone<EnemyElementalRuntimeState>()
            .Build();

        missingStacksBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyData>()
            .WithNone<EnemyElementStackElement>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        bool hasMissingRuntimeState = missingRuntimeStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingStacksBuffer = missingStacksBufferQuery.IsEmptyIgnoreFilter == false;

        if (hasMissingRuntimeState == false && hasMissingStacksBuffer == false)
            return;

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        if (hasMissingRuntimeState)
            AddMissingRuntimeStates(ref commandBuffer);

        if (hasMissingStacksBuffer)
            AddMissingStackBuffers(ref commandBuffer);

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private void AddMissingRuntimeStates(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingRuntimeStateQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
        {
            commandBuffer.AddComponent(entities[index], new EnemyElementalRuntimeState
            {
                SlowPercent = 0f
            });
        }

        entities.Dispose();
    }

    private void AddMissingStackBuffers(ref EntityCommandBuffer commandBuffer)
    {
        NativeArray<Entity> entities = missingStacksBufferQuery.ToEntityArray(Allocator.Temp);

        for (int index = 0; index < entities.Length; index++)
            commandBuffer.AddBuffer<EnemyElementStackElement>(entities[index]);

        entities.Dispose();
    }
    #endregion

    #endregion
}
