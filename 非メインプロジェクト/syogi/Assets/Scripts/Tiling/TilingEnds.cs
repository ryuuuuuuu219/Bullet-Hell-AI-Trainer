using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquarium.Tiling
{
    public sealed partial class TilingGenerator
    {
        public enum EndShape { None, TriangleTip, TrianglePairTip, SquareTip, SquareEdge, SquarePairEdge }
        public string UpperEndStatus { get; private set; } = "未処理";
        public string LowerEndStatus { get; private set; } = "未処理";
        public int EndTilesAdded { get; private set; }
        private bool endsProcessed;

        public static string EndShapeName(EndShape shape)
        {
            switch (shape)
            {
                case EndShape.TriangleTip: return "三角形の頂点";
                case EndShape.TrianglePairTip: return "ひし形の頂点";
                case EndShape.SquareTip: return "正方形の頂点";
                case EndShape.SquareEdge: return "正方形の辺";
                case EndShape.SquarePairEdge: return "正方形2枚の辺";
                default: return "未形成";
            }
        }

        // A cap must occupy the actual global extreme, not merely exist inside the body.
        public static EndShape DetectEnd(IReadOnlyList<Tile> parts, bool upper)
        {
            if (parts.Count == 0) return EndShape.None;
            int sign = upper ? 1 : -1;
            double extreme = parts.SelectMany(t => t.Vertices).Max(p => sign * p.Y);
            var tips = parts.SelectMany(t => t.Vertices).Where(p => Math.Abs(sign * p.Y - extreme) < Epsilon).ToArray();
            bool Only(Point[] allowed) => tips.All(p => allowed.Any(q => Near(p, q)));
            foreach (var tile in parts)
            {
                if (MirrorPair(tile).Count == 1)
                {
                    var top = tile.Vertices.Where(p => Math.Abs(sign * p.Y - extreme) < Epsilon).ToArray();
                    if (top.Length == 1 && Math.Abs(top[0].X) < Epsilon && Only(top))
                        return tile.Triangle ? EndShape.TriangleTip : EndShape.SquareTip;
                    if (!tile.Triangle && top.Length == 2 && Only(top)
                        && Math.Abs(top[0].X + top[1].X) < Epsilon && Math.Abs(top[0].X - top[1].X) > 1 - Epsilon)
                        return EndShape.SquareEdge;
                }
                var pair = MirrorPair(tile);
                if (pair.Count != 2 || !parts.Any(t => Same(t, pair[1]))) continue;
                for (int i = 0; i < tile.Vertices.Length; i++)
                {
                    var a = tile.Vertices[i]; var b = tile.Vertices[(i + 1) % tile.Vertices.Length];
                    if (Math.Abs(a.X) >= Epsilon || Math.Abs(b.X) >= Epsilon) continue;
                    var top = pair.SelectMany(t => t.Vertices).Where(p => Math.Abs(sign * p.Y - extreme) < Epsilon).ToArray();
                    if (top.Length == 0 || !Only(top)) continue;
                    if (tile.Triangle && top.All(p => Math.Abs(p.X) < Epsilon)) return EndShape.TrianglePairTip;
                    if (!tile.Triangle && top.Any(p => Math.Abs(p.X - 1) < Epsilon)
                        && top.Any(p => Math.Abs(p.X + 1) < Epsilon)) return EndShape.SquarePairEdge;
                }
            }
            return EndShape.None;
        }

        private sealed class EndBranch
        {
            public List<Tile> Parts;
            public List<List<Tile>> Additions;
            public double Score;
        }
        private static string ArrangementKey(IEnumerable<Tile> parts) => string.Join(";", parts.Select(t =>
            string.Join("/", t.Vertices.Select(Key).OrderBy(k => k, StringComparer.Ordinal))).OrderBy(k => k, StringComparer.Ordinal));

        // Bounded best-first branching. Candidate states never mutate the live ship.
        // Yield between expansions so the scene can continue repainting.
        public IEnumerable<int> FormEnds(int maximumDepth = 6, int candidateLimit = 1200)
        {
            if (!Done || endsProcessed) yield break;
            endsProcessed = true;
            if (!InitialSymmetric) { UpperEndStatus = LowerEndStatus = "初期形状が非対称"; yield break; }
            maximumDepth = Math.Max(0, Math.Min(12, maximumDepth));
            candidateLimit = Math.Max(0, Math.Min(10000, candidateLimit));
            foreach (bool upper in new[] { true, false })
            {
                int sign = upper ? 1 : -1;
                bool preserveOpposite = DetectEnd(tiles, !upper) != EndShape.None;
                string name = upper ? "上端" : "下端";
                var existing = DetectEnd(tiles, upper);
                if (existing != EndShape.None)
                {
                    if (upper) UpperEndStatus = EndShapeName(existing); else LowerEndStatus = EndShapeName(existing);
                    continue;
                }
                if (upper) UpperEndStatus = "探索中"; else LowerEndStatus = "探索中";
                double extreme = tiles.SelectMany(t => t.Vertices).Max(p => sign * p.Y);
                double floor = Math.Max(extreme - 2, tiles.SelectMany(t => t.Vertices).Min(p => sign * p.Y));
                double maxWidth = tiles.SelectMany(t => t.Vertices).Max(p => Math.Abs(p.X)) + 1;
                var open = new List<EndBranch> { new EndBranch { Parts = new List<Tile>(tiles), Additions = new List<List<Tile>>() } };
                var visited = new HashSet<string> { ArrangementKey(tiles) };
                EndBranch solution = null;
                int examined = 0;
                while (open.Count > 0 && examined < candidateLimit && solution == null)
                {
                    open.Sort((a,b) => a.Score.CompareTo(b.Score));
                    var branch = open[0]; open.RemoveAt(0);
                    if (branch.Additions.Count >= maximumDepth) continue;
                    foreach (var section in GetFrontier(branch.Parts))
                    {
                        foreach (var edge in section.Edges)
                        {
                            if (Math.Max(edge.A.X, edge.B.X) < -Epsilon) continue;
                            if (Math.Min(sign * edge.A.Y, sign * edge.B.Y) < floor - Epsilon) continue;
                            for (int shape = 0; shape < 2 && examined < candidateLimit; shape++)
                            {
                                examined++;
                                var added = MirrorPair(Attach(edge, shape == 0));
                                if (added.Any(t => t.Vertices.Any(p => sign * p.Y < floor - Epsilon || Math.Abs(p.X) > maxWidth))) continue;
                                if (!Fits(added, branch.Parts)) continue;
                                var next = new EndBranch { Parts = branch.Parts.Concat(added).ToList(), Additions = new List<List<Tile>>(branch.Additions) { added } };
                                if (preserveOpposite && DetectEnd(next.Parts, !upper) == EndShape.None) continue;
                                if (!visited.Add(ArrangementKey(next.Parts))) continue;
                                if (DetectEnd(next.Parts, upper) != EndShape.None) { solution = next; break; }
                                double tip = next.Parts.SelectMany(t => t.Vertices).Max(p => sign * p.Y);
                                // Prefer small additions approaching the axis near the extreme.
                                double distance = next.Parts.SelectMany(t => t.Vertices)
                                    .Min(p => Math.Abs(p.X) + Math.Max(0, tip - sign * p.Y));
                                next.Score = distance + next.Additions.Sum(a => a.Count) * .12;
                                open.Add(next);
                            }
                            if (solution != null || examined >= candidateLimit) break;
                        }
                        if (solution != null || examined >= candidateLimit) break;
                    }
                    // Bound retained states as well as candidate evaluations.
                    if (open.Count > 128) { open.Sort((a,b) => a.Score.CompareTo(b.Score)); open.RemoveRange(128, open.Count - 128); }
                    yield return examined;
                }
                if (solution != null)
                {
                    foreach (var added in solution.Additions)
                    {
                        tiles.AddRange(added); EndTilesAdded += added.Count;
                        Frames.Add(new Frame(tiles, name + "を形成", added.Count));
                        yield return examined;
                    }
                }
                string result = solution != null ? EndShapeName(DetectEnd(tiles, upper)) : "未形成（探索打切り）";
                if (upper) UpperEndStatus = result; else LowerEndStatus = result;
            }
        }
    }
}
