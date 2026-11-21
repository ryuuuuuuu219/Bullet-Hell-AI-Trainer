using System;
using System.Linq;
using System.Reflection;
using System.Globalization;
using Script;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ConstructionMovementChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    private static void Near(double actual, double expected, string label, double tolerance = .001)
        => Check(Math.Abs(actual - expected) <= tolerance, $"{label}: {actual} != {expected}");
    private static object Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Hidden).Invoke(target, args);

    public static void RunInPlayMode()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ShipConstruction.unity");
        SessionState.SetBool("ConstructionMovementChecks.Pending", true);
        Install();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Install()
    {
        EditorApplication.update -= PlayCheck;
        if (SessionState.GetBool("ConstructionMovementChecks.Pending", false)) EditorApplication.update += PlayCheck;
    }

    private static void PlayCheck()
    {
        if (Application.isBatchMode) { EditorApplication.isPaused = false; Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate(); }
        if (!Application.isPlaying || Time.frameCount < 3) return;
        EditorApplication.update -= PlayCheck;
        SessionState.SetBool("ConstructionMovementChecks.Pending", false);
        try
        {
            Run();
            ShipSteeringChecks.Run();
            MvpWeaponCatalogChecks.Run();
            ShipFiringChecks.Run();
            CombatHitChecks.Run();
            ScriptRegressionChecks.Run();
            var go = new GameObject("Input path check");
            var oldInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            var oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            try
            {
                var motor = go.AddComponent<ShipMovement>();
                motor.Initialize(new ShipPerformance(250, 25), true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); UpdateGameInput();
                Call(motor, "Update");
                Check(motor.Command == ShipThrottle.AheadSlow, $"W selects slow ahead from stop (command={motor.Command}, enabled={keyboard.enabled}, pressed={keyboard.wKey.isPressed}, edge={keyboard.wKey.wasPressedThisFrame}, current={Keyboard.current == keyboard})");
                UpdateGameInput(); Call(motor, "Update");
                Check(motor.Command == ShipThrottle.AheadSlow, "held W does not repeat throttle steps");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.A)); UpdateGameInput();
                Call(motor, "Update");
                Check(motor.Command == ShipThrottle.Stop && motor.TurnInput == 1, "stop and port helm input");
            }
            finally {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = oldInputBehavior;
                InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("CONSTRUCTION_MOVEMENT_PLAYMODE_PASS: runtime generation and keyboard input path.");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    // Editor callbacks default to editor input updates. Exercise the game's dynamic update explicitly.
    private static void UpdateGameInput()
        => typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Dynamic });

    [MenuItem("戦艦TPS/検証/建造と操艦（工程02・03）")]
    public static void Run()
    {
        bool existed = PlayerPrefs.HasKey(ShipBuildStore.Key);
        string old = PlayerPrefs.GetString(ShipBuildStore.Key);
        bool legacyExisted = PlayerPrefs.HasKey(ShipBuildStore.LegacyKey);
        string oldLegacy = PlayerPrefs.GetString(ShipBuildStore.LegacyKey);
        BattleshipInstance a = null, b = null;
        GameObject preview = null;
        Material material = null;
        try
        {
            var data = new ShipBuildData { seed = 0, coreHPSettingID = 2, referenceSections = 25 };
            data.weaponCounts = new[] { 8, 0, 0, 12, 0, 0, 0 };
            data.armor[0] = new ShipArmorSettings { count = 5, hpSettingID = 1, defenseThicknessSettingID = 2 };
            Check(data.Validate(out _), "P4 build inputs");
            Near(data.TurretHP, 400, "turret HP");
            // Five basic armor sections, weighted ID sum 3: FA=.992, FH=1.3, FN=.8.
            Near(data.CalculatePerformance().MaximumSpeed, 257.92, "speed formula");
            Near(data.CalculatePerformance().MaximumTurnSpeed, 43.688, "turn formula");
            var extreme = data.Snapshot();
            extreme.baseSpeed = extreme.baseTurnSpeed = 100000;
            Near(extreme.CalculatePerformance().MaximumSpeed, 1000, "speed clamp");
            Near(extreme.CalculatePerformance().MaximumTurnSpeed, 135, "turn clamp");
            extreme.baseTurnSpeed = 0;
            Near(extreme.CalculatePerformance().MaximumTurnSpeed, 5, "turn minimum");
            extreme.armor[0].count = 61;
            Check(!extreme.Validate(out _), "reject armor overflow");
            extreme = data.Snapshot(); extreme.baseSpeed = float.NaN;
            Check(!extreme.Validate(out _), "reject nonfinite");
            extreme = data.Snapshot(); extreme.weaponCounts = new int[6];
            Check(!extreme.Validate(out _), "reject array length");
            var noArmor = new ShipBuildData { referenceSections = 5, baseSpeed = 250 };
            noArmor.armor[0].count = 0; noArmor.weaponCounts = new[] { 5, 0, 0, 0, 0, 0, 0 };
            Near(noArmor.CalculatePerformance().MaximumSpeed, 450, "zero armor formula");

            Check(ShipBuildStore.TrySave(data, out _), "commit valid");
            var invalid = data.Snapshot(); invalid.referenceSections++;
            Check(!ShipBuildStore.TrySave(invalid, out _), "reject mismatched input");
            Check(ShipBuildStore.TryLoad(out var loaded) && loaded.referenceSections == 25, "preserve last valid save");
            data.seed = 123;
            Check(loaded.seed == 0, "save isolated from draft");
            data = loaded;

            var precise = data.Snapshot();
            precise.generatorSpeed = 1.23459f;
            precise.heatGainReduction = .12349f;
            precise.heatCapOffset = -20.1234f;
            Check(precise.TryCreateConstructionSnapshot(out var normalized, out _), "equipment normalization");
            Near(normalized.generatorSpeed, 1.2345, "generator truncation", .00001);
            Near(normalized.heatGainReduction, .1234, "reduction truncation", .00001);
            Check(ShipEquipmentValues.TryEncode(normalized, out var equipment, out _), "encode equipment");
            string equipmentJson = JsonUtility.ToJson(equipment);
            Check(equipmentJson.Contains("speedMultiplier=1.2345") && equipmentJson.Contains("heatCapOffset=-20.1234"), "four decimal strings");
            Array.Reverse(equipment.EquipmentID); Array.Reverse(equipment.PerformanceValues);
            int generatorIndex = Array.IndexOf(equipment.EquipmentID, 7);
            equipment.PerformanceValues[generatorIndex] = "turnMultiplier=1.0000,speedMultiplier=1.2345";
            Check(equipment.TryApply(precise, out _), "equipment and field order independence");
            Near(precise.generatorSpeed, 1.2345, "decoded multiplier", .00001);
            equipment.EquipmentID[0] = equipment.EquipmentID[1];
            Check(!equipment.TryApply(precise, out _), "reject duplicate equipment ID");
            precise.heatCapOffset = -.12349f;
            Check(!precise.TryCreateConstructionSnapshot(out _, out _), "negative ambiguous rounding rejected");
            var culture = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Check(ShipBuildStore.TrySave(normalized, out _), "culture independent save");
                Check(ShipBuildStore.TryLoad(out var cultureLoaded), "culture independent reload");
                Near(cultureLoaded.generatorSpeed, 1.2345, "culture independent load", .00001);
            } finally { CultureInfo.CurrentCulture = culture; }
            PlayerPrefs.DeleteKey(ShipBuildStore.Key);
            string legacyJson = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(data),
                ",\"(?:radarCorrection|heatGainReduction|emGainReduction|heatCapOffset|emCapOffset)\":[^,}]+", "");
            PlayerPrefs.SetString(ShipBuildStore.LegacyKey, legacyJson);
            Check(ShipBuildStore.TryLoad(out var migrated) && migrated.seed == data.seed, "v1 migration");
            Near(migrated.radarCorrection, 1, "v1 missing equipment defaults");
            Near(migrated.heatGainReduction, 0, "v1 missing reduction defaults");
            Check(ShipBuildStore.TrySave(migrated, out _), "save v2 after migration");
            string validSave = PlayerPrefs.GetString(ShipBuildStore.Key);
            PlayerPrefs.SetString(ShipBuildStore.Key, "{broken}");
            Check(!ShipBuildStore.TryLoad(out _, out var corruption) && !string.IsNullOrEmpty(corruption), "corrupt save reports error");
            PlayerPrefs.SetString(ShipBuildStore.Key, validSave);

            material = new Material(Shader.Find("Battleship/TilingTest"));
            a = ShipBuilder.Build(data, material, Vector2.zero, 0, true, false, true);
            var equipmentBuild = data.Snapshot();
            equipmentBuild.heatGainReduction = .25f; equipmentBuild.emGainReduction = .5f;
            equipmentBuild.heatCapOffset = -20; equipmentBuild.emCapOffset = -49;
            b = ShipBuilder.Build(equipmentBuild, material, Vector2.zero, 0, false, false, true);
            Call(b, "AccumulateImpact", 100d, 100d);
            Near(b.HeatAccumulation, 75, "heat equipment reduction");
            Near(b.EmAccumulation, 50, "EM equipment reduction");
            Check(!b.EmpActive, "subthreshold EM remains inactive");
            Call(b, "AccumulateImpact", 1000d, 1000d);
            Near(b.HeatAccumulation, 130, "heat equipment cap");
            Near(b.EmAccumulation, 101, "EM equipment cap");
            Check(b.EmpActive, "equipment cap permits EMP activation");
            var sa = a.GetComponentsInChildren<BattleshipSection>();
            var sb = b.GetComponentsInChildren<BattleshipSection>();
            Check(sa.Length == sb.Length && sa.Length > 1, "deterministic count");
            for (int i = 0; i < sa.Length; i++)
            {
                Check(sa[i].Role == sb[i].Role && sa[i].Mode == sb[i].Mode, "deterministic roles");
                Check(sa[i].GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(sb[i].GetComponent<MeshFilter>().sharedMesh.vertices), "deterministic geometry");
                if (sa[i].Role == BattleshipSectionRole.Turret) Near(sa[i].MaximumHealth, 400, "assigned turret HP");
                if (sa[i].Role == BattleshipSectionRole.Armor) {
                    Near(sa[i].MaximumHealth, 50, "assigned armor HP");
                    Near(sa[i].ArmorThickness, 100, "assigned thickness");
                    Near(sa[i].ArmorInterception, .15, "assigned interception");
                }
            }
            Near(a.CoreHealthLimit, 2000, "assigned core HP");
            var destroyed = sb.First(s => s.Role == BattleshipSectionRole.Turret);
            var damagedPerformance = b.GetComponent<ShipMovement>().Performance;
            Call(b, "ReceiveSingleSectionHit", destroyed, destroyed.MaximumHealth);
            Check(!destroyed.IsAttached, "turret destruction exercised");
            Near(b.GetComponent<ShipMovement>().Performance.MaximumSpeed, damagedPerformance.MaximumSpeed, "destroyed section preserves speed");
            Near(b.GetComponent<ShipMovement>().Performance.MaximumTurnSpeed, damagedPerformance.MaximumTurnSpeed, "destroyed section preserves turn");
            var motor = a.GetComponent<ShipMovement>();
            float speed = motor.Performance.MaximumSpeed, turn = motor.Performance.MaximumTurnSpeed;
            motor.Tick(1); Near(a.transform.position.x, 0, "initial stopped");
            motor.SetThrottle(ShipThrottle.AheadFull); motor.Tick(1);
            Near(a.transform.position.x, speed, "full ahead");
            motor.SetThrottle(ShipThrottle.ReverseSlow); motor.Tick(1);
            Near(a.transform.position.x, speed * .75, "reverse");
            motor.SetThrottle(ShipThrottle.AheadSlow); motor.Tick(1);
            Near(a.transform.position.x, speed, "slow ahead");
            motor.SetThrottle(ShipThrottle.Stop); motor.Tick(1);
            Near(a.transform.position.x, speed, "stop");
            motor.TurnInput = 1; float angle = a.transform.eulerAngles.z; motor.Tick(1);
            Near(Mathf.DeltaAngle(angle, a.transform.eulerAngles.z), turn, "turn limit");
            motor.TurnInput = 0;
            Call(a, "ReceiveSingleSectionHit", sa[0], 10d);
            Near(motor.Performance.MaximumSpeed, speed, "damage preserves speed");
            data.baseSpeed = 0;
            Near(a.ConstructionSnapshot.baseSpeed, 250, "immutable construction snapshot");

            Call(a, "AccumulateImpact", 0d, 150d);
            Check(a.EmpActive, "EMP activates above 100");
            typeof(BattleshipInstance).GetField("emAccumulation", Hidden).SetValue(a, 50d);
            Check(a.EmpActive, "EMP persists below threshold");
            motor.TurnInput = 1; angle = a.transform.eulerAngles.z; motor.Tick(1);
            Near(Mathf.DeltaAngle(angle, a.transform.eulerAngles.z), turn * .5, "EMP turn factor");
            motor.TurnInput = 0;
            a.transform.rotation = Quaternion.Euler(0, 0, -90); a.transform.position = Vector3.zero;
            motor.SetThrottle(ShipThrottle.AheadFull);
            motor.Tick(.5f); Near(a.transform.position.x, 0, "EMP delayed command");
            motor.Tick(1); Near(a.transform.position.x, speed * .5, "EMP split frame");
            motor.halfExtent = 10; motor.Tick(1); Near(a.transform.position.x, 10, "boundary clamp");
            Near(motor.Performance.MaximumSpeed, speed, "clamp preserves performance");
            typeof(BattleshipInstance).GetField("emAccumulation", Hidden).SetValue(a, 0d);
            Call(a, "Update");
            Check(!a.EmpActive, "EMP recovers at zero");

            motor.halfExtent = 30000;
            motor.gravityCenter = new Vector2(0, 1000); motor.gravitySpeed = 5;
            motor.SetThrottle(ShipThrottle.Stop); a.transform.position = Vector3.zero;
            motor.Tick(.5f); Near(a.transform.position.y, 2.5, "gravity is velocity times delta time");
            motor.Tick(.5f); Near(a.transform.position.y, 5, "gravity does not accumulate acceleration");
            a.transform.position = Vector3.zero; motor.SetThrottle(ShipThrottle.AheadFull);
            motor.Tick(.5f);
            Near(a.transform.position.x, speed * .5, "gravity preserves commanded forward velocity");
            Near(a.transform.position.y, 2.5, "gravity is vector composed with movement");
            Near(motor.Performance.MaximumSpeed, speed, "gravity preserves construction speed");

            preview = new GameObject("Construction UI regression");
            var ui = preview.AddComponent<TilingTestScene>();
            Call(ui, "ApplySavedBuild", loaded);
            ui.GenerateForValidation();
            Check(ui.TryGetBuild(out var uiData, out _), "UI captures valid build");
            Check(uiData.seed == 0 && uiData.coreHPSettingID == 2 && ui.TurretCount > 0, "UI build connection");
            var firingBuild = new ShipBuildData();
            firingBuild.weaponCounts = new[] { 2, 2, 2, 4, 0, 0, 0 };
            Call(ui, "BeginMovementTrial", firingBuild);
            var trial = (BattleshipInstance)typeof(TilingTestScene).GetField("movementTrial", Hidden).GetValue(ui);
            var rounds = (GameObject)typeof(TilingTestScene).GetField("movementTrialProjectiles", Hidden).GetValue(ui);
            var camera = (Camera)typeof(TilingTestScene).GetField("viewCamera", Hidden).GetValue(ui);
            var screenPoint = new Vector2(camera.pixelRect.center.x, camera.pixelRect.yMin + camera.pixelRect.height * .7f);
            foreach (var section in trial.GetComponentsInChildren<BattleshipSection>()) {
                var runtime = typeof(BattleshipSection).GetField("weapon", Hidden).GetValue(section);
                if (runtime != null) runtime.GetType().GetField("Spread").SetValue(runtime, 0f);
            }
            Call(ui, "TickMovementTrialFire", 0f, false, screenPoint);
            Check(rounds.transform.childCount == 0, "manual fire requires mouse press");
            Call(ui, "TickMovementTrialFire", 0f, true, new Vector2(20, Screen.height - 10));
            var returnRect = StellarSystem.ShipConstructionReturnUI.PanelRect;
            Call(ui, "TickMovementTrialFire", 0f, true, new Vector2(returnRect.center.x, Screen.height - returnRect.center.y));
            Check(rounds.transform.childCount == 0, $"UI clicks do not fire (screen {Screen.width}x{Screen.height}, viewport {camera.pixelRect}, return {returnRect}, rounds {rounds.transform.childCount})");
            Call(ui, "TickMovementTrialFire", 0f, true, screenPoint);
            int volleyCount = rounds.transform.childCount;
            Check(volleyCount == trial.GetComponentsInChildren<BattleshipSection>().Count(s => s.Role == BattleshipSectionRole.Turret), "ready turrets fire one volley");
            Check(volleyCount > 0, "volley contains rounds");
            var aimRay = camera.ScreenPointToRay(screenPoint);
            new Plane(Vector3.forward, Vector3.zero).Raycast(aimRay, out float aimDistance);
            Vector2 aim = aimRay.GetPoint(aimDistance);
            foreach (var round in rounds.GetComponentsInChildren<BattleshipProjectile>()) {
                var shotDirection = (Vector2)typeof(BattleshipProjectile).GetField("direction", Hidden).GetValue(round);
                Near(Vector2.Dot(shotDirection, (aim - (Vector2)round.transform.position).normalized), 1, "round aims at mouse world position");
                Check((Vector2)typeof(BattleshipProjectile).GetField("fixedAimPoint", Hidden).GetValue(round) == aim, "fixed missile aim point");
            }
            Call(ui, "TickMovementTrialFire", 0f, true, screenPoint);
            Check(rounds.transform.childCount == volleyCount, "held fire respects reload");
            Call(ui, "TickMovementTrialFire", 10f, false, screenPoint);
            Call(ui, "TickMovementTrialFire", 0f, true, screenPoint);
            Check(rounds.transform.childCount == volleyCount * 2, "idle reload stores only one volley");
            Vector3 shotPosition = rounds.transform.GetChild(0).position;
            trial.transform.position += Vector3.right * 100;
            Check(rounds.transform.GetChild(0).position == shotPosition, "fired rounds do not follow ship transform");
            Call(ui, "StopMovementTrial");
            Check(!rounds.activeSelf, "ending trial clears its projectiles");
            Debug.Log("CONSTRUCTION_MOUSE_FIRE_PASS: always available, click/UI gating, world aim, reload, projectile independence and cleanup.");
            Debug.Log("CONSTRUCTION_MOVEMENT_CHECKS_PASS: formulas, validation, save isolation, reproducible hull, section presets, UI generation, four speeds, turn, damage, EMP, boundary.");
        }
        finally
        {
            if (preview != null) UnityEngine.Object.DestroyImmediate(preview);
            if (a != null) UnityEngine.Object.DestroyImmediate(a.gameObject);
            if (b != null) UnityEngine.Object.DestroyImmediate(b.gameObject);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            if (existed) PlayerPrefs.SetString(ShipBuildStore.Key, old); else PlayerPrefs.DeleteKey(ShipBuildStore.Key);
            if (legacyExisted) PlayerPrefs.SetString(ShipBuildStore.LegacyKey, oldLegacy); else PlayerPrefs.DeleteKey(ShipBuildStore.LegacyKey);
            PlayerPrefs.Save();
        }
    }
}
