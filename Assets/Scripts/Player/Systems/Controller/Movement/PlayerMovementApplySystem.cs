using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementSpeedSystem))]
public partial struct PlayerMovementApplySystem : ISystem
{
    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<LocalTransform>();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<LocalTransform> localTransform, RefRO<PlayerMovementState> movementState) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerMovementState>>())
        {
            float3 displacement = movementState.ValueRO.Velocity * deltaTime;

            if (math.lengthsq(displacement) < 1e-8f)
                continue;

            localTransform.ValueRW.Position += displacement;
        }
    }
    #endregion
}
#endregion
