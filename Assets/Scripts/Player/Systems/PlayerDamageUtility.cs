using Unity.Mathematics;

/// <summary>
/// Provides shared player damage helpers used by enemy-hit and elemental systems.
/// </summary>
public static class PlayerDamageUtility
{
    #region Methods

    #region Public Methods
    /// <summary>
    /// Applies incoming flat damage to player shield first and then to health.
    /// </summary>
    /// <param name="playerHealth">Mutable player health state that receives remaining damage after shield absorption.</param>
    /// <param name="playerShield">Mutable player shield state that absorbs damage before health.</param>
    /// <param name="incomingDamage">Raw incoming damage value. Negative values are treated as zero.</param>
    public static void ApplyFlatShieldDamage(ref PlayerHealth playerHealth, ref PlayerShield playerShield, float incomingDamage)
    {
        float remainingDamage = math.max(0f, incomingDamage);

        if (remainingDamage <= 0f)
            return;

        if (playerShield.Current > 0f)
        {
            float absorbedDamage = math.min(playerShield.Current, remainingDamage);
            playerShield.Current -= absorbedDamage;
            remainingDamage -= absorbedDamage;

            if (playerShield.Current < 0f)
                playerShield.Current = 0f;
        }

        if (remainingDamage <= 0f)
            return;

        playerHealth.Current -= remainingDamage;

        if (playerHealth.Current < 0f)
            playerHealth.Current = 0f;
    }
    #endregion

    #endregion
}
