using System;

public static class CarnivoreSpacing
{
    public static void Repulsion(double dx, double dy, int selfId, int otherId,
        double radius, double strength, out double x, out double y)
    {
        x = y = 0;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (radius <= 0 || distance >= radius) return;
        if (distance < 1e-8)
        {
            // Opposite directions for the same pair, even at identical positions.
            uint hash = unchecked((uint)Math.Min(selfId, otherId) * 73856093u ^
                (uint)Math.Max(selfId, otherId) * 19349663u);
            double angle = (hash % 3600) * Math.PI / 1800;
            double sign = selfId < otherId ? 1 : -1;
            x = Math.Cos(angle) * strength * sign;
            y = Math.Sin(angle) * strength * sign;
            return;
        }
        double speed = strength * (1 - distance / radius);
        x = dx / distance * speed;
        y = dy / distance * speed;
    }

    public static void Limit(ref double x, ref double y, double maximum)
    {
        double speed = Math.Sqrt(x * x + y * y);
        if (speed <= maximum || speed == 0) return;
        x *= maximum / speed;
        y *= maximum / speed;
    }

    public static bool TryBirthPosition(double px, double py, double headingX, double headingY,
        double radius, double distanceSample, bool leftFirst, double halfSize,
        out double x, out double y)
    {
        x = px; y = py;
        double length = Math.Sqrt(headingX * headingX + headingY * headingY);
        if (length == 0) { headingX = 1; headingY = 0; length = 1; }
        double distance = radius * (1.5 + Math.Max(0, Math.Min(1, distanceSample)));
        double side = leftFirst ? 1 : -1;
        for (int attempt = 0; attempt < 2; attempt++, side = -side)
        {
            x = px - headingY / length * distance * side;
            y = py + headingX / length * distance * side;
            if (x >= -halfSize && x < halfSize && y >= -halfSize && y < halfSize) return true;
        }
        return false;
    }
}
