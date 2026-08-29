using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LayerInfoPanelController : MonoBehaviour
{
    private const float RefreshInterval = 0.1f;
    private const float MaximumScoreFontSize = 24f;
    private const float MinimumScoreFontSize = 10f;
    private const float FontSizeReductionPerExtraCharacter = 2f;
    private const int ReferenceScoreCharacterCount = 5;

    private StageSpawnManager stageSpawnManager;
    private TMP_Text playerNameText;
    private TMP_Text scoreText;
    private int logicalLayer;
    private float nextRefreshTime;

    public void Initialize(StageSpawnManager manager, int layer)
    {
        stageSpawnManager = manager;
        logicalLayer = Mathf.Max(0, layer);
        gameObject.name = $"Layer {logicalLayer + 1} Info";

        ResolveTextFields();

        Toggle visibilityToggle = GetComponentInChildren<Toggle>(true);
        if (visibilityToggle != null)
        {
            visibilityToggle.onValueChanged.RemoveAllListeners();
            visibilityToggle.SetIsOnWithoutNotify(
                LogicalLayerVisibility.IsVisible(logicalLayer));
            visibilityToggle.onValueChanged.AddListener(
                visible => LogicalLayerVisibility.SetVisible(
                    logicalLayer,
                    visible));
        }

        RefreshDisplay();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        RefreshDisplay();
    }

    private void ResolveTextFields()
    {
        TMP_Text[] textFields = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textField in textFields)
        {
            if (textField.gameObject.name == "Text (TMP) (1)" ||
                textField.text.Contains("score"))
            {
                scoreText = textField;
            }
            else if (playerNameText == null)
            {
                playerNameText = textField;
            }
        }
    }

    private void RefreshDisplay()
    {
        if (stageSpawnManager == null ||
            !stageSpawnManager.TryGetLayerInfo(
                logicalLayer,
                out string playerName,
                out float score))
        {
            if (playerNameText != null)
            {
                playerNameText.text = $"Player {logicalLayer + 1}";
            }

            if (scoreText != null)
            {
                SetScoreText("--");
            }

            return;
        }

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (scoreText != null)
        {
            SetScoreText(score.ToString("F2"));
        }
    }

    private void SetScoreText(string scoreValue)
    {
        if (scoreText == null)
        {
            return;
        }

        int extraCharacterCount = Mathf.Max(
            0,
            scoreValue.Length - ReferenceScoreCharacterCount);
        scoreText.enableAutoSizing = false;
        scoreText.fontSize = Mathf.Max(
            MinimumScoreFontSize,
            MaximumScoreFontSize -
            extraCharacterCount * FontSizeReductionPerExtraCharacter);
        scoreText.text = $"score : {scoreValue}";
    }
}
