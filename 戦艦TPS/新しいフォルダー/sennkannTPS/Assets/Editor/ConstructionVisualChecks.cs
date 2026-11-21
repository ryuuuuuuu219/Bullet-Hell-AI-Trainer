using System;
using System.IO;
using System.Reflection;
using Script;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders the actual construction scene UI; never captures another application.
public static class ConstructionVisualChecks
{
    private const string Pending = "ConstructionVisualChecks.Pending";
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static TilingTestScene view;
    private static int stage, frame;
    private static string destination;
    private static readonly string[] names = { "facilities", "facilities-middle", "facilities-lower", "performance", "performance-lower", "armor" };
    public static void Capture()
    {
        var gameView = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        gameView.position = new Rect(0, 0, 1280, 800);
        EditorSceneManager.OpenScene("Assets/Scenes/ShipConstruction.unity");
        SessionState.SetBool(Pending, true);
        Install();
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Install()
    {
        EditorApplication.update -= Advance;
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Advance;
    }
    private static void Advance()
    {
        if (Application.isBatchMode) { EditorApplication.isPaused = false; Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate(); }
        if (!Application.isPlaying || Time.frameCount < 10) return;
        try {
            if (view == null) {
                view = UnityEngine.Object.FindFirstObjectByType<TilingTestScene>();
                if (view == null) throw new Exception("Construction scene not loaded");
                typeof(TilingTestScene).GetMethod("ApplySavedBuild", Hidden).Invoke(view, new object[] { new ShipBuildData() });
                typeof(TilingTestScene).GetField("seedText", Hidden).SetValue(view, "0");
                typeof(TilingTestScene).GetField("targetText", Hidden).SetValue(view, "12");
                typeof(TilingTestScene).GetField("attemptsText", Hidden).SetValue(view, "160");
                view.GenerateForValidation();
                destination = Path.GetFullPath("../ConstructionQAImages");
                Directory.CreateDirectory(destination);
                stage = 0; SetView();
            }
            if (Time.frameCount < frame + 12) return;
            if (stage % 2 == 0) {
                string path = Path.Combine(destination, names[stage / 2] + ".png");
                ScreenCapture.CaptureScreenshot(path);
                stage++; frame = Time.frameCount;
            } else {
                string path = Path.Combine(destination, names[stage / 2] + ".png");
                if (!File.Exists(path)) {
                    if (Time.frameCount > frame + 120) throw new Exception("UI rendering did not produce " + path);
                    return;
                }
                stage++;
                if (stage == names.Length * 2) {
                    Debug.Log($"CONSTRUCTION_UI_CAPTURE_PASS: {Screen.width}x{Screen.height}, {destination}");
                    Finish(0); return;
                }
                SetView();
            }
        } catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }
    private static void SetView()
    {
        int index = stage / 2;
        var tab = typeof(TilingTestScene).GetField("constructionTab", Hidden);
        tab.SetValue(view, Enum.ToObject(tab.FieldType, index <= 2 ? 3 : index <= 4 ? 4 : 2));
        typeof(TilingTestScene).GetField("panelScroll", Hidden).SetValue(view, new Vector2(0, index == 1 ? 130 : index == 2 || index == 4 ? 10000 : 0));
        frame = Time.frameCount;
    }
    private static void Finish(int code)
    {
        EditorApplication.update -= Advance;
        SessionState.SetBool(Pending, false);
        EditorApplication.Exit(code);
    }
}
