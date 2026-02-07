using Unity.Collections;
using Unity.Entities;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerControllerInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingInputStateQuery;
    private EntityQuery missingMovementStateQuery;
    private EntityQuery missingLookStateQuery;
    private EntityQuery missingMovementModifiersQuery;
    private EntityQuery missingShootingStateQuery;
    private EntityQuery missingProjectilePoolStateQuery;
    private EntityQuery missingShootRequestBufferQuery;
    private EntityQuery missingProjectilePoolBufferQuery;
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControllerConfig>();

        missingInputStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig>()
            .WithNone<PlayerInputState>()
            .Build();

        missingMovementStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig>()
            .WithNone<PlayerMovementState>()
            .Build();

        missingLookStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig>()
            .WithNone<PlayerLookState>()
            .Build();

        missingMovementModifiersQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig>()
            .WithNone<PlayerMovementModifiers>()
            .Build();

        missingShootingStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig>()
            .WithNone<PlayerShootingState>()
            .Build();

        missingProjectilePoolStateQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, ShooterProjectilePrefab>()
            .WithNone<ProjectilePoolState>()
            .Build();

        missingShootRequestBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, ShooterProjectilePrefab>()
            .WithNone<ShootRequest>()
            .Build();

        missingProjectilePoolBufferQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerControllerConfig, ShooterProjectilePrefab>()
            .WithNone<ProjectilePoolElement>()
            .Build();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        bool hasMissingInput = missingInputStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingMovement = missingMovementStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingLook = missingLookStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingModifiers = missingMovementModifiersQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingShootingState = missingShootingStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingProjectilePoolState = missingProjectilePoolStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingShootRequestBuffer = missingShootRequestBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingProjectilePoolBuffer = missingProjectilePoolBufferQuery.IsEmptyIgnoreFilter == false;

        if (hasMissingInput == false &&
            hasMissingMovement == false &&
            hasMissingLook == false &&
            hasMissingModifiers == false &&
            hasMissingShootingState == false &&
            hasMissingProjectilePoolState == false &&
            hasMissingShootRequestBuffer == false &&
            hasMissingProjectilePoolBuffer == false)
            return;

        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        if (hasMissingInput)
        {
            NativeArray<Entity> entities = missingInputStateQuery.ToEntityArray(Allocator.Temp);

            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                commandBuffer.AddComponent(entity, new PlayerInputState());
            }

            entities.Dispose();
        }

        if (hasMissingMovement)
        {
            NativeArray<Entity> entities = missingMovementStateQuery.ToEntityArray(Allocator.Temp);

            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                commandBuffer.AddComponent(entity, new PlayerMovementState());
            }

            entities.Dispose();
        }

        if (hasMissingLook)
        {
            NativeArray<Entity> entities = missingLookStateQuery.ToEntityArray(Allocator.Temp);

            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                commandBuffer.AddComponent(entity, new PlayerLookState());
            }

            entities.Dispose();
        }

        if (hasMissingModifiers)
        {
            NativeArray<Entity> entities = missingMovementModifiersQuery.ToEntityArray(Allocator.Temp);

            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                commandBuffer.AddComponent(entity, new PlayerMovementModifiers
                {
                    MaxSpeedMultiplier = 1f,
                    AccelerationMultiplier = 1f
                });
            }

            entities.Dispose();
        }

        if (hasMissingShootingState)
        {
            NativeArray<Entity> entities = missingShootingStateQuery.ToEntityArray(Allocator.Temp);

            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                commandBuffer.AddComponent(entity, new PlayerShootingState());
            }

            entities.Dispose();
        }

        if (hasMissingShootRequestBuffer)
        {
            NativeArray<Entity> entities = missingShootRequestBufferQuery.ToEntityArray(Allocator.Temp);

            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                commandBuffer.AddBuffer<ShootRequest>(entity);
            }

            entities.Dispose();
        }

        if (hasMissingProjectilePoolState)
        {
            NativeArray<Entity> entities = missingProjectilePoolStateQuery.ToEntityArray(Allocator.Temp);

            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                commandBuffer.AddComponent(entity, new ProjectilePoolState
                {
                    InitialCapacity = 512,
                    ExpandBatch = 128,
                    Initialized = 0
                });
            }

            entities.Dispose();
        }

        if (hasMissingProjectilePoolBuffer)
        {
            NativeArray<Entity> entities = missingProjectilePoolBufferQuery.ToEntityArray(Allocator.Temp);

            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                commandBuffer.AddBuffer<ProjectilePoolElement>(entity);
            }

            entities.Dispose();
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion
}
#endregion
