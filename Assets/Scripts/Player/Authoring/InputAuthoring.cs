using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class InputAuthoring : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Input action asset used to drive the player controller at runtime.")]
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActionsAsset;

    [Tooltip("Optional PlayerAuthoring source for resolving the controller preset. Leave empty when the player is baked in a SubScene.")]
    [Header("Preset Sources")]
    [SerializeField] private PlayerAuthoring playerAuthoringSource;

    [Tooltip("Optional master preset used when a PlayerAuthoring source is not available (recommended for SubScene workflows).")]
    [SerializeField] private PlayerMasterPreset masterPresetOverride;

    [Tooltip("Optional controller preset override used when neither a PlayerAuthoring source nor a master preset is available.")]
    [SerializeField] private PlayerControllerPreset controllerPresetOverride;
    #endregion

    #region Private
    private PlayerAuthoring playerAuthoring;
    #endregion
    #endregion

    #region Methods

    #region Unity Methods
    private void OnEnable()
    {
        if (inputActionsAsset == null)
        {
            PlayerInputRuntime.Shutdown();
            return;
        }

        PlayerControllerPreset controllerPreset = ResolveControllerPreset();
        string moveActionId = string.Empty;
        string lookActionId = string.Empty;
        string shootActionId = string.Empty;

        if (controllerPreset != null)
        {
            moveActionId = controllerPreset.MoveActionId;
            lookActionId = controllerPreset.LookActionId;
            shootActionId = controllerPreset.ShootActionId;
        }

        PlayerInputRuntime.Initialize(inputActionsAsset, moveActionId, lookActionId, shootActionId);

        #if UNITY_EDITOR
        LogPlayerEntitiesPresence();
        #endif
    }

    private void OnDisable()
    {
        PlayerInputRuntime.Shutdown();
    }
    #endregion

    #region Helpers
    private PlayerControllerPreset ResolveControllerPreset()
    {
        PlayerAuthoring authoringSource = playerAuthoringSource;

        if (authoringSource != null)
        {
            PlayerControllerPreset presetFromSource = authoringSource.GetControllerPreset();

            if (presetFromSource != null)
                return presetFromSource;
        }

        PlayerAuthoring authoring = playerAuthoring;

        if (authoring == null)
            authoring = GetComponent<PlayerAuthoring>();

        if (authoring != null)
        {
            playerAuthoring = authoring;
            PlayerControllerPreset presetFromAuthoring = authoring.GetControllerPreset();

            if (presetFromAuthoring != null)
                return presetFromAuthoring;
        }

        PlayerMasterPreset masterPreset = masterPresetOverride;

        if (masterPreset != null)
        {
            PlayerControllerPreset presetFromMaster = masterPreset.ControllerPreset;

            if (presetFromMaster != null)
                return presetFromMaster;
        }

        if (controllerPresetOverride != null)
            return controllerPresetOverride;

        return null;
    }

    #if UNITY_EDITOR
    private static void LogPlayerEntitiesPresence()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null)
        {
            Debug.LogWarning("[InputAuthoring] Default world not available. ECS might not be initialized yet.");
            return;
        }

        EntityManager entityManager = world.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControllerConfig>());
        int count = query.CalculateEntityCount();
        query.Dispose();

        if (count > 0)
        {
            Debug.Log(string.Format("[InputAuthoring] PlayerControllerConfig entities found: {0}", count));
            return;
        }

        Debug.LogWarning("[InputAuthoring] No PlayerControllerConfig entities found. Player baking/spawn might be missing.");
    }
    #endif
    #endregion

    #endregion
}
