using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameSceneManager
{
    public const string MainMenuSceneName = "Mainmenu";
    public const string SettingSceneName = "Setting";
    public const string StageSelectSceneName = "StageSelect";
    public const string StageSceneName = "Stage";

    public static int StageId { get; private set; }
    public static bool TeacherModeEnabled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureScene(SceneManager.GetActiveScene());
    }

    public static void LoadMainMenu()
    {
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public static void LoadSetting()
    {
        SceneManager.LoadScene(SettingSceneName);
    }

    public static void LoadStageSelect()
    {
        SceneManager.LoadScene(StageSelectSceneName);
    }

    public static void LoadStage()
    {
        SceneManager.LoadScene(StageSceneName);
    }

    public static void LoadStage(int stageId)
    {
        SetStage(stageId);
        LoadStage();
    }

    public static void SetStage(int stageId)
    {
        StageId = stageId;
        RefreshStageDescription();
    }

    public static void SetTeacherModeEnabled(bool enabled)
    {
        TeacherModeEnabled = enabled;
    }

    public static string GetStageDescription(int stageId)
    {
        return BulletHellStageAttackDefinitions.GetStage(stageId)?.Description ??
               string.Empty;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureScene(scene);
    }

    private static void ConfigureScene(Scene scene)
    {
        switch (scene.name)
        {
            case MainMenuSceneName:
                BindButton("Setting", LoadSetting);
                BindButton("Basic Challenge", LoadStageSelect);
                break;
            case SettingSceneName:
                BindButton("Back", LoadMainMenu);
                SettingView.Build();
                break;
            case StageSelectSceneName:
                BindButton("back", LoadMainMenu);
                BindButton("Start", LoadStage);
                StageSelectView.Build();
                RefreshStageDescription();
                break;
            case StageSceneName:
                BindButton("back", LoadStageSelect);
                StageView.Build();
                ConfigureStageSpawner();
                break;
        }
    }

    private static void BindButton(string label, UnityEngine.Events.UnityAction action)
    {
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (Button button in buttons)
        {
            TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
            if (buttonText == null ||
                !string.Equals(buttonText.text.Trim(), label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
            return;
        }

        Debug.LogWarning($"Scene button was not found: {label}");
    }

    private static void RefreshStageDescription()
    {
        if (SceneManager.GetActiveScene().name != StageSelectSceneName)
        {
            return;
        }

        GameObject descriptionObject = GameObject.Find("Discription");
        TMP_Text description = descriptionObject != null
            ? descriptionObject.GetComponentInChildren<TMP_Text>(true)
            : null;

        if (description == null)
        {
            Debug.LogWarning("Discription TMP was not found in StageSelect scene.");
            return;
        }

        description.text = GetStageDescription(StageId);
    }

    private static void ConfigureStageSpawner()
    {
        StageSpawnManager spawnManager = UnityEngine.Object.FindAnyObjectByType<StageSpawnManager>();
        if (spawnManager == null)
        {
            GameObject managerObject = new GameObject(nameof(StageSpawnManager));
            spawnManager = managerObject.AddComponent<StageSpawnManager>();
        }

        spawnManager.Initialize(StageId, TeacherModeEnabled);
    }
}
