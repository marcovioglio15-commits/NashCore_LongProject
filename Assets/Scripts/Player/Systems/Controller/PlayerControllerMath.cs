using Unity.Mathematics;

#region Utilities
public static class PlayerControllerMath
{
    #region Direction Helpers
    public const byte DigitalUpFlag = 1;
    public const byte DigitalDownFlag = 2;
    public const byte DigitalLeftFlag = 4;
    public const byte DigitalRightFlag = 8;
    public const float DigitalNormalizedDiagonal = 0.70710678f;
    public static float3 NormalizePlanar(float3 direction, float3 fallback)
    {
        direction.y = 0f;

        if (math.lengthsq(direction) < 1e-6f)
            return math.normalizesafe(fallback, new float3(0f, 0f, 1f));

        return math.normalize(direction);
    }

    public static void GetReferenceBasis(ReferenceFrame reference, float3 playerForward, float3 cameraForward, bool hasCamera, out float3 forward, out float3 right)
    {
        float3 baseForward;

        switch (reference)
        {
            case ReferenceFrame.CameraForward:
                if (hasCamera)
                    baseForward = cameraForward;
                else
                    baseForward = playerForward;
                break;
            case ReferenceFrame.PlayerForward:
                baseForward = playerForward;
                break;
            default:
                baseForward = new float3(0f, 0f, 1f);
                break;
        }

        forward = NormalizePlanar(baseForward, new float3(0f, 0f, 1f));
        right = math.normalize(math.cross(new float3(0f, 1f, 0f), forward));
    }

    public static float WrapAngleRadians(float angle)
    {
        return math.atan2(math.sin(angle), math.cos(angle));
    }

    public static float WrapAnglePositive(float angle)
    {
        float wrapped = WrapAngleRadians(angle);

        if (wrapped < 0f)
            wrapped += math.PI * 2f;

        return wrapped;
    }

    public static float QuantizeAngle(float angle, float step, float offset)
    {
        float relative = WrapAngleRadians(angle - offset);
        float snapped = math.round(relative / step) * step + offset;
        return WrapAngleRadians(snapped);
    }

    public static float3 DirectionFromAngle(float angle)
    {
        return new float3(math.sin(angle), 0f, math.cos(angle));
    }

    public static float2 NormalizeSafe(float2 value)
    {
        if (math.lengthsq(value) < 1e-6f)
            return float2.zero;

        return math.normalize(value);
    }

    public static bool IsDigitalInput(float2 value, float epsilon)
    {
        float xRounded = math.round(value.x);
        float yRounded = math.round(value.y);

        if (math.abs(value.x - xRounded) > epsilon)
            return false;

        if (math.abs(value.y - yRounded) > epsilon)
            return false;

        if (math.abs(xRounded) > 1f)
            return false;

        if (math.abs(yRounded) > 1f)
            return false;

        return true;
    }

    public static float2 SnapToDigital(float2 value)
    {
        return new float2(math.round(value.x), math.round(value.y));
    }

    public static bool IsDigitalLike(float2 value, float tolerance)
    {
        float absX = math.abs(value.x);
        float absY = math.abs(value.y);

        if (absX > 1f + tolerance || absY > 1f + tolerance)
            return false;

        if (IsDigitalAxisValue(absX, tolerance) == false)
            return false;

        if (IsDigitalAxisValue(absY, tolerance) == false)
            return false;

        return true;
    }

    public static byte BuildDigitalMask(float2 input, float threshold)
    {
        byte mask = 0;

        if (input.y >= threshold)
            mask |= DigitalUpFlag;
        else if (input.y <= -threshold)
            mask |= DigitalDownFlag;

        if (input.x >= threshold)
            mask |= DigitalRightFlag;
        else if (input.x <= -threshold)
            mask |= DigitalLeftFlag;

        return mask;
    }

