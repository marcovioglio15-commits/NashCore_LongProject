using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Consumes authoritative swap commands issued by HUD or world-space prompts for dropped power-up containers.
///  none.
/// returns none.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMilestonePowerUpSelectionResolveSystem))]
[UpdateBefore(typeof(PlayerPowerUpActivationSystem))]
public partial struct PlayerPowerUpContainerSwapResolveSystem : ISystem
{
    #region Methods

    #region Lifecycle
    /// <summary>
    /// Registers the runtime components required to resolve dropped-container swap commands.
    ///  state: Current ECS system state.
    /// returns void.
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerPowerUpContainerSwapCommand>();
        state.RequireForUpdate<PlayerDroppedPowerUpContainerContent>();
        state.RequireForUpdate<PlayerPowerUpsConfig>();
        state.RequireForUpdate<PlayerPowerUpsState>();
    }

    /// <summary>
    /// Applies the first valid dropped-container swap command queued on each player entity.
    ///  state: Current ECS system state.
    /// returns void.
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        ComponentLookup<PlayerDroppedPowerUpContainerContent> droppedContainerContentLookup = SystemAPI.GetComponentLookup<PlayerDroppedPowerUpContainerContent>(false);
        ComponentLookup<PlayerPowerUpContainerInteractionConfig> interactionConfigLookup = SystemAPI.GetComponentLookup<PlayerPowerUpContainerInteractionConfig>(true);
        EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.Temp);

        foreach ((DynamicBuffer<PlayerPowerUpContainerSwapCommand> swapCommands,
                  RefRW<PlayerPowerUpsConfig> powerUpsConfig,
                  RefRW<PlayerPowerUpsState> powerUpsState,
                  Entity playerEntity)
                 in SystemAPI.Query<DynamicBuffer<PlayerPowerUpContainerSwapCommand>,
                                    RefRW<PlayerPowerUpsConfig>,
                                    RefRW<PlayerPowerUpsState>>().WithEntityAccess())
        {
            if (swapCommands.Length <= 0)
                continue;

            if (!interactionConfigLookup.HasComponent(playerEntity))
            {
                swapCommands.Clear();
                continue;
            }

            PlayerPowerUpContainerStoredStateMode storedStateMode = interactionConfigLookup[playerEntity].StoredStateMode;

            for (int commandIndex = 0; commandIndex < swapCommands.Length; commandIndex++)
            {
                PlayerPowerUpContainerSwapCommand swapCommand = swapCommands[commandIndex];

                if (!droppedContainerContentLookup.HasComponent(swapCommand.ContainerEntity))
                    continue;

                PlayerDroppedPowerUpContainerContent containerContent = droppedContainerContentLookup[swapCommand.ContainerEntity];
                PlayerStoredActivePowerUpData storedPowerUp = containerContent.StoredPowerUp;

                if (!PlayerPowerUpLoadoutRuntimeUtility.TrySwapStoredPowerUpWithSlot(ref storedPowerUp,
                                                                                     swapCommand.TargetSlotIndex,
                                                                                     storedStateMode,
                                                                                     ref powerUpsConfig.ValueRW,
                                                                                     ref powerUpsState.ValueRW,
                                                                                     out bool storedPowerUpConsumed))
                    continue;

                if (storedPowerUpConsumed)
                {
                    commandBuffer.DestroyEntity(swapCommand.ContainerEntity);
                }
                else
                {
                    containerContent.StoredPowerUp = storedPowerUp;
                    droppedContainerContentLookup[swapCommand.ContainerEntity] = containerContent;
                }

                powerUpsState.ValueRW.IsShootingSuppressed = 0;
                powerUpsState.ValueRW.PreviousPrimaryPressed = 0;
                powerUpsState.ValueRW.PreviousSecondaryPressed = 0;
                break;
            }

            swapCommands.Clear();
        }

        commandBuffer.Playback(state.EntityManager);
        commandBuffer.Dispose();
    }
    #endregion

    #endregion
}
