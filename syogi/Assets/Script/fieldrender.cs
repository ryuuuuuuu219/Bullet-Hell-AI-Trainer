using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class fieldrender : MonoBehaviour
{
    public GameObject foundation;
    public Vector2 fieldnum;
    [Tooltip("土台画像の右上アンカーからのオフセット（UI座標）")]
    public Vector2 FieldRightTopPos;

    List<GameObject> lines = new List<GameObject>();
    void BuildLine(RectTransform parent, string name, Vector2 start, Vector2 end)
    {
        var line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        line.layer = parent.gameObject.layer;
        var rect = line.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.Min(start, end);
        rect.anchorMax = Vector2.Max(start, end);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = FieldRightTopPos;
        rect.sizeDelta = Vector2.one;
        var image = line.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        lines.Add(line);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var foundationRect = foundation != null ? foundation.GetComponent<RectTransform>() : null;
        if (foundationRect == null)
        {
            Debug.LogError("foundation に土台画像の RectTransform を持つオブジェクトを設定してください。", this);
            return;
        }

        int column = (int)fieldnum.x;
        int row = (int)fieldnum.y;
        if (column <= 0 || row <= 0)
        {
            Debug.LogError("マス数は縦横ともに1以上に設定してください。", this);
            return;
        }

        for (int i = 0; i <= column; i++)
        {
            float x = 1f - (float)i / column;
            BuildLine(foundationRect, "VerticalLine_" + i, new Vector2(x, 1f), new Vector2(x, 0f));

        }
        for (int j = 0; j <= row; j++)
        {
            float y = 1f - (float)j / row;
            BuildLine(foundationRect, "HorizontalLine_" + j, new Vector2(1f, y), new Vector2(0f, y));
        }

    }
}
