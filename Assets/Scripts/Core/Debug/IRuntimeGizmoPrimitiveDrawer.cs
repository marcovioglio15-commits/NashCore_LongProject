using UnityEngine;

/// <summary>
/// Defines the primitive drawing operations required by the shared runtime gizmo rendering utility.
/// /params none.
/// /returns none.
/// </summary>
public interface IRuntimeGizmoPrimitiveDrawer
{
    #region Methods

    #region Drawing
    /// <summary>
    /// Draws one planar wire disc centered on the supplied world position.
    /// /params center: World-space center of the disc.
    /// /params radius: Radius expressed in gameplay world units.
    /// /params color: Final line color used by the active rendering backend.
    /// /returns void.
    /// </summary>
    void DrawWireDisc(Vector3 center, float radius, Color color);

    /// <summary>
    /// Draws one directional indicator starting from a world-space origin.
    /// /params origin: World-space starting point of the vector.
    /// /params direction: World-space direction expected to be normalized or safely normalizable.
    /// /params length: Final vector length expressed in gameplay world units.
    /// /params color: Final line color used by the active rendering backend.
    /// /returns void.
    /// </summary>
    void DrawDirection(Vector3 origin, Vector3 direction, float length, Color color);

    /// <summary>
    /// Draws one straight world-space link between two positions.
    /// /params start: Link starting point in world space.
    /// /params end: Link end point in world space.
    /// /params color: Final line color used by the active rendering backend.
    /// /returns void.
    /// </summary>
    void DrawLink(Vector3 start, Vector3 end, Color color);

    /// <summary>
    /// Draws one compact marker used to highlight a world-space point of interest.
    /// /params position: World-space marker position.
    /// /params radius: Marker size hint expressed in gameplay world units.
    /// /params color: Final marker color used by the active rendering backend.
    /// /returns void.
    /// </summary>
    void DrawMarker(Vector3 position, float radius, Color color);

    /// <summary>
    /// Draws one short text label anchored to a world-space position.
    /// /params position: World-space label anchor.
    /// /params text: Text shown by the active rendering backend.
    /// /returns void.
    /// </summary>
    void DrawLabel(Vector3 position, string text);
    #endregion

    #endregion
}
