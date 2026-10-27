using System;

static class PredatorAbilityTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Equal(double a, double b, string message) => Check(Math.Abs(a - b) < 1e-6, message + $": {a} != {b}");
    public static void Run()
    {
        for (int i = 0; i < 5; i++)
            Equal(PredatorAbilities.ShotAngle(i, 5, 8, false), -16 + i * 8, "snake fan");
        for (int i = 0; i < 16; i++)
            Equal(PredatorAbilities.ShotAngle(i, 16, 8, true), i * 22.5, "beetle circle");
        Equal(PredatorAbilities.ShotAngle(0, 1, 8, false), 0, "single shot stays forward");
        Equal(PredatorAbilities.NavigationTurn(100, 0, -200, 0, 200, 2.8, 0, 30, 0.02), 0, "head-on requires no turn");
        double positive = PredatorAbilities.NavigationTurn(100, 0, -200, 100, 200, 2.8, 0, 30, 0.02);
        Equal(positive, 0.6, "PN clamped to 30 deg/s");
        Equal(PredatorAbilities.NavigationTurn(100, 0, -200, -100, 200, 2.8, 0, 30, 0.02), -0.6, "mirrored PN turn");
        Equal(PredatorAbilities.NavigationTurn(100, 0, -100, 1, 200, 2.8, 0, 30, 0.02),
            2.8 * 0.5 * 0.01 * 180 / Math.PI * 0.02, "navigation constant scales lateral correction");
        Equal(PredatorAbilities.NavigationTurn(100, 0, 200, 0, 200, 2.8, 90, 30, 0.02), 0.6, "opening target reacquisition");
        Check(PredatorAbilities.CanReflect(true, 100, 50), "threshold includes equality");
        Check(!PredatorAbilities.CanReflect(true, 100, 50.01), "over threshold penetrates");
        Check(!PredatorAbilities.CanReflect(true, 20, 15), "threshold uses current HP");
        Check(!PredatorAbilities.CanReflect(false, 500, 1), "disabled reflection");
        Check(!PredatorAbilities.CanReflect(true, 100, 0), "expired bullet cannot reflect");
        var ally = new AnimalResources(100);
        ally.Hit(10, "beetle", "beetle", out _);
        Equal(ally.Health, 99, "ordinary ally reduction");
        var neutralVictim = new AnimalResources(100);
        Equal(neutralVictim.Hit(10, null, "beetle", out _), 0, "neutral bullet still consumes power on impact");
        Equal(neutralVictim.Health, 90, "neutral bullet has full damage");
        var night = new AnimalTraits { activeLightMinimum = 0, activeLightMaximum = 0.3f, includeActiveLightMinimum = true };
        Check(night.IsActive(0) && night.IsActive(0.3f) && !night.IsActive(0.301), "snake activity includes both boundaries");
        Console.WriteLine("PASS predator navigation, firing angles, current-HP reflection, neutral damage and nocturnal boundaries");
    }
}
