using System;

// Damage and remaining projectile power both use the pre-impact state.
public static class ProjectilePenetration
{

    public static bool IsValid(double penetration)
        => penetration > 0 && !double.IsNaN(penetration) && !double.IsInfinity(penetration);
    public static bool IsValid(double penetration, double interception)
        => penetration > 0 && !double.IsNaN(penetration) && !double.IsInfinity(penetration) &&
        interception >= 0 && !double.IsNaN(interception) && !double.IsInfinity(interception);


    public static double RemainingPower(double power, double penetration, double interception,
        double Health, out double afterHealth)
    {
        if (!IsValid(penetration,interception))
            throw new ArgumentOutOfRangeException(nameof(penetration), "貫通力は0より大きい有限値が必要です。");
        double togetherDamage = Math.Max(0,(power * (penetration - interception)));
        double ratio = interception / penetration;
        afterHealth = Math.Max(0, Health - togetherDamage);
        return Math.Max(0, power - togetherDamage*ratio);
    }
}
