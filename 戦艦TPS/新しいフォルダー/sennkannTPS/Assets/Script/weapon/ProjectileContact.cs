using UnityEngine;

namespace Script
{
    // CircleCast against these circles detects contact even when a frame crosses the whole bullet.
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class ProjectileContact : MonoBehaviour
    {
        internal ProjectileFlight Flight { get; private set; }
        private CircleCollider2D circle;

        internal void Initialize(ProjectileFlight flight)
        {
            Flight = flight;
            circle = GetComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = Mathf.Max(.001f, flight.Radius);
            circle.enabled = !ProjectileEffects.IsEnergy(flight.Affect);
            flight.Contact = this;
        }

        internal void StopContact() { if (circle != null) circle.enabled = false; }

        internal bool CanContact(ProjectileFlight other)
        {
            if (!isActiveAndEnabled || Flight == null || Flight == other || Flight.Stopped || other.Stopped ||
                ProjectileEffects.IsEnergy(Flight.Affect) || ProjectileEffects.IsEnergy(other.Affect)) return false;
            if (Flight.Owner == null || other.Owner == null || Flight.Owner == other.Owner) return false;
            return Flight.Owner.TryGetComponent(out IFF a) && other.Owner.TryGetComponent(out IFF b) && a.IsFriend != b.IsFriend;
        }

        internal void Resolve(ProjectileFlight other)
        {
            if (!CanContact(other)) return;
            double a = Flight.Power, b = other.Power;
            Flight.Power = a - b;
            other.Power = b - a;
            // Both subtractions precede either blast; use each bullet's pre-interception power.
            if (Flight.Stopped) Flight.Finish(a);
            if (other.Stopped) other.Finish(b);
        }
    }
}
