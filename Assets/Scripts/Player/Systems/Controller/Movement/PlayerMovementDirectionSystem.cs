using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

#region Systems
[UpdateInGroup(typeof(PlayerControllerSystemGroup))]
[UpdateAfter(typeof(PlayerInputBridgeSystem))]
public partial struct PlayerMovementDirectionSystem : ISystem
{
    #region Constants
    private const float DigitalAngleEpsilon = 0.001f;
    private const float DigitalInputThreshold = 0.5f;
    private const float DigitalLikeTolerance = 0.12f;
    #endregion

    #region Lifecycle
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<PlayerMovementState>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }
    #endregion

    #region Update
    public void OnUpdate(ref SystemState state)
    {
        Camera camera = Camera.main;
        bool hasCamera = camera != null;
        float3 cameraForward = hasCamera ? (float3)camera.transform.forward : new float3(0f, 0f, 1f);

        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRO<PlayerInputState> inputState,
                  RefRW<PlayerMovementState> movementState,
                  RefRO<PlayerControllerConfig> controllerConfig,
                  RefRO<LocalTransform> localTransform) in SystemAPI.Query<RefRO<PlayerInputState>, RefRW<PlayerMovementState>, RefRO<PlayerControllerConfig>, RefRO<LocalTransform>>())
        {
            ref MovementConfig movementConfig = ref controllerConfig.ValueRO.Config.Value.Movement;

            float2 moveInput = inputState.ValueRO.Move;
            float deadZone = movementConfig.Values.InputDeadZone;
            float releaseGraceSeconds = math.max(0f, movementConfig.Values.DigitalReleaseGraceSeconds);

            if (math.lengthsq(moveInput) <= deadZone * deadZone)
            {
                movementState.ValueRW.DesiredDirection = float3.zero;
                continue;
            }

            float3 playerForward = PlayerControllerMath.NormalizePlanar(math.forward(localTransform.ValueRO.Rotation), new float3(0f, 0f, 1f));
            PlayerControllerMath.GetReferenceBasis(movementConfig.MovementReference, playerForward, cameraForward, hasCamera, out float3 forward, out float3 right);

            float2 resolvedInput = moveInput;

            bool digitalLike = PlayerControllerMath.IsDigitalLike(moveInput, DigitalLikeTolerance);

            if (digitalLike)
                resolvedInput = ResolveDigitalInput(ref movementState.ValueRW, moveInput, elapsedTime, releaseGraceSeconds);

            float2 inputDir = PlayerControllerMath.NormalizeSafe(resolvedInput);
            float3 desiredDirection;

            switch (movementConfig.DirectionsMode)
            {
                case MovementDirectionsMode.DiscreteCount:
                    int count = math.max(1, movementConfig.DiscreteDirectionCount);
                    float step = (math.PI * 2f) / count;
                    float offset = math.radians(movementConfig.DirectionOffsetDegrees);
                    float angle = math.atan2(inputDir.x, inputDir.y);

                    if (digitalLike)
                    {
                        if (PlayerControllerMath.TryGetAlignedDiscreteAngle(angle, step, offset, DigitalAngleEpsilon, out float alignedAngle) == false)
                        {
                            movementState.ValueRW.DesiredDirection = float3.zero;
                            continue;
                        }

                        float3 alignedDirection = PlayerControllerMath.DirectionFromAngle(alignedAngle);
                        desiredDirection = right * alignedDirection.x + forward * alignedDirection.z;
                        break;
                    }

                    float snappedAngle = PlayerControllerMath.QuantizeAngle(angle, step, offset);
                    float3 localDirection = PlayerControllerMath.DirectionFromAngle(snappedAngle);
                    desiredDirection = right * localDirection.x + forward * localDirection.z;
                    break;
                default:
                    desiredDirection = right * inputDir.x + forward * inputDir.y;
                    break;
            }

            movementState.ValueRW.DesiredDirection = math.normalizesafe(desiredDirection);
        }
    }
    #endregion

    #region Helpers
    private static float2 ResolveDigitalInput(ref PlayerMovementState movementState, float2 rawInput, float elapsedTime, float releaseGraceSeconds)
    {
        byte prevMask = movementState.CurrMoveMask;
        byte currMask = PlayerControllerMath.BuildDigitalMask(rawInput, DigitalInputThreshold);
        movementState.PrevMoveMask = prevMask;
        movementState.CurrMoveMask = currMask;

        float4 pressTimes = movementState.MovePressTimes;
        PlayerControllerMath.UpdateDigitalPressTimes(prevMask, currMask, elapsedTime, ref pressTimes);
        movementState.MovePressTimes = pressTimes;

        bool releaseOnly = PlayerControllerMath.IsReleaseOnly(prevMask, currMask);
        bool prevDiagonal = PlayerControllerMath.IsDiagonalMask(prevMask);
        bool currSingle = PlayerControllerMath.IsSingleAxisMask(currMask);

        if (movementState.ReleaseHoldUntilTime > elapsedTime)
        {
            if (currMask != 0 && (currMask & ~movementState.ReleaseHoldMask) == 0)
                return PlayerControllerMath.ResolveDigitalMask(movementState.ReleaseHoldMask, pressTimes);

            movementState.ReleaseHoldUntilTime = 0f;
        }

        if (prevDiagonal && currSingle && releaseOnly && releaseGraceSeconds > 0f)
        {
            movementState.ReleaseHoldMask = prevMask;
            movementState.ReleaseHoldUntilTime = elapsedTime + releaseGraceSeconds;
            return PlayerControllerMath.ResolveDigitalMask(prevMask, pressTimes);
        }

        return PlayerControllerMath.ResolveDigitalMask(currMask, pressTimes);
    }
    #endregion
}
#endregion
