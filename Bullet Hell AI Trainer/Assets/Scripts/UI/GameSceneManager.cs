using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum ChallengeCategory
{
    Basic,
    Applied,
    Advanced,
    Final,
}

public static class GameSceneManager
{
    public const string MainMenuSceneName = "Mainmenu";
    public const string SettingSceneName = "Setting";
    public const string StageSelectSceneName = "StageSelect";
    public const string StageSceneName = "Stage";

    public static int StageId { get; private set; }
    public static ChallengeCategory SelectedCategory { get; private set; } =
        ChallengeCategory.Basic;
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

    public static void LoadStageSelect(ChallengeCategory category)
    {
        SelectedCategory = category;
        StageId = 0;
        LoadStageSelect();
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
        return BulletHellStageAttackDefinitions.GetStage(
                   SelectedCategory,
                   stageId)?.Description ??
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
                BindButton("Basic Challenge", LoadBasicChallengeSelect);
                BindButton("Applied Challenge", LoadAppliedChallengeSelect);
                BindButton("Advanced Challenge", LoadAdvancedChallengeSelect);
                BindButton("Final Challenge", LoadFinalChallengeSelect);
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

        Button startButton = FindButton("Start");
        BulletHellStageDefinition selectedStage =
            BulletHellStageAttackDefinitions.GetStage(
                SelectedCategory,
                StageId);
        if (startButton != null)
        {
            startButton.interactable = selectedStage?.IsPlayable ?? false;
        }
    }

    private static void ConfigureStageSpawner()
    {
        StageSpawnManager spawnManager = UnityEngine.Object.FindAnyObjectByType<StageSpawnManager>();
        if (spawnManager == null)
        {
            GameObject managerObject = new GameObject(nameof(StageSpawnManager));
            spawnManager = managerObject.AddComponent<StageSpawnManager>();
        }

        spawnManager.Initialize(
            SelectedCategory,
            StageId,
            TeacherModeEnabled);
    }

    private static Button FindButton(string label)
    {
        foreach (Button button in
                 UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
            if (buttonText != null && string.Equals(
                    buttonText.text.Trim(),
                    label,
                    StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private static void LoadBasicChallengeSelect()
    {
        LoadStageSelect(ChallengeCategory.Basic);
    }

    private static void LoadAppliedChallengeSelect()
    {
        LoadStageSelect(ChallengeCategory.Applied);
    }

    private static void LoadAdvancedChallengeSelect()
    {
        LoadStageSelect(ChallengeCategory.Advanced);
    }

    private static void LoadFinalChallengeSelect()
    {
        LoadStageSelect(ChallengeCategory.Final);
    }
}
