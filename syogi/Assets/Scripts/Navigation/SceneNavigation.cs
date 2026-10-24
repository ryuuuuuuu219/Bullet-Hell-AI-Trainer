using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class UnlockState
{
    public bool isUnlocked;
    public string sceneName;
}

[System.Serializable]
public class StageButtonGroup
{
    public string sceneName;
    public GameObject[] buttons = new GameObject[3];
}

public sealed class SceneNavigation : MonoBehaviour
{
    [SerializeField] private string[] sceneNames =
    {
        "main menu",
        "battle0",
        "nobel0",
        "debriefing0"
    };

    [SerializeField] private StageButtonGroup[] toStoryButtons = new StageButtonGroup[0];

    public UnlockState[] Scenes =
    {
        new UnlockState { isUnlocked = true, sceneName = "battle0" },
        new UnlockState { isUnlocked = false, sceneName = "battle1" }
    };

    // Shared for this play session; starting a new session restores Inspector defaults.
    private static readonly Dictionary<string, UnlockState> sessionStates =
        new Dictionary<string, UnlockState>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        sessionStates.Clear();
    }

    private void Awake()
    {
        if (Scenes == null)
        {
            Debug.LogError("Scenes is not assigned.", this);
            return;
        }

        var names = new HashSet<string>();
        for (int i = 0; i < Scenes.Length; i++)
        {
            UnlockState state = Scenes[i];
            if (state == null || string.IsNullOrWhiteSpace(state.sceneName) || !names.Add(state.sceneName))
            {
                Debug.LogError($"Invalid or duplicate stage at index {i}.", this);
                continue;
            }

            if (!sessionStates.TryGetValue(state.sceneName, out UnlockState shared))
            {
                shared = new UnlockState { sceneName = state.sceneName, isUnlocked = state.isUnlocked };
                sessionStates.Add(state.sceneName, shared);
            }
            Scenes[i] = shared;
        }
        RefreshStageButtons();
    }

    public void RefreshStageButtons()
    {
        if (toStoryButtons == null)
            return;

        foreach (StageButtonGroup group in toStoryButtons)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.sceneName) ||
                !sessionStates.TryGetValue(group.sceneName, out UnlockState state))
            {
                Debug.LogError("Button group has an unknown stage.", this);
                continue;
            }
            if (group.buttons == null || group.buttons.Length != 3)
            {
                Debug.LogError($"Stage '{group.sceneName}' must have exactly three buttons.", this);
                continue;
            }
            foreach (GameObject button in group.buttons)
            {
                if (button == null)
                {
                    Debug.LogError($"Stage '{group.sceneName}' has an unassigned button.", this);
                    continue;
                }
                button.SetActive(state.isUnlocked);
            }
        }
    }

    public void UnlockNextStage(string currentSceneName)
    {
        if (Scenes == null)
            return;

        int index = System.Array.FindIndex(Scenes,
            state => state != null && state.sceneName == currentSceneName);
        if (index < 0)
        {
            Debug.LogError($"Unknown stage: {currentSceneName}", this);
            return;
        }
        if (index + 1 >= Scenes.Length)
            return; // The final stage has no successor.

        UnlockState next = Scenes[index + 1];
        if (next == null || string.IsNullOrWhiteSpace(next.sceneName))
        {
            Debug.LogError("The next stage is invalid.", this);
            return;
        }
        next.isUnlocked = true;
        sessionStates[next.sceneName] = next;
        RefreshStageButtons();
    }

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
