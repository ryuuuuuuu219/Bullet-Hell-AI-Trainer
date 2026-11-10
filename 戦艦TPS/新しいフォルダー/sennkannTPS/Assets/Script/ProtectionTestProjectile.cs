using System;
using System.Collections.Generic;
using UnityEngine;

namespace Script
{
    // Uses the same section damage/remaining-power path as combat projectiles.
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ProtectionTestProjectile : MonoBehaviour
    {
        public Vector2 InitialPosition { get; private set; }
        public Vector2 InitialVelocity { get; private set; }
        public Vector2 Velocity { get; private set; }
        private float remainingSeconds;
        private float collisionRadius;
        private double penetration;
        private BattleshipProjectile.Affect affect;
        private float explosionRadius;
        public double Penetration => penetration;
        private BattleshipAnimalInstance target;
        private Action<string> report;
        private readonly HashSet<BattleshipSection> hitSections = new HashSet<BattleshipSection>();
        private readonly List<RaycastHit2D> contacts = new List<RaycastHit2D>();
        public double RemainingPower { get; private set; }

        public void Initialize(Material material, Vector2 position, Vector2 velocity,
            float radius, float lifetime, BattleshipAnimalInstance targetShip,
            double power, double penetrationValue, Action<string> hitReport = null,
            BattleshipProjectile.Affect projectileAffect = BattleshipProjectile.Affect.AP, float blastRadius = 1f)
        {
            if (!ProjectilePenetration.IsValid(penetrationValue) ||
                double.IsNaN(power) || double.IsInfinity(power) || power <= 0)
                throw new ArgumentOutOfRangeException(nameof(power), "威力と貫通力には正の有限値が必要です。");
            target = targetShip;
            penetration = penetrationValue;
            affect = projectileAffect;
            explosionRadius = blastRadius;
            RemainingPower = power;
            collisionRadius = radius;
            report = hitReport;
            InitialPosition = position;
            InitialVelocity = velocity;
            Velocity = velocity;
            remainingSeconds = Mathf.Max(.01f, lifetime);
            transform.position = new Vector3(position.x, position.y, -.1f);
            var line = GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 24;
            line.startWidth = line.endWidth = Mathf.Max(.1f, radius * .25f);
            line.startColor = line.endColor = Color.white;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 1f;
            trail.minVertexDistance = Mathf.Max(.1f, radius * .1f);
            trail.startWidth = radius;
            trail.endWidth = 0f;
            trail.startColor = trail.endColor = Color.cyan;
        }

        private void Update()
        {
            SimulateStep(Time.deltaTime);
        }

        internal void SimulateStep(float seconds)
        {
            if (seconds <= 0 || remainingSeconds <= 0) return;
            float step = Mathf.Min(seconds, remainingSeconds);
            Vector2 start = transform.position;
            float distance = Velocity.magnitude * step;
            Vector2 direction = Velocity.normalized;
            if (target != null && target.IsAlive && distance > 0)
            {
                // Test sections are solid static colliders, so trigger-query settings cannot hide them.
                Physics2D.SyncTransforms();
                var filter = new ContactFilter2D().NoFilter();
                filter.useTriggers = true;
                Physics2D.CircleCast(start, collisionRadius, direction, filter, contacts, distance);
                contacts.Sort((a, b) =>
                {
                    int order = a.distance.CompareTo(b.distance);
                    if (order != 0) return order;
                    int aId = a.collider.GetComponent<BattleshipSection>()?.SectionId ?? int.MaxValue;
                    int bId = b.collider.GetComponent<BattleshipSection>()?.SectionId ?? int.MaxValue;
                    return aId.CompareTo(bId);
                });
                foreach (var hit in contacts)
                {
                    if (!target.IsAlive) break;
                    var section = hit.collider.GetComponent<BattleshipSection>();
                    if (section == null || section.Owner != target || !section.IsAttached ||
                        !hitSections.Add(section)) continue;
                    if (affect == BattleshipProjectile.Affect.HE)
                    {
                        ExplosionEffect.Detonate(hit.point, explosionRadius, RemainingPower, penetration,
                            null, default, GetComponent<LineRenderer>().sharedMaterial, target);
                        report?.Invoke($"HE着弾 / 爆発半径 {explosionRadius:0.##} / 威力 {RemainingPower:0.##}");
                        RemainingPower = 0;
                        remainingSeconds = 0;
                        transform.position = new Vector3(hit.centroid.x, hit.centroid.y, transform.position.z);
                        Destroy(gameObject);
                        return;
                    }
                    double beforePower = RemainingPower, beforeHealth = section.CurrentHealth, beforePenetration = penetration;
                    RemainingPower = section.HitProjectile(RemainingPower, ref penetration, hit.point, direction, hit.normal,
                        out Vector2 outgoingDirection);
                    bool stopped = RemainingPower <= 1e-9;
                    string thickness = double.IsPositiveInfinity(section.LastEffectiveArmor) ? "∞" : section.LastEffectiveArmor.ToString("0.##");
                    report?.Invoke($"区画{section.SectionId + 1} ({section.Role}) / 入射角 {section.LastImpactAngle:0.##}° / 実装甲厚 {section.Armor:0.##} / 実効装甲厚 {thickness} / HP {beforeHealth:0.##} → {section.CurrentHealth:0.##} / 威力 {beforePower:0.##} → {RemainingPower:0.##} / 貫通力 {beforePenetration:0.##} → {penetration:0.##} / {(stopped ? "停止" : "通過")}");
                    if (stopped)
                    {
                        // The cast centroid is the projectile centre at first contact.
                        transform.position = new Vector3(hit.centroid.x, hit.centroid.y, transform.position.z);
                        remainingSeconds = 0;
                        Destroy(gameObject);
                        return;
                    }
                    Vector2 nextDirection = outgoingDirection.normalized;
                    if ((nextDirection - direction).sqrMagnitude > .00000001f)
                    {
                        float speed = Velocity.magnitude;
                        float travelledSeconds = Mathf.Clamp(hit.distance / speed, 0f, step);
                        Velocity = nextDirection * speed;
                        transform.position = new Vector3(hit.centroid.x, hit.centroid.y, transform.position.z);
                        remainingSeconds -= travelledSeconds;
                        // Discard contacts along the old heading and sweep the remaining time anew.
                        SimulateStep(step - travelledSeconds);
                        return;
                    }
                }
            }
            transform.position += (Vector3)(Velocity * step);
            remainingSeconds -= step;
            if (remainingSeconds <= 0f) Destroy(gameObject);
        }
    }
}
