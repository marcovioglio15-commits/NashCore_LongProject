using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Applies runtime transforms, materials, and shader properties to pooled Laser Beam visuals.
/// /params None.
/// /returns None.
/// </summary>
internal static class PlayerLaserBeamPresentationRuntimeRenderUtility
{
    #region Constants
    private static readonly int BeamColorAPropertyId = Shader.PropertyToID("_BeamColorA");
    private static readonly int BeamColorBPropertyId = Shader.PropertyToID("_BeamColorB");
    private static readonly int CoreColorPropertyId = Shader.PropertyToID("_CoreColor");
    private static readonly int RimColorPropertyId = Shader.PropertyToID("_RimColor");
    private static readonly int OpacityPropertyId = Shader.PropertyToID("_Opacity");
    private static readonly int CoreBrightnessPropertyId = Shader.PropertyToID("_CoreBrightness");
    private static readonly int RimBrightnessPropertyId = Shader.PropertyToID("_RimBrightness");
    private static readonly int FlowScrollSpeedPropertyId = Shader.PropertyToID("_FlowScrollSpeed");
    private static readonly int FlowPulseFrequencyPropertyId = Shader.PropertyToID("_FlowPulseFrequency");
    private static readonly int WobbleAmplitudePropertyId = Shader.PropertyToID("_WobbleAmplitude");
    private static readonly int BubbleDriftSpeedPropertyId = Shader.PropertyToID("_BubbleDriftSpeed");
    private static readonly int BodyProfilePropertyId = Shader.PropertyToID("_BodyProfile");
    private static readonly int BeamRolePropertyId = Shader.PropertyToID("_BeamRole");
    private static readonly int CapShapePropertyId = Shader.PropertyToID("_CapShape");
    private static readonly int SegmentLengthPropertyId = Shader.PropertyToID("_SegmentLength");
    private static readonly int WidthScalePropertyId = Shader.PropertyToID("_WidthScale");
    private static readonly int PrimaryPulseProgressPropertyId = Shader.PropertyToID("_PrimaryPulseProgress");
    private static readonly int SecondaryPulseProgressPropertyId = Shader.PropertyToID("_SecondaryPulseProgress");
    private static readonly int PulseLengthNormalizedPropertyId = Shader.PropertyToID("_PulseLengthNormalized");
    private static readonly int PulseBrightnessBoostPropertyId = Shader.PropertyToID("_PulseBrightnessBoost");
    private static readonly int TerminalBlockedByWallPropertyId = Shader.PropertyToID("_TerminalBlockedByWall");
    #endregion

    #region Fields
    private static readonly MaterialPropertyBlock sharedPropertyBlock = new MaterialPropertyBlock();
    private static Camera cachedPresentationCamera;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Resolves the current gameplay camera used to orient ribbon meshes toward the player view.
    /// /params None.
    /// /returns Active presentation camera when available.
    /// </summary>
    public static Camera ResolvePresentationCamera()
    {
        if (cachedPresentationCamera != null)
            return cachedPresentationCamera;

        cachedPresentationCamera = Camera.main;
        return cachedPresentationCamera;
    }

