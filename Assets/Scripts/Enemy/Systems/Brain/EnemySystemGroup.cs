using Unity.Entities;

/// <summary>
/// System group containing all enemy runtime systems.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerControllerSystemGroup))]
public sealed partial class EnemySystemGroup : ComponentSystemGroup
{
}
