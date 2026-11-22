using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Script;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Matched mirror-build trials. Editor only; never reads or writes construction saves.
public static class AmmoBalanceChecks
{
    [Serializable] public class Config { public int seed = 20261122; public int builds = 16; public int repeats = 2; public float limit = 90; public float step = 1f / 30; }
    [Serializable] public class Row {
        public int build, repeat, seed; public bool apLeft; public string winner;
        public float seconds, deltaTime; public double apHP, heHP, initialHP;
        public int apSections, heSections, initialSections; public ShipBuildData design;
    }
    static Config config; static ShipBuildData[] designs; static DuelSandbox duel;
    static string output; static int index, lastFrame = -1, initialSections; static double initialHP;
    static float wallStart;
    public static void RunInPlayMode() {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("AmmoBalance.Pending", true); Install(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void Install() {
        EditorApplication.update -= Tick;
        if (SessionState.GetBool("AmmoBalance.Pending", false)) EditorApplication.update += Tick;
    }
    static string Argument(string key) {
        var args = Environment.GetCommandLineArgs(); int n = Array.IndexOf(args, key);
        return n >= 0 && n + 1 < args.Length ? args[n + 1] : null;
    }
    static void Init() {
        string path = Argument("-ammoConfig"); config = path == null ? new Config() : JsonUtility.FromJson<Config>(File.ReadAllText(path));
        if (config.builds < 1 || config.repeats < 1 || config.limit <= 0 || config.step <= 0 || config.step > .1f) throw new Exception("Invalid trial configuration");
        output = Argument("-ammoOutput") ?? Path.GetFullPath("../AmmoBalanceResults.jsonl");
        if (File.Exists(output)) throw new Exception("Refusing to overwrite results: " + output);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output + ".config.json", JsonUtility.ToJson(config, true));
        var random = new System.Random(config.seed); designs = new ShipBuildData[config.builds];
        for (int i = 0; i < designs.Length; i++) {
            int guns = random.Next(1, 9), armor = random.Next(0, 13);
            var d = new ShipBuildData { seed = random.Next(), coreHPSettingID = random.Next(0, 5),
                referenceSections = guns + armor, weaponCounts = new int[7] };
            d.weaponCounts[random.Next(0, 2)] = guns;
            d.armor[0] = new ShipArmorSettings { count = armor, hpSettingID = random.Next(5), defenseThicknessSettingID = random.Next(5) };
            if (!d.TryCreateConstructionSnapshot(out designs[i], out string error)) throw new Exception(error);
        }
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        Time.timeScale = 1; Time.captureDeltaTime = config.step;
        duel = new GameObject("Ammo comparison").AddComponent<DuelSandbox>(); StartTrial();
    }
    static void SetAmmo(BattleshipInstance ship, BattleshipProjectile.Affect ammo) {
        var property = typeof(BattleshipSection).GetProperty("ConstructionWeapon", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var section in ship.GetComponentsInChildren<BattleshipSection>()) {
            object weapon = property.GetValue(section); if (weapon == null) continue;
            Type t = weapon.GetType(); t.GetField("Affect").SetValue(weapon, ammo);
            t.GetField("ExplosionRadius").SetValue(weapon, Convert.ToDouble(t.GetField("Power").GetValue(weapon)) * 1.5);
        }
    }
    static void StartTrial() {
        int build = index / (config.repeats * 2), repeat = index / 2 % config.repeats;
        UnityEngine.Random.InitState(unchecked(config.seed + build * 1009 + repeat));
        if (!duel.Configure(designs[build], designs[build], AIType.BS, AIType.BS, out string error)) throw new Exception(error);
        duel.Restart(true);
        bool apLeft = index % 2 == 0;
        SetAmmo(duel.PlayerShip, apLeft ? BattleshipProjectile.Affect.AP : BattleshipProjectile.Affect.HE);
        SetAmmo(duel.EnemyShip, apLeft ? BattleshipProjectile.Affect.HE : BattleshipProjectile.Affect.AP);
        initialHP = duel.PlayerShip.CoreHealth; initialSections = duel.PlayerShip.LivingSectionCount;
        if (initialHP != duel.EnemyShip.CoreHealth || initialSections != duel.EnemyShip.LivingSectionCount) throw new Exception("Mirror build mismatch");
        wallStart = Time.realtimeSinceStartup;
    }
    static void Tick() {
        EditorApplication.isPaused = false; Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
        if (!Application.isPlaying || Time.frameCount < 3 || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try {
            if (duel == null) { Init(); return; }
            if (Time.realtimeSinceStartup - wallStart > 180) throw new Exception("Trial wall timeout");
            if (duel.Result == DuelResult.Running && duel.Elapsed < config.limit) return;
            bool apLeft = index % 2 == 0;
            var ap = apLeft ? duel.PlayerShip : duel.EnemyShip; var he = apLeft ? duel.EnemyShip : duel.PlayerShip;
            // DuelSandbox disables the whole round on completion; IsAlive then returns false even for the winner.
            bool apAlive = ap != null && ap.CoreHealth > 0 && ap.LivingSectionCount > 0;
            bool heAlive = he != null && he.CoreHealth > 0 && he.LivingSectionCount > 0;
            var row = new Row { build = index / (config.repeats * 2), repeat = index / 2 % config.repeats,
                seed = config.seed, apLeft = apLeft, winner = apAlive && heAlive ? "timeout" : !apAlive && !heAlive ? "draw" : apAlive ? "AP" : "HE",
                seconds = duel.Elapsed, deltaTime = Time.deltaTime, apHP = ap == null ? 0 : Math.Max(0, ap.CoreHealth),
                heHP = he == null ? 0 : Math.Max(0, he.CoreHealth), initialHP = initialHP, initialSections = initialSections,
                apSections = ap == null ? 0 : ap.LivingSectionCount, heSections = he == null ? 0 : he.LivingSectionCount,
                design = designs[index / (config.repeats * 2)] };
            File.AppendAllText(output, JsonUtility.ToJson(row) + Environment.NewLine);
            Debug.Log($"AMMO_TRIAL {index + 1}/{designs.Length * config.repeats * 2} build={row.build} {row.winner} {row.seconds:F2}s");
            index++; if (index == designs.Length * config.repeats * 2) { Finish(0); return; } StartTrial();
        } catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int code) {
        Time.captureDeltaTime = 0; SessionState.SetBool("AmmoBalance.Pending", false);
        EditorApplication.update -= Tick; Debug.Log("AMMO_BALANCE_EXIT " + code); EditorApplication.Exit(code);
    }
}
