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

public static class GrassSightLine
{
    // Coordinates are continuous cell coordinates (0..width, 0..height).
    // Count endpoints once. Corner-only contacts are not crossed cells; an edge
    // belongs to the cell on its positive side, matching GrassGrid's half-open cells.
    public static double Sum(int width, int height, double ax, double ay, double bx, double by,
        Func<int, int, double> amount)
    {
        if (!(ax >= 0 && ax < width && bx >= 0 && bx < width &&
              ay >= 0 && ay < height && by >= 0 && by < height)) return 0;
        int x = (int)Math.Floor(ax), y = (int)Math.Floor(ay);
        int endX = (int)Math.Floor(bx), endY = (int)Math.Floor(by);
        double dx = bx - ax, dy = by - ay;
        int sx = Math.Sign(dx), sy = Math.Sign(dy);
        double stepX = sx == 0 ? double.PositiveInfinity : 1 / Math.Abs(dx);
        double stepY = sy == 0 ? double.PositiveInfinity : 1 / Math.Abs(dy);
        double nextX = sx == 0 ? double.PositiveInfinity : ((sx > 0 ? x + 1 : x) - ax) / dx;
        double nextY = sy == 0 ? double.PositiveInfinity : ((sy > 0 ? y + 1 : y) - ay) / dy;
        double sum = 0;
        for (int count = 0; count <= width + height; count++)
        {
            sum += Math.Max(0, amount(x, y));
            if (x == endX && y == endY) break;
            // Once an end axis is reached it must not step past an endpoint on a grid line.
            double tx = x == endX ? double.PositiveInfinity : nextX;
            double ty = y == endY ? double.PositiveInfinity : nextY;
            if (tx < ty) { x += sx; nextX += stepX; }
            else if (ty < tx) { y += sy; nextY += stepY; }
            else { x += sx; y += sy; nextX += stepX; nextY += stepY; }
        }
        return sum;
    }
}
