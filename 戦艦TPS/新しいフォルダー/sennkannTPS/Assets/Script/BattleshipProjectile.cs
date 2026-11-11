using System.Collections.Generic;
using UnityEngine;

namespace Script
{
    [DefaultExecutionOrder(40)]
    public sealed class BattleshipProjectile : MonoBehaviour
    {
        [SerializeField] private double penetration = 1.0;
        [SerializeField] private double explosionRadius = 1.0;
        public double Penetration => penetration;

        private BattleshipInstance owner;
        private BattleshipClass sourceClass;
        private BattleshipWeaponKind kind;
        private Vector2 direction;
        private double power;
        private ProjectileFlight flight;
        private ProjectileEffectSettings effects;
        private float speed;
        private double decay;
        private float radius;
        private float guidanceTurnSpeed;
        private float navigationConstant;
        private Vector2 previousLineOfSight;

        public enum Affect
        {
            AP, APC, APCR, HE, HEAT, HESH,
            [InspectorName("光子ビーム")] Photon, [InspectorName("プラズマ砲")] Plasma,
            [InspectorName("電子ビーム")] Electron, [InspectorName("中性子ビーム（仮）")] Neutron,
        }

        [field: SerializeField] public Affect ProjectileAffect { get; set; } = Affect.AP;

        internal static void Fire(BattleshipSection source, BattleshipWeaponRuntime weapon, Vector2 direction)
        {
            var obj = new GameObject($"{weapon.Kind} {weapon.Grade} Bullet");
            obj.transform.position = new Vector3(source.transform.position.x, source.transform.position.y, -0.05f);
            if (weapon.Affect == Affect.Photon || weapon.Affect == Affect.Neutron)
            {
                obj.AddComponent<ProtectionTestProjectile>().Initialize(source.GetComponent<Renderer>()?.sharedMaterial,
                    source.transform.position, direction.normalized, 1f, weapon.Effects.beamDuration, null,
                    weapon.Power, 1d, null, weapon.Affect, (float)weapon.ExplosionRadius, weapon.Effects, source.Owner);
                return;
            }
            var projectile = obj.AddComponent<BattleshipProjectile>();
            projectile.owner = source.Owner;
            projectile.sourceClass = source.Owner.ShipClass;
            projectile.kind = weapon.Kind;
            projectile.direction = direction.sqrMagnitude > 0 ? direction.normalized : Vector2.up;
            projectile.power = weapon.Power;
            projectile.ProjectileAffect = weapon.Affect;
            projectile.explosionRadius = weapon.ExplosionRadius;
            projectile.effects = weapon.Effects.Snapshot();
            projectile.speed = weapon.Speed;
            projectile.decay = weapon.Decay;
            projectile.radius = RadiusFor(weapon.Kind);
            projectile.guidanceTurnSpeed = weapon.GuidanceTurnSpeed;
            projectile.navigationConstant = weapon.NavigationConstant;
            Vector2 lineOfSight = source.Owner.CurrentTargetPosition - (Vector2)obj.transform.position;
            projectile.previousLineOfSight = lineOfSight.sqrMagnitude > 0.000001f
                ? lineOfSight.normalized : projectile.direction;
            projectile.CreateDisplay(source.GetComponent<Renderer>()?.sharedMaterial, ColorFor(weapon.Kind), weapon.Kind != BattleshipWeaponKind.MachineGun);
        }

        private void Update()
        {
            if (!ProjectilePenetration.IsValid(penetration))
            {
                Debug.LogError("弾の貫通力は0より大きい有限値が必要です。弾を停止・除去します。", this);
                Destroy(gameObject);
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0 || power <= 0 || owner == null || !owner.IsAlive) { Destroy(gameObject); return; }
            if (kind == BattleshipWeaponKind.AntiShipMissile) Guide(dt);

            Advance(dt);
        }

