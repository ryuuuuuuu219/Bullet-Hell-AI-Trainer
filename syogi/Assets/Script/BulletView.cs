using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))]
public class BulletView : MonoBehaviour
{
    Bullet bullet;
    Vector2Int boardSize;
    Vector2 boardOffset;
    RectTransform rect;
    TMP_Text hpLabel;

    public void Initialize(Bullet owner, Vector2Int size, Vector2 offset)
    {
        bullet = owner;
        boardSize = size;
        boardOffset = offset;
        rect = GetComponent<RectTransform>();
        var image = GetComponent<Image>();
        image.color = Color.blue;
        image.raycastTarget = false;
        if (hpLabel == null)
        {
            var obj = new GameObject("HP", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.layer = gameObject.layer;
            var hpRect = obj.GetComponent<RectTransform>();
            hpRect.SetParent(rect, false);
            hpRect.anchorMin = Vector2.zero;
            hpRect.anchorMax = Vector2.one;
            hpRect.offsetMin = hpRect.offsetMax = Vector2.zero;
            hpLabel = obj.GetComponent<TextMeshProUGUI>();
            hpLabel.color = Color.white;
            hpLabel.alignment = TextAlignmentOptions.Center;
            hpLabel.enableAutoSizing = true;
            hpLabel.fontSizeMin = 1f;
            hpLabel.fontSizeMax = 200f;
            hpLabel.raycastTarget = false;
        }
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        if (bullet == null || bullet.Data == null || rect == null || boardSize.x <= 0 || boardSize.y <= 0) return;
        rect.anchorMin = new Vector2((bullet.Position.x + 0.325f) / boardSize.x, (bullet.Position.y + 0.325f) / boardSize.y);
        rect.anchorMax = new Vector2((bullet.Position.x + 0.675f) / boardSize.x, (bullet.Position.y + 0.675f) / boardSize.y);
        rect.anchoredPosition = boardOffset;
        rect.sizeDelta = Vector2.zero;
        hpLabel.text = bullet.HP.ToString();
        gameObject.name = "Bullet_" + bullet.Position.x + "_" + bullet.Position.y;
    }
}
