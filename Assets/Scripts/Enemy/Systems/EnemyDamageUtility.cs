using Unity.Mathematics;

/// <summary>
/// Provides shared enemy damage helpers used by combat and elemental systems.
/// </summary>
public static class EnemyDamageUtility
{
    #region Methods

    #region Public Methods
    public static void ApplyFlatShieldDamage(ref EnemyHealth enemyHealth, float incomingDamage)
    {
        ConsumeFlatShieldDamage(ref enemyHealth, incomingDamage);
    }

    public static float ConsumeFlatShieldDamage(ref EnemyHealth enemyHealth, float incomingDamage)
    {
        float remainingDamage = math.max(0f, incomingDamage);

        if (remainingDamage <= 0f)
            return 0f;

        if (enemyHealth.CurrentShield > 0f)
        {
            float absorbedDamage = math.min(enemyHealth.CurrentShield, remainingDamage);
            enemyHealth.CurrentShield -= absorbedDamage;
            remainingDamage -= absorbedDamage;

            if (enemyHealth.CurrentShield < 0f)
                enemyHealth.CurrentShield = 0f;
        }

        if (remainingDamage <= 0f)
            return 0f;

        float appliedHealthDamage = math.min(enemyHealth.Current, remainingDamage);
        enemyHealth.Current -= appliedHealthDamage;
        remainingDamage -= appliedHealthDamage;

        if (enemyHealth.Current < 0f)
            enemyHealth.Current = 0f;

        return math.max(0f, remainingDamage);
    }
    #endregion

    #endregion
}
