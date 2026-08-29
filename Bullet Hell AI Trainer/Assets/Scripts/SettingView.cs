using System;
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
        "DPS",
        "生存期間",
        "画面端への衝突時間",
        "画面端との距離",
    };

    private static readonly bool[] InputEnabled = { true, true, true };
    private static readonly bool[] RewardEnabled = { true, true, true, true };

    private static readonly Color EnabledColor = new Color(0.45f, 0.72f, 0.92f, 1f);
    private static readonly Color DisabledColor = new Color(0.38f, 0.38f, 0.38f, 1f);
    private static readonly Color ActionColor = new Color(0.86f, 0.86f, 0.86f, 1f);
    private static readonly Color TextColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    public static int PopulationSize { get; private set; } = populationSetting.MaximumPopulationSize;
    public static bool AdvanceWhenAllIndividualsAreHit { get; private set; } = true;
    public static int PendingManualGenerationRequests { get; private set; }

    public static bool IsInputEnabled(int index)
    {
        return index >= 0 && index < InputEnabled.Length && InputEnabled[index];
    }

    public static bool IsRewardEnabled(int index)
    {
        return index >= 0 && index < RewardEnabled.Length && RewardEnabled[index];
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
        GameObject inputContent = GameObject.Find("Content");
        if (canvas == null || inputContent == null)
        {
            Debug.LogWarning("Setting scene Canvas or Input Content was not found.");
            return;
        }

        if (canvas.transform.Find("Setting Controls") != null)
        {
            return;
        }

        TMP_FontAsset font = FindSettingFont();
        BuildInputButtons(inputContent, font);

        RectTransform controls = CreateRectObject("Setting Controls", canvas.transform);
        controls.anchorMin = new Vector2(0.5f, 0.5f);
        controls.anchorMax = new Vector2(0.5f, 0.5f);
        controls.anchoredPosition = Vector2.zero;
        controls.sizeDelta = Vector2.zero;

        BuildRewardButtons(controls, font);
        BuildGenerationButtons(controls, font);
    }

    private static void BuildInputButtons(GameObject contentObject, TMP_FontAsset font)
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

        for (int index = 0; index < InputLabels.Length; index++)
        {
            int capturedIndex = index;
            CreateToggleButton(
                contentObject.transform,
                $"Input Button {index}",
                InputLabels[index],
                () => InputEnabled[capturedIndex],
                () =>
                {
                    InputEnabled[capturedIndex] = !InputEnabled[capturedIndex];
                    SaveInputSettings();
                },
                font);
        }
    }

    private static void BuildRewardButtons(Transform parent, TMP_FontAsset font)
    {
        RectTransform panel = CreateVerticalPanel(
            "Reward Buttons",
            parent,
            new Vector2(-224.58f, 245f),
            new Vector2(330f, 330f));

        for (int index = 0; index < RewardLabels.Length; index++)
        {
            int capturedIndex = index;
            CreateToggleButton(
                panel,
                $"Reward Button {index}",
                RewardLabels[index],
                () => RewardEnabled[capturedIndex],
                () =>
                {
                    RewardEnabled[capturedIndex] = !RewardEnabled[capturedIndex];
                    SavePopulationSettings();
                },
                font);
        }
    }

    private static void BuildGenerationButtons(Transform parent, TMP_FontAsset font)
    {
        RectTransform panel = CreateVerticalPanel(
            "Generation Buttons",
            parent,
            new Vector2(-224.58f, -245f),
            new Vector2(330f, 330f));

        RectTransform populationRow = CreateRectObject("Population Row", panel);
        LayoutElement rowLayout = populationRow.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 62f;

        HorizontalLayoutGroup horizontal = populationRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 8f;
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;

        CreateActionButton(populationRow, "Population Minus", "－", () =>
        {
            PopulationSize = Mathf.Max(
                populationSetting.MinimumPopulationSize,
                PopulationSize - 1);
            SavePopulationSettings();
            RefreshPopulationLabel(populationRow);
        }, font, 62f);

        CreateLabel(populationRow, "Population Value", GetPopulationLabel(), font, 180f);

        CreateActionButton(populationRow, "Population Plus", "＋", () =>
        {
            PopulationSize = Mathf.Min(
                populationSetting.MaximumPopulationSize,
                PopulationSize + 1);
            SavePopulationSettings();
            RefreshPopulationLabel(populationRow);
        }, font, 62f);

        CreateToggleButton(
            panel,
            "Generation Mode",
            "全滅時の世代更新",
            () => AdvanceWhenAllIndividualsAreHit,
            () =>
            {
                AdvanceWhenAllIndividualsAreHit = !AdvanceWhenAllIndividualsAreHit;
                SavePopulationSettings();
            },
            font,
            enabledSuffix: "自動",
            disabledSuffix: "任意");

        Button manualButton = CreateActionButton(
            panel,
            "Next Generation",
            "次世代へ",
            null,
            font);

        manualButton.onClick.AddListener(() =>
        {
            PendingManualGenerationRequests++;
            SavePopulationSettings();
            TMP_Text text = manualButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = $"次世代へ（要求 {PendingManualGenerationRequests}）";
            }
        });
    }

    private static RectTransform CreateVerticalPanel(
        string name,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        RectTransform panel = CreateRectObject(name, parent);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = anchoredPosition;
        panel.sizeDelta = size;

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return panel;
    }

    private static Button CreateToggleButton(
        Transform parent,
        string objectName,
        string label,
        Func<bool> getValue,
        Action toggleValue,
        TMP_FontAsset font,
        string enabledSuffix = "ON",
        string disabledSuffix = "OFF")
    {
        Button button = CreateActionButton(parent, objectName, string.Empty, null, font);

        void Refresh()
        {
            bool enabled = getValue();
            button.image.color = enabled ? EnabledColor : DisabledColor;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = $"{label}：{(enabled ? enabledSuffix : disabledSuffix)}";
                text.color = enabled ? TextColor : Color.white;
            }
        }

        button.onClick.AddListener(() =>
        {
            toggleValue();
            Refresh();
        });
        Refresh();
        return button;
    }

    private static Button CreateActionButton(
        Transform parent,
        string objectName,
        string label,
        Action action,
        TMP_FontAsset font,
        float preferredWidth = -1f)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));

        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = ActionColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        if (action != null)
        {
            button.onClick.AddListener(() => action());
        }

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 62f;
        layout.minHeight = 52f;
        if (preferredWidth > 0f)
        {
            layout.preferredWidth = preferredWidth;
            layout.minWidth = preferredWidth;
        }

        CreateButtonLabel(buttonObject.transform, label, font);
        return button;
    }

    private static void CreateButtonLabel(Transform parent, string label, TMP_FontAsset font)
    {
        RectTransform textRect = CreateRectObject("Label", parent);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 5f);
        textRect.offsetMax = new Vector2(-8f, -5f);

        TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextColor;
        text.fontSize = 22f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 22f;
        text.raycastTarget = false;
        if (font != null)
        {
            text.font = font;
        }
    }

    private static void CreateLabel(
        Transform parent,
        string objectName,
        string label,
        TMP_FontAsset font,
        float preferredWidth)
    {
        RectTransform labelRect = CreateRectObject(objectName, parent);
        LayoutElement layout = labelRect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = preferredWidth;

        TextMeshProUGUI text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = 22f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 22f;
        text.raycastTarget = false;
        if (font != null)
        {
            text.font = font;
        }
    }

    private static void RefreshPopulationLabel(Transform populationRow)
    {
        TMP_Text label = populationRow.Find("Population Value")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            label.text = GetPopulationLabel();
        }
    }

    private static string GetPopulationLabel()
    {
        return $"個体数 {PopulationSize}";
    }

    private static void LoadSavedSettings()
    {
        AiSaveData aiData = Aidata.LoadData();
        InputEnabled[0] = aiData.useProximityInput;
        InputEnabled[1] = aiData.useCircularSensorInput;
        InputEnabled[2] = aiData.useWarningLineInput;

        PopulationSettingsData populationData = populationSetting.LoadData();
        PopulationSize = populationData.populationSize;
        AdvanceWhenAllIndividualsAreHit = populationData.advanceWhenAllIndividualsAreHit;
        PendingManualGenerationRequests = populationData.pendingManualGenerationRequests;
        RewardEnabled[0] = populationData.evaluateDps;
        RewardEnabled[1] = populationData.evaluateSurvivalTime;
        RewardEnabled[2] = populationData.evaluateTimeToScreenEdgeCollision;
        RewardEnabled[3] = populationData.evaluateDistanceFromScreenEdge;
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
        data.advanceWhenAllIndividualsAreHit = AdvanceWhenAllIndividualsAreHit;
        data.pendingManualGenerationRequests = PendingManualGenerationRequests;
        data.evaluateDps = RewardEnabled[0];
        data.evaluateSurvivalTime = RewardEnabled[1];
        data.evaluateTimeToScreenEdgeCollision = RewardEnabled[2];
        data.evaluateDistanceFromScreenEdge = RewardEnabled[3];
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
