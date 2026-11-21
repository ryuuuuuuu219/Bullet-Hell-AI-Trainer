using System;
using UnityEngine;

namespace Script
{
    public static class CombatHitRules
    {
        public const double MvpAccuracyCorrection = 1;
        public static double Probability(double basicHit, double correction, double evasion)
        {
            if (double.IsNaN(basicHit) || double.IsInfinity(basicHit) || basicHit < 0 ||
                double.IsNaN(correction) || double.IsInfinity(correction) ||
                double.IsNaN(evasion) || double.IsInfinity(evasion)) return 0;
            correction = Math.Max(1, Math.Min(100, correction));
            evasion = Math.Max(0, Math.Min(.99999, evasion));
            return Math.Max(.0001, Math.Min(1, basicHit * correction * (1 - evasion)));
        }

        public static bool Roll(double basicHit, double correction, double evasion, double sample)
            => sample >= 0 && sample < 1 && sample < Probability(basicHit, correction, evasion);

        public static bool Resolve(WeaponTarget target, double basicHit, double correction, double sample, Action onHit)
        {
            if (target == null || !target.Alive || !Roll(basicHit, correction, target.Evasion, sample)) return false;
            onHit?.Invoke();
            return true;
        }

        internal static bool Eligible(BattleshipInstance target, BattleshipInstance owner,
            BattleshipClass legacyClass, BattleshipInstance onlyTarget)
        {
            if (target == null || !target.IsAlive) return false;
            if (onlyTarget != null) return target == onlyTarget; // Explicit protection-test target.
            if (target == owner) return false;
            if (owner != null && owner.HasConstructionData)
                return owner.TryGetComponent(out IFF sourceIff) && target.TryGetComponent(out IFF targetIff) &&
                    sourceIff.IsFriend != targetIff.IsFriend;
            return target.ShipClass < legacyClass; // Isolated legacy prototype behavior.
        }

        internal static bool Allows(BattleshipInstance target, BattleshipProjectile.Affect affect, ProjectileEffectSettings settings)
        {
            // Beam/particle evasion is undecided; preserve the existing effect path.
            if (ProjectileEffects.IsEnergy(affect)) return true;
            var receiver = target.GetComponent<WeaponTarget>();
            double evasion = receiver == null ? 0 : receiver.Evasion;
            double sample = settings?.hitSample != null ? settings.hitSample() : UnityEngine.Random.value;
            // Unity Random.value includes 1; use a half-open variate for the probability comparison.
            if (settings?.hitSample == null && sample >= 1) sample = .999999999999;
            return Roll(settings?.basicHitCoefficient ?? 1, MvpAccuracyCorrection, evasion, sample);
        }
    }
}