        private void Advance(float dt)
        {
            if (dt <= 0) return;
            if (flight == null)
                flight = new ProjectileFlight {
                    Position = transform.position, Direction = direction, Power = power, Penetration = penetration,
                    Affect = ProjectileAffect, Settings = effects ?? new ProjectileEffectSettings(), Radius = radius,
                    BlastRadius = (float)explosionRadius, Owner = owner, SourceClass = sourceClass,
                    Material = GetComponent<LineRenderer>()?.sharedMaterial
                };
            flight.Direction = direction;
            flight.Advance(dt, speed, decay);
            power = flight.Power; penetration = flight.Penetration; direction = flight.Direction;
            transform.position = new Vector3(flight.Position.x, flight.Position.y, transform.position.z);
            if (flight.Stopped) Destroy(gameObject);
        }

        private void Guide(float dt)
        {
            Vector2 desired = owner.CurrentTargetPosition - (Vector2)transform.position;
            if (desired.sqrMagnitude <= 0.000001f) return;
            Vector2 lineOfSight = desired.normalized;
            float sightRate = Vector2.SignedAngle(previousLineOfSight, lineOfSight) / dt;
            float turnRate = Mathf.Clamp(navigationConstant * sightRate, -guidanceTurnSpeed, guidanceTurnSpeed);
            direction = Quaternion.Euler(0, 0, turnRate * dt) * direction;
            direction.Normalize();
            previousLineOfSight = lineOfSight;
        }

        private void CreateDisplay(Material material, Color color, bool trail)
        {
            var line = gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 16;
            line.widthMultiplier = 1f;
            line.startColor = line.endColor = color;
            line.sortingOrder = 2;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
            if (!trail) return;
            var trailRenderer = gameObject.AddComponent<TrailRenderer>();
            trailRenderer.sharedMaterial = material;
            trailRenderer.time = ProjectileAffect == Affect.Plasma ? .8f : 1f;
            trailRenderer.startWidth = radius * 2f;
            trailRenderer.endWidth = 0f;
            trailRenderer.startColor = trailRenderer.endColor = ProjectileAffect == Affect.Plasma ? new Color(.7f, 0, 1f) : color;
            trailRenderer.sortingOrder = 1;
        }

        private static float RadiusFor(BattleshipWeaponKind value) => value switch
        {
            BattleshipWeaponKind.MachineGun => 2f,
            BattleshipWeaponKind.AntiShipMissile => 7f,
            _ => 5f
        };

        private static Color ColorFor(BattleshipWeaponKind value) => value switch
        {
            BattleshipWeaponKind.MachineGun => Color.yellow,
            BattleshipWeaponKind.SmallGun => new Color(1f, 0.5f, 0f),
            BattleshipWeaponKind.HeavyGun => new Color(1f, 0.25f, 0f),
            _ => new Color(1f, 0.3f, 0.65f)
        };
    }

    public class ExplosionEffect : MonoBehaviour
    {
        private const float Duration = .3f;
        private float explosionRadius;
        private LineRenderer outline;
        private float timer;
        private Material ownedMaterial;

