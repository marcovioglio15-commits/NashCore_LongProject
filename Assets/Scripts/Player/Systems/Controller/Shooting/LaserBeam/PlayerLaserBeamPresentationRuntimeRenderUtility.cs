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
    #endregion

    #region Fields
    private static readonly MaterialPropertyBlock sharedPropertyBlock = new MaterialPropertyBlock();
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Updates one body visual instance to match the requested body sample.
    /// /params visual Pooled body visual to update.
    /// /params sample Render-time body sample.
    /// /params visualConfig Shared visual config.
    /// /params laserBeamConfig Runtime passive config used for material properties.
    /// /params palette Resolved palette colors.
    /// /params bodyMaterial Optional shared body material override.
    /// /returns None.
    /// </summary>
    public static void ApplyBodyVisual(PlayerLaserBeamManagedBodyVisual visual,
                                       in PlayerLaserBeamBodySample sample,
                                       in PlayerLaserBeamVisualConfig visualConfig,
                                       in LaserBeamPassiveConfig laserBeamConfig,
                                       in PlayerLaserBeamResolvedPalette palette,
                                       Material bodyMaterial)
    {
        if (visual == null || visual.InstanceObject == null || visual.RootTransform == null)
            return;

        if (!visual.InstanceObject.activeSelf)
            visual.InstanceObject.SetActive(true);

        float resolvedBodyWidth = ResolveBodyVisualWidth(sample.Width);
        visual.RootTransform.position = ToVector3(sample.Position);
        visual.RootTransform.rotation = ToQuaternion(sample.Rotation);
        visual.RootTransform.localScale = new Vector3(resolvedBodyWidth,
                                                      resolvedBodyWidth,
                                                      sample.Length);

        if (visual.Renderers == null || visual.Renderers.Length <= 0)
            return;

        // Apply layered blob materials for the outer shell and inner core.
        for (int rendererIndex = 0; rendererIndex < visual.Renderers.Length; rendererIndex++)
        {
            Renderer renderer = visual.Renderers[rendererIndex];

            if (renderer == null)
                continue;

            if (bodyMaterial != null && renderer.sharedMaterial != bodyMaterial)
                renderer.sharedMaterial = bodyMaterial;

            sharedPropertyBlock.Clear();
            sharedPropertyBlock.SetColor(BeamColorAPropertyId, palette.BodyColorA);
            sharedPropertyBlock.SetColor(BeamColorBPropertyId, palette.BodyColorB);
            sharedPropertyBlock.SetColor(CoreColorPropertyId, palette.CoreColor);
            sharedPropertyBlock.SetColor(RimColorPropertyId, palette.RimColor);
            sharedPropertyBlock.SetFloat(OpacityPropertyId,
                                         rendererIndex <= 0
                                             ? math.saturate(laserBeamConfig.BodyOpacity * 0.76f)
                                             : math.saturate(laserBeamConfig.BodyOpacity * 0.48f));
            sharedPropertyBlock.SetFloat(CoreBrightnessPropertyId,
                                         rendererIndex <= 0
                                             ? math.max(0f, laserBeamConfig.CoreBrightness * 0.92f)
                                             : math.max(0f, laserBeamConfig.CoreBrightness * 1.45f));
            sharedPropertyBlock.SetFloat(RimBrightnessPropertyId,
                                         rendererIndex <= 0
                                             ? math.max(0f, laserBeamConfig.RimBrightness * 0.82f)
                                             : math.max(0f, laserBeamConfig.RimBrightness * 0.22f));
            sharedPropertyBlock.SetFloat(FlowScrollSpeedPropertyId, math.max(0f, laserBeamConfig.FlowScrollSpeed));
            sharedPropertyBlock.SetFloat(FlowPulseFrequencyPropertyId, math.max(0f, laserBeamConfig.FlowPulseFrequency));
            sharedPropertyBlock.SetFloat(WobbleAmplitudePropertyId, math.max(0f, laserBeamConfig.WobbleAmplitude));
            sharedPropertyBlock.SetFloat(BubbleDriftSpeedPropertyId, math.max(0f, laserBeamConfig.BubbleDriftSpeed));
            sharedPropertyBlock.SetFloat(BodyProfilePropertyId, (float)laserBeamConfig.BodyProfile);
            sharedPropertyBlock.SetFloat(BeamRolePropertyId, 0f);
            sharedPropertyBlock.SetFloat(CapShapePropertyId, 0f);
            sharedPropertyBlock.SetFloat(SegmentLengthPropertyId, math.max(visualConfig.MinimumSegmentLength, sample.Length));
            sharedPropertyBlock.SetFloat(WidthScalePropertyId, math.max(0.01f, resolvedBodyWidth));
            renderer.SetPropertyBlock(sharedPropertyBlock);
        }
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
        float3 referenceNormal = isImpact ? endpoint.TerminalNormal : math.up();
        quaternion rotation = quaternion.LookRotationSafe(direction, math.normalizesafe(referenceNormal, math.up()));
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
                             isImpact);
    }
    #endregion

    #region Private Methods
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
    /// /params isImpact True when configuring the terminal splash path.
    /// /returns None.
    /// </summary>
    private static void ApplyParticlePalette(PlayerLaserBeamManagedParticleVisual visual,
                                             in PlayerLaserBeamResolvedPalette palette,
                                             in LaserBeamPassiveConfig laserBeamConfig,
                                             LaserBeamCapShape capShape,
                                             float width,
                                             bool isImpact)
    {
        if (visual == null)
            return;

        Color minimumColor = isImpact ? palette.CoreColor : palette.BodyColorA;
        Color maximumColor = isImpact ? palette.RimColor : palette.BodyColorB;
        ParticleSystem.MinMaxGradient startGradient = new ParticleSystem.MinMaxGradient(minimumColor, maximumColor);
        float resolvedEndpointWidth = ResolveBodyVisualWidth(width);

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
                                         math.saturate(laserBeamConfig.BodyOpacity * (isImpact ? 0.55f : 0.42f)));
            sharedPropertyBlock.SetFloat(CoreBrightnessPropertyId,
                                         math.max(0f, isImpact ? laserBeamConfig.CoreBrightness * 1.2f : laserBeamConfig.CoreBrightness * 0.82f));
            sharedPropertyBlock.SetFloat(RimBrightnessPropertyId,
                                         math.max(0f, isImpact ? laserBeamConfig.RimBrightness * 0.72f : laserBeamConfig.RimBrightness * 0.48f));
            sharedPropertyBlock.SetFloat(FlowScrollSpeedPropertyId, math.max(0f, laserBeamConfig.FlowScrollSpeed));
            sharedPropertyBlock.SetFloat(FlowPulseFrequencyPropertyId, math.max(0f, laserBeamConfig.FlowPulseFrequency));
            sharedPropertyBlock.SetFloat(WobbleAmplitudePropertyId, math.max(0f, laserBeamConfig.WobbleAmplitude));
            sharedPropertyBlock.SetFloat(BubbleDriftSpeedPropertyId, math.max(0f, laserBeamConfig.BubbleDriftSpeed));
            sharedPropertyBlock.SetFloat(BodyProfilePropertyId, (float)laserBeamConfig.BodyProfile);
            sharedPropertyBlock.SetFloat(BeamRolePropertyId, 1f);
            sharedPropertyBlock.SetFloat(CapShapePropertyId, (float)capShape);
            sharedPropertyBlock.SetFloat(SegmentLengthPropertyId, math.max(0.05f, resolvedEndpointWidth));
            sharedPropertyBlock.SetFloat(WidthScalePropertyId, math.max(0.01f, resolvedEndpointWidth));
            renderer.SetPropertyBlock(sharedPropertyBlock);
        }
    }

    /// <summary>
    /// Compresses the raw beam width into a readable art width so very large projectile-size stacks do not flood the screen.
    /// /params rawWidth Raw body width inherited from gameplay lane generation.
    /// /returns Compressed art width used by the mesh body visuals.
    /// </summary>
    private static float ResolveBodyVisualWidth(float rawWidth)
    {
        float compressedWidth = 0.42f * math.sqrt(math.max(0.01f, rawWidth));
        return math.clamp(compressedWidth, 0.1f, 0.32f);
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
        float baseFactor = isImpact ? 0.34f : 0.26f;
        float maximumScale = isImpact ? 0.56f : 0.4f;
        float compressedScale = baseFactor *
                                math.sqrt(math.max(0.01f, rawWidth)) *
                                math.sqrt(math.max(0.01f, authoredScaleMultiplier));
        return math.clamp(compressedScale, 0.1f, maximumScale);
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
