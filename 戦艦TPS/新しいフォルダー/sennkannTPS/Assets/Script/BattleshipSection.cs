using System;
using System.Collections.Generic;
using UnityEngine;

namespace Script
{
    public enum BattleshipSectionRole { Core, Armor, Turret }
    public enum ArmorMode { [InspectorName("ベーシック")] Basic, ERA }
    internal enum BattleshipWeaponKind { SmallGun, HeavyGun, AntiShipMissile, MachineGun }

    internal sealed class BattleshipWeaponRuntime
    {
        public BattleshipWeaponKind Kind;
        public int Grade;
        public float Power;
        public BattleshipProjectile.Affect Affect = BattleshipProjectile.Affect.AP;
        public double ExplosionRadius = 1d;
        public float Interval;
        public float Speed;
        public float Decay;
        public float Spread;
        public float SpCost;
        public float GuidanceTurnSpeed;
        public float NavigationConstant;
        public float Timer;
        public System.Random Random;
    }

    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class BattleshipSection : MonoBehaviour
    {
        private readonly List<BattleshipSection> neighbors = new List<BattleshipSection>();
        private BattleshipAnimalInstance owner;
        private BattleshipWeaponRuntime weapon;
        private Renderer sectionRenderer;
        private Collider2D sectionCollider;
        private MaterialPropertyBlock opacityProperties;
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        [SerializeField, Min(0f), InspectorName("実装甲厚")]
        private float armorThickness = 50f;
        [SerializeField, Min(0f), InspectorName("ERA印加ベクトルの絶対値")]
        private float eraVectorMagnitude = 50f;
        [SerializeField, Min(0f), InspectorName("装甲区画の迎撃力")]
        private double armorInterception = 0.1d;
        public double ArmorInterception { get => Math.Max(0d, armorInterception); set => armorInterception = Math.Max(0d, value); }
        public float ArmorThickness { get => armorThickness; set => armorThickness = Mathf.Max(0f, value); }
        public float EraVectorMagnitude { get => Mathf.Max(0f, eraVectorMagnitude); set => eraVectorMagnitude = Mathf.Max(0f, value); }
        [SerializeField, InspectorName("装甲モード")] private ArmorMode armorMode = ArmorMode.Basic;
        public ArmorMode Mode { get => armorMode; set => armorMode = value; }
        internal event Action<Vector2, double, double, double> HitResolved;

        public int SectionId { get; private set; }
        public BattleshipSectionRole Role { get; private set; }
        public double CurrentHealth { get; private set; }
        public double MaximumHealth { get; private set; } = 100d;
        public double Armor => Role == BattleshipSectionRole.Armor ? Math.Max(0, armorThickness) : 0d;
        public float LastImpactAngle { get; private set; }
        public double LastEffectiveArmor { get; private set; }
        public double Interception => Role == BattleshipSectionRole.Armor ? ArmorInterception : 0.0d;
        public bool IsAttached { get; private set; } = true;
        public BattleshipAnimalInstance Owner => owner;
        public IReadOnlyList<BattleshipSection> Neighbors => neighbors;

        internal void Initialize(BattleshipAnimalInstance value, int id, BattleshipSectionRole role,
            BattleshipWeaponRuntime assignedWeapon)
        {
            owner = value;
            SectionId = id;
            Role = role;
            weapon = assignedWeapon;
            CurrentHealth = MaximumHealth = 100d;
            IsAttached = true;
            sectionRenderer = GetComponent<Renderer>();
            sectionCollider = GetComponent<Collider2D>();
            if (sectionCollider != null) sectionCollider.isTrigger = true;
            UpdateOpacity();
        }

        internal void AddNeighbor(BattleshipSection neighbor)
        {
            if (neighbor != null && neighbor != this && !neighbors.Contains(neighbor)) neighbors.Add(neighbor);
        }

        internal void ApplyDamage(double amount)
        {
            if (!IsAttached || amount <= 0) return;
            CurrentHealth = Math.Max(0, CurrentHealth - amount);
            UpdateOpacity();
        }

        internal void Recover(double amount)
        {
            if (!IsAttached || amount <= 0) return;
            CurrentHealth = Math.Min(MaximumHealth, CurrentHealth + amount);
            UpdateOpacity();
        }

        private void UpdateOpacity()
        {
            if (opacityProperties == null) opacityProperties = new MaterialPropertyBlock();
            float opacity = MaximumHealth > 0 ? Mathf.Clamp01((float)(CurrentHealth / MaximumHealth)) : 0f;
            foreach (Renderer visual in GetComponentsInChildren<Renderer>())
            {
                visual.GetPropertyBlock(opacityProperties);
                opacityProperties.SetFloat(OpacityId, opacity);
                visual.SetPropertyBlock(opacityProperties);
            }
        }

        internal double Hit(double power, double penetration = 1.0)
            => HitAt(power, penetration, sectionRenderer != null ? (Vector2)sectionRenderer.bounds.center : (Vector2)transform.position);

        internal double HitAt(double power, double penetration, Vector2 position)
            => HitSurface(power, penetration, position, Vector2.right, Vector2.left, out _);

        internal double HitSurface(double power, double penetration, Vector2 position,
            Vector2 incomingDirection, Vector2 surfaceNormal, out Vector2 outgoingDirection)
            => HitProjectile(power, ref penetration, position, incomingDirection, surfaceNormal, out outgoingDirection);

        internal double HitProjectile(double power, ref double penetration, Vector2 position,
            Vector2 incomingDirection, Vector2 surfaceNormal, out Vector2 outgoingDirection)
        {
            // Change this output to apply deflection/reflection after impact.
            // Zero stops movement; a non-zero vector supplies the new heading (speed is preserved).
            outgoingDirection = incomingDirection;
            if (!IsAttached || owner == null || power <= 0) return power;
            if (Role == BattleshipSectionRole.Armor && armorMode == ArmorMode.ERA)
                outgoingDirection += surfaceNormal.normalized * EraVectorMagnitude;
            // Angle is measured from the face normal: 0 degrees is perpendicular impact.
            float cosine = outgoingDirection.sqrMagnitude > .000001f && surfaceNormal.sqrMagnitude > .000001f
                ? Mathf.Clamp01(Mathf.Abs(Vector2.Dot(outgoingDirection.normalized, surfaceNormal.normalized)))
                : 1f; // Legacy/non-geometric hits use perpendicular impact.
            LastImpactAngle = Mathf.Acos(cosine) * Mathf.Rad2Deg;
            LastEffectiveArmor = Armor <= 0 ? 0 : cosine <= .000001f ? double.PositiveInfinity : Armor / cosine;
            double before = CurrentHealth;
            // Thickness >= current power blocks the projectile, including equality.
            if (LastEffectiveArmor >= power)
            {
                outgoingDirection = Vector2.zero;
                HitResolved?.Invoke(position, CurrentHealth, 0d, MaximumHealth);
                return 0d;
            }
            double remaining = ProjectilePenetration.RemainingPower(power, penetration, Interception, before, out double afterHealth);
            // Armor is only a passage gate; apply the shared HP calculation without subtracting it again.
            double damage = Role == BattleshipSectionRole.Core
                ? Math.Max(0, power * (penetration - Interception))
                : before - afterHealth;
            owner.ReceiveHitWithArmor(this, damage, 0d);
            UpdateOpacity();
            HitResolved?.Invoke(position, CurrentHealth, before - CurrentHealth, MaximumHealth);
            penetration = Math.Max(0d, penetration - Interception);
            if (penetration <= 0d)
            {
                outgoingDirection = Vector2.zero;
                return 0d;
            }
            return remaining;
        }

        internal void TickWeapon(float seconds)
        {
            if (weapon == null || owner == null || !owner.IsAlive) return;
            weapon.Timer += seconds;
            while (weapon.Timer >= weapon.Interval)
            {
                weapon.Timer -= weapon.Interval;
                Vector2 direction = owner.CurrentTargetPosition - (Vector2)transform.position;
                if (direction.sqrMagnitude <= 0.000001f) direction = owner.transform.up;
                float scatter = weapon.Spread <= 0 ? 0 : ((float)weapon.Random.NextDouble() - 0.5f) * weapon.Spread;
                direction = Quaternion.Euler(0, 0, scatter) * direction.normalized;
                BattleshipProjectile.Fire(this, weapon, direction);
            }
        }

        internal void Detach()
        {
            if (!IsAttached) return;
            IsAttached = false;
            if (sectionRenderer != null) sectionRenderer.enabled = false;
            foreach (Renderer child in GetComponentsInChildren<Renderer>()) child.enabled = false;
            if (sectionCollider != null) sectionCollider.enabled = false;
        }
    }
}
