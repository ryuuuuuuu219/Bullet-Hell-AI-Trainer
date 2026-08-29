using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class StageSelectView
{
    private static readonly int[] ImplementedStageIds = { 0 };
    private static readonly Color SelectedColor = new Color(0.55f, 0.78f, 1f, 1f);
    private static readonly Color NormalColor = Color.white;

    public static void Build()
    {
        GameObject contentObject = GameObject.Find("Content");
        if (contentObject == null)
        {
            Debug.LogWarning("StageSelect Scroll View Content was not found.");
            return;
        }

        ConfigureLayout(contentObject);

        foreach (int stageId in ImplementedStageIds)
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
        switch (stageId)
        {
            case 0:
                return "課題1　自機狙い1way";
            default:
                return $"Stage {stageId}";
        }
    }
}
