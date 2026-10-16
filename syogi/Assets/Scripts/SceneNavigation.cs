using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneNavigation : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
