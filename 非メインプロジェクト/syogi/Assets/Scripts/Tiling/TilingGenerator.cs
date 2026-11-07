using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquarium.Tiling
{
    // Independent of Unity so the actual geometry can also be checked in a console test.
    public sealed partial class TilingGenerator
    {
        public const double Epsilon = 0.000001;
        public struct Point
        {
            public double X, Y;
            public Point(double x, double y) { X = x; Y = y; }
            public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);
            public static Point operator -(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y);
            public static Point operator *(Point a, double b) => new Point(a.X * b, a.Y * b);
        }
        public sealed class Tile
        {
            public readonly Point[] Vertices;
            public bool Triangle => Vertices.Length == 3;
            public double Area => Triangle ? Math.Sqrt(3) / 4 : 1;
            public Tile(params Point[] vertices) { Vertices = vertices; }
        }
        public sealed class Frame
        {
            public readonly Tile[] Tiles;
            public readonly string Label;
            public readonly int Added;
            public Frame(IEnumerable<Tile> tiles, string label, int added = 0)
            {
                Tiles = tiles.ToArray();
                Label = label; Added = added;
            }
        }
        private struct Edge { public Point A, B; public Edge(Point a, Point b) { A = a; B = b; } }
        private sealed class Frontier { public List<Edge> Edges = new List<Edge>(); }
        private readonly Random random;
        private readonly int target, maxAttempts;
        private List<Tile> tiles;
        public readonly List<Frame> Frames = new List<Frame>();
        public int Attempts { get; private set; }
        public int Accepted { get; private set; }
        public int OverlapRejected { get; private set; }
        public int AxisGapFilled { get; private set; }
        public int Count => tiles.Count;
        public bool InitialSymmetric { get; }
        public bool Reached => InitialSymmetric && Count >= target;
        public bool Done => !InitialSymmetric || Reached || Attempts >= maxAttempts;

        public TilingGenerator(int seed, int target = 24, int maxAttempts = 160, bool triangleStart = false, double? initialAngle = null)
            : this(new Random(seed), target, maxAttempts, triangleStart, initialAngle) { }

        public TilingGenerator(Random random, int target = 24, int maxAttempts = 160, bool triangleStart = false, double? initialAngle = null)
        {
            this.target = Math.Max(1, Math.Min(128, target));
            this.maxAttempts = Math.Max(1, Math.Min(2000, maxAttempts));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            double angle = initialAngle ?? (triangleStart ? 30 : 0);
            if (double.IsNaN(angle) || double.IsInfinity(angle)) throw new ArgumentOutOfRangeException(nameof(initialAngle));
            double radians = (angle % 360) * Math.PI / 180;
            double c = Math.Cos(radians), s = Math.Sin(radians), r = Math.Sqrt(3);
            var p = triangleStart
                ? new[] { new Point(-r / 6, -.5), new Point(r / 3, 0), new Point(-r / 6, .5) }
                : new[] { new Point(-.5, -.5), new Point(.5, -.5), new Point(.5, .5), new Point(-.5, .5) };
            tiles = new List<Tile> { new Tile(p.Select(v => new Point(v.X * c - v.Y * s, v.X * s + v.Y * c)).ToArray()) };
            InitialSymmetric = MirrorPair(tiles[0]).Count == 1;
            Frames.Add(new Frame(tiles, "初期区画"));
        }

        // One bounded attempt; the scene yields between attempts to remain responsive.
        public void Step()
        {
            if (Done) return;
            Attempts++;
            var frontier = GetFrontier(tiles);
            if (frontier.Count == 0) { Attempts = maxAttempts; return; }
            var selected = frontier[random.Next(frontier.Count)];
            var edge = selected.Edges[random.Next(selected.Edges.Count)];
            var pair = MirrorPair(Attach(edge, random.Next(2) == 0));
            if (!Fits(pair, tiles)) { OverlapRejected++; return; }
            tiles.AddRange(pair);
            Frames.Add(new Frame(tiles, "区画を増設", pair.Count));
            Accepted++;
            Tile gap;
            while ((gap = FindAxisGap(tiles)) != null)
            {
                tiles.Add(gap);
                AxisGapFilled++;
                Frames.Add(new Frame(tiles, "Y軸上の三角形を補完", 1));
            }
        }

        internal static Tile FindAxisGap(List<Tile> parts)
        {
            var edges = GetFrontier(parts).SelectMany(f => f.Edges).ToList();
            foreach (var edge in edges)
            {
                Point side, axis;
                if (Math.Abs(edge.A.X - .5) < Epsilon && Math.Abs(edge.B.X) < Epsilon)
                { side = edge.A; axis = edge.B; }
                else if (Math.Abs(edge.B.X - .5) < Epsilon && Math.Abs(edge.A.X) < Epsilon)
                { side = edge.B; axis = edge.A; }
                else continue;
                var left = new Point(-side.X, side.Y);
                if (!edges.Any(e => (Near(e.A, left) && Near(e.B, axis)) || (Near(e.B, left) && Near(e.A, axis)))) continue;
                // Attach on the exterior side, not merely inside a triangle made
                // by three points. Both sloping sides must be unshared edges.
                var candidate = Attach(edge, true);
                if (!candidate.Vertices.Any(p => Near(p, left))) continue;
                if (MirrorPair(candidate).Count != 1) continue;
                if (!Fits(new List<Tile> { candidate }, parts)) continue;
                return candidate;
            }
            return null;
        }

        private static Tile Attach(Edge e, bool triangle)
        {
            var d = e.B - e.A; var n = new Point(d.Y, -d.X);
            return triangle
                ? new Tile(e.A, (e.A + e.B) * .5 + n * (Math.Sqrt(3) / 2), e.B)
                : new Tile(e.A, e.A + n, e.B + n, e.B);
        }
        private static List<Tile> MirrorPair(Tile t)
        {
            var reflected = new Tile(t.Vertices.Reverse().Select(p => new Point(-p.X, p.Y)).ToArray());
            return Same(t, reflected) ? new List<Tile> { t } : new List<Tile> { t, reflected };
        }
        private static bool Near(Point a, Point b) => Math.Abs(a.X - b.X) < Epsilon && Math.Abs(a.Y - b.Y) < Epsilon;
        private static bool Same(Tile a, Tile b) => a.Vertices.Length == b.Vertices.Length && a.Vertices.All(p => b.Vertices.Any(q => Near(p, q)));
        private static string Key(Point p) => Math.Round(p.X * 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + Math.Round(p.Y * 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        private static string Key(Edge e)
        {
            string a = Key(e.A), b = Key(e.B);
            return string.CompareOrdinal(a, b) < 0 ? a + ":" + b : b + ":" + a;
        }
        private static List<Frontier> GetFrontier(List<Tile> parts)
        {
            var counts = new Dictionary<string, int>();
            var all = new List<List<Edge>>();
            foreach (var t in parts)
            {
                var edges = new List<Edge>();
                for (int i = 0; i < t.Vertices.Length; i++)
                {
                    var e = new Edge(t.Vertices[i], t.Vertices[(i + 1) % t.Vertices.Length]); edges.Add(e);
                    string key = Key(e); counts.TryGetValue(key, out int n); counts[key] = n + 1;
                }
                all.Add(edges);
            }
            var result = new List<Frontier>();
            foreach (var edges in all)
            {
                var f = new Frontier(); f.Edges.AddRange(edges.Where(e => counts[Key(e)] == 1));
                if (f.Edges.Count > 0) result.Add(f);
            }
            return result;
        }
        private static bool Fits(List<Tile> added, List<Tile> existing)
        {
            for (int i = 0; i < added.Count; i++)
            {
                if (existing.Any(t => Overlaps(t, added[i]))) return false;
                for (int j = 0; j < i; j++) if (Overlaps(added[j], added[i])) return false;
            }
            return true;
        }
        public static bool Overlaps(Tile a, Tile b)
        {
            // Separating axis test. Shared edges and points are allowed.
            foreach (var poly in new[] { a, b })
                for (int i = 0; i < poly.Vertices.Length; i++)
                {
                    var d = poly.Vertices[(i + 1) % poly.Vertices.Length] - poly.Vertices[i];
                    var axis = new Point(-d.Y, d.X);
                    double amin = double.PositiveInfinity, amax = double.NegativeInfinity, bmin = amin, bmax = amax;
                    foreach (var p in a.Vertices) { double v = Dot(p, axis); amin = Math.Min(amin, v); amax = Math.Max(amax, v); }
                    foreach (var p in b.Vertices) { double v = Dot(p, axis); bmin = Math.Min(bmin, v); bmax = Math.Max(bmax, v); }
                    if (Math.Min(amax, bmax) - Math.Max(amin, bmin) <= Epsilon) return false;
                }
            return true;
        }
        private static double Dot(Point a, Point b) => a.X * b.X + a.Y * b.Y;
    }
}