        // Damage is instantaneous. The expanding ring is visual only.
        internal static void Detonate(Vector2 position, double radius, double power, double penetration,
            BattleshipInstance owner, BattleshipClass sourceClass, Material material,
            BattleshipInstance onlyTarget = null, BattleshipProjectile.Affect affect = BattleshipProjectile.Affect.HE,
            ProjectileEffectSettings settings = null)
        {
            if (double.IsNaN(radius) || double.IsInfinity(radius) || radius <= 0 || radius > float.MaxValue ||
                double.IsNaN(power) || double.IsInfinity(power) || power <= 0 ||
                !ProjectilePenetration.IsValid(penetration)) return;
            var sections = new HashSet<BattleshipSection>();
            var players = new HashSet<Player>();
            var filter = new ContactFilter2D().NoFilter();
            filter.useTriggers = true;
            var overlaps = new List<Collider2D>();
            Physics2D.SyncTransforms();
            bool previousQueries = Physics2D.queriesHitTriggers;
            try
            {
                Physics2D.queriesHitTriggers = true;
                Physics2D.OverlapCircle(position, (float)radius, filter, overlaps);
            }
            finally { Physics2D.queriesHitTriggers = previousQueries; }
            foreach (var collider in overlaps)
            {
                var section = collider.GetComponentInParent<BattleshipSection>();
                if (section != null)
                {
                    if (!section.IsAttached || section.Owner == null || !section.Owner.IsAlive ||
                        (onlyTarget != null ? section.Owner != onlyTarget :
                        section.Owner == owner || section.Owner.ShipClass >= sourceClass)) continue;
                    var polygon = section.GetComponent<PolygonCollider2D>();
                    if (polygon == null || !polygon.enabled ||
                        (polygon.ClosestPoint(position) - position).sqrMagnitude > radius * radius || !sections.Add(section)) continue;
                    // Each section gets an independent copy; interception must not change the blast radius
                    // or the penetration available to another section.
                    double localPenetration = penetration;
                    double localPower = affect == BattleshipProjectile.Affect.HE
                        ? ProjectileEffects.BlastPower(power, Vector2.Distance(position, section.transform.position)) : power;
                    section.HitEffect(localPower, ref localPenetration, polygon.ClosestPoint(position),
                        Vector2.zero, Vector2.zero, out _, affect, false, null);
                    if (settings != null) section.Owner.AccumulateImpact(settings.heat, settings.em);
                    continue;
                }
                var player = collider.GetComponentInParent<Player>();
                if (onlyTarget == null && player != null && players.Add(player))
                    player.Hit(affect == BattleshipProjectile.Affect.HE
                        ? ProjectileEffects.BlastPower(power, Vector2.Distance(position, player.transform.position)) : power, penetration);
            }

            var effect = new GameObject("HE Explosion").AddComponent<ExplosionEffect>();
            effect.transform.position = new Vector3(position.x, position.y, -.1f);
            effect.explosionRadius = (float)radius;
            effect.outline = effect.gameObject.AddComponent<LineRenderer>();
            var outline = effect.outline;
            if (material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) material = effect.ownedMaterial = new Material(shader);
            }
            outline.sharedMaterial = material;
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.positionCount = 24;
            outline.widthMultiplier = Mathf.Max(.05f, (float)radius * .03f);
            outline.sortingOrder = 3;
            effect.UpdateDisplay();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= Duration) { Destroy(gameObject); return; }
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (outline == null) return;
            float progress = Mathf.Clamp01(timer / Duration);
            float radius = explosionRadius * Mathf.Lerp(.1f, 1f, progress);
            outline.startColor = outline.endColor = new Color(1f, .5f, 0f, 1f - progress);
            for (int i = 0; i < outline.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / outline.positionCount;
                outline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
        }

        private void OnDestroy() { if (ownedMaterial != null) Destroy(ownedMaterial); }
    }
}

namespace Script
{
    [System.Serializable]
    public sealed class ProjectileEffectSettings
    {
        [Min(0), InspectorName("光子ビーム秒間ダメージ")] public float photonDps = 50f;
        [Min(.001f), InspectorName("光子ビーム判定間隔")] public float photonInterval = .2f;
        [Min(.01f), InspectorName("ビーム照射時間")] public float beamDuration = 1f;
        [Min(1), InspectorName("ビーム射程")] public float beamRange = 2000f;
        [Min(0), InspectorName("熱量（区画ごと）")] public float heat;
        [Min(0), InspectorName("EM付与量（区画ごと）")] public float em;
        [Min(0), InspectorName("電子ビームEM付与量（試験値）")] public float electronEm = 25f;
        [Min(0), InspectorName("バリア軽減倍率・光子（仮設定）")] public float photonBarrierMultiplier = 1f;
        [Min(0), InspectorName("バリア軽減倍率・中性子（仮設定）")] public float neutronBarrierMultiplier = 1f;
        [Min(0), InspectorName("バリア軽減倍率・プラズマ（仮設定）")] public float plasmaBarrierMultiplier = 1f;
        [Min(0), InspectorName("バリア軽減倍率・電子（仮設定）")] public float electronBarrierMultiplier = 1f;
        public ProjectileEffectSettings Snapshot() => (ProjectileEffectSettings)MemberwiseClone();
        internal float BarrierMultiplier(BattleshipProjectile.Affect affect) => affect switch {
            BattleshipProjectile.Affect.Photon => photonBarrierMultiplier,
            BattleshipProjectile.Affect.Neutron => neutronBarrierMultiplier,
            BattleshipProjectile.Affect.Plasma => plasmaBarrierMultiplier,
            BattleshipProjectile.Affect.Electron => electronBarrierMultiplier,
            _ => 1f
        };
    }

