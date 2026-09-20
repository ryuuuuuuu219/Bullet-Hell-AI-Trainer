using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(fieldrender))]
public class CardPlacement : MonoBehaviour
{
    public fieldrender board;
    public CardSelect cardSelect;
    public BulletManager bulletManager;
    public List<bulletData_Card> placedBullets = new List<bulletData_Card>();
    public List<Bullet> bulletObjects = new List<Bullet>();
    readonly Dictionary<Vector2Int, GameObject> overrideDisplays = new Dictionary<Vector2Int, GameObject>();

    public void ClearPlaced()
    {
        ClearOverrideDisplays();
        foreach (var bullet in bulletObjects)
            if (bullet != null) Destroy(bullet.gameObject);
        bulletObjects.Clear();
        placedBullets.Clear();
    }

    public void ClearOverrideDisplays()
    {
        foreach (var display in overrideDisplays.Values)
            if (display != null) Destroy(display);
        overrideDisplays.Clear();
    }

    void RefreshOverrideDisplay(int x, int y, RectTransform parent, int columns, int rows, CardSelect selector)
    {
        var position = new Vector2Int(x, y);
        var hpParts = new List<string>();
        var overlapping = new List<Bullet>();
        int totalHP = 0;
        foreach (var bullet in bulletObjects)
        {
            if (bullet == null || bullet.Position != position) continue;
            overlapping.Add(bullet);
            hpParts.Add(bullet.HP.ToString());
            totalHP += bullet.HP;
        }
        if (overlapping.Count < 2) return;

        var remainingParts = new List<string>();
        int remainingTotal = 0;
        foreach (var bullet in overlapping)
        {
            // ガスは接触ダメージを受けない。それ以外は同じマスの他弾のHP合計を受ける。
            int remaining = bullet.Attribute == Attribute.gus ? bullet.HP : 2 * bullet.HP - totalHP;
            if (remaining <= 0) continue;
            remainingParts.Add(remaining.ToString());
            remainingTotal += remaining;
        }

        if (!overrideDisplays.TryGetValue(position, out var display) || display == null)
        {
            display = new GameObject("Override_" + x + "_" + y,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            display.layer = parent.gameObject.layer;
            var rect = display.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2((float)x / columns, (float)y / rows);
            rect.anchorMax = new Vector2((float)(x + 1) / columns, (float)(y + 1) / rows);
            rect.anchoredPosition = board.FieldRightTopPos;
            rect.sizeDelta = Vector2.zero;
            var image = display.GetComponent<Image>();
            image.color = new Color(0.1f, 0.2f, 0.65f, 0.95f);
            image.raycastTarget = false;

            var labelObject = new GameObject("OverrideText", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.layer = display.layer;
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            if (selector.DescriptionFont != null) label.font = selector.DescriptionFont;
            label.enableAutoSizing = true;
            label.fontSizeMin = 6f;
            label.fontSizeMax = 16f;
            label.raycastTarget = false;
            overrideDisplays[position] = display;
        }
        display.transform.SetAsLastSibling();
        string outcome = remainingParts.Count == 0 ? "!消滅!" :
            "!減衰!\n残りHP:" + string.Join("+", remainingParts) +
            (remainingParts.Count > 1 ? "=" + remainingTotal : "");
        display.GetComponentInChildren<TMP_Text>().text =
            "上書き注意！\n" + string.Join("+", hpParts) + "=" + totalHP + "\n" + outcome;
    }

    public void PlaceSelectedCard(int cellX, int cellY)
    {
        if (board == null) board = GetComponent<fieldrender>();
        var selector = cardSelect != null ? cardSelect : GetComponent<CardSelect>();
        var card = selector != null ? selector.SelectedCard : null;
        var parent = board.FoundationRect;
        int columns = board.Columns;
        int rows = board.Rows;
        if (parent == null || selector == null || !selector.CanPlaceSelectedCard || card.bullet == null || card.AffectAreaRow <= 0 ||
            columns <= 0 || rows <= 0 || cellX < 0 || cellX >= columns || cellY < 0 || cellY >= rows) return;

        int center = card.AffectAreaRow / 2;
        int placedCount = 0;
        foreach (var source in card.bullet)
        {
            int x = cellX + source.x - center;
            int y = cellY + source.y - center;
            if (x < 0 || x >= columns || y < 0 || y >= rows) continue;
            var data = source.CopyAt(x, y, true);
            placedBullets.Add(data);

            var bullet = Bullet.Create(parent, data, new Vector2Int(columns, rows), board.FieldRightTopPos);
            bulletObjects.Add(bullet);
            if (bulletManager == null) bulletManager = GetComponent<BulletManager>();
            if (bulletManager != null) bulletManager.Add(bullet);
            RefreshOverrideDisplay(x, y, parent, columns, rows, selector);
            placedCount++;
        }
        if (placedCount > 0) selector.ConsumeSelectedCard();
    }
}
