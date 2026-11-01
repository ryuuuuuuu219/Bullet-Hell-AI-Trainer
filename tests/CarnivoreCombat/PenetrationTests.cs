using System;

static class PenetrationTests
{
    static void Equal(double actual, double expected)
    {
        if (Math.Abs(actual - expected) > 1e-9) throw new Exception($"Penetration: {actual} != {expected}");
    }
    public static void Run()
    {
        Equal(ProjectilePenetration.RemainingPower(110, 1, 0, 100, 25), 0);
        Equal(ProjectilePenetration.RemainingPower(200, 1, 0.1, 100, 25), 55);
        Equal(ProjectilePenetration.RemainingPower(110, 2, 0, 100, 25), 47.5);
        Equal(ProjectilePenetration.RemainingPower(110, 2, 0.1, 100, 25), 42);
        Equal(ProjectilePenetration.RemainingPower(200, 1, 1, 100, 0), 0);
        var survivor = new AnimalResources(100);
        Equal(survivor.Interception, 0);
        Equal(survivor.Hit(60, out _, 2), 10);
        Equal(survivor.Health, 40); // A surviving target may be penetrated.
        var victim = new AnimalResources(100) { Interception = 0.1 };
        Equal(victim.Hit(200, out _), 80);
        Equal(victim.Health, 0);
        var unchanged = new AnimalResources(100);
        try { unchanged.Hit(20, out _, 0); throw new Exception("Zero penetration accepted"); }
        catch (ArgumentOutOfRangeException) { Equal(unchanged.Health, 100); }
    }
}
