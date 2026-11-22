using System;

// Section penetration uses AfterArmor; RemainingPower is the legacy non-ship adapter.
public static class ProjectilePenetration
{
    // Version 2: armor consumes penetration, never projectile power. Equality fails.
    public static double AfterArmor(double power, double penetration, double effectiveArmor)
    {
        if (!IsValid(penetration) || power <= 0 || double.IsNaN(power) ||
            double.IsInfinity(power) || double.IsNaN(effectiveArmor) || effectiveArmor < 0)
            return 0;
        double effectivePenetration = power * penetration;
        return effectivePenetration <= effectiveArmor ? 0
            : Math.Max(0, penetration * (1 - effectiveArmor / effectivePenetration));
    }

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
