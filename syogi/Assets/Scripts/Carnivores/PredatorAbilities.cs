using System;

public static class PredatorAbilities
{
    public static double ShotAngle(int index, int count, double spacingDegrees, bool fullCircle)
        => fullCircle ? index * 360.0 / Math.Max(1, count) : (index - (count - 1) * 0.5) * spacingDegrees;

    // Pure proportional navigation: lateral acceleration = N * closing speed * LOS rate.
    // Reacquire by direct pursuit if the target is not closing or the hunter is stopped.
    public static double NavigationTurn(double rx, double ry, double relativeVx, double relativeVy,
        double speed, double navigationConstant, double directTurnDegrees, double maxDegreesPerSecond, double seconds)
    {
        double squared = rx * rx + ry * ry;
        double desired = directTurnDegrees;
        if (squared > 1e-8 && speed > 1e-8)
        {
            double closing = -(rx * relativeVx + ry * relativeVy) / Math.Sqrt(squared);
            if (closing > 0)
                desired = navigationConstant * closing / speed *
                    (rx * relativeVy - ry * relativeVx) / squared * (180 / Math.PI) * seconds;
        }
        double limit = Math.Max(0, maxDegreesPerSecond) * Math.Max(0, seconds);
        return Math.Max(-limit, Math.Min(limit, desired));
    }

    public static bool CanReflect(bool enabled, double currentHealth, double power)
        => enabled && currentHealth > 0 && power > 0 && power <= currentHealth * 0.5;
}
