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
        private struct Contact
        {
            public RaycastHit2D Hit;
            public BattleshipSection Section;
            public GameObject Body;
        }

        private BattleshipAnimalInstance owner;
        private BattleshipAnimalClass sourceClass;
        private BattleshipWeaponKind kind;
        private Vector2 direction;
        private double power;
        private float speed;
        private double decay;
        private float radius;
        private float guidanceTurnSpeed;
        private float navigationConstant;
        private Vector2 previousLineOfSight;
        private readonly HashSet<BattleshipSection> hitSections = new HashSet<BattleshipSection>();
        private readonly HashSet<GameObject> hitBodies = new HashSet<GameObject>();
        private readonly List<Contact> contacts = new List<Contact>();

        public enum Affect
        {
            AP, APC, APCR, HE, HEAT, HESH,
        }

        [field: SerializeField] public Affect ProjectileAffect { get; set; } = Affect.AP;

        internal static void Fire(BattleshipSection source, BattleshipWeaponRuntime weapon, Vector2 direction)
        {
            var obj = new GameObject($"{weapon.Kind} {weapon.Grade} Bullet");
            obj.transform.position = new Vector3(source.transform.position.x, source.transform.position.y, -0.05f);
            var projectile = obj.AddComponent<BattleshipProjectile>();
            projectile.owner = source.Owner;
            projectile.sourceClass = source.Owner.ShipClass;
            projectile.kind = weapon.Kind;
            projectile.direction = direction.sqrMagnitude > 0 ? direction.normalized : Vector2.up;
            projectile.power = weapon.Power;
            projectile.ProjectileAffect = weapon.Affect;
            projectile.explosionRadius = weapon.ExplosionRadius;
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
            if (power <= 1e-9) { Destroy(gameObject); return; }
            if (direction.sqrMagnitude <= .00000001f)
            {
                power = System.Math.Max(0, power - decay * dt);
                if (power <= 1e-9) Destroy(gameObject);
                return;
            }
            Vector2 start = transform.position;
            Vector2 end = start + direction * (speed * dt);
            contacts.Clear();
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(start, radius, direction, speed * dt))
            {
                if (hit.collider == null) continue;
                BattleshipSection section = hit.collider.GetComponent<BattleshipSection>();
                if (section != null)
                {
                    if (!section.IsAttached || section.Owner == owner || section.Owner.ShipClass == sourceClass ||
                        section.Owner.ShipClass > sourceClass || hitSections.Contains(section)) continue;
                    contacts.Add(new Contact { Hit = hit, Section = section });
                    continue;
                }
                Player player = hit.collider.GetComponentInParent<Player>();
                GameObject body = player != null && player.isActiveAndEnabled && player.Health > 0
                    ? player.gameObject : null;
                if (body != null && !hitBodies.Contains(body))
                    contacts.Add(new Contact { Hit = hit, Body = body });
            }
            contacts.Sort((a, b) =>
            {
                int distance = a.Hit.distance.CompareTo(b.Hit.distance);
                if (distance != 0) return distance;
                int aId = a.Section == null ? int.MaxValue : a.Section.SectionId;
                int bId = b.Section == null ? int.MaxValue : b.Section.SectionId;
                return aId.CompareTo(bId);
            });

            BattleshipAnimalInstance previousShip = null;
            float previousShipDistance = float.NegativeInfinity;
            foreach (Contact contact in contacts)
            {
                if (power <= 0) break;
                if (ProjectileAffect == Affect.HE)
                {
                    ExplosionEffect.Detonate(contact.Hit.point, explosionRadius, power, penetration,
                        owner, sourceClass, GetComponent<LineRenderer>()?.sharedMaterial);
                    power = 0;
                    transform.position = new Vector3(contact.Hit.centroid.x, contact.Hit.centroid.y, transform.position.z);
                    Destroy(gameObject);
                    return;
                }
                if (contact.Section != null)
                {
                    if (!contact.Section.IsAttached || hitSections.Contains(contact.Section)) continue;
                    if (contact.Section.Owner == previousShip &&
                        Mathf.Abs(contact.Hit.distance - previousShipDistance) <= 0.0001f) continue;
                    previousShip = contact.Section.Owner;
                    previousShipDistance = contact.Hit.distance;
                    hitSections.Add(contact.Section);
                    power = contact.Section.HitProjectile(power, ref penetration, contact.Hit.point, direction, contact.Hit.normal,
                        out Vector2 outgoingDirection);
                    if (power <= 1e-9)
                    {
                        transform.position = new Vector3(contact.Hit.centroid.x, contact.Hit.centroid.y, transform.position.z);
                        Destroy(gameObject);
                        return;
                    }
                    Vector2 nextDirection = outgoingDirection.normalized;
                    if ((nextDirection - direction).sqrMagnitude > .00000001f)
                    {
                        float travelledSeconds = Mathf.Clamp(contact.Hit.distance / Mathf.Max(speed, .000001f), 0f, dt);
                        direction = nextDirection;
                        transform.position = new Vector3(contact.Hit.centroid.x, contact.Hit.centroid.y, transform.position.z);
                        power = System.Math.Max(0, power - decay * travelledSeconds);
                        Advance(dt - travelledSeconds);
                        return;
                    }
                }
                else if (contact.Body != null && hitBodies.Add(contact.Body))
                {
                    Player player = contact.Body.GetComponent<Player>();
                    if (player != null)
                        power = player.Hit(power, penetration);
                }
            }

            power = System.Math.Max(0, power - decay * dt);
            if (power <= 1e-9) { Destroy(gameObject); return; }
            transform.position = new Vector3(end.x, end.y, transform.position.z);
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
            trailRenderer.time = 1f;
            trailRenderer.startWidth = radius * 2f;
            trailRenderer.endWidth = 0f;
            trailRenderer.startColor = trailRenderer.endColor = color;
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
            BattleshipAnimalInstance owner, BattleshipAnimalClass sourceClass, Material material,
            BattleshipAnimalInstance onlyTarget = null)
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
            Physics2D.OverlapCircle(position, (float)radius, filter, overlaps);
            foreach (var collider in overlaps)
            {
                var section = collider.GetComponentInParent<BattleshipSection>();
                if (section != null)
                {
                    if (!section.IsAttached || section.Owner == null || !section.Owner.IsAlive ||
                        (onlyTarget != null ? section.Owner != onlyTarget :
                        section.Owner == owner || section.Owner.ShipClass >= sourceClass) || !sections.Add(section)) continue;
                    // Each section gets an independent copy; interception must not change the blast radius
                    // or the penetration available to another section. No distance falloff is specified yet.
                    double localPenetration = penetration;
                    section.HitProjectile(power, ref localPenetration, collider.ClosestPoint(position),
                        Vector2.zero, Vector2.zero, out _);
                    continue;
                }
                var player = collider.GetComponentInParent<Player>();
                if (onlyTarget == null && player != null && players.Add(player)) player.Hit(power, penetration);
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