    public static class ProjectileEffects
    {
        public static bool IsEnergy(BattleshipProjectile.Affect affect)
            => affect >= BattleshipProjectile.Affect.Photon;

        // normal can face either way. Total-reflection conditions preserve the incident heading.
        public static Vector2 Refract(Vector2 vector, Vector2 normal, float ratio)
        {
            float length = vector.magnitude;
            if (length <= .000001f || normal.sqrMagnitude <= .000001f) return vector;
            Vector2 direction = vector / length;
            normal.Normalize();
            if (Vector2.Dot(direction, normal) > 0) normal = -normal;
            float cosine = Mathf.Clamp01(-Vector2.Dot(direction, normal));
            float k = 1f - ratio * ratio * (1f - cosine * cosine);
            if (k < 0) return vector;
            return (ratio * direction + (ratio * cosine - Mathf.Sqrt(k)) * normal).normalized * length;
        }

        public static double BlastPower(double power, double distance)
            => distance <= 0 ? power : System.Math.Max(0d, System.Math.Min(power / distance, power));

        internal static bool CircleBoundary(Vector2 start, Vector2 direction, Vector2 center, float radius,
            float limit, out float distance, out Vector2 normal, out bool entering)
        {
            distance = 0; normal = Vector2.zero; entering = false;
            Vector2 offset = start - center;
            float b = Vector2.Dot(offset, direction);
            float c = offset.sqrMagnitude - radius * radius;
            float discriminant = b * b - c;
            if (radius <= 0 || discriminant <= .000001f) return false;
            float root = Mathf.Sqrt(discriminant);
            float near = -b - root, far = -b + root;
            distance = near > .0001f ? near : far;
            if (distance <= .0001f || distance > limit) return false;
            normal = (start + direction * distance - center).normalized;
            entering = Vector2.Dot(direction, normal) < 0;
            return true;
        }

        internal static bool PolygonBoundary(PolygonCollider2D polygon, Vector2 start, Vector2 direction,
            float limit, out float distance, out Vector2 normal)
        {
            distance = limit; normal = Vector2.zero;
            bool found = false;
            for (int p = 0; p < polygon.pathCount; p++)
            {
                Vector2[] vertices = polygon.GetPath(p);
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector2 a = polygon.transform.TransformPoint(vertices[i] + polygon.offset);
                    Vector2 b = polygon.transform.TransformPoint(vertices[(i + 1) % vertices.Length] + polygon.offset);
                    Vector2 edge = b - a, delta = a - start;
                    float denominator = Cross(direction, edge);
                    if (Mathf.Abs(denominator) < .000001f) continue;
                    float t = Cross(delta, edge) / denominator, u = Cross(delta, direction) / denominator;
                    if (t <= .0001f || t > distance || u < 0 || u > 1) continue;
                    distance = t;
                    normal = new Vector2(-edge.y, edge.x).normalized;
                    found = true;
                }
            }
            return found;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }

