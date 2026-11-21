using System;
using UnityEngine;

namespace Script
{
    public static class FiringSolution
    {
        // Double precision and a stable quadratic root avoid cancellation near equal velocities.
        public static Vector2 Direction(Vector2 origin, Vector2 target, Vector2 sourceVelocity,
            Vector2 targetVelocity, float speed, out double time)
        {
            time = 0;
            Vector2 r = target - origin;
            if (float.IsNaN(speed) || speed <= 0) return Vector2.zero;
            if (float.IsPositiveInfinity(speed) || r.magnitude / speed <= .2f) return r.normalized;
            Vector2 v = targetVelocity - sourceVelocity;
            double a = (double)v.x * v.x + (double)v.y * v.y - (double)speed * speed;
            double b = 2 * ((double)r.x * v.x + (double)r.y * v.y);
            double c = (double)r.x * r.x + (double)r.y * r.y;
            double result = double.PositiveInfinity;
            if (Math.Abs(a) < 1e-10)
            { if (Math.Abs(b) > 1e-10 && -c / b > 0) result = -c / b; }
            else
            {
                double discriminant = b * b - 4 * a * c;
                if (discriminant >= 0)
                {
                    double root = Math.Sqrt(discriminant);
                    double q = -.5 * (b + (b >= 0 ? root : -root));
                    double first = q / a, second = q == 0 ? double.PositiveInfinity : c / q;
                    if (first > 0) result = first;
                    if (second > 0) result = Math.Min(result, second);
                }
            }
            if (double.IsInfinity(result) || double.IsNaN(result)) return r.normalized;
            time = result;
            double x = r.x + v.x * result, y = r.y + v.y * result;
            double length = Math.Sqrt(x * x + y * y);
            return length > 0 ? new Vector2((float)(x / length), (float)(y / length)) : r.normalized;
        }
    }

    [DefaultExecutionOrder(35)]
    public sealed class ShipFiringAI : MonoBehaviour
    {
        public Transform ProjectileRoot { get; set; }
        // Per firing ship, per section: turret / Basic / ERA / AEM / core.
        public float[] sectionWeights = { 1, 1, 1, 1, 1 };
        public WeaponTargetCategory[] weaponCategories = {
            WeaponTargetCategory.Ship, WeaponTargetCategory.Ship, WeaponTargetCategory.Ship, WeaponTargetCategory.Ship };
        private BattleshipInstance owner;
        private BattleshipSection[] mounts;
        private void Awake() { owner = GetComponent<BattleshipInstance>(); }
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || owner == null || !owner.IsAlive) return;
            if (mounts == null) mounts = GetComponentsInChildren<BattleshipSection>();
            foreach (var mount in mounts)
                if (mount != null) mount.TickAutomaticWeapon(seconds, this);
        }

        internal WeaponTarget FindTarget(Vector2 origin, float range, WeaponTargetCategory categories, WeaponTarget current)
        {
            if (Valid(current, origin, range, categories)) return current;
            WeaponTarget best = null;
            float nearest = float.PositiveInfinity;
            foreach (var target in WeaponTarget.Active)
            {
                if (!Valid(target, origin, range, categories)) continue;
                float distance = (target.Position - origin).sqrMagnitude;
                // Equal distance: stable instance order for this runtime, not a persistent ID rule.
                if (distance < nearest || (distance == nearest && (best == null || target.GetInstanceID() < best.GetInstanceID())))
                { nearest = distance; best = target; }
            }
            return best;
        }

        private bool Valid(WeaponTarget target, Vector2 origin, float range, WeaponTargetCategory categories)
            => target != null && target.Alive && target.Ship != owner && owner.TryGetComponent(out IFF iff) &&
                target.IsFriend != iff.IsFriend && (categories & target.category) != 0 &&
                (target.Position - origin).sqrMagnitude <= range * range;

        internal bool TryAim(WeaponTarget target, System.Random random, out Vector2 position)
        {
            position = target.Position;
            if (target.Ship == null) return true; // Representative point adapter for aircraft/missiles.
            var sections = target.Ship.GetComponentsInChildren<BattleshipSection>();
            double total = 0;
            foreach (var section in sections) total += Weight(section);
            if (total <= 0) return false; // Invalid/zero weights: hold fire; do not silently replace configuration.
            double sample = random.NextDouble() * total;
            foreach (var section in sections)
            {
                double weight = Weight(section);
                if (weight <= 0) continue;
                sample -= weight;
                if (sample < 0) { position = section.GetComponent<Renderer>().bounds.center; return true; }
            }
            return false;
        }

        private double Weight(BattleshipSection section)
        {
            if (!section.IsAttached || section.CurrentHealth <= 0 || sectionWeights == null || sectionWeights.Length != 5) return 0;
            int id = section.Role == BattleshipSectionRole.Core ? 4 : section.Role == BattleshipSectionRole.Turret ? 0 :
                section.Mode == ArmorMode.Basic ? 1 : section.Mode == ArmorMode.ERA ? 2 : 3;
            float value = sectionWeights[id];
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0 ? 0 : value;
        }
    }
}
