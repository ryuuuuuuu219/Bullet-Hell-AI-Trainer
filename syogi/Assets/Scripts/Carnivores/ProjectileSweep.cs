using System;

public static class ProjectileSweep
{
    // Swept circle against a circular body: first contact along a finite segment.
    public static bool TryHit(double startX, double startY, double endX, double endY,
        double centerX, double centerY, double radius, out double fraction)
    {
        fraction = 0;
        double x = startX - centerX, y = startY - centerY;
        double c = x * x + y * y - radius * radius;
        if (c <= 0) return true;
        double dx = endX - startX, dy = endY - startY;
        double a = dx * dx + dy * dy;
        if (a <= 0) return false;
        double b = x * dx + y * dy;
        double discriminant = b * b - a * c;
        if (discriminant < 0) return false;
        fraction = (-b - Math.Sqrt(discriminant)) / a;
        return fraction >= 0 && fraction <= 1;
    }
}
