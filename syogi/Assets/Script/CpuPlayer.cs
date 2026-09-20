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
        if (parent == null || bulletManager == null || board.Columns <= 0 || board.Rows < CardDefinitions.PlacementRows) return;

        handPlaced = true;
        var cards = CardDefinitions.Create();
        var groupCards = new List<CardData>();
        var singleCards = new List<CardData>();
        int totalBullets = 0;
        foreach (var card in cards)
        {
            for (int copy = 0; copy < card.RemainingCount; copy++)
            {
                if (card.bullet == null || card.bullet.Count == 0) continue;
                totalBullets += card.bullet.Count;
                if (card.bullet.Count == 1) singleCards.Add(card);
                else groupCards.Add(card);
            }
        }

        int capacity = board.Columns * CardDefinitions.PlacementRows;
        int overlapCount = Mathf.Max(0, totalBullets - capacity);
        if (overlapCount > singleCards.Count)
            Debug.LogWarning("CPUの重ね置きに必要な単発カード数が不足しています。", this);
        overlapCount = Mathf.Min(overlapCount, singleCards.Count);
        var overlapCards = SelectOverlapCardsById(singleCards, overlapCount);

        // 編隊を先に置くと、空きマスへの配置と重ね置きの発数を正確に分けられる。
        foreach (var card in groupCards) PlaceCard(card, false, parent);
        foreach (var card in singleCards) PlaceCard(card, false, parent);
        foreach (var card in overlapCards) PlaceCard(card, true, parent);
    }

    static List<CardData> SelectOverlapCardsById(List<CardData> cards, int count)
    {
        var selected = new List<CardData>();
        for (int i = 0; i < count; i++)
        {
            var ids = new List<int>();
            foreach (var card in cards)
                if (!ids.Contains(card.CardID)) ids.Add(card.CardID);
            int chosenId = ids[Random.Range(0, ids.Count)];
            int index = cards.FindIndex(card => card.CardID == chosenId);
            selected.Add(cards[index]);
            cards.RemoveAt(index);
        }
        return selected;
    }

    void PlaceCard(CardData card, bool overlap, RectTransform parent)
    {
        var candidates = FindPlacements(card, overlap);
        if (candidates.Count == 0)
        {
            Debug.LogWarning("CPUのカードを奥" + CardDefinitions.PlacementRows + "行に配置できません: " + card.CardName, this);
            return;
        }
        var center = candidates[Random.Range(0, candidates.Count)];
        int cardCenter = card.AffectAreaRow / 2;
        foreach (var source in card.bullet)
        {
            int x = center.x + source.x - cardCenter;
            int y = center.y + source.y - cardCenter;
            var data = source.CopyAt(x, y, false);
            var bullet = Bullet.Create(parent, data, new Vector2Int(board.Columns, board.Rows), board.FieldRightTopPos, false);
            placedBullets.Add(bullet);
            bulletManager.Add(bullet);
        }
    }

    List<Vector2Int> FindPlacements(CardData card, bool overlap)
    {
        var candidates = new List<Vector2Int>();
        if (card.bullet == null || card.AffectAreaRow <= 0) return candidates;
        int cardCenter = card.AffectAreaRow / 2;
        int backStart = board.Rows - CardDefinitions.PlacementRows;
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
                        IsOccupied(targetX, targetY) != overlap)
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
