using UnityEngine;

public sealed class BattleSceneTransitionMonitor : MonoBehaviour
{
    [SerializeField] private SceneNavigation sceneNavigation;
    [SerializeField, Min(0f)] private float transitionDelaySeconds = 10f;

    private float elapsedSeconds;
    private bool transitionRequested;

    private void Start()
    {
        if (sceneNavigation == null)
        {
            Debug.LogError("SceneNavigation is not assigned.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (transitionRequested)
            return;

        elapsedSeconds += Time.unscaledDeltaTime;
        if (elapsedSeconds < transitionDelaySeconds)
            return;

        transitionRequested = true;
        sceneNavigation.ReturnToMainMenu();
    }
}
