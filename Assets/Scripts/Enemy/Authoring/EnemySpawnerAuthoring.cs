using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring component that configures pooled enemy spawning.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawnerAuthoring : MonoBehaviour
{
    #region Constants
    private static readonly Color SpawnRadiusGizmoColor = new Color(0.2f, 0.85f, 0.35f, 0.9f);
    private static readonly Color DespawnRadiusGizmoColor = new Color(1f, 0.65f, 0.2f, 0.9f);
    #endregion

    #region Fields

    #region Serialized Fields
    [Header("Prefab")]
    [Tooltip("Enemy prefab converted to ECS entities and used by this spawner pool.")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn Timing")]
    [Tooltip("Delay in seconds before the first spawn tick is executed.")]
    [SerializeField] private float initialSpawnDelay;

    [Tooltip("Number of enemies spawned every spawn tick.")]
    [SerializeField] private int spawnPerTick = 4;

    [Tooltip("Interval in seconds between two consecutive spawn ticks.")]
    [SerializeField] private float spawnInterval = 0.5f;

    [Header("Pool")]
    [Tooltip("Number of enemies prewarmed in the pool during initialization.")]
    [SerializeField] private int initialPoolCapacity = 128;

    [Tooltip("Minimum number of enemies instantiated when the pool needs expansion.")]
    [SerializeField] private int expandBatch = 64;

    [Tooltip("Maximum amount of simultaneously alive enemies for this spawner.")]
    [SerializeField] private int maxAliveCount = 1024;

    [Header("Spawn Shape")]
    [Tooltip("Radius around the spawner used to generate random spawn positions.")]
    [SerializeField] private float spawnRadius = 12f;

    [Tooltip("Vertical offset added to spawn positions.")]
    [SerializeField] private float spawnHeightOffset;

    [Header("Lifecycle")]
    [Tooltip("Distance from the player beyond which alive enemies return to this pool. Set to 0 to disable.")]
    [SerializeField] private float despawnDistance = 85f;

    [Header("Random")]
    [Tooltip("Deterministic random seed for this spawner. Set to 0 for auto-generated seed.")]
    [SerializeField] private uint randomSeed;

    [Header("Debug Gizmos")]
    [Tooltip("Draw the spawn radius gizmo when the spawner is selected.")]
    [SerializeField] private bool drawSpawnRadiusGizmo = true;

    [Tooltip("Draw the despawn radius gizmo when the spawner is selected.")]
    [SerializeField] private bool drawDespawnRadiusGizmo = true;
    #endregion

    #endregion

    #region Properties
    public GameObject EnemyPrefab
    {
        get
        {
            return enemyPrefab;
        }
    }

    public float InitialSpawnDelay
    {
        get
        {
            return initialSpawnDelay;
        }
    }

    public int SpawnPerTick
    {
        get
        {
            return spawnPerTick;
        }
    }

    public float SpawnInterval
    {
        get
        {
            return spawnInterval;
        }
    }

    public int InitialPoolCapacity
    {
        get
        {
            return initialPoolCapacity;
        }
    }

    public int ExpandBatch
    {
        get
        {
            return expandBatch;
        }
    }

    public int MaxAliveCount
    {
        get
        {
            return maxAliveCount;
        }
    }

    public float SpawnRadius
    {
        get
        {
            return spawnRadius;
        }
    }

    public float SpawnHeightOffset
    {
        get
        {
            return spawnHeightOffset;
        }
    }

    public float DespawnDistance
    {
        get
        {
            return despawnDistance;
        }
    }

    public uint RandomSeed
    {
        get
        {
            return randomSeed;
        }
    }
    #endregion

    #region Methods

    #region Unity Methods
    private void OnValidate()
    {
        if (initialSpawnDelay < 0f)
            initialSpawnDelay = 0f;

        if (spawnPerTick < 1)
            spawnPerTick = 1;

        if (spawnInterval < 0.01f)
            spawnInterval = 0.01f;

        if (initialPoolCapacity < 0)
            initialPoolCapacity = 0;

        if (expandBatch < 1)
            expandBatch = 1;

        if (maxAliveCount < spawnPerTick)
            maxAliveCount = spawnPerTick;

        if (spawnRadius < 0f)
            spawnRadius = 0f;

        if (despawnDistance < 0f)
            despawnDistance = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        if (drawSpawnRadiusGizmo)
        {
            float radius = math.max(0f, spawnRadius);

            if (radius > 0f)
            {
                Gizmos.color = SpawnRadiusGizmoColor;
                Gizmos.DrawWireSphere(center, radius);
            }
        }

        if (drawDespawnRadiusGizmo)
        {
            float radius = math.max(0f, despawnDistance);

            if (radius > 0f)
            {
                Gizmos.color = DespawnRadiusGizmoColor;
                Gizmos.DrawWireSphere(center, radius);
            }
        }
    }
    #endregion

    #endregion
}

/// <summary>
/// Bakes EnemySpawnerAuthoring data into ECS spawner components.
/// </summary>
public sealed class EnemySpawnerAuthoringBaker : Baker<EnemySpawnerAuthoring>
{
    #region Methods

    #region Bake
    public override void Bake(EnemySpawnerAuthoring authoring)
    {
        if (authoring == null)
            return;

        if (authoring.EnemyPrefab == null)
            return;

        Entity spawnerEntity = GetEntity(TransformUsageFlags.Dynamic);
        Entity enemyPrefabEntity = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic);

        uint seed = ResolveSeed(authoring);

        int spawnPerTick = math.max(1, authoring.SpawnPerTick);
        int maxAliveCount = math.max(spawnPerTick, authoring.MaxAliveCount);

        AddComponent(spawnerEntity, new EnemySpawner
        {
            EnemyPrefab = enemyPrefabEntity,
            SpawnPerTick = spawnPerTick,
            SpawnInterval = math.max(0.01f, authoring.SpawnInterval),
            InitialSpawnDelay = math.max(0f, authoring.InitialSpawnDelay),
            InitialPoolCapacity = math.max(0, authoring.InitialPoolCapacity),
            ExpandBatch = math.max(1, authoring.ExpandBatch),
            MaxAliveCount = maxAliveCount,
            SpawnRadius = math.max(0f, authoring.SpawnRadius),
            SpawnHeightOffset = authoring.SpawnHeightOffset,
            DespawnDistance = math.max(0f, authoring.DespawnDistance)
        });

        AddComponent(spawnerEntity, new EnemySpawnerState
        {
            NextSpawnTime = 0f,
            AliveCount = 0,
            RandomState = seed,
            Initialized = 0
        });

        AddBuffer<EnemyPoolElement>(spawnerEntity);
    }
    #endregion

    #region Helpers
    private static uint ResolveSeed(EnemySpawnerAuthoring authoring)
    {
        uint seed = authoring.RandomSeed;

        if (seed != 0u)
            return seed;

        int instanceId = authoring.gameObject.GetInstanceID();

        if (instanceId < 0)
            instanceId = -instanceId;

        seed = (uint)instanceId;

        if (seed == 0u)
            seed = 1u;

        return seed;
    }
    #endregion

    #endregion
}
