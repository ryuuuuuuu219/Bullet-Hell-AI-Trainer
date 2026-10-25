using System;

static class Program
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static void Equal(double actual, double expected, string message)
        => Check(Math.Abs(actual - expected) < 1e-8, message + $": {actual} != {expected}");

    static void Main()
    {
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
    }
}