    public static void UpdateDigitalPressTimes(byte prevMask, byte currMask, float elapsedTime, ref float4 pressTimes)
    {
        if (WasPressed(prevMask, currMask, DigitalUpFlag))
            pressTimes.x = elapsedTime;

        if (WasPressed(prevMask, currMask, DigitalDownFlag))
            pressTimes.y = elapsedTime;

        if (WasPressed(prevMask, currMask, DigitalLeftFlag))
            pressTimes.z = elapsedTime;

        if (WasPressed(prevMask, currMask, DigitalRightFlag))
            pressTimes.w = elapsedTime;
    }

    public static float2 ResolveDigitalMask(byte mask, float4 pressTimes)
    {
        int horizontal = 0;
        int vertical = 0;

        bool hasLeft = (mask & DigitalLeftFlag) != 0;
        bool hasRight = (mask & DigitalRightFlag) != 0;

        if (hasLeft && hasRight)
        {
            if (pressTimes.w >= pressTimes.z)
                horizontal = 1;
            else
                horizontal = -1;
        }
        else if (hasRight)
            horizontal = 1;
        else if (hasLeft)
            horizontal = -1;

        bool hasUp = (mask & DigitalUpFlag) != 0;
        bool hasDown = (mask & DigitalDownFlag) != 0;

        if (hasUp && hasDown)
        {
            if (pressTimes.x >= pressTimes.y)
                vertical = 1;
            else
                vertical = -1;
        }
        else if (hasUp)
            vertical = 1;
        else if (hasDown)
            vertical = -1;

        return new float2(horizontal, vertical);
    }

    public static bool IsDiagonalMask(byte mask)
    {
        if (HasHorizontal(mask) == false)
            return false;

        if (HasVertical(mask) == false)
            return false;

        return true;
    }

    public static bool IsSingleAxisMask(byte mask)
    {
        if (mask == 0)
            return false;

        bool hasHorizontal = HasHorizontal(mask);
        bool hasVertical = HasVertical(mask);

        return hasHorizontal ^ hasVertical;
    }

    public static bool IsReleaseOnly(byte prevMask, byte currMask)
    {
        if (prevMask == currMask)
            return false;

        if ((currMask & ~prevMask) != 0)
            return false;

        return true;
    }

    public static bool TryGetAlignedDiscreteAngle(float angle, float step, float offset, float epsilon, out float alignedAngle)
    {
        alignedAngle = QuantizeAngle(angle, step, offset);
        float delta = math.abs(WrapAngleRadians(angle - alignedAngle));

        if (delta <= epsilon)
            return true;

        return false;
    }
    #endregion

    #region Digital Helpers
    private static bool IsDigitalAxisValue(float value, float tolerance)
    {
        if (value <= tolerance)
            return true;

        if (math.abs(value - 1f) <= tolerance)
            return true;

        if (math.abs(value - DigitalNormalizedDiagonal) <= tolerance)
            return true;

        return false;
    }

    private static bool WasPressed(byte prevMask, byte currMask, byte flag)
    {
        if ((prevMask & flag) != 0)
            return false;

        if ((currMask & flag) == 0)
            return false;

        return true;
    }

    private static bool HasHorizontal(byte mask)
    {
        if ((mask & DigitalLeftFlag) != 0)
            return true;

        if ((mask & DigitalRightFlag) != 0)
            return true;

        return false;
    }

    private static bool HasVertical(byte mask)
    {
        if ((mask & DigitalUpFlag) != 0)
            return true;

        if ((mask & DigitalDownFlag) != 0)
            return true;

        return false;
    }
    #endregion

    #region Camera Helpers
    public static float3 SmoothCameraPosition(float3 current, float3 target, in CameraValuesBlob values, float deltaTime)
    {
        float3 toTarget = target - current;
        float distance = math.length(toTarget);

        if (distance <= values.DeadZoneRadius)
            return current;

        if (values.MaxFollowDistance > 0f && distance > values.MaxFollowDistance)
        {
            current = target - (toTarget / distance) * values.MaxFollowDistance;
            toTarget = target - current;
            distance = values.MaxFollowDistance;
        }

        float smooth = math.max(0.0001f, values.CameraLag + values.Damping);
        float t = 1f - math.exp(-(values.FollowSpeed * deltaTime) / smooth);
        t = math.clamp(t, 0f, 1f);

        return current + toTarget * t;
    }
    #endregion
}
#endregion
