using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aquarium.Tiling
{
    public enum BattleshipAnimalClass
    {
        [InspectorName("フリゲート艦")]
        Frigate,
        [InspectorName("駆逐艦")]
        Destroyer,
        [InspectorName("巡洋艦")]
        Cruiser,
        [InspectorName("戦艦")]
        Battleship
    }

    /// <summary>区画、資源、移動、対象選択、砲撃をまとめる戦艦型動物の実行時本体。</summary>
    [DefaultExecutionOrder(20)]
    public sealed class BattleshipAnimalInstance : MonoBehaviour
    {
        private const float FleeDistance = 1500f;
        private static readonly List<BattleshipAnimalInstance> active = new List<BattleshipAnimalInstance>();

        [SerializeField] private BattleshipAnimalClass shipClass;
        [SerializeField] private int requestedSections;
        [SerializeField] private int actualSections;
        [SerializeField] private int generationSeed;
        [SerializeField] private double currentResource;
        [SerializeField] private double resourceLimit;
        [SerializeField] private double currentSp;
        [SerializeField] private double spLimit;
        [SerializeField] private double coreHealth;
        [SerializeField] private double coreHealthLimit;
        [SerializeField] private float ageSeconds;

        private readonly List<BattleshipSection> sections = new List<BattleshipSection>();
        private BattleshipAnimalManager manager;
        private object attackTarget;
        private Vector2 observedVelocity;
        private Vector2 previousPosition;
        private bool dying;
        private float nextTargetScan;

        public static IReadOnlyList<BattleshipAnimalInstance> Active => active;
        public BattleshipAnimalClass ShipClass => shipClass;
        public int RequestedSections => requestedSections;
        public int ActualSections => actualSections;
        public int GenerationSeed => generationSeed;
        public int LivingSectionCount => sections.Count(x => x != null && x.IsAttached);
        public double CurrentResource => currentResource;
        public double ResourceLimit => resourceLimit;
        public double CurrentSp => currentSp;
        public double SpLimit => spLimit;
        public double CoreHealth => coreHealth;
        public double CoreHealthLimit => coreHealthLimit;
        public bool IsAlive => isActiveAndEnabled && !dying && coreHealth > 0 && LivingSectionCount > 0;
        public Vector2 ObservedVelocity => observedVelocity;
        internal object AttackTarget => ValidTarget(attackTarget) ? attackTarget : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();

        private void OnEnable()
        {
            previousPosition = transform.position;
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        internal void Initialize(BattleshipAnimalManager owner, BattleshipAnimalClass value,
            int requested, int actual, int seed, IReadOnlyList<BattleshipSection> createdSections)
        {
            manager = owner;
            shipClass = value;
            requestedSections = requested;
            actualSections = actual;
            generationSeed = seed;
            sections.Clear();
            sections.AddRange(createdSections);
            resourceLimit = spLimit = actual * 100d;
            currentResource = actual * 30d;
            currentSp = actual * 50d;
            coreHealthLimit = coreHealth = sections.Sum(x => x.MaximumHealth);
            ageSeconds = 0;
            previousPosition = transform.position;
        }

        private void Update()
        {
            if (!IsAlive || Time.deltaTime <= 0) return;
            float dt = Time.deltaTime;
            ageSeconds += dt;
            AdvanceResources(dt);
            if (!IsAlive) return;

            if (Time.time >= nextTargetScan)
            {
                nextTargetScan = Time.time + 0.2f;
                SelectAttackTarget();
            }
            Move(dt);
            TickTurrets(dt);
            manager?.TryReproduce(this);
        }

        private void LateUpdate()
        {
            if (Time.deltaTime > 0)
                observedVelocity = ((Vector2)transform.position - previousPosition) / Time.deltaTime;
            previousPosition = transform.position;
        }

        private void AdvanceResources(float seconds)
        {
            int count = LivingSectionCount;
            double maintenance = count * 0.2d;
            double aliveSeconds = maintenance <= 0 ? seconds : Math.Min(seconds, currentResource / maintenance);
            double recovery = (currentResource - maintenance * aliveSeconds * 0.5d) * aliveSeconds *
                AnimalResources.RecoveryPerCarbonPerSecond;
            foreach (BattleshipSection section in sections)
                if (section != null && section.IsAttached) section.Recover(recovery);
            currentSp = Math.Min(spLimit, currentSp + recovery);
            currentResource = Math.Max(0, currentResource - maintenance * seconds);
            if (currentResource <= 1e-9) Die();
        }

        private void SelectAttackTarget()
        {
            if (shipClass == BattleshipAnimalClass.Cruiser)
            {
                BattleshipAnimalInstance friend = active
                    .Where(x => x != this && x.IsAlive && x.shipClass == shipClass)
                    .OrderBy(x => ((Vector2)(x.transform.position - transform.position)).sqrMagnitude)
                    .FirstOrDefault();
                attackTarget = friend?.AttackTarget;
                return;
            }

            var candidates = new List<object>();
            candidates.AddRange(active.Where(x => x != this && x.IsAlive && x.shipClass < shipClass));
            candidates.AddRange(AnimalBody.Active.Where(x => x != null && x.IsAlive && (x.Species == null || !x.Species.isShip)));
            if (candidates.Count == 0) { attackTarget = null; return; }

            switch (shipClass)
            {
                case BattleshipAnimalClass.Frigate:
                    attackTarget = candidates.OrderBy(CurrentHealth).ThenBy(DistanceSquared).First();
                    break;
                case BattleshipAnimalClass.Destroyer:
                    attackTarget = candidates.OrderBy(DistanceSquared).First();
                    break;
                default:
                    var ships = candidates.OfType<BattleshipAnimalInstance>().ToList();
                    attackTarget = ships.Count > 0
                        ? ships.OrderByDescending(x => x.shipClass).ThenByDescending(x => x.coreHealth).First()
                        : candidates.OrderByDescending(CurrentHealth).First();
                    break;
            }
        }

        private void Move(float seconds)
        {
            Vector2 position = transform.position;
            Vector2 desired;
            float half = GrassField.FieldSize * 0.5f;
            const float edgeMargin = 200f;
            if (Mathf.Abs(position.x) >= half - edgeMargin || Mathf.Abs(position.y) >= half - edgeMargin)
                desired = -position;
            else
            {
                BattleshipAnimalInstance threat = active
                    .Where(x => x != this && x.IsAlive && x.shipClass >= shipClass &&
                        ((Vector2)(x.transform.position - transform.position)).sqrMagnitude <= FleeDistance * FleeDistance)
                    .OrderBy(x => ((Vector2)(x.transform.position - transform.position)).sqrMagnitude)
                    .FirstOrDefault();
                if (threat != null) desired = position - (Vector2)threat.transform.position;
                else if (AttackTarget != null)
                {
                    Vector2 toward = TargetPosition(AttackTarget) - position;
                    desired = shipClass <= BattleshipAnimalClass.Destroyer
                        ? toward
                        : new Vector2(toward.y, -toward.x);
                }
                else desired = -position;
            }

            float speed = SpeedFor(shipClass);
            if (desired.sqrMagnitude <= 1f)
            {
                if (AttackTarget == null && position.sqrMagnitude <= 1f) return;
                desired = transform.up;
            }
            float desiredAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg - 90f;
            float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, desiredAngle, TurnSpeedFor(shipClass) * seconds);
            transform.rotation = Quaternion.Euler(0, 0, angle);
            Vector2 next = position + (Vector2)transform.up * (speed * seconds);
            transform.position = new Vector3(Mathf.Clamp(next.x, -half, half - 0.001f),
                Mathf.Clamp(next.y, -half, half - 0.001f), transform.position.z);
        }

        private void TickTurrets(float seconds)
        {
            foreach (BattleshipSection section in sections)
                if (section != null && section.IsAttached) section.TickWeapon(seconds);
        }

        internal bool TrySpendSp(double amount)
        {
            if (amount < 0 || currentSp + 1e-9 < amount) return false;
            currentSp = Math.Max(0, currentSp - amount);
            return true;
        }

        internal bool TryPayBirth(double resourceAmount, double spAmount)
        {
            if (!IsAlive || resourceAmount < 0 || spAmount < 0 ||
                currentResource + 1e-9 < resourceAmount || currentSp + 1e-9 < spAmount) return false;
            currentResource = Math.Max(0, currentResource - resourceAmount);
            currentSp = Math.Max(0, currentSp - spAmount);
            return true;
        }

        internal void ReceiveHit(BattleshipSection hitSection, double power)
        {
            if (!IsAlive || hitSection == null || !hitSection.IsAttached || power <= 0) return;
            if (hitSection.Role == BattleshipSectionRole.Core)
            {
                coreHealth -= power;
                var living = sections.Where(x => x != null && x.IsAttached).ToList();
                double share = living.Count == 0 ? 0 : power / living.Count;
                foreach (BattleshipSection section in living) section.ApplyDamage(share);
            }
            else
            {
                double damage = Math.Max(0, power - hitSection.Armor);
                hitSection.ApplyDamage(damage);
                coreHealth -= damage;
            }
            ProcessDestroyedSections();
        }

        private void ProcessDestroyedSections()
        {
            foreach (BattleshipSection section in sections.OrderBy(x => x.SectionId).ToList())
                if (section.IsAttached && section.CurrentHealth <= 0) Detach(section, false);
            if (sections.Count == 0 || !sections[0].IsAttached) { Die(); return; }

            var connected = new HashSet<BattleshipSection>();
            var queue = new Queue<BattleshipSection>();
            connected.Add(sections[0]); queue.Enqueue(sections[0]);
            while (queue.Count > 0)
            {
                BattleshipSection current = queue.Dequeue();
                foreach (BattleshipSection neighbor in current.Neighbors)
                    if (neighbor != null && neighbor.IsAttached && connected.Add(neighbor)) queue.Enqueue(neighbor);
            }
            foreach (BattleshipSection section in sections.OrderBy(x => x.SectionId))
                if (section.IsAttached && !connected.Contains(section)) Detach(section, true);
            if (coreHealth <= 0 || !sections[0].IsAttached) Die();
        }

        private void Detach(BattleshipSection section, bool disconnected)
        {
            int before = LivingSectionCount;
            if (before <= 0 || !section.IsAttached) return;
            if (disconnected) coreHealth -= Math.Max(0, section.CurrentHealth);
            section.Detach();
            if (section.Role != BattleshipSectionRole.Core)
            {
                double ratio = (before - 1d) / before;
                currentResource *= ratio; resourceLimit *= ratio;
                currentSp *= ratio; spLimit *= ratio;
            }
            ScatterSectionCarbon(section.transform.position);
        }

        internal double AbsorbCarbon(DropCarbon drop)
        {
            if (!IsAlive || drop == null) return 0;
            double accepted = drop.TakeCarbon(SpeciesId, Math.Max(0, resourceLimit - currentResource));
            currentResource += accepted;
            return accepted;
        }

        internal bool CanReproduce => IsAlive && ageSeconds >= 60f && resourceLimit > 0 && spLimit > 0 &&
            currentResource >= resourceLimit * 0.8d && currentSp >= spLimit * 0.8d;

        internal void ResetReproductionAge() => ageSeconds = 0;
        internal Vector2 CurrentTargetPosition => AttackTarget == null
            ? (Vector2)transform.position + (Vector2)transform.up * 1000f
            : TargetPosition(AttackTarget);
        internal bool MayHit(AnimalBody body) => body != null && body.IsAlive && (body.Species == null || !body.Species.isShip);

        private void Die()
        {
            if (dying) return;
            dying = true;
            foreach (BattleshipSection section in sections.OrderBy(x => x.SectionId))
            {
                if (section == null || !section.IsAttached) continue;
                section.Detach();
                ScatterSectionCarbon(section.transform.position);
            }
            gameObject.SetActive(false);
            manager?.NotifyDeath(this);
            Destroy(gameObject);
        }

        private void ScatterSectionCarbon(Vector3 position) =>
            DropCarbon.Scatter(position, 100d, manager == null ? null : manager.SectionMaterial, SpeciesId);

        private string SpeciesId => "battleship-" + shipClass.ToString().ToLowerInvariant();

        private bool ValidTarget(object target) => target switch
        {
            BattleshipAnimalInstance ship => ship != null && ship.IsAlive,
            AnimalBody body => body != null && body.IsAlive,
            _ => false
        };

        private Vector2 TargetPosition(object target) => target switch
        {
            BattleshipAnimalInstance ship => ship.transform.position,
            AnimalBody body => body.transform.position,
            _ => transform.position
        };

        private double CurrentHealth(object target) => target switch
        {
            BattleshipAnimalInstance ship => ship.coreHealth,
            AnimalBody body => body.Resources.Health,
            _ => 0
        };

        private float DistanceSquared(object target) => (TargetPosition(target) - (Vector2)transform.position).sqrMagnitude;

        private static float SpeedFor(BattleshipAnimalClass value) => value switch
        {
            BattleshipAnimalClass.Frigate => 100f,
            BattleshipAnimalClass.Destroyer => 80f,
            BattleshipAnimalClass.Cruiser => 50f,
            _ => 30f
        };

        private static float TurnSpeedFor(BattleshipAnimalClass value) => value switch
        {
            BattleshipAnimalClass.Frigate => 120f,
            BattleshipAnimalClass.Destroyer => 90f,
            BattleshipAnimalClass.Cruiser => 60f,
            _ => 30f
        };
    }
}
