using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(fieldrender))]
public class CardPlacement : MonoBehaviour
{
    public fieldrender board;
    public CardSelect cardSelect;
    public BulletManager bulletManager;
    public List<bulletData_Card> placedBullets = new List<bulletData_Card>();
    public List<Bullet> bulletObjects = new List<Bullet>();

    public void PlaceSelectedCard(int cellX, int cellY)
    {
        if (board == null) board = GetComponent<fieldrender>();
        var selector = cardSelect != null ? cardSelect : GetComponent<CardSelect>();
        var card = selector != null ? selector.SelectedCard : null;
        var parent = board.FoundationRect;
        int columns = board.Columns;
        int rows = board.Rows;
        if (parent == null || card == null || card.bullet == null || card.AffectAreaRow <= 0 ||
            columns <= 0 || rows <= 0 || cellX < 0 || cellX >= columns || cellY < 0 || cellY >= rows) return;

        int center = card.AffectAreaRow / 2;
        foreach (var source in card.bullet)
        {
            int x = cellX + source.x - center;
            int y = cellY + source.y - center;
            if (x < 0 || x >= columns || y < 0 || y >= rows) continue;
            var data = new bulletData_Card
            {
                x = x, y = y,
                nextX = cellX + source.nextX - center,
                nextY = cellY + source.nextY - center,
                HP = source.HP
            };
            placedBullets.Add(data);

            var bullet = Bullet.Create(parent, data, new Vector2Int(columns, rows), board.FieldRightTopPos);
            bulletObjects.Add(bullet);
            if (bulletManager == null) bulletManager = GetComponent<BulletManager>();
            if (bulletManager != null) bulletManager.Add(bullet);
        }
    }
}
