using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Maintains one managed attached TrailRenderer VFX instance per player while Elemental Trail passive is enabled.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerPowerUpsInitializeSystem))]
[UpdateAfter(typeof(PlayerMovementApplySystem))]
public partial struct PlayerElementalTrailAttachedVfxSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<Entity, ManagedTrailVfxInstance> managedInstances = new Dictionary<Entity, ManagedTrailVfxInstance>(4);
    private static readonly List<Entity> invalidOwnerEntities = new List<Entity>(8);
    #if UNITY_EDITOR
    private static readonly HashSet<int> missingTrailRendererLogCache = new HashSet<int>();
    #endif
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<PlayerElementalTrailAttachedVfxState>();
        state.RequireForUpdate<LocalTransform>();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (managedInstances.Count <= 0)
            return;

        Dictionary<Entity, ManagedTrailVfxInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
            DestroyManagedInstance(enumerator.Current.Value);

        enumerator.Dispose();
        managedInstances.Clear();
        invalidOwnerEntities.Clear();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();
        EntityManager entityManager = state.EntityManager;
        CleanupInvalidOwnerInstances(entityManager);

        foreach ((RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRO<LocalTransform> playerTransform,
                  RefRW<PlayerElementalTrailAttachedVfxState> trailAttachedVfxState,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerPassiveToolsState>,
                                    RefRO<LocalTransform>,
                                    RefRW<PlayerElementalTrailAttachedVfxState>>()
                             .WithEntityAccess())
        {
            PlayerElementalTrailAttachedVfxState previousTrailState = trailAttachedVfxState.ValueRO;
            ReleaseLegacyPooledTrailEntityIfAny(entityManager, previousTrailState.VfxEntity);

            GameObject trailPrefab = ResolveTrailPrefab(entityManager, playerEntity);
            bool shouldBeActive = passiveToolsState.ValueRO.HasElementalTrail != 0 && trailPrefab != null;

            if (shouldBeActive == false)
            {
                SetManagedInstanceActive(playerEntity, false, float3.zero, 1f, 0.02f);
                trailAttachedVfxState.ValueRW = default;
                continue;
            }

            ManagedTrailVfxInstance managedInstance = GetOrCreateManagedInstance(playerEntity, trailPrefab);

            if (managedInstance == null || managedInstance.InstanceObject == null)
            {
                trailAttachedVfxState.ValueRW = default;
                continue;
            }

            ElementalTrailPassiveConfig trailConfig = passiveToolsState.ValueRO.ElementalTrail;
            float radius = math.max(0.05f, trailConfig.TrailRadius);
            float widthMultiplier = math.max(0.01f, trailConfig.TrailAttachedVfxScaleMultiplier);
            float desiredTrailWidth = math.max(0.02f, radius * 2f * widthMultiplier);
            float3 desiredPosition = playerTransform.ValueRO.Position + trailConfig.TrailAttachedVfxOffset;

            SetManagedInstanceActive(playerEntity, true, desiredPosition, 1f, desiredTrailWidth);
            trailAttachedVfxState.ValueRW = default;
        }
    }
    #endregion

    #region Helpers
    private static GameObject ResolveTrailPrefab(EntityManager entityManager, Entity playerEntity)
    {
        if (entityManager.HasComponent<PlayerElementalTrailAttachedVfxPrefabReference>(playerEntity) == false)
            return null;

        PlayerElementalTrailAttachedVfxPrefabReference prefabReference = entityManager.GetComponentObject<PlayerElementalTrailAttachedVfxPrefabReference>(playerEntity);

        if (prefabReference == null)
            return null;

        return prefabReference.Prefab;
    }

    private static ManagedTrailVfxInstance GetOrCreateManagedInstance(Entity playerEntity, GameObject trailPrefab)
    {
        ManagedTrailVfxInstance managedInstance;

        if (managedInstances.TryGetValue(playerEntity, out managedInstance))
        {
            bool requiresRebuild = managedInstance == null ||
                                   managedInstance.InstanceObject == null ||
                                   managedInstance.SourcePrefab != trailPrefab;

            if (requiresRebuild == false)
                return managedInstance;

            DestroyManagedInstance(managedInstance);
            managedInstances.Remove(playerEntity);
        }

        if (trailPrefab == null)
            return null;

        GameObject instanceObject = Object.Instantiate(trailPrefab);

        if (instanceObject == null)
            return null;

        instanceObject.name = string.Format("{0}_ElementalTrail", trailPrefab.name);
        TrailRenderer[] trailRenderers = instanceObject.GetComponentsInChildren<TrailRenderer>(true);
        managedInstance = new ManagedTrailVfxInstance
        {
            SourcePrefab = trailPrefab,
            InstanceObject = instanceObject,
            TrailRenderers = trailRenderers,
            VisualCenterOffset = ResolveVisualCenterOffset(instanceObject.transform, trailRenderers)
        };
        managedInstances[playerEntity] = managedInstance;

    #if UNITY_EDITOR
        if ((trailRenderers == null || trailRenderers.Length <= 0) && missingTrailRendererLogCache.Add(playerEntity.Index))
        {
            Debug.LogWarning(string.Format("[ElementalTrailVfx] Prefab '{0}' has no TrailRenderer in children. Attached trail will be invisible.", trailPrefab.name));
        }
    #endif

        return managedInstance;
    }

    private static void SetManagedInstanceActive(Entity playerEntity,
                                                 bool isActive,
                                                 float3 worldPosition,
                                                 float uniformScale,
                                                 float desiredTrailWidth)
    {
        ManagedTrailVfxInstance managedInstance;

        if (managedInstances.TryGetValue(playerEntity, out managedInstance) == false)
            return;

        if (managedInstance == null || managedInstance.InstanceObject == null)
            return;

        if (isActive == false)
        {
            ApplyTrailRenderersState(managedInstance, false, 0f);

            if (managedInstance.InstanceObject.activeSelf)
                managedInstance.InstanceObject.SetActive(false);

            return;
        }

        Transform instanceTransform = managedInstance.InstanceObject.transform;
        float3 visualCenterOffset = managedInstance.VisualCenterOffset;
        instanceTransform.position = new Vector3(worldPosition.x - visualCenterOffset.x,
                                                 worldPosition.y - visualCenterOffset.y,
                                                 worldPosition.z - visualCenterOffset.z);
        instanceTransform.rotation = Quaternion.identity;
        instanceTransform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);

        if (managedInstance.InstanceObject.activeSelf == false)
            managedInstance.InstanceObject.SetActive(true);

        ApplyTrailRenderersState(managedInstance, true, desiredTrailWidth);
    }

    private static void ApplyTrailRenderersState(ManagedTrailVfxInstance managedInstance, bool isEmitting, float desiredTrailWidth)
    {
        if (managedInstance == null || managedInstance.TrailRenderers == null || managedInstance.TrailRenderers.Length <= 0)
            return;

        for (int rendererIndex = 0; rendererIndex < managedInstance.TrailRenderers.Length; rendererIndex++)
        {
            TrailRenderer trailRenderer = managedInstance.TrailRenderers[rendererIndex];

            if (trailRenderer == null)
                continue;

            if (isEmitting && trailRenderer.enabled == false)
                trailRenderer.Clear();

            trailRenderer.enabled = isEmitting;
            trailRenderer.emitting = isEmitting;

            if (isEmitting)
                trailRenderer.widthMultiplier = math.max(0.01f, desiredTrailWidth);
            else
                trailRenderer.Clear();
        }
    }

    private static float3 ResolveVisualCenterOffset(Transform rootTransform, TrailRenderer[] trailRenderers)
    {
        if (rootTransform == null || trailRenderers == null || trailRenderers.Length <= 0)
            return float3.zero;

        float3 accumulatedOffset = float3.zero;
        int validRendererCount = 0;

        for (int rendererIndex = 0; rendererIndex < trailRenderers.Length; rendererIndex++)
        {
            TrailRenderer trailRenderer = trailRenderers[rendererIndex];

            if (trailRenderer == null)
                continue;

            Vector3 localPosition = rootTransform.InverseTransformPoint(trailRenderer.transform.position);
            accumulatedOffset += new float3(localPosition.x, localPosition.y, localPosition.z);
            validRendererCount++;
        }

        if (validRendererCount <= 0)
            return float3.zero;

        return accumulatedOffset / validRendererCount;
    }

    private static void CleanupInvalidOwnerInstances(EntityManager entityManager)
    {
        if (managedInstances.Count <= 0)
            return;

        invalidOwnerEntities.Clear();
        Dictionary<Entity, ManagedTrailVfxInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Entity ownerEntity = enumerator.Current.Key;

            if (IsValidEntity(entityManager, ownerEntity))
                continue;

            DestroyManagedInstance(enumerator.Current.Value);
            invalidOwnerEntities.Add(ownerEntity);
        }

        enumerator.Dispose();

        for (int index = 0; index < invalidOwnerEntities.Count; index++)
            managedInstances.Remove(invalidOwnerEntities[index]);

        invalidOwnerEntities.Clear();
    }

    private static void DestroyManagedInstance(ManagedTrailVfxInstance managedInstance)
    {
        if (managedInstance == null || managedInstance.InstanceObject == null)
            return;

        Object.Destroy(managedInstance.InstanceObject);
        managedInstance.InstanceObject = null;
        managedInstance.TrailRenderers = null;
        managedInstance.SourcePrefab = null;
    }

    private static void ReleaseLegacyPooledTrailEntityIfAny(EntityManager entityManager, Entity vfxEntity)
    {
        if (IsValidEntity(entityManager, vfxEntity) == false)
            return;

        if (entityManager.HasComponent<PlayerPowerUpVfxLifetime>(vfxEntity))
            entityManager.RemoveComponent<PlayerPowerUpVfxLifetime>(vfxEntity);

        if (entityManager.HasComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity))
            entityManager.RemoveComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity);

        if (entityManager.HasComponent<PlayerPowerUpVfxVelocity>(vfxEntity))
            entityManager.RemoveComponent<PlayerPowerUpVfxVelocity>(vfxEntity);

        if (entityManager.IsEnabled(vfxEntity))
            entityManager.SetEnabled(vfxEntity, false);
    }

    private static bool IsValidEntity(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null)
            return false;

        if (entity.Index < 0)
            return false;

        if (entityManager.Exists(entity) == false)
            return false;

        return true;
    }
    #endregion

    #region Nested Types
    private sealed class ManagedTrailVfxInstance
    {
        public GameObject SourcePrefab;
        public GameObject InstanceObject;
        public TrailRenderer[] TrailRenderers;
        public float3 VisualCenterOffset;
    }
    #endregion

    #endregion
}
