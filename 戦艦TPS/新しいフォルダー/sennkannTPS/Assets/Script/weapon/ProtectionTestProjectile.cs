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
        private ProjectileFlight flight;
        private ProjectileEffectSettings effects;
        private float beamElapsed, beamAccumulator;
        public double Penetration => penetration;
        private BattleshipInstance target;
        private BattleshipInstance sourceOwner;
        private Action<string> report;
        public double RemainingPower { get; private set; }

        public void Initialize(Material material, Vector2 position, Vector2 velocity,
            float radius, float lifetime, BattleshipInstance targetShip,
            double power, double penetrationValue, Action<string> hitReport = null,
            BattleshipProjectile.Affect projectileAffect = BattleshipProjectile.Affect.AP, float blastRadius = 1f, ProjectileEffectSettings effectSettings = null,
            BattleshipInstance source = null)
        {
            if (!ProjectilePenetration.IsValid(penetrationValue) ||
                double.IsNaN(power) || double.IsInfinity(power) || power <= 0)
                throw new ArgumentOutOfRangeException(nameof(power), "威力と貫通力には正の有限値が必要です。");
            target = targetShip;
            sourceOwner = source;
            penetration = penetrationValue;
            affect = projectileAffect;
            explosionRadius = blastRadius;
            effects = (effectSettings ?? new ProjectileEffectSettings()).Snapshot();
            RemainingPower = power;
            collisionRadius = radius;
            report = hitReport;
            InitialPosition = position;
            InitialVelocity = velocity;
            Velocity = velocity;
            remainingSeconds = Mathf.Max(.01f, lifetime);
            if (affect == BattleshipProjectile.Affect.Photon) remainingSeconds = Mathf.Max(.001f, effects.beamDuration);
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
            trail.time = affect == BattleshipProjectile.Affect.Plasma ? .8f : 1f;
            trail.minVertexDistance = Mathf.Max(.1f, radius * .1f);
            trail.startWidth = radius;
            trail.endWidth = 0f;
            trail.startColor = trail.endColor = affect == BattleshipProjectile.Affect.Plasma ? new Color(.7f, 0f, 1f) : Color.cyan;
            flight = NewFlight(power, penetrationValue);
            if (affect == BattleshipProjectile.Affect.Photon || affect == BattleshipProjectile.Affect.Neutron) { trail.emitting = false; line.loop = false; line.useWorldSpace = true; line.positionCount = 2; line.SetPosition(0, position); line.SetPosition(1, position); }
            if (affect == BattleshipProjectile.Affect.Neutron) TraceNeutron();
        }

        private void TraceNeutron()
        {
            flight.Sweep(Mathf.Max(1f, effects.beamRange));
            RemainingPower = flight.Power;
            penetration = flight.Penetration;
            ShowBeam(flight);
        }
        private void Update()
        {
            SimulateStep(Time.deltaTime);
        }

        internal void SimulateStep(float seconds)
        {
            if (seconds <= 0 || remainingSeconds <= 0) return;
            float step = Mathf.Min(seconds, remainingSeconds);
            if (affect == BattleshipProjectile.Affect.Photon)
            {
                float durationStep = Mathf.Min(step, Mathf.Max(0f, effects.beamDuration - beamElapsed));
                beamElapsed += durationStep;
                beamAccumulator += durationStep;
                float interval = Mathf.Max(.001f, effects.photonInterval);
                while (beamAccumulator + .000001f >= interval)
                {
                    beamAccumulator = Mathf.Max(0f, beamAccumulator - interval);
                    var pulse = NewFlight(Mathf.Max(0, effects.photonDps) * interval, penetration);
                    pulse.Sweep(Mathf.Max(1, effects.beamRange));
                    ShowBeam(pulse);
                }
                if (beamElapsed >= effects.beamDuration)
                {
                    if (beamAccumulator > .000001f)
                    {
                        var pulse = NewFlight(Mathf.Max(0, effects.photonDps) * beamAccumulator, penetration);
                        pulse.Sweep(Mathf.Max(1, effects.beamRange)); ShowBeam(pulse);
                    }
                    remainingSeconds = 0; Destroy(gameObject); return;
                }
            }
            else
            {
                if (affect == BattleshipProjectile.Affect.Neutron)
                {
                    remainingSeconds -= step;
                    if (remainingSeconds <= 0) Destroy(gameObject);
                    return;
                }
                flight.Advance(step, Velocity.magnitude);
                Velocity = flight.Direction * Velocity.magnitude;
                transform.position = new Vector3(flight.Position.x, flight.Position.y, transform.position.z);
                RemainingPower = flight.Power; penetration = flight.Penetration;
                if (flight.Stopped) { remainingSeconds = 0; Destroy(gameObject); return; }
            }
            remainingSeconds -= step;
            if (remainingSeconds <= 0) Destroy(gameObject);
        }

        private ProjectileFlight NewFlight(double power, double p) => new ProjectileFlight {
            Position = InitialPosition, Direction = InitialVelocity.normalized, Power = power, Penetration = p,
            Affect = affect, Settings = effects, Radius = collisionRadius, BlastRadius = explosionRadius,
            OnlyTarget = target, Owner = sourceOwner, SourceClass = sourceOwner != null ? sourceOwner.ShipClass : default,
            Material = GetComponent<LineRenderer>().sharedMaterial, Report = report
        };

        private void ShowBeam(ProjectileFlight pulse)
        {
            var line = GetComponent<LineRenderer>();
            line.positionCount = pulse.Path.Count;
            line.SetPositions(pulse.Path.ToArray());
        }
    }
}
