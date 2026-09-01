using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class StageSelectView
{
    private static readonly Color SelectedColor = new Color(0.55f, 0.78f, 1f, 1f);
    private static readonly Color NormalColor = Color.white;
    private const string ManualToggleObjectName = "Manual Player Toggle";

    public static void Build()
    {
        BuildManualPlayerToggle();

        GameObject contentObject = GameObject.Find("Content");
        if (contentObject == null)
        {
            Debug.LogWarning("StageSelect Scroll View Content was not found.");
            return;
        }

        ConfigureLayout(contentObject);

        for (int stageId = 0;
             stageId < BulletHellStageAttackDefinitions.Count;
             stageId++)
        {
            string objectName = GetButtonObjectName(stageId);
            if (contentObject.transform.Find(objectName) != null)
            {
                continue;
            }

            CreateStageButton(contentObject.transform, stageId);
        }

        RefreshSelection();
    }

    public static void RefreshSelection()
    {
        foreach (StageSelectButton stageButton in
                 Object.FindObjectsByType<StageSelectButton>(FindObjectsInactive.Include))
        {
            Image image = stageButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = stageButton.StageId == GameSceneManager.StageId
                    ? SelectedColor
                    : NormalColor;
            }
        }
    }

    private static void ConfigureLayout(GameObject contentObject)
    {
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = contentObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.padding = new RectOffset(12, 12, 12, 12);
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

    private static void CreateStageButton(Transform parent, int stageId)
    {
        GameObject buttonObject = new GameObject(
            GetButtonObjectName(stageId),
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement),
            typeof(StageSelectButton));

        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = NormalColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 72f;
        layoutElement.minHeight = 60f;

        StageSelectButton stageButton = buttonObject.GetComponent<StageSelectButton>();
        stageButton.SetStageId(stageId);

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = GetButtonLabel(stageId);
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        text.fontSize = 24f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 24f;

        TMP_Text descriptionText = GameObject.Find("Discription")?.GetComponentInChildren<TMP_Text>(true);
        if (descriptionText != null)
        {
            text.font = descriptionText.font;
        }
    }

    private static string GetButtonObjectName(int stageId)
    {
        return $"Stage Button {stageId}";
    }

    private static string GetButtonLabel(int stageId)
    {
        BulletHellStageDefinition stage =
            BulletHellStageAttackDefinitions.GetStage(stageId);
        return stage != null
            ? $"課題A-{stageId + 1}　{stage.Title}"
            : $"Stage {stageId}";
    }

    private static void BuildManualPlayerToggle()
    {
        GameObject toggleObject = GameObject.Find(ManualToggleObjectName);
        if (toggleObject == null)
        {
            Button startButton = FindButton("Start");
            if (startButton == null)
            {
                Debug.LogWarning("Stage Start button was not found for the manual player toggle.");
                return;
            }

            toggleObject = new GameObject(
                ManualToggleObjectName,
                typeof(RectTransform));
            toggleObject.layer = startButton.gameObject.layer;
            toggleObject.transform.SetParent(startButton.transform.parent, false);

            RectTransform startRect = startButton.GetComponent<RectTransform>();
            RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = startRect.anchorMin;
            toggleRect.anchorMax = startRect.anchorMax;
            toggleRect.pivot = startRect.pivot;
            toggleRect.sizeDelta = new Vector2(340f, startRect.sizeDelta.y);
            toggleRect.anchoredPosition = startRect.anchoredPosition +
                Vector2.left * (startRect.sizeDelta.x * 0.5f + 190f);
        }

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        if (toggle == null)
        {
            toggle = toggleObject.AddComponent<Toggle>();
        }

        Image background = GetOrCreateToggleImage(
            toggleObject.transform,
            "Background",
            new Color(0.92f, 0.92f, 0.92f, 1f));
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(8f, 0f);
        backgroundRect.sizeDelta = new Vector2(40f, 40f);

        Image checkmark = GetOrCreateToggleImage(
            background.transform,
            "Checkmark",
            new Color(0f, 0f, 1f, 1f));
        RectTransform checkmarkRect = checkmark.rectTransform;
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        checkmarkRect.offsetMin = new Vector2(7f, 7f);
        checkmarkRect.offsetMax = new Vector2(-7f, -7f);

        TextMeshProUGUI label = GetOrCreateToggleLabel(toggleObject.transform);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(58f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        toggle.SetIsOnWithoutNotify(GameSceneManager.TeacherModeEnabled);
        toggle.onValueChanged.RemoveListener(GameSceneManager.SetTeacherModeEnabled);
        toggle.onValueChanged.AddListener(GameSceneManager.SetTeacherModeEnabled);
    }

    private static Image GetOrCreateToggleImage(
        Transform parent,
        string objectName,
        Color color)
    {
        Transform existing = parent.Find(objectName);
        GameObject imageObject;
        if (existing != null)
        {
            imageObject = existing.gameObject;
        }
        else
        {
            imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.layer = parent.gameObject.layer;
            imageObject.transform.SetParent(parent, false);
        }

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI GetOrCreateToggleLabel(Transform parent)
    {
        Transform existing = parent.Find("Label");
        GameObject labelObject;
        if (existing != null)
        {
            labelObject = existing.gameObject;
        }
        else
        {
            labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = parent.gameObject.layer;
            labelObject.transform.SetParent(parent, false);
        }

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "教師モード（手動操作）";
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        label.fontSize = 24f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 24f;

        TMP_Text descriptionText =
            GameObject.Find("Discription")?.GetComponentInChildren<TMP_Text>(true);
        if (descriptionText != null)
        {
            label.font = descriptionText.font;
        }

        return label;
    }

    private static Button FindButton(string label)
    {
        foreach (Button button in
                 UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null && string.Equals(
                    text.text.Trim(),
                    label,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }
}
