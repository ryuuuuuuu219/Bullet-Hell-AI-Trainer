using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(fieldrender), typeof(CardPlacement))]
public class BoardCellInput : MonoBehaviour
{
    public fieldrender board;
    public CardPlacement placement;
    readonly List<GameObject> buttons = new List<GameObject>();

    public void SetPlacementEnabled(bool enabled)
    {
        foreach (var obj in buttons)
            if (obj != null) obj.GetComponent<Button>().interactable = enabled;
    }

    void Start()
    {
        if (board == null) board = GetComponent<fieldrender>();
        if (placement == null) placement = GetComponent<CardPlacement>();
        if (board.FoundationRect == null || board.Columns <= 0 || board.Rows <= 0) return;
        BuildFrontRowButtons(board.FoundationRect, board.Columns, board.Rows);
    }

    void BuildFrontRowButtons(RectTransform parent, int columns, int rows)
    {
        // 盤面の下端を手前として、下から最大3行に配置する。
        for (int y = 0; y < Mathf.Min(3, rows); y++)
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
                int cellX = x;
                int cellY = y;
                button.onClick.AddListener(() => placement.PlaceSelectedCard(cellX, cellY));
                buttons.Add(obj);
            }
        }
    }

    void OnDestroy()
    {
        foreach (var obj in buttons) if (obj != null) Destroy(obj);
    }
}
