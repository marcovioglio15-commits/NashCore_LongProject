using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Synchronizes enemy world-space health and shield bars from ECS runtime data.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(EnemyVisualDistanceCullingSystem))]
public partial struct EnemyWorldSpaceStatusBarsSyncSystem : ISystem
{
    #region Constants
    private const float CameraResolveRetryIntervalSeconds = 0.5f;
    #endregion

    #region Fields
    private EntityQuery enemyQuery;
    private static Camera cachedMainCamera;
    private static Transform cachedMainCameraTransform;
    private static float nextCameraResolveTime;
    private static EnemyWorldSpaceStatusBarsView fallbackTemplateView;
    private static Dictionary<Entity, EnemyWorldSpaceStatusBarsView> fallbackViewsByEnemy;
    private static Stack<EnemyWorldSpaceStatusBarsView> fallbackViewPool;
    private static List<Entity> fallbackReleaseCandidates;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyHealth, EnemyVisualRuntimeState, LocalTransform, EnemyActive, EnemyWorldSpaceStatusBarsRuntimeLink>()
            .Build();

        cachedMainCamera = null;
        cachedMainCameraTransform = null;
        nextCameraResolveTime = 0f;
        fallbackTemplateView = null;
        fallbackViewsByEnemy = new Dictionary<Entity, EnemyWorldSpaceStatusBarsView>(512);
        fallbackViewPool = new Stack<EnemyWorldSpaceStatusBarsView>(256);
        fallbackReleaseCandidates = new List<Entity>(256);
        state.RequireForUpdate(enemyQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        ResolveMainCameraTransform(elapsedTime);
        Transform cameraTransform = cachedMainCameraTransform;
        Camera mainCamera = cachedMainCamera;
        SyncStatusBarsViews(ref state, entityManager, SystemAPI.Time.DeltaTime, cameraTransform, mainCamera);
        ReleaseInactiveFallbackViews(entityManager);
    }

    public void OnDestroy(ref SystemState state)
    {
        DestroyAllFallbackViews();
    }
    #endregion

