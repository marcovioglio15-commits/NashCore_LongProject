using UnityEngine;

/// <summary>
/// Marks the animated transform that should drive the player's projectile origin at runtime.
/// Attach this component to the visual prefab root and assign one transform that follows the animated weapon.
/// /params None.
/// /returns None.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerVisualMuzzleAnchor : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Muzzle")]
    [Tooltip("Animated transform used as the authoritative projectile origin for the player visual.")]
    [SerializeField] private Transform muzzleTransform;

    [Tooltip("When enabled, draws a selected gizmo that visualizes the muzzle forward axis.")]
    [SerializeField] private bool drawDebugGizmos = true;

    [Tooltip("Length in world units of the muzzle forward gizmo ray.")]
    [SerializeField] private float debugRayLength = 0.45f;

    [Tooltip("Color used by the muzzle debug ray and origin sphere.")]
    [SerializeField] private Color debugGizmoColor = new Color(1f, 0.52f, 0.18f, 1f);
    #endregion

    #endregion

    #region Properties
    public Transform MuzzleTransform
    {
        get
        {
            if (muzzleTransform != null)
                return muzzleTransform;

            return transform;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnValidate()
    {
        if (debugRayLength < 0f)
            debugRayLength = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Transform resolvedMuzzleTransform = MuzzleTransform;

        if (resolvedMuzzleTransform == null)
            return;

        Gizmos.color = debugGizmoColor;
        Vector3 origin = resolvedMuzzleTransform.position;
        Vector3 forward = resolvedMuzzleTransform.forward;
        Gizmos.DrawSphere(origin, 0.03f);
        Gizmos.DrawLine(origin, origin + forward * debugRayLength);
    }
    #endregion

    #endregion
}
