using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders one pooled world-space ring used to preview an upcoming enemy spawn location.
/// </summary>
public sealed class EnemySpawnWarningRingView : MonoBehaviour
{
    #region Constants
    private const int CircleSegmentCount = 40;
    #endregion

    #region Fields
    private LineRenderer lineRenderer;
    private float warningDurationSeconds;
    private float fadeOutSeconds;
    private float remainingWarningSeconds;
    private float remainingFadeOutSeconds;
    private float ringWidth;
    private float maximumAlpha;
    private Color ringColor;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures the internal LineRenderer exists and is configured for runtime warning rendering.
    /// /params sharedMaterial: Shared material assigned to the pooled LineRenderer.
    /// /returns None.
    /// </summary>
    public void Initialize(Material sharedMaterial)
    {
        EnsureLineRenderer();

        if (sharedMaterial != null && lineRenderer.sharedMaterial != sharedMaterial)
            lineRenderer.sharedMaterial = sharedMaterial;
    }

    /// <summary>
    /// Arms the pooled view with one new warning payload and makes it visible.
    /// /params worldPosition: World position where the warning ring should appear.
    /// /params durationSecondsValue: Remaining warning lifetime in seconds before the spawn happens.
    /// /params fadeOutSecondsValue: Extra fade-out duration applied after the spawn happens.
    /// /params radius: Ring radius in world units.
    /// /params ringWidthValue: Ring width in world units.
    /// /params ringColorValue: Linear-space tint color used by the ring.
    /// /params maximumAlphaValue: Maximum opacity reached near the end of the warning.
    /// /returns None.
    /// </summary>
    public void Play(Vector3 worldPosition,
                     float durationSecondsValue,
                     float fadeOutSecondsValue,
                     float radius,
                     float ringWidthValue,
                     Color ringColorValue,
                     float maximumAlphaValue)
    {
        EnsureLineRenderer();
        transform.position = worldPosition;
        warningDurationSeconds = Mathf.Max(0.01f, durationSecondsValue);
        fadeOutSeconds = Mathf.Max(0f, fadeOutSecondsValue);
        remainingWarningSeconds = warningDurationSeconds;
        remainingFadeOutSeconds = fadeOutSeconds;
        ringWidth = Mathf.Max(0.01f, ringWidthValue);
        maximumAlpha = Mathf.Clamp01(maximumAlphaValue);
        ringColor = ringColorValue;
        RebuildCircle(Mathf.Max(0.05f, radius));
        ApplyWarningState(0f);

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    /// <summary>
    /// Advances one active warning instance and updates its visible intensity.
    /// /params deltaTime: Current frame delta time in seconds.
    /// /returns True while the warning should stay alive, otherwise false.
    /// </summary>
    public bool Tick(float deltaTime)
    {
        float clampedDeltaTime = Mathf.Max(0f, deltaTime);

        if (remainingWarningSeconds > 0f)
        {
            remainingWarningSeconds = Mathf.Max(0f, remainingWarningSeconds - clampedDeltaTime);
            float normalizedProgress = 1f - remainingWarningSeconds / warningDurationSeconds;
            ApplyWarningState(normalizedProgress);

            if (remainingWarningSeconds > 0f)
                return true;

            if (fadeOutSeconds <= 0f)
                return false;
        }

        if (remainingFadeOutSeconds <= 0f)
            return false;

        remainingFadeOutSeconds = Mathf.Max(0f, remainingFadeOutSeconds - clampedDeltaTime);
        ApplyFadeOutState();
        return remainingFadeOutSeconds > 0f;
    }

    /// <summary>
    /// Hides the pooled view without destroying its cached renderer.
    /// /returns None.
    /// </summary>
    public void Deactivate()
    {
        remainingWarningSeconds = 0f;
        remainingFadeOutSeconds = 0f;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Creates and configures the internal LineRenderer the first time the pooled view is used.
    /// /returns None.
    /// </summary>
    private void EnsureLineRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = CircleSegmentCount;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 0;
        lineRenderer.numCornerVertices = 3;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.lightProbeUsage = LightProbeUsage.Off;
        lineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        lineRenderer.allowOcclusionWhenDynamic = false;
    }

    /// <summary>
    /// Rebuilds one flat XZ circle used by the warning ring.
    /// /params radius: Target circle radius in world units.
    /// /returns None.
    /// </summary>
    private void RebuildCircle(float radius)
    {
        float stepRadians = Mathf.PI * 2f / CircleSegmentCount;

        for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
        {
            float angleRadians = stepRadians * segmentIndex;
            float x = Mathf.Cos(angleRadians) * radius;
            float z = Mathf.Sin(angleRadians) * radius;
            lineRenderer.SetPosition(segmentIndex, new Vector3(x, 0f, z));
        }
    }

    /// <summary>
    /// Applies width and opacity based on the current normalized warning progress.
    /// /params normalizedProgress: Current warning progress in the [0..1] range.
    /// /returns None.
    /// </summary>
    private void ApplyWarningState(float normalizedProgress)
    {
        float clampedProgress = Mathf.Clamp01(normalizedProgress);
        float smoothedProgress = clampedProgress * clampedProgress * (3f - 2f * clampedProgress);
        float currentWidth = ringWidth * Mathf.Lerp(0.8f, 1.18f, smoothedProgress);
        Color currentColor = ringColor;
        currentColor.a = maximumAlpha * Mathf.Lerp(0.18f, 1f, smoothedProgress);
        lineRenderer.widthMultiplier = currentWidth;
        lineRenderer.startColor = currentColor;
        lineRenderer.endColor = currentColor;
    }

    /// <summary>
    /// Applies a smooth fade-out once the spawn has already happened.
    /// /returns None.
    /// </summary>
    private void ApplyFadeOutState()
    {
        if (fadeOutSeconds <= 0f)
            return;

        float normalizedFade = Mathf.Clamp01(remainingFadeOutSeconds / fadeOutSeconds);
        float currentWidth = ringWidth * Mathf.Lerp(1.18f, 0.82f, 1f - normalizedFade);
        Color currentColor = ringColor;
        currentColor.a = maximumAlpha * normalizedFade;
        lineRenderer.widthMultiplier = currentWidth;
        lineRenderer.startColor = currentColor;
        lineRenderer.endColor = currentColor;
    }
    #endregion

    #endregion
}
