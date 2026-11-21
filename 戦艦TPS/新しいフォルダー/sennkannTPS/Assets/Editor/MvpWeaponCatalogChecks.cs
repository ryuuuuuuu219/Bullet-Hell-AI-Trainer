using System;
using System.Linq;
using System.Reflection;
using Script;
using UnityEditor;
using UnityEngine;

public static class MvpWeaponCatalogChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    private static void Near(double value, double expected, string label)
        => Check(Math.Abs(value - expected) < .0001, $"{label}: {value} != {expected}");
    private static object Field(object obj, string name) => obj.GetType().GetField(name).GetValue(obj);

    [MenuItem("戦艦TPS/検証/MVP兵装データ（工程05）")]
    public static void Run()
    {
        double[,] expected = { {20,2,300,5000,2,5,0,1.1}, {50,5,450,8000,3.5,12,0,1.2},
            {200,3,300,10000,5,10,1,1.2}, {5,.1,500,2000,.4,.2,0,1} };
        for (int id = 0; id < 4; id++)
        {
            var d = MvpWeaponCatalog.Get(id);
            double[] actual = { d.Power, d.Penetration, d.Speed, d.Range, d.ReloadSeconds, d.Heat, d.Em, d.CoreMultiplier };
            for (int i = 0; i < actual.Length; i++) Near(actual[i], expected[id,i], $"catalog {id} field {i}");
            Check(d.ShotCount == 1 && d.BaseHitCoefficient == 1, "MVP single shot and H=1");
            Check(!d.BaseSpreadDegrees.HasValue && !d.TargetCategories.HasValue, "unspecified values remain unspecified");
        }
        Near(MvpWeaponCatalog.Get(2).ExplosionRadius, 300, "missile blast radius");
        Near(MvpWeaponCatalog.Get(2).DistanceLifetime.Value, 10000, "missile distance lifetime");
        Near(MvpWeaponCatalog.Get(2).MissileTurnSpeed, 35, "missile turn cap");
        Near(MvpWeaponCatalog.Get(2).NavigationConstant, 2.8, "missile navigation");
        Near(MvpWeaponCatalog.ComposeSpread(3, 4), 4.6, "add base full angle and same-weapon count");
        bool rejected = false;
        try { MvpWeaponCatalog.Get(4); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "unknown local ID rejected");
        var invalid = new ShipBuildData(); invalid.weaponIDs[0] = 4;
        Check(!invalid.Validate(out _), "construction uses catalogue ID validation");
        if (Application.isPlaying) CheckConstructionAndProjectiles();
        Debug.Log("MVP_WEAPON_CATALOG_PASS: fixed definitions, unknown IDs, spread composition and unspecified fields.");
    }

    private static void CheckConstructionAndProjectiles()
    {
        var material = new Material(Shader.Find("Battleship/TilingTest"));
        var projectileRoot = new GameObject("Catalogue projectiles");
        BattleshipInstance ship = null;
        try
        {
            var data = new ShipBuildData { referenceSections = 12,
                weaponIDs = new[] {0,1,2,3,0,-1,-1}, weaponCounts = new[] {1,2,2,2,1,0,0} };
            data.armor[0].count = 4;
            ship = ShipBuilder.Build(data, material, new Vector2(15000,15000), 0, false, false, true);
            var mounts = ship.GetComponentsInChildren<BattleshipSection>().Where(s => s.Role == BattleshipSectionRole.Turret).ToArray();
            var getWeapon = typeof(BattleshipSection).GetProperty("ConstructionWeapon", Hidden);
            var fire = typeof(BattleshipProjectile).GetMethod("Fire", BindingFlags.Static | BindingFlags.NonPublic);
            var runtime = mounts.Select(s => getWeapon.GetValue(s)).ToArray();
            Check(runtime.Length == 8, "fixture mounts exist");
            for (int i = 0; i < mounts.Length; i++)
            {
                var w = runtime[i];
                var d = (MvpWeaponDefinition)Field(w, "Definition");
                Check(ReferenceEquals(d, MvpWeaponCatalog.Get(d.LocalId)), "runtime shares immutable definition");
                Near((float)Field(w, "Interval"), d.ReloadSeconds, "reload from catalogue");
                Near((float)Field(w, "Timer"), d.ReloadSeconds, "initially loaded");
                Near((float)Field(w, "Spread"), .8, "same-weapon mounted count, including duplicate slots");
                Near((float)Field(w, "Power"), d.Power, "nonrandom power");
                var effect = (ProjectileEffectSettings)Field(w, "Effects");
                Near(effect.heat, d.Heat, "heat from catalogue"); Near(effect.em, d.Em, "EM from catalogue");
                fire.Invoke(null, new object[] { mounts[i], w, Vector2.right, (Vector2?)new Vector2(20000,15000), projectileRoot.transform });
            }
            var bullets = projectileRoot.GetComponentsInChildren<BattleshipProjectile>();
            Check(bullets.Length == 8, "each mount produced a projectile");
            foreach (var bullet in bullets)
            {
                var d = bullet.WeaponDefinition;
                Check(d != null, "projectile retains definition");
                Near(bullet.Penetration, d.Penetration, "projectile penetration from catalogue");
                Near((float)typeof(BattleshipProjectile).GetField("speed", Hidden).GetValue(bullet), d.Speed, "projectile speed");
                var effect = (ProjectileEffectSettings)typeof(BattleshipProjectile).GetField("effects", Hidden).GetValue(bullet);
                Near(effect.heat, d.Heat, "projectile heat"); Near(effect.em, d.Em, "projectile EM");
            }
            var firstEffects = (ProjectileEffectSettings)Field(runtime[0], "Effects"); firstEffects.heat = 999;
            Check(((ProjectileEffectSettings)Field(runtime[1], "Effects")).heat != 999, "mount effects isolated");
            Check(((ProjectileEffectSettings)typeof(BattleshipProjectile).GetField("effects", Hidden).GetValue(bullets[0])).heat != 999,
                "launched effects are a snapshot");
            Debug.Log("MVP_WEAPON_PIPELINE_PASS: build -> mount -> projectile, duplicate slots, penetration/heat/EM, independent state.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectileRoot);
            if (ship != null) UnityEngine.Object.DestroyImmediate(ship.gameObject);
            UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
