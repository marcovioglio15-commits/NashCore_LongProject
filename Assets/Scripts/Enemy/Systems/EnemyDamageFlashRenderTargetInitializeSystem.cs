using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Marks one enemy root after its flash render targets have received the runtime material override components.
/// returns None.
/// </summary>
public struct EnemyDamageFlashRenderTargetsInitialized : IComponentData
{
}

/// <summary>
/// Initializes hit-flash material override components on every renderer entity referenced by enemy roots.
/// It runs before pool prewarm so instantiated pooled enemies inherit ready-to-use flash properties from initialized prefabs.
/// </summary>
[UpdateInGroup(typeof(EnemySystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(EnemyPoolInitializeSystem))]
public partial struct EnemyDamageFlashRenderTargetInitializeSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the minimum data required to run the initialization pass.
    /// state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DamageFlashConfig>();
        state.RequireForUpdate<DamageFlashRenderTargetElement>();
    }

    /// <summary>
    /// Adds missing per-renderer material property components to every uninitialized enemy root.
    /// state: Current ECS system state.
    /// returns None.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

        try
        {
            foreach ((RefRO<DamageFlashConfig> damageFlashConfig,
                      DynamicBuffer<DamageFlashRenderTargetElement> renderTargets,
                      Entity enemyEntity)
                     in SystemAPI.Query<RefRO<DamageFlashConfig>, DynamicBuffer<DamageFlashRenderTargetElement>>()
                                 .WithNone<EnemyDamageFlashRenderTargetsInitialized>()
                                 .WithEntityAccess())
            {
                float4 flashColor = damageFlashConfig.ValueRO.FlashColor;

                for (int targetIndex = 0; targetIndex < renderTargets.Length; targetIndex++)
                {
                    DamageFlashRenderTargetElement renderTarget = renderTargets[targetIndex];
                    EnemyDamageFlashRenderUtility.EnsureGpuFlashComponents(entityManager,
                                                                           entityCommandBuffer,
                                                                           renderTarget.Value,
                                                                           renderTarget.BaseColor,
                                                                           flashColor);
                }

                entityCommandBuffer.AddComponent<EnemyDamageFlashRenderTargetsInitialized>(enemyEntity);
            }

            entityCommandBuffer.Playback(entityManager);
        }
        finally
        {
            entityCommandBuffer.Dispose();
        }
    }
    #endregion

    #endregion
}
