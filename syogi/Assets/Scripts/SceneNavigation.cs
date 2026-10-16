using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneNavigation : MonoBehaviour
{
    [SerializeField] private string[] sceneNames =
    {
        "main menu",
        "battle0",
        "nobel0",
        "debriefing0"
    };

    public void LoadScene(int sceneIndex)
    {
        if (sceneNames == null || sceneIndex < 0 || sceneIndex >= sceneNames.Length)
        {
            Debug.LogError($"Scene index is out of range: {sceneIndex}", this);
            return;
        }

        LoadSceneByName(sceneNames[sceneIndex]);
    }

    public void ReturnToMainMenu()
    {
        LoadSceneByName("main menu");
    }

    private void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene is empty or not available in the build: {sceneName}", this);
            return;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
