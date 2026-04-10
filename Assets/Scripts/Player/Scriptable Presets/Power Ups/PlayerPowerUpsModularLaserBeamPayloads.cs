using System;
using UnityEngine;

#region Enums
/// <summary>
/// Selects the predefined liquid-antibiotic palette used by the Laser Beam presentation runtime.
/// /params None.
/// /returns None.
/// </summary>
public enum LaserBeamVisualPalette
{
    AntibioticBlue = 0,
    SterileMint = 1,
    ToxicLime = 2,
    PlasmaAmber = 3
}

/// <summary>
/// Selects the body silhouette used by the Laser Beam presentation runtime.
/// /params None.
/// /returns None.
/// </summary>
public enum LaserBeamBodyProfile
{
    RoundedTube = 0,
    TaperedJet = 1,
    DenseRibbon = 2
}

/// <summary>
/// Selects the cap-shape family used by Laser Beam source and impact visuals.
/// /params None.
/// /returns None.
/// </summary>
public enum LaserBeamCapShape
{
    BubbleBurst = 0,
    StarBloom = 1,
    SoftDisc = 2
}
#endregion

#region Runtime Payloads
/// <summary>
/// Stores scalable gameplay and presentation settings for the Laser Beam passive shooting override.
/// /params None.
/// /returns None.
/// </summary>
[Serializable]
public sealed class PowerUpLaserBeamModuleData
{
    #region Fields

    #region Serialized Fields
    [Header("Gameplay")]
    [Tooltip("Damage multiplier applied on top of the current projectile-derived damage budget used by Laser Beam ticks.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Speed multiplier applied to the virtual projectile speed that drives Laser Beam reach growth and throughput.")]
    [SerializeField] private float virtualProjectileSpeedMultiplier = 1f;

    [Tooltip("Seconds between consecutive Laser Beam damage applications while at least one lane is valid.")]
    [SerializeField] private float damageTickIntervalSeconds = 0.15f;

    [Tooltip("Maximum uninterrupted active time before Laser Beam overheats. Ignored when Cooldown Seconds is 0.")]
    [SerializeField] private float maximumContinuousActiveSeconds = 2.5f;

    [Tooltip("Cooldown applied only after the uninterrupted active-time cap is reached. When set to 0 the beam can remain active indefinitely while held.")]
    [SerializeField] private float cooldownSeconds = 2f;

    [Tooltip("Maximum number of reflected wall segments resolved per lane when bouncing projectiles are also active.")]
    [SerializeField] private int maximumBounceSegments = 0;

    [Header("Presentation")]
    [Tooltip("Predefined beam palette used by runtime materials and cap effects.")]
    [SerializeField] private LaserBeamVisualPalette visualPalette = LaserBeamVisualPalette.AntibioticBlue;

    [Tooltip("Body silhouette profile used by the beam shader and segment scaling.")]
    [SerializeField] private LaserBeamBodyProfile bodyProfile = LaserBeamBodyProfile.RoundedTube;

    [Tooltip("Shape family used by the source burst effect near the muzzle.")]
    [SerializeField] private LaserBeamCapShape sourceShape = LaserBeamCapShape.SoftDisc;

    [Tooltip("Shape family used by the impact effect at the current beam termination point.")]
    [SerializeField] private LaserBeamCapShape impactShape = LaserBeamCapShape.StarBloom;

    [Tooltip("Visual width multiplier applied to the rendered beam body.")]
    [SerializeField] private float bodyWidthMultiplier = 4.25f;

    [Tooltip("Additional width multiplier applied to gameplay collision checks performed by the beam.")]
    [SerializeField] private float collisionWidthMultiplier = 1f;

    [Tooltip("Scale multiplier applied to the source burst effect.")]
    [SerializeField] private float sourceScaleMultiplier = 1.35f;

    [Tooltip("Scale multiplier applied to the impact burst effect.")]
    [SerializeField] private float impactScaleMultiplier = 1.85f;

    [Tooltip("Overall opacity multiplier applied by the beam shader.")]
    [SerializeField] private float bodyOpacity = 0.96f;

    [Tooltip("Brightness multiplier applied to the inner beam core.")]
    [SerializeField] private float coreBrightness = 1.45f;

    [Tooltip("Brightness multiplier applied to the outer rim and liquid foam band.")]
    [SerializeField] private float rimBrightness = 1.32f;

    [Tooltip("Scroll speed used by the liquid flow pattern.")]
    [SerializeField] private float flowScrollSpeed = 1.15f;

    [Tooltip("Frequency of the rhythmic pulse applied to the flowing liquid highlight.")]
    [SerializeField] private float flowPulseFrequency = 0.4f;

    [Tooltip("Amplitude of the lateral wobble used by the liquid beam body.")]
    [SerializeField] private float wobbleAmplitude = 0.05f;

    [Tooltip("Speed of the secondary bubble drift pattern used by source and impact effects.")]
    [SerializeField] private float bubbleDriftSpeed = 1.4f;
    #endregion

    #endregion

    #region Properties
    public float DamageMultiplier
    {
        get
        {
            return damageMultiplier;
        }
    }

    public float VirtualProjectileSpeedMultiplier
    {
        get
        {
            return virtualProjectileSpeedMultiplier;
        }
    }

    public float DamageTickIntervalSeconds
    {
        get
        {
            return damageTickIntervalSeconds;
        }
    }

    public float MaximumContinuousActiveSeconds
    {
        get
        {
            return maximumContinuousActiveSeconds;
        }
    }

    public float CooldownSeconds
    {
        get
        {
            return cooldownSeconds;
        }
    }

    public int MaximumBounceSegments
    {
        get
        {
            return maximumBounceSegments;
        }
    }

    public LaserBeamVisualPalette VisualPalette
    {
        get
        {
            return visualPalette;
        }
    }

    public LaserBeamBodyProfile BodyProfile
    {
        get
        {
            return bodyProfile;
        }
    }

    public LaserBeamCapShape SourceShape
    {
        get
        {
            return sourceShape;
        }
    }

    public LaserBeamCapShape ImpactShape
    {
        get
        {
            return impactShape;
        }
    }

    public float BodyWidthMultiplier
    {
        get
        {
            return bodyWidthMultiplier;
        }
    }

    public float CollisionWidthMultiplier
    {
        get
        {
            return collisionWidthMultiplier;
        }
    }

    public float SourceScaleMultiplier
    {
        get
        {
            return sourceScaleMultiplier;
        }
    }

    public float ImpactScaleMultiplier
    {
        get
        {
            return impactScaleMultiplier;
        }
    }

    public float BodyOpacity
    {
        get
        {
            return bodyOpacity;
        }
    }

    public float CoreBrightness
    {
        get
        {
            return coreBrightness;
        }
    }

    public float RimBrightness
    {
        get
        {
            return rimBrightness;
        }
    }

    public float FlowScrollSpeed
    {
        get
        {
            return flowScrollSpeed;
        }
    }

    public float FlowPulseFrequency
    {
        get
        {
            return flowPulseFrequency;
        }
    }

    public float WobbleAmplitude
    {
        get
        {
            return wobbleAmplitude;
        }
    }

    public float BubbleDriftSpeed
    {
        get
        {
            return bubbleDriftSpeed;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Keeps the payload API symmetrical with the other modular payloads without mutating authored numeric values.
    /// /params None.
    /// /returns None.
    /// </summary>
    public void Validate()
    {
    }
    #endregion

    #endregion
}
#endregion