    // Shared by combat and the protection test. Every changed heading starts a new sweep.
    internal sealed class ProjectileFlight
    {
        internal Vector2 Position, Direction;
        internal double Power, Penetration;
        internal BattleshipProjectile.Affect Affect;
        internal ProjectileEffectSettings Settings;
        internal BattleshipInstance Owner, OnlyTarget;
        internal BattleshipClass SourceClass;
        internal float Radius, BlastRadius;
        internal Material Material;
        internal System.Action<string> Report;
        internal readonly List<Vector3> Path = new List<Vector3>();
        private readonly HashSet<BattleshipSection> damaged = new HashSet<BattleshipSection>();
        private readonly HashSet<Player> hitPlayers = new HashSet<Player>();
        private bool firstContact = true;
        private float plasmaTimer = Random.Range(.1f, .5f);
        internal bool Stopped => Power <= 1e-9 || Penetration <= 0;
        private bool Eligible(BattleshipInstance ship)
            => ship != null && ship.IsAlive && (OnlyTarget != null ? ship == OnlyTarget : ship != Owner && ship.ShipClass < SourceClass);

        internal void Advance(float seconds, float speed, double decay = 0)
        {
            int slices = 0;
            while (seconds > .000001f && !Stopped && slices++ < 10000)
            {
                float step = Affect == BattleshipProjectile.Affect.Plasma ? Mathf.Min(seconds, plasmaTimer) : seconds;
                Sweep(speed * step);
                Power = System.Math.Max(0, Power - decay * step);
                seconds -= step;
                if (Affect == BattleshipProjectile.Affect.Plasma)
                {
                    plasmaTimer -= step;
                    if (plasmaTimer <= .000001f)
                    {
                        Direction = (Quaternion.Euler(0, 0, Random.Range(-30f, 30f)) * Direction).normalized;
                        plasmaTimer = Random.Range(.1f, .5f);
                    }
                }
            }
        }

