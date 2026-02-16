using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Spawns optional power-up VFX entities from queued requests.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup))]
[UpdateAfter(typeof(PlayerElementalTrailResolveSystem))]
[UpdateAfter(typeof(PlayerPassiveExplosionResolveSystem))]
public partial struct PlayerPowerUpVfxSpawnSystem : ISystem
{
    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpVfxSpawnRequest>();
        state.RequireForUpdate<PlayerPowerUpVfxCapConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((DynamicBuffer<PlayerPowerUpVfxSpawnRequest> vfxRequests,
                 DynamicBuffer<PlayerPowerUpVfxPoolElement> vfxPool,
                  RefRO<PlayerPowerUpVfxCapConfig> vfxCapConfig)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpVfxSpawnRequest>,
                                    DynamicBuffer<PlayerPowerUpVfxPoolElement>,
                                    RefRO<PlayerPowerUpVfxCapConfig>>())
        {
            if (vfxRequests.Length <= 0)
                continue;

            PlayerPowerUpVfxCapConfig capConfig = SanitizeCapConfig(in vfxCapConfig.ValueRO);
            int estimatedCapacity = math.max(8, vfxPool.Length + vfxRequests.Length);
            NativeParallelHashMap<VfxAreaKey, int> areaCounts = new NativeParallelHashMap<VfxAreaKey, int>(estimatedCapacity, Allocator.Temp);
            NativeParallelHashMap<VfxTargetKey, int> targetCounts = new NativeParallelHashMap<VfxTargetKey, int>(estimatedCapacity, Allocator.Temp);
            NativeParallelHashMap<VfxTargetKey, Entity> targetInstances = new NativeParallelHashMap<VfxTargetKey, Entity>(estimatedCapacity, Allocator.Temp);
            int activeOneShotCount = 0;
            BuildActiveVfxSnapshot(entityManager,
                                   vfxPool,
                                   in capConfig,
                                   ref areaCounts,
                                   ref targetCounts,
                                   ref targetInstances,
                                   ref activeOneShotCount);

            for (int requestIndex = 0; requestIndex < vfxRequests.Length; requestIndex++)
            {
                PlayerPowerUpVfxSpawnRequest request = vfxRequests[requestIndex];

                if (request.PrefabEntity == Entity.Null)
                    continue;

                if (entityManager.Exists(request.PrefabEntity) == false)
                    continue;

                bool hasTargetCapKey = TryBuildTargetKey(in request, out VfxTargetKey targetCapKey);

                if (hasTargetCapKey && capConfig.MaxAttachedSamePrefabPerTarget > 0)
                {
                    int currentTargetCount = 0;
                    targetCounts.TryGetValue(targetCapKey, out currentTargetCount);

                    if (currentTargetCount >= capConfig.MaxAttachedSamePrefabPerTarget)
                    {
                        Entity existingTargetVfxEntity;

                        if (targetInstances.TryGetValue(targetCapKey, out existingTargetVfxEntity))
                        {
                            if (capConfig.RefreshAttachedLifetimeOnCapHit != 0)
                                RefreshExistingLifetime(entityManager, ref commandBuffer, existingTargetVfxEntity, request.LifetimeSeconds);
                        }

                        continue;
                    }
                }

                bool applyAreaCap = request.FollowTargetEntity == Entity.Null && capConfig.MaxSamePrefabPerCell > 0;
                VfxAreaKey areaCapKey = default;

                if (applyAreaCap)
                {
                    areaCapKey = BuildAreaKey(request.PrefabEntity, request.Position, capConfig.CellSize);
                    int areaCount;

                    if (areaCounts.TryGetValue(areaCapKey, out areaCount) && areaCount >= capConfig.MaxSamePrefabPerCell)
                        continue;
                }

                if (capConfig.MaxActiveOneShotVfx > 0 && activeOneShotCount >= capConfig.MaxActiveOneShotVfx)
                    continue;

                bool reusedInstance;
                Entity vfxEntity = PlayerPowerUpVfxPoolUtility.AcquireVfxEntity(entityManager,
                                                                                ref commandBuffer,
                                                                                vfxPool,
                                                                                request.PrefabEntity,
                                                                                out reusedInstance);

                if (vfxEntity == Entity.Null)
                    continue;

                LocalTransform localTransform = LocalTransform.FromPositionRotationScale(request.Position,
                                                                                        request.Rotation,
                                                                                        math.max(0.01f, request.UniformScale));
                SetOrAddComponent(entityManager,
                                  ref commandBuffer,
                                  vfxEntity,
                                  request.PrefabEntity,
                                  reusedInstance,
                                  localTransform);

                PlayerPowerUpVfxLifetime lifetime = new PlayerPowerUpVfxLifetime
                {
                    RemainingSeconds = math.max(0.01f, request.LifetimeSeconds)
                };
                SetOrAddComponent(entityManager,
                                  ref commandBuffer,
                                  vfxEntity,
                                  request.PrefabEntity,
                                  reusedInstance,
                                  lifetime);
                SetOrAddComponent(entityManager,
                                  ref commandBuffer,
                                  vfxEntity,
                                  request.PrefabEntity,
                                  reusedInstance,
                                  new PlayerPowerUpVfxPooled());

                if (request.FollowTargetEntity != Entity.Null)
                {
                    PlayerPowerUpVfxFollowTarget followTarget = new PlayerPowerUpVfxFollowTarget
                    {
                        TargetEntity = request.FollowTargetEntity,
                        PositionOffset = request.FollowPositionOffset,
                        ValidationEntity = request.FollowValidationEntity,
                        ValidationSpawnVersion = request.FollowValidationSpawnVersion
                    };
                    SetOrAddComponent(entityManager,
                                      ref commandBuffer,
                                      vfxEntity,
                                      request.PrefabEntity,
                                      reusedInstance,
                                      followTarget);
                    commandBuffer.RemoveComponent<PlayerPowerUpVfxVelocity>(vfxEntity);
                }
                else
                {
                    float velocitySquaredLength = math.lengthsq(request.Velocity);

                    if (velocitySquaredLength > 1e-6f)
                    {
                        PlayerPowerUpVfxVelocity velocity = new PlayerPowerUpVfxVelocity
                        {
                            Value = request.Velocity
                        };
                        SetOrAddComponent(entityManager,
                                          ref commandBuffer,
                                          vfxEntity,
                                          request.PrefabEntity,
                                          reusedInstance,
                                          velocity);
                        commandBuffer.RemoveComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity);
                    }
                    else
                    {
                        commandBuffer.RemoveComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity);
                        commandBuffer.RemoveComponent<PlayerPowerUpVfxVelocity>(vfxEntity);
                    }
                }

