using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Declares one drop-definition entry for experience drops.
/// </summary>
[Serializable]
public sealed class EnemyExperienceDropDefinitionData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Prefab spawned for this experience drop entry.")]
    [SerializeField] private GameObject dropPrefab;

    [Tooltip("Experience granted when this drop is collected by the player.")]
    [SerializeField] private float experienceAmount = 1f;
    #endregion

    #endregion

    #region Properties
    public GameObject DropPrefab
    {
        get
        {
            return dropPrefab;
        }
    }

    public float ExperienceAmount
    {
        get
        {
            return experienceAmount;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Normalizes one experience drop-definition entry.
    /// </summary>
    /// <returns>Void.</returns>
    public void Validate()
    {
        if (experienceAmount < 0f)
            experienceAmount = 0f;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores movement settings used by spawned experience drops.
/// </summary>
[Serializable]
public sealed class EnemyExperienceDropCollectionSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Movement speed used while drops move toward the player.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("Distance from the player used to convert the drop into experience.")]
    [SerializeField] private float collectDistance = 0.3f;

    [Tooltip("Linear multiplier applied to collect distance using current player speed. Effective distance = Collect Distance + (Player Speed * this value).")]
    [SerializeField] private float collectDistancePerPlayerSpeed = 0.05f;

    [Tooltip("Minimum duration (seconds) used for the initial spawn spread animation.")]
    [SerializeField] private float spawnAnimationMinDuration = 0.08f;

    [Tooltip("Maximum duration (seconds) used for the initial spawn spread animation.")]
    [SerializeField] private float spawnAnimationMaxDuration = 0.16f;
    #endregion

    #endregion

    #region Properties
    public float MoveSpeed
    {
        get
        {
            return moveSpeed;
        }
    }

    public float CollectDistance
    {
        get
        {
            return collectDistance;
        }
    }

    public float CollectDistancePerPlayerSpeed
    {
        get
        {
            return collectDistancePerPlayerSpeed;
        }
    }

    public float SpawnAnimationMinDuration
    {
        get
        {
            return spawnAnimationMinDuration;
        }
    }

    public float SpawnAnimationMaxDuration
    {
        get
        {
            return spawnAnimationMaxDuration;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Normalizes movement settings used by collected experience drops.
    /// </summary>
    /// <returns>Void.</returns>
    public void Validate()
    {
        if (moveSpeed < 0f)
            moveSpeed = 0f;

        if (collectDistance < 0.01f)
            collectDistance = 0.01f;

        if (collectDistancePerPlayerSpeed < 0f)
            collectDistancePerPlayerSpeed = 0f;

        if (spawnAnimationMinDuration < 0f)
            spawnAnimationMinDuration = 0f;

        if (spawnAnimationMaxDuration < spawnAnimationMinDuration)
            spawnAnimationMaxDuration = spawnAnimationMinDuration;
    }
    #endregion

    #endregion
}

/// <summary>
/// Stores experience drop payload values used by DropItems modules.
/// </summary>
[Serializable]
public sealed class EnemyExperienceDropPayload
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Definitions catalog used to compose the spawned experience drops.")]
    [SerializeField] private List<EnemyExperienceDropDefinitionData> dropDefinitions = new List<EnemyExperienceDropDefinitionData>();

    [Tooltip("Minimum total experience dropped at enemy death, distributed over the defined drop entries.")]
    [FormerlySerializedAs("complessiveExperienceDrop")]
    [SerializeField] private float complessiveExperienceDropMinimum = 10f;

    [Tooltip("Maximum total experience dropped at enemy death, distributed over the defined drop entries.")]
    [SerializeField] private float complessiveExperienceDropMaximum;

    [Tooltip("Distribution bias for drop composition: 0 favors low-value drops count, 1 favors high-value drops count.")]
    [Range(0f, 1f)]
    [SerializeField] private float dropsDistribution = 0.5f;

    [Tooltip("Radius around the enemy used to distribute spawned drops uniformly.")]
    [SerializeField] private float dropRadius = 0.6f;

    [Tooltip("Collection movement settings applied to spawned experience drops.")]
    [SerializeField] private EnemyExperienceDropCollectionSettings collectionMovement = new EnemyExperienceDropCollectionSettings();
    #endregion

    #endregion

    #region Properties
    public IReadOnlyList<EnemyExperienceDropDefinitionData> DropDefinitions
    {
        get
        {
            return dropDefinitions;
        }
    }

    public float ComplessiveExperienceDropMinimum
    {
        get
        {
            return complessiveExperienceDropMinimum;
        }
    }

    public float ComplessiveExperienceDropMaximum
    {
        get
        {
            return complessiveExperienceDropMaximum;
        }
    }

    public float DropsDistribution
    {
        get
        {
            return dropsDistribution;
        }
    }

    public float DropRadius
    {
        get
        {
            return dropRadius;
        }
    }

    public EnemyExperienceDropCollectionSettings CollectionMovement
    {
        get
        {
            return collectionMovement;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Normalizes experience payload values and nested drop definitions.
    /// </summary>
    /// <returns>Void.</returns>
    public void Validate()
    {
        if (dropDefinitions == null)
            dropDefinitions = new List<EnemyExperienceDropDefinitionData>();

        for (int index = dropDefinitions.Count - 1; index >= 0; index--)
        {
            EnemyExperienceDropDefinitionData definition = dropDefinitions[index];

            if (definition == null)
            {
                dropDefinitions.RemoveAt(index);
                continue;
            }

            definition.Validate();
        }

        if (dropsDistribution < 0f)
            dropsDistribution = 0f;

        if (dropsDistribution > 1f)
            dropsDistribution = 1f;

        if (dropRadius < 0f)
            dropRadius = 0f;

        if (collectionMovement == null)
            collectionMovement = new EnemyExperienceDropCollectionSettings();

        collectionMovement.Validate();
    }
    #endregion

    #endregion
}

/// <summary>
/// Groups DropItems module selection and payload settings.
/// </summary>
[Serializable]
public sealed class EnemyDropItemsModuleData
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Drop payload category used by this module.")]
    [SerializeField] private EnemyDropItemsPayloadKind dropPayloadKind = EnemyDropItemsPayloadKind.Experience;

    [Tooltip("Experience payload used when Drop Payload Kind is Experience.")]
    [SerializeField] private EnemyExperienceDropPayload experience = new EnemyExperienceDropPayload();
    #endregion

    #endregion

    #region Properties
    public EnemyDropItemsPayloadKind DropPayloadKind
    {
        get
        {
            return dropPayloadKind;
        }
    }

    public EnemyExperienceDropPayload Experience
    {
        get
        {
            return experience;
        }
    }
    #endregion

    #region Methods

    #region Validation
    /// <summary>
    /// Normalizes DropItems module selection and nested payload values.
    /// </summary>
    /// <returns>Void.</returns>
    public void Validate()
    {
        switch (dropPayloadKind)
        {
            case EnemyDropItemsPayloadKind.Experience:
                break;

            default:
                dropPayloadKind = EnemyDropItemsPayloadKind.Experience;
                break;
        }

        if (experience == null)
            experience = new EnemyExperienceDropPayload();

        experience.Validate();
    }
    #endregion

    #endregion
}
