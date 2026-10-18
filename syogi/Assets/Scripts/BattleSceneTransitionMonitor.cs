using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BattleSceneTransitionMonitor : MonoBehaviour
{
    [SerializeField] private SceneNavigation sceneNavigation;
    [SerializeField, Min(0f)] private float transitionDelaySeconds = 10f;
    [SerializeField] private bool autoFinishAfterDelay;
    [SerializeField] private CardCatalog cardCatalog;
    [SerializeField, Min(1)] private int stageID = 1;

    public float elapsedSeconds;
    private bool transitionRequested;
    private string currentSceneName;

    public float score = 0f;
    public float isClearThreshold = 80f;
    public bool isClear=> score >= isClearThreshold;
    public bool isComplete => score >= 100f;

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
        if (transitionRequested || !autoFinishAfterDelay) return;

        // Debug: return after ten real-time seconds.
        elapsedSeconds += Time.unscaledDeltaTime;
        if (elapsedSeconds < transitionDelaySeconds)
            return;

        FinishBattle();
    }

    // Call once from the battle completion condition or a UI button.
    [ContextMenu("Finish Battle")]
    public void FinishBattle()
    {
        if (!Application.isPlaying || transitionRequested) return;
        if (sceneNavigation == null || cardCatalog == null ||
            !Application.CanStreamedLevelBeLoaded("main menu"))
        {
            Debug.LogError("Battle completion requires navigation, a card catalog and the main menu scene.", this);
            return;
        }
        CardInventory inventory = CardSelectionSession.GetInventory(cardCatalog);
        if (!inventory.TryAddDrop(score, stageID, out CardData drop, out string error))
        {
            Debug.LogError(error, this);
            return;
        }
        transitionRequested = true;
        Debug.Log($"Card dropped: {drop.displayName} ({inventory.Candidates.Count}/{CardInventory.Capacity})", this);
        if (isClear)
            sceneNavigation.UnlockNextStage(currentSceneName);

        sceneNavigation.ReturnToMainMenu();
    }
}
