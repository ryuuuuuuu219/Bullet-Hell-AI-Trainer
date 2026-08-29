using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class StageView
{
    private const string ButtonObjectName = "Next Generation Button";
    private const string LayerInfoScrollViewObjectName = "Scroll View";

    public static void Build()
    {
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Button existingButton = FindButtonByObjectName(ButtonObjectName);
        if (existingButton != null)
        {
            RefreshGenerationLabel();
            return;
        }

        Button backButton = FindButton("back");
        if (backButton == null)
        {
            Debug.LogWarning("Stage back button was not found for Next Generation button template.");
            return;
        }

        Button nextGenerationButton = UnityEngine.Object.Instantiate(
            backButton,
            backButton.transform.parent,
            false);
        nextGenerationButton.gameObject.name = ButtonObjectName;

        RectTransform rect = nextGenerationButton.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(-650f, 497f);
        rect.sizeDelta = new Vector2(240f, 40f);

        nextGenerationButton.onClick.RemoveAllListeners();
        nextGenerationButton.onClick.AddListener(() =>
        {
            PopulationSettingsData data = populationSetting.LoadData();
            data.pendingManualGenerationRequests++;
            populationSetting.SaveData(data);
            RefreshLabel(nextGenerationButton, data);
        });

        PopulationSettingsData savedData = populationSetting.LoadData();
        RefreshLabel(nextGenerationButton, savedData);
    }

    public static void RefreshGenerationLabel()
    {
        Button button = FindButtonByObjectName(ButtonObjectName);
        if (button != null)
        {
            RefreshLabel(button, populationSetting.LoadData());
        }
    }

    public static void BuildLayerInfo(
        GameObject infoPrefab,
        StageSpawnManager stageSpawnManager,
        int layerCount)
    {
        if (infoPrefab == null || stageSpawnManager == null)
        {
            Debug.LogWarning("Layer info prefab or StageSpawnManager is not assigned.");
            return;
        }

        ScrollRect infoScrollView = FindScrollRectByObjectName(
            LayerInfoScrollViewObjectName);
        if (infoScrollView == null || infoScrollView.content == null)
        {
            Debug.LogWarning("Layer info Scroll View Content was not found.");
            return;
        }

        RectTransform contentRect = infoScrollView.content;
        for (int childIndex = contentRect.childCount - 1;
             childIndex >= 0;
             childIndex--)
        {
            Transform child = contentRect.GetChild(childIndex);
            if (child.GetComponent<LayerInfoPanelController>() != null)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        VerticalLayoutGroup layout = contentRect.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter contentSizeFitter =
            contentRect.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter == null)
        {
            contentSizeFitter =
                contentRect.gameObject.AddComponent<ContentSizeFitter>();
        }

        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int layer = 0; layer < layerCount; layer++)
        {
            GameObject row = UnityEngine.Object.Instantiate(
                infoPrefab,
                contentRect,
                false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            if (rowRect != null)
            {
                rowRect.sizeDelta = new Vector2(338f, 36f);
            }

            LayerInfoPanelController controller =
                row.GetComponent<LayerInfoPanelController>();
            if (controller == null)
            {
                controller = row.AddComponent<LayerInfoPanelController>();
            }

            controller.Initialize(stageSpawnManager, layer);
        }

        Button infoButton = FindButtonByObjectName("info");
        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(() =>
                infoScrollView.gameObject.SetActive(
                    !infoScrollView.gameObject.activeSelf));
        }
    }

    private static ScrollRect FindScrollRectByObjectName(string objectName)
    {
        foreach (ScrollRect scrollRect in
                 UnityEngine.Object.FindObjectsByType<ScrollRect>(
                     FindObjectsInactive.Include))
        {
            if (scrollRect.gameObject.name == objectName)
            {
                return scrollRect;
            }
        }

        return null;
    }

    private static Button FindButton(string label)
    {
        foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null && string.Equals(
                    text.text.Trim(),
                    label,
                    StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private static Button FindButtonByObjectName(string objectName)
    {
        foreach (Button button in
                 UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (button.gameObject.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private static void RefreshLabel(
        Button button,
        PopulationSettingsData populationData)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
        {
            return;
        }

        text.text = populationData.pendingManualGenerationRequests > 0
            ? $"第{populationData.currentGeneration}世代 " +
              $"→ 次世代（要求 {populationData.pendingManualGenerationRequests}）"
            : $"第{populationData.currentGeneration}世代 → 次世代";
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 24f;
    }
}
