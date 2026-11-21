using System;
using System.Linq;
using System.Reflection;
using Script;
using UnityEditor;
using UnityEngine;

public static class CombatHitChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    private static void Near(double value, double expected, string label, double tolerance = .000001)
        => Check(Math.Abs(value - expected) <= tolerance, $"{label}: {value} != {expected}");

    [MenuItem("戦艦TPS/検証/命中・回避（工程07）")]
    public static void Run()
    {
        Near(CombatHitRules.Probability(1,1,.3), .7, "H F E formula");
        Near(CombatHitRules.Probability(1,1,.99999), .0001, "probability floor");
        Near(CombatHitRules.Probability(1.5,100,.5), 1, "probability ceiling");
        Near(CombatHitRules.Probability(1,0,.5), .5, "F clamps to 1");
        Check(CombatHitRules.Roll(1,1,.5,.4999) && !CombatHitRules.Roll(1,1,.5,.5), "half-open probability boundary");
        Check(!CombatHitRules.Roll(double.NaN,1,0,0), "invalid H fails closed");
        var dummy = new GameObject("Nonship hit adapter");
        try
        {
            var adapter = dummy.AddComponent<WeaponTarget>();
            adapter.Evasion = 2; Near(adapter.Evasion, .99999, "E upper bound");
            foreach (var category in new[] { WeaponTargetCategory.Ship, WeaponTargetCategory.Aircraft, WeaponTargetCategory.Missile })
            {
                adapter.category = category; adapter.Evasion = .5f; int damageCalls = 0;
                Check(!CombatHitRules.Resolve(adapter,1,1,.9, () => damageCalls++), "adapter miss");
                Check(CombatHitRules.Resolve(adapter,1,1,.1, () => damageCalls++), "adapter hit");
                Check(damageCalls == 1, "only successful roll invokes effect");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(dummy); }
        if (Application.isPlaying) Runtime();
        Debug.Log("COMBAT_HIT_CHECKS_PASS: formula bounds, deterministic roll and all target categories.");
    }

    private static void Runtime()
    {
        BattleshipInstance source = null, target = null, friendly = null;
        var material = new Material(Shader.Find("Battleship/TilingTest"));
        var priorExplosions = UnityEngine.Object.FindObjectsByType<ExplosionEffect>(FindObjectsSortMode.None);
        try
        {
            var data = new ShipBuildData { referenceSections = 5, weaponCounts = new int[7] };
            data.armor[0].count = 5;
            source = ShipBuilder.Build(data, material, new Vector2(-15000,-15000), 0, true,false,true);
            target = ShipBuilder.Build(data, material, new Vector2(10000,-10000), 0, false,false,true);
            friendly = ShipBuilder.Build(data, material, new Vector2(10000,-10000), 0, true,false,true);
            var parts = target.GetComponentsInChildren<BattleshipSection>();
            var friendParts = friendly.GetComponentsInChildren<BattleshipSection>();
            foreach (var part in parts) part.ArmorThickness = 0;
            double[] health = parts.Select(p => p.CurrentHealth).ToArray();
            double[] friendHealth = friendParts.Select(p => p.CurrentHealth).ToArray();
            int rolls = 0;
            var settings = new ProjectileEffectSettings { heat = 5, em = 2, hitSample = () => { rolls++; return .99; } };
            target.GetComponent<WeaponTarget>().Evasion = .9f;
            var type = typeof(BattleshipProjectile).Assembly.GetType("Script.ProjectileFlight");
            object NewFlight(Vector2 position, BattleshipProjectile.Affect affect = BattleshipProjectile.Affect.AP)
            {
                var flight = Activator.CreateInstance(type, true);
                void Set(string name, object value) => type.GetField(name, Hidden).SetValue(flight, value);
                Set("Position", position); Set("Direction", Vector2.right); Set("Power", 50d); Set("Penetration", 2d);
                Set("Radius", 1f); Set("Affect", affect); Set("Settings", settings); Set("Owner", source);
                Set("SourceClass", source.ShipClass); Set("Material", material);
                return flight;
            }
            Vector2 origin = (Vector2)parts[0].GetComponent<Renderer>().bounds.center - Vector2.right * 2000;
            void Sweep(object flight, float distance) => type.GetMethod("Sweep", Hidden).Invoke(flight, new object[] {distance});
            var missed = NewFlight(origin);
            Sweep(missed, 10); Check(rolls == 0, "no physical contact means no roll");
            Sweep(missed, 4000);
            Check(rolls == 1, "direct projectile rolls once per target ship");
            Check(parts.Select(p => p.CurrentHealth).SequenceEqual(health), "miss changes no section HP");
            Near(target.HeatAccumulation,0,"miss no heat"); Near(target.EmAccumulation,0,"miss no EM");
            Near((double)type.GetField("Power",Hidden).GetValue(missed),50,"miss preserves power");
            Near((double)type.GetField("Penetration",Hidden).GetValue(missed),2,"miss preserves penetration");
            rolls = 0; settings.hitSample = () => {rolls++; return 0;};
            Sweep(NewFlight(origin),4000);
            Check(rolls == 1 && parts.Where((p,i) => p.CurrentHealth < health[i]).Any(), "hit damages equal-class hostile ship");
            Check(friendParts.Select(p => p.CurrentHealth).SequenceEqual(friendHealth), "friendly ship excluded from direct damage");
            Check(target.HeatAccumulation > 0 && target.EmAccumulation > 0, "successful hit applies status effects");

            // Refresh only the target for independent AoE checks.
            UnityEngine.Object.DestroyImmediate(target.gameObject);
            target = ShipBuilder.Build(data, material, new Vector2(10000,-10000),0,false,false,true);
            parts = target.GetComponentsInChildren<BattleshipSection>();
            foreach (var part in parts) part.ArmorThickness = 0;
            // Isolate local armor damage: the existing core hit redistributes damage over the hull.
            parts.Single(p => p.Role == BattleshipSectionRole.Core).GetComponent<Collider2D>().enabled = false;
            int blastCandidates = parts.Length - 1;
            parts.First(p => p.Role == BattleshipSectionRole.Armor).gameObject.AddComponent<BoxCollider2D>();
            health = parts.Select(p => p.CurrentHealth).ToArray();
            target.GetComponent<WeaponTarget>().Evasion = .5f;
            settings.hitSample = () => { rolls++; return .99; }; rolls = 0;
            void Blast() => typeof(ExplosionEffect).GetMethod("Detonate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { (Vector2)target.transform.position, 5000d, 1d, 2d, source, source.ShipClass, material,
                    null, BattleshipProjectile.Affect.HE, settings });
            Blast(); Check(rolls == blastCandidates, "AoE independently rolls once per overlapping section");
            Check(parts.Select(p => p.CurrentHealth).SequenceEqual(health), "AoE miss no HP change");
            Near(target.HeatAccumulation,0,"AoE miss no status effect");
            rolls = 0; settings.hitSample = () => rolls++ % 2 == 0 ? 0 : .99;
            Blast(); Check(rolls == blastCandidates, "AoE mixed independent rolls");
            Check(parts.Where((p,i) => p.CurrentHealth < health[i]).Any(), "some AoE sections hit");
            Check(parts.Where((p,i) => p.CurrentHealth == health[i]).Any(), "some AoE sections evaded");
            Check(friendParts.Select(p => p.CurrentHealth).SequenceEqual(friendHealth), "friendly ship excluded from AoE");
            settings.hitSample = () => throw new Exception("Beam must not use an undecided evasion rule");
            bool beamAllowed = (bool)typeof(CombatHitRules).GetMethod("Allows", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] {target, BattleshipProjectile.Affect.Photon, settings});
            Check(beamAllowed, "beam keeps existing path without probability roll");
            Debug.Log("COMBAT_HIT_RUNTIME_PASS: collision-before-roll, no miss effects, direct caching, AoE per-section and IFF.");
        }
        finally
        {
            foreach (var ship in new[] {source,target,friendly}) if (ship != null) UnityEngine.Object.DestroyImmediate(ship.gameObject);
            foreach (var explosion in UnityEngine.Object.FindObjectsByType<ExplosionEffect>(FindObjectsSortMode.None).Except(priorExplosions))
                UnityEngine.Object.DestroyImmediate(explosion.gameObject);
            UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
