using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Owns pooled managed Laser Beam body and particle instances for the 3D presentation path.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerLaserBeamPresentationRuntimeUtility
{
    #region Constants
    private const int LaserBeamVisualSortingOrder = 12;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Removes pooled managed instances owned by destroyed ECS entities.
    /// /params entityManager World entity manager used to validate owner entities.
    /// /params managedInstances Mutable runtime instance dictionary.
    /// /params invalidOwnerEntities Reusable list that receives dead owners before cleanup.
    /// /returns None.
    /// </summary>
    public static void CleanupInvalidOwnerInstances(EntityManager entityManager,
                                                    Dictionary<Entity, PlayerLaserBeamManagedInstance> managedInstances,
                                                    List<Entity> invalidOwnerEntities)
    {
        if (managedInstances.Count <= 0)
            return;

        invalidOwnerEntities.Clear();
        Dictionary<Entity, PlayerLaserBeamManagedInstance>.Enumerator enumerator = managedInstances.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Entity ownerEntity = enumerator.Current.Key;
            PlayerLaserBeamManagedInstance managedInstance = enumerator.Current.Value;

            if (entityManager.Exists(ownerEntity) &&
                managedInstance != null &&
                managedInstance.RootObject != null)
            {
                continue;
            }

            invalidOwnerEntities.Add(ownerEntity);
        }

        enumerator.Dispose();

        for (int invalidIndex = 0; invalidIndex < invalidOwnerEntities.Count; invalidIndex++)
        {
            Entity invalidOwnerEntity = invalidOwnerEntities[invalidIndex];
            PlayerLaserBeamManagedInstance managedInstance;

            if (!managedInstances.TryGetValue(invalidOwnerEntity, out managedInstance))
                continue;

            DestroyManagedInstance(managedInstance);
            managedInstances.Remove(invalidOwnerEntity);
        }
    }

    /// <summary>
    /// Creates or reuses the pooled managed instance that owns all visuals for one player beam.
    /// /params playerEntity Owner entity used to key the pooled dictionary.
    /// /params managedInstances Mutable runtime instance dictionary.
    /// /returns Managed instance ready for rendering.
    /// </summary>
    public static PlayerLaserBeamManagedInstance GetOrCreateManagedInstance(Entity playerEntity,
                                                                            Dictionary<Entity, PlayerLaserBeamManagedInstance> managedInstances)
    {
        PlayerLaserBeamManagedInstance managedInstance;

        if (managedInstances.TryGetValue(playerEntity, out managedInstance))
        {
            if (managedInstance != null && managedInstance.RootObject != null)
                return managedInstance;

            DestroyManagedInstance(managedInstance);
            managedInstances.Remove(playerEntity);
        }

        GameObject rootObject = new GameObject(string.Format("PlayerLaserBeam3D_{0}", playerEntity.Index));
        PlayerLaserBeamManagedInstance createdInstance = new PlayerLaserBeamManagedInstance
        {
            RootObject = rootObject,
            RootTransform = rootObject.transform
        };
        managedInstances[playerEntity] = createdInstance;
        return createdInstance;
    }

    /// <summary>
    /// Hides one pooled beam instance without destroying its owned visuals.
    /// /params playerEntity Owner entity used to resolve the pooled instance.
    /// /params managedInstances Runtime instance dictionary.
    /// /returns None.
    /// </summary>
    public static void DisableManagedInstance(Entity playerEntity,
                                              Dictionary<Entity, PlayerLaserBeamManagedInstance> managedInstances)
    {
        PlayerLaserBeamManagedInstance managedInstance;

        if (!managedInstances.TryGetValue(playerEntity, out managedInstance))
            return;

        if (managedInstance == null || managedInstance.RootObject == null)
            return;

        StopParticleVisuals(managedInstance.SourceVisuals);
        StopParticleVisuals(managedInstance.TerminalCapVisuals);
        StopParticleVisuals(managedInstance.ContactFlareVisuals);

        if (managedInstance.RootObject.activeSelf)
            managedInstance.RootObject.SetActive(false);
    }

    /// <summary>
    /// Destroys one pooled managed instance and all visuals owned by it.
    /// /params managedInstance Instance to destroy.
    /// /returns None.
    /// </summary>
    public static void DestroyManagedInstance(PlayerLaserBeamManagedInstance managedInstance)
    {
        if (managedInstance == null)
            return;

        DestroyBodyVisuals(managedInstance.BodyVisuals);
        DestroyParticleVisuals(managedInstance.SourceVisuals);
        DestroyParticleVisuals(managedInstance.TerminalCapVisuals);
        DestroyParticleVisuals(managedInstance.ContactFlareVisuals);

        if (managedInstance.RootObject != null)
            Object.Destroy(managedInstance.RootObject);
    }

    /// <summary>
    /// Ensures the pooled body visual list can render the requested lane count.
    /// /params managedInstance Instance owning the pooled visuals.
    /// /params requiredCount Number of lane visuals required this frame.
    /// /returns None.
    /// </summary>
    public static void EnsureBodyVisualCount(PlayerLaserBeamManagedInstance managedInstance,
                                             int requiredCount)
    {
        if (managedInstance == null || managedInstance.RootTransform == null)
            return;

        if (requiredCount < 0)
            requiredCount = 0;

        while (managedInstance.BodyVisuals.Count < requiredCount)
            managedInstance.BodyVisuals.Add(CreateBodyVisual(managedInstance.RootTransform, managedInstance.BodyVisuals.Count));

        for (int visualIndex = requiredCount; visualIndex < managedInstance.BodyVisuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedBodyVisual visual = managedInstance.BodyVisuals[visualIndex];

            if (visual == null || visual.InstanceObject == null)
                continue;

            if (visual.InstanceObject.activeSelf)
                visual.InstanceObject.SetActive(false);
        }
    }

    /// <summary>
    /// Ensures the pooled particle visual list can render the requested lane count using the selected prefab.
    /// /params visuals Mutable pooled visual list.
    /// /params requiredCount Number of lane endpoint visuals required this frame.
    /// /params prefab Resolved particle prefab that should back the pooled visuals.
    /// /params parentTransform Parent transform that receives new pooled instances.
    /// /params label Prefix used when renaming created instances.
    /// /returns None.
    /// </summary>
    public static void EnsureParticleVisualCount(List<PlayerLaserBeamManagedParticleVisual> visuals,
                                                 int requiredCount,
                                                 GameObject prefab,
                                                 Transform parentTransform,
                                                 string label)
    {
        if (visuals == null || parentTransform == null)
            return;

        if (requiredCount < 0)
            requiredCount = 0;

        EnsureParticleVisualCapacity(visuals, requiredCount, prefab, parentTransform, label);

        for (int visualIndex = requiredCount; visualIndex < visuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedParticleVisual visual = visuals[visualIndex];

            if (visual == null || visual.InstanceObject == null)
                continue;

            StopParticleVisual(visual);

            if (visual.InstanceObject.activeSelf)
                visual.InstanceObject.SetActive(false);
        }
    }

    /// <summary>
    /// Restarts all particle systems owned by one pooled particle visual.
    /// /params visual Pooled particle visual to restart.
    /// /returns None.
    /// </summary>
    public static void RestartParticleVisual(PlayerLaserBeamManagedParticleVisual visual)
    {
        if (visual == null || visual.ParticleSystems == null)
            return;

        for (int particleIndex = 0; particleIndex < visual.ParticleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = visual.ParticleSystems[particleIndex];

            if (particleSystem == null)
                continue;

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Ensures the particle visual pool matches the required count and prefab source.
    /// /params visuals Mutable pooled particle visual list.
    /// /params requiredCount Required number of pooled particle visuals.
    /// /params prefab Prefab that should back every pooled particle visual.
    /// /params parentTransform Parent transform that receives new pooled instances.
    /// /params label Prefix used when renaming created instances.
    /// /returns None.
    /// </summary>
    private static void EnsureParticleVisualCapacity(List<PlayerLaserBeamManagedParticleVisual> visuals,
                                                     int requiredCount,
                                                     GameObject prefab,
                                                     Transform parentTransform,
                                                     string label)
    {
        while (visuals.Count < requiredCount)
        {
            PlayerLaserBeamManagedParticleVisual createdVisual = CreateParticleVisual(prefab, parentTransform, label);
            visuals.Add(createdVisual);
        }

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedParticleVisual visual = visuals[visualIndex];

            if (visual != null &&
                visual.InstanceObject != null &&
                visual.SourcePrefab == prefab)
            {
                continue;
            }

            DestroyParticleVisual(visual);
            visuals[visualIndex] = CreateParticleVisual(prefab, parentTransform, label);
        }
    }

    /// <summary>
    /// Creates one pooled ribbon body visual backed by a dedicated dynamic mesh.
    /// /params parentTransform Parent transform that receives the pooled instance.
    /// /params visualIndex Stable index used to name the created GameObject.
    /// /returns Newly created pooled body visual, or null when creation fails.
    /// </summary>
    private static PlayerLaserBeamManagedBodyVisual CreateBodyVisual(Transform parentTransform,
                                                                     int visualIndex)
    {
        if (parentTransform == null)
            return null;

        GameObject instanceObject = new GameObject(string.Format("PlayerLaserBeamBody_{0}", visualIndex));
        instanceObject.transform.SetParent(parentTransform, false);
        instanceObject.layer = 0;
        Mesh dynamicMesh = new Mesh
        {
            name = string.Format("PlayerLaserBeamBodyMesh_{0}", visualIndex)
        };
        dynamicMesh.indexFormat = IndexFormat.UInt32;
        dynamicMesh.MarkDynamic();
        PlayerLaserBeamManagedBodyVisual bodyVisual = new PlayerLaserBeamManagedBodyVisual
        {
            InstanceObject = instanceObject,
            RootTransform = instanceObject.transform,
            DynamicMesh = dynamicMesh
        };
        bodyVisual.LayerVisuals.Add(CreateBodyLayerVisual(instanceObject.transform,
                                                          dynamicMesh,
                                                          PlayerLaserBeamBodyLayerRole.Core,
                                                          "Core",
                                                          LaserBeamVisualSortingOrder));
        bodyVisual.LayerVisuals.Add(CreateBodyLayerVisual(instanceObject.transform,
                                                          dynamicMesh,
                                                          PlayerLaserBeamBodyLayerRole.Flow,
                                                          "Flow",
                                                          LaserBeamVisualSortingOrder + 1));
        bodyVisual.LayerVisuals.Add(CreateBodyLayerVisual(instanceObject.transform,
                                                          dynamicMesh,
                                                          PlayerLaserBeamBodyLayerRole.Storm,
                                                          "Storm",
                                                          LaserBeamVisualSortingOrder + 2));
        return bodyVisual;
    }

    /// <summary>
    /// Instantiates one pooled particle visual from the resolved source or impact prefab.
    /// /params prefab Resolved particle prefab to instantiate.
    /// /params parentTransform Parent transform that receives the pooled instance.
    /// /params label Prefix used when renaming the created instance.
    /// /returns Newly created pooled particle visual, or null when creation fails.
    /// </summary>
    private static PlayerLaserBeamManagedParticleVisual CreateParticleVisual(GameObject prefab,
                                                                             Transform parentTransform,
                                                                             string label)
    {
        if (prefab == null || parentTransform == null)
            return null;

        GameObject instanceObject = Object.Instantiate(prefab, parentTransform);

        if (instanceObject == null)
            return null;

        instanceObject.name = string.Format("{0}_{1}", prefab.name, label);
        ParticleSystem[] particleSystems = instanceObject.GetComponentsInChildren<ParticleSystem>(true);
        ParticleSystemRenderer[] renderers = instanceObject.GetComponentsInChildren<ParticleSystemRenderer>(true);
        return new PlayerLaserBeamManagedParticleVisual
        {
            SourcePrefab = prefab,
            InstanceObject = instanceObject,
            RootTransform = instanceObject.transform,
            ParticleSystems = particleSystems,
            Renderers = renderers
        };
    }

    /// <summary>
    /// Destroys all pooled body visuals stored in one list.
    /// /params visuals Mutable pooled body visual list.
    /// /returns None.
    /// </summary>
    private static void DestroyBodyVisuals(List<PlayerLaserBeamManagedBodyVisual> visuals)
    {
        if (visuals == null)
            return;

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
            DestroyBodyVisual(visuals[visualIndex]);

        visuals.Clear();
    }

    /// <summary>
    /// Destroys all pooled particle visuals stored in one list.
    /// /params visuals Mutable pooled particle visual list.
    /// /returns None.
    /// </summary>
    private static void DestroyParticleVisuals(List<PlayerLaserBeamManagedParticleVisual> visuals)
    {
        if (visuals == null)
            return;

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
            DestroyParticleVisual(visuals[visualIndex]);

        visuals.Clear();
    }

    /// <summary>
    /// Destroys one pooled body visual instance and its owned dynamic mesh.
    /// /params visual Pooled body visual to destroy.
    /// /returns None.
    /// </summary>
    private static void DestroyBodyVisual(PlayerLaserBeamManagedBodyVisual visual)
    {
        if (visual == null)
            return;

        DestroyBodyLayerVisuals(visual.LayerVisuals);

        if (visual.DynamicMesh != null)
            Object.Destroy(visual.DynamicMesh);

        if (visual.InstanceObject != null)
            Object.Destroy(visual.InstanceObject);
    }

    /// <summary>
    /// Destroys one pooled particle visual instance.
    /// /params visual Pooled particle visual to destroy.
    /// /returns None.
    /// </summary>
    private static void DestroyParticleVisual(PlayerLaserBeamManagedParticleVisual visual)
    {
        if (visual == null || visual.InstanceObject == null)
            return;

        Object.Destroy(visual.InstanceObject);
    }

    /// <summary>
    /// Stops and clears every pooled particle visual in one list.
    /// /params visuals Mutable pooled particle visual list.
    /// /returns None.
    /// </summary>
    private static void StopParticleVisuals(List<PlayerLaserBeamManagedParticleVisual> visuals)
    {
        if (visuals == null)
            return;

        for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
        {
            PlayerLaserBeamManagedParticleVisual visual = visuals[visualIndex];

            if (visual == null)
                continue;

            StopParticleVisual(visual);
        }
    }

    /// <summary>
    /// Stops and clears every particle system owned by one pooled particle visual.
    /// /params visual Pooled particle visual to stop.
    /// /returns None.
    /// </summary>
    private static void StopParticleVisual(PlayerLaserBeamManagedParticleVisual visual)
    {
        if (visual == null || visual.ParticleSystems == null)
            return;

        for (int particleIndex = 0; particleIndex < visual.ParticleSystems.Length; particleIndex++)
        {
            ParticleSystem particleSystem = visual.ParticleSystems[particleIndex];

            if (particleSystem == null)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>
    /// Creates one mesh-renderer child that shares the lane body mesh and renders one visual layer role.
    /// /params parentTransform Parent transform that receives the new child.
    /// /params sharedMesh Dynamic lane mesh shared across all body layers.
    /// /params layerRole Visual role rendered by the created child.
    /// /params childName Readable name appended to the child GameObject.
    /// /params sortingOrder Renderer sorting order used for the layer.
    /// /returns Newly created body-layer visual, or null when creation fails.
    /// </summary>
    private static PlayerLaserBeamManagedBodyLayerVisual CreateBodyLayerVisual(Transform parentTransform,
                                                                               Mesh sharedMesh,
                                                                               PlayerLaserBeamBodyLayerRole layerRole,
                                                                               string childName,
                                                                               int sortingOrder)
    {
        if (parentTransform == null || sharedMesh == null)
            return null;

        GameObject childObject = new GameObject(string.Format("LaserBeamBodyLayer_{0}", childName));
        childObject.transform.SetParent(parentTransform, false);
        childObject.layer = 0;
        MeshFilter meshFilter = childObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = sharedMesh;
        MeshRenderer meshRenderer = childObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.sortingOrder = sortingOrder;
        return new PlayerLaserBeamManagedBodyLayerVisual
        {
            InstanceObject = childObject,
            RootTransform = childObject.transform,
            MeshFilter = meshFilter,
            MeshRenderer = meshRenderer,
            LayerRole = layerRole
        };
    }

    /// <summary>
    /// Destroys all child mesh-renderer layers owned by one body visual.
    /// /params layerVisuals Mutable layer list to destroy and clear.
    /// /returns None.
    /// </summary>
    private static void DestroyBodyLayerVisuals(List<PlayerLaserBeamManagedBodyLayerVisual> layerVisuals)
    {
        if (layerVisuals == null)
            return;

        for (int layerIndex = 0; layerIndex < layerVisuals.Count; layerIndex++)
            DestroyBodyLayerVisual(layerVisuals[layerIndex]);

        layerVisuals.Clear();
    }

    /// <summary>
    /// Destroys one child mesh-renderer layer owned by a lane body visual.
    /// /params layerVisual Layer visual to destroy.
    /// /returns None.
    /// </summary>
    private static void DestroyBodyLayerVisual(PlayerLaserBeamManagedBodyLayerVisual layerVisual)
    {
        if (layerVisual == null || layerVisual.InstanceObject == null)
            return;

        Object.Destroy(layerVisual.InstanceObject);
    }
    #endregion

    #endregion
}
