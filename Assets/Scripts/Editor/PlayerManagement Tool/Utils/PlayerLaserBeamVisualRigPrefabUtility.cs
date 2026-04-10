using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the authored Laser Beam body and particle prefabs, then synchronizes the runtime visual bridge rig.
/// /params None.
/// /returns None.
/// </summary>
public static class PlayerLaserBeamVisualRigPrefabUtility
{
    #region Constants
    private const string PlayerVisualPrefabPath = "Assets/Prefabs/Player/PF_PlayerVisual.prefab";
    private const string RoundedTubeBodyPrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamBody.prefab";
    private const string TaperedJetBodyPrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamBody_TaperedJet.prefab";
    private const string DenseRibbonBodyPrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamBody_DenseRibbon.prefab";
    private const string BubbleBurstSourcePrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamSource_BubbleBurst.prefab";
    private const string StarBloomSourcePrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamSource_StarBloom.prefab";
    private const string SoftDiscSourcePrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamSource_SoftDisc.prefab";
    private const string BubbleBurstImpactPrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamImpact_BubbleBurst.prefab";
    private const string StarBloomImpactPrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamImpact_StarBloom.prefab";
    private const string SoftDiscImpactPrefabPath = "Assets/Prefabs/Player/PF_PlayerLaserBeamImpact_SoftDisc.prefab";
    private const string BodyCapsuleMeshAssetPath = "Assets/3D/Meshes/Player/ME_PlayerLaserBeamBodyCapsule.asset";
    private const string ParticleSphereMeshAssetPath = "Assets/3D/Meshes/Player/ME_PlayerLaserBeamParticleSphere.asset";
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds the full authored Laser Beam visual rig and updates the player visual bridge prefab.
    /// /params None.
    /// /returns None.
    /// </summary>
    [MenuItem("Tools/Player/Rebuild Laser Beam Visual Rig Prefabs")]
    public static void RebuildLaserBeamVisualRigPrefabs()
    {
        ExecuteRebuild();
    }

    /// <summary>
    /// Executes the rebuild entry point from Unity batch mode via -executeMethod.
    /// /params None.
    /// /returns None.
    /// </summary>
    public static void ExecuteRebuild()
    {
        Material bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultBodyMaterialPath);
        Material bubbleMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultSourceBubbleMaterialPath);
        Material splashMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlayerLaserBeamVisualDefaultsUtility.DefaultImpactSplashMaterialPath);
        Mesh bodyMesh = PlayerLaserBeamVisualRigPrefabBuildUtility.RebuildPrimitiveMeshAsset(BodyCapsuleMeshAssetPath,
                                                                                              PrimitiveType.Capsule,
                                                                                              Quaternion.Euler(90f, 0f, 0f));
        Mesh particleMesh = PlayerLaserBeamVisualRigPrefabBuildUtility.RebuildPrimitiveMeshAsset(ParticleSphereMeshAssetPath,
                                                                                                  PrimitiveType.Sphere,
                                                                                                  Quaternion.identity);

        if (bodyMaterial == null)
            throw new InvalidOperationException(string.Format("Laser Beam body material not found at '{0}'.", PlayerLaserBeamVisualDefaultsUtility.DefaultBodyMaterialPath));

        if (bubbleMaterial == null)
            throw new InvalidOperationException(string.Format("Laser Beam source material not found at '{0}'.", PlayerLaserBeamVisualDefaultsUtility.DefaultSourceBubbleMaterialPath));

        if (splashMaterial == null)
            throw new InvalidOperationException(string.Format("Laser Beam impact material not found at '{0}'.", PlayerLaserBeamVisualDefaultsUtility.DefaultImpactSplashMaterialPath));

        if (bodyMesh == null)
            throw new InvalidOperationException(string.Format("Laser Beam body mesh could not be built at '{0}'.", BodyCapsuleMeshAssetPath));

        if (particleMesh == null)
            throw new InvalidOperationException(string.Format("Laser Beam particle mesh could not be built at '{0}'.", ParticleSphereMeshAssetPath));

        PlayerLaserBeamVisualRigPrefabBuildUtility.EnsureFolder(Path.GetDirectoryName(RoundedTubeBodyPrefabPath));
        GameObject roundedTubeBodyPrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildBodyPrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateRoundedTubeBodyDefinition(RoundedTubeBodyPrefabPath),
                                                                                                      bodyMaterial,
                                                                                                      bodyMesh);
        GameObject taperedJetBodyPrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildBodyPrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateTaperedJetBodyDefinition(TaperedJetBodyPrefabPath),
                                                                                                     bodyMaterial,
                                                                                                     bodyMesh);
        GameObject denseRibbonBodyPrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildBodyPrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateDenseRibbonBodyDefinition(DenseRibbonBodyPrefabPath),
                                                                                                      bodyMaterial,
                                                                                                      bodyMesh);
        GameObject bubbleBurstSourcePrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateBubbleBurstSourceDefinition(BubbleBurstSourcePrefabPath),
                                                                                                             bubbleMaterial,
                                                                                                             particleMesh);
        GameObject starBloomSourcePrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateStarBloomSourceDefinition(StarBloomSourcePrefabPath),
                                                                                                           bubbleMaterial,
                                                                                                           particleMesh);
        GameObject softDiscSourcePrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateSoftDiscSourceDefinition(SoftDiscSourcePrefabPath),
                                                                                                          bubbleMaterial,
                                                                                                          particleMesh);
        GameObject bubbleBurstImpactPrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateBubbleBurstImpactDefinition(BubbleBurstImpactPrefabPath),
                                                                                                             splashMaterial,
                                                                                                             particleMesh);
        GameObject starBloomImpactPrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateStarBloomImpactDefinition(StarBloomImpactPrefabPath),
                                                                                                           splashMaterial,
                                                                                                           particleMesh);
        GameObject softDiscImpactPrefab = PlayerLaserBeamVisualRigPrefabBuildUtility.BuildParticlePrefab(PlayerLaserBeamVisualRigPrefabDefinitions.CreateSoftDiscImpactDefinition(SoftDiscImpactPrefabPath),
                                                                                                          splashMaterial,
                                                                                                          particleMesh);
        PlayerLaserBeamVisualRigPrefabBuildUtility.SynchronizePlayerVisualBridge(PlayerVisualPrefabPath,
                                                                                 roundedTubeBodyPrefab,
                                                                                 taperedJetBodyPrefab,
                                                                                 denseRibbonBodyPrefab,
                                                                                 bubbleBurstSourcePrefab,
                                                                                 starBloomSourcePrefab,
                                                                                 softDiscSourcePrefab,
                                                                                 bubbleBurstImpactPrefab,
                                                                                 starBloomImpactPrefab,
                                                                                 softDiscImpactPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerLaserBeamVisualRigPrefabUtility] Laser Beam visual rig prefabs rebuilt successfully.");
    }
    #endregion

    #endregion
}
