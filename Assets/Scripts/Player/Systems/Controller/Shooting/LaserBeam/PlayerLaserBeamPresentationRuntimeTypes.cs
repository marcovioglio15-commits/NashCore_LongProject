using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Stores one pooled root instance that owns all managed visuals for a single player beam.
/// /params None.
/// /returns None.
/// </summary>
internal sealed class PlayerLaserBeamManagedInstance
{
    #region Fields
    public GameObject RootObject;
    public Transform RootTransform;
    public readonly List<PlayerLaserBeamManagedBodyVisual> BodyVisuals = new List<PlayerLaserBeamManagedBodyVisual>(64);
    public readonly List<PlayerLaserBeamManagedParticleVisual> SourceVisuals = new List<PlayerLaserBeamManagedParticleVisual>(8);
    public readonly List<PlayerLaserBeamManagedParticleVisual> ImpactVisuals = new List<PlayerLaserBeamManagedParticleVisual>(8);
    #endregion
}

/// <summary>
/// Stores one pooled mesh-based body visual instance.
/// /params None.
/// /returns None.
/// </summary>
internal sealed class PlayerLaserBeamManagedBodyVisual
{
    #region Fields
    public GameObject SourcePrefab;
    public GameObject InstanceObject;
    public Transform RootTransform;
    public Renderer[] Renderers;
    #endregion
}

/// <summary>
/// Stores one pooled particle visual instance used for the beam source or impact.
/// /params None.
/// /returns None.
/// </summary>
internal sealed class PlayerLaserBeamManagedParticleVisual
{
    #region Fields
    public GameObject SourcePrefab;
    public GameObject InstanceObject;
    public Transform RootTransform;
    public ParticleSystem[] ParticleSystems;
    public ParticleSystemRenderer[] Renderers;
    #endregion
}

/// <summary>
/// Stores one render-time body blob sample derived from the authoritative gameplay lanes.
/// /params None.
/// /returns None.
/// </summary>
internal struct PlayerLaserBeamBodySample
{
    #region Fields
    public float3 Position;
    public quaternion Rotation;
    public float Length;
    public float Width;
    #endregion
}

/// <summary>
/// Stores the render-time start and end anchors of one beam lane.
/// /params None.
/// /returns None.
/// </summary>
internal struct PlayerLaserBeamLaneEndpoint
{
    #region Fields
    public int LaneIndex;
    public float3 StartPoint;
    public float3 StartDirection;
    public float StartWidth;
    public float3 EndPoint;
    public float3 EndDirection;
    public float EndWidth;
    public float3 TerminalNormal;
    #endregion
}

/// <summary>
/// Stores the resolved managed palette colors used by body and particle visuals.
/// /params None.
/// /returns None.
/// </summary>
internal struct PlayerLaserBeamResolvedPalette
{
    #region Fields
    public Color BodyColorA;
    public Color BodyColorB;
    public Color CoreColor;
    public Color RimColor;
    #endregion
}