    /// <summary>
    /// Updates one body visual instance to match the requested lane metadata and body material properties.
    /// /params visual Pooled body visual to update.
    /// /params laneVisual Render-time lane metadata.
    /// /params visualConfig Shared visual config.
    /// /params laserBeamConfig Runtime passive config used for material properties.
    /// /params laserBeamState Runtime state used to animate travelling tick pulses.
    /// /params palette Resolved palette colors.
    /// /params bodyMaterial Optional shared body material override.
    /// /returns None.
    /// </summary>
    public static void ApplyBodyVisual(PlayerLaserBeamManagedBodyVisual visual,
                                       in PlayerLaserBeamLaneVisual laneVisual,
                                       in PlayerLaserBeamVisualConfig visualConfig,
                                       in LaserBeamPassiveConfig laserBeamConfig,
                                       in PlayerLaserBeamState laserBeamState,
                                       in PlayerLaserBeamResolvedPalette palette,
                                       Material bodyMaterial)
    {
        if (visual == null || visual.InstanceObject == null || visual.MeshRenderer == null)
            return;

        if (!visual.InstanceObject.activeSelf)
            visual.InstanceObject.SetActive(true);

        visual.RootTransform.localPosition = Vector3.zero;
        visual.RootTransform.localRotation = Quaternion.identity;
        visual.RootTransform.localScale = Vector3.one;

        MeshRenderer renderer = visual.MeshRenderer;

        if (bodyMaterial != null && renderer.sharedMaterial != bodyMaterial)
            renderer.sharedMaterial = bodyMaterial;

        float laneLength = math.max(visualConfig.MinimumSegmentLength, laneVisual.TotalLength);
        float maximumWidth = PlayerLaserBeamPresentationRuntimeMeshUtility.ResolveBodyVisualWidth(math.max(laneVisual.StartWidth, laneVisual.EndWidth));
        sharedPropertyBlock.Clear();
        sharedPropertyBlock.SetColor(BeamColorAPropertyId, palette.BodyColorA);
        sharedPropertyBlock.SetColor(BeamColorBPropertyId, palette.BodyColorB);
        sharedPropertyBlock.SetColor(CoreColorPropertyId, palette.CoreColor);
        sharedPropertyBlock.SetColor(RimColorPropertyId, palette.RimColor);
        sharedPropertyBlock.SetFloat(OpacityPropertyId, math.saturate(laserBeamConfig.BodyOpacity));
        sharedPropertyBlock.SetFloat(CoreBrightnessPropertyId, math.max(0f, laserBeamConfig.CoreBrightness));
        sharedPropertyBlock.SetFloat(RimBrightnessPropertyId, math.max(0f, laserBeamConfig.RimBrightness));
        sharedPropertyBlock.SetFloat(FlowScrollSpeedPropertyId, math.max(0f, laserBeamConfig.FlowScrollSpeed));
        sharedPropertyBlock.SetFloat(FlowPulseFrequencyPropertyId, math.max(0f, laserBeamConfig.FlowPulseFrequency));
        sharedPropertyBlock.SetFloat(WobbleAmplitudePropertyId, math.max(0f, laserBeamConfig.WobbleAmplitude));
        sharedPropertyBlock.SetFloat(BubbleDriftSpeedPropertyId, math.max(0f, laserBeamConfig.BubbleDriftSpeed));
        sharedPropertyBlock.SetFloat(BodyProfilePropertyId, (float)laserBeamConfig.BodyProfile);
        sharedPropertyBlock.SetFloat(BeamRolePropertyId, 0f);
        sharedPropertyBlock.SetFloat(CapShapePropertyId, 0f);
        sharedPropertyBlock.SetFloat(SegmentLengthPropertyId, laneLength);
        sharedPropertyBlock.SetFloat(WidthScalePropertyId, math.max(0.01f, maximumWidth));
        sharedPropertyBlock.SetFloat(PrimaryPulseProgressPropertyId,
                                     ResolvePulseProgress(laserBeamState.HasPrimaryTickPulse != 0,
                                                          laserBeamState.PrimaryTickPulseElapsedSeconds,
                                                          laserBeamConfig.TickPulseTravelSpeed,
                                                          laneLength));
        sharedPropertyBlock.SetFloat(SecondaryPulseProgressPropertyId,
                                     ResolvePulseProgress(laserBeamState.HasSecondaryTickPulse != 0,
                                                          laserBeamState.SecondaryTickPulseElapsedSeconds,
                                                          laserBeamConfig.TickPulseTravelSpeed,
                                                          laneLength));
        sharedPropertyBlock.SetFloat(PulseLengthNormalizedPropertyId,
                                     math.max(0.0025f, math.max(0.01f, laserBeamConfig.TickPulseLength) / laneLength));
        sharedPropertyBlock.SetFloat(PulseBrightnessBoostPropertyId, math.max(0f, laserBeamConfig.TickPulseBrightnessBoost));
        sharedPropertyBlock.SetFloat(TerminalBlockedByWallPropertyId, laneVisual.TerminalBlockedByWall != 0 ? 1f : 0f);
        renderer.SetPropertyBlock(sharedPropertyBlock);
    }

