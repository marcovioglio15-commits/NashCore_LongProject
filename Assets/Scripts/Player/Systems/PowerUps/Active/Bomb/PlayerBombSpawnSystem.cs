using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns bomb entities from activation requests and initializes fuse data.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerBombSpawnSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerBombSpawnRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach (DynamicBuffer<PlayerBombSpawnRequest> bombRequests in SystemAPI.Query<DynamicBuffer<PlayerBombSpawnRequest>>())
        {
            for (int requestIndex = 0; requestIndex < bombRequests.Length; requestIndex++)
            {
                PlayerBombSpawnRequest request = bombRequests[requestIndex];

                if (request.BombPrefabEntity == Entity.Null)
                    continue;

                Entity bombEntity = commandBuffer.Instantiate(request.BombPrefabEntity);

                LocalTransform bombTransform = LocalTransform.FromPositionRotation(request.Position, request.Rotation);

                if (entityManager.HasComponent<LocalTransform>(request.BombPrefabEntity))
                    commandBuffer.SetComponent(bombEntity, bombTransform);
                else
                    commandBuffer.AddComponent(bombEntity, bombTransform);

                BombFuseState fuseState = new BombFuseState
                {
                    Position = request.Position,
                    Velocity = request.Velocity,
                    CollisionRadius = math.max(0.01f, request.CollisionRadius),
                    BounceOnWalls = request.BounceOnWalls,
                    BounceDamping = math.clamp(request.BounceDamping, 0f, 1f),
                    LinearDampingPerSecond = math.max(0f, request.LinearDampingPerSecond),
                    FuseRemaining = math.max(0.05f, request.FuseSeconds),
                    Radius = math.max(0.1f, request.Radius),
                    Damage = math.max(0f, request.Damage),
                    AffectAllEnemiesInRadius = request.AffectAllEnemiesInRadius
                };

                if (entityManager.HasComponent<BombFuseState>(request.BombPrefabEntity))
                    commandBuffer.SetComponent(bombEntity, fuseState);
                else
                    commandBuffer.AddComponent(bombEntity, fuseState);

                if (entityManager.HasComponent<BombExplodeRequest>(request.BombPrefabEntity))
                    commandBuffer.RemoveComponent<BombExplodeRequest>(bombEntity);
            }

            bombRequests.Clear();
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #endregion
}