    #region Helpers
    private static Transform ResolveMainCameraTransform(float elapsedTime)
    {
        if (cachedMainCameraTransform != null)
        {
            return cachedMainCameraTransform;
        }

        if (elapsedTime < nextCameraResolveTime)
        {
            return null;
        }

        nextCameraResolveTime = elapsedTime + CameraResolveRetryIntervalSeconds;
        Camera resolvedCamera = Camera.main;

        if (resolvedCamera == null)
        {
            Camera[] allCameras = Camera.allCameras;

            for (int cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
            {
                Camera camera = allCameras[cameraIndex];

                if (camera == null)
                {
                    continue;
                }

                if (camera.isActiveAndEnabled == false)
                {
                    continue;
                }

                resolvedCamera = camera;
                break;
            }
        }

        cachedMainCamera = resolvedCamera;

        if (cachedMainCamera == null)
        {
            cachedMainCameraTransform = null;
            return null;
        }

        cachedMainCameraTransform = cachedMainCamera.transform;
        return cachedMainCameraTransform;
    }

    private void SyncStatusBarsViews(ref SystemState state,
                                     EntityManager entityManager,
                                     float deltaTime,
                                     Transform mainCameraTransform,
                                     Camera mainCamera)
    {
        if (enemyQuery.IsEmptyIgnoreFilter)
            return;

        foreach ((RefRO<EnemyHealth> enemyHealth,
                  RefRO<EnemyVisualRuntimeState> visualRuntimeState,
                  RefRO<LocalTransform> enemyTransform,
                  RefRW<EnemyWorldSpaceStatusBarsRuntimeLink> runtimeLink,
                  Entity enemyEntity)
                 in SystemAPI.Query<RefRO<EnemyHealth>,
                                    RefRO<EnemyVisualRuntimeState>,
                                    RefRO<LocalTransform>,
                                    RefRW<EnemyWorldSpaceStatusBarsRuntimeLink>>()
                             .WithAll<EnemyActive>()
                             .WithEntityAccess())
        {
            bool enemyActive = entityManager.IsComponentEnabled<EnemyActive>(enemyEntity);

            if (enemyActive == false)
                continue;

            EnemyWorldSpaceStatusBarsRuntimeLink currentRuntimeLink = runtimeLink.ValueRO;
            Entity statusBarsViewEntity = currentRuntimeLink.ViewEntity;
            bool hasValidRuntimeLink = IsValidStatusBarsViewEntity(entityManager, statusBarsViewEntity);

            if (hasValidRuntimeLink == false)
            {
                statusBarsViewEntity = ResolveStatusBarsViewEntity(entityManager, enemyEntity);
                currentRuntimeLink.ViewEntity = statusBarsViewEntity;
                runtimeLink.ValueRW = currentRuntimeLink;
            }

            if (statusBarsViewEntity == Entity.Null)
                continue;

            if (entityManager.Exists(statusBarsViewEntity) == false)
                continue;

            if (entityManager.HasComponent<EnemyWorldSpaceStatusBarsView>(statusBarsViewEntity) == false)
                continue;

            EnemyWorldSpaceStatusBarsView statusBarsView = entityManager.GetComponentObject<EnemyWorldSpaceStatusBarsView>(statusBarsViewEntity);

            if (statusBarsView == null)
                continue;

            statusBarsView = ResolveRuntimeUsableView(enemyEntity, statusBarsView);

            if (statusBarsView == null)
                continue;

            statusBarsView.SyncCanvasCamera(mainCamera);
            float3 enemyPositionFloat3 = enemyTransform.ValueRO.Position;
            Vector3 enemyPosition = new Vector3(enemyPositionFloat3.x, enemyPositionFloat3.y, enemyPositionFloat3.z);
            statusBarsView.SyncWorldPose(enemyPosition, mainCameraTransform);

            EnemyHealth healthState = enemyHealth.ValueRO;
            float normalizedHealth = 0f;

            if (healthState.Max > 0f)
                normalizedHealth = math.clamp(healthState.Current / healthState.Max, 0f, 1f);

            float normalizedShield = 0f;

            if (healthState.MaxShield > 0f)
                normalizedShield = math.clamp(healthState.CurrentShield / healthState.MaxShield, 0f, 1f);

            bool enemyVisible = visualRuntimeState.ValueRO.IsVisible != 0;
            statusBarsView.SyncFromRuntime(normalizedHealth,
                                           normalizedShield,
                                           enemyActive,
                                           enemyVisible,
                                           deltaTime);
        }
    }

    private static bool IsValidStatusBarsViewEntity(EntityManager entityManager, Entity statusBarsViewEntity)
    {
        if (statusBarsViewEntity == Entity.Null)
        {
            return false;
        }

        if (entityManager.Exists(statusBarsViewEntity) == false)
        {
            return false;
        }

        return entityManager.HasComponent<EnemyWorldSpaceStatusBarsView>(statusBarsViewEntity);
    }

    private static Entity ResolveStatusBarsViewEntity(EntityManager entityManager, Entity enemyEntity)
    {
        Entity linkedGroupViewEntity = ResolveStatusBarsViewEntityFromLinkedGroup(entityManager, enemyEntity);

        if (linkedGroupViewEntity != Entity.Null)
        {
            return linkedGroupViewEntity;
        }

        if (entityManager.HasComponent<EnemyWorldSpaceStatusBarsLink>(enemyEntity))
        {
            EnemyWorldSpaceStatusBarsLink authoredLink = entityManager.GetComponentData<EnemyWorldSpaceStatusBarsLink>(enemyEntity);

            if (IsValidStatusBarsViewEntity(entityManager, authoredLink.ViewEntity))
            {
                return authoredLink.ViewEntity;
            }
        }

        if (IsValidStatusBarsViewEntity(entityManager, enemyEntity))
        {
            return enemyEntity;
        }

        return Entity.Null;
    }

    private static Entity ResolveStatusBarsViewEntityFromLinkedGroup(EntityManager entityManager, Entity enemyEntity)
    {
        if (entityManager.HasBuffer<LinkedEntityGroup>(enemyEntity) == false)
        {
            return Entity.Null;
        }

        DynamicBuffer<LinkedEntityGroup> linkedEntities = entityManager.GetBuffer<LinkedEntityGroup>(enemyEntity);

        for (int linkedIndex = 0; linkedIndex < linkedEntities.Length; linkedIndex++)
        {
            Entity linkedEntity = linkedEntities[linkedIndex].Value;

            if (linkedEntity == Entity.Null)
            {
                continue;
            }

            if (entityManager.HasComponent<EnemyWorldSpaceStatusBarsView>(linkedEntity) == false)
            {
                continue;
            }

            return linkedEntity;
        }

        return Entity.Null;
    }

    private static EnemyWorldSpaceStatusBarsView ResolveRuntimeUsableView(Entity enemyEntity, EnemyWorldSpaceStatusBarsView resolvedView)
    {
        if (resolvedView != null)
        {
            GameObject resolvedObject = resolvedView.gameObject;

            if (resolvedObject != null &&
                resolvedObject.scene.IsValid())
            {
                return resolvedView;
            }

            if (fallbackTemplateView == null)
            {
                fallbackTemplateView = resolvedView;
            }
        }

        if (fallbackViewsByEnemy == null)
        {
            fallbackViewsByEnemy = new Dictionary<Entity, EnemyWorldSpaceStatusBarsView>(512);
        }

        if (fallbackViewsByEnemy.TryGetValue(enemyEntity, out EnemyWorldSpaceStatusBarsView fallbackView))
        {
            if (fallbackView != null)
            {
                GameObject fallbackObject = fallbackView.gameObject;

                if (fallbackObject != null &&
                    fallbackObject.activeSelf == false)
                {
                    fallbackObject.SetActive(true);
                }

                return fallbackView;
            }

            fallbackViewsByEnemy.Remove(enemyEntity);
        }

        EnemyWorldSpaceStatusBarsView acquiredView = AcquireFallbackViewInstance();

        if (acquiredView == null)
        {
            return null;
        }

        fallbackViewsByEnemy[enemyEntity] = acquiredView;
        return acquiredView;
    }

    private static EnemyWorldSpaceStatusBarsView AcquireFallbackViewInstance()
    {
        if (fallbackViewPool != null)
        {
            while (fallbackViewPool.Count > 0)
            {
                EnemyWorldSpaceStatusBarsView pooledView = fallbackViewPool.Pop();

                if (pooledView == null)
                {
                    continue;
                }

                GameObject pooledObject = pooledView.gameObject;

                if (pooledObject == null)
                {
                    continue;
                }

                pooledObject.SetActive(true);
                return pooledView;
            }
        }

        if (fallbackTemplateView == null)
        {
            return null;
        }

        GameObject templateObject = fallbackTemplateView.gameObject;

        if (templateObject == null)
        {
            return null;
        }

        GameObject instanceObject = Object.Instantiate(templateObject);
        instanceObject.name = string.Format("{0}_RuntimeClone", templateObject.name);
        instanceObject.SetActive(true);
        return instanceObject.GetComponent<EnemyWorldSpaceStatusBarsView>();
    }

    private static void ReleaseInactiveFallbackViews(EntityManager entityManager)
    {
        if (fallbackViewsByEnemy == null ||
            fallbackViewsByEnemy.Count <= 0)
        {
            return;
        }

        if (fallbackReleaseCandidates == null)
        {
            fallbackReleaseCandidates = new List<Entity>(256);
        }

        fallbackReleaseCandidates.Clear();

        foreach (KeyValuePair<Entity, EnemyWorldSpaceStatusBarsView> pair in fallbackViewsByEnemy)
        {
            Entity enemyEntity = pair.Key;
            bool shouldRelease = false;

            if (entityManager.Exists(enemyEntity) == false)
            {
                shouldRelease = true;
            }
            else if (entityManager.HasComponent<EnemyActive>(enemyEntity) == false)
            {
                shouldRelease = true;
            }
            else if (entityManager.IsComponentEnabled<EnemyActive>(enemyEntity) == false)
            {
                shouldRelease = true;
            }

            if (shouldRelease)
            {
                fallbackReleaseCandidates.Add(enemyEntity);
            }
        }

        for (int releaseIndex = 0; releaseIndex < fallbackReleaseCandidates.Count; releaseIndex++)
        {
            Entity enemyEntity = fallbackReleaseCandidates[releaseIndex];

            if (fallbackViewsByEnemy.TryGetValue(enemyEntity, out EnemyWorldSpaceStatusBarsView fallbackView))
            {
                if (fallbackView != null)
                {
                    GameObject fallbackObject = fallbackView.gameObject;

                    if (fallbackObject != null)
                    {
                        fallbackObject.SetActive(false);
                    }

                    if (fallbackViewPool != null)
                    {
                        fallbackViewPool.Push(fallbackView);
                    }
                }

                fallbackViewsByEnemy.Remove(enemyEntity);
            }
        }
    }

    private static void DestroyAllFallbackViews()
    {
        if (fallbackViewsByEnemy != null)
        {
            foreach (KeyValuePair<Entity, EnemyWorldSpaceStatusBarsView> pair in fallbackViewsByEnemy)
            {
                EnemyWorldSpaceStatusBarsView fallbackView = pair.Value;

                if (fallbackView == null)
                {
                    continue;
                }

                GameObject fallbackObject = fallbackView.gameObject;

                if (fallbackObject != null)
                {
                    Object.Destroy(fallbackObject);
                }
            }

            fallbackViewsByEnemy.Clear();
        }

        if (fallbackViewPool != null)
        {
            while (fallbackViewPool.Count > 0)
            {
                EnemyWorldSpaceStatusBarsView pooledView = fallbackViewPool.Pop();

                if (pooledView == null)
                {
                    continue;
                }

                GameObject pooledObject = pooledView.gameObject;

                if (pooledObject != null)
                {
                    Object.Destroy(pooledObject);
                }
            }
        }

        if (fallbackReleaseCandidates != null)
        {
            fallbackReleaseCandidates.Clear();
        }

        fallbackTemplateView = null;
    }
    #endregion

    #endregion
}
