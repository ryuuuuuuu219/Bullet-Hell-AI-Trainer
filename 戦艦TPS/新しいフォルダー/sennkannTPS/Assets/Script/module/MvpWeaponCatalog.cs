using System;
using System.Collections.Generic;

namespace Script
{
    [Flags]
    public enum WeaponTargetCategory { None = 0, Ship = 1, Aircraft = 2, Missile = 4 }

    // Immutable confirmed catalogue values. LocalId preserves existing build saves;
    // it is NOT a final inventory/reward catalogue identifier.
    public sealed class MvpWeaponDefinition
    {
        public int LocalId { get; }
        public string Name { get; }
        public float Power { get; }
        public double Penetration { get; }
        public float Speed { get; }
        public float Range { get; }
        public float ReloadSeconds { get; }
        public int ShotCount => 1;
        public float Heat { get; }
        public float Em { get; }
        public float CoreMultiplier { get; }
        public float BaseHitCoefficient => 1;
        public float? BaseSpreadDegrees => null; // Unspecified by the current catalogue.
        public WeaponTargetCategory? TargetCategories => null;
        public bool IsMissile => LocalId == 2;
        public float MissileTurnSpeed => IsMissile ? 35 : 0;
        public float NavigationConstant => IsMissile ? 2.8f : 0;
        public float? DistanceLifetime => IsMissile ? 10000f : (float?)null;
        // Potential blast radius; the projectile affect determines whether it explodes.
        public double ExplosionRadius => Power * 1.5d;

        internal MvpWeaponDefinition(int id, string name, float power, double penetration,
            float speed, float range, float reload, float heat, float em, float coreMultiplier)
        {
            LocalId = id; Name = name; Power = power; Penetration = penetration;
            Speed = speed; Range = range; ReloadSeconds = reload;
            Heat = heat; Em = em; CoreMultiplier = coreMultiplier;
        }
    }

    public static class MvpWeaponCatalog
    {
        private static readonly IReadOnlyList<MvpWeaponDefinition> entries = Array.AsReadOnly(new[] {
            new MvpWeaponDefinition(0, "小口径砲", 20, 2, 300, 5000, 2, 5, 0, 1.1f),
            new MvpWeaponDefinition(1, "大口径砲", 50, 5, 450, 8000, 3.5f, 12, 0, 1.2f),
            new MvpWeaponDefinition(2, "対艦ミサイル", 200, 3, 300, 10000, 5, 10, 1, 1.2f),
            new MvpWeaponDefinition(3, "機関砲", 5, .1, 500, 2000, .4f, .2f, 0, 1)
        });
        public static IReadOnlyList<MvpWeaponDefinition> Entries => entries;
        public static bool Contains(int localId) => localId >= 0 && localId < entries.Count;
        public static MvpWeaponDefinition Get(int localId)
            => Contains(localId) ? entries[localId] : throw new ArgumentOutOfRangeException(nameof(localId));

        public static float ComposeSpread(float baseFullAngle, int sameWeaponTurretCount)
        {
            if (float.IsNaN(baseFullAngle) || float.IsInfinity(baseFullAngle) || baseFullAngle < 0 || sameWeaponTurretCount < 0)
                throw new ArgumentOutOfRangeException(nameof(baseFullAngle));
            return baseFullAngle + sameWeaponTurretCount * .4f;
        }

        internal static BattleshipWeaponRuntime CreateRuntime(int localId, int seed, int sameWeaponTurretCount)
        {
            var data = Get(localId);
            return new BattleshipWeaponRuntime {
                Definition = data, Kind = (BattleshipWeaponKind)localId, Grade = 1,
                Power = data.Power, Penetration = data.Penetration, Speed = data.Speed,
                Interval = data.ReloadSeconds, Timer = data.ReloadSeconds,
                GuidanceTurnSpeed = data.MissileTurnSpeed, NavigationConstant = data.NavigationConstant,
                Random = new Random(seed),
                Affect = data.IsMissile ? BattleshipProjectile.Affect.HE : BattleshipProjectile.Affect.AP,
                ExplosionRadius = data.ExplosionRadius,
                // Explicit test defaults, not a decision about the missing catalogue values.
                TargetCategories = data.TargetCategories ?? WeaponTargetCategory.Ship,
                Spread = ComposeSpread(data.BaseSpreadDegrees ?? 0, sameWeaponTurretCount),
                Effects = new ProjectileEffectSettings { heat = data.Heat, em = data.Em, basicHitCoefficient = data.BaseHitCoefficient }
            };
        }
    }
}
