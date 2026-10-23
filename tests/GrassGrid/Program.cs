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
        var initial = new GrassGrid(100, 100, 150, 42);
        var replay = new GrassGrid(100, 100, 150, 42);
        int count = 0;
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
            for (int i = 0; i < 20; i++) grid.Step(10, 1, 1, true, wrap);
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++) Check(grid[x, y] == 10, "Saturation");
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
        Check(feeding[51, 49] == 1, "Depleted cell rebirth on next tick");
        var lone = new GrassGrid(1, 1, 1, 42);
        lone.ConsumeAtWorldPosition(0, 0, 100, 1);
        lone.Step(10, 1, 1, true, false);
        Check(lone[0, 0] == 0, "Consumption survives buffer swap without neighbors");
        Console.WriteLine("PASS: simulation rules, continuous world coordinates, consumption, boundaries, invalid inputs and post-consumption updates.");
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
