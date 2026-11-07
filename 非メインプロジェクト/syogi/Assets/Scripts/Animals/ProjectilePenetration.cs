using System;

// Damage and remaining projectile power both use the pre-impact state.
public static class ProjectilePenetration
{
    public static bool IsValid(double penetration)
        => penetration > 0 && !double.IsNaN(penetration) && !double.IsInfinity(penetration);

    public static double RemainingPower(double power, double penetration, double interception,
        double previousHealth, double armor)
    {
        if (!IsValid(penetration))
            throw new ArgumentOutOfRangeException(nameof(penetration), "貫通力は0より大きい有限値が必要です。");
        return Math.Max(0, (power * (penetration - interception) - (previousHealth + armor)) / penetration);
    }
}
