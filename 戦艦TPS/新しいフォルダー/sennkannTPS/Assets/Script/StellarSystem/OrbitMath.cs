using System;

namespace StellarSystem
{
    public struct Position2D
    {
        public double X, Y;
        public Position2D(double x, double y) { X = x; Y = y; }
        public static Position2D operator +(Position2D a, Position2D b) => new Position2D(a.X + b.X, a.Y + b.Y);
        public static Position2D operator -(Position2D a, Position2D b) => new Position2D(a.X - b.X, a.Y - b.Y);
        public static Position2D operator *(Position2D a, double b) => new Position2D(a.X * b, a.Y * b);
        public double Length => Math.Sqrt(X * X + Y * Y);
    }

    // Units: AU, days, solar masses. Each planet is an independent circular two-body orbit.
    // Positions are star-relative, not an inertial N-body solution.
    public static class OrbitMath
    {
        public static double Period(double radiusAU, double starMass, double planetMass)
        {
            Positive(radiusAU); Positive(starMass); Positive(planetMass);
            return 365.25 * Math.Sqrt(radiusAU * radiusAU * radiusAU / (starMass + planetMass));
        }
        public static Position2D Rotate(Position2D p, double angle)
        {
            double c = Math.Cos(angle), s = Math.Sin(angle);
            return new Position2D(p.X * c - p.Y * s, p.X * s + p.Y * c);
        }
        public static double Phase(double days, double period, double phaseDegrees)
        {
            return (days % period) / period * (2 * Math.PI) + phaseDegrees * Math.PI / 180;
        }
        public static Position2D Velocity(Position2D position, double period)
        {
            double rate = 2 * Math.PI / period;
            return new Position2D(-position.Y * rate, position.X * rate);
        }
        public static Position2D Lagrange(double starMass, double planetMass, int point)
        {
            Positive(starMass); Positive(planetMass);
            if (planetMass >= starMass) throw new ArgumentException("Planet mass must be smaller than star mass.");
            double mu = planetMass / (starMass + planetMass);
            if (point == 4 || point == 5)
                return new Position2D(0.5, (point == 4 ? 1 : -1) * Math.Sqrt(3) / 2);
            // Solve the rotating-frame equilibrium in barycentric coordinates,
            // then shift the result to the star-relative frame (star at x=0, planet at x=1).
            double epsilon = 1e-12, lo, hi;
            switch (point)
            {
                case 1: lo = -mu + epsilon; hi = 1 - mu - epsilon; break;
                case 2: lo = 1 - mu + epsilon; hi = 3; break;
                case 3: lo = -3; hi = -mu - epsilon; break;
                default: throw new ArgumentOutOfRangeException(nameof(point));
            }
            for (int i = 0; i < 100; i++)
            {
                double mid = (lo + hi) * 0.5;
                if (Residual(mid, mu) > 0) hi = mid; else lo = mid;
            }
            return new Position2D((lo + hi) * 0.5 + mu, 0);
        }
        public static double Residual(double x, double mu)
        {
            double a = x + mu, b = x - 1 + mu;
            return x - (1 - mu) * a / Math.Pow(Math.Abs(a), 3) - mu * b / Math.Pow(Math.Abs(b), 3);
        }
        private static void Positive(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
