using System;
using System.Collections.Generic;
using System.Linq;

static class Program
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Main()
    {
        var grid = new SpatialGrid<int>(200);
        var candidates = new List<int>();
        grid.Update(1, -0.01, -200);
        grid.Update(2, 200, 0);
        grid.Query(-200, -200, 200, 0, candidates);
        Check(candidates.Count == 2, "negative and inclusive cell boundaries");
        grid.Update(1, 1000, 1000);
        grid.Query(-200, -200, 200, 0, candidates);
        Check(candidates.SequenceEqual(new[] { 2 }), "movement removes old cell");
        grid.Update(1, 1000, 1000);
        grid.Query(1000, 1000, 1000, 1000, candidates);
        Check(candidates.SequenceEqual(new[] { 1 }), "same-cell update has no duplicate");
        grid.Remove(1);
        grid.Update(1, 0, 0);
        grid.Remove(2);
        grid.Query(-2000, -2000, 2000, 2000, candidates);
        Check(candidates.SequenceEqual(new[] { 1 }), "remove and reenable");
        grid.Clear();
        grid.Query(-2000, -2000, 2000, 2000, candidates);
        Check(candidates.Count == 0, "reset clears results and registry");

        var random = new Random(701);
        var x = new double[400];
        var y = new double[400];
        var radius = new double[400];
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = random.NextDouble() * 10000 - 5000;
            y[i] = random.NextDouble() * 10000 - 5000;
            radius[i] = 5 + random.NextDouble() * 95;
            grid.Update(i, x[i], y[i]);
        }
        long checkedCandidates = 0;
        for (int trial = 0; trial < 1000; trial++)
        {
            int moved = trial % x.Length;
            x[moved] = random.NextDouble() * 10000 - 5000;
            y[moved] = random.NextDouble() * 10000 - 5000;
            grid.Update(moved, x[moved], y[moved]);
            double sx = x[trial % x.Length], sy = y[trial % x.Length];
            // Includes stationary bullets and long diagonal sweeps crossing many cells.
            double ex = sx + (trial % 10 == 0 ? 0 : random.NextDouble() * 3000 - 1500);
            double ey = sy + (trial % 10 == 0 ? 0 : random.NextDouble() * 3000 - 1500);
            grid.Query(Math.Min(sx, ex) - 105, Math.Min(sy, ey) - 105,
                Math.Max(sx, ex) + 105, Math.Max(sy, ey) + 105, candidates);
            var expected = Hits(Enumerable.Range(0, x.Length), sx, sy, ex, ey, x, y, radius);
            var actual = Hits(candidates, sx, sy, ex, ey, x, y, radius);
            Check(expected.SequenceEqual(actual), "swept hits and ordering match full scan");

            grid.Query(sx - 200, sy - 200, sx + 200, sy + 200, candidates);
            candidates.Sort();
            checkedCandidates += candidates.Count;
            var fullForce = Force(Enumerable.Range(0, x.Length), trial % x.Length, sx, sy, x, y);
            var gridForce = Force(candidates, trial % x.Length, sx, sy, x, y);
            Check(fullForce == gridForce, "repulsion matches full scan including overlap");
        }
        // A body center outside the swept segment's cells must still be found by its radius.
        grid.Clear();
        grid.Update(0, 210, 0);
        grid.Query(-105, -105, 295, 105, candidates);
        Check(candidates.Contains(0) && ProjectileSweep.TryHit(0, 0, 190, 0, 210, 0, 100, out _),
            "large body across cell boundary");
        Console.WriteLine($"PASS: lifecycle/boundaries and 1000 full-scan collision/repulsion comparisons; average nearby candidates {checkedCandidates / 1000.0:F2}/400.");
    }

    static int[] Hits(IEnumerable<int> ids, double sx, double sy, double ex, double ey,
        double[] x, double[] y, double[] radius)
    {
        var hits = new List<(int id, double fraction)>();
        foreach (int i in ids)
            if (ProjectileSweep.TryHit(sx, sy, ex, ey, x[i], y[i], radius[i] + 5, out double t))
                hits.Add((i, t));
        return hits.OrderBy(hit => hit.fraction).ThenBy(hit => hit.id).Select(hit => hit.id).ToArray();
    }

    static (double, double) Force(IEnumerable<int> ids, int self, double sx, double sy, double[] x, double[] y)
    {
        double fx = 0, fy = 0;
        foreach (int i in ids)
        {
            if (i == self) continue;
            CarnivoreSpacing.Repulsion(sx - x[i], sy - y[i], self, i, 200, 50, out double rx, out double ry);
            fx += rx; fy += ry;
        }
        CarnivoreSpacing.Limit(ref fx, ref fy, 50);
        return (fx, fy);
    }
}
