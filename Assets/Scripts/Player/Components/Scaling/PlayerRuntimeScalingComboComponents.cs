using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Stores immutable combo runtime rules used to rebuild the active combo config whenever scalable stats change.
/// none.
/// returns none.
/// </summary>
public struct PlayerBaseComboCounterConfig : IComponentData
{
    public byte Enabled;
    public int ComboGainPerKill;
    public PlayerComboDamageBreakMode DamageBreakMode;
    public byte ShieldDamageBreaksCombo;
}

/// <summary>
/// Stores the current combo runtime rules after progression Add Scaling formulas are resolved.
/// none.
/// returns none.
/// </summary>
public struct PlayerRuntimeComboCounterConfig : IComponentData
{
    public byte Enabled;
    public int ComboGainPerKill;
    public PlayerComboDamageBreakMode DamageBreakMode;
    public byte ShieldDamageBreaksCombo;
}

/// <summary>
/// Stores one immutable combo-rank milestone, point-decay rate, and flattened Character Tuning formula range used by that rank.
/// none.
/// returns none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerBaseComboRankElement : IBufferElementData
{
    public FixedString64Bytes RankId;
    public int RequiredComboValue;
    public float PointsDecayPerSecond;
    public int BonusFormulaStartIndex;
    public int BonusFormulaCount;
}

/// <summary>
/// Stores one current combo-rank milestone and point-decay rate after progression Add Scaling formulas are resolved.
/// none.
/// returns none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeComboRankElement : IBufferElementData
{
    public FixedString64Bytes RankId;
    public int RequiredComboValue;
    public float PointsDecayPerSecond;
    public int BonusFormulaStartIndex;
    public int BonusFormulaCount;
}

/// <summary>
/// Identifies one combo runtime field that can be rebuilt from a progression Add Scaling rule.
/// none.
/// returns none.
/// </summary>
public enum PlayerRuntimeComboCounterFieldId : byte
{
    Enabled = 0,
    ComboGainPerKill = 1,
    ShieldDamageBreaksCombo = 2,
    DamageBreakMode = 3,
    RankRequiredComboValue = 4,
    RankPointsDecayPerSecond = 5
}

/// <summary>
/// Stores one combo scaling metadata entry baked from progression Add Scaling authoring data.
/// none.
/// returns none.
/// </summary>
[InternalBufferCapacity(0)]
public struct PlayerRuntimeComboCounterScalingElement : IBufferElementData
{
    public PlayerRuntimeComboCounterFieldId FieldId;
    public int RankIndex;
    public byte ValueType;
    public float BaseValue;
    public byte BaseBooleanValue;
    public byte IsInteger;
    public FixedString512Bytes Formula;
}
