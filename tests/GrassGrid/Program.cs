using System;

class Program
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static void Main()
    {
        TestTargeting();
        TestBirthNeighborRange();
        TestFractionalGrass();
        TestResources();
        var initial = new GrassGrid(100, 100, 150, 42);
        var replay = new GrassGrid(100, 100, 150, 42);
        float count = 0;
        for (int y = 0; y < 100; y++)
        for (int x = 0; x < 100; x++)
        {
            count += initial[x, y];
            Check(initial[x, y] == replay[x, y], "Seed reproducibility");
        }
        Check(count == 150, "150 distinct cells with amount 1");
        foreach (bool wrap in new[] { false, true })
        {
            var grid = new GrassGrid(7, 7, 1, 42);
            int sx = 0, sy = 0;
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
                if (grid[x, y] == 1) { sx = x; sy = y; }
            grid.Step(10, 1, 1, true, wrap);
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
            {
                int dx = Math.Abs(x - sx), dy = Math.Abs(y - sy);
                if (wrap) { dx = Math.Min(dx, 7 - dx); dy = Math.Min(dy, 7 - dy); }
                int expected = dx == 0 && dy == 0 ? 2 : dx <= 1 && dy <= 1 ? 1 : 0;
                Check(grid[x, y] == expected, "Eight neighbors, synchronous birth, boundary");
            }
            for (int i = 0; i < 20; i++) grid.Step(3, 1, 1, true, wrap);
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++) Check(grid[x, y] >= 0 && grid[x, y] <= 3, "Grass stays within cap during death cycles");
        }
        var empty = new GrassGrid(5, 5, 0, 42);
        empty.Step(10, 1, 1, true, false);
        Check(empty[2, 2] == 0, "No spontaneous birth");
        var full = new GrassGrid(3, 3, 9, 42);
        full.Step(10, 3, 1, false, false);
        Check(full[1, 1] == 1, "Disabled growth");
        full.Step(10, 3, 1, true, false);
        Check(full[1, 1] == 4, "Configurable increase");
        var feeding = new GrassGrid(100, 100, 10000, 42);
        feeding.Step(10, 9, 1, true, false);
        int cx, cy;
        Check(feeding.TryWorldToCell(123.4, -56.7, 100, out cx, out cy)
            && cx == 51 && cy == 49, "Continuous XY mapping");
        Check(feeding.ConsumeAtWorldPosition(123.4, -56.7, 100, 3) == 3, "Consume requested amount");
        Check(feeding[51, 49] == 7 && feeding[50, 49] == 10, "Only containing cell changes");
        Check(feeding.ConsumeAtWorldPosition(199.9, -0.1, 100, 99) == 7, "Same cell and clamp to zero");
        Check(feeding.ConsumeAtWorldPosition(123.4, -56.7, 100, 1) == 0, "Repeated consumption of empty cell");
        Check(feeding.ConsumeAtWorldPosition(-5000, -5000, 100, 1) == 1, "Inclusive minimum");
        Check(feeding.ConsumeAtWorldPosition(4999.999, 4999.999, 100, 1) == 1, "Last cell");
        foreach (double outside in new[] { -5000.01, 5000, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Check(feeding.ConsumeAtWorldPosition(outside, 0, 100, 1) == 0, "Invalid X");
            Check(feeding.ConsumeAtWorldPosition(0, outside, 100, 1) == 0, "Invalid Y");
        }
        Check(feeding.TryWorldToCell(-0.01, 0, 100, out cx, out cy) && cx == 49 && cy == 50, "Negative fraction");
        Check(feeding.TryWorldToCell(100, -100, 100, out cx, out cy) && cx == 51 && cy == 49, "Exact cell edges");
        Check(feeding.ConsumeAtWorldPosition(0, 0, 100, -1) == 0
            && feeding.ConsumeAtWorldPosition(0, 0, 100, 0) == 0 && feeding[50, 50] == 10, "Non-positive requests");
        feeding.Step(10, 1, 1, true, false);
        Check(feeding[51, 49] == 0, "Eight occupied neighbors block depleted cell rebirth");
        var lone = new GrassGrid(1, 1, 1, 42);
        lone.ConsumeAtWorldPosition(0, 0, 100, 1);
        lone.Step(10, 1, 1, true, false);
        Check(lone[0, 0] == 0, "Consumption survives buffer swap without neighbors");
        var aging = new GrassGrid(3, 3, 9, 42);
        aging.Step(3, 1, 1, true, false);
        Check(aging[1, 1] == 2, "Growth 1 to 2");
        aging.Step(3, 1, 1, true, false);
        Check(aging[1, 1] == 3, "Growth 2 to 3 persists for one tick");
        aging.ConsumeAtWorldPosition(0, 0, 100, 3);
        aging.Step(3, 1, 1, false, false);
        Check(aging[0, 0] == 0, "Mature grass dies even with growth disabled");
        Check(aging[1, 1] == 0, "Eight mature neighbors block birth before simultaneous death");
        aging.Step(3, 1, 1, true, false);
        Check(aging[0, 0] == 0, "All dead cells remain empty without neighbors");
        var isolated = new GrassGrid(1, 1, 1, 42);
        isolated.Step(3, 99, 1, true, false);
        Check(isolated[0, 0] == 3, "Large growth clamps to cap");
        isolated.Step(3, 1, 1, true, false);
        Check(isolated[0, 0] == 0, "3 to 0 death");
        isolated.Step(3, 1, 1, true, false);
        Check(isolated[0, 0] == 0, "No spontaneous rebirth");
        Console.WriteLine("PASS: simulation, consumption, boundaries, cap=3, death and simultaneous rebirth rules.");
    }

    static void TestResources()
    {
        var animal = new HerbivoreResources();
        Check(animal.Amount == 10, "Initial resources, including newborn");
        animal.Maintain(1);
        animal.Feed(1);
        Check(Math.Abs(animal.Amount - 10.8) < 1e-9, "Maintenance 0.2 and food conversion 1:1");
        animal = new HerbivoreResources();
        animal.Feed(29);
        Check(!animal.TryReproduce() && animal.Amount == 39, "No reproduction below 40");
        animal.Feed(1);
        Check(animal.TryReproduce() && animal.Amount == 10, "Reproduction at 40 costs 30");
        Check(!animal.TryReproduce(), "No repeated reproduction without new resources");
        animal.Feed(100);
        Check(animal.Amount == 50, "Resource cap");
        Check(animal.TryReproduce() && animal.Amount == 20, "Reproduction from cap");
        animal = new HerbivoreResources();
        for (int i = 0; i < 500; i++) animal.Maintain(0.1);
        Check(animal.IsDead && animal.Amount == 0, "Starvation after 50 seconds including roundoff");
        animal.Feed(1);
        Check(animal.IsDead && !animal.TryReproduce(), "Dead animals cannot revive or reproduce");
        var grid = new GrassGrid(1, 1, 1, 42);
        grid.ConsumeAtWorldPosition(0, 0, 100, 0.75f);
        animal = new HerbivoreResources();
        animal.Feed(grid.ConsumeAtWorldPosition(0, 0, 100, 1));
        Check(animal.Amount == 10.25 && grid[0, 0] == 0, "Only actual partial meal becomes resources");
        Console.WriteLine("PASS: resources, maintenance, cap, reproduction cost, newborn resources, starvation and partial meals.");
    }

    static void TestFractionalGrass()
    {
        var grid = new GrassGrid(1, 1, 1, 1);
        float total = 0;
        for (int i = 0; i < 5; i++)
        {
            float eaten = grid.ConsumeAtWorldPosition(0, 0, 100, 0.2f);
            Check(Math.Abs(eaten - 0.2f) < 0.000001f, "Fractional meal");
            total += eaten;
        }
        Check(grid[0, 0] == 0 && Math.Abs(total - 1f) < 0.000001f, "No residual grass after five meals");
        Check(grid.ConsumeAtWorldPosition(0, 0, 100, 0.2f) == 0, "Empty fractional meal");
        grid = new GrassGrid(1, 1, 1, 1);
        grid.ConsumeAtWorldPosition(0, 0, 100, 0.9f);
        float available = grid[0, 0];
        Check(grid.ConsumeAtWorldPosition(0, 0, 100, 0.2f) == available && grid[0, 0] == 0, "Partial meal capped by availability");
        grid = new GrassGrid(1, 1, 1, 1);
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.2f })
            Check(grid.ConsumeAtWorldPosition(0, 0, 100, invalid) == 0 && grid[0, 0] == 1, "Reject invalid consumption");
        grid.ConsumeAtWorldPosition(0, 0, 100, 0.2f);
        grid.Step(3f, 0.5f, 1, true, false);
        Check(Math.Abs(grid[0, 0] - 1.3f) < 0.000001f, "Fractional growth retained");
        var neighbor = new GrassGrid(3, 3, 1, 42);
        int sx = 0, sy = 0;
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 3; x++)
            if (neighbor[x, y] > 0) { sx = x; sy = y; }
        neighbor.ConsumeAtWorldPosition((sx - 1) * 100, (sy - 1) * 100, 100, 0.8f);
        neighbor.Step(3, 1, 1, true, false);
        int nx = sx == 2 ? 1 : sx + 1;
        Check(neighbor[nx, sy] == 1, "Fractional positive amount counts as occupied neighbor");
        Console.WriteLine("PASS: real-valued grass, 0.2 meals, partial meals, rounding, fractional growth and birth.");
    }

    static void TestBirthNeighborRange()
    {
        foreach (bool wrap in new[] { false, true })
        for (int count = 0; count <= 8; count++)
        {
            var grid = new GrassGrid(3, 3, 9, 42);
            grid.Step(3, 2, 1, true, wrap);
            grid.ConsumeAtWorldPosition(0, 0, 100, 3);
            int kept = 0;
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                if (x == 1 && y == 1) continue;
                if (kept++ >= count) grid.ConsumeAtWorldPosition((x - 1) * 100, (y - 1) * 100, 100, 3);
            }
            grid.Step(3, 1, 1, true, wrap);
            Check(grid[1, 1] == (count == 1 ? 1 : 0), "Birth requires exactly one neighbor: " + count);
            // All occupied neighbors were mature: birth uses the pre-death snapshot.
        }
        Console.WriteLine("PASS: birth for exactly 1 occupied neighbor; rejected for 0 and 2..8, including simultaneous death.");
    }

    static GrassGrid Targets(params (int x, int y, int amount)[] cells)
    {
        var grid = new GrassGrid(15, 15, 225, 1);
        grid.Step(10, 9, 1, true, false);
        for (int y = 0; y < 15; y++)
        for (int x = 0; x < 15; x++)
        {
            int amount = 0;
            foreach (var cell in cells) if (cell.x == x && cell.y == y) amount = cell.amount;
            grid.ConsumeAtWorldPosition((x - 7) * 100, (y - 7) * 100, 100, 10 - amount);
        }
        return grid;
    }

    static void TestTargeting()
    {
        GrassTargeting.Candidate target;
        foreach (var sample in new[] { (x: 8, y: 7, score: 1.0), (x: 7, y: 8, score: 0.6), (x: 6, y: 7, score: 0.2) })
        {
            Check(GrassTargeting.TrySelect(Targets((sample.x, sample.y, 10)), 0, 0, 2, 0, 100, out target), "All directions eligible");
            Check(Math.Abs(target.Score - sample.score) < 1e-9, "Linear directional weighting and normalization");
        }
        Check(GrassTargeting.TrySelect(Targets((8, 7, 1), (6, 7, 10)), 0, 0, 1, 0, 100, out target)
            && target.CellX == 6, "More grass behind can beat less grass ahead");
        Check(GrassTargeting.TrySelect(Targets((12, 12, 1), (13, 7, 10)), 0, 0, 1, 0, 100, out target)
            && target.CellX == 12 && target.CellY == 12, "Square corners included; sixth cell excluded");
        Check(!GrassTargeting.TrySelect(Targets((7, 7, 1), (8, 7, 10)), 0, 0, 1, 0, 100, out target), "Grass here stops search");
        Check(!GrassTargeting.TrySelect(Targets(), 0, 0, 1, 0, 100, out target), "Empty search has no target");
        Check(GrassTargeting.TrySelect(Targets((8, 7, 6), (7, 8, 10)), 0, 0, 1, 0, 100, out target)
            && target.CellX == 8, "Equal scores prefer alignment");
        Check(GrassTargeting.TrySelect(Targets((8, 7, 5), (9, 7, 5)), 0, 0, 1, 0, 100, out target)
            && target.CellX == 8, "Equal alignment prefers nearer cell");
        Check(GrassTargeting.TrySelect(Targets((0, 0, 1)), -601.2, -650.4, 0, 0, 100, out target)
            && target.CellX == 0, "Continuous coordinates, edge clipping and zero heading fallback");
        Console.WriteLine("PASS: square targeting, score, stopping, no food, tie breaks and edge search.");
    }
}
