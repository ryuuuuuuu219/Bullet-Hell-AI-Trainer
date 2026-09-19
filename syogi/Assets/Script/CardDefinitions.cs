using System.Collections.Generic;

public static class CardDefinitions
{
    public static CardData[] Create()
    {
        var Card1bulletData = new List<bulletData_Card> { new bulletData_Card { x = 0, y = 0, nextX = 0, nextY = 1, HP = 1 }, new bulletData_Card { x = 1, y = 0, nextX = 1, nextY = 1, HP = 1 }, new bulletData_Card { x = 2, y = 0, nextX = 2, nextY = 1, HP = 1 } };
        return new CardData[]
        {
            new CardData { CardID = 0, CardName = "横一列斉射", CardDiscription = "横一列の編隊になって飛翔するHP1の弾幕3斉射", AffectAreaRow = 3, bullet = Card1bulletData },
            new CardData { CardID = 1, CardName = "防御壁", CardDiscription = "3*3の正方形で留まる壁HP9", AffectAreaRow = 3,
                bullet = new List<bulletData_Card> { new bulletData_Card { x = 1, y = 1, nextX = 1, nextY = 1, HP = 9 } } },
            new CardData { CardID = 2, CardName = "貫通弾", CardDiscription = "一発のみ、HP9で飛翔する貫通弾", AffectAreaRow = 3,
                bullet = new List<bulletData_Card> { new bulletData_Card { x = 1, y = 0, nextX = 1, nextY = 1, HP = 9 } } }
        };
    }
}
