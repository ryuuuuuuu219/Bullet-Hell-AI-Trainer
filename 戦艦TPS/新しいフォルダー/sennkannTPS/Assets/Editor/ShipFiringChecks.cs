using System;
using System.Linq;
using System.Reflection;
using Script;
using UnityEngine;
using UnityEditor;

public static class ShipFiringChecks
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    private static void Near(float value, float expected, string label, float tolerance = .01f)
        => Check(Mathf.Abs(value - expected) <= tolerance, $"{label}: {value} != {expected}");
    [MenuItem("戦艦TPS/検証/共通射撃AI（工程06）")]
    public static void Run()
    {
        Vector2 r = new Vector2(1000, 300), vt = new Vector2(-10, 40);
        var direction = FiringSolution.Direction(Vector2.zero, r,  vt, 300, out double time);
        Check(time > 0, "positive meeting time");
        Near(((direction * 300) * (float)time - (r + vt * (float)time)).magnitude, 0, "intercept equation");
        direction = FiringSolution.Direction(Vector2.zero, Vector2.right * 60, Vector2.up * 100, 300, out time);
        Check(time == 0 && direction == Vector2.right, "0.2 second direct aim inclusive");
        FiringSolution.Direction(Vector2.zero, r, vt, float.PositiveInfinity, out time);
        Check(time == 0, "instant beam bypass");
        direction = FiringSolution.Direction(Vector2.zero, Vector2.right * 1000, Vector2.right * 400, 300, out time);
        Check(time == 0 && direction == Vector2.right, "no meeting solution direct aim");
        FiringSolution.Direction(Vector2.zero, Vector2.right * 1000, Vector2.left * 300, 300, out time);
        Near((float)time, 1000f / 600, "linear degenerate equation");
        FiringSolution.Direction(Vector2.zero, Vector2.right * 1000, Vector2.left * 400, 300, out time);
        Near((float)time, 1000f / 700, "smallest positive root");
        Check(FiringSolution.Direction(Vector2.zero, r, vt, 0, out _) == Vector2.zero, "invalid speed blocks shot");
        if (Application.isPlaying) Runtime();
        Debug.Log("SHIP_FIRING_CHECKS_PASS: interception, direct aim boundaries, roots and runtime firing.");
    }

    private static void Runtime()
    {
        var material = new Material(Shader.Find("Battleship/TilingTest"));
        BattleshipInstance source = null, a = null, b = null;
        var extra = new GameObject("Aircraft target adapter");
        var before = UnityEngine.Object.FindObjectsByType<BattleshipProjectile>(FindObjectsSortMode.None);
        try
        {
            var data = new ShipBuildData { referenceSections = 5, weaponCounts = new[] {1,0,0,0,0,0,0} };
            data.armor[0].count = 4;
            source = ShipBuilder.Build(data, material, new Vector2(10000,10000), 0, true, false, true);
            a = ShipBuilder.Build(data, material, new Vector2(12000,10000), 0, false, false, true);
            b = ShipBuilder.Build(data, material, new Vector2(13000,10000), 0, false, false, true);
            var ai = source.GetComponent<ShipFiringAI>();
            var mount = source.GetComponentsInChildren<BattleshipSection>().Single(s => s.Role == BattleshipSectionRole.Turret);
            var hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            object weapon = typeof(BattleshipSection).GetProperty("ConstructionWeapon", hidden).GetValue(mount);
            weapon.GetType().GetField("Spread").SetValue(weapon, 0f);
            BattleshipProjectile[] Bullets() => UnityEngine.Object.FindObjectsByType<BattleshipProjectile>(FindObjectsSortMode.None).Except(before).ToArray();
            ai.Tick(.01f);
            Check(mount.LockedWeaponTarget.Ship == a && Bullets().Length == 1, "nearest enemy and initially loaded shot");
            b.transform.position = new Vector3(11000,10000);
            ai.Tick(.1f); Check(mount.LockedWeaponTarget.Ship == a, "closer newcomer does not steal lock");
            a.transform.position = new Vector3(20000,10000);
            ai.Tick(.1f); Check(mount.LockedWeaponTarget.Ship == b, "out of range reacquires");
            b.gameObject.SetActive(false);
            ai.Tick(100); Check(mount.LockedWeaponTarget == null, "inactive target releases lock");
            int count = Bullets().Length;
            b.gameObject.SetActive(true); ai.Tick(.01f);
            Check(Bullets().Length == count + 1, "idle holds one loaded shot, no backlog");
            count = Bullets().Length; ai.Tick(4.5f);
            Check(Bullets().Length == count + 2, "long frame emits elapsed reload cycles");
            float timer = (float)weapon.GetType().GetField("Timer").GetValue(weapon);
            Check(timer > .4f && timer < .6f, "reload remainder preserved");
            ai.sectionWeights = new float[5]; ai.Tick(10);
            count = Bullets().Length; ai.Tick(10); Check(Bullets().Length == count, "zero weights hold fire");
            ai.sectionWeights = new[] {0f,0,0,0,1};
            var motor = source.GetComponent<ShipMovement>();
            motor.SetThrottle(ShipThrottle.AheadFull); motor.Tick(.1f);
            var existing = Bullets(); ai.Tick(.01f);
            var bullet = Bullets().Except(existing).Single();
            Vector2 shotDirection = (Vector2)typeof(BattleshipProjectile).GetField("direction", hidden).GetValue(bullet);
            float worldSpeed = (float)typeof(BattleshipProjectile).GetField("speed", hidden).GetValue(bullet);
            Vector2 expectedDirection = FiringSolution.Direction(mount.FiringPosition, b.GetComponentsInChildren<BattleshipSection>().Single(s => s.Role == BattleshipSectionRole.Core).GetComponent<Renderer>().bounds.center,
                 b.TranslationVelocity, 300, out _);
            Near((shotDirection * worldSpeed - (expectedDirection * 300)).magnitude, 0, "actual projectile inherits source velocity");
            var adapter = extra.AddComponent<WeaponTarget>(); adapter.category = WeaponTargetCategory.Aircraft;
            extra.transform.position = source.transform.position + Vector3.right * 1000;
            ai.weaponCategories[0] = WeaponTargetCategory.Aircraft;
            ai.Tick(.01f); Check(mount.LockedWeaponTarget == adapter, "category filtering and non-ship representative point");
            adapter.friend = true; ai.Tick(.01f); Check(mount.LockedWeaponTarget == null, "friendly target excluded");
            adapter.friend = false; adapter.category = WeaponTargetCategory.Missile;
            ai.weaponCategories[0] = WeaponTargetCategory.Missile;
            ai.Tick(.01f); Check(mount.LockedWeaponTarget == adapter, "missile adapter category");
            // Statistical check: weight is applied per section, not divided across a category.
            ai.sectionWeights = new[] {1f,1,1,1,1};
            var aim = typeof(ShipFiringAI).GetMethod("TryAim", hidden);
            var targetSections = b.GetComponentsInChildren<BattleshipSection>();
            Vector2 corePoint = targetSections.Single(s => s.Role == BattleshipSectionRole.Core).GetComponent<Renderer>().bounds.center;
            var random = new System.Random(902);
            int coreSamples = 0;
            for (int i = 0; i < 3000; i++)
            {
                object[] args = { b.GetComponent<WeaponTarget>(), random, Vector2.zero };
                Check((bool)aim.Invoke(ai, args), "weighted aim valid");
                if (((Vector2)args[2] - corePoint).sqrMagnitude < .001f) coreSamples++;
            }
            Near(coreSamples / 3000f, 1f / targetSections.Length, "equal weights are per section", .03f);
            var armor = targetSections.First(s => s.Role == BattleshipSectionRole.Armor);
            typeof(BattleshipSection).GetMethod("ApplyDamage", hidden).Invoke(armor, new object[] { armor.CurrentHealth });
            ai.sectionWeights = new[] {0f,1,1,1,0};
            Vector2 destroyedPoint = armor.GetComponent<Renderer>().bounds.center;
            for (int i = 0; i < 100; i++)
            {
                object[] args = { b.GetComponent<WeaponTarget>(), random, Vector2.zero };
                Check((bool)aim.Invoke(ai, args) && ((Vector2)args[2] - destroyedPoint).sqrMagnitude > .001f, "destroyed section excluded");
            }
            typeof(BattleshipSection).GetMethod("Detach", hidden).Invoke(mount, null);
            count = Bullets().Length; ai.Tick(10); Check(Bullets().Length == count, "detached turret stops firing");
            Debug.Log("SHIP_FIRING_RUNTIME_PASS: persistent per-mount lock, categories, reload, weights and velocity inheritance.");
        }
        finally
        {
            foreach (var bullet in UnityEngine.Object.FindObjectsByType<BattleshipProjectile>(FindObjectsSortMode.None).Except(before)) UnityEngine.Object.DestroyImmediate(bullet.gameObject);
            foreach (var ship in new[] { source,a,b }) if (ship != null) UnityEngine.Object.DestroyImmediate(ship.gameObject);
            UnityEngine.Object.DestroyImmediate(extra); UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
