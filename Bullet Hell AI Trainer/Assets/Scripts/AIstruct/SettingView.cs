using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class SettingView
{
    private static readonly string[] InputLabels =
    {
        "近接認識",
        "扇形センサー",
        "予告線センサー",
    };

    private static readonly string[] RewardLabels =
    {
        "与ダメージ係数",
        "生存時間係数",
        "画面端衝突累計時間係数",
        "画面中央からの距離係数",
    };

    private static readonly string[] GeneticSaveAxisLabels =
    {
        "与ダメージ",
        "生存時間",
        "画面端衝突累計時間",
        "画面中央からの距離総和",
    };

    private static readonly bool[] InputEnabled = { true, true, true };
    private static readonly float[] RewardWeights = { 0.3f, 1f, -5f, -0.5f };
    private static readonly float[] RewardMinimums = { 0f, 0f, -10f, -10f };
    private static readonly float[] RewardMaximums = { 10f, 10f, 0f, 0f };
    private static GeneticSaveEvaluationAxis geneticSaveEvaluationAxis =
        GeneticSaveEvaluationAxis.SurvivalTime;

    private static GameObject settingItemTemplate;

    public static int PopulationSize { get; private set; } = populationSetting.MaximumPopulationSize;
    public static float MutationRate { get; private set; } = 0.01f;
    public static bool AdvanceWhenAllIndividualsAreHit { get; private set; } = true;
    public static int PendingManualGenerationRequests { get; private set; }

    public static bool IsInputEnabled(int index)
    {
        return index >= 0 && index < InputEnabled.Length && InputEnabled[index];
    }

    public static float GetRewardWeight(int index)
    {
        return index >= 0 && index < RewardWeights.Length
            ? RewardWeights[index]
            : 0f;
    }

    public static bool ConsumeManualGenerationRequest()
    {
        PopulationSettingsData data = populationSetting.LoadData();
        if (data.pendingManualGenerationRequests <= 0)
        {
            return false;
        }

        data.pendingManualGenerationRequests--;
        populationSetting.SaveData(data);
        PendingManualGenerationRequests = data.pendingManualGenerationRequests;
        return true;
    }

    public static void Build()
    {
        LoadSavedSettings();

        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("Setting scene Canvas was not found.");
            return;
        }

        if (canvas.transform.Find("Setting Controls") != null)
        {
            return;
        }

        settingItemTemplate = FindAndHideSettingItemTemplate(canvas);
        if (settingItemTemplate == null)
        {
            Debug.LogWarning("Setting item Image.prefab instance was not found in Setting scene.");
            return;
        }

        ScrollRect inputScroll = FindScrollRect("Scroll View");
        ScrollRect rewardScroll = FindScrollRect("Scroll View (1)");
        ScrollRect generationScroll = FindScrollRect("Scroll View (2)");
        if (inputScroll?.content == null ||
            rewardScroll?.content == null ||
            generationScroll?.content == null)
        {
            Debug.LogWarning("Setting scene Input, Reward, or Generation Scroll View Content was not found.");
            return;
        }

        TMP_FontAsset font = FindSettingFont();
        CreateRectObject("Setting Controls", canvas.transform);
        BuildInputButtons(inputScroll.content.gameObject, font);
        BuildRewardButtons(rewardScroll.content.gameObject, font);
        BuildGenerationButtons(generationScroll.content.gameObject, font);
        BuildNetworkView();
        ConfigureSaveButton();
    }

    private static void BuildNetworkView()
    {
        GameObject area = GameObject.Find("Netwark vision area");
        if (area == null)
        {
            Debug.LogWarning("Setting scene Netwark vision area was not found.");
            return;
        }

        View view = area.GetComponent<View>();
        if (view == null)
        {
            view = area.AddComponent<View>();
        }

        view.Rebuild();
    }

    private static void ConfigureSaveButton()
    {
        foreach (Button button in
                 UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null || label.text.Trim() != "保存")
            {
                continue;
            }

            button.gameObject.name = "Save Button";
            button.onClick.RemoveListener(SaveSettingsAndRedrawNetwork);
            button.onClick.AddListener(SaveSettingsAndRedrawNetwork);
            return;
        }

        Debug.LogWarning("Setting scene save button was not found.");
    }

    private static void SaveSettingsAndRedrawNetwork()
    {
        SaveInputSettings();
        SavePopulationSettings();

        View[] views = UnityEngine.Object.FindObjectsByType<View>(FindObjectsInactive.Include);
        if (views.Length == 0)
        {
            BuildNetworkView();
        }
        else
        {
            foreach (View view in views)
            {
                view.Rebuild();
            }
        }

        Debug.Log("Setting data was saved to PlayerPrefs and the network view was rebuilt.");
    }

    private static void BuildInputButtons(GameObject contentObject, TMP_FontAsset font)
    {
        ConfigureContentLayout(contentObject);

        for (int index = 0; index < InputLabels.Length; index++)
        {
            int capturedIndex = index;
            CreateSettingItem(
                contentObject.transform,
                $"Input Item {index}",
                InputLabels[index],
                0f,
                1f,
                InputEnabled[capturedIndex] ? 1f : 0f,
                true,
                value =>
                {
                    InputEnabled[capturedIndex] = value >= 0.5f;
                },
                value => value >= 0.5f ? "ON" : "OFF",
                font);
        }
    }

    private static void ConfigureContentLayout(GameObject contentObject)
    {
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = contentObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = contentObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void BuildRewardButtons(GameObject contentObject, TMP_FontAsset font)
    {
        ConfigureContentLayout(contentObject);

        CreateSettingItem(
            contentObject.transform,
            "Genetic Save Evaluation Axis Item",
            "遺伝子保存評価軸",
            0f,
            GeneticSaveAxisLabels.Length - 1,
            (int)geneticSaveEvaluationAxis,
            true,
            value =>
            {
                geneticSaveEvaluationAxis =
                    (GeneticSaveEvaluationAxis)Mathf.RoundToInt(value);
            },
            value => GeneticSaveAxisLabels[Mathf.Clamp(
                Mathf.RoundToInt(value),
                0,
                GeneticSaveAxisLabels.Length - 1)],
            font,
            preferredHeight: 72f);

        for (int index = 0; index < RewardLabels.Length; index++)
        {
            int capturedIndex = index;
            CreateSettingItem(
                contentObject.transform,
                $"Reward Item {index}",
                RewardLabels[index],
                RewardMinimums[capturedIndex],
                RewardMaximums[capturedIndex],
                RewardWeights[capturedIndex],
                false,
                value =>
                {
                    RewardWeights[capturedIndex] = value;
                },
                value => value.ToString("0.###"),
                font);
        }
    }

    private static void BuildGenerationButtons(GameObject contentObject, TMP_FontAsset font)
    {
        ConfigureContentLayout(contentObject);

        CreateSettingItem(
            contentObject.transform,
            "Population Item",
            "個体数",
            populationSetting.MinimumPopulationSize,
            populationSetting.MaximumPopulationSize,
            PopulationSize,
            true,
            value =>
            {
                PopulationSize = Mathf.RoundToInt(value);
            },
            value => Mathf.RoundToInt(value).ToString(),
            font);

        CreateSettingItem(
            contentObject.transform,
            "Mutation Rate Item",
            "突然変異率",
            0f,
            1f,
            MutationRate,
            false,
            value =>
            {
                MutationRate = value;
            },
            value => value.ToString("0.000"),
            font);

        CreateSettingItem(
            contentObject.transform,
            "Generation Mode Item",
            "全滅時の世代更新",
            0f,
            1f,
            AdvanceWhenAllIndividualsAreHit ? 1f : 0f,
            true,
            value =>
            {
                AdvanceWhenAllIndividualsAreHit = value >= 0.5f;
            },
            value => value >= 0.5f ? "自動" : "任意",
            font,
            preferredHeight: 62f);

    }

    private static ScrollRect FindScrollRect(string objectName)
    {
        foreach (ScrollRect scrollRect in
                 UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Include))
        {
            if (scrollRect.gameObject.name == objectName)
            {
                return scrollRect;
            }
        }

        return null;
    }

    private static GameObject FindAndHideSettingItemTemplate(Canvas canvas)
    {
        HashSet<GameObject> prefabRoots = new HashSet<GameObject>();
        foreach (Slider slider in UnityEngine.Object.FindObjectsByType<Slider>(FindObjectsInactive.Include))
        {
            Transform root = slider.transform;
            while (root.parent != null && root.name != "Image")
            {
                root = root.parent;
            }

            if (root.name == "Image" && root.IsChildOf(canvas.transform))
            {
                prefabRoots.Add(root.gameObject);
            }
        }

        GameObject template = null;
        foreach (GameObject prefabRoot in prefabRoots)
        {
            template ??= prefabRoot;
            prefabRoot.SetActive(false);
        }

        return template;
    }

    private static GameObject CreateSettingItem(
        Transform parent,
        string objectName,
        string label,
        float minimum,
        float maximum,
        float initialValue,
        bool wholeNumbers,
        Action<float> valueChanged,
        Func<float, string> formatValue,
        TMP_FontAsset font,
        float preferredHeight = 62f)
    {
        GameObject item = UnityEngine.Object.Instantiate(settingItemTemplate, parent, false);
        item.name = objectName;
        item.SetActive(true);

        LayoutElement layout = item.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = item.AddComponent<LayoutElement>();
        }

        layout.preferredHeight = preferredHeight;
        layout.minHeight = preferredHeight;

        TMP_Text labelText = null;
        TMP_Text valueText = null;
        foreach (TMP_Text text in item.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name == "Text (TMP)")
            {
                labelText = text;
            }
            else if (text.gameObject.name == "Text (TMP) (1)")
            {
                valueText = text;
            }

            if (font != null)
            {
                text.font = font;
            }
        }

        if (labelText != null)
        {
            labelText.text = label;
        }

        Slider slider = item.GetComponentInChildren<Slider>(true);
        if (slider == null)
        {
            Debug.LogWarning($"Setting item Slider was not found: {objectName}");
            return item;
        }

        slider.onValueChanged.RemoveAllListeners();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = wholeNumbers;
        slider.SetValueWithoutNotify(initialValue);

        void RefreshValue(float value)
        {
            if (valueText != null)
            {
                valueText.text = formatValue(value);
            }
        }

        slider.onValueChanged.AddListener(value =>
        {
            RefreshValue(value);
            valueChanged(value);
        });
        RefreshValue(slider.value);
        return item;
    }

    private static void LoadSavedSettings()
    {
        AiSaveData aiData = Aidata.LoadData();
        InputEnabled[0] = aiData.useProximityInput;
        InputEnabled[1] = aiData.useCircularSensorInput;
        InputEnabled[2] = aiData.useWarningLineInput;

        PopulationSettingsData populationData = populationSetting.LoadData();
        PopulationSize = populationData.populationSize;
        MutationRate = populationData.mutationRate;
        AdvanceWhenAllIndividualsAreHit = populationData.advanceWhenAllIndividualsAreHit;
        PendingManualGenerationRequests = populationData.pendingManualGenerationRequests;
        geneticSaveEvaluationAxis = populationData.geneticSaveEvaluationAxis;
        RewardWeights[0] = populationData.damageWeight;
        RewardWeights[1] = populationData.survivalTimeWeight;
        RewardWeights[2] = populationData.edgeCollisionCumulativeTimeWeight;
        RewardWeights[3] = populationData.centerDistanceSampledSumWeight;
    }

    private static void SaveInputSettings()
    {
        AiSaveData data = Aidata.LoadData();
        data.useProximityInput = InputEnabled[0];
        data.useCircularSensorInput = InputEnabled[1];
        data.useWarningLineInput = InputEnabled[2];
        Aidata.SaveData(data);
    }

    private static void SavePopulationSettings()
    {
        PopulationSettingsData data = populationSetting.LoadData();
        data.populationSize = PopulationSize;
        data.mutationRate = MutationRate;
        data.advanceWhenAllIndividualsAreHit = AdvanceWhenAllIndividualsAreHit;
        data.pendingManualGenerationRequests = PendingManualGenerationRequests;
        data.geneticSaveEvaluationAxis = geneticSaveEvaluationAxis;
        data.damageWeight = RewardWeights[0];
        data.survivalTimeWeight = RewardWeights[1];
        data.edgeCollisionCumulativeTimeWeight = RewardWeights[2];
        data.centerDistanceSampledSumWeight = RewardWeights[3];
        populationSetting.SaveData(data);
    }

    private static TMP_FontAsset FindSettingFont()
    {
        foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            string value = text.text.Trim();
            if (value == "入力" || value == "評価軸" || value == "世代設定")
            {
                return text.font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private static RectTransform CreateRectObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }
}
