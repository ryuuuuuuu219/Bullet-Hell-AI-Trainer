using System;

static class TacticsTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Equal(double a, double b, string message) => Check(Math.Abs(a - b) < 1e-7, message + $" {a} != {b}");
    public static void Run()
    {
        Equal(AnimalTactics.Radius(0), 5, "zero HP avoids log zero");
        Equal(AnimalTactics.Radius(0.1), 5, "low HP clamps");
        Equal(AnimalTactics.Radius(1), 5, "HP one clamps");
        Equal(AnimalTactics.Radius(30), 70.0239476332, "herbivore radius");
        Equal(AnimalTactics.Radius(50), 80.2404601086, "carnivore radius");
        var body = new AnimalResources(50);
        double before = AnimalTactics.Radius(body.Health);
        body.Hit(40, out _);
        double injured = AnimalTactics.Radius(body.Health);
        Check(injured < before, "damage shrinks radius");
        body.Feed(20);
        Check(AnimalTactics.Radius(body.Health) > injured, "healing grows radius");
        Check(ProjectileSweep.TryHit(0, 70, 200, 70, 100, 0, before + 5, out _), "full health hit");
        Check(!ProjectileSweep.TryHit(0, 70, 200, 70, 100, 0, injured + 5, out _), "injured small target misses same bullet");

        Check(!AnimalTactics.Abandon(30, 40, 10, 20), "equal thresholds permit pursuit");
        Check(AnimalTactics.Abandon(30, 40.01, 10, 10), "HP condition alone");
        Check(AnimalTactics.Abandon(50, 30, 0, 10.01), "resource gap alone");
        Check(AnimalTactics.Abandon(100, 30, 50, 20.01), "prey resource cap alone");
        Check(!AnimalTactics.Abandon(100, 30, 50, 20), "twenty is allowed");

        var clock = new EvasionClock();
        int samples = 0;
        Func<double> success = () => { samples++; return 0.099; };
        Equal(clock.Step(1.5, true, success), 0, "not yet due");
        Equal(clock.Step(100, false, success), 0, "non-running time frozen");
        Equal(clock.Elapsed, 1.5, "timer retained");
        Equal(clock.Step(0.5, true, success), 1, "two second trigger");
        Equal(samples, 1, "one draw");
        Equal(clock.Step(6, true, success), 3, "long step draws each interval");
        Equal(clock.Step(2, true, () => 0.1), 0, "ten percent cutoff exclusive");
        Equal(clock.Step(2, true, () => 0), 1, "zero sample succeeds");

        var grid = new double[4,4];
        for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) grid[x,y] = 1;
        Func<int,int,double> amount = (x,y) => grid[x,y];
        Equal(GrassSightLine.Sum(4,4,0.5,0.5,3.5,0.5,amount),4,"horizontal includes endpoints once");
        Equal(GrassSightLine.Sum(4,4,3.5,0.5,0.5,0.5,amount),4,"reverse horizontal");
        Equal(GrassSightLine.Sum(4,4,0.5,0.5,0.5,3.5,amount),4,"vertical");
        Equal(GrassSightLine.Sum(4,4,0.5,0.5,3.5,3.5,amount),4,"diagonal skips corner-only contacts");
        Equal(GrassSightLine.Sum(4,4,3.5,3.5,0.5,0.5,amount),4,"reverse diagonal");
        Equal(GrassSightLine.Sum(4,4,1,1,1,1,amount),1,"coincident center counted once");
        Equal(GrassSightLine.Sum(4,4,0,0,3,3,amount),4,"exact corner endpoints");
        Equal(GrassSightLine.Sum(4,4,3,3,0,0,amount),4,"reverse exact corners");
        grid[0,0]=0.25; grid[1,0]=-3; grid[2,0]=0; grid[3,0]=2.5;
        Equal(GrassSightLine.Sum(4,4,0.5,0.5,3.5,0.5,amount),2.75,"sum amounts not cells; ignore dead grass");
        Equal(GrassSightLine.Sum(4,4,4,0,0,0,amount),0,"outside field");
        var traits = new AnimalTraits { grassOcclusionPerUnit = 0.5f };
        Equal(traits.DetectionRadius(1,0,0,2),500,"intervening grass halves detection");
        Equal(traits.DetectionRadius(1,1,0.5,2),250,"local hiding and intervening grass combine");
        Check(600 > traits.DetectionRadius(1,0,0,2),"previous 600-distance target becomes lost");
        Equal(traits.DetectionRadius(1,0,0,0),1000,"no grass unchanged");
        Console.WriteLine("PASS HP radius, OR abandonment, evasion cadence and grass sightline totals");
    }
}
