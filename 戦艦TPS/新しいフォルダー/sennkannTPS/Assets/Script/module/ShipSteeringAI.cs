using System;
using UnityEngine;

namespace Script
{
    // Implementation-local names, not persistent stage/save IDs.
    public enum AIType { C1, C2, TOr, Obs, DF, AC, BS }

    [Serializable]
    public sealed class ShipSteeringSettings
    {
        public AIType major = AIType.C1;
        public AIType fallback = AIType.AC;
        public Transform attackTarget;
        public Transform escortTarget;
        public Transform fallbackTarget;
        public Vector2 orbitCenterPosition;
        [Tooltip("0 uses TOr=3000 / Obs=2000. DF requires a positive radius.")]
        public float orbitRadius;

        public ShipSteeringSettings Snapshot() => (ShipSteeringSettings)MemberwiseClone();

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(AIType), major) ||
                (fallback != AIType.AC && fallback != AIType.C1 && fallback != AIType.C2))
                throw new ArgumentException("Invalid steering AI or fallback (AC/C1/C2 only).");
            if (!Finite(orbitRadius) || orbitRadius < 0 || (major == AIType.DF && orbitRadius <= 0) ||
                !Finite(orbitCenterPosition.x) || !Finite(orbitCenterPosition.y))
                throw new ArgumentException("Invalid orbit radius or center.");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // Runs after ship status updates (20), before the common movement motor (30).
    // Steering targets are explicit references, independent of the legacy shooting target scan.
    [DefaultExecutionOrder(25), RequireComponent(typeof(ShipMovement))]
    public sealed class ShipSteeringAI : MonoBehaviour
    {
        [SerializeField] private ShipSteeringSettings settings = new ShipSteeringSettings();
        public AIType ActiveType { get; private set; }
        public bool UsingFallback { get; private set; }
        public bool MissingTarget { get; private set; }
        public float HeldOrbitAngle { get; private set; }
        public int OrbitDirection { get; private set; }
        public float LosRate { get; private set; }
        public bool BroadsideTurning { get; private set; }
        public const float CenterEpsilon = .001f; // Provisional, see phase 04 implementation memo.
        private ShipMovement motor;
        private BattleshipInstance owner;
        private Transform previousTarget;
        private bool initialized, orbitInitialized, hasLos;
        private float orbitElapsed, previousLos;

        private void Awake()
        {
            motor = GetComponent<ShipMovement>();
            owner = GetComponent<BattleshipInstance>();
        }

        // Call again to explicitly change/reacquire a target. Loss transitions latch until reconfigured.
        public void Configure(ShipSteeringSettings value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            value.Validate();
            if (motor == null) motor = GetComponent<ShipMovement>();
            if (owner == null) owner = GetComponent<BattleshipInstance>();
            settings = value.Snapshot();
            ActiveType = settings.major;
            UsingFallback = MissingTarget = false;
            initialized = true;
            ResetGuidance();
            motor.playerControlled = false;
        }

        private void ResetGuidance()
        {
            previousTarget = null;
            orbitInitialized = hasLos = BroadsideTurning = false;
            HeldOrbitAngle = orbitElapsed = LosRate = 0;
            OrbitDirection = 0;
        }

        private void Update() => Tick(Time.deltaTime);
        private void OnDisable()
        {
            if (motor != null && !motor.playerControlled)
            { motor.TurnInput = 0; motor.SetThrottle(ShipThrottle.Stop); }
        }

        private static bool Alive(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            var ship = target.GetComponentInParent<BattleshipInstance>();
            return ship == null || ship.IsAlive;
        }

        public void Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (!initialized) Configure(settings);
            if (motor.playerControlled || (owner != null && !owner.IsAlive)) return;
            Transform target = UsingFallback ? settings.fallbackTarget :
                (ActiveType == AIType.DF ? settings.escortTarget : settings.attackTarget);
            if (ActiveType != AIType.AC && !Alive(target))
            {
                if (!UsingFallback)
                {
                    UsingFallback = true;
                    ActiveType = settings.fallback;
                    ResetGuidance();
                    target = settings.fallbackTarget;
                }
                if (ActiveType != AIType.AC && !Alive(target))
                {
                    // No implicit nearest-target selection: stage resolution belongs to a later phase.
                    MissingTarget = true;
                    motor.SetThrottle(ShipThrottle.Stop);
                    motor.TurnInput = 0;
                    return;
                }
            }
            MissingTarget = false;
            motor.TurnInput = 0;
            motor.SetThrottle(ActiveType == AIType.AC ? ShipThrottle.Stop :
                ActiveType == AIType.BS ? ShipThrottle.AheadSlow : ShipThrottle.AheadFull);
            if (ActiveType == AIType.AC) return; // Provisional: stationary hull also holds heading.
            if (previousTarget != target) { ResetGuidance(); previousTarget = target; }

            Vector2 position = transform.position;
            Vector2 center = ActiveType == AIType.Obs ? settings.orbitCenterPosition : (Vector2)target.position;
            Vector2 offset = center - position;
            float heading = transform.eulerAngles.z + 90;
            bool orbit = ActiveType == AIType.TOr || ActiveType == AIType.Obs || ActiveType == AIType.DF;
            if (orbit && orbitInitialized) orbitElapsed += seconds;
            if (offset.sqrMagnitude <= CenterEpsilon * CenterEpsilon)
            { hasLos = false; LosRate = 0; return; }
            float azimuth = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            float error = Mathf.DeltaAngle(heading, azimuth);
            if (orbit)
            {
                float radius = settings.orbitRadius > 0 ? settings.orbitRadius : ActiveType == AIType.TOr ? 3000 : 2000;
                if (!orbitInitialized || orbitElapsed >= 3)
                {
                    HeldOrbitAngle = SelectOrbitAngle(offset.magnitude, radius);
                    orbitElapsed = orbitInitialized ? orbitElapsed % 3 : 0;
                }
                if (!orbitInitialized)
                {
                    OrbitDirection = Mathf.Abs(Mathf.DeltaAngle(heading, azimuth - HeldOrbitAngle)) <=
                        Mathf.Abs(Mathf.DeltaAngle(heading, azimuth + HeldOrbitAngle)) ? 1 : -1;
                    orbitInitialized = true;
                }
                error = Mathf.DeltaAngle(heading, azimuth - OrbitDirection * HeldOrbitAngle);
                if (Mathf.Abs(error) > 10) TurnTo(error, seconds);
            }
            else if (ActiveType == AIType.C1) TurnTo(error, seconds);
            else if (ActiveType == AIType.C2)
            {
                LosRate = hasLos ? Mathf.DeltaAngle(previousLos, azimuth) / seconds : 0;
                previousLos = azimuth;
                hasLos = true;
                if (Mathf.Abs(error) >= 90) TurnTo(error, seconds);
                else motor.TurnInput = LosRate >= 5 ? 1 : LosRate <= -5 ? -1 : 0;
            }
            else if (ActiveType == AIType.BS)
            {
                float desired = azimuth + (error >= 0 ? -90 : 90);
                error = Mathf.DeltaAngle(heading, desired);
                if (Mathf.Abs(error) <= 8) BroadsideTurning = false;
                else if (Mathf.Abs(error) > 15) BroadsideTurning = true;
                if (BroadsideTurning) TurnTo(error, seconds);
            }
        }

        public static float SelectOrbitAngle(float distance, float radius)
            => distance < .92d * radius ? 105 : distance <= 1.08d * radius ? 90 : 75;

        private void TurnTo(float error, float seconds)
        {
            float limit = motor.EffectiveMaximumTurnSpeed * seconds;
            motor.TurnInput = limit > 0 ? Mathf.Clamp(error / limit, -1, 1) : 0;
        }
    }
}
