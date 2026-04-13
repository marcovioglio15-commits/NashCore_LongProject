using Unity.Entities;

#region Components
/// <summary>
/// Runtime aggregated elemental status values applied to the player.
/// </summary>
public struct PlayerElementalRuntimeState : IComponentData
{
    public float SlowPercent;
}

/// <summary>
/// Elemental stack payload stored per player and per element type.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerElementStackElement : IBufferElementData
{
    public ElementType ElementType;
    public ElementalEffectKind EffectKind;
    public ElementalProcMode ProcMode;
    public ElementalProcReapplyMode ReapplyMode;
    public float CurrentStacks;
    public float MaximumStacks;
    public float ProcThresholdStacks;
    public float StackDecayPerSecond;
    public byte ConsumeStacksOnProc;
    public float DotDamagePerTick;
    public float DotTickInterval;
    public float DotDurationSeconds;
    public float DotRemainingSeconds;
    public float DotTickTimer;
    public float ImpedimentSlowPercentPerStack;
    public float ImpedimentProcSlowPercent;
    public float ImpedimentMaxSlowPercent;
    public float ImpedimentDurationSeconds;
    public float ImpedimentRemainingSeconds;
    public float CurrentImpedimentSlowPercent;
}
#endregion
