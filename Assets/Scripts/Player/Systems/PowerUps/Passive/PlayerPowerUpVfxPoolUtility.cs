using Unity.Entities;

/// <summary>
/// Shared helper for acquiring and releasing pooled VFX entities.
/// </summary>
public static class PlayerPowerUpVfxPoolUtility
{
    #region Methods

    #region Public Methods
    public static Entity AcquireVfxEntity(EntityManager entityManager,
                                          ref EntityCommandBuffer commandBuffer,
                                          Entity poolOwnerEntity,
                                          DynamicBuffer<PlayerPowerUpVfxPoolElement> poolBuffer,
                                          Entity prefabEntity,
                                          out bool reusedInstance)
    {
        reusedInstance = false;

        if (prefabEntity == Entity.Null)
            return Entity.Null;

        if (entityManager.Exists(prefabEntity) == false)
            return Entity.Null;

        for (int index = 0; index < poolBuffer.Length; index++)
        {
            PlayerPowerUpVfxPoolElement poolElement = poolBuffer[index];
            Entity pooledEntity = poolElement.VfxEntity;

            if (pooledEntity == Entity.Null)
            {
                poolBuffer.RemoveAt(index);
                index--;
                continue;
            }

            // Keep deferred entities created in the same frame.
            // They become valid after ECB playback and must stay in the pool metadata.
            if (pooledEntity.Index < 0)
                continue;

            if (entityManager.Exists(pooledEntity) == false)
            {
                poolBuffer.RemoveAt(index);
                index--;
                continue;
            }

            if (poolElement.PrefabEntity != prefabEntity)
                continue;

            if (entityManager.IsEnabled(pooledEntity))
                continue;

            commandBuffer.SetEnabled(pooledEntity, true);
            reusedInstance = true;
            return pooledEntity;
        }

        Entity newVfxEntity = commandBuffer.Instantiate(prefabEntity);
        commandBuffer.AppendToBuffer(poolOwnerEntity, new PlayerPowerUpVfxPoolElement
        {
            PrefabEntity = prefabEntity,
            VfxEntity = newVfxEntity
        });
        return newVfxEntity;
    }

    public static void ReleaseVfxEntity(EntityManager entityManager,
                                        ref EntityCommandBuffer commandBuffer,
                                        Entity vfxEntity)
    {
        if (vfxEntity == Entity.Null)
            return;

        if (vfxEntity.Index < 0)
            return;

        if (entityManager.Exists(vfxEntity) == false)
            return;

        commandBuffer.RemoveComponent<PlayerPowerUpVfxLifetime>(vfxEntity);
        commandBuffer.RemoveComponent<PlayerPowerUpVfxFollowTarget>(vfxEntity);
        commandBuffer.RemoveComponent<PlayerPowerUpVfxVelocity>(vfxEntity);
        commandBuffer.SetEnabled(vfxEntity, false);
    }
    #endregion

    #endregion
}
