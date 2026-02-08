using Unity.Entities;
using Unity.Mathematics;


/// <summary>
/// This system calculates the player's movement speed based on input 
/// direction, acceleration, deceleration, and any active modifiers. 
/// It updates the player's velocity accordingly, ensuring that it respects the configured 
/// base speed, maximum speed, and acceleration/deceleration rates. 
/// The system runs after the PlayerMovementDirectionSystem and PlayerLookMultiplierSystem 
/// to ensure that it has the necessary input and modifier data available for accurate speed calculation.
/// </summary>
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerMovementDirectionSystem))]
[UpdateAfter(typeof(PlayerLookMultiplierSystem))]
public partial struct PlayerMovementSpeedSystem : ISystem
{
    #region Lifecycle
    /// <summary>
    /// Configures the system to require updates for entities that have 
    /// PlayerMovementState, PlayerMovementModifiers, and PlayerControllerConfig components.
    /// </summary>
    /// <param name="state"></param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerMovementModifiers>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    /// <summary>
    /// Updates the player's movement speed based on the desired direction, current velocity,
    /// and the movement configuration. 
    /// It applies acceleration when there is input and deceleration when there is no input,
    /// while respecting any active speed modifiers. 
    /// The resulting velocity is then stored back in the PlayerMovementState component.
    /// </summary>
    /// <param name="state"></param>
    public void OnUpdate(ref SystemState state)
    {
        // Get the time delta for this frame to ensure smooth acceleration and deceleration
        float deltaTime = SystemAPI.Time.DeltaTime;

        // Iterate through all entities that have the required components for movement speed calculation
        foreach ((RefRW<PlayerMovementState> movementState,
                  RefRO<PlayerMovementModifiers> modifiers,
                  RefRO<PlayerControllerConfig> controllerConfig) in SystemAPI.Query<RefRW<PlayerMovementState>, RefRO<PlayerMovementModifiers>, RefRO<PlayerControllerConfig>>())
        {
            // Get the movement configuration from the controller config blob asset
            ref MovementConfig movementConfig = ref controllerConfig.ValueRO.Config.Value.Movement;

            // Get the desired movement direction from the movement state
            float3 desiredDirection = movementState.ValueRO.DesiredDirection;
            bool hasInput = math.lengthsq(desiredDirection) > 1e-6f;

            // Calculate the effective speed and acceleration multipliers from the modifiers, ensuring they are not negative
            float speedMultiplier = math.max(0f, modifiers.ValueRO.MaxSpeedMultiplier);
            float accelerationMultiplier = math.max(0f, modifiers.ValueRO.AccelerationMultiplier);
            bool forceZeroSpeed = speedMultiplier <= 0f;


            // Calculate the base speed, maximum speed, acceleration,
            // and deceleration values from the movement configuration,
            // applying the multipliers
            float baseSpeed = movementConfig.Values.BaseSpeed * speedMultiplier;
            float maxSpeed = movementConfig.Values.MaxSpeed * speedMultiplier;
            float acceleration = movementConfig.Values.Acceleration * accelerationMultiplier;
            float deceleration = movementConfig.Values.Deceleration;
            bool hasMaxSpeed = movementConfig.Values.MaxSpeed > 0f;

            // Ensure that the base speed does not exceed the maximum speed if a maximum speed is defined
            if (maxSpeed > 0f && baseSpeed > maxSpeed)
                baseSpeed = maxSpeed;


            // Get the current velocity from the movement state and calculate the current speed and direction
            float3 currentVelocity = movementState.ValueRO.Velocity;
            float currentSpeed = math.length(currentVelocity);
            float3 currentDirection = float3.zero;

            // If the current speed is above a small threshold,
            // calculate the current direction as a normalized vector of the velocity
            if (currentSpeed > 1e-6f)
                currentDirection = currentVelocity / currentSpeed;

            // If the speed multiplier is zero or negative, force the current speed to zero
            if (forceZeroSpeed)
            {
                currentSpeed = 0f;
            }
            // Otherwise, if there is input, apply acceleration towards the desired direction,
            else
            {
                if (hasInput)
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

                    currentDirection = desiredDirection;
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

            // Calculate the new velocity based on the current direction and speed,
            float3 velocity = float3.zero;

            // If the current speed is above a small threshold and the current direction is valid,
            // calculate the new velocity as the current direction multiplied by the current speed
            if (currentSpeed > 1e-6f && math.lengthsq(currentDirection) > 1e-6f)
                velocity = currentDirection * currentSpeed;

            // Update the movement state with the new velocity
            movementState.ValueRW.Velocity = velocity;
        }
    }
    #endregion


}
