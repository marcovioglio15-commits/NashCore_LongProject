using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Maintains pooled 3D body blobs and particle endpoints for the Laser Beam presentation path.
/// /params None.
/// /returns None.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLaserBeamSimulationSystem))]
public partial struct PlayerLaserBeamPresentationSystem : ISystem
{
    #region Fields
    private static readonly Dictionary<Entity, PlayerLaserBeamManagedInstance> managedInstances = new Dictionary<Entity, PlayerLaserBeamManagedInstance>(4);
    private static readonly List<Entity> invalidOwnerEntities = new List<Entity>(8);
    private static readonly List<PlayerLaserBeamBodySample> bodySamples = new List<PlayerLaserBeamBodySample>(128);
    private static readonly List<PlayerLaserBeamLaneEndpoint> laneEndpoints = new List<PlayerLaserBeamLaneEndpoint>(16);
#if UNITY_EDITOR
    private static readonly HashSet<int> missingVisualRigLogCache = new HashSet<int>();
#endif
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime data required by the Laser Beam presentation path.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerLaserBeamState>();
        state.RequireForUpdate<PlayerLaserBeamLaneElement>();
        state.RequireForUpdate<PlayerPassiveToolsState>();
        state.RequireForUpdate<PlayerLaserBeamVisualConfig>();
        state.RequireForUpdate<PlayerLaserBeamBodyVariantElement>();
        state.RequireForUpdate<PlayerLaserBeamSourceVariantElement>();
        state.RequireForUpdate<PlayerLaserBeamImpactVariantElement>();
        state.RequireForUpdate<PlayerLaserBeamPaletteElement>();
    }

    /// <summary>
    /// Releases all pooled managed visuals owned by the presentation system.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnDestroy(ref SystemState state)
    {
        Dictionary<Entity, PlayerLaserBeamManagedInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
            PlayerLaserBeamPresentationRuntimeUtility.DestroyManagedInstance(enumerator.Current.Value);

        enumerator.Dispose();
        managedInstances.Clear();
        invalidOwnerEntities.Clear();
        bodySamples.Clear();
        laneEndpoints.Clear();
#if UNITY_EDITOR
        missingVisualRigLogCache.Clear();
#endif
    }

    /// <summary>
    /// Synchronizes pooled managed visuals with the current authoritative Laser Beam lane buffer.
    /// /params state Mutable system state.
    /// /returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();
        PlayerLaserBeamPresentationRuntimeUtility.CleanupInvalidOwnerInstances(state.EntityManager,
                                                                               managedInstances,
                                                                               invalidOwnerEntities);
        BufferLookup<PlayerLaserBeamBodyVariantElement> bodyVariantLookup = SystemAPI.GetBufferLookup<PlayerLaserBeamBodyVariantElement>(true);
        BufferLookup<PlayerLaserBeamSourceVariantElement> sourceVariantLookup = SystemAPI.GetBufferLookup<PlayerLaserBeamSourceVariantElement>(true);
        BufferLookup<PlayerLaserBeamImpactVariantElement> impactVariantLookup = SystemAPI.GetBufferLookup<PlayerLaserBeamImpactVariantElement>(true);
        BufferLookup<PlayerLaserBeamPaletteElement> paletteLookup = SystemAPI.GetBufferLookup<PlayerLaserBeamPaletteElement>(true);

        foreach ((RefRO<PlayerPassiveToolsState> passiveToolsState,
                  RefRO<PlayerLaserBeamState> laserBeamState,
                  DynamicBuffer<PlayerLaserBeamLaneElement> laserBeamLanes,
                  RefRO<PlayerLaserBeamVisualConfig> visualConfig,
                  Entity playerEntity)
                 in SystemAPI.Query<RefRO<PlayerPassiveToolsState>,
                                    RefRO<PlayerLaserBeamState>,
                                    DynamicBuffer<PlayerLaserBeamLaneElement>,
                                    RefRO<PlayerLaserBeamVisualConfig>>()
                             .WithEntityAccess())
        {
            if (!bodyVariantLookup.HasBuffer(playerEntity) ||
                !sourceVariantLookup.HasBuffer(playerEntity) ||
                !impactVariantLookup.HasBuffer(playerEntity) ||
                !paletteLookup.HasBuffer(playerEntity))
            {
                PlayerLaserBeamPresentationRuntimeUtility.DisableManagedInstance(playerEntity, managedInstances);
                continue;
            }

            DynamicBuffer<PlayerLaserBeamBodyVariantElement> bodyVariants = bodyVariantLookup[playerEntity];
            DynamicBuffer<PlayerLaserBeamSourceVariantElement> sourceVariants = sourceVariantLookup[playerEntity];
            DynamicBuffer<PlayerLaserBeamImpactVariantElement> impactVariants = impactVariantLookup[playerEntity];
            DynamicBuffer<PlayerLaserBeamPaletteElement> paletteBuffer = paletteLookup[playerEntity];
            bool shouldRender = passiveToolsState.ValueRO.HasLaserBeam != 0 &&
                                laserBeamState.ValueRO.IsActive != 0 &&
                                laserBeamLanes.Length > 0;

            if (!shouldRender)
            {
                PlayerLaserBeamPresentationRuntimeUtility.DisableManagedInstance(playerEntity, managedInstances);
                continue;
            }

            LaserBeamPassiveConfig laserBeamConfig = passiveToolsState.ValueRO.LaserBeam;
            GameObject bodyPrefab = PlayerLaserBeamPresentationRuntimeGeometryUtility.ResolveBodyPrefab(bodyVariants, laserBeamConfig.BodyProfile);
            GameObject sourcePrefab = PlayerLaserBeamPresentationRuntimeGeometryUtility.ResolveSourcePrefab(sourceVariants, laserBeamConfig.SourceShape);
            GameObject impactPrefab = PlayerLaserBeamPresentationRuntimeGeometryUtility.ResolveImpactPrefab(impactVariants, laserBeamConfig.ImpactShape);

            if (bodyPrefab == null || sourcePrefab == null || impactPrefab == null)
            {
#if UNITY_EDITOR
                if (missingVisualRigLogCache.Add(playerEntity.Index))
                    Debug.LogWarning("[PlayerLaserBeamPresentationSystem] Laser Beam rig prefabs are missing on the active runtime visual bridge prefab. Assign PlayerLaserBeamVisualRigAuthoring variants on the visual bridge asset.");
#endif
                PlayerLaserBeamPresentationRuntimeUtility.DisableManagedInstance(playerEntity, managedInstances);
                continue;
            }

            if (!PlayerLaserBeamPresentationRuntimeGeometryUtility.BuildLaneVisualData(laserBeamLanes,
                                                                                       in visualConfig.ValueRO,
                                                                                       bodySamples,
                                                                                       laneEndpoints))
            {
                PlayerLaserBeamPresentationRuntimeUtility.DisableManagedInstance(playerEntity, managedInstances);
                continue;
            }

            PlayerLaserBeamManagedInstance managedInstance = PlayerLaserBeamPresentationRuntimeUtility.GetOrCreateManagedInstance(playerEntity,
                                                                                                                                managedInstances);

            if (managedInstance == null || managedInstance.RootObject == null)
                continue;

            if (!managedInstance.RootObject.activeSelf)
                managedInstance.RootObject.SetActive(true);

            PlayerLaserBeamResolvedPalette palette = PlayerLaserBeamPresentationRuntimeGeometryUtility.ResolvePalette(laserBeamConfig.VisualPalette,
                                                                                                                       paletteBuffer);
            Material bodyMaterial = visualConfig.ValueRO.BodyMaterial.Value;
            Material sourceMaterial = visualConfig.ValueRO.SourceBubbleMaterial.Value;
            Material impactMaterial = visualConfig.ValueRO.ImpactSplashMaterial.Value;
            PlayerLaserBeamPresentationRuntimeUtility.EnsureBodyVisualCount(managedInstance, bodySamples.Count, bodyPrefab);
            PlayerLaserBeamPresentationRuntimeUtility.EnsureParticleVisualCount(managedInstance.SourceVisuals,
                                                                                laneEndpoints.Count,
                                                                                sourcePrefab,
                                                                                managedInstance.RootTransform,
                                                                                "LaserBeamSource");
            PlayerLaserBeamPresentationRuntimeUtility.EnsureParticleVisualCount(managedInstance.ImpactVisuals,
                                                                                laneEndpoints.Count,
                                                                                impactPrefab,
                                                                                managedInstance.RootTransform,
                                                                                "LaserBeamImpact");

            // Push body blob transforms and liquid shader properties.
            for (int sampleIndex = 0; sampleIndex < bodySamples.Count; sampleIndex++)
            {
                PlayerLaserBeamManagedBodyVisual bodyVisual = managedInstance.BodyVisuals[sampleIndex];
                PlayerLaserBeamBodySample sample = bodySamples[sampleIndex];
                PlayerLaserBeamPresentationRuntimeRenderUtility.ApplyBodyVisual(bodyVisual,
                                                                                in sample,
                                                                                in visualConfig.ValueRO,
                                                                                in laserBeamConfig,
                                                                                in palette,
                                                                                bodyMaterial);
            }

            // Update the origin bubble and terminal splash for each active lane.
            for (int laneIndex = 0; laneIndex < laneEndpoints.Count; laneIndex++)
            {
                PlayerLaserBeamManagedParticleVisual sourceVisual = managedInstance.SourceVisuals[laneIndex];
                PlayerLaserBeamManagedParticleVisual impactVisual = managedInstance.ImpactVisuals[laneIndex];
                PlayerLaserBeamLaneEndpoint endpoint = laneEndpoints[laneIndex];
                PlayerLaserBeamPresentationRuntimeRenderUtility.ApplyParticleVisual(sourceVisual,
                                                                                    in endpoint,
                                                                                    in visualConfig.ValueRO,
                                                                                    in laserBeamConfig,
                                                                                    in palette,
                                                                                    sourceMaterial,
                                                                                    laserBeamConfig.SourceShape,
                                                                                    false);
                PlayerLaserBeamPresentationRuntimeRenderUtility.ApplyParticleVisual(impactVisual,
                                                                                    in endpoint,
                                                                                    in visualConfig.ValueRO,
                                                                                    in laserBeamConfig,
                                                                                    in palette,
                                                                                    impactMaterial,
                                                                                    laserBeamConfig.ImpactShape,
                                                                                    true);
            }
        }
    }
    #endregion

    #endregion
}
