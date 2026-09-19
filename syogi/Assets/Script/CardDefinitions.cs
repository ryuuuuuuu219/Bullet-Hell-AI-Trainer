using System.Collections.Generic;

public static class CardDefinitions
{
    public const int BoardSize = 9;
    public const int PlacementRows = 3;

    static void ValidateAffectAreaRow(int affectAreaRow)
    {
        if (affectAreaRow % 2 != 0) return;
        string message = $"AffectAreaRow は奇数にしてください。指定値: {affectAreaRow}";
        UnityEngine.Debug.LogError(message);
        throw new System.ArgumentOutOfRangeException(nameof(affectAreaRow), affectAreaRow, message);
    }

    public static CardData[] Create()
    {
        return new[]
        {
            CreateHorizontalVolleyCard(0, 3, 3),
            CreateDefenseWallCard(1, 3, 1),
            CreatePiercingBulletCard(2, 3, 1)
        };
    }

    public static CardData CreateHorizontalVolleyCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        var bullets = new List<bulletData_Card>();
        for (int x = 0; x < affectAreaRow; x++)
        {
            bullets.Add(new bulletData_Card { x = x, y = 0, nextX = x, nextY = 1, HP = 1 });
        }

        return new CardData
        {
            CardID = cardId,
            CardName = "横一列斉射",
            CardDiscription = "横一列の編隊になって飛翔するHP1の弾幕3斉射",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = bullets
        };
    }

    public static CardData CreateDefenseWallCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return new CardData
        {
            CardID = cardId,
            CardName = "防御壁",
            CardDiscription = "留まる壁HP9",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card { x = center, y = center, nextX = center, nextY = center, HP = 9 }
            }
        };
    }

    public static CardData CreatePiercingBulletCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return new CardData
        {
            CardID = cardId,
            CardName = "貫通弾",
            CardDiscription = "一発のみ、HP9で飛翔する貫通弾",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card { x = center, y = center, nextX = center, nextY = center + 1, HP = 9 }
            }
        };
    }
}
