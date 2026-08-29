using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class StageView
{
    private const string ButtonObjectName = "Next Generation Button";

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
