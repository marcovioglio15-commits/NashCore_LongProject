using UnityEngine;

/// <summary>
/// Stores authored prefab variants and sampling defaults used by the 3D Laser Beam presentation runtime.
/// /params None.
/// /returns None.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerLaserBeamVisualRigAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Body Variants")]
    [Tooltip("3D prefab used when the Laser Beam body profile resolves to Rounded Tube.")]
    [SerializeField] private GameObject roundedTubeBodyPrefab;

    [Tooltip("3D prefab used when the Laser Beam body profile resolves to Tapered Jet.")]
    [SerializeField] private GameObject taperedJetBodyPrefab;

    [Tooltip("3D prefab used when the Laser Beam body profile resolves to Dense Ribbon.")]
    [SerializeField] private GameObject denseRibbonBodyPrefab;

    [Header("Source Variants")]
    [Tooltip("Particle prefab used at the muzzle when the source shape resolves to Bubble Burst.")]
    [SerializeField] private GameObject bubbleBurstSourcePrefab;

    [Tooltip("Particle prefab used at the muzzle when the source shape resolves to Star Bloom.")]
    [SerializeField] private GameObject starBloomSourcePrefab;

    [Tooltip("Particle prefab used at the muzzle when the source shape resolves to Soft Disc.")]
    [SerializeField] private GameObject softDiscSourcePrefab;

    [Header("Impact Variants")]
    [Tooltip("Particle prefab used at the beam terminal point when the impact shape resolves to Bubble Burst.")]
    [SerializeField] private GameObject bubbleBurstImpactPrefab;

    [Tooltip("Particle prefab used at the beam terminal point when the impact shape resolves to Star Bloom.")]
    [SerializeField] private GameObject starBloomImpactPrefab;

    [Tooltip("Particle prefab used at the beam terminal point when the impact shape resolves to Soft Disc.")]
    [SerializeField] private GameObject softDiscImpactPrefab;

    [Header("Sampling")]
    [Tooltip("Maximum world-space length of one visual blob segment after gameplay lanes are subdivided for rendering.")]
    [SerializeField] private float maximumVisualSegmentLength = 0.34f;

    [Tooltip("Spacing multiplier applied between consecutive body blobs. Values below 1 create overlap for a thicker liquid look.")]
    [SerializeField] private float bodyBlobSpacingMultiplier = 0.92f;

    [Tooltip("Length multiplier applied to each 3D body blob along the beam forward axis.")]
    [SerializeField] private float bodyBlobLengthMultiplier = 1.04f;

    [Tooltip("Width multiplier applied to each 3D body blob before runtime width scaling is evaluated.")]
    [SerializeField] private float bodyBlobWidthMultiplier = 0.72f;

    [Tooltip("Forward offset applied to the source particle effect from the first sampled beam point.")]
    [SerializeField] private float sourceForwardOffset = 0.035f;

    [Tooltip("Forward offset applied to the impact particle effect from the terminal beam point.")]
    [SerializeField] private float impactForwardOffset = 0.015f;
    #endregion

    #endregion

    #region Properties
    public GameObject RoundedTubeBodyPrefab
    {
        get
        {
            return roundedTubeBodyPrefab;
        }
    }

    public GameObject TaperedJetBodyPrefab
    {
        get
        {
            return taperedJetBodyPrefab;
        }
    }

    public GameObject DenseRibbonBodyPrefab
    {
        get
        {
            return denseRibbonBodyPrefab;
        }
    }

    public GameObject BubbleBurstSourcePrefab
    {
        get
        {
            return bubbleBurstSourcePrefab;
        }
    }

    public GameObject StarBloomSourcePrefab
    {
        get
        {
            return starBloomSourcePrefab;
        }
    }

    public GameObject SoftDiscSourcePrefab
    {
        get
        {
            return softDiscSourcePrefab;
        }
    }

    public GameObject BubbleBurstImpactPrefab
    {
        get
        {
            return bubbleBurstImpactPrefab;
        }
    }

    public GameObject StarBloomImpactPrefab
    {
        get
        {
            return starBloomImpactPrefab;
        }
    }

    public GameObject SoftDiscImpactPrefab
    {
        get
        {
            return softDiscImpactPrefab;
        }
    }

    public float MaximumVisualSegmentLength
    {
        get
        {
            return maximumVisualSegmentLength;
        }
    }

    public float BodyBlobSpacingMultiplier
    {
        get
        {
            return bodyBlobSpacingMultiplier;
        }
    }

    public float BodyBlobLengthMultiplier
    {
        get
        {
            return bodyBlobLengthMultiplier;
        }
    }

    public float BodyBlobWidthMultiplier
    {
        get
        {
            return bodyBlobWidthMultiplier;
        }
    }

    public float SourceForwardOffset
    {
        get
        {
            return sourceForwardOffset;
        }
    }

    public float ImpactForwardOffset
    {
        get
        {
            return impactForwardOffset;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnValidate()
    {
        ValidateAssignments();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Resolves the authored body prefab for one runtime body profile.
    /// /params bodyProfile Runtime body profile selector.
    /// /returns Matching prefab when configured, otherwise null.
    /// </summary>
    public GameObject ResolveBodyPrefab(LaserBeamBodyProfile bodyProfile)
    {
        switch (bodyProfile)
        {
            case LaserBeamBodyProfile.TaperedJet:
                return taperedJetBodyPrefab;
            case LaserBeamBodyProfile.DenseRibbon:
                return denseRibbonBodyPrefab;
            default:
                return roundedTubeBodyPrefab;
        }
    }

    /// <summary>
    /// Resolves the authored source particle prefab for one runtime cap-shape selector.
    /// /params capShape Runtime cap-shape selector.
    /// /returns Matching prefab when configured, otherwise null.
    /// </summary>
    public GameObject ResolveSourcePrefab(LaserBeamCapShape capShape)
    {
        switch (capShape)
        {
            case LaserBeamCapShape.StarBloom:
                return starBloomSourcePrefab;
            case LaserBeamCapShape.SoftDisc:
                return softDiscSourcePrefab;
            default:
                return bubbleBurstSourcePrefab;
        }
    }

    /// <summary>
    /// Resolves the authored impact particle prefab for one runtime cap-shape selector.
    /// /params capShape Runtime cap-shape selector.
    /// /returns Matching prefab when configured, otherwise null.
    /// </summary>
    public GameObject ResolveImpactPrefab(LaserBeamCapShape capShape)
    {
        switch (capShape)
        {
            case LaserBeamCapShape.StarBloom:
                return starBloomImpactPrefab;
            case LaserBeamCapShape.SoftDisc:
                return softDiscImpactPrefab;
            default:
                return bubbleBurstImpactPrefab;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Emits editor-time warnings for missing prefab variants and invalid sampling values.
    /// /params None.
    /// /returns None.
    /// </summary>
    private void ValidateAssignments()
    {
        if (maximumVisualSegmentLength <= 0f)
            Debug.LogWarning("[PlayerLaserBeamVisualRigAuthoring] Maximum Visual Segment Length should be greater than 0 to avoid degenerate sampling.", this);

        if (bodyBlobSpacingMultiplier <= 0f)
            Debug.LogWarning("[PlayerLaserBeamVisualRigAuthoring] Body Blob Spacing Multiplier should be greater than 0 to keep body blobs ordered.", this);

        if (bodyBlobLengthMultiplier <= 0f)
            Debug.LogWarning("[PlayerLaserBeamVisualRigAuthoring] Body Blob Length Multiplier should be greater than 0 to keep body blobs visible.", this);

        if (bodyBlobWidthMultiplier <= 0f)
            Debug.LogWarning("[PlayerLaserBeamVisualRigAuthoring] Body Blob Width Multiplier should be greater than 0 to keep body blobs visible.", this);

        ValidatePrefabReference(roundedTubeBodyPrefab, "Rounded Tube body");
        ValidatePrefabReference(taperedJetBodyPrefab, "Tapered Jet body");
        ValidatePrefabReference(denseRibbonBodyPrefab, "Dense Ribbon body");
        ValidatePrefabReference(bubbleBurstSourcePrefab, "Bubble Burst source");
        ValidatePrefabReference(starBloomSourcePrefab, "Star Bloom source");
        ValidatePrefabReference(softDiscSourcePrefab, "Soft Disc source");
        ValidatePrefabReference(bubbleBurstImpactPrefab, "Bubble Burst impact");
        ValidatePrefabReference(starBloomImpactPrefab, "Star Bloom impact");
        ValidatePrefabReference(softDiscImpactPrefab, "Soft Disc impact");
    }

    /// <summary>
    /// Emits one warning when an authored prefab slot is empty.
    /// /params prefab Assigned prefab to validate.
    /// /params label Human-readable slot description.
    /// /returns None.
    /// </summary>
    private void ValidatePrefabReference(GameObject prefab, string label)
    {
        if (prefab != null)
            return;

        Debug.LogWarning(string.Format("[PlayerLaserBeamVisualRigAuthoring] Missing prefab assignment for {0}. Laser Beam visual variants using that selector will not render.", label), this);
    }
    #endregion

    #endregion
}
