using System.Collections.Generic;
using UnityEngine;

namespace Aquarium.Tiling
{
    [DefaultExecutionOrder(40)]
    public sealed class BattleshipProjectile : MonoBehaviour
    {
        private struct Contact
        {
            public RaycastHit2D Hit;
            public BattleshipSection Section;
            public AnimalBody Body;
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
        private readonly HashSet<AnimalBody> hitBodies = new HashSet<AnimalBody>();
        private readonly List<Contact> contacts = new List<Contact>();

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
            float dt = Time.deltaTime;
            if (dt <= 0 || power <= 0 || owner == null || !owner.IsAlive) { Destroy(gameObject); return; }
            if (kind == BattleshipWeaponKind.AntiShipMissile) Guide(dt);

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
                AnimalBody body = hit.collider.GetComponentInParent<AnimalBody>();
                if (body != null && owner.MayHit(body) && !hitBodies.Contains(body))
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
                if (contact.Section != null)
                {
                    if (!contact.Section.IsAttached || hitSections.Contains(contact.Section)) continue;
                    if (contact.Section.Owner == previousShip &&
                        Mathf.Abs(contact.Hit.distance - previousShipDistance) <= 0.0001f) continue;
                    previousShip = contact.Section.Owner;
                    previousShipDistance = contact.Hit.distance;
                    hitSections.Add(contact.Section);
                    power = contact.Section.Hit(power);
                }
                else if (contact.Body != null && contact.Body.IsAlive && hitBodies.Add(contact.Body))
                    power = contact.Body.Hit(power, null, null);
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
}
