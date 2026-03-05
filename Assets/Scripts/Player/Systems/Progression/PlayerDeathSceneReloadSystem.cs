using Unity.Entities;
using UnityEngine.SceneManagement;

/// <summary>
/// Reloads the active scene once when a player reaches zero health.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EnemySystemGroup))]
public partial struct PlayerDeathSceneReloadSystem : ISystem
{
    #region Fields
    private bool reloadQueued;
    #endregion

    #region Methods

    #region Lifecycle
    /// <summary>
    /// Declares the runtime requirement for player health data.
    /// </summary>
    /// <param name="state">System state used to configure update requirements.</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerHealth>();
        state.RequireForUpdate<PlayerControllerConfig>();
    }

    /// <summary>
    /// Checks player health and reloads the current scene when death is detected.
    /// </summary>
    /// <param name="state">System state used by ECS while running the update.</param>
    public void OnUpdate(ref SystemState state)
    {
        bool hasAnyPlayer = false;
        bool hasDeadPlayer = false;

        foreach (RefRO<PlayerHealth> playerHealth in SystemAPI.Query<RefRO<PlayerHealth>>().WithAll<PlayerControllerConfig>())
        {
            hasAnyPlayer = true;

            if (playerHealth.ValueRO.Current > 0f)
                continue;

            hasDeadPlayer = true;
            break;
        }

        if (hasAnyPlayer == false)
            return;

        if (hasDeadPlayer == false)
        {
            reloadQueued = false;
            return;
        }

        if (reloadQueued)
            return;

        reloadQueued = true;
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.IsValid() == false)
            return;

        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex, LoadSceneMode.Single);
            return;
        }

        if (string.IsNullOrWhiteSpace(activeScene.name))
            return;

        SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
    }
    #endregion

    #endregion
}
