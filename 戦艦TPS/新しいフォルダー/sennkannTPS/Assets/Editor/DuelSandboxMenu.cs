using Script;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DuelSandboxMenu
{
    [MenuItem("戦艦TPS/試験/1対1戦闘（工程08）")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CreateScene();
        EditorApplication.EnterPlaymode();
    }

    public static void CreateScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Duel camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 2200;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .025f, .04f);
        new GameObject("Duel sandbox").AddComponent<DuelSandbox>();
    }
}
