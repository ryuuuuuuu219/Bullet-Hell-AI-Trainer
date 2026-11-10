using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Script
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
        // Shared center-to-center range for threat detection and attack acquisition.
        public const float EncounterDistance = 1500f;
        private static readonly List<BattleshipAnimalInstance> active = new List<BattleshipAnimalInstance>();

        [SerializeField] private BattleshipAnimalClass shipClass;
        [SerializeField] private int requestedSections;
        [SerializeField] private int actualSections;
        [SerializeField] private int generationSeed;
        [SerializeField] private double coreHealth;
        [SerializeField] private double coreHealthLimit;

        private readonly List<BattleshipSection> sections = new List<BattleshipSection>();
        private object attackTarget;
        private Vector2 observedVelocity;
        private Vector2 previousPosition;
        private bool dying;
        private bool stationaryTestTarget;
        private float nextTargetScan;

        public static IReadOnlyList<BattleshipAnimalInstance> Active => active;
        public BattleshipAnimalClass ShipClass => shipClass;
        public int RequestedSections => requestedSections;
        public int ActualSections => actualSections;
        public int GenerationSeed => generationSeed;
        public int LivingSectionCount => sections.Count(x => x != null && x.IsAttached);
        public double CoreHealth => coreHealth;
        public double CoreHealthLimit => coreHealthLimit;
        public bool IsAlive => isActiveAndEnabled && !dying && coreHealth > 0 && LivingSectionCount > 0;
        public Vector2 ObservedVelocity => observedVelocity;
        internal object AttackTarget => ValidTarget(attackTarget) && WithinEncounterRange(attackTarget) ? attackTarget : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();

        private void OnEnable()
        {
            previousPosition = transform.position;
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        internal void Initialize(BattleshipAnimalClass value,
            int requested, int actual, int seed, IReadOnlyList<BattleshipSection> createdSections,
            bool stationary = false)
        {
            stationaryTestTarget = stationary;
            shipClass = value;
            requestedSections = requested;
            actualSections = actual;
            generationSeed = seed;
            sections.Clear();
            sections.AddRange(createdSections);
            coreHealthLimit = coreHealth = sections.Sum(x => x.MaximumHealth);
            previousPosition = transform.position;
        }

        private void Update()
        {
            if (stationaryTestTarget || !IsAlive || Time.deltaTime <= 0) return;
            float dt = Time.deltaTime;
            if (!IsAlive) return;

            if (Time.time >= nextTargetScan)
            {
                nextTargetScan = Time.time + 0.2f;
                SelectAttackTarget();
            }
            Move(dt);
            TickTurrets(dt);
        }

        private void LateUpdate()
        {
            if (Time.deltaTime > 0)
                observedVelocity = ((Vector2)transform.position - previousPosition) / Time.deltaTime;
            previousPosition = transform.position;
        }

        public enum TargetSelectionMode
        {
            Closest,
            Farthest,
            Covered,
        }
        public TargetSelectionMode targetSelectionMode = TargetSelectionMode.Closest;

        private void SelectAttackTarget()
        {
            attackTarget = null;
            if (!TryGetComponent(out IFF iff)) return;
            double best = targetSelectionMode == TargetSelectionMode.Farthest
                ? double.NegativeInfinity : double.PositiveInfinity;
            var candidates = iff.IsFriend ? ObjectManager.EnemyObjects : ObjectManager.AllyObjects;
            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate == gameObject || !candidate.activeInHierarchy ||
                    !candidate.TryGetComponent(out IFF other) || other.IsFriend == iff.IsFriend) continue;
                object target = candidate.GetComponent<BattleshipAnimalInstance>();
                if (target == null) target = candidate.GetComponent<Player>();
                if (!ValidTarget(target) || !WithinEncounterRange(target)) continue;
                double score = targetSelectionMode == TargetSelectionMode.Covered
                    ? CurrentHealth(target) : DistanceSquared(target);
                if (targetSelectionMode == TargetSelectionMode.Farthest ? score > best : score < best)
                {
                    best = score;
                    attackTarget = target;
                }
            }
        }
        public float half = 10f;

        private void Move(float seconds)
        {
            Vector2 position = transform.position;
            Vector2 desired;
            const float edgeMargin = 200f;
            if (Mathf.Abs(position.x) >= half - edgeMargin || Mathf.Abs(position.y) >= half - edgeMargin)
                desired = -position;
            else
            {
                BattleshipAnimalInstance threat = active
                    .Where(x => x != this && x.IsAlive && x.shipClass >= shipClass &&
                        ((Vector2)(x.transform.position - transform.position)).sqrMagnitude <= EncounterDistance * EncounterDistance)
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

        internal void ReceiveHit(BattleshipSection hitSection, double power)
            => ReceiveHitWithArmor(hitSection, power, hitSection != null ? hitSection.Armor : 0d);

        internal void ReceiveHitWithArmor(BattleshipSection hitSection, double power, double effectiveArmor)
        {
            if (!IsAlive || hitSection == null || !hitSection.IsAttached || power <= 0) return;
            if (hitSection.Role == BattleshipSectionRole.Core)
            {

                var living = sections.Where(x => x != null && x.IsAttached).ToList();
                double share = living.Count == 0 ? 0 : power / living.Count;
                foreach (BattleshipSection section in living)
                {
                    double before = section.CurrentHealth;
                    section.ApplyDamage(share);
                    coreHealth -= before - section.CurrentHealth;
                }
            }
            else
            {
                double damage = Math.Min(hitSection.CurrentHealth, Math.Max(0, power - effectiveArmor));
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
        }
        
        internal Vector2 CurrentTargetPosition => AttackTarget == null
            ? (Vector2)transform.position + (Vector2)transform.up * 1000f
            : TargetPosition(AttackTarget);

        private void Die()
        {
            if (dying) return;
            dying = true;
            foreach (BattleshipSection section in sections.OrderBy(x => x.SectionId))
            {
                if (section == null || !section.IsAttached) continue;
                section.Detach();
            }
            // Keep the test target and its final HP available for inspection/reset.
            if (stationaryTestTarget) return;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }


        private string Id => "battleship-" + shipClass.ToString().ToLowerInvariant();

        private bool ValidTarget(object target) => target switch
        {
            BattleshipAnimalInstance ship => ship != null && ship.IsAlive,
            Player body => body != null && body.isActiveAndEnabled && body.Health > 0,
            _ => false
        };

        private Vector2 TargetPosition(object target) => target switch
        {
            BattleshipAnimalInstance ship => ship.transform.position,
            Player body => body.transform.position,
            _ => transform.position
        };

        private double CurrentHealth(object target) => target switch
        {
            BattleshipAnimalInstance ship => ship.coreHealth,
            Player body => body.Health,
            _ => 0
        };

        private bool WithinEncounterRange(object target) => DistanceSquared(target) <= EncounterDistance * EncounterDistance;

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