        internal void Sweep(float distance)
        {
            Direction.Normalize();
            Path.Clear(); Path.Add(Position);
            Physics2D.SyncTransforms();
            int crossings = 0;
            while (distance > .000001f && !Stopped && Direction.sqrMagnitude > .000001f)
            {
                if (++crossings > 512) { Report?.Invoke("境界処理上限に達したため弾を停止しました"); Power = 0; break; }
                float nearest = distance;
                Vector2 normal = Vector2.zero, point = Position + Direction * distance;
                BattleshipSection section = null;
                Player player = null;
                BattleshipInstance barrier = null;
                bool entering = false;
                if (Affect == BattleshipProjectile.Affect.Neutron)
                {
                    var filter = new ContactFilter2D().NoFilter(); filter.useTriggers = true;
                    var rayHits = new List<RaycastHit2D>();
                    Physics2D.Raycast(Position, Direction, filter, rayHits, distance);
                    foreach (var hit in rayHits)
                    {
                        var part = hit.collider.GetComponentInParent<BattleshipSection>();
                        if (hit.fraction > 0 && hit.distance <= nearest && part != null &&
                            part.IsAttached && Eligible(part.Owner))
                        { nearest = hit.distance; normal = hit.normal; section = part; point = hit.point; }
                    }
                    // An inside-origin ray hit has distance zero; find its exit edge separately.
                    foreach (var ship in BattleshipInstance.ActiveShips)
                    {
                        if (!Eligible(ship)) continue;
                        foreach (var part in ship.GetComponentsInChildren<BattleshipSection>())
                            if (part.IsAttached && part.TryGetComponent(out PolygonCollider2D polygon) && polygon.enabled && polygon.OverlapPoint(Position) &&
                                ProjectileEffects.PolygonBoundary(polygon, Position, Direction, nearest, out float d, out Vector2 n))
                            { nearest = d; normal = n; section = part; point = Position + Direction * d; }
                    }
                }
                else
                {
                    var filter = new ContactFilter2D().NoFilter(); filter.useTriggers = true;
                    var hits = new List<RaycastHit2D>();
                    bool previousQueries = Physics2D.queriesHitTriggers;
                    try
                    {
                        Physics2D.queriesHitTriggers = true;
                        if (Affect == BattleshipProjectile.Affect.Photon)
                            Physics2D.Raycast(Position, Direction, filter, hits, distance);
                        else Physics2D.CircleCast(Position, Radius, Direction, filter, hits, distance);
                    }
                    finally { Physics2D.queriesHitTriggers = previousQueries; }
                    hits.Sort((a,b) => a.distance.CompareTo(b.distance));
                    foreach (var hit in hits)
                    {
                        var part = hit.collider.GetComponentInParent<BattleshipSection>();
                        if (part != null && part.IsAttached && Eligible(part.Owner) && !damaged.Contains(part))
                        { nearest = hit.distance; section = part; normal = hit.normal; point = hit.point; break; }
                        var candidate = hit.collider.GetComponentInParent<Player>();
                        if (OnlyTarget == null && part == null && candidate != null && candidate.Health > 0 && !hitPlayers.Contains(candidate))
                        { nearest = hit.distance; player = candidate; normal = hit.normal; point = hit.point; break; }
                    }
                }
                if (ProjectileEffects.IsEnergy(Affect))
                    foreach (var ship in BattleshipInstance.ActiveShips)
                        if (Eligible(ship) && ship.barrierEnabled &&
                            ProjectileEffects.CircleBoundary(Position, Direction, ship.CorePosition, ship.BarrierRadius, nearest,
                                out float d, out Vector2 n, out bool inward))
                        { nearest = d; barrier = ship; entering = inward; normal = n; }
                Position += Direction * nearest;
                distance -= nearest;
                Path.Add(Position);
                if (barrier != null)
                {
                    Direction = ProjectileEffects.Refract(Direction, normal, entering ? 1f / .8f : .8f).normalized;
                    if (entering || barrier.barrierReduceOnExit)
                        Power *= 1d - Mathf.Clamp01(barrier.barrierReduction * Settings.BarrierMultiplier(Affect));
                    Report?.Invoke($"バリア{(entering ? "進入" : "退出")} / 威力 {Power:0.##}");
                }
                else if (section != null || player != null)
                {
                    if (Affect == BattleshipProjectile.Affect.HE || Affect == BattleshipProjectile.Affect.Plasma)
                    {
                        ExplosionEffect.Detonate(point, BlastRadius, Power, Penetration, Owner, SourceClass, Material, OnlyTarget, Affect, Settings);
                        Report?.Invoke($"{Affect}着弾 / 半径 {BlastRadius:0.##}"); Power = 0; break;
                    }
                    if (section != null)
                    {
                        if (Affect == BattleshipProjectile.Affect.Neutron)
                            Direction = ProjectileEffects.Refract(Direction, normal, 1f / 1.1f).normalized;
                        if (damaged.Add(section))
                        {
                            double beforePower = Power, beforeHP = section.CurrentHealth, beforeP = Penetration;
                            Power = section.HitEffect(Power, ref Penetration, point, Direction, normal, out Vector2 next,
                                Affect, firstContact, Settings);
                            firstContact = false;
                            if (!Stopped) Direction = next.normalized;
                            Report?.Invoke($"{Affect} 区画{section.SectionId + 1} / HP {beforeHP:0.##} → {section.CurrentHealth:0.##} / 威力 {beforePower:0.##} → {Power:0.##} / 貫通力 {beforeP:0.##} → {Penetration:0.##} / 入射角 {section.LastImpactAngle:0.##}° / 実効装甲厚 {section.LastEffectiveArmor:0.##} / {(Stopped ? "停止" : "通過")}");
                        }
                    }
                    else if (hitPlayers.Add(player))
                    {
                        Power = player.Hit(Power, Penetration); firstContact = false;
                    }
                    if (Affect == BattleshipProjectile.Affect.Photon) { Power = 0; break; }
                }
                else break;
                if (Stopped) break;
                // Leave the processed boundary on its outgoing side, avoiding a zero-distance loop.
                float epsilon = Mathf.Min(.001f, distance);
                Position += Direction * epsilon; distance -= epsilon;
            }
            Path.Add(Position);
        }
    }
}
