using System;

public static class AnimalTactics
{
    public static double Radius(double health) => health > 0 ? Math.Max(5, (Math.Log(health) + 0.1) * 20) : 5;

    public static bool Abandon(double selfHealth, double targetHealth, double selfResources, double targetResources)
        => selfHealth + 10 < targetHealth || selfResources + 10 < targetResources || 20 < targetResources;
}

/// <summary>Uses running, awake seconds only. Samples once per complete two-second interval.</summary>
public sealed class EvasionClock
{
    public double Elapsed { get; private set; }
    public int Step(double seconds, bool running, Func<double> sample)
    {
        if (!running || seconds <= 0) return 0;
        Elapsed += seconds;
        int turns = 0;
        while (Elapsed >= 2)
        {
            Elapsed -= 2;
            if (sample() < 0.1) turns++;
        }
        return turns;
    }
}
