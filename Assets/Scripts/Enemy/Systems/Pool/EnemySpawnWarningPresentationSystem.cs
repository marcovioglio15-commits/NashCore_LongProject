using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Presents pooled world-space spawn-warning rings emitted by enemy spawners shortly before activation.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct EnemySpawnWarningPresentationSystem : ISystem
{
    #region Fields
    private static readonly List<EnemySpawnWarningRingView> activeViews = new List<EnemySpawnWarningRingView>(256);
    private static readonly Stack<EnemySpawnWarningRingView> pooledViews = new Stack<EnemySpawnWarningRingView>(128);

    private static GameObject runtimeRootObject;
    private static Material sharedWarningMaterial;
    #endregion

    #region Methods

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemySpawnWarningRequestElement>();
    }

    public void OnUpdate(ref SystemState state)
    {
        ConsumeSpawnWarningRequests(ref state);
        TickActiveViews(SystemAPI.Time.DeltaTime);
    }

    public void OnDestroy(ref SystemState state)
    {
        DestroyAllViews();
        DestroySharedMaterial();
        DestroyRuntimeRoot();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Converts transient ECS warning requests into pooled runtime ring views.
    /// returns None.
    /// </summary>
    private void ConsumeSpawnWarningRequests(ref SystemState state)
    {
        foreach (DynamicBuffer<EnemySpawnWarningRequestElement> requests in SystemAPI.Query<DynamicBuffer<EnemySpawnWarningRequestElement>>())
        {
            if (requests.Length <= 0)
                continue;

            for (int requestIndex = 0; requestIndex < requests.Length; requestIndex++)
            {
                EnemySpawnWarningRequestElement request = requests[requestIndex];

                if (request.DurationSeconds <= 0f || request.Radius <= 0f || request.RingWidth <= 0f || request.MaximumAlpha <= 0f)
                    continue;

                EnemySpawnWarningRingView view = AcquireView();

                if (view == null)
                    continue;

                view.Play(request.WorldPosition,
                          request.DurationSeconds,
                          request.FadeOutSeconds,
                          request.Radius,
                          request.RingWidth,
                          new Color(request.Color.x, request.Color.y, request.Color.z, request.Color.w),
                          request.MaximumAlpha);
            }

            requests.Clear();
        }
    }

    /// <summary>
    /// Advances every active pooled ring and returns expired instances back to the pool.
    ///  deltaTime: Current frame delta time in seconds.
    /// returns None.
    /// </summary>
    private static void TickActiveViews(float deltaTime)
    {
        for (int viewIndex = activeViews.Count - 1; viewIndex >= 0; viewIndex--)
        {
            EnemySpawnWarningRingView view = activeViews[viewIndex];

            if (view == null)
            {
                activeViews.RemoveAt(viewIndex);
                continue;
            }

            if (view.Tick(deltaTime))
                continue;

            view.Deactivate();
            activeViews.RemoveAt(viewIndex);
            pooledViews.Push(view);
        }
    }

    /// <summary>
    /// Acquires one pooled warning ring view or creates a new one when the pool is empty.
    /// returns Runtime warning ring view ready for playback, or null when creation failed.
    /// </summary>
    private static EnemySpawnWarningRingView AcquireView()
    {
        while (pooledViews.Count > 0)
        {
            EnemySpawnWarningRingView pooledView = pooledViews.Pop();

            if (pooledView == null)
                continue;

            if (pooledView.transform.parent != ResolveRuntimeRootTransform())
                pooledView.transform.SetParent(ResolveRuntimeRootTransform(), false);

            pooledView.Initialize(ResolveSharedWarningMaterial());
            activeViews.Add(pooledView);
            return pooledView;
        }

        GameObject viewObject = new GameObject("EnemySpawnWarningRingView");
        viewObject.hideFlags = HideFlags.HideAndDontSave;
        Transform runtimeRootTransform = ResolveRuntimeRootTransform();

        if (runtimeRootTransform != null)
            viewObject.transform.SetParent(runtimeRootTransform, false);

        EnemySpawnWarningRingView createdView = viewObject.AddComponent<EnemySpawnWarningRingView>();
        createdView.Initialize(ResolveSharedWarningMaterial());
        activeViews.Add(createdView);
        return createdView;
    }

    /// <summary>
    /// Resolves the hidden runtime root used to keep pooled warning ring objects organized.
    /// returns Transform of the runtime root.
    /// </summary>
    private static Transform ResolveRuntimeRootTransform()
    {
        if (runtimeRootObject == null)
        {
            runtimeRootObject = new GameObject("EnemySpawnWarningRuntimeRoot");
            runtimeRootObject.hideFlags = HideFlags.HideAndDontSave;
        }

        return runtimeRootObject.transform;
    }

    /// <summary>
    /// Resolves the shared material used by every pooled LineRenderer.
    /// returns Shared runtime material, or null when no supported shader was found.
    /// </summary>
    private static Material ResolveSharedWarningMaterial()
    {
        if (sharedWarningMaterial != null)
            return sharedWarningMaterial;

        Shader resolvedShader = Shader.Find("Sprites/Default");

        if (resolvedShader == null)
            resolvedShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (resolvedShader == null)
            resolvedShader = Shader.Find("Universal Render Pipeline/Unlit");

        if (resolvedShader == null)
            resolvedShader = Shader.Find("Unlit/Color");

        if (resolvedShader == null)
            return null;

        sharedWarningMaterial = new Material(resolvedShader);
        sharedWarningMaterial.name = "EnemySpawnWarningRing_Runtime";
        sharedWarningMaterial.hideFlags = HideFlags.HideAndDontSave;
        sharedWarningMaterial.enableInstancing = true;
        return sharedWarningMaterial;
    }

    /// <summary>
    /// Destroys every active and pooled warning ring instance.
    /// returns None.
    /// </summary>
    private static void DestroyAllViews()
    {
        for (int viewIndex = 0; viewIndex < activeViews.Count; viewIndex++)
        {
            EnemySpawnWarningRingView activeView = activeViews[viewIndex];

            if (activeView == null)
                continue;

            DestroyObject(activeView.gameObject);
        }

        activeViews.Clear();

        while (pooledViews.Count > 0)
        {
            EnemySpawnWarningRingView pooledView = pooledViews.Pop();

            if (pooledView == null)
                continue;

            DestroyObject(pooledView.gameObject);
        }
    }

    /// <summary>
    /// Destroys the shared warning material created at runtime.
    /// returns None.
    /// </summary>
    private static void DestroySharedMaterial()
    {
        if (sharedWarningMaterial == null)
            return;

        DestroyObject(sharedWarningMaterial);
        sharedWarningMaterial = null;
    }

    /// <summary>
    /// Destroys the hidden runtime root used by pooled warning rings.
    /// returns None.
    /// </summary>
    private static void DestroyRuntimeRoot()
    {
        if (runtimeRootObject == null)
            return;

        DestroyObject(runtimeRootObject);
        runtimeRootObject = null;
    }

    /// <summary>
    /// Destroys one runtime UnityEngine.Object using the correct editor-or-play-mode API.
    ///  targetObject: Unity object that should be destroyed.
    /// returns None.
    /// </summary>
    private static void DestroyObject(Object targetObject)
    {
        if (targetObject == null)
            return;

        if (Application.isPlaying)
        {
            Object.Destroy(targetObject);
            return;
        }

        Object.DestroyImmediate(targetObject);
    }
    #endregion

    #endregion
}
