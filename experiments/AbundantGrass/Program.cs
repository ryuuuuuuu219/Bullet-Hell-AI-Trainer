using System.Numerics;
using System.Text.Json;

// Exploratory model, NOT Unity Play Mode. Unlimited grass at every herbivore position.
// Resource accounting and swept collision are linked directly from game sources.
// Movement, scheduling and spawning are approximated here at fixed time steps.
class Animal(bool herb, Vector2 pos, double heading)
{
    public bool Herb = herb;
    public Vector2 Pos = pos;
    public double Heading = heading, Decision, Shot, Feed;
    public bool Stopped, HadTarget;
    public Animal Target;
    public AnimalResources R = herb ? new HerbivoreResources() : new AnimalResources(50);
}
class Bullet(Animal owner)
{
    public Animal Owner = owner;
    public Vector2 Pos = owner.Pos;
    public Vector2 Dir = new((float)Math.Cos(owner.Heading), (float)Math.Sin(owner.Heading));
    public double Power = 15;
    public HashSet<Animal> Hit = new();
}
class Program
{
    static void Choose(Animal a, List<Animal> animals)
    {
        if (a.Target != null && !a.Target.R.IsDead) return;
        a.Target = null; a.HadTarget = false; a.Stopped = false;
        double nearest = 1000 * 1000;
        foreach (var b in animals)
        {
            if (!b.Herb || b.R.IsDead) continue;
            var d = Vector2.DistanceSquared(a.Pos, b.Pos);
            if (d > nearest) continue;
            nearest = d; a.Target = b;
        }
        a.HadTarget = a.Target != null;
    }
    static object Run(int spacing, double dt)
    {
        var animals = new List<Animal>(); var bullets = new List<Bullet>();
        for (int i = -2; i <= 2; i++) animals.Add(new(true, new(0, i * spacing), 0));
        var first = new Animal(false, new(-400, 0), 0); animals.Add(first); Choose(first, animals);
        int birthsH = 0, birthsC = 0, killedH = 0, killedC = 0, starvedC = 0, friendlyHits = 0, blocked = 0, shots = 0;
        double firstKill = -1, firstBirth = -1, extinct = -1, friendlyDamage = 0;
        var snapshots = new List<object>();
        void Snapshot(double t) => snapshots.Add(new { time = Math.Round(t, 3), herbivores = animals.Count(a => a.Herb && !a.R.IsDead), carnivores = animals.Count(a => !a.Herb && !a.R.IsDead), birthsH, birthsC, killedH, killedC, starvedC, friendlyHits, blocked, shots });
        Snapshot(0);
        for (int step = 0; step < (int)Math.Round(120 / dt); step++)
        {
            double time = (step + 1) * dt;
            foreach (var a in animals.Where(a => a.Herb && !a.R.IsDead).ToArray())
            {
                a.R.Maintain(dt); a.Feed = Math.Max(0, a.Feed - dt);
                if (a.R.IsDead || a.Feed > 1e-9) continue;
                a.R.Feed(1); a.Feed = 1;
                if (a.R.TryReproduce()) { animals.Add(new(true, a.Pos, a.Heading + Math.PI)); birthsH++; }
            }
            foreach (var a in animals.Where(a => !a.Herb && !a.R.IsDead).ToArray())
            {
                a.R.Maintain(dt);
                if (a.R.IsDead) { starvedC++; continue; }
                a.Decision += dt;
                if (a.Decision >= 1 - 1e-9) { a.Decision -= 1; Choose(a, animals); }
                bool boundary = Math.Abs(a.Pos.X) >= 5000 - (210 / Math.PI + 80) || Math.Abs(a.Pos.Y) >= 5000 - (210 / Math.PI + 80);
                bool targetAlive = a.Target != null && !a.Target.R.IsDead;
                Vector2 desired = new((float)Math.Cos(a.Heading), (float)Math.Sin(a.Heading));
                if (boundary || (a.HadTarget && !targetAlive)) { a.Stopped = false; desired = -a.Pos; }
                else if (targetAlive)
                {
                    desired = a.Target.Pos - a.Pos;
                    if (a.Stopped) { if (desired.LengthSquared() >= 200 * 200) a.Stopped = false; }
                    else if (desired.LengthSquared() <= 150 * 150) a.Stopped = true;
                }
                else a.Stopped = false;
                if (desired.LengthSquared() > 0)
                {
                    double delta = Math.Atan2(desired.Y, desired.X) - a.Heading;
                    delta = Math.Atan2(Math.Sin(delta), Math.Cos(delta));
                    a.Heading += Math.Clamp(delta, -Math.PI * dt, Math.PI * dt);
                }
                if (!a.Stopped)
                {
                    a.Pos += new Vector2((float)Math.Cos(a.Heading), (float)Math.Sin(a.Heading)) * (float)(105 * dt);
                    a.Pos = Vector2.Clamp(a.Pos, new(-5000), new(4999.999f));
                }
                a.Shot = Math.Min(1, a.Shot + dt);
                if (!boundary && targetAlive && Vector2.DistanceSquared(a.Pos, a.Target.Pos) <= 40000 && a.Shot >= 1 - 1e-9)
                { a.Shot = 0; bullets.Add(new(a)); shots++; }
            }
            foreach (var b in bullets.ToArray())
            {
                var end = b.Pos + b.Dir * (float)(250 * dt);
                var contacts = new List<(Animal a, double f)>();
                foreach (var a in animals)
                    if (a != b.Owner && !a.R.IsDead && !b.Hit.Contains(a) && ProjectileSweep.TryHit(b.Pos.X,b.Pos.Y,end.X,end.Y,a.Pos.X,a.Pos.Y,85,out double f)) contacts.Add((a,f));
                double previous = 0;
                foreach (var (a, f) in contacts.OrderBy(c => c.f))
                {
                    if (a.R.IsDead) continue;
                    b.Power = Math.Max(0, b.Power - 10 * dt * (f - previous)); previous = f;
                    if (b.Power <= 0) break;
                    b.Hit.Add(a);
                    double hp = a.R.Health;
                    b.Power = a.R.Hit(b.Power, 2, a.Herb ? 1 : 2, out double stolen);
                    if (!a.Herb) { friendlyHits++; friendlyDamage += hp - a.R.Health; if (b.Power <= 0) blocked++; }
                    if (!a.R.IsDead) continue;
                    if (a.Herb) { killedH++; if (firstKill < 0) firstKill = time; } else killedC++;
                    if (!b.Owner.R.IsDead)
                    {
                        b.Owner.R.Feed(stolen);
                        if (b.Owner.R.TryReproduce())
                        {
                            var child = new Animal(false, b.Owner.Pos, b.Owner.Heading + Math.PI);
                            animals.Add(child); Choose(child, animals); birthsC++;
                            if (firstBirth < 0) firstBirth = time;
                        }
                    }
                    if (b.Power <= 0) break;
                }
                b.Power = Math.Max(0, b.Power - 10 * dt * (1 - previous));
                b.Pos = end;
                if (b.Power <= 0) bullets.Remove(b);
            }
            if (extinct < 0 && !animals.Any(a => !a.Herb && !a.R.IsDead)) extinct = time;
            if ((step + 1) % (int)Math.Round(10 / dt) == 0) Snapshot(time);
        }
        return new { spacing, dt, firstKill, firstBirth, carnivoreExtinction = extinct, friendlyDamage, snapshots };
    }
    static void Main()
    {
        var results = new List<object>();
        foreach (var dt in new[] { 0.02, 0.01 }) foreach (int spacing in new[] { 60, 180, 300 }) results.Add(Run(spacing, dt));
        Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
