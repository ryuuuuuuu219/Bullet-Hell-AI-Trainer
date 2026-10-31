using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aquarium.Tiling
{
    public enum BattleshipSectionRole { Core, Armor, Turret }
    internal enum BattleshipWeaponKind { SmallGun, HeavyGun, AntiShipMissile, MachineGun }

    internal sealed class BattleshipWeaponRuntime
    {
        public BattleshipWeaponKind Kind;
        public int Grade;
        public float Power;
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

        public int SectionId { get; private set; }
        public BattleshipSectionRole Role { get; private set; }
        public double CurrentHealth { get; private set; }
        public double MaximumHealth { get; private set; } = 100d;
        public double Armor => Role == BattleshipSectionRole.Armor ? 25d : 0d;
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
        }

        internal void AddNeighbor(BattleshipSection neighbor)
        {
            if (neighbor != null && neighbor != this && !neighbors.Contains(neighbor)) neighbors.Add(neighbor);
        }

        internal void ApplyDamage(double amount)
        {
            if (!IsAttached || amount <= 0) return;
            CurrentHealth = Math.Max(0, CurrentHealth - amount);
        }

        internal void Recover(double amount)
        {
            if (!IsAttached || amount <= 0) return;
            CurrentHealth = Math.Min(MaximumHealth, CurrentHealth + amount);
        }

        internal double Hit(double power)
        {
            if (!IsAttached || owner == null || power <= 0) return power;
            double previousHealth = CurrentHealth;
            owner.ReceiveHit(this, power);
            return Math.Max(0, power - previousHealth);
        }

        internal void TickWeapon(float seconds)
        {
            if (weapon == null || owner == null || !owner.IsAlive) return;
            weapon.Timer += seconds;
            while (weapon.Timer >= weapon.Interval)
            {
                weapon.Timer -= weapon.Interval;
                if (!owner.TrySpendSp(weapon.SpCost)) continue;
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
            if (sectionCollider != null) sectionCollider.enabled = false;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!IsAttached || owner == null) return;
            DropCarbon drop = other.GetComponent<DropCarbon>();
            if (drop != null) owner.AbsorbCarbon(drop);
        }
    }
}
