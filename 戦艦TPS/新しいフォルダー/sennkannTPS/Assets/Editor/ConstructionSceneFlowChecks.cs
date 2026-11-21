using System;
using System.Reflection;
using Script;
using StellarSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run on a validation copy. Uses the same save/return action as the UI button.
public static class ConstructionSceneFlowChecks
{
    private const string Pending = "ConstructionSceneFlowChecks.Pending";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int stage, nextFrame;
    private static bool backedUp, existed, legacyExisted;
    private static string original, originalLegacy, draftSave;
    private static ShipBuildData expected;
    private static double deadline;

    public static void RunInPlayMode()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/StellarSystem.unity");
        SessionState.SetBool(Pending, true);
        Install();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Install()
    {
        EditorApplication.update -= Advance;
        if (!SessionState.GetBool(Pending, false)) return;
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Advance;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Advance()
    {
        try {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Construction scene round trip timed out");
            if (Application.isBatchMode) { EditorApplication.isPaused = false; Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate(); }
            if (!Application.isPlaying || Time.frameCount < Math.Max(5, nextFrame)) return;
            if (stage == 0) {
                existed = PlayerPrefs.HasKey(ShipBuildStore.Key); original = PlayerPrefs.GetString(ShipBuildStore.Key);
                legacyExisted = PlayerPrefs.HasKey(ShipBuildStore.LegacyKey); originalLegacy = PlayerPrefs.GetString(ShipBuildStore.LegacyKey);
                backedUp = true;
                Check(ShipBuildStore.TrySave(new ShipBuildData(), out var error), error);
                draftSave = PlayerPrefs.GetString(ShipBuildStore.Key);
                var clock = UnityEngine.Object.FindFirstObjectByType<SystemController>();
                Check(clock != null, "Map clock missing");
                clock.StopAt(123); clock.SortieTime = 45; clock.SetTimeScale(100);
                OpenConstruction();
            } else if (stage == 1) {
                Check(SceneManager.GetActiveScene().name == SceneTransitionManager.ConstructionSceneName, "Construction scene not loaded");
                var ui = UnityEngine.Object.FindFirstObjectByType<TilingTestScene>();
                var exit = UnityEngine.Object.FindFirstObjectByType<ShipConstructionReturnUI>();
                Check(ui != null && exit != null, "Construction components missing");
                expected = new ShipBuildData { seed = 42, coreHPSettingID = 2, baseSpeed = 350,
                    generatorSpeed = 1.23459f, heatGainReduction = .12349f, emCapOffset = -49 };
                typeof(TilingTestScene).GetMethod("ApplySavedBuild", Hidden).Invoke(ui, new object[] { expected });
                // Simulate typed precision, rather than the four-decimal loaded display.
                typeof(TilingTestScene).GetField("generatorSpeedText", Hidden).SetValue(ui, "1.23459");
                typeof(TilingTestScene).GetField("heatReductionText", Hidden).SetValue(ui, "0.12349");
                typeof(TilingTestScene).GetField("targetText", Hidden).SetValue(ui, "13");
                Check(!exit.TryCommitAndReturn(out _), "Invalid design allowed return");
                Check(SceneManager.GetActiveScene().name == SceneTransitionManager.ConstructionSceneName, "Invalid design changed scene");
                Check(PlayerPrefs.GetString(ShipBuildStore.Key) == draftSave, "Draft overwrote committed design");
                typeof(TilingTestScene).GetField("targetText", Hidden).SetValue(ui, "12");
                Check(expected.TryCreateConstructionSnapshot(out expected, out var error), error);
                Check(exit.TryCommitAndReturn(out error), error);
            } else if (stage == 2) {
                Check(SceneManager.GetActiveScene().name == SceneTransitionManager.MapSceneName, "Map return failed");
                var clock = UnityEngine.Object.FindFirstObjectByType<SystemController>();
                Check(clock != null && clock.currentTime == 123 && clock.SortieTime == 45 && clock.timeScale == 100 && clock.isPaused,
                    "Construction did not preserve map time and pause state");
                Check(ShipBuildStore.TryLoad(out var loaded), "Committed design missing");
                Check(JsonUtility.ToJson(loaded) == JsonUtility.ToJson(expected), "Saved design differs from normalized draft");
                OpenConstruction();
            } else {
                var ui = UnityEngine.Object.FindFirstObjectByType<TilingTestScene>();
                Check(ui != null, "Reopened construction missing");
                Check(ui.TryGetBuild(out var loaded, out var error), error);
                Check(JsonUtility.ToJson(loaded) == JsonUtility.ToJson(expected), "Reopened input fields differ from saved design");
                Debug.Log("CONSTRUCTION_SCENE_FLOW_PASS: invalid draft protected, valid save/return, map clock preserved, reopened design restored.");
                Finish(0); return;
            }
            stage++; nextFrame = Time.frameCount + 5;
        } catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }

    private static void OpenConstruction()
    {
        var map = UnityEngine.Object.FindFirstObjectByType<StellarSystemMap>();
        var clock = UnityEngine.Object.FindFirstObjectByType<SystemController>();
        Check(SceneTransitionManager.OpenConstruction(map, clock, out var error), error);
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Advance;
        SessionState.SetBool(Pending, false);
        if (backedUp) {
            if (existed) PlayerPrefs.SetString(ShipBuildStore.Key, original); else PlayerPrefs.DeleteKey(ShipBuildStore.Key);
            if (legacyExisted) PlayerPrefs.SetString(ShipBuildStore.LegacyKey, originalLegacy); else PlayerPrefs.DeleteKey(ShipBuildStore.LegacyKey);
            PlayerPrefs.Save();
        }
        EditorApplication.Exit(code);
    }
}
