using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(fieldrender), typeof(CardPlacement), typeof(BulletManager))]
public class BoardCellInput : MonoBehaviour
{
    public fieldrender board;
    public CardPlacement placement;
    public BulletManager bulletManager;
    readonly List<Button> buttons = new List<Button>();
    RectTransform detailsRect;
    TextMeshProUGUI detailsText;
    bool battleMode;

    public void SetBattleMode(bool enabled, bool allowPlacement = true)
    {
        battleMode = enabled;
        HideDetails();
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null)
                buttons[i].interactable = enabled || allowPlacement;
        }
    }

    public void HideDetails()
    {
        if (detailsRect != null) detailsRect.gameObject.SetActive(false);
    }

    void Start()
    {
        if (board == null) board = GetComponent<fieldrender>();
        if (placement == null) placement = GetComponent<CardPlacement>();
        if (bulletManager == null) bulletManager = GetComponent<BulletManager>();
        if (board.FoundationRect == null || board.Columns <= 0 || board.Rows <= 0) return;
        BuildCellButtons(board.FoundationRect, board.Columns, board.Rows);
    }

    void BuildCellButtons(RectTransform parent, int columns, int rows)
    {
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var obj = new GameObject("CellButton_" + x + "_" + y,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.layer = parent.gameObject.layer;
                var rect = obj.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2((float)x / columns, (float)y / rows);
                rect.anchorMax = new Vector2((float)(x + 1) / columns, (float)(y + 1) / rows);
                rect.anchoredPosition = board.FieldRightTopPos;
                rect.sizeDelta = Vector2.zero;
                var image = obj.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;
                var button = obj.GetComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.interactable = true;
                int cellX = x;
                int cellY = y;
                button.onClick.AddListener(() => OnCellClicked(cellX, cellY));
                buttons.Add(button);
            }
        }
    }

    void OnCellClicked(int x, int y)
    {
        if (HasBulletAt(x, y)) OnOccupiedCellClicked(x, y);
        else OnEmptyCellClicked(x, y);
    }

    bool HasBulletAt(int x, int y)
    {
        if (bulletManager == null) bulletManager = GetComponent<BulletManager>();
        if (bulletManager == null) return false;
        foreach (var bullet in bulletManager.bullets)
            if (bullet != null && bullet.gameObject.activeInHierarchy && bullet.HP > 0 &&
                bullet.Position.x == x && bullet.Position.y == y) return true;
        return false;
    }

    void OnEmptyCellClicked(int x, int y)
    {
        if (battleMode) ShowDetails(x, y);
        else if (y < CardDefinitions.PlacementRows && placement != null)
            placement.PlaceSelectedCard(x, y);
    }

    void OnOccupiedCellClicked(int x, int y)
    {
        ShowDetails(x, y);
    }

    void ShowDetails(int x, int y)
    {
        if (bulletManager == null) bulletManager = GetComponent<BulletManager>();
        var lines = new StringBuilder();
        int count = 0;
        if (bulletManager != null)
        {
            foreach (var bullet in bulletManager.bullets)
            {
                if (bullet == null || bullet.HP <= 0 || bullet.Position.x != x || bullet.Position.y != y) continue;
                if (count++ > 0) lines.Append('\n');
                lines.Append(string.IsNullOrEmpty(bullet.Data.name) ? "弾" : bullet.Data.name);
                lines.Append('：').Append(bullet.HP);
            }
        }
        if (count == 0) lines.Append("弾なし");
        EnsureDetails();
        if (detailsRect == null) return;
        detailsText.text = lines.ToString();
        var parent = board.FoundationRect;
        detailsRect.anchorMin = detailsRect.anchorMax = new Vector2((x + 0.5f) / board.Columns, (y + 0.5f) / board.Rows);
        detailsRect.pivot = new Vector2(x < board.Columns / 2 ? 0f : 1f, y < board.Rows / 2 ? 0f : 1f);
        detailsRect.anchoredPosition = board.FieldRightTopPos;
        detailsRect.sizeDelta = new Vector2(Mathf.Min(360f, parent.rect.width * 0.5f),
            Mathf.Min(parent.rect.height * 0.8f, 20f + Mathf.Max(1, count) * 32f));
        detailsRect.gameObject.SetActive(true);
        detailsRect.SetAsLastSibling();
    }

    void EnsureDetails()
    {
        if (detailsRect != null || board == null || board.FoundationRect == null) return;
        var obj = new GameObject("CellBulletDetails", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = board.FoundationRect.gameObject.layer;
        detailsRect = obj.GetComponent<RectTransform>();
        detailsRect.SetParent(board.FoundationRect, false);
        var background = obj.GetComponent<Image>();
        background.color = new Color(0.06f, 0.08f, 0.16f, 0.95f);
        background.raycastTarget = false;

        var textObject = new GameObject("DetailsText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = obj.layer;
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(detailsRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 10f);
        textRect.offsetMax = new Vector2(-10f, -10f);
        detailsText = textObject.GetComponent<TextMeshProUGUI>();
        var cardSelect = GetComponent<CardSelect>();
        if (cardSelect != null && cardSelect.DescriptionFont != null) detailsText.font = cardSelect.DescriptionFont;
        detailsText.color = Color.white;
        detailsText.fontSize = 24f;
        detailsText.enableAutoSizing = true;
        detailsText.fontSizeMin = 12f;
        detailsText.fontSizeMax = 24f;
        detailsText.raycastTarget = false;
        obj.SetActive(false);
    }

    void OnDestroy()
    {
        foreach (var button in buttons) if (button != null) Destroy(button.gameObject);
        if (detailsRect != null) Destroy(detailsRect.gameObject);
    }
}
