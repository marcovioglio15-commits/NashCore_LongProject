using Unity.Entities;
using UnityEngine;

/// <summary>
/// Managed bridge reference to the visual Animator used by one player entity.
/// </summary>
public sealed class PlayerAnimatorReference : IComponentData
{
    #region Fields
    public Animator Animator;
    public RuntimeAnimatorController AnimatorController;
    public byte ControllerAssigned;
    #endregion
}

/// <summary>
/// Runtime hash configuration for animator parameters consumed by ECS animation sync.
/// </summary>
public struct PlayerAnimatorParameterConfig : IComponentData
{
    #region Fields
    public int MoveXHash;
    public int MoveYHash;
    public int MoveSpeedHash;
    public int AimXHash;
    public int AimYHash;
    public int IsMovingHash;
    public int IsShootingHash;
    public int IsDashingHash;
    public int ShotPulseHash;
    public int ProceduralRecoilHash;
    public int ProceduralAimWeightHash;
    public int ProceduralLeanHash;

    public float FloatDampTime;
    public float MovingSpeedThreshold;
    public float ProceduralRecoilKick;
    public float ProceduralRecoilRecoveryPerSecond;
    public float ProceduralAimWeightSmoothing;
    public float ProceduralLeanSmoothing;

    public byte UseFloatDamping;
    public byte DisableRootMotion;
    public byte ProceduralRecoilEnabled;
    public byte ProceduralAimWeightEnabled;
    public byte ProceduralLeanEnabled;

    public byte HasMoveX;
    public byte HasMoveY;
    public byte HasMoveSpeed;
    public byte HasAimX;
    public byte HasAimY;
    public byte HasIsMoving;
    public byte HasIsShooting;
    public byte HasIsDashing;
    public byte HasShotPulse;
    public byte HasProceduralRecoil;
    public byte HasProceduralAimWeight;
    public byte HasProceduralLean;
    #endregion
}

/// <summary>
/// Runtime animation bridge state used to detect one-frame transitions (e.g. shoot pulses).
/// </summary>
public struct PlayerAnimatorRuntimeState : IComponentData
{
    #region Fields
    public byte PreviousShooting;
    public float RecoilValue;
    public float AimWeightValue;
    public float LeanValue;
    #endregion
}
