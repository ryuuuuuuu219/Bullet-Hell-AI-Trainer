using System;
using System.Linq;
using System.Reflection;
using Script;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class DuelSandboxChecks
{
    private static DuelSandbox duel;
    private static bool started, playerMoved, enemyMoved, shots, damage, broken;
    private static Vector2 playerStart, enemyStart;
    private static int initialSections;
    private static float startTime;
    private static int captureFrame;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void RunInPlayMode()
    {
        var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        view.position = new Rect(0, 0, 1280, 800);
        DuelSandboxMenu.CreateScene();
        SessionState.SetBool("DuelChecks.Pending", true);
        Install(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Install()
    {
        EditorApplication.update -= Tick;
        if (SessionState.GetBool("DuelChecks.Pending", false)) EditorApplication.update += Tick;
    }
    private static void Sink(BattleshipInstance ship)
    {
        var core = ship.GetComponentsInChildren<BattleshipSection>().First(s => s.Role == BattleshipSectionRole.Core);
        typeof(BattleshipInstance).GetMethod("ReceiveSingleSectionHit", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(ship, new object[] { core, 100000d });
    }
    private static void Tick()
    {
        EditorApplication.isPaused = false; Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
        if (!Application.isPlaying || Time.frameCount < 5) return;
        try
        {
            if (settingsCaptureFrame > 0)
            {
                if (Time.frameCount < settingsCaptureFrame + 10) return;
                Check(System.IO.File.Exists("../ConstructionQAImages/duel-settings.png"), "settings capture exists");
                Finish(0); return;
            }
            if (!started)
            {
                duel = UnityEngine.Object.FindFirstObjectByType<DuelSandbox>();
                if (captureFrame == 0)
                {
                    System.IO.Directory.CreateDirectory("../ConstructionQAImages");
                    ScreenCapture.CaptureScreenshot("../ConstructionQAImages/duel.png");
                    captureFrame = Time.frameCount; return;
                }
                if (Time.frameCount < captureFrame + 10) return;
                Check(duel != null && !duel.AutomaticPlayer, "manual entry");
                Check(duel.PlayerShip.GetComponent<ShipMovement>().playerControlled &&
                    !duel.PlayerShip.GetComponent<ShipFiringAI>().enabled, "manual control, no duplicate auto fire");
                ManualFire();
                Configuration();
                Sink(duel.EnemyShip); duel.EvaluateResult(); duel.EvaluateResult();
                Check(duel.Result == DuelResult.Victory && duel.CompletionCount == 1, "victory latches once");
                duel.Restart(false); Sink(duel.PlayerShip); duel.EvaluateResult();
                Check(duel.Result == DuelResult.Defeat, "player sinking defeats");
                duel.Restart(false); Sink(duel.PlayerShip); Sink(duel.EnemyShip); duel.EvaluateResult();
                Check(duel.Result == DuelResult.Defeat, "simultaneous defeat precedence");
                duel.Restart(true);
                Check(duel.Result == DuelResult.Running && duel.CompletionCount == 0 &&
                    duel.PlayerShip.CoreHealth == 500 && duel.EnemyShip.CoreHealth == 500, "restart resets both ships and result");
                playerStart = duel.PlayerShip.CorePosition; enemyStart = duel.EnemyShip.CorePosition;
                initialSections = duel.PlayerShip.LivingSectionCount + duel.EnemyShip.LivingSectionCount;
                startTime = Time.realtimeSinceStartup; Time.timeScale = 20; started = true;
                return;
            }
            if (duel.PlayerShip != null) playerMoved |= Vector2.Distance(playerStart, duel.PlayerShip.CorePosition) > 50;
            if (duel.EnemyShip != null) enemyMoved |= Vector2.Distance(enemyStart, duel.EnemyShip.CorePosition) > 50;
            shots |= UnityEngine.Object.FindObjectsByType<BattleshipProjectile>(FindObjectsSortMode.None).Length > 0;
            damage |= (duel.PlayerShip != null && duel.PlayerShip.CoreHealth < 500) ||
                (duel.EnemyShip != null && duel.EnemyShip.CoreHealth < 500);
            if (duel.PlayerShip != null && duel.EnemyShip != null)
                broken |= duel.PlayerShip.LivingSectionCount + duel.EnemyShip.LivingSectionCount < initialSections;
            if (duel.Result == DuelResult.Running)
            {
                Check(Time.realtimeSinceStartup - startTime < 120 && duel.Elapsed < 1200, "live duel timed out");
                return;
            }
            Check(playerMoved && enemyMoved && shots && damage && broken, $"live integration: move {playerMoved}/{enemyMoved}, fire {shots}, damage {damage}, broken {broken}");
            Check(duel.CompletionCount == 1, "natural combat result once");
            Check(UnityEngine.Object.FindObjectsByType<BattleshipProjectile>(FindObjectsSortMode.None).Length == 0, "no active shots after result");
            Debug.Log($"DUEL_SANDBOX_PLAYMODE_PASS: movement, fire, damage, section loss, natural {duel.Result} at {duel.Elapsed:F1}s; forced victory/defeat and restart.");
            duel.OpenSettings();
            ScreenCapture.CaptureScreenshot("../ConstructionQAImages/duel-settings.png");
            settingsCaptureFrame = Time.frameCount;
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }
    private static int settingsCaptureFrame;
    private static void ManualFire()
    {
        var oldBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
        try
        {
            var updateInput = typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(InputUpdateType) }, null);
            var updateDuel = typeof(DuelSandbox).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            var root = duel.PlayerShip.GetComponent<ShipFiringAI>().ProjectileRoot;
            int before = root.childCount;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(25, Screen.height - 25), buttons = 1 });
            updateInput.Invoke(null, new object[] { InputUpdateType.Dynamic }); updateDuel.Invoke(duel, null);
            Check(root.childCount == before, "HUD click does not shoot");
            Vector3 aim = Camera.main.WorldToScreenPoint(duel.EnemyShip.CorePosition);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = aim, buttons = 1 });
            updateInput.Invoke(null, new object[] { InputUpdateType.Dynamic }); updateDuel.Invoke(duel, null);
            Check(root.childCount > before, "mouse hold fires manual mounts into round root");
            int after = root.childCount;
            updateDuel.Invoke(duel, null);
            Check(root.childCount == after, "held click respects reload");
            Debug.Log("DUEL_MANUAL_INPUT_PASS: HUD exclusion, mouse fire and reload.");
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldBehavior;
            InputSystem.settings.backgroundBehavior = oldBackground;
        }
    }
    private static void Configuration()
    {
        var player = DuelSandbox.TestDesign();
        player.coreHPSettingID = 2;
        player.weaponCounts = new[] { 2, 2, 2, 2, 0, 0, 0 };
        player.armor = new[] { new ShipArmorSettings { count = 1, hpSettingID = 1, defenseThicknessSettingID = 2 },
            new ShipArmorSettings { count = 1, hpSettingID = 2, defenseThicknessSettingID = 3 },
            new ShipArmorSettings { count = 1, hpSettingID = 3, defenseThicknessSettingID = 4 } };
        player.referenceSections = 11;
        Check(duel.Configure(player, DuelSandbox.TestDesign(), AIType.C2, AIType.AC, out var error), error);
        player.coreHPSettingID = 0;
        duel.Restart(true);
        Check(duel.PlayerShip.CoreHealth == 2000 && duel.EnemyShip.CoreHealth == 500, "independent snapshot HP");
        Check(duel.PlayerShip.GetComponent<ShipSteeringAI>().ActiveType == AIType.C2 &&
            duel.EnemyShip.GetComponent<ShipSteeringAI>().ActiveType == AIType.AC, "configured AI");
        var snapshot = duel.PlayerShip.ConstructionSnapshot;
        MissileGuidance();
        Check(snapshot.weaponCounts.Take(4).All(n => n == 2) && snapshot.armor[2].hpSettingID == 3 &&
            snapshot.armor[1].defenseThicknessSettingID == 3, "mount and armor settings preserved");
        foreach (var section in duel.PlayerShip.GetComponentsInChildren<BattleshipSection>().Where(s => s.Role == BattleshipSectionRole.Armor))
        {
            var armor = snapshot.armor[(int)section.Mode];
            Check(section.MaximumHealth == ShipBuildData.ArmorHP[armor.hpSettingID] &&
                section.ArmorThickness == ShipBuildData.Thickness[armor.defenseThicknessSettingID], "armor runtime presets");
        }
        var weaponField = typeof(BattleshipSection).GetField("weapon", BindingFlags.Instance | BindingFlags.NonPublic);
        duel.SelectAmmo(false, BattleshipProjectile.Affect.APCR);
        duel.SelectAmmo(true, BattleshipProjectile.Affect.HEAT);
        foreach (var section in duel.PlayerShip.GetComponentsInChildren<BattleshipSection>())
        {
            var weapon = weaponField.GetValue(section); if (weapon == null) continue;
            var type = weapon.GetType(); bool missile = type.GetField("Kind").GetValue(weapon).ToString() == "AntiShipMissile";
            Check((BattleshipProjectile.Affect)type.GetField("Affect").GetValue(weapon) ==
                (missile ? BattleshipProjectile.Affect.HEAT : BattleshipProjectile.Affect.APCR), "all player mounts update ammo");
            if (missile) Check((double)type.GetField("ExplosionRadius").GetValue(weapon) == 300, "missile radius remains 300");
        }
        foreach (var section in duel.EnemyShip.GetComponentsInChildren<BattleshipSection>())
        {
            var weapon = weaponField.GetValue(section); if (weapon == null) continue;
            Check((BattleshipProjectile.Affect)weapon.GetType().GetField("Affect").GetValue(weapon) == BattleshipProjectile.Affect.AP,
                "player selection leaves enemy unchanged");
        }
        Check(!duel.Configure(player, player, AIType.DF, AIType.BS, out error), "missing escort rejected");
        var invalid = DuelSandbox.TestDesign(); invalid.armor[0].count = 61;
        Check(!duel.Configure(invalid, player, AIType.BS, AIType.BS, out error) && duel.PlayerDesign.coreHPSettingID == 2,
            "invalid config preserves committed snapshot");
        duel.OpenSettings(); duel.EvaluateResult();
        Check(duel.Result == DuelResult.Running, "editing inactive ships does not trigger defeat");
        Check(duel.Configure(DuelSandbox.TestDesign(), DuelSandbox.TestDesign(), AIType.BS, AIType.BS, out error), error);
        duel.SelectAmmo(false, BattleshipProjectile.Affect.AP); duel.SelectAmmo(true, BattleshipProjectile.Affect.HE);
        duel.Restart(false);
        Debug.Log("DUEL_CONFIGURATION_PASS: independent design snapshots, AI, armor, mounts, ammo switching and invalid input.");
    }
    private static void MissileGuidance()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var firing = duel.PlayerShip.GetComponent<ShipFiringAI>();
        firing.Tick(.1f);
        var missile = firing.ProjectileRoot.GetComponentsInChildren<BattleshipProjectile>()
            .First(p => p.WeaponDefinition.LocalId == 2);
        var target = duel.EnemyShip.GetComponent<WeaponTarget>();
        Check((WeaponTarget)typeof(BattleshipProjectile).GetField("guidanceTarget", hidden).GetValue(missile) == target,
            "automatic missile retains locked target");
        Check(duel.PlayerShip.GetComponentsInChildren<BattleshipSection>().All(s => s.ManualMissileTarget == target),
            "manual duel mounts receive opponent");
        var direction = typeof(BattleshipProjectile).GetField("direction", hidden);
        var guide = typeof(BattleshipProjectile).GetMethod("Guide", hidden);
        Vector2 before = (Vector2)direction.GetValue(missile);
        Vector3 oldPosition = duel.EnemyShip.transform.position;
        duel.EnemyShip.transform.position += Vector3.up * 3000;
        guide.Invoke(missile, new object[] { .1f });
        Vector2 after = (Vector2)direction.GetValue(missile);
        Check(Math.Abs(Vector2.SignedAngle(before, after) - 3.5f) < .001f,
            "moving target causes steering at 35 degrees per second cap");
        duel.EnemyShip.gameObject.SetActive(false);
        guide.Invoke(missile, new object[] { .1f });
        Check(Vector2.Distance(after, (Vector2)direction.GetValue(missile)) < .00001f, "target loss stops steering");
        duel.EnemyShip.gameObject.SetActive(true); duel.EnemyShip.transform.position = oldPosition;
        guide.Invoke(missile, new object[] { .1f });
        Check(Vector2.Distance(after, (Vector2)direction.GetValue(missile)) < .00001f, "lost missile does not silently reacquire");
        Debug.Log("DUEL_MISSILE_GUIDANCE_PASS: live target reference, moving LOS, turn cap and target loss.");
    }
    private static void Finish(int code)
    {
        Time.timeScale = 1; SessionState.SetBool("DuelChecks.Pending", false);
        EditorApplication.update -= Tick; EditorApplication.Exit(code);
    }
}
