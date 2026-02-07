using System;
using UnityEngine;

[Serializable]
public sealed class ShootingSettings
{
    #region Serialized Fields
    [Header("Shooting Trigger")]
    [Tooltip("Defines whether shooting is toggled on/off or triggered once per button press.")]
    [SerializeField] private ShootingTriggerMode triggerMode = ShootingTriggerMode.AutomaticToggle;

    [Tooltip("When enabled, projectile movement keeps inheriting the shooter's horizontal velocity after spawn.")]
    [SerializeField] private bool projectilesInheritPlayerSpeed;

    [Header("Shooting References")]
    [Tooltip("Projectile prefab to spawn at runtime when shooting.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Local offset from the weapon reference set on PlayerAuthoring (or player transform when no reference is assigned).")]
    [SerializeField] private Vector3 shootOffset = new Vector3(0f, 0.5f, 0.8f);

    [Header("Shooting Values")]
    [Tooltip("Numeric shooting tuning values.")]
    [SerializeField] private ShootingValues values = new ShootingValues();

    [Header("Object Pool")]
    [Tooltip("Number of projectiles pre-created when the shooter pool initializes.")]
    [SerializeField] private int initialPoolCapacity = 512;

    [Tooltip("Number of projectiles created when the pool needs expansion.")]
    [SerializeField] private int poolExpandBatch = 128;
    #endregion

    #region Properties
    public ShootingTriggerMode TriggerMode
    {
        get
        {
            return triggerMode;
        }
    }

    public GameObject ProjectilePrefab
    {
        get
        {
            return projectilePrefab;
        }
    }

    public bool ProjectilesInheritPlayerSpeed
    {
        get
        {
            return projectilesInheritPlayerSpeed;
        }
    }

    public Vector3 ShootOffset
    {
        get
        {
            return shootOffset;
        }
    }

    public ShootingValues Values
    {
        get
        {
            return values;
        }
    }

    public int InitialPoolCapacity
    {
        get
        {
            return initialPoolCapacity;
        }
    }

    public int PoolExpandBatch
    {
        get
        {
            return poolExpandBatch;
        }
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (values == null)
            values = new ShootingValues();

        values.Validate();

        if (initialPoolCapacity < 0)
            initialPoolCapacity = 0;

        if (poolExpandBatch < 1)
            poolExpandBatch = 1;
    }
    #endregion
}

[Serializable]
public sealed class ShootingValues
{
    #region Serialized Fields
    [Tooltip("Projectile speed in world units per second.")]
    [SerializeField] private float shootSpeed = 24f;

    [Tooltip("Number of projectiles fired per second.")]
    [SerializeField] private float rateOfFire = 8f;

    [Tooltip("Optional maximum travel distance before despawn. Zero or negative disables range despawn.")]
    [SerializeField] private float range = 0f;

    [Tooltip("Optional maximum lifetime in seconds before despawn. Zero or negative disables lifetime despawn.")]
    [SerializeField] private float lifetime = 0f;

    [Tooltip("Damage applied by each projectile.")]
    [SerializeField] private float damage = 1f;
    #endregion

    #region Properties
    public float ShootSpeed
    {
        get
        {
            return shootSpeed;
        }
    }

    public float RateOfFire
    {
        get
        {
            return rateOfFire;
        }
    }

    public float Range
    {
        get
        {
            return range;
        }
    }

    public float Lifetime
    {
        get
        {
            return lifetime;
        }
    }

    public float Damage
    {
        get
        {
            return damage;
        }
    }
    #endregion

    #region Validation
    public void Validate()
    {
        if (shootSpeed < 0f)
            shootSpeed = 0f;

        if (rateOfFire < 0f)
            rateOfFire = 0f;

        if (damage < 0f)
            damage = 0f;
    }
    #endregion
}
