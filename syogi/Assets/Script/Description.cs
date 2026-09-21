using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Description : MonoBehaviour
{
    public CardSelect cardSelect;
    public RectTransform field;
    CardData SelectedCard=> cardSelect.SelectedCard;

    readonly List<GameObject> bulletpool = new List<GameObject>();
    readonly List<GameObject> linepool = new List<GameObject>();
    readonly List<GameObject> HPpool = new List<GameObject>();
    CardData displayedCard;
    Vector2 displayedSize;

    private void LateUpdate()
    {
        if (cardSelect == null || field == null) return;
        if (displayedCard != SelectedCard || displayedSize != field.rect.size) Visiblebullet();
    }

    public void Visiblebullet()
    {
        if (cardSelect == null || field == null) return;
        foreach (var obj in bulletpool) { obj.SetActive(false); Destroy(obj); }
        foreach (var obj in linepool) { obj.SetActive(false); Destroy(obj); }
        foreach (var obj in HPpool) { obj.SetActive(false); Destroy(obj); }
        bulletpool.Clear(); linepool.Clear(); HPpool.Clear();
        displayedCard = SelectedCard;
        displayedSize = field.rect.size;
        if (SelectedCard == null || SelectedCard.bullet == null || SelectedCard.AffectAreaRow <= 0) return;
        foreach (var data in SelectedCard.bullet) bullet(data, SelectedCard.AffectAreaRow);
    }
    void BuildLine(Vector2 start, Vector2 end, Color color)
    {
        var line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        line.layer = field.gameObject.layer;
        var rect = line.GetComponent<RectTransform>();
        rect.SetParent(field, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (start + end) * 0.5f;
        var delta = end - start;
        rect.sizeDelta = new Vector2(delta.magnitude, 3f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        var image = line.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        linepool.Add(line);
    }

    void bullet(bulletData_Card bulletData, int range, bool isplayer=true)
    {
        Color color = isplayer ? Color.blue : Color.red;
        string hp = string.IsNullOrEmpty(bulletData.displayCode)
            ? bulletData.HP.ToString()
            : "<size=60%>" + bulletData.displayCode + "</size>\n" + bulletData.HP;
        Vector2 startAnchor = new Vector2((bulletData.x + 0.5f) / range, (bulletData.y + 0.5f) / range);
        Vector2 endAnchor = new Vector2((bulletData.nextX + 0.5f) / range, (bulletData.nextY + 0.5f) / range);
        Vector2 startPos = Vector2.Scale(startAnchor, field.rect.size);
        Vector2 endPos = Vector2.Scale(endAnchor, field.rect.size);
        float cell = Mathf.Min(field.rect.width, field.rect.height) / range;
        if ((endPos - startPos).sqrMagnitude > 0.001f)
        {
            var direction = (endPos - startPos).normalized;
            var normal = new Vector2(-direction.y, direction.x);
            float head = Mathf.Min(cell * 0.15f, (endPos - startPos).magnitude * 0.25f);
            BuildLine(startPos + direction * cell * 0.2f, endPos, color);
            BuildLine(endPos - direction * head + normal * head * 0.6f, endPos, color);
            BuildLine(endPos - direction * head - normal * head * 0.6f, endPos, color);
        }
        var piece = new GameObject("Piece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        piece.layer = field.gameObject.layer;
        var pieceRect = piece.GetComponent<RectTransform>();
        pieceRect.SetParent(field, false);
        pieceRect.anchorMin = pieceRect.anchorMax = startAnchor;
        pieceRect.anchoredPosition = Vector2.zero;
        pieceRect.sizeDelta = Vector2.one * cell * 0.35f;
        piece.GetComponent<Image>().color = color;
        piece.GetComponent<Image>().raycastTarget = false;
        bulletpool.Add(piece);
        var hpObj = new GameObject("HP", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        hpObj.layer = field.gameObject.layer;
        var hpRect = hpObj.GetComponent<RectTransform>();
        hpRect.SetParent(pieceRect, false);
        hpRect.anchorMin = Vector2.zero;
        hpRect.anchorMax = Vector2.one;
        hpRect.offsetMin = hpRect.offsetMax = Vector2.zero;
        var label = hpObj.GetComponent<TextMeshProUGUI>();
        label.text = hp;
        label.fontSize = cell * 0.2f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1f;
        label.fontSizeMax = cell * 0.2f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        HPpool.Add(hpObj);
    }

    void OnDestroy()
    {
        foreach (var obj in bulletpool) if (obj != null) Destroy(obj);
        foreach (var obj in linepool) if (obj != null) Destroy(obj);
    }
}
