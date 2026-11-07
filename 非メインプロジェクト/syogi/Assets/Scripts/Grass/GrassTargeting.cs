using System;

// Visits each cell once: no parallel arrays, sorting or duplicate samples.
public static class GrassTargeting
{
    public struct Candidate
    {
        public int CellX, CellY;
        public double X, Y, Weight, Dot, Score, DistanceSquared;
    }

    public static bool TrySelect(GrassGrid grid, double x, double y,
        double directionX, double directionY, double cellSize, out Candidate best, double searchRadius = double.PositiveInfinity)
    {
        best = default(Candidate);
        int cx, cy;
        if (!grid.TryWorldToCell(x, y, cellSize, out cx, out cy) || grid[cx, cy] > 0) return false;
        double length = Math.Sqrt(directionX * directionX + directionY * directionY);
        if (length <= 0) { directionX = 1; directionY = 0; }
        else { directionX /= length; directionY /= length; }
        bool found = false;
        int cells = double.IsPositiveInfinity(searchRadius) ? 5 :
            (int)Math.Min(Math.Max(grid.Width, grid.Height), Math.Ceiling(Math.Max(0, searchRadius) / cellSize));
        for (int iy = Math.Max(0, cy - cells); iy <= Math.Min(grid.Height - 1, cy + cells); iy++)
        for (int ix = Math.Max(0, cx - cells); ix <= Math.Min(grid.Width - 1, cx + cells); ix++)
        {
            if (grid[ix, iy] <= 0) continue;
            double tx = (ix + 0.5 - grid.Width / 2.0) * cellSize;
            double ty = (iy + 0.5 - grid.Height / 2.0) * cellSize;
            double dx = tx - x, dy = ty - y;
            double distanceSquared = dx * dx + dy * dy;
            if (searchRadius < 0 || distanceSquared > searchRadius * searchRadius) continue;
            double dot = Math.Max(-1, Math.Min(1, (dx * directionX + dy * directionY) / Math.Sqrt(distanceSquared)));
            double weight = grid[ix, iy] / 10.0;
            double score = weight * ((dot + 1) * 0.4 + 0.2);
            const double epsilon = 1e-9;
            if (!found || score > best.Score + epsilon
                || (Math.Abs(score - best.Score) <= epsilon && (dot > best.Dot + epsilon
                || (Math.Abs(dot - best.Dot) <= epsilon && distanceSquared < best.DistanceSquared))))
            {
                best = new Candidate { CellX = ix, CellY = iy, X = tx, Y = ty,
                    Weight = weight, Dot = dot, Score = score, DistanceSquared = distanceSquared };
                found = true;
            }
        }
        return found;
    }
}
