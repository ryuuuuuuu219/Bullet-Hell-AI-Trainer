using System.Collections.Generic;

public static class CardDefinitions
{
    public const int BoardSize = 9;
    public const int PlacementRows = 3;

    public static CardData[] Create()
    {
        return new[]
        {
            CreateHorizontalVolleyCard(0),
            CreateDefenseWallCard(1),
            CreatePiercingBulletCard(2)
        };
    }

    public static CardData CreateHorizontalVolleyCard(int cardId)
    {
        return new CardData
        {
            CardID = cardId,
            CardName = "横一列斉射",
            CardDiscription = "横一列の編隊になって飛翔するHP1の弾幕3斉射",
            AffectAreaRow = 3,
            RemainingCount = 3,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card { x = 0, y = 0, nextX = 0, nextY = 1, HP = 1 },
                new bulletData_Card { x = 1, y = 0, nextX = 1, nextY = 1, HP = 1 },
                new bulletData_Card { x = 2, y = 0, nextX = 2, nextY = 1, HP = 1 }
            }
        };
    }

    public static CardData CreateDefenseWallCard(int cardId)
    {
        return new CardData
        {
            CardID = cardId,
            CardName = "防御壁",
            CardDiscription = "留まる壁HP9",
            AffectAreaRow = 3,
            RemainingCount = 1,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card { x = 1, y = 1, nextX = 1, nextY = 1, HP = 9 }
            }
        };
    }

    public static CardData CreatePiercingBulletCard(int cardId)
    {
        return new CardData
        {
            CardID = cardId,
            CardName = "貫通弾",
            CardDiscription = "一発のみ、HP9で飛翔する貫通弾",
            AffectAreaRow = 3,
            RemainingCount = 1,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card { x = 1, y = 0, nextX = 1, nextY = 1, HP = 9 }
            }
        };
    }
}
