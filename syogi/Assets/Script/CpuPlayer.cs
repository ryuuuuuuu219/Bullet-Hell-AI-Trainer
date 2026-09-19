using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(fieldrender), typeof(BulletManager))]
public class CpuPlayer : MonoBehaviour
{
    public fieldrender board;
    public BulletManager bulletManager;

    readonly List<Bullet> placedBullets = new List<Bullet>();
    bool handPlaced;

    public void PlaceHand()
    {
        if (handPlaced) return;
        if (board == null) board = GetComponent<fieldrender>();
        if (bulletManager == null) bulletManager = GetComponent<BulletManager>();
        var parent = board != null ? board.FoundationRect : null;
        if (parent == null || bulletManager == null || board.Columns <= 0 || board.Rows < 3) return;

        handPlaced = true;
        var cards = CardDefinitions.Create();
        foreach (var card in cards)
        {
            for (int copy = 0; copy < card.RemainingCount; copy++)
            {
                var candidates = FindPlacements(card);
                if (candidates.Count == 0)
                {
                    Debug.LogWarning("CPUのカードを奥3行に配置できません: " + card.CardName, this);
                    continue;
                }
                var center = candidates[Random.Range(0, candidates.Count)];
                int cardCenter = card.AffectAreaRow / 2;
                foreach (var source in card.bullet)
                {
                    int x = center.x + source.x - cardCenter;
                    int y = center.y + source.y - cardCenter;
                    var data = new bulletData_Card
                    {
                        x = x,
                        y = y,
                        nextX = x + source.nextX - source.x,
                        nextY = y - (source.nextY - source.y),
                        HP = source.HP
                    };
                    var bullet = Bullet.Create(parent, data, new Vector2Int(board.Columns, board.Rows), board.FieldRightTopPos, false);
                    placedBullets.Add(bullet);
                    bulletManager.Add(bullet);
                }
            }
        }
    }

    List<Vector2Int> FindPlacements(CardData card)
    {
        var candidates = new List<Vector2Int>();
        if (card.bullet == null || card.AffectAreaRow <= 0) return candidates;
        int cardCenter = card.AffectAreaRow / 2;
        int backStart = board.Rows - 3;
        for (int y = backStart; y < board.Rows; y++)
        {
            for (int x = 0; x < board.Columns; x++)
            {
                bool available = true;
                foreach (var source in card.bullet)
                {
                    int targetX = x + source.x - cardCenter;
                    int targetY = y + source.y - cardCenter;
                    if (targetX < 0 || targetX >= board.Columns || targetY < backStart || targetY >= board.Rows ||
                        IsOccupied(targetX, targetY))
                    {
                        available = false;
                        break;
                    }
                }
                if (available) candidates.Add(new Vector2Int(x, y));
            }
        }
        return candidates;
    }

    bool IsOccupied(int x, int y)
    {
        foreach (var bullet in bulletManager.bullets)
            if (bullet != null && bullet.Position.x == x && bullet.Position.y == y) return true;
        return false;
    }

    public void ClearPlaced()
    {
        if (bulletManager == null) bulletManager = GetComponent<BulletManager>();
        foreach (var bullet in placedBullets)
        {
            if (bullet == null) continue;
            if (bulletManager != null) bulletManager.bullets.Remove(bullet);
            Destroy(bullet.gameObject);
        }
        placedBullets.Clear();
        handPlaced = false;
    }
}
