using Unity.Entities;
using Unity.Mathematics;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementDirectionSystem))]
[UpdateAfter(typeof(PlayerLookMultiplierSystem))]
public partial struct PlayerMovementSpeedSystem : ISystem
{
    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerMovementModifiers>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<PlayerMovementState> movementState,
                  RefRO<PlayerMovementModifiers> modifiers,
                  RefRO<PlayerControllerConfig> controllerConfig) in SystemAPI.Query<RefRW<PlayerMovementState>, RefRO<PlayerMovementModifiers>, RefRO<PlayerControllerConfig>>())
        {
            ref MovementConfig movementConfig = ref controllerConfig.ValueRO.Config.Value.Movement;

            float3 desiredDirection = movementState.ValueRO.DesiredDirection;
            bool hasInput = math.lengthsq(desiredDirection) > 1e-6f;

            float speedMultiplier = math.max(0f, modifiers.ValueRO.MaxSpeedMultiplier);
            float accelerationMultiplier = math.max(0f, modifiers.ValueRO.AccelerationMultiplier);
            bool forceZeroSpeed = speedMultiplier <= 0f;

            float baseSpeed = movementConfig.Values.BaseSpeed * speedMultiplier;
            float maxSpeed = movementConfig.Values.MaxSpeed * speedMultiplier;
            float acceleration = movementConfig.Values.Acceleration * accelerationMultiplier;
            float deceleration = movementConfig.Values.Deceleration;
            bool hasMaxSpeed = movementConfig.Values.MaxSpeed > 0f;

            if (maxSpeed > 0f && baseSpeed > maxSpeed)
                baseSpeed = maxSpeed;

            float3 currentVelocity = movementState.ValueRO.Velocity;
            float currentSpeed = math.length(currentVelocity);
            float3 currentDirection = float3.zero;

            if (currentSpeed > 1e-6f)
                currentDirection = currentVelocity / currentSpeed;

            if (hasInput)
            {
                if (forceZeroSpeed)
                {
                    currentSpeed = 0f;
                }
                else
                {
                    if (baseSpeed > 0f && currentSpeed < baseSpeed)
                        currentSpeed = baseSpeed;

                    if (acceleration < 0f)
                    {
                        if (hasMaxSpeed)
                            currentSpeed = maxSpeed;
                    }
                    else
                    {
                        currentSpeed += acceleration * deltaTime;

                        if (hasMaxSpeed)
                            currentSpeed = math.min(currentSpeed, maxSpeed);
                    }
                }

                currentDirection = desiredDirection;
            }
            else
            {
                if (forceZeroSpeed)
                {
                    currentSpeed = 0f;
                }
                else
                {
                    if (deceleration < 0f)
                    {
                        currentSpeed = 0f;
                    }
                    else
                    {
                        currentSpeed -= deceleration * deltaTime;

                        if (currentSpeed < 0f)
                            currentSpeed = 0f;
                    }
                }
            }

            float3 velocity = float3.zero;

            if (currentSpeed > 1e-6f && math.lengthsq(currentDirection) > 1e-6f)
                velocity = currentDirection * currentSpeed;

            movementState.ValueRW.Velocity = velocity;
        }
    }
    #endregion

}
#endregion
