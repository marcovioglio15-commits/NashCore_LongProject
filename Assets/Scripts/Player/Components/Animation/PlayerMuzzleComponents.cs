using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Stores the current world-space muzzle pose resolved from the managed player visual.
/// Used by gameplay systems that need projectile origins aligned with animated weapon motion.
/// /params None.
/// /returns None.
/// </summary>
public struct PlayerAnimatedMuzzleWorldPose : IComponentData
{
    #region Fields
    public float3 Position;
    public quaternion Rotation;
    public byte IsValid;
    #endregion
}