    /// <summary>
    /// Updates one particle visual instance to match the requested lane endpoint.
    /// /params visual Pooled particle visual to update.
    /// /params endpoint Per-lane source or impact anchor.
    /// /params visualConfig Shared visual config.
    /// /params laserBeamConfig Runtime passive config used for scale and material properties.
    /// /params palette Resolved palette colors.
    /// /params materialOverride Optional shared material override.
    /// /params capShape Shape selector applied to the shader.
    /// /params isImpact True when updating the terminal splash, otherwise false for the muzzle effect.
    /// /returns None.
    /// </summary>
    public static void ApplyParticleVisual(PlayerLaserBeamManagedParticleVisual visual,
                                           in PlayerLaserBeamLaneEndpoint endpoint,
                                           in PlayerLaserBeamVisualConfig visualConfig,
                                           in LaserBeamPassiveConfig laserBeamConfig,
                                           in PlayerLaserBeamResolvedPalette palette,
                                           Material materialOverride,
                                           LaserBeamCapShape capShape,
                                           bool isImpact)
    {
        if (visual == null || visual.InstanceObject == null || visual.RootTransform == null)
            return;

        float3 direction = isImpact ? endpoint.EndDirection : endpoint.StartDirection;
        quaternion rotation = ResolveEndpointRotation(in endpoint, direction, isImpact);
        float width = math.max(0.05f, isImpact ? endpoint.EndWidth : endpoint.StartWidth);
        float forwardOffset = isImpact ? visualConfig.ImpactForwardOffset : visualConfig.SourceForwardOffset;
        float authoredScaleMultiplier = isImpact
            ? math.max(0.01f, laserBeamConfig.ImpactScaleMultiplier)
            : math.max(0.01f, laserBeamConfig.SourceScaleMultiplier);
        float uniformScale = ResolveEndpointVisualScale(width, authoredScaleMultiplier, isImpact);
        float3 anchorPoint = isImpact ? endpoint.EndPoint : endpoint.StartPoint;
        float3 worldPosition = anchorPoint + direction * forwardOffset;

        if (!visual.InstanceObject.activeSelf)
        {
            visual.InstanceObject.SetActive(true);
            PlayerLaserBeamPresentationRuntimeUtility.RestartParticleVisual(visual);
        }

        visual.RootTransform.position = ToVector3(worldPosition);
        visual.RootTransform.rotation = ToQuaternion(rotation);
        visual.RootTransform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
        ApplyParticleMaterials(visual, materialOverride);
        ApplyParticlePalette(visual,
                             palette,
                             laserBeamConfig,
                             capShape,
                             width,
                             endpoint.TerminalBlockedByWall != 0,
                             isImpact);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resolves a stable normalized pulse progress for shader-side brightness shaping.
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
    /// Resolves the endpoint rotation used by source bubbles and impact splashes.
    /// /params endpoint Per-lane endpoint metadata.
    /// /params direction Forward direction used by the current endpoint.
    /// /params isImpact True when resolving the terminal splash orientation.
    /// /returns Endpoint world rotation.
    /// </summary>
    private static quaternion ResolveEndpointRotation(in PlayerLaserBeamLaneEndpoint endpoint,
                                                      float3 direction,
                                                      bool isImpact)
    {
        if (isImpact &&
            endpoint.TerminalBlockedByWall != 0 &&
            math.lengthsq(endpoint.TerminalNormal) > 1e-5f)
        {
            return quaternion.LookRotationSafe(-math.normalizesafe(endpoint.TerminalNormal, direction),
                                               math.normalizesafe(direction, new float3(0f, 1f, 0f)));
        }

        return quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));
    }

    /// <summary>
    /// Applies the shared material override to all particle renderers of one pooled visual.
    /// /params visual Pooled particle visual that owns the renderers.
    /// /params materialOverride Shared material override to assign.
    /// /returns None.
    /// </summary>
    private static void ApplyParticleMaterials(PlayerLaserBeamManagedParticleVisual visual, Material materialOverride)
    {
        if (visual == null || visual.Renderers == null || materialOverride == null)
            return;

        for (int rendererIndex = 0; rendererIndex < visual.Renderers.Length; rendererIndex++)
        {
            ParticleSystemRenderer renderer = visual.Renderers[rendererIndex];

            if (renderer == null)
                continue;

            if (renderer.sharedMaterial != materialOverride)
                renderer.sharedMaterial = materialOverride;
        }
    }

    /// <summary>
    /// Pushes palette colors and liquid shader properties into one pooled particle visual.
    /// /params visual Pooled particle visual to update.
    /// /params palette Resolved palette colors.
    /// /params laserBeamConfig Runtime passive config that drives the shader response.
    /// /params capShape Shape selector applied to the shader.
    /// /params width Beam width at the endpoint.
    /// /params terminalBlockedByWall True when the terminal point is a wall hit.
    /// /params isImpact True when configuring the terminal splash path.
    /// /returns None.
    /// </summary>
    private static void ApplyParticlePalette(PlayerLaserBeamManagedParticleVisual visual,
                                             in PlayerLaserBeamResolvedPalette palette,
                                             in LaserBeamPassiveConfig laserBeamConfig,
                                             LaserBeamCapShape capShape,
                                             float width,
                                             bool terminalBlockedByWall,
                                             bool isImpact)
    {
        if (visual == null)
            return;

        Color minimumColor = isImpact ? palette.BodyColorB : palette.BodyColorA;
        Color maximumColor = isImpact ? palette.CoreColor : palette.BodyColorB;
        ParticleSystem.MinMaxGradient startGradient = new ParticleSystem.MinMaxGradient(minimumColor, maximumColor);
        float resolvedEndpointWidth = PlayerLaserBeamPresentationRuntimeMeshUtility.ResolveBodyVisualWidth(width);

        if (visual.ParticleSystems != null)
        {
            for (int particleIndex = 0; particleIndex < visual.ParticleSystems.Length; particleIndex++)
            {
                ParticleSystem particleSystem = visual.ParticleSystems[particleIndex];

                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule mainModule = particleSystem.main;
                mainModule.startColor = startGradient;
            }
        }

        if (visual.Renderers == null)
            return;

        for (int rendererIndex = 0; rendererIndex < visual.Renderers.Length; rendererIndex++)
        {
            ParticleSystemRenderer renderer = visual.Renderers[rendererIndex];

            if (renderer == null)
                continue;

            sharedPropertyBlock.Clear();
            sharedPropertyBlock.SetColor(BeamColorAPropertyId, palette.BodyColorA);
            sharedPropertyBlock.SetColor(BeamColorBPropertyId, palette.BodyColorB);
            sharedPropertyBlock.SetColor(CoreColorPropertyId, palette.CoreColor);
            sharedPropertyBlock.SetColor(RimColorPropertyId, palette.RimColor);
            sharedPropertyBlock.SetFloat(OpacityPropertyId,
                                         math.saturate(laserBeamConfig.BodyOpacity * (isImpact ? 0.68f : 0.5f)));
            sharedPropertyBlock.SetFloat(CoreBrightnessPropertyId,
                                         math.max(0f, isImpact ? laserBeamConfig.CoreBrightness * 1.28f : laserBeamConfig.CoreBrightness * 0.95f));
            sharedPropertyBlock.SetFloat(RimBrightnessPropertyId,
                                         math.max(0f, isImpact ? laserBeamConfig.RimBrightness * 0.92f : laserBeamConfig.RimBrightness * 0.55f));
            sharedPropertyBlock.SetFloat(FlowScrollSpeedPropertyId, math.max(0f, laserBeamConfig.FlowScrollSpeed));
            sharedPropertyBlock.SetFloat(FlowPulseFrequencyPropertyId, math.max(0f, laserBeamConfig.FlowPulseFrequency));
            sharedPropertyBlock.SetFloat(WobbleAmplitudePropertyId, math.max(0f, laserBeamConfig.WobbleAmplitude));
            sharedPropertyBlock.SetFloat(BubbleDriftSpeedPropertyId, math.max(0f, laserBeamConfig.BubbleDriftSpeed));
            sharedPropertyBlock.SetFloat(BodyProfilePropertyId, (float)laserBeamConfig.BodyProfile);
            sharedPropertyBlock.SetFloat(BeamRolePropertyId, isImpact ? 2f : 1f);
            sharedPropertyBlock.SetFloat(CapShapePropertyId, (float)capShape);
            sharedPropertyBlock.SetFloat(SegmentLengthPropertyId, math.max(0.05f, resolvedEndpointWidth));
            sharedPropertyBlock.SetFloat(WidthScalePropertyId, math.max(0.01f, resolvedEndpointWidth));
            sharedPropertyBlock.SetFloat(PrimaryPulseProgressPropertyId, -1f);
            sharedPropertyBlock.SetFloat(SecondaryPulseProgressPropertyId, -1f);
            sharedPropertyBlock.SetFloat(PulseLengthNormalizedPropertyId, 0.1f);
            sharedPropertyBlock.SetFloat(PulseBrightnessBoostPropertyId, 0f);
            sharedPropertyBlock.SetFloat(TerminalBlockedByWallPropertyId, terminalBlockedByWall ? 1f : 0f);
            renderer.SetPropertyBlock(sharedPropertyBlock);
        }
    }

    /// <summary>
    /// Compresses endpoint effect scale so source and impact bursts remain readable while still reacting to projectile-size passives.
    /// /params rawWidth Raw beam width inherited from gameplay lane generation.
    /// /params authoredScaleMultiplier Authored endpoint scale multiplier from the Laser Beam passive config.
    /// /params isImpact True when resolving the terminal splash scale.
    /// /returns Compressed uniform endpoint effect scale.
    /// </summary>
    private static float ResolveEndpointVisualScale(float rawWidth, float authoredScaleMultiplier, bool isImpact)
    {
        float baseFactor = isImpact ? 0.44f : 0.31f;
        float maximumScale = isImpact ? 0.82f : 0.52f;
        float compressedScale = baseFactor *
                                math.sqrt(math.max(0.01f, rawWidth)) *
                                math.sqrt(math.max(0.01f, authoredScaleMultiplier));
        return math.clamp(compressedScale, 0.12f, maximumScale);
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

    /// <summary>
    /// Converts one ECS quaternion into a managed Unity Quaternion.
    /// /params value ECS quaternion value.
    /// /returns Managed Unity Quaternion.
    /// </summary>
    private static Quaternion ToQuaternion(quaternion value)
    {
        return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
    }
    #endregion

    #endregion
}
