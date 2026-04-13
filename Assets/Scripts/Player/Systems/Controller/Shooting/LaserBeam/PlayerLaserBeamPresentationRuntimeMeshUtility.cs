using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds the volumetric 3D Laser Beam body mesh from authoritative lane samples.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerLaserBeamPresentationRuntimeMeshUtility
{
    #region Constants
    private const int TubeSideCount = 6;
    private const float MinimumTubeRadius = 0.01f;
    private const float SourceInitialApertureScale = 0.48f;
    private const float SourceMidApertureScale = 0.82f;
    #endregion

    #region Fields
    private static readonly float angleStepRadians = math.PI * 2f / TubeSideCount;
    private static readonly List<Vector3> sharedVertices = new List<Vector3>(768);
    private static readonly List<Vector3> sharedNormals = new List<Vector3>(768);
    private static readonly List<int> sharedTriangles = new List<int>(1536);
    private static readonly List<Vector2> sharedUvs = new List<Vector2>(768);
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Rebuilds one managed body visual as a volumetric prism-tube that follows the authoritative lane path.
    /// /params visual Managed body visual that owns the dynamic mesh.
    /// /params laneVisual Render-time lane metadata describing the point range.
    /// /params ribbonPoints Shared point list containing all sampled lane points.
    /// /params visualConfig Shared beam visual config used to shape the terminal closure.
    /// /params laserBeamConfig Runtime passive config driving width and storm response.
    /// /params laserBeamState Runtime state used to resolve the current storm-burst strength.
    /// /params elapsedTimeSeconds Global elapsed time used by the body breathing animation.
    /// /returns None.
    /// </summary>
    public static void BuildBodyVolumeMesh(PlayerLaserBeamManagedBodyVisual visual,
                                           in PlayerLaserBeamLaneVisual laneVisual,
                                           List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                           in PlayerLaserBeamVisualConfig visualConfig,
                                           in LaserBeamPassiveConfig laserBeamConfig,
                                           in PlayerLaserBeamState laserBeamState,
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
        sharedNormals.Clear();
        sharedTriangles.Clear();
        sharedUvs.Clear();

        float laneLength = math.max(visualConfig.MinimumSegmentLength, laneVisual.TotalLength);
        float stormBurstNormalized = ResolveStormBurstNormalized(in laserBeamConfig, in laserBeamState);
        float3 minimumBounds = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
        float3 maximumBounds = new float3(float.MinValue, float.MinValue, float.MinValue);
        float3 transportedNormal = ResolveInitialFrameNormal(ribbonPoints,
                                                             laneVisual.PointStartIndex,
                                                             laneVisual.PointCount);
        float3 firstTangent = ResolvePointTangent(ribbonPoints,
                                                  laneVisual.PointStartIndex,
                                                  laneVisual.PointCount,
                                                  0);
        float3 lastTangent = firstTangent;
        int firstRingStartIndex = -1;
        int previousRingStartIndex = -1;

        // Build one shaped ring for each sampled point along the authoritative lane.
        for (int pointIndex = 0; pointIndex < laneVisual.PointCount; pointIndex++)
        {
            int absolutePointIndex = laneVisual.PointStartIndex + pointIndex;
            PlayerLaserBeamRibbonPoint point = ribbonPoints[absolutePointIndex];
            float3 tangent = ResolvePointTangent(ribbonPoints,
                                                 laneVisual.PointStartIndex,
                                                 laneVisual.PointCount,
                                                 pointIndex);

            if (pointIndex > 0)
                transportedNormal = TransportFrameNormal(transportedNormal, tangent);

            float3 binormal = ResolveFrameBinormal(transportedNormal, tangent);
            transportedNormal = ResolveFrameNormal(binormal, tangent);
            float normalizedDistance = math.saturate(point.Distance / laneLength);
            float diameter = ResolveTubeDiameter(point,
                                                 normalizedDistance,
                                                 laneLength,
                                                 stormBurstNormalized,
                                                 in visualConfig,
                                                 in laneVisual,
                                                 in laserBeamConfig,
                                                 elapsedTimeSeconds);
            int ringStartIndex = AddTubeRing(point.Position,
                                             tangent,
                                             transportedNormal,
                                             binormal,
                                             diameter * 0.5f,
                                             normalizedDistance,
                                             in laserBeamConfig,
                                             ref minimumBounds,
                                             ref maximumBounds);

            if (pointIndex == 0)
            {
                firstRingStartIndex = ringStartIndex;
                AddStartCap(point.Position,
                            tangent,
                            ringStartIndex,
                            ref minimumBounds,
                            ref maximumBounds);
            }

            if (previousRingStartIndex >= 0)
                AddTubeBridge(previousRingStartIndex, ringStartIndex);

            previousRingStartIndex = ringStartIndex;
            lastTangent = tangent;
        }

        // Close the final ring with a rounded cap instead of a pointed comet tip.
        if (previousRingStartIndex >= 0 && firstRingStartIndex >= 0)
            AddEndCap(ribbonPoints[laneVisual.PointStartIndex + laneVisual.PointCount - 1].Position,
                      lastTangent,
                      previousRingStartIndex,
                      ref minimumBounds,
                      ref maximumBounds);

        Mesh dynamicMesh = visual.DynamicMesh;
        dynamicMesh.Clear(false);
        dynamicMesh.SetVertices(sharedVertices);
        dynamicMesh.SetNormals(sharedNormals);
        dynamicMesh.SetUVs(0, sharedUvs);
        dynamicMesh.SetTriangles(sharedTriangles, 0, false);
        dynamicMesh.bounds = BuildBounds(minimumBounds, maximumBounds);
    }

    /// <summary>
    /// Compresses the raw gameplay width into a readable art width that remains stable in crowded rooms.
    /// /params rawWidth Raw body width inherited from gameplay lane generation.
    /// /returns Compressed art width used by the body mesh.
    /// </summary>
    public static float ResolveBodyVisualWidth(float rawWidth)
    {
        float compressedWidth = 0.12f + 0.32f * math.pow(math.max(0.01f, rawWidth), 0.62f);
        return math.clamp(compressedWidth, 0.11f, 1.45f);
    }

    /// <summary>
    /// Resolves the normalized storm-burst amount currently active on the beam.
    /// /params laserBeamConfig Runtime passive config that provides the authored burst duration.
    /// /params laserBeamState Runtime state that stores the current burst countdown.
    /// /returns Normalized burst strength in the 0-1 range.
    /// </summary>
    public static float ResolveStormBurstNormalized(in LaserBeamPassiveConfig laserBeamConfig,
                                                    in PlayerLaserBeamState laserBeamState)
    {
        float durationSeconds = PlayerLaserBeamStateUtility.ResolveStormTickTotalDurationSeconds(in laserBeamConfig);

        if (durationSeconds <= 0f)
            return 0f;

        return math.saturate(laserBeamState.StormBurstRemainingSeconds / durationSeconds);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Adds one shaped cross-section ring to the shared body mesh buffers.
    /// /params center Center of the ring.
    /// /params tangent Forward direction at the current ring.
    /// /params normal Vertical frame axis used by the current ring.
    /// /params binormal Horizontal frame axis used by the current ring.
    /// /params radius Radius of the current ring.
    /// /params normalizedDistance Normalized distance of the ring along the lane.
    /// /params laserBeamConfig Runtime passive config used to resolve the active body profile.
    /// /params minimumBounds Current minimum mesh bounds.
    /// /params maximumBounds Current maximum mesh bounds.
    /// /returns Start vertex index of the created ring.
    /// </summary>
    private static int AddTubeRing(float3 center,
                                   float3 tangent,
                                   float3 normal,
                                   float3 binormal,
                                   float radius,
                                   float normalizedDistance,
                                   in LaserBeamPassiveConfig laserBeamConfig,
                                   ref float3 minimumBounds,
                                   ref float3 maximumBounds)
    {
        ResolveBodyProfileShapeScales(laserBeamConfig.BodyProfile,
                                      normalizedDistance,
                                      out float normalAxisScale,
                                      out float binormalAxisScale);
        int ringStartIndex = sharedVertices.Count;

        // Duplicate the seam vertex so the storm helix can scroll cleanly across the circumference.
        for (int sideIndex = 0; sideIndex <= TubeSideCount; sideIndex++)
        {
            float angleRadians = sideIndex * angleStepRadians;
            float cosine = math.cos(angleRadians);
            float sine = math.sin(angleRadians);
            float3 radialOffset = normal * (cosine * radius * normalAxisScale) +
                                  binormal * (sine * radius * binormalAxisScale);
            float3 vertexPosition = center + radialOffset;
            float3 normalDirection = math.normalizesafe(normal * (cosine / math.max(0.001f, normalAxisScale)) +
                                                        binormal * (sine / math.max(0.001f, binormalAxisScale)),
                                                        binormal);
            sharedVertices.Add(ToVector3(vertexPosition));
            sharedNormals.Add(ToVector3(normalDirection));
            sharedUvs.Add(new Vector2(normalizedDistance, sideIndex / (float)TubeSideCount));
            ExpandBounds(vertexPosition, ref minimumBounds, ref maximumBounds);
        }

        return ringStartIndex;
    }

    /// <summary>
    /// Connects two neighboring rings with side-surface triangles.
    /// /params previousRingStartIndex Start index of the previous ring.
    /// /params currentRingStartIndex Start index of the current ring.
    /// /returns None.
    /// </summary>
    private static void AddTubeBridge(int previousRingStartIndex, int currentRingStartIndex)
    {
        for (int sideIndex = 0; sideIndex < TubeSideCount; sideIndex++)
        {
            int previousA = previousRingStartIndex + sideIndex;
            int previousB = previousRingStartIndex + sideIndex + 1;
            int currentA = currentRingStartIndex + sideIndex;
            int currentB = currentRingStartIndex + sideIndex + 1;
            sharedTriangles.Add(previousA);
            sharedTriangles.Add(currentA);
            sharedTriangles.Add(previousB);
            sharedTriangles.Add(previousB);
            sharedTriangles.Add(currentA);
            sharedTriangles.Add(currentB);
        }
    }

    /// <summary>
    /// Closes the beam start with a simple cap so the tube does not appear hollow near the muzzle.
    /// /params center Start-point position.
    /// /params tangent Forward tangent at the first point.
    /// /params firstRingStartIndex Start index of the first body ring.
    /// /params minimumBounds Current minimum mesh bounds.
    /// /params maximumBounds Current maximum mesh bounds.
    /// /returns None.
    /// </summary>
    private static void AddStartCap(float3 center,
                                    float3 tangent,
                                    int firstRingStartIndex,
                                    ref float3 minimumBounds,
                                    ref float3 maximumBounds)
    {
        int centerVertexIndex = sharedVertices.Count;
        sharedVertices.Add(ToVector3(center));
        sharedNormals.Add(ToVector3(-math.normalizesafe(tangent, new float3(0f, 0f, 1f))));
        sharedUvs.Add(new Vector2(0f, 0.5f));
        ExpandBounds(center, ref minimumBounds, ref maximumBounds);

        for (int sideIndex = 0; sideIndex < TubeSideCount; sideIndex++)
        {
            sharedTriangles.Add(centerVertexIndex);
            sharedTriangles.Add(firstRingStartIndex + sideIndex + 1);
            sharedTriangles.Add(firstRingStartIndex + sideIndex);
        }
    }

    /// <summary>
    /// Closes the final ring with a rounded cap so the beam terminates cleanly without a pointed spear tip.
    /// /params endPosition Real terminal point of the lane.
    /// /params tangent Forward tangent at the end of the lane.
    /// /params finalRingStartIndex Start index of the final body ring.
    /// /params minimumBounds Current minimum mesh bounds.
    /// /params maximumBounds Current maximum mesh bounds.
    /// /returns None.
    /// </summary>
    private static void AddEndCap(float3 endPosition,
                                  float3 tangent,
                                  int finalRingStartIndex,
                                  ref float3 minimumBounds,
                                  ref float3 maximumBounds)
    {
        int centerVertexIndex = sharedVertices.Count;
        sharedVertices.Add(ToVector3(endPosition));
        sharedNormals.Add(ToVector3(math.normalizesafe(tangent, new float3(0f, 0f, 1f))));
        sharedUvs.Add(new Vector2(1f, 0.5f));
        ExpandBounds(endPosition, ref minimumBounds, ref maximumBounds);

        for (int sideIndex = 0; sideIndex < TubeSideCount; sideIndex++)
        {
            sharedTriangles.Add(finalRingStartIndex + sideIndex);
            sharedTriangles.Add(centerVertexIndex);
            sharedTriangles.Add(finalRingStartIndex + sideIndex + 1);
        }
    }

    /// <summary>
    /// Resolves the final body diameter at one point after breathing, profile shaping, source opening, and terminal closure are applied.
    /// /params point Current sampled point.
    /// /params normalizedDistance Normalized distance along the lane.
    /// /params laneLength Total length of the current lane.
    /// /params stormBurstNormalized Normalized storm burst currently active on the beam.
    /// /params visualConfig Shared visual config used by the terminal closure.
    /// /params laneVisual Render-time lane metadata.
    /// /params laserBeamConfig Runtime passive config.
    /// /params elapsedTimeSeconds Global elapsed time.
    /// /returns Final full body diameter.
    /// </summary>
    private static float ResolveTubeDiameter(PlayerLaserBeamRibbonPoint point,
                                             float normalizedDistance,
                                             float laneLength,
                                             float stormBurstNormalized,
                                             in PlayerLaserBeamVisualConfig visualConfig,
                                             in PlayerLaserBeamLaneVisual laneVisual,
                                             in LaserBeamPassiveConfig laserBeamConfig,
                                             float elapsedTimeSeconds)
    {
        float baseDiameter = ResolveBodyVisualWidth(point.Width);
        float breathingWave = math.sin(elapsedTimeSeconds * 5.4f - point.Distance * 4.8f + laneVisual.LaneIndex * 0.31f);
        float breathingMultiplier = 1f + laserBeamConfig.WobbleAmplitude * 0.32f * breathingWave;
        float stormWidthMultiplier = 1f + math.saturate(stormBurstNormalized * laserBeamConfig.StormBurstIntensity) * 0.08f;
        float bodyProfileMultiplier = ResolveBodyProfileDiameterMultiplier(laserBeamConfig.BodyProfile, normalizedDistance);
        float sourceApertureMultiplier = ResolveSourceApertureDiameterMultiplier(point.Distance,
                                                                                 laneLength,
                                                                                 baseDiameter,
                                                                                 in laserBeamConfig);
        float terminalClosureMultiplier = ResolveTerminalClosureDiameterMultiplier(point.Distance,
                                                                                   laneLength,
                                                                                   baseDiameter,
                                                                                   in visualConfig,
                                                                                   in laserBeamConfig);
        float resolvedDiameter = baseDiameter *
                                 math.max(0.35f, breathingMultiplier) *
                                 stormWidthMultiplier *
                                 bodyProfileMultiplier *
                                 sourceApertureMultiplier *
                                 terminalClosureMultiplier;
        return math.max(MinimumTubeRadius * 2f, resolvedDiameter);
    }

    /// <summary>
    /// Resolves the profile-driven overall diameter multiplier used to preserve authored silhouette variety.
    /// /params bodyProfile Active body profile selector.
    /// /params normalizedDistance Normalized distance along the lane.
    /// /returns Diameter multiplier derived from the active profile.
    /// </summary>
    private static float ResolveBodyProfileDiameterMultiplier(LaserBeamBodyProfile bodyProfile,
                                                              float normalizedDistance)
    {
        switch (bodyProfile)
        {
            case LaserBeamBodyProfile.TaperedJet:
                return math.lerp(0.92f, 1.05f, normalizedDistance);
            case LaserBeamBodyProfile.DenseRibbon:
                return 1.08f;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// Resolves the vertical and horizontal ellipse scales used by one ring cross-section.
    /// /params bodyProfile Active body profile selector.
    /// /params normalizedDistance Normalized distance along the lane.
    /// /params normalAxisScale Vertical ellipse scale aligned with the frame normal.
    /// /params binormalAxisScale Horizontal ellipse scale aligned with the frame binormal.
    /// /returns None.
    /// </summary>
    private static void ResolveBodyProfileShapeScales(LaserBeamBodyProfile bodyProfile,
                                                      float normalizedDistance,
                                                      out float normalAxisScale,
                                                      out float binormalAxisScale)
    {
        switch (bodyProfile)
        {
            case LaserBeamBodyProfile.TaperedJet:
                normalAxisScale = math.lerp(0.78f, 0.62f, normalizedDistance);
                binormalAxisScale = math.lerp(0.95f, 1.12f, normalizedDistance);
                return;
            case LaserBeamBodyProfile.DenseRibbon:
                normalAxisScale = 0.56f;
                binormalAxisScale = 1.24f;
                return;
            default:
                normalAxisScale = 0.82f;
                binormalAxisScale = 1f;
                return;
        }
    }

    /// <summary>
    /// Resolves the diameter multiplier applied near the source so the beam starts sealed and opens outward.
    /// /params distanceAlongLane Current point distance.
    /// /params laneLength Total lane length.
    /// /params baseDiameter Current base body diameter.
    /// /params laserBeamConfig Runtime passive config used to scale the source offset.
    /// /returns Diameter multiplier applied near the source aperture.
    /// </summary>
    private static float ResolveSourceApertureDiameterMultiplier(float distanceAlongLane,
                                                                 float laneLength,
                                                                 float baseDiameter,
                                                                 in LaserBeamPassiveConfig laserBeamConfig)
    {
        float apertureLength = math.clamp(math.max(baseDiameter * 1.55f, laserBeamConfig.SourceOffset * 1.9f),
                                          0.08f,
                                          laneLength * 0.28f);

        if (apertureLength <= 0f || distanceAlongLane >= apertureLength)
            return 1f;

        float fastOpenLength = apertureLength * 0.24f;

        if (distanceAlongLane <= fastOpenLength)
        {
            float fastOpenInterpolation = math.saturate(distanceAlongLane / math.max(0.0001f, fastOpenLength));
            float smoothFastOpenInterpolation = fastOpenInterpolation * fastOpenInterpolation * (3f - 2f * fastOpenInterpolation);
            return math.lerp(SourceInitialApertureScale, SourceMidApertureScale, smoothFastOpenInterpolation);
        }

        float apertureInterpolation = math.saturate((distanceAlongLane - fastOpenLength) /
                                                    math.max(0.0001f, apertureLength - fastOpenLength));
        float smoothApertureInterpolation = apertureInterpolation * apertureInterpolation * (3f - 2f * apertureInterpolation);
        return math.lerp(SourceMidApertureScale, 1f, smoothApertureInterpolation);
    }

    /// <summary>
    /// Resolves the taper multiplier applied near the end of the lane so the body closes cleanly into the rounded terminal cap.
    /// /params distanceAlongLane Current point distance.
    /// /params laneLength Total lane length.
    /// /params baseDiameter Current base body diameter.
    /// /params visualConfig Shared visual config used to shape the terminal closure.
    /// /params laserBeamConfig Runtime passive config used to scale the terminal emphasis.
    /// /returns Diameter multiplier applied near the terminal section.
    /// </summary>
    private static float ResolveTerminalClosureDiameterMultiplier(float distanceAlongLane,
                                                                  float laneLength,
                                                                  float baseDiameter,
                                                                  in PlayerLaserBeamVisualConfig visualConfig,
                                                                  in LaserBeamPassiveConfig laserBeamConfig)
    {
        float tipLength = math.clamp(baseDiameter *
                                     math.sqrt(math.max(0.01f, laserBeamConfig.TerminalCapScaleMultiplier)) *
                                     math.max(1f, visualConfig.TerminalSplashLengthMultiplier) *
                                     2.65f,
                                     0.12f,
                                     laneLength * 0.32f);
        float tipStartDistance = math.max(0f, laneLength - tipLength);

        if (distanceAlongLane <= tipStartDistance)
            return 1f;

        float tipInterpolation = math.saturate((distanceAlongLane - tipStartDistance) / math.max(0.0001f, laneLength - tipStartDistance));
        float smoothInterpolation = tipInterpolation * tipInterpolation * (3f - 2f * tipInterpolation);
        float closureFloor = math.clamp(0.78f + math.max(0f, visualConfig.TerminalSplashWidthMultiplier - 1f) * 0.06f,
                                        0.72f,
                                        0.9f);
        return math.lerp(1f, closureFloor, smoothInterpolation);
    }

    /// <summary>
    /// Resolves the tangent used to orient one sampled point between its neighbors.
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
    /// Resolves the first frame-normal axis used to seed the ring transport along the lane.
    /// /params ribbonPoints Shared ribbon point list.
    /// /params pointStartIndex Start index of the current lane inside the shared point list.
    /// /params pointCount Number of points belonging to the current lane.
    /// /returns Initial transported normal axis.
    /// </summary>
    private static float3 ResolveInitialFrameNormal(List<PlayerLaserBeamRibbonPoint> ribbonPoints,
                                                    int pointStartIndex,
                                                    int pointCount)
    {
        float3 tangent = ResolvePointTangent(ribbonPoints, pointStartIndex, pointCount, 0);
        float3 projectedUp = ProjectOntoPlane(new float3(0f, 1f, 0f), tangent);

        if (math.lengthsq(projectedUp) > 1e-6f)
            return math.normalizesafe(projectedUp, new float3(0f, 1f, 0f));

        float3 projectedRight = ProjectOntoPlane(new float3(1f, 0f, 0f), tangent);
        return math.normalizesafe(projectedRight, new float3(1f, 0f, 0f));
    }

    /// <summary>
    /// Transports the previous frame-normal axis onto the plane orthogonal to the new tangent.
    /// /params previousNormal Previous transported frame-normal axis.
    /// /params tangent Current tangent.
    /// /returns Stabilized transported normal axis.
    /// </summary>
    private static float3 TransportFrameNormal(float3 previousNormal, float3 tangent)
    {
        float3 projectedNormal = ProjectOntoPlane(previousNormal, tangent);

        if (math.lengthsq(projectedNormal) > 1e-6f)
            return math.normalizesafe(projectedNormal, previousNormal);

        return math.normalizesafe(ProjectOntoPlane(new float3(0f, 1f, 0f), tangent), new float3(0f, 1f, 0f));
    }

    /// <summary>
    /// Resolves the orthogonal frame binormal from the transported normal axis and tangent.
    /// /params normal Current transported normal axis.
    /// /params tangent Current tangent.
    /// /returns Stabilized frame binormal.
    /// </summary>
    private static float3 ResolveFrameBinormal(float3 normal, float3 tangent)
    {
        float3 binormal = math.cross(tangent, normal);
        return math.normalizesafe(binormal, new float3(1f, 0f, 0f));
    }

    /// <summary>
    /// Re-orthonormalizes the transported normal axis after the binormal was resolved.
    /// /params binormal Current frame binormal.
    /// /params tangent Current tangent.
    /// /returns Stabilized frame normal axis.
    /// </summary>
    private static float3 ResolveFrameNormal(float3 binormal, float3 tangent)
    {
        float3 normal = math.cross(binormal, tangent);
        return math.normalizesafe(normal, new float3(0f, 1f, 0f));
    }

    /// <summary>
    /// Projects one vector onto the plane orthogonal to the provided normal.
    /// /params vector Vector to project.
    /// /params planeNormal Plane normal used for the projection.
    /// /returns Projected vector.
    /// </summary>
    private static float3 ProjectOntoPlane(float3 vector, float3 planeNormal)
    {
        return vector - planeNormal * math.dot(vector, planeNormal);
    }

    /// <summary>
    /// Expands the mesh bounds with one new point.
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
    /// /returns Mesh bounds covering the current body.
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
