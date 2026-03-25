using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

/// <summary>
/// Applies and resets GPU hit-flash material overrides on every renderer entity owned by one enemy root.
/// </summary>
public static class EnemyDamageFlashRenderUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures one render entity exposes the component set required by GPU hit-flash presentation.
    /// /params entityManager: Entity manager used to query current component presence.
    /// /params entityCommandBuffer: Deferred writer used to avoid structural changes while iterating ECS queries.
    /// /params renderEntity: Concrete render entity to initialize.
    /// /params baseColor: Original material color restored when the flash ends.
    /// /params flashColor: Flash color written into shader overrides.
    /// /returns None.
    /// </summary>
    public static void EnsureGpuFlashComponents(EntityManager entityManager,
                                                EntityCommandBuffer entityCommandBuffer,
                                                Entity renderEntity,
                                                float4 baseColor,
                                                float4 flashColor)
    {
        if (!entityManager.Exists(renderEntity))
            return;

        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new DamageFlashBaseColor
                              {
                                  Value = baseColor
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new URPMaterialPropertyBaseColor
                              {
                                  Value = baseColor
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialColor
                              {
                                  Value = baseColor
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialHitFlashColor
                              {
                                  Value = flashColor
                              });
        SetOrAddComponentData(entityManager,
                              entityCommandBuffer,
                              renderEntity,
                              new MaterialHitFlashBlend
                              {
                                  Value = 0f
                              });
    }

    /// <summary>
    /// Writes the current flash blend to all registered renderer entities of one enemy.
    /// /params entityManager: Entity manager used to access flash render targets.
    /// /params enemyEntity: Enemy root entity that owns the flash config and render target buffer.
    /// /params damageFlashConfig: Immutable flash config currently active on the enemy.
    /// /params targetBlend: Flash blend to write this frame.
    /// /returns None.
    /// </summary>
    public static void ApplyGpuFlash(EntityManager entityManager,
                                     Entity enemyEntity,
                                     in DamageFlashConfig damageFlashConfig,
                                     float targetBlend)
    {
        if (!entityManager.Exists(enemyEntity))
            return;

        if (entityManager.HasBuffer<DamageFlashRenderTargetElement>(enemyEntity))
        {
            DynamicBuffer<DamageFlashRenderTargetElement> renderTargets = entityManager.GetBuffer<DamageFlashRenderTargetElement>(enemyEntity);

            for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
                ApplyGpuFlashToEntity(entityManager, renderTargets[targetIndex].Value, in damageFlashConfig, targetBlend);

            return;
        }

        ApplyGpuFlashToEntity(entityManager, enemyEntity, in damageFlashConfig, targetBlend);
    }

    /// <summary>
    /// Restores all registered renderer entities to their baked non-flashing state.
    /// /params entityManager: Entity manager used to access flash render targets.
    /// /params enemyEntity: Enemy root entity that owns the flash render target buffer.
    /// /returns None.
    /// </summary>
    public static void ResetGpuFlash(EntityManager entityManager, Entity enemyEntity)
    {
        if (!entityManager.Exists(enemyEntity))
            return;

        if (entityManager.HasBuffer<DamageFlashRenderTargetElement>(enemyEntity))
        {
            DynamicBuffer<DamageFlashRenderTargetElement> renderTargets = entityManager.GetBuffer<DamageFlashRenderTargetElement>(enemyEntity);

            for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
                ResetGpuFlashOnEntity(entityManager, renderTargets[targetIndex].Value);

            return;
        }

        ResetGpuFlashOnEntity(entityManager, enemyEntity);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Writes per-instance material overrides for one concrete render entity.
    /// /params entityManager: Entity manager used to write component data.
    /// /params renderEntity: Concrete renderer entity to update.
    /// /params damageFlashConfig: Immutable flash config currently active on the enemy.
    /// /params targetBlend: Flash blend to write this frame.
    /// /returns None.
    /// </summary>
    private static void ApplyGpuFlashToEntity(EntityManager entityManager,
                                              Entity renderEntity,
                                              in DamageFlashConfig damageFlashConfig,
                                              float targetBlend)
    {
        if (!entityManager.Exists(renderEntity))
            return;

        if (entityManager.HasComponent<DamageFlashBaseColor>(renderEntity))
        {
            DamageFlashBaseColor baseColor = entityManager.GetComponentData<DamageFlashBaseColor>(renderEntity);
            float4 blendedBaseColor = DamageFlashRuntimeUtility.ResolveBaseColor(in baseColor,
                                                                                 in damageFlashConfig,
                                                                                 targetBlend);

            if (entityManager.HasComponent<URPMaterialPropertyBaseColor>(renderEntity))
            {
                entityManager.SetComponentData(renderEntity, new URPMaterialPropertyBaseColor
                {
                    Value = blendedBaseColor
                });
            }

            if (entityManager.HasComponent<MaterialColor>(renderEntity))
            {
                entityManager.SetComponentData(renderEntity, new MaterialColor
                {
                    Value = blendedBaseColor
                });
            }
        }

        if (entityManager.HasComponent<MaterialHitFlashColor>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialHitFlashColor
            {
                Value = damageFlashConfig.FlashColor
            });
        }

        if (entityManager.HasComponent<MaterialHitFlashBlend>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialHitFlashBlend
            {
                Value = targetBlend
            });
        }
    }

    /// <summary>
    /// Restores one concrete render entity to its baked base color and zero flash blend.
    /// /params entityManager: Entity manager used to write component data.
    /// /params renderEntity: Concrete renderer entity to reset.
    /// /returns None.
    /// </summary>
    private static void ResetGpuFlashOnEntity(EntityManager entityManager, Entity renderEntity)
    {
        if (!entityManager.Exists(renderEntity))
            return;

        if (entityManager.HasComponent<DamageFlashBaseColor>(renderEntity))
        {
            float4 baseColor = entityManager.GetComponentData<DamageFlashBaseColor>(renderEntity).Value;

            if (entityManager.HasComponent<URPMaterialPropertyBaseColor>(renderEntity))
            {
                entityManager.SetComponentData(renderEntity, new URPMaterialPropertyBaseColor
                {
                    Value = baseColor
                });
            }

            if (entityManager.HasComponent<MaterialColor>(renderEntity))
            {
                entityManager.SetComponentData(renderEntity, new MaterialColor
                {
                    Value = baseColor
                });
            }
        }

        if (entityManager.HasComponent<MaterialHitFlashBlend>(renderEntity))
        {
            entityManager.SetComponentData(renderEntity, new MaterialHitFlashBlend
            {
                Value = 0f
            });
        }
    }

    /// <summary>
    /// Writes one component value, adding the component first when it is still missing on the target entity.
    /// /params entityManager: Entity manager used to inspect current component presence.
    /// /params entityCommandBuffer: Deferred writer used to record add/set operations safely.
    /// /params entity: Target entity that must receive the component value.
    /// /params componentData: Value to write.
    /// /returns None.
    /// </summary>
    private static void SetOrAddComponentData<T>(EntityManager entityManager,
                                                 EntityCommandBuffer entityCommandBuffer,
                                                 Entity entity,
                                                 T componentData)
        where T : unmanaged, IComponentData
    {
        if (entityManager.HasComponent<T>(entity))
        {
            entityCommandBuffer.SetComponent(entity, componentData);
            return;
        }

        entityCommandBuffer.AddComponent(entity, componentData);
    }
    #endregion

    #endregion
}
