using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds the continuous camera-facing Laser Beam ribbon mesh from authoritative lane samples.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerLaserBeamPresentationRuntimeMeshUtility
{
    #region Fields
    private static readonly List<Vector3> sharedVertices = new List<Vector3>(256);
    private static readonly List<int> sharedTriangles = new List<int>(384);
    private static readonly List<Vector2> sharedUvs = new List<Vector2>(256);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds one managed body visual as a continuous ribbon that follows the authoritative lane path.
    /// /params visual Managed body visual that owns the dynamic mesh.
    /// /params laneVisual Render-time lane metadata describing the point range.
    /// /params ribbonPoints Shared ribbon point list containing all sampled lane points.
    /// /params visualConfig Shared beam visual config used to shape the integrated splash.
    /// /params laserBeamConfig Runtime passive config driving pulse and breathing response.
    /// /params laserBeamState Runtime pulse state used to animate travelling damage rings.
    /// /params presentationCamera Camera used to orient the ribbon toward the current gameplay view.
    /// /params elapsedTimeSeconds Global elapsed time used by the secondary breathing animation.
    /// /returns None.
    /// </summary>
    public static void BuildBodyRibbonMesh(PlayerLaserBeamManagedBodyVisual visual,
                                           in PlayerLaserBeamLaneVisual laneVisual,
                                           List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                           in PlayerLaserBeamVisualConfig visualConfig,
                                           in LaserBeamPassiveConfig laserBeamConfig,
                                           in PlayerLaserBeamState laserBeamState,
                                           Camera presentationCamera,
                                           float elapsedTimeSeconds)
    {
        if (visual == null || visual.DynamicMesh == null)
            return;

        if (laneVisual.PointCount < 2)
        {
            visual.DynamicMesh.Clear(false);
            return;
        }

        sharedVertices.Clear();
        sharedTriangles.Clear();
        sharedUvs.Clear();

        float laneLength = math.max(visualConfig.MinimumSegmentLength, laneVisual.TotalLength);
        float primaryPulseProgress = ResolvePulseProgress(laserBeamState.HasPrimaryTickPulse != 0,
                                                         laserBeamState.PrimaryTickPulseElapsedSeconds,
                                                         laserBeamConfig.TickPulseTravelSpeed,
                                                         laneLength);
        float secondaryPulseProgress = ResolvePulseProgress(laserBeamState.HasSecondaryTickPulse != 0,
                                                           laserBeamState.SecondaryTickPulseElapsedSeconds,
                                                           laserBeamConfig.TickPulseTravelSpeed,
                                                           laneLength);
        float pulseHalfLengthNormalized = math.max(0.0025f,
                                                   math.max(0.01f, laserBeamConfig.TickPulseLength) / laneLength * 0.5f);
        float3 cameraForward = ResolveCameraForward(presentationCamera);
        float3 minimumBounds = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
        float3 maximumBounds = new float3(float.MinValue, float.MinValue, float.MinValue);

        for (int pointIndex = 0; pointIndex < laneVisual.PointCount; pointIndex++)
        {
            int absolutePointIndex = laneVisual.PointStartIndex + pointIndex;
            PlayerLaserBeamRibbonPoint point = ribbonPoints[absolutePointIndex];
            float3 tangent = ResolvePointTangent(ribbonPoints,
                                                 laneVisual.PointStartIndex,
                                                 laneVisual.PointCount,
                                                 pointIndex);
            float3 widthAxis = ResolveWidthAxis(tangent, cameraForward);
            float normalizedDistance = point.Distance / laneLength;
            float primaryPulseMask = ResolvePulseMask(normalizedDistance, primaryPulseProgress, pulseHalfLengthNormalized);
            float secondaryPulseMask = ResolvePulseMask(normalizedDistance, secondaryPulseProgress, pulseHalfLengthNormalized);
            float resolvedWidth = ResolveRibbonWidth(point,
                                                     normalizedDistance,
                                                     laneLength,
                                                     primaryPulseMask,
                                                     secondaryPulseMask,
                                                     in visualConfig,
                                                     in laneVisual,
                                                     in laserBeamConfig,
                                                     elapsedTimeSeconds);
            float3 halfOffset = widthAxis * (resolvedWidth * 0.5f);
            float3 leftVertex = point.Position - halfOffset;
            float3 rightVertex = point.Position + halfOffset;
            sharedVertices.Add(ToVector3(leftVertex));
            sharedVertices.Add(ToVector3(rightVertex));
            sharedUvs.Add(new Vector2(normalizedDistance, 0f));
            sharedUvs.Add(new Vector2(normalizedDistance, 1f));
            ExpandBounds(leftVertex, ref minimumBounds, ref maximumBounds);
            ExpandBounds(rightVertex, ref minimumBounds, ref maximumBounds);

            if (pointIndex <= 0)
                continue;

            int baseVertexIndex = pointIndex * 2;
            sharedTriangles.Add(baseVertexIndex - 2);
            sharedTriangles.Add(baseVertexIndex);
            sharedTriangles.Add(baseVertexIndex - 1);
            sharedTriangles.Add(baseVertexIndex - 1);
            sharedTriangles.Add(baseVertexIndex);
            sharedTriangles.Add(baseVertexIndex + 1);
        }

        Mesh dynamicMesh = visual.DynamicMesh;
        dynamicMesh.Clear(false);
        dynamicMesh.SetVertices(sharedVertices);
        dynamicMesh.SetUVs(0, sharedUvs);
        dynamicMesh.SetTriangles(sharedTriangles, 0, false);
        dynamicMesh.bounds = BuildBounds(minimumBounds, maximumBounds);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves the travelling pulse progress for one stored pulse age.
    /// /params hasPulse True when the pulse is currently valid.
    /// /params elapsedSeconds Pulse age in seconds.
    /// /params travelSpeed World-space pulse travel speed.
    /// /params laneLength Total length of the current lane.
    /// /returns Normalized pulse progress, or -1 when inactive.
    /// </summary>
    private static float ResolvePulseProgress(bool hasPulse,
                                              float elapsedSeconds,
                                              float travelSpeed,
                                              float laneLength)
    {
        if (!hasPulse || travelSpeed <= 0f || laneLength <= 0f)
            return -1f;

        return math.max(0f, elapsedSeconds) * travelSpeed / laneLength;
    }

    /// <summary>
    /// Resolves a smooth bell-shaped pulse mask centered around one normalized progress value.
    /// /params normalizedDistance Current normalized point distance along the lane.
    /// /params pulseProgress Normalized pulse center.
    /// /params pulseHalfLengthNormalized Normalized half-length of the pulse ring.
    /// /returns Smooth pulse mask in the 0-1 range.
    /// </summary>
    private static float ResolvePulseMask(float normalizedDistance,
                                          float pulseProgress,
                                          float pulseHalfLengthNormalized)
    {
        if (pulseProgress < 0f)
            return 0f;

        float normalizedOffset = math.abs(normalizedDistance - pulseProgress) / math.max(0.0001f, pulseHalfLengthNormalized);
        float clampedOffset = math.saturate(1f - normalizedOffset);
        return clampedOffset * clampedOffset * (3f - 2f * clampedOffset);
    }

    /// <summary>
    /// Resolves the camera-facing width axis for one ribbon point tangent.
    /// /params tangent Lane tangent at the current point.
    /// /params cameraForward Current gameplay camera forward.
    /// /returns Normalized width axis.
    /// </summary>
    private static float3 ResolveWidthAxis(float3 tangent, float3 cameraForward)
    {
        float3 widthAxis = math.cross(tangent, cameraForward);

        if (math.lengthsq(widthAxis) <= 1e-6f)
            widthAxis = math.cross(tangent, new float3(0f, 1f, 0f));

        if (math.lengthsq(widthAxis) <= 1e-6f)
            widthAxis = new float3(1f, 0f, 0f);

        return math.normalizesafe(widthAxis, new float3(1f, 0f, 0f));
    }

    /// <summary>
    /// Resolves the tangent used to orient one ribbon point between its neighbors.
    /// /params ribbonPoints Shared ribbon point list.
    /// /params pointStartIndex Start index of the current lane inside the shared point list.
    /// /params pointCount Number of points belonging to the current lane.
    /// /params localPointIndex Zero-based point index inside the current lane.
    /// /returns Normalized tangent.
    /// </summary>
    private static float3 ResolvePointTangent(List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                              int pointStartIndex,
                                              int pointCount,
                                              int localPointIndex)
    {
        int previousLocalIndex = math.max(0, localPointIndex - 1);
        int nextLocalIndex = math.min(pointCount - 1, localPointIndex + 1);
        float3 previousPoint = ribbonPoints[pointStartIndex + previousLocalIndex].Position;
        float3 nextPoint = ribbonPoints[pointStartIndex + nextLocalIndex].Position;
        return math.normalizesafe(nextPoint - previousPoint, new float3(0f, 0f, 1f));
    }

    /// <summary>
    /// Resolves the final ribbon width at one point after breathing, travelling pulses, and terminal widening are applied.
    /// /params point Current ribbon point.
    /// /params normalizedDistance Current normalized point distance along the lane.
    /// /params laneLength Total length of the current lane.
    /// /params primaryPulseMask Primary travelling pulse mask.
    /// /params secondaryPulseMask Secondary travelling pulse mask.
    /// /params visualConfig Shared visual config used by the integrated splash.
    /// /params laneVisual Render-time lane metadata.
    /// /params laserBeamConfig Runtime passive config.
    /// /params elapsedTimeSeconds Global elapsed time.
    /// /returns Final full ribbon width.
    /// </summary>
    private static float ResolveRibbonWidth(PlayerLaserBeamRibbonPoint point,
                                            float normalizedDistance,
                                            float laneLength,
                                            float primaryPulseMask,
                                            float secondaryPulseMask,
                                            in PlayerLaserBeamVisualConfig visualConfig,
                                            in PlayerLaserBeamLaneVisual laneVisual,
                                            in LaserBeamPassiveConfig laserBeamConfig,
                                            float elapsedTimeSeconds)
    {
        float baseWidth = ResolveBodyVisualWidth(point.Width);
        float breathingWave = math.sin(elapsedTimeSeconds * 5.8f - point.Distance * 4.25f + laneVisual.LaneIndex * 0.37f);
        float breathingMultiplier = 1f + laserBeamConfig.WobbleAmplitude * 0.45f * breathingWave;
        float pulseWidthMultiplier = 1f +
                                     primaryPulseMask * laserBeamConfig.TickPulseWidthBoost +
                                     secondaryPulseMask * laserBeamConfig.TickPulseWidthBoost * 0.72f;
        float splashMultiplier = ResolveTerminalSplashMultiplier(point.Distance,
                                                                 laneLength,
                                                                 baseWidth,
                                                                 in visualConfig,
                                                                 in laserBeamConfig);
        float normalizedBodyProfileMultiplier = ResolveBodyProfileWidthMultiplier(laserBeamConfig.BodyProfile,
                                                                                  normalizedDistance);
        return math.max(0.025f,
                        baseWidth *
                        math.max(0.25f, breathingMultiplier) *
                        pulseWidthMultiplier *
                        splashMultiplier *
                        normalizedBodyProfileMultiplier);
    }

    /// <summary>
    /// Resolves the integrated terminal widening applied near the lane end so the body flows into the splash instead of ending abruptly.
    /// /params distanceAlongLane Current point distance.
    /// /params laneLength Total lane length.
    /// /params baseWidth Current base ribbon width before splash widening.
    /// /params visualConfig Shared visual config used to shape the widening.
    /// /params laserBeamConfig Runtime passive config used to scale the terminal emphasis.
    /// /returns Width multiplier applied near the terminal section.
    /// </summary>
    private static float ResolveTerminalSplashMultiplier(float distanceAlongLane,
                                                         float laneLength,
                                                         float baseWidth,
                                                         in PlayerLaserBeamVisualConfig visualConfig,
                                                         in LaserBeamPassiveConfig laserBeamConfig)
    {
        float terminalLength = math.clamp(baseWidth *
                                          math.sqrt(math.max(0.01f, laserBeamConfig.ImpactScaleMultiplier)) *
                                          visualConfig.TerminalSplashLengthMultiplier *
                                          5.5f,
                                          0.18f,
                                          laneLength * 0.45f);
        float terminalStartDistance = math.max(0f, laneLength - terminalLength);

        if (distanceAlongLane <= terminalStartDistance)
            return 1f;

        float wideningInterpolation = math.saturate((distanceAlongLane - terminalStartDistance) / math.max(0.0001f, laneLength - terminalStartDistance));
        float smoothInterpolation = wideningInterpolation * wideningInterpolation * (3f - 2f * wideningInterpolation);
        float maximumMultiplier = math.max(1f,
                                           math.sqrt(math.max(0.01f, laserBeamConfig.ImpactScaleMultiplier)) *
                                           visualConfig.TerminalSplashWidthMultiplier);
        return math.lerp(1f, maximumMultiplier, smoothInterpolation);
    }

    /// <summary>
    /// Resolves small profile-driven width differences so the body profile enum still changes the beam silhouette without fragmenting the ribbon.
    /// /params bodyProfile Runtime body profile selector.
    /// /params normalizedDistance Current normalized point distance along the lane.
    /// /returns Width multiplier derived from the active body profile.
    /// </summary>
    private static float ResolveBodyProfileWidthMultiplier(LaserBeamBodyProfile bodyProfile,
                                                           float normalizedDistance)
    {
        switch (bodyProfile)
        {
            case LaserBeamBodyProfile.TaperedJet:
                return math.lerp(0.9f, 1.08f, normalizedDistance);
            case LaserBeamBodyProfile.DenseRibbon:
                return 1.14f;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Compresses the raw gameplay width into a readable art width that remains stable in crowded rooms.
    /// /params rawWidth Raw body width inherited from gameplay lane generation.
    /// /returns Compressed art width used by the ribbon mesh.
    /// </summary>
    public static float ResolveBodyVisualWidth(float rawWidth)
    {
        float compressedWidth = 0.44f * math.sqrt(math.max(0.01f, rawWidth));
        return math.clamp(compressedWidth, 0.11f, 0.38f);
    }

    /// <summary>
    /// Resolves the camera forward vector used to orient the ribbon towards the gameplay camera.
    /// /params presentationCamera Active gameplay camera when available.
    /// /returns Normalized camera forward vector.
    /// </summary>
    private static float3 ResolveCameraForward(Camera presentationCamera)
    {
        if (presentationCamera != null)
            return presentationCamera.transform.forward;

        return new float3(0f, -1f, 0f);
    }

    /// <summary>
    /// Expands the mesh bounds with one new vertex.
    /// /params point New point to include.
    /// /params minimumBounds Current minimum bounds.
    /// /params maximumBounds Current maximum bounds.
    /// /returns None.
    /// </summary>
    private static void ExpandBounds(float3 point,
                                     ref float3 minimumBounds,
                                     ref float3 maximumBounds)
    {
        minimumBounds = math.min(minimumBounds, point);
        maximumBounds = math.max(maximumBounds, point);
    }

    /// <summary>
    /// Builds a Unity bounds value from accumulated min and max points.
    /// /params minimumBounds Minimum sampled bounds.
    /// /params maximumBounds Maximum sampled bounds.
    /// /returns Mesh bounds covering the current ribbon.
    /// </summary>
    private static Bounds BuildBounds(float3 minimumBounds, float3 maximumBounds)
    {
        Vector3 minimumVector = ToVector3(minimumBounds);
        Vector3 maximumVector = ToVector3(maximumBounds);
        Bounds bounds = new Bounds((minimumVector + maximumVector) * 0.5f,
                                   maximumVector - minimumVector);

        if (bounds.size.sqrMagnitude <= 0f)
            bounds.Expand(0.05f);

        return bounds;
    }

    /// <summary>
    /// Converts one ECS float3 into a managed Unity Vector3.
    /// /params value ECS float3 value.
    /// /returns Managed Unity Vector3.
    /// </summary>
    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }
    #endregion

    #endregion
}
