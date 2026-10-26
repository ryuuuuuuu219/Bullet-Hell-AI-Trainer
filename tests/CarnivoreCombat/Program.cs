using System;

static class Program
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static void Equal(double actual, double expected, string message)
        => Check(Math.Abs(actual - expected) < 1e-8, message + $": {actual} != {expected}");

    static void TestActivityAndReproduction()
    {
        var traits = AnimalTraits.HerbivoreDefaults();
        traits.activeLightMinimum = 0.25f;
        traits.activeLightMaximum = 0.75f;
        var resources = new AnimalResources(30);
        resources.Feed(40);
        var activity = new AnimalActivity();
        activity.Step(10, 0.5, false, traits, resources);
        Equal(activity.ActiveAge, 10, "awake time advances maturity");
        Equal(resources.Amount, 48, "awake maintenance");
        activity.Step(100, 0, true, traits, resources);
        Equal(activity.ActiveAge, 10, "sleep freezes maturity");
        Equal(resources.Amount, 48, "sleep has no maintenance or running cost");
        Check(activity.IsSleeping && !activity.IsRunning, "threat cannot wake sleeper");
        activity.Step(1, 0.25, true, traits, resources);
        Check(!activity.IsSleeping && activity.IsRunning, "inclusive activity boundary and immediate escape");
        Equal(resources.Amount, 47.7, "running maintenance plus extra cost");
        activity.Step(100, 1, false, traits, resources);
        Equal(activity.EscapeRemaining, 2, "sleep freezes escape timer");
        activity.Step(1, 0.75, false, traits, resources);
        Check(activity.IsRunning, "escape persists after threat lost");
        activity.Step(2, 0.5, false, traits, resources);
        Check(!activity.IsRunning, "escape expires after hold");
        Equal(resources.Amount, 46.9, "only remaining escape time charged across long step");
        Equal(activity.ActiveAge, 14, "only awake duration counted");
        Check(!resources.TryReproduce(traits, activity.ActiveAge), "immature cannot reproduce");
        traits.offspringCount = 3;
        traits.offspringResources = 5;
        traits.reproductionCostRatio = 2;
        Check(resources.TryReproduce(traits, 30), "maturity boundary permits litter");
        Equal(resources.Amount, 16.9, "whole litter cost charged once");
        Check(!resources.TryReproduce(traits, 31), "threshold prevents second litter");
        resources.Feed(100);
        traits.offspringCount = 6;
        Check(!resources.TryReproduce(traits, 100), "cannot afford litter despite threshold");
        Equal(resources.Amount, 50, "failed litter has no cost");
        traits.offspringResources = double.NaN;
        Check(!resources.TryReproduce(traits, 100), "reject invalid birth settings");
        var child = new AnimalResources(30);
        child.SetBirthResources(7);
        Equal(child.Amount, 7, "configurable newborn allocation");
        Equal(child.Health, 30, "allocation does not heal newborn");
        Equal(traits.SearchRadius(0), 250, "night sensing");
        Equal(traits.SearchRadius(1), 500, "day sensing");
        Equal(traits.DetectionRadius(1, 1, 0.5), 250, "full grass conceals target");
        Equal(traits.DetectionRadius(0, 1, 0.5), 125, "night and concealment combine");
        Equal(traits.DetectionRadius(1, -1, 0.5), 500, "dead grass cannot increase detection");
        Equal(traits.DetectionRadius(1, 2, 2), 0, "concealment clamps at zero distance");
        traits.escapeHoldSeconds = 0;
        activity.Step(0.1, 0.5, true, traits, resources);
        Check(activity.IsRunning, "zero hold still runs while threat present");
        Console.WriteLine("PASS activity, dormancy, escape cost, maturity and configurable litters");
    }

    static void Main()
    {
        var accelerationTraits = new AnimalTraits();
        Equal(AccelerationSkill.Speed(accelerationTraits, false), 105, "skill off preserves walking speed");
        Equal(AccelerationSkill.Speed(accelerationTraits, true), 150, "skill on uses running speed");
        accelerationTraits.runningSpeed = 80;
        Equal(AccelerationSkill.Speed(accelerationTraits, true), 105, "acceleration cannot slow down walking");
        var accelerated = new AnimalResources(50);
        accelerated.Maintain(2);
        AccelerationSkill.Consume(accelerationTraits, accelerated, 2);
        Equal(accelerated.Amount, 9.4, "acceleration cost adds to maintenance exactly once");
        AccelerationSkill.Consume(accelerationTraits, accelerated, 100);
        Check(accelerated.IsDead, "acceleration can exhaust resources");
        TacticsTests.Run();
        TestActivityAndReproduction();
        var herbivore = new HerbivoreResources();
        Equal(herbivore.Hit(10, out double stolen), 0, "first bullet consumed");
        Equal(stolen, 0, "no food before kill");
        Equal(herbivore.Health, 20, "first hit");
        herbivore.Hit(10, out stolen);
        Check(!herbivore.IsDead, "alive after two hits");
        Equal(herbivore.Hit(10, out stolen), 0, "equal HP and power both reach zero");
        Check(herbivore.IsDead, "three hits kill herbivore");
        Equal(stolen, 10, "all remaining food transferred");
        Equal(herbivore.Amount, 0, "victim food drained");
        herbivore.Hit(10, out stolen);
        Equal(stolen, 0, "dead victim cannot pay twice");
        herbivore.Feed(100);
        Check(herbivore.IsDead && !herbivore.TryReproduce(), "no resurrection or reproduction");
        Console.WriteLine("PASS herbivore damage, kill and single resource transfer");

        var predator = new AnimalResources(50);
        for (int i = 0; i < 4; i++) predator.Hit(10, out _);
        Check(!predator.IsDead, "carnivore survives four hits");
        predator.Hit(10, out stolen);
        Check(predator.IsDead && stolen == 10, "fifth hit kills carnivore");

        var prey = new HerbivoreResources();
        prey.Maintain(5);
        prey.Hit(27, out _);
        double power = prey.Hit(10, out stolen);
        Equal(power, 7, "piercing uses previous HP of three");
        Equal(stolen, 9, "transfer current, not initial food");
        var next = new AnimalResources(50);
        Equal(next.Hit(power, out _), 0, "remaining bullet absorbed by next target");
        Equal(next.Health, 43, "remaining power damages next target");
        Console.WriteLine("PASS carnivore combat and piercing arithmetic");

        predator = new AnimalResources(50);
        predator.Feed(35); // food45, HP85
        predator.Hit(20, out _); // HP65
        predator.Feed(20); // food clamp50, HP85 (not just +5)
        Equal(predator.Amount, 50, "resource clamp");
        Equal(predator.Health, 85, "heal full transferred amount independently of resource cap");
        Check(predator.TryReproduce(), "reproduce after capped transfer");
        Equal(predator.Amount, 40, "reproduction cost10");
        Equal(predator.Health, 85, "no HP cost for birth");
        Check(predator.TryReproduce(), "second birth allowed at threshold40");
        Equal(predator.Amount, 30, "second birth costs10");
        Check(!predator.TryReproduce(), "no second birth below threshold");
        predator.Feed(100);
        Equal(predator.Health, 100, "HP cap100");
        var child = new AnimalResources(50);
        Equal(child.Health, 50, "child initial HP");
        Equal(child.Amount, 10, "child initial resources");
        child.Maintain(50);
        Check(child.IsDead, "starvation after fifty seconds");
        Equal(child.Hit(10, out stolen), 10, "starved target does not absorb bullet");
        Equal(stolen, 0, "starved target yields no food");
        herbivore = new HerbivoreResources();
        herbivore.Hit(25, out _);
        herbivore.Feed(1);
        Equal(herbivore.Health, 6, "grass feeding heals actual consumption");
        Console.WriteLine("PASS healing, caps, reproduction, starvation and grass recovery");

        Check(ProjectileSweep.TryHit(0, 0, 200, 0, 150, 0, 85, out double t), "sweep prevents tunnelling");
        Equal(t, 0.325, "first contact distance65");
        Check(ProjectileSweep.TryHit(0, 0, 200, 0, 150, 85, 85, out _), "tangent hit");
        Check(!ProjectileSweep.TryHit(0, 0, 200, 0, 150, 86, 85, out _), "near miss");
        Check(!ProjectileSweep.TryHit(0, 0, 200, 0, 286, 0, 85, out _), "no hit beyond range");
        Check(!ProjectileSweep.TryHit(0, 0, 200, 0, -100, 0, 85, out _), "no hit behind bullet");
        Check(ProjectileSweep.TryHit(0, 0, 0, 0, 30, 0, 85, out t) && t == 0, "initial overlap");
        Check(!ProjectileSweep.TryHit(0, 0, 0, 0, 100, 0, 85, out _), "stationary non-overlap");
        Console.WriteLine("PASS swept contacts, overlaps, misses and range boundary");

        var ally = new AnimalResources(50);
        Equal(ally.Hit(15, 2, 2, out stolen), 0, "healthy ally still absorbs bullet");
        Equal(ally.Health, 48.5, "same species receives one tenth damage");
        Equal(stolen, 0, "surviving ally yields no resources");
        var otherSpecies = new AnimalResources(50);
        otherSpecies.Hit(15, 2, 3, out _);
        Equal(otherSpecies.Health, 35, "different species takes full damage");
        ally = new AnimalResources(50);
        ally.Hit(49, out _);
        power = ally.Hit(15, 2, 2, out stolen);
        Check(ally.IsDead, "reduced damage can still kill weak ally");
        Equal(stolen, 10, "same species kill retains resource transfer");
        Equal(power, 14, "piercing power is not scaled by friendly damage reduction");
        prey = new HerbivoreResources();
        prey.Hit(power, 2, 1, out _);
        Equal(prey.Health, 16, "subsequent prey takes full remaining power");
        ally = new AnimalResources(50);
        ally.Hit(45, out _);
        Equal(ally.Hit(15, 2, 2, out _), 10, "existing HP-based power consumption retained");
        Equal(ally.Health, 3.5, "surviving weak ally can be pierced");
        Console.WriteLine("PASS species damage reduction, piercing and kill transfer");

        foreach (bool left in new[] { false, true })
        foreach (double sample in new[] { 0.0, 0.5, 1.0 })
        {
            Check(CarnivoreSpacing.TryBirthPosition(10, 20, 0.6, 0.8, 80, sample, left, 5000,
                out double bx, out double by), "lateral birth succeeds");
            double dx = bx - 10, dy = by - 20;
            Equal(dx * 0.6 + dy * 0.8, 0, "birth perpendicular to parent heading");
            Equal(Math.Sqrt(dx * dx + dy * dy), 80 * (1.5 + sample), "birth distance 120 to 200");
        }
        Check(CarnivoreSpacing.TryBirthPosition(0, 4990, 1, 0, 80, 1, true, 5000,
            out double edgeX, out double edgeY) && edgeY == 4790, "outside candidate uses opposite side");
        Check(!CarnivoreSpacing.TryBirthPosition(4999, 4999, 1, 1, 80, 1, true, 5000,
            out _, out _), "corner can reject both sides before food charge");
        CarnivoreSpacing.Repulsion(100, 0, 1, 2, 200, 50, out double rx, out double ry);
        Equal(rx, 25, "linear falloff at half radius"); Equal(ry, 0, "push away");
        CarnivoreSpacing.Repulsion(200, 0, 1, 2, 200, 50, out rx, out ry);
        Equal(rx, 0, "no force at radius");
        CarnivoreSpacing.Repulsion(0, 0, 1, 2, 200, 50, out rx, out ry);
        CarnivoreSpacing.Repulsion(0, 0, 2, 1, 200, 50, out double sx, out double sy);
        Equal(rx + sx, 0, "overlap pair opposite x"); Equal(ry + sy, 0, "overlap pair opposite y");
        Equal(Math.Sqrt(rx * rx + ry * ry), 50, "overlap has finite nonzero push");
        rx = 300; ry = 400;
        CarnivoreSpacing.Limit(ref rx, ref ry, 50);
        Equal(rx, 30, "summed force capped x"); Equal(ry, 40, "summed force capped y");
        CarnivoreSpacing.Limit(ref rx, ref ry, 0);
        Equal(rx, 0, "zero cap disables separation");
        Console.WriteLine("PASS lateral birth, bounds, separation overlap and speed cap");
    }
}
