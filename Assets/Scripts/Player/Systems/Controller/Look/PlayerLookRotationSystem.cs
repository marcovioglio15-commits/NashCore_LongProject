using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerLookMultiplierSystem))]
public partial struct PlayerLookRotationSystem : ISystem
{
    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerLookState>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<PlayerLookState> lookState,
                  RefRW<LocalTransform> localTransform,
                  RefRO<PlayerControllerConfig> controllerConfig) in SystemAPI.Query<RefRW<PlayerLookState>, RefRW<LocalTransform>, RefRO<PlayerControllerConfig>>())
        {
            ref LookConfig lookConfig = ref controllerConfig.ValueRO.Config.Value.Look;

            float3 currentForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRO.Rotation), new float3(0f, 0f, 1f));
            float3 desiredForward = PlayerControllerMath.NormalizePlanar(lookState.ValueRO.DesiredDirection, currentForward);

            if (lookConfig.DirectionsMode == LookDirectionsMode.FollowMovementDirection)
            {
                localTransform.ValueRW.Rotation = quaternion.LookRotationSafe(desiredForward, new float3(0f, 1f, 0f));
                lookState.ValueRW.CurrentDirection = desiredForward;
                lookState.ValueRW.AngularSpeed = 0f;
                continue;
            }

            switch (lookConfig.RotationMode)
            {
                case RotationMode.SnapToAllowedDirections:
                    localTransform.ValueRW.Rotation = quaternion.LookRotationSafe(desiredForward, new float3(0f, 1f, 0f));
                    lookState.ValueRW.CurrentDirection = desiredForward;
                    lookState.ValueRW.AngularSpeed = 0f;
                    continue;
            }

            float angle = SignedAngleRadians(currentForward, desiredForward);
            float absAngle = math.abs(angle);

            if (absAngle < 1e-5f)
            {
                lookState.ValueRW.CurrentDirection = currentForward;
                lookState.ValueRW.AngularSpeed = 0f;
                continue;
            }

            float targetSpeedDeg = lookConfig.RotationSpeed;
            float maxSpeedDeg = lookConfig.Values.RotationMaxSpeed;

            if (targetSpeedDeg <= 0f)
                targetSpeedDeg = maxSpeedDeg;

            if (maxSpeedDeg > 0f)
                targetSpeedDeg = math.min(targetSpeedDeg, maxSpeedDeg);

            float angularSpeedDeg = lookState.ValueRO.AngularSpeed;
            float damping = lookConfig.Values.RotationDamping;

            if (damping > 0f)
                angularSpeedDeg = math.lerp(angularSpeedDeg, targetSpeedDeg, 1f - math.exp(-deltaTime / damping));
            else
                angularSpeedDeg = targetSpeedDeg;

            if (maxSpeedDeg > 0f)
                angularSpeedDeg = math.min(angularSpeedDeg, maxSpeedDeg);

            float maxStep = math.radians(angularSpeedDeg) * deltaTime;

            if (maxStep <= 0f)
            {
                lookState.ValueRW.CurrentDirection = currentForward;
                lookState.ValueRW.AngularSpeed = 0f;
                continue;
            }

            float step = math.min(absAngle, maxStep);
            float signedStep = step * math.sign(angle);

            if (step > 1e-6f)
            {
                quaternion deltaRotation = quaternion.AxisAngle(new float3(0f, 1f, 0f), signedStep);
                localTransform.ValueRW.Rotation = math.normalize(math.mul(deltaRotation, localTransform.ValueRO.Rotation));
            }
            else
            {
                localTransform.ValueRW.Rotation = quaternion.LookRotationSafe(desiredForward, new float3(0f, 1f, 0f));
            }

            float3 newForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRW.Rotation), desiredForward);
            lookState.ValueRW.CurrentDirection = newForward;
            lookState.ValueRW.AngularSpeed = angularSpeedDeg;
        }
    }
    #endregion

    #region Helpers
    private static float SignedAngleRadians(float3 from, float3 to)
    {
        float3 cross = math.cross(from, to);
        float sign = math.sign(math.dot(cross, new float3(0f, 1f, 0f)));
        float dot = math.clamp(math.dot(from, to), -1f, 1f);
        float angle = math.acos(dot);
        return angle * sign;
    }
    #endregion
}
#endregion
