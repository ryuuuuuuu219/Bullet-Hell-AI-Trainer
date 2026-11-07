using System;
using System.IO;
using Aquarium.Tiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TilingTestSceneTools
{
    public const string ScenePath = "Assets/Scenes/TilingTest.unity";
    private const string PlayCheckKey = "Aquarium.Tiling.PlayCheck";
    private static double checkStarted;
    private static bool playError;

    public static void ValidatePlayMode()
    {
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(PlayCheckKey, true);
        RegisterPlayCheck();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void RegisterPlayCheck()
    {
        if (!SessionState.GetBool(PlayCheckKey, false)) return;
        checkStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update -= CheckPlayMode;
        EditorApplication.update += CheckPlayMode;
        Application.logMessageReceived -= CheckLog;
        Application.logMessageReceived += CheckLog;
    }
    private static void CheckLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) playError = true;
    }
    private static void CheckPlayMode()
    {
        bool timeout = EditorApplication.timeSinceStartup - checkStarted > 90;
        var test = EditorApplication.isPlaying ? UnityEngine.Object.FindAnyObjectByType<TilingTestScene>() : null;
        bool ready = test != null && test.HasFinished && test.TurretCount > 0 && test.ActiveTestBulletCount > 0;
        if (!timeout && !playError && !ready) return;
        bool passed = !timeout && !playError && test != null && test.DisplayedTileCount > 0
            && test.GetComponentsInChildren<MeshFilter>().Length == test.DisplayedTileCount
            && test.TurretCount > 0 && test.ActiveTestBulletCount > 0;
        SessionState.SetBool(PlayCheckKey, false);
        EditorApplication.update -= CheckPlayMode;
        Application.logMessageReceived -= CheckLog;
        Debug.Log(passed ? $"TILING_PLAYMODE_VALIDATED tiles={test.DisplayedTileCount} turrets={test.TurretCount} bullets={test.ActiveTestBulletCount}"
            : "TILING_PLAYMODE_FAILED");
        EditorApplication.Exit(passed ? 0 : 1);
    }

    [MenuItem("Tools/Aquarium/Open Tiling Test Scene")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) Create();
        else EditorSceneManager.OpenScene(ScenePath);
    }

    private static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Tiling Test");
        var component = root.AddComponent<TilingTestScene>();
        var serialized = new SerializedObject(component);
        serialized.FindProperty("tileShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Tiling/TilingTest.shader");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Batch entry point: create the actual scene, compile/import and exercise geometry + mesh setup.
    public static void CreateAndValidate()
    {
        if (!File.Exists(ScenePath)) Create();
        else EditorSceneManager.OpenScene(ScenePath);
        var test = UnityEngine.Object.FindAnyObjectByType<TilingTestScene>();
        if (test == null) throw new Exception("Test scene component is missing.");
        var shader = Shader.Find("Aquarium/TilingTest");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Tiling shader failed import.");
        test.GenerateForValidation();
        if (!test.HasFinished || test.DisplayedTileCount < 1) throw new Exception("Generation did not finish.");
        if (test.GetComponentsInChildren<MeshFilter>().Length != test.DisplayedTileCount) throw new Exception("Missing section meshes.");
        if (test.GetComponentInChildren<Camera>() == null) throw new Exception("Missing camera.");
        Debug.Log("TILING_SCENE_VALIDATED tiles=" + test.DisplayedTileCount);
        // The preview objects are runtime-only; do not save them into the scene.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}
