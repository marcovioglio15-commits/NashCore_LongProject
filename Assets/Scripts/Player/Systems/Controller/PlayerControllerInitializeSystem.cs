using Unity.Collections;
using Unity.Entities;


/// <summary>
/// This system initializes player controller entities by adding necessary state 
/// components and buffers based on the presence of a PlayerControllerConfig component. 
/// It runs at the beginning of the PlayerControllerSystemGroup to ensure that all required components 
/// for player control are set up before any other systems in the group execute. 
/// The system checks for missing components such as PlayerInputState, PlayerMovementState, 
/// PlayerLookState, PlayerMovementModifiers, PlayerShootingState, ProjectilePoolState, 
/// ShootRequest buffer, and ProjectilePoolElement buffer, and adds them to entities that 
/// have a PlayerControllerConfig but are missing any of these components.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup), OrderFirst = true)]
public partial struct PlayerControllerInitializeSystem : ISystem
{
    #region Fields
    private EntityQuery missingInputStateQuery;// Query to find entities with PlayerControllerConfig but missing PlayerInputState
    private EntityQuery missingMovementStateQuery; // Query to find entities with PlayerControllerConfig but missing PlayerMovementState
    private EntityQuery missingLookStateQuery; // Query to find entities with PlayerControllerConfig but missing PlayerLookState
    private EntityQuery missingMovementModifiersQuery; // Query to find entities with PlayerControllerConfig but missing PlayerMovementModifiers
    private EntityQuery missingShootingStateQuery; // Query to find entities with PlayerControllerConfig but missing PlayerShootingState
    private EntityQuery missingProjectilePoolStateQuery; // Query to find entities with PlayerControllerConfig and ShooterProjectilePrefab but missing ProjectilePoolState
    private EntityQuery missingShootRequestBufferQuery; // Query to find entities with PlayerControllerConfig and ShooterProjectilePrefab but missing ShootRequest buffer
    private EntityQuery missingProjectilePoolBufferQuery; // Query to find entities with PlayerControllerConfig and ShooterProjectilePrefab but missing ProjectilePoolElement buffer
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        // Require the system to update only when there are entities
        // with PlayerControllerConfig, ensuring that it runs only when necessary
        // to initialize player controller entities.
        state.RequireForUpdate<PlayerControllerConfig>();

        // Define EntityQueries to identify entities that have a
        // PlayerControllerConfig but are missing required components or buffers.

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

    /// <summary>
    /// Update method that checks for entities with PlayerControllerConfig 
    /// that are missing required components and buffers, and adds them using an EntityCommandBuffer.
    /// </summary>
    /// <param name="state"></param>
    public void OnUpdate(ref SystemState state)
    {
        // Check if there are any entities missing required components or buffers
        bool hasMissingInput = missingInputStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingMovement = missingMovementStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingLook = missingLookStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingModifiers = missingMovementModifiersQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingShootingState = missingShootingStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingProjectilePoolState = missingProjectilePoolStateQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingShootRequestBuffer = missingShootRequestBufferQuery.IsEmptyIgnoreFilter == false;
        bool hasMissingProjectilePoolBuffer = missingProjectilePoolBufferQuery.IsEmptyIgnoreFilter == false;

        // If no entities are missing any required components or buffers, exit early
        if (hasMissingInput == false &&
            hasMissingMovement == false &&
            hasMissingLook == false &&
            hasMissingModifiers == false &&
            hasMissingShootingState == false &&
            hasMissingProjectilePoolState == false &&
            hasMissingShootRequestBuffer == false &&
            hasMissingProjectilePoolBuffer == false)
            return;

        // Create an EntityCommandBuffer to batch add components and buffers to entities
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        // For each type of missing component or buffer, query the entities and add the necessary components or buffers using the command buffer
        if (hasMissingInput)
        {
            // Get entities missing PlayerInputState and add the component
            NativeArray<Entity> entities = missingInputStateQuery.ToEntityArray(Allocator.Temp);

            // Loop through the entities and add PlayerInputState component
            for (int index = 0; index < entities.Length; index++)
            {
                Entity entity = entities[index];
                commandBuffer.AddComponent(entity, new PlayerInputState());
            }

            // Dispose of the temporary entity array
            entities.Dispose();
        }

        // Repeat the process for PlayerMovementState, PlayerLookState,
        // PlayerMovementModifiers, PlayerShootingState,
        // ProjectilePoolState, ShootRequest buffer, and ProjectilePoolElement buffer
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

        // Repeat the process for PlayerLookState
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


        // Repeat the process for PlayerMovementModifiers
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

        // Repeat the process for PlayerShootingState
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

        // Repeat the process for ShootRequest buffer
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

        // Repeat the process for ProjectilePoolState
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

        // Repeat the process for ProjectilePoolElement buffer
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

        // Playback the command buffer to apply all component
        // and buffer additions to the entities, then dispose of the command buffer
        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }

    #endregion



}
