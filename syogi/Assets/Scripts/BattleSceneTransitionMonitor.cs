using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BattleSceneTransitionMonitor : MonoBehaviour
{
    [SerializeField] private SceneNavigation sceneNavigation;
    [SerializeField, Min(0f)] private float transitionDelaySeconds = 10f;

    public float elapsedSeconds;
    private bool transitionRequested;
    private string currentSceneName;

    public float score = 0f;

    private void Start()
    {
        if (sceneNavigation == null)
        {
            Debug.LogError("SceneNavigation is not assigned.", this);
            enabled = false;
            return;
        }

        currentSceneName = SceneManager.GetActiveScene().name;
        if (sceneNavigation.Scenes == null ||
            System.Array.FindIndex(sceneNavigation.Scenes,
                state => state != null && state.sceneName == currentSceneName) < 0)
        {
            Debug.LogError($"Current scene '{currentSceneName}' is not found in Scenes array.", this);
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (transitionRequested)
            return;

        // Debug: return after ten real-time seconds.
        elapsedSeconds += Time.unscaledDeltaTime;
        if (elapsedSeconds < transitionDelaySeconds)
            return;

        transitionRequested = true;
        if (score > 80f)
            sceneNavigation.UnlockNextStage(currentSceneName);

        sceneNavigation.ReturnToMainMenu();
    }
}
