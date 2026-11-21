using Script;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ShipSteeringSandboxMenu
{
    [MenuItem("戦艦TPS/試験/操艦AI 7種類（工程04）")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CreateScene();
        EditorApplication.EnterPlaymode();
    }

    private static void CreateScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("AI test camera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 6500;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .025f, .04f);
        new GameObject("AI sandbox").AddComponent<ShipSteeringSandbox>();
    }

    public static void CaptureBatch()
    {
        var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        view.position = new Rect(0, 0, 1280, 800);
        CreateScene();
        SessionState.SetBool("SteeringSandbox.Capture", true);
        Install();
        EditorApplication.EnterPlaymode();
    }

    private static int capturedFrame;
    [InitializeOnLoadMethod]
    private static void Install()
    {
        EditorApplication.update -= Capture;
        if (SessionState.GetBool("SteeringSandbox.Capture", false)) EditorApplication.update += Capture;
    }

    private static void Capture()
    {
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!Application.isPlaying || Time.frameCount < 90) return;
        try
        {
            string path = System.IO.Path.GetFullPath("../ConstructionQAImages/steering-ai.png");
            if (capturedFrame == 0)
            {
                var ai = Object.FindFirstObjectByType<ShipSteeringAI>();
                if (ai == null || ai.ActiveType != AIType.TOr || ai.GetComponent<ShipMovement>().Velocity.magnitude < 249)
                    throw new System.Exception("Sandbox Update integration failed");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                ScreenCapture.CaptureScreenshot(path);
                capturedFrame = Time.frameCount;
                return;
            }
            if (Time.frameCount < capturedFrame + 15) return;
            if (!System.IO.File.Exists(path)) throw new System.Exception("Sandbox capture missing");
            Debug.Log("SHIP_STEERING_SANDBOX_PASS: runtime Update integration and rendered test UI.");
            Finish(0);
        }
        catch (System.Exception ex) { Debug.LogException(ex); Finish(1); }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool("SteeringSandbox.Capture", false);
        EditorApplication.update -= Capture;
        EditorApplication.Exit(code);
    }
}
