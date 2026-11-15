using System;

// Damage and remaining projectile power use the pre-impact penetration.
public static class ProjectilePenetration
{

    public static bool IsValid(double penetration)
        => penetration > 0 && !double.IsNaN(penetration) && !double.IsInfinity(penetration);
    public static bool IsValid(double penetration, double interception)
        => penetration > 0 && !double.IsNaN(penetration) && !double.IsInfinity(penetration) &&
        interception >= 0 && !double.IsNaN(interception) && !double.IsInfinity(interception);


    public static double RemainingPower(double power, double penetration, double interception,
        double Health, out double afterHealth)
        => RemainingPower(power, penetration, interception, Health, out afterHealth, out _);

    public static double RemainingPower(double power, double penetration, double interception,
        double Health, out double afterHealth, out double afterPenetration)
    {
        if (!IsValid(penetration,interception))
            throw new ArgumentOutOfRangeException(nameof(penetration), "貫通力は0より大きい有限値が必要です。");
        double ratio = interception / penetration;
        afterHealth = Math.Max(0, Health - power);
        afterPenetration = Math.Max(0d, penetration - interception);
        if (afterPenetration <= 0d) return 0d;
        return Math.Max(0, power *(1-ratio));
    }
}
