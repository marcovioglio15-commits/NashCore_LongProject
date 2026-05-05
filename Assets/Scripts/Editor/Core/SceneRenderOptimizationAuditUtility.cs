using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides editor-only render optimization diagnostics for open scenes without mutating scene or prefab data.
/// </summary>
public static class SceneRenderOptimizationAuditUtility
{
    #region Constants
    private const int MaxLoggedExamplesPerCategory = 24;
    #endregion

    #region Methods

    #region Menu
    /// <summary>
    /// Audits currently open scenes for common render cost risks such as missing LODGroups and multi-material renderers.
    /// </summary>
    [MenuItem("Tools/Optimization/Audit Open Scenes Rendering")]
    private static void AuditOpenScenesRendering()
    {
        string report = BuildOpenScenesRenderingAuditReport();
        Debug.Log(report);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Builds a text report for currently loaded scenes that can be copied from the Unity Console.
    /// </summary>
    /// <returns>Human-readable render optimization report.</returns>
    public static string BuildOpenScenesRenderingAuditReport()
    {
        MeshRenderer[] meshRenderers = Resources.FindObjectsOfTypeAll<MeshRenderer>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>();
        SceneRenderAuditStats stats = new SceneRenderAuditStats();
        stats.UniqueMaterials = new HashSet<Material>();
        StringBuilder reportBuilder = new StringBuilder(4096);

        reportBuilder.AppendLine("[Scene Render Optimization Audit]");
        reportBuilder.AppendLine("Scope: loaded scenes only. No scene or prefab data was modified.");
        reportBuilder.AppendLine();

        AuditMeshRenderers(meshRenderers, ref stats, reportBuilder);
        AuditSkinnedMeshRenderers(skinnedMeshRenderers, ref stats, reportBuilder);
        AppendSummary(in stats, reportBuilder);

        return reportBuilder.ToString();
    }
    #endregion

    #region Audit
    /// <summary>
    /// Audits MeshRenderer components for static batching, LODGroup coverage and material slot count.
    /// </summary>
    /// <param name="meshRenderers">Renderer array collected from loaded editor resources.</param>
    /// <param name="stats">Mutable aggregate report counters.</param>
    /// <param name="reportBuilder">Text builder receiving detailed examples.</param>
    private static void AuditMeshRenderers(MeshRenderer[] meshRenderers,
                                           ref SceneRenderAuditStats stats,
                                           StringBuilder reportBuilder)
    {
        if (meshRenderers == null)
            return;

        for (int rendererIndex = 0; rendererIndex < meshRenderers.Length; rendererIndex++)
        {
            MeshRenderer meshRenderer = meshRenderers[rendererIndex];

            if (!IsSceneRenderer(meshRenderer))
                continue;

            stats.MeshRendererCount++;
            TrackMaterials(meshRenderer.sharedMaterials, ref stats);

            if (!meshRenderer.gameObject.isStatic)
            {
                stats.NonStaticMeshRendererCount++;
                AppendExample(reportBuilder,
                              "Static batching candidate",
                              meshRenderer,
                              stats.NonStaticMeshRendererCount);
            }

            if (meshRenderer.GetComponentInParent<LODGroup>() == null)
            {
                stats.MeshRenderersWithoutLodGroup++;
                AppendExample(reportBuilder,
                              "MeshRenderer without LODGroup",
                              meshRenderer,
                              stats.MeshRenderersWithoutLodGroup);
            }

            AppendMaterialSlotWarning(meshRenderer,
                                      meshRenderer.sharedMaterials,
                                      ref stats,
                                      reportBuilder);
        }
    }

    /// <summary>
    /// Audits SkinnedMeshRenderer components for LODGroup coverage and material slot count.
    /// </summary>
    /// <param name="skinnedMeshRenderers">Renderer array collected from loaded editor resources.</param>
    /// <param name="stats">Mutable aggregate report counters.</param>
    /// <param name="reportBuilder">Text builder receiving detailed examples.</param>
    private static void AuditSkinnedMeshRenderers(SkinnedMeshRenderer[] skinnedMeshRenderers,
                                                  ref SceneRenderAuditStats stats,
                                                  StringBuilder reportBuilder)
    {
        if (skinnedMeshRenderers == null)
            return;

        for (int rendererIndex = 0; rendererIndex < skinnedMeshRenderers.Length; rendererIndex++)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = skinnedMeshRenderers[rendererIndex];

            if (!IsSceneRenderer(skinnedMeshRenderer))
                continue;

            stats.SkinnedMeshRendererCount++;
            TrackMaterials(skinnedMeshRenderer.sharedMaterials, ref stats);

            if (skinnedMeshRenderer.GetComponentInParent<LODGroup>() == null)
            {
                stats.SkinnedRenderersWithoutLodGroup++;
                AppendExample(reportBuilder,
                              "SkinnedMeshRenderer without LODGroup",
                              skinnedMeshRenderer,
                              stats.SkinnedRenderersWithoutLodGroup);
            }

            AppendMaterialSlotWarning(skinnedMeshRenderer,
                                      skinnedMeshRenderer.sharedMaterials,
                                      ref stats,
                                      reportBuilder);
        }
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Resolves whether a renderer belongs to a loaded scene rather than an asset or hidden editor object.
    /// </summary>
    /// <param name="renderer">Renderer candidate to inspect.</param>
    /// <returns>True when the renderer is part of one loaded scene.</returns>
    private static bool IsSceneRenderer(Renderer renderer)
    {
        if (renderer == null)
            return false;

        if (EditorUtility.IsPersistent(renderer))
            return false;

        Scene scene = renderer.gameObject.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    /// <summary>
    /// Tracks unique shared materials used by audited renderers.
    /// </summary>
    /// <param name="materials">Renderer shared material array.</param>
    /// <param name="stats">Mutable aggregate report counters.</param>
    private static void TrackMaterials(Material[] materials, ref SceneRenderAuditStats stats)
    {
        if (materials == null)
            return;

        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
        {
            Material material = materials[materialIndex];

            if (material == null)
                continue;

            stats.UniqueMaterials.Add(material);
        }
    }

    /// <summary>
    /// Adds a material-slot warning when one renderer contributes more than one draw slot.
    /// </summary>
    /// <param name="renderer">Renderer being inspected.</param>
    /// <param name="materials">Renderer shared material array.</param>
    /// <param name="stats">Mutable aggregate report counters.</param>
    /// <param name="reportBuilder">Text builder receiving detailed examples.</param>
    private static void AppendMaterialSlotWarning(Renderer renderer,
                                                  Material[] materials,
                                                  ref SceneRenderAuditStats stats,
                                                  StringBuilder reportBuilder)
    {
        int materialSlotCount = materials != null ? materials.Length : 0;

        if (materialSlotCount <= 1)
            return;

        stats.MultiMaterialRendererCount++;
        AppendExample(reportBuilder,
                      string.Format("Renderer with {0} material slots", materialSlotCount),
                      renderer,
                      stats.MultiMaterialRendererCount);
    }

    /// <summary>
    /// Appends a bounded example line for one warning category.
    /// </summary>
    /// <param name="reportBuilder">Text builder receiving detailed examples.</param>
    /// <param name="label">Warning category label.</param>
    /// <param name="renderer">Renderer used to resolve scene path context.</param>
    /// <param name="categoryCount">Current category count used to cap example spam.</param>
    private static void AppendExample(StringBuilder reportBuilder,
                                      string label,
                                      Renderer renderer,
                                      int categoryCount)
    {
        if (reportBuilder == null || renderer == null)
            return;

        if (categoryCount > MaxLoggedExamplesPerCategory)
            return;

        reportBuilder.Append(" - ");
        reportBuilder.Append(label);
        reportBuilder.Append(": ");
        reportBuilder.Append(renderer.gameObject.scene.name);
        reportBuilder.Append("/");
        reportBuilder.Append(GetHierarchyPath(renderer.transform));
        reportBuilder.AppendLine();
    }

    /// <summary>
    /// Appends the aggregate report summary after all renderer examples have been collected.
    /// </summary>
    /// <param name="stats">Aggregate audit counters.</param>
    /// <param name="reportBuilder">Text builder receiving summary lines.</param>
    private static void AppendSummary(in SceneRenderAuditStats stats, StringBuilder reportBuilder)
    {
        if (reportBuilder == null)
            return;

        reportBuilder.AppendLine();
        reportBuilder.AppendLine("Summary:");
        reportBuilder.AppendLine(string.Format(" MeshRenderers: {0}", stats.MeshRendererCount));
        reportBuilder.AppendLine(string.Format(" SkinnedMeshRenderers: {0}", stats.SkinnedMeshRendererCount));
        reportBuilder.AppendLine(string.Format(" Unique shared materials: {0}", stats.UniqueMaterials.Count));
        reportBuilder.AppendLine(string.Format(" MeshRenderers without LODGroup: {0}", stats.MeshRenderersWithoutLodGroup));
        reportBuilder.AppendLine(string.Format(" SkinnedMeshRenderers without LODGroup: {0}", stats.SkinnedRenderersWithoutLodGroup));
        reportBuilder.AppendLine(string.Format(" Non-static MeshRenderers: {0}", stats.NonStaticMeshRendererCount));
        reportBuilder.AppendLine(string.Format(" Multi-material renderers: {0}", stats.MultiMaterialRendererCount));
    }

    /// <summary>
    /// Builds a stable hierarchy path for one transform to make audit examples easy to find.
    /// </summary>
    /// <param name="transform">Transform to describe.</param>
    /// <returns>Slash-separated hierarchy path.</returns>
    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        StringBuilder pathBuilder = new StringBuilder(transform.name);
        Transform currentTransform = transform.parent;

        while (currentTransform != null)
        {
            pathBuilder.Insert(0, "/");
            pathBuilder.Insert(0, currentTransform.name);
            currentTransform = currentTransform.parent;
        }

        return pathBuilder.ToString();
    }
    #endregion

    #endregion

    #region Nested Types
    /// <summary>
    /// Stores aggregate counters collected while auditing renderers in loaded scenes.
    /// </summary>
    private struct SceneRenderAuditStats
    {
        public int MeshRendererCount;
        public int SkinnedMeshRendererCount;
        public int MeshRenderersWithoutLodGroup;
        public int SkinnedRenderersWithoutLodGroup;
        public int NonStaticMeshRendererCount;
        public int MultiMaterialRendererCount;
        public HashSet<Material> UniqueMaterials;
    }
    #endregion
}