                activeOneShotCount++;

                if (applyAreaCap)
                    IncrementAreaCount(ref areaCounts, in areaCapKey);

                if (hasTargetCapKey && capConfig.MaxAttachedSamePrefabPerTarget > 0)
                {
                    IncrementTargetCount(ref targetCounts, in targetCapKey);

                    Entity existingTargetVfxEntity;

                    if (targetInstances.TryGetValue(targetCapKey, out existingTargetVfxEntity) == false)
                        targetInstances.TryAdd(targetCapKey, vfxEntity);
                }
            }

            vfxRequests.Clear();
            areaCounts.Dispose();
            targetCounts.Dispose();
            targetInstances.Dispose();
        }

        commandBuffer.Playback(entityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #region Helpers
    private static PlayerPowerUpVfxCapConfig SanitizeCapConfig(in PlayerPowerUpVfxCapConfig sourceConfig)
    {
        PlayerPowerUpVfxCapConfig config = sourceConfig;

        if (config.MaxSamePrefabPerCell < 0)
            config.MaxSamePrefabPerCell = 0;

        if (config.CellSize < 0.1f)
            config.CellSize = 0.1f;

        if (config.MaxAttachedSamePrefabPerTarget < 0)
            config.MaxAttachedSamePrefabPerTarget = 0;

        if (config.MaxActiveOneShotVfx < 0)
            config.MaxActiveOneShotVfx = 0;

        return config;
    }

    private static void BuildActiveVfxSnapshot(EntityManager entityManager,
                                               DynamicBuffer<PlayerPowerUpVfxPoolElement> vfxPool,
                                               in PlayerPowerUpVfxCapConfig capConfig,
                                               ref NativeParallelHashMap<VfxAreaKey, int> areaCounts,
                                               ref NativeParallelHashMap<VfxTargetKey, int> targetCounts,
                                               ref NativeParallelHashMap<VfxTargetKey, Entity> targetInstances,
                                               ref int activeOneShotCount)
    {
        for (int poolIndex = 0; poolIndex < vfxPool.Length; poolIndex++)
        {
            PlayerPowerUpVfxPoolElement poolElement = vfxPool[poolIndex];
            Entity vfxEntity = poolElement.VfxEntity;

            if (vfxEntity == Entity.Null || vfxEntity.Index < 0)
                continue;

            if (entityManager.Exists(vfxEntity) == false)
                continue;

            if (entityManager.IsEnabled(vfxEntity) == false)
                continue;

            if (entityManager.HasComponent<PlayerPowerUpVfxLifetime>(vfxEntity) == false)
                continue;

            activeOneShotCount++;

            if (capConfig.MaxSamePrefabPerCell > 0 &&
                entityManager.HasComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity) == false &&
                entityManager.HasComponent<LocalTransform>(vfxEntity))
            {
                LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(vfxEntity);
                VfxAreaKey areaKey = BuildAreaKey(poolElement.PrefabEntity, localTransform.Position, capConfig.CellSize);
                IncrementAreaCount(ref areaCounts, in areaKey);
            }

            if (capConfig.MaxAttachedSamePrefabPerTarget > 0 &&
                entityManager.HasComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity))
            {
                PlayerPowerUpVfxFollowTarget followTarget = entityManager.GetComponentData<PlayerPowerUpVfxFollowTarget>(vfxEntity);

                if (followTarget.ValidationEntity == Entity.Null || followTarget.ValidationSpawnVersion == 0u)
                    continue;

                VfxTargetKey targetKey = new VfxTargetKey
                {
                    PrefabEntity = poolElement.PrefabEntity,
                    ValidationEntity = followTarget.ValidationEntity,
                    ValidationSpawnVersion = followTarget.ValidationSpawnVersion
                };

                IncrementTargetCount(ref targetCounts, in targetKey);

                Entity existingTargetVfxEntity;

                if (targetInstances.TryGetValue(targetKey, out existingTargetVfxEntity) == false)
                    targetInstances.TryAdd(targetKey, vfxEntity);
            }
        }
    }

    private static bool TryBuildTargetKey(in PlayerPowerUpVfxSpawnRequest request, out VfxTargetKey targetKey)
    {
        targetKey = default;

        if (request.FollowTargetEntity == Entity.Null)
            return false;

        if (request.FollowValidationEntity == Entity.Null)
            return false;

        if (request.FollowValidationSpawnVersion == 0u)
            return false;

        targetKey = new VfxTargetKey
        {
            PrefabEntity = request.PrefabEntity,
            ValidationEntity = request.FollowValidationEntity,
            ValidationSpawnVersion = request.FollowValidationSpawnVersion
        };
        return true;
    }

    private static VfxAreaKey BuildAreaKey(Entity prefabEntity, float3 position, float cellSize)
    {
        float inverseCellSize = 1f / math.max(0.1f, cellSize);
        int cellX = (int)math.floor(position.x * inverseCellSize);
        int cellY = (int)math.floor(position.z * inverseCellSize);

        return new VfxAreaKey
        {
            PrefabEntity = prefabEntity,
            CellX = cellX,
            CellY = cellY
        };
    }

    private static void IncrementAreaCount(ref NativeParallelHashMap<VfxAreaKey, int> areaCounts, in VfxAreaKey areaKey)
    {
        int currentCount;

        if (areaCounts.TryGetValue(areaKey, out currentCount))
        {
            areaCounts[areaKey] = currentCount + 1;
            return;
        }

        areaCounts.TryAdd(areaKey, 1);
    }

    private static void IncrementTargetCount(ref NativeParallelHashMap<VfxTargetKey, int> targetCounts, in VfxTargetKey targetKey)
    {
        int currentCount;

        if (targetCounts.TryGetValue(targetKey, out currentCount))
        {
            targetCounts[targetKey] = currentCount + 1;
            return;
        }

        targetCounts.TryAdd(targetKey, 1);
    }

    private static void RefreshExistingLifetime(EntityManager entityManager,
                                                ref EntityCommandBuffer commandBuffer,
                                                Entity vfxEntity,
                                                float requestedLifetimeSeconds)
    {
        if (vfxEntity == Entity.Null || vfxEntity.Index < 0)
            return;

        if (entityManager.Exists(vfxEntity) == false)
            return;

        if (entityManager.HasComponent<PlayerPowerUpVfxLifetime>(vfxEntity) == false)
            return;

        PlayerPowerUpVfxLifetime currentLifetime = entityManager.GetComponentData<PlayerPowerUpVfxLifetime>(vfxEntity);
        float desiredLifetime = math.max(0.01f, requestedLifetimeSeconds);

        if (desiredLifetime <= currentLifetime.RemainingSeconds)
            return;

        commandBuffer.SetComponent(vfxEntity, new PlayerPowerUpVfxLifetime
        {
            RemainingSeconds = desiredLifetime
        });
    }

    private static void SetOrAddComponent<TComponent>(EntityManager entityManager,
                                                      ref EntityCommandBuffer commandBuffer,
                                                      Entity entity,
                                                      Entity prefabEntity,
                                                      bool entityExistsNow,
                                                      in TComponent component)
        where TComponent : unmanaged, IComponentData
    {
        bool hasComponent;

        if (entityExistsNow)
            hasComponent = entityManager.HasComponent<TComponent>(entity);
        else
            hasComponent = entityManager.HasComponent<TComponent>(prefabEntity);

        if (hasComponent)
            commandBuffer.SetComponent(entity, component);
        else
            commandBuffer.AddComponent(entity, component);
    }
    #endregion

    #region Key Types
    private struct VfxAreaKey : System.IEquatable<VfxAreaKey>
    {
        public Entity PrefabEntity;
        public int CellX;
        public int CellY;

        public bool Equals(VfxAreaKey other)
        {
            return PrefabEntity.Equals(other.PrefabEntity) &&
                   CellX == other.CellX &&
                   CellY == other.CellY;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PrefabEntity.Index;
                hash = (hash * 397) ^ PrefabEntity.Version;
                hash = (hash * 397) ^ CellX;
                hash = (hash * 397) ^ CellY;
                return hash;
            }
        }
    }

    private struct VfxTargetKey : System.IEquatable<VfxTargetKey>
    {
        public Entity PrefabEntity;
        public Entity ValidationEntity;
        public uint ValidationSpawnVersion;

        public bool Equals(VfxTargetKey other)
        {
            return PrefabEntity.Equals(other.PrefabEntity) &&
                   ValidationEntity.Equals(other.ValidationEntity) &&
                   ValidationSpawnVersion == other.ValidationSpawnVersion;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PrefabEntity.Index;
                hash = (hash * 397) ^ PrefabEntity.Version;
                hash = (hash * 397) ^ ValidationEntity.Index;
                hash = (hash * 397) ^ ValidationEntity.Version;
                hash = (hash * 397) ^ (int)ValidationSpawnVersion;
                return hash;
            }
        }
    }
    #endregion

    #endregion
}
