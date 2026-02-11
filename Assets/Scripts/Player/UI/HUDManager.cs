using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages player HUD widgets and updates the player health bar from ECS data.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDManager : MonoBehaviour
{
    #region Fields

    #region Serialized Fields
    [Header("Health")]
    [Tooltip("UI Image used as fillable health bar. Fill method should be Horizontal or Radial.")]
    [SerializeField] private Image playerHealthFillImage;

    [Tooltip("Seconds used to smooth visual fill transitions. Set 0 for immediate updates.")]
    [SerializeField] private float healthBarSmoothingSeconds = 0.08f;

    [Tooltip("Hide health bar image when no player entity with PlayerHealth is available.")]
    [SerializeField] private bool hideHealthBarWhenPlayerMissing = true;
    #endregion

    private World defaultWorld;
    private EntityManager entityManager;
    private EntityQuery playerHealthQuery;
    private bool playerHealthQueryInitialized;
    private float displayedHealthNormalized = 1f;
    #endregion

    #region Methods

    #region Unity Methods
    private void Awake()
    {
        if (healthBarSmoothingSeconds < 0f)
            healthBarSmoothingSeconds = 0f;

        TryInitializeEcsBindings();
        ApplyHealthFill(displayedHealthNormalized);
    }

    private void Update()
    {
        if (playerHealthFillImage == null)
            return;

        if (TryInitializeEcsBindings() == false)
        {
            HandleMissingPlayer();
            return;
        }

        if (playerHealthQuery.IsEmptyIgnoreFilter)
        {
            HandleMissingPlayer();
            return;
        }

        int playerCount = playerHealthQuery.CalculateEntityCount();

        if (playerCount != 1)
        {
            HandleMissingPlayer();
            return;
        }

        Entity playerEntity = playerHealthQuery.GetSingletonEntity();

        if (entityManager.Exists(playerEntity) == false)
        {
            HandleMissingPlayer();
            return;
        }

        if (entityManager.HasComponent<PlayerHealth>(playerEntity) == false)
        {
            HandleMissingPlayer();
            return;
        }

        PlayerHealth playerHealth = entityManager.GetComponentData<PlayerHealth>(playerEntity);
        float targetNormalizedValue = 0f;

        if (playerHealth.Max > 0f)
            targetNormalizedValue = Mathf.Clamp01(playerHealth.Current / playerHealth.Max);

        if (healthBarSmoothingSeconds <= 0f)
            displayedHealthNormalized = targetNormalizedValue;
        else
            displayedHealthNormalized = Mathf.MoveTowards(displayedHealthNormalized,
                                                          targetNormalizedValue,
                                                          Time.deltaTime / healthBarSmoothingSeconds);

        ApplyHealthFill(displayedHealthNormalized);
    }
    #endregion

    #region Helpers
    private bool TryInitializeEcsBindings()
    {
        World currentWorld = World.DefaultGameObjectInjectionWorld;

        if (currentWorld == null || currentWorld.IsCreated == false)
        {
            defaultWorld = null;
            playerHealthQueryInitialized = false;
            return false;
        }

        if (ReferenceEquals(defaultWorld, currentWorld) == false)
        {
            defaultWorld = currentWorld;
            playerHealthQueryInitialized = false;
        }

        entityManager = defaultWorld.EntityManager;

        if (playerHealthQueryInitialized == false)
        {
            EntityQueryDesc queryDescription = new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PlayerControllerConfig>(),
                    ComponentType.ReadOnly<PlayerHealth>()
                }
            };

            playerHealthQuery = entityManager.CreateEntityQuery(queryDescription);
            playerHealthQueryInitialized = true;
        }

        return playerHealthQueryInitialized;
    }

    private void HandleMissingPlayer()
    {
        if (hideHealthBarWhenPlayerMissing)
            playerHealthFillImage.enabled = false;
        else
            playerHealthFillImage.enabled = true;
    }

    private void ApplyHealthFill(float normalizedValue)
    {
        playerHealthFillImage.enabled = true;
        playerHealthFillImage.fillAmount = Mathf.Clamp01(normalizedValue);
    }
    #endregion

    #endregion
}
